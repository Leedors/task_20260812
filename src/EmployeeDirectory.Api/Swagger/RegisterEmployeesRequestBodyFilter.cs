using EmployeeDirectory.Api.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EmployeeDirectory.Api.Swagger;

/// <summary>
/// <see cref="EmployeeController.RegisterEmployees"/> 의 요청 본문을 OpenAPI 문서에 직접 기술한다.
/// </summary>
/// <remarks>
/// 이 엔드포인트는 파일 업로드와 원시 텍스트를 모두 받기 위해 모델 바인딩을 쓰지 않는다.
/// 그래서 Swashbuckle 이 시그니처만 보고는 본문 스키마를 만들 수 없어, 여기서 명시한다.
/// 덕분에 리뷰어가 Swagger UI 에서 네 가지 입력 방식을 그대로 시험해 볼 수 있다.
/// </remarks>
internal sealed class RegisterEmployeesRequestBodyFilter : IOperationFilter
{
    private const string CsvExample = """
                                      홍길동, gildong@example.com, 01075312468, 2018.03.07
                                      성춘향, chunhyang@example.com, 01087654321, 2021.04.28
                                      임꺽정, kkeokjeong@example.com, 01012345678, 2015.08.15
                                      """;

    private const string JsonExample = """
                                       [
                                         {"name":"홍길동", "email":"gildong@example.com", "tel":"010-1111-2424", "joined":"2012-01-05"},
                                         {"name":"성춘향", "email":"chunhyang@example.com", "tel":"010-3535-7979", "joined":"2013-07-01"},
                                         {"name":"임꺽정", "email":"kkeokjeong@example.com", "tel":"010-8531-7942", "joined":"2019-12-05"}
                                       ]
                                       """;

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.Name != nameof(EmployeeController.RegisterEmployees))
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "csv 또는 json. 파일 업로드(multipart/form-data) 와 본문 직접 입력을 모두 지원합니다.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["file"] = new()
                            {
                                Type = "string",
                                Format = "binary",
                                Description = "csv 또는 json 파일 (<input type=\"file\">)"
                            },
                            ["content"] = new()
                            {
                                Type = "string",
                                Description = "파일 대신 텍스트를 직접 보낼 때 사용 (<textarea>)"
                            }
                        }
                    }
                },
                ["text/csv"] = new()
                {
                    Schema = new OpenApiSchema { Type = "string" },
                    Example = new OpenApiString(CsvExample)
                },
                ["application/json"] = new()
                {
                    Schema = BuildJsonSchema(),
                    Example = OpenApiAnyFactory.CreateFromJson(JsonExample)
                },
                ["text/plain"] = new()
                {
                    Schema = new OpenApiSchema { Type = "string" },
                    Example = new OpenApiString(CsvExample)
                }
            }
        };
    }

    private static OpenApiSchema BuildJsonSchema() => new()
    {
        Type = "array",
        Items = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "name", "email", "tel", "joined" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["name"] = new() { Type = "string", Example = new OpenApiString("홍길동") },
                ["email"] = new() { Type = "string", Example = new OpenApiString("gildong@example.com") },
                ["tel"] = new() { Type = "string", Example = new OpenApiString("010-1111-2424") },
                ["joined"] = new() { Type = "string", Example = new OpenApiString("2012-01-05") }
            }
        }
    };
}
