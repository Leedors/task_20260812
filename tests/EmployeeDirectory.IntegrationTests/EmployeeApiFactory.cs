using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.IntegrationTests;

/// <summary>
/// 실제 파이프라인(라우팅 → 컨트롤러 → CQRS → EF Core/SQLite)을 그대로 띄우는 테스트 호스트.
/// </summary>
/// <remarks>
/// <para>DI 를 뜯어고치는 대신 <b>설정만 덮어써서</b> 테스트 전용 SQLite 파일을 쓰게 했다.
/// 프로덕션과 동일한 등록 경로를 그대로 통과하므로, DI 구성 자체의 실수도 테스트가 잡아낸다.</para>
/// <para>테스트 클래스마다 별도 DB 파일을 쓰므로 병렬 실행에도 서로 간섭하지 않는다.</para>
/// </remarks>
public sealed class EmployeeApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"employee-directory-tests-{Guid.NewGuid():n}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                // 시드가 켜져 있으면 테스트가 자기 데이터만 다루지 못한다.
                ["Seed:Enabled"] = "false"
            }));

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // 임시 파일 정리 실패는 테스트 결과에 영향을 주지 않는다.
        }
    }
}
