using System.Net;

namespace EmployeeDirectory.IntegrationTests;

/// <summary>
/// 검색은 실제 SQL 로 번역돼야 의미가 있어(LIKE 이스케이프, 대소문자, 전화번호 정규화)
/// 단위 테스트가 아닌 통합 테스트로 검증한다.
/// </summary>
public sealed class SearchEndpointTests(EmployeeApiFactory factory)
    : IClassFixture<EmployeeApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        const string Csv = """
                           김수색, search.kim@clovf.com, 01075312468, 2018.03.07
                           박수색, search.park@clovf.com, 01087654321, 2021.04.28
                           이검색, LOOKUP.Lee@clovf.com, 01012345678, 2015.08.15
                           특수문자, percent_100@clovf.com, 01099998888, 2020.01.01
                           """;

        var response = await _client.PostRawAsync(Csv, "text/csv");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task 이름_부분_일치로_찾는다()
    {
        var body = await (await _client.GetAsync("/api/employee?q=수색")).ReadAsync<PagedResponse>();

        Assert.Equal(2, body.TotalCount);
        Assert.All(body.Items, item => Assert.Contains("수색", item.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task 이메일_부분_일치는_대소문자를_구분하지_않는다()
    {
        var body = await (await _client.GetAsync("/api/employee?q=lookup")).ReadAsync<PagedResponse>();

        Assert.Equal("이검색", Assert.Single(body.Items).Name);
    }

    [Fact]
    public async Task 하이픈을_넣어_검색해도_전화번호가_매칭된다()
    {
        // 저장 값은 01075312468 이지만 사용자는 하이픈을 넣어 검색한다.
        var body = await (await _client.GetAsync("/api/employee?q=010-7531")).ReadAsync<PagedResponse>();

        Assert.Equal("김수색", Assert.Single(body.Items).Name);
    }

    [Fact]
    public async Task LIKE_와일드카드는_글자로_취급한다()
    {
        // 이스케이프를 빠뜨리면 '%' 한 글자가 전체 행을 반환한다.
        var wildcard = await (await _client.GetAsync("/api/employee?q=%25")).ReadAsync<PagedResponse>();
        Assert.Equal(0, wildcard.TotalCount);

        // 언더스코어도 마찬가지로 리터럴이어야 한다.
        var underscore = await (await _client.GetAsync("/api/employee?q=percent_100")).ReadAsync<PagedResponse>();
        Assert.Equal("특수문자", Assert.Single(underscore.Items).Name);
    }

    [Fact]
    public async Task 검색_결과에도_페이징_정보가_그대로_적용된다()
    {
        var body = await (await _client.GetAsync("/api/employee?q=수색&page=1&pageSize=1")).ReadAsync<PagedResponse>();

        Assert.Single(body.Items);
        Assert.Equal(2, body.TotalCount);
        Assert.Equal(2, body.TotalPages);
        Assert.True(body.HasNextPage);
    }

    [Fact]
    public async Task 일치하는_결과가_없으면_빈_목록을_반환한다()
    {
        var body = await (await _client.GetAsync("/api/employee?q=존재하지않는이름")).ReadAsync<PagedResponse>();

        Assert.Empty(body.Items);
        Assert.Equal(0, body.TotalCount);
    }

    [Fact]
    public async Task 검색어가_너무_길면_400이다()
    {
        var tooLong = new string('가', 101);

        var response = await _client.GetAsync($"/api/employee?q={Uri.EscapeDataString(tooLong)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("search.keyword_too_long", (await response.ReadAsync<ProblemResponse>()).Errors![0].Code);
    }

    [Fact]
    public async Task 검색어가_없으면_전체를_반환한다()
    {
        var body = await (await _client.GetAsync("/api/employee")).ReadAsync<PagedResponse>();

        Assert.Equal(4, body.TotalCount);
    }
}

public sealed class HealthEndpointTests(EmployeeApiFactory factory) : IClassFixture<EmployeeApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task 헬스체크는_저장소_연결까지_확인한다()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.ReadAsync<HealthResponse>();
        Assert.Equal("Healthy", body.Status);

        var database = Assert.Single(body.Checks);
        Assert.Equal("database", database.Name);
        Assert.Equal("Healthy", database.Status);
    }

    [Fact]
    public async Task 헬스체크는_API_문서에_노출되지_않는다()
    {
        // 운영 확인용 경로라 공개 API 스펙에 섞이면 안 된다.
        var swagger = await _client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.DoesNotContain("\"/health\"", swagger, StringComparison.Ordinal);
    }
}
