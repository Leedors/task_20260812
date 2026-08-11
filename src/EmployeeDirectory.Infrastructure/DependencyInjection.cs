using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Time;
using EmployeeDirectory.Domain.Employees;
using EmployeeDirectory.Infrastructure.Parsing;
using EmployeeDirectory.Infrastructure.Persistence;
using EmployeeDirectory.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeDirectory.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionStringName = "Default";
    public const string FallbackConnectionString = "Data Source=employee-directory.db";

    /// <summary>영속성·파서·시간 등 바깥 세계와 맞닿는 구현들을 등록한다.</summary>
    /// <param name="services">서비스 컬렉션.</param>
    /// <param name="configuration">연결 문자열과 시드 설정을 읽어올 설정.</param>
    /// <param name="configureDbContext">
    /// 테스트에서 SQLite in-memory 등으로 갈아끼우기 위한 훅. 지정하지 않으면 설정의 연결 문자열을 쓴다.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        // 연결 문자열은 컨테이너가 만들어질 때 IConfiguration 에서 "그때" 읽는다.
        // 등록 시점에 미리 읽어두면 이후에 덧붙는 설정 소스(테스트 오버라이드, 환경 변수 등)가 무시된다.
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            if (configureDbContext is not null)
            {
                configureDbContext(options);
                return;
            }

            var current = provider.GetRequiredService<IConfiguration>();
            options.UseSqlite(current.GetConnectionString(DefaultConnectionStringName) ?? FallbackConnectionString);
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeReadStore, EmployeeReadStore>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IDatabaseProbe, DatabaseProbe>();

        // 등록 순서 = 형식 추론 우선순위. json 은 시작 문자로 정확히 판별되고 csv 가 폴백이다.
        services.AddScoped<IEmployeeSourceParser, JsonEmployeeParser>();
        services.AddScoped<IEmployeeSourceParser, CsvEmployeeParser>();
        services.AddScoped<IEmployeeSourceParserResolver, EmployeeSourceParserResolver>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}
