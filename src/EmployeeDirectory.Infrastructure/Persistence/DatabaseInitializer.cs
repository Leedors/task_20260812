using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Application.Employees.Commands.RegisterEmployees;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmployeeDirectory.Infrastructure.Persistence;

/// <summary>스키마 생성과(옵션) 초기 데이터 적재.</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

/// <summary>시드 데이터 설정.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>비어 있는 DB 에 <see cref="Directory"/> 의 샘플 파일을 적재할지 여부.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>상대 경로면 실행 파일 기준으로 해석한다.</summary>
    public string Directory { get; set; } = "samples";
}

/// <remarks>
/// <para>EF Migrations 대신 <c>EnsureCreated</c> 를 쓴 이유: 과제 조건이
/// "git clone 후 build 성공 및 결과 확인 가능"이라 마이그레이션 도구 설치나 별도 명령 없이
/// 실행 즉시 동작해야 하기 때문이다. 스키마 이력 관리가 필요한 실제 운영에서는
/// <c>Migrate()</c> 로 바꾸는 것이 맞다(교체 지점이 이 클래스 한 곳으로 고립되어 있다).</para>
/// <para>시드는 별도 코드 경로를 만들지 않고 <see cref="RegisterEmployeesCommand"/> 를 그대로 재사용한다.
/// 시드 경로에서만 통하는 "특별한 검증"이 생기지 않게 하기 위해서다.</para>
/// </remarks>
internal sealed class DatabaseInitializer(
    ApplicationDbContext dbContext,
    ICommandDispatcher commandDispatcher,
    IOptions<SeedOptions> options,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        if (!options.Value.Enabled)
        {
            return;
        }

        if (await dbContext.Employees.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation("이미 데이터가 존재하여 시드를 건너뜁니다.");
            return;
        }

        var directory = ResolveDirectory(options.Value.Directory);
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("시드 디렉터리를 찾을 수 없습니다: {Directory}", directory);
            return;
        }

        var files = Directory
            .EnumerateFiles(directory)
            .Where(file => SourceFormatHint.FromFileName(file) is not null)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            var payload = new EmployeePayload(content, SourceFormatHint.FromFileName(file), Path.GetFileName(file));

            var result = await commandDispatcher
                .SendAsync(new RegisterEmployeesCommand(payload), cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "시드 적재 완료: {File} (신규 {Created}건, 갱신 {Updated}건)",
                    Path.GetFileName(file),
                    result.Value.Created,
                    result.Value.Updated);
            }
            else
            {
                // 시드 실패로 애플리케이션 기동을 막지는 않는다. API 자체는 정상 동작해야 하기 때문이다.
                logger.LogWarning(
                    "시드 적재 실패: {File} - {Errors}",
                    Path.GetFileName(file),
                    string.Join(" | ", result.Errors.Take(5)));
            }
        }
    }

    private static string ResolveDirectory(string configured)
        => Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);
}
