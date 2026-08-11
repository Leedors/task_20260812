using EmployeeDirectory.Application.Abstractions.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EmployeeDirectory.Api.Diagnostics;

/// <summary>
/// 저장소 연결 상태를 확인한다.
/// </summary>
/// <remarks>
/// 긴급 연락망은 <b>정작 비상시에 살아 있어야</b> 하는 시스템이라 가용성 확인 경로가 특히 중요하다.
/// 프로세스가 떠 있는 것만으로는 부족하고(DB 파일 권한 문제 등으로 조회가 전부 실패할 수 있다)
/// 실제로 저장소에 닿는지까지 확인해야 의미가 있다.
/// </remarks>
internal sealed class DatabaseHealthCheck(IDatabaseProbe databaseProbe, ILogger<DatabaseHealthCheck> logger)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await databaseProbe.CanConnectAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Healthy("데이터베이스에 연결할 수 있습니다.")
                : HealthCheckResult.Unhealthy("데이터베이스에 연결할 수 없습니다.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "헬스체크 중 데이터베이스 연결 확인에 실패했습니다.");
            return HealthCheckResult.Unhealthy("데이터베이스 연결 확인 중 오류가 발생했습니다.", ex);
        }
    }
}
