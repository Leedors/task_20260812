using System.Text;
using EmployeeDirectory.Api.Common;
using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Application.Employees.Commands.RegisterEmployees;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Application.Employees.Queries.GetEmployeeByName;
using EmployeeDirectory.Application.Employees.Queries.GetEmployees;
using EmployeeDirectory.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeDirectory.Api.Controllers;

/// <summary>직원 긴급 연락망 API.</summary>
// [Produces("application/json")] 를 붙이지 않는다. 실패 응답은 RFC 9457 의 application/problem+json 으로
// 내려가야 하는데, 클래스 레벨 Produces 가 그 Content-Type 을 application/json 으로 덮어쓰기 때문이다.
[ApiController]
[Route("api/employee")]
public sealed class EmployeeController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    /// <summary>업로드 파일이 아닌 폼 필드로 텍스트를 보낼 때 인식하는 필드명들.</summary>
    private static readonly string[] TextFieldNames = ["content", "data", "text", "payload", "body"];

    /// <summary>직원 목록을 페이지 단위로 조회합니다.</summary>
    /// <param name="page">1부터 시작하는 페이지 번호.</param>
    /// <param name="pageSize">페이지 크기(최대 200).</param>
    /// <param name="cancellationToken">취소 토큰.</param>
    /// <response code="200">페이지 항목과 전체 건수를 함께 반환합니다.</response>
    /// <response code="400">페이징 파라미터가 올바르지 않습니다.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetEmployees(
        [FromQuery] int page = GetEmployeesQuery.DefaultPage,
        [FromQuery] int pageSize = GetEmployeesQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetEmployeesQuery(page, pageSize), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.Problem(result, HttpContext);
    }

    /// <summary>이름으로 직원 한 명의 상세 연락처를 조회합니다.</summary>
    /// <param name="name">직원 이름(정확히 일치).</param>
    /// <param name="cancellationToken">취소 토큰.</param>
    /// <response code="200">직원 상세 정보.</response>
    /// <response code="404">해당 이름의 직원이 없습니다.</response>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetEmployeeByName(
        string name,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetEmployeeByNameQuery(name), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.Problem(result, HttpContext);
    }

    /// <summary>csv 또는 json 으로 직원 연락처를 등록합니다.</summary>
    /// <remarks>
    /// 다음 네 가지 입력을 모두 지원합니다.
    /// <list type="number">
    ///   <item><c>multipart/form-data</c> 로 csv 파일 업로드 (<c>&lt;input type="file"&gt;</c>)</item>
    ///   <item><c>multipart/form-data</c> 로 json 파일 업로드</item>
    ///   <item>요청 본문에 csv 텍스트 직접 입력 (<c>&lt;textarea&gt;</c>, Content-Type: text/csv)</item>
    ///   <item>요청 본문에 json 텍스트 직접 입력 (<c>&lt;textarea&gt;</c>, Content-Type: application/json)</item>
    /// </list>
    /// Content-Type 이 불명확해도(text/plain 등) 본문 내용으로 형식을 추론합니다.
    /// 한 건이라도 유효하지 않으면 아무것도 저장하지 않고 400 과 함께 실패 항목을 모두 알려줍니다.
    /// </remarks>
    /// <response code="201">등록 성공. 신규/갱신 건수를 반환합니다.</response>
    /// <response code="400">형식 오류 또는 데이터 검증 실패.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RegisterEmployeesResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterEmployeesResult>> RegisterEmployees(CancellationToken cancellationToken)
    {
        var payload = await ReadPayloadAsync(cancellationToken);
        if (payload.IsFailure)
        {
            return ApiResults.Problem(payload, HttpContext);
        }

        var result = await commandDispatcher.SendAsync(
            new RegisterEmployeesCommand(payload.Value),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiResults.Problem(result, HttpContext);
        }

        return CreatedAtAction(
            nameof(GetEmployees),
            new { page = GetEmployeesQuery.DefaultPage, pageSize = GetEmployeesQuery.DefaultPageSize },
            result.Value);
    }

    /// <summary>
    /// "어떻게 도착했는가"(파일/본문)를 흡수해 Application 이 다루는 페이로드 하나로 정규화한다.
    /// </summary>
    private async Task<Result<EmployeePayload>> ReadPayloadAsync(CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            return await ReadFromFormAsync(cancellationToken);
        }

        var body = await ReadAllTextAsync(Request.Body, cancellationToken);

        return string.IsNullOrWhiteSpace(body)
            ? Result.Failure<EmployeePayload>(
                Error.Validation("payload.empty", "요청 본문이 비어 있습니다. csv 또는 json 데이터를 전달해 주세요."))
            : Result.Success(new EmployeePayload(
                body,
                SourceFormatHint.FromContentType(Request.ContentType),
                SourceName: "(request body)"));
    }

    private async Task<Result<EmployeePayload>> ReadFromFormAsync(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken);

        // 필드명은 관례상 "file" 을 우선하되, Front-end 구현에 따라 달라질 수 있어 첫 번째 파일도 받아들인다.
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

        if (file is not null)
        {
            if (file.Length == 0)
            {
                return Result.Failure<EmployeePayload>(
                    Error.Validation("payload.file_empty", $"업로드한 파일이 비어 있습니다: {file.FileName}"));
            }

            await using var stream = file.OpenReadStream();
            var content = await ReadAllTextAsync(stream, cancellationToken);

            return Result.Success(new EmployeePayload(
                content,
                SourceFormatHint.FromFileName(file.FileName) ?? SourceFormatHint.FromContentType(file.ContentType),
                file.FileName));
        }

        // 파일 없이 폼 필드로 텍스트만 보낸 경우도 지원한다.
        foreach (var fieldName in TextFieldNames)
        {
            if (form.TryGetValue(fieldName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return Result.Success(new EmployeePayload(value.ToString(), DeclaredFormat: null, SourceName: fieldName));
            }
        }

        return Result.Failure<EmployeePayload>(
            Error.Validation(
                "payload.missing",
                "업로드된 파일이나 텍스트 필드를 찾지 못했습니다. 'file' 필드로 파일을 보내거나 'content' 필드에 텍스트를 넣어주세요."));
    }

    /// <summary>BOM 이 있으면 인코딩을 따르고, 없으면 UTF-8 로 읽는다.</summary>
    private static async Task<string> ReadAllTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
