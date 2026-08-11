using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using EmployeeDirectory.Api.Common;
using EmployeeDirectory.Api.Middleware;
using EmployeeDirectory.Api.Swagger;
using EmployeeDirectory.Application;
using EmployeeDirectory.Infrastructure;
using EmployeeDirectory.Infrastructure.Persistence;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // 열거형은 숫자가 아니라 이름으로 주고받아야 Front-end 가 읽기 쉽다.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Employee Directory API",
        Version = "v1",
        Description = "직원 긴급 연락망 API. csv/json 업로드와 조회·페이징을 제공합니다."
    });

    options.OperationFilter<RegisterEmployeesRequestBodyFilter>();

    // DateOnly 를 date 문자열로 문서화 (기본 스키마는 구조체 내부가 노출된다)
    options.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date", Example = new Microsoft.OpenApi.Any.OpenApiString("2018-03-07") });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

// clone 직후 별도 명령 없이 바로 확인할 수 있도록 스키마 생성과 샘플 시드를 기동 시 수행한다.
await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Directory API v1");
    options.DocumentTitle = "Employee Directory API";
});

app.MapControllers();

// 루트로 들어오면 API 문서로 안내한다.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

/// <summary>통합 테스트에서 <c>WebApplicationFactory&lt;Program&gt;</c> 로 참조하기 위해 노출한다.</summary>
public partial class Program;
