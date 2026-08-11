using System.Text;
using EmployeeDirectory.Api.Common;
using EmployeeDirectory.Api.Contracts;
using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Application.Employees.Commands.DeleteEmployee;
using EmployeeDirectory.Application.Employees.Commands.RegisterEmployees;
using EmployeeDirectory.Application.Employees.Commands.UpdateEmployee;
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

    /// <summary>직원 목록을 페이지 단위로 조회합니다. 검색어를 주면 부분 일치로 좁힙니다.</summary>
    /// <param name="page">1부터 시작하는 페이지 번호.</param>
    /// <param name="pageSize">페이지 크기(최대 200).</param>
    /// <param name="q">
    /// 검색어. 이름·이메일·전화번호에 대해 부분 일치로 찾습니다.
    /// 전화번호는 하이픈을 빼고 비교하므로 <c>010-7531</c> 로도 검색됩니다.
    /// </param>
    /// <param name="cancellationToken">취소 토큰.</param>
    /// <response code="200">페이지 항목과 전체 건수를 함께 반환합니다.</response>
    /// <response code="400">페이징 파라미터 또는 검색어가 올바르지 않습니다.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetEmployees(
        [FromQuery] int page = GetEmployeesQuery.DefaultPage,
        [FromQuery] int pageSize = GetEmployeesQuery.DefaultPageSize,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.QueryAsync<GetEmployeesQuery, PagedResult<EmployeeDto>>(
            new GetEmployeesQuery(page, pageSize, q),
            cancellationToken);

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
        var result = await queryDispatcher.QueryAsync<GetEmployeeByNameQuery, EmployeeDto>(
            new GetEmployeeByNameQuery(name),
            cancellationToken);

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

        var result = await commandDispatcher.SendAsync<RegisterEmployeesCommand, RegisterEmployeesResult>(
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

    /// <summary>직원 한 명의 연락처를 수정합니다(전체 교체).</summary>
    /// <remarks>
    /// 일괄 업로드로도 갱신할 수 있지만, 전화번호 한 자리를 고치려고 파일 전체를 다시 올리는 것은
    /// 실제 운용에서 부담이 큽니다. 연락처는 개별 정정이 잦은 데이터라 단건 수정 경로를 제공합니다.
    /// </remarks>
    /// <param name="id">직원 식별자(목록 응답의 <c>id</c>).</param>
    /// <param name="request">교체할 연락처 전체.</param>
    /// <param name="cancellationToken">취소 토큰.</param>
    /// <response code="200">수정된 직원 정보.</response>
    /// <response code="400">입력값이 올바르지 않습니다.</response>
    /// <response code="404">해당 직원이 없거나 이미 연락망에서 제외되었습니다.</response>
    /// <response code="409">이메일을 다른 직원이 사용 중입니다.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(
        int id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync<UpdateEmployeeCommand, EmployeeDto>(
            new UpdateEmployeeCommand(id, request.Name, request.Email, request.Tel, request.Joined),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.Problem(result, HttpContext);
    }

    /// <summary>직원을 연락망에서 제외합니다.</summary>
    /// <remarks>
    /// 물리 삭제가 아니라 제외 표시(soft delete)입니다. 연락망에서 사람이 빠진 것은
    /// 기록해야 할 사건이고, 오삭제 복구와 감사 추적이 가능해야 하기 때문입니다.
    /// 같은 이메일로 다시 업로드하면 복구됩니다.
    /// </remarks>
    /// <param name="id">직원 식별자.</param>
    /// <param name="cancellationToken">취소 토큰.</param>
    /// <response code="204">제외 완료.</response>
    /// <response code="404">해당 직원이 없거나 이미 제외되었습니다.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmployee(int id, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync<DeleteEmployeeCommand, Unit>(
            new DeleteEmployeeCommand(id),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : ApiResults.Problem(result, HttpContext);
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
