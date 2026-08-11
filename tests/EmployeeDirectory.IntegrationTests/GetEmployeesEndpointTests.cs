using System.Net;
using System.Text;

namespace EmployeeDirectory.IntegrationTests;

public sealed class GetEmployeesEndpointTests(EmployeeApiFactory factory)
    : IClassFixture<EmployeeApiFactory>, IAsyncLifetime
{
    private const int SeedCount = 25;

    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        var csv = new StringBuilder();
        for (var i = 1; i <= SeedCount; i++)
        {
            csv.AppendLine($"직원{i:00}, member{i:00}@clovf.com, 010-0000-{i:0000}, 2020.01.01");
        }

        var response = await _client.PostRawAsync(csv.ToString(), "text/csv");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task 페이지와_전체_건수를_함께_반환한다()
    {
        var response = await _client.GetAsync("/api/employee?page=2&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsync<PagedResponse>();
        body.Items.Should().HaveCount(10);
        body.Page.Should().Be(2);
        body.PageSize.Should().Be(10);
        body.TotalCount.Should().Be(SeedCount);
        body.TotalPages.Should().Be(3);
        body.HasPreviousPage.Should().BeTrue();
        body.HasNextPage.Should().BeTrue();
        body.Items[0].Name.Should().Be("직원11");
    }

    [Fact]
    public async Task 파라미터를_생략하면_기본값으로_조회한다()
    {
        var body = await (await _client.GetAsync("/api/employee")).ReadAsync<PagedResponse>();

        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
        body.Items.Should().HaveCount(20);
    }

    [Fact]
    public async Task 마지막_페이지에는_다음_페이지가_없다()
    {
        var body = await (await _client.GetAsync("/api/employee?page=3&pageSize=10")).ReadAsync<PagedResponse>();

        body.Items.Should().HaveCount(5);
        body.HasNextPage.Should().BeFalse();
        body.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task 범위를_벗어난_페이지는_빈_목록과_전체_건수를_반환한다()
    {
        var body = await (await _client.GetAsync("/api/employee?page=999&pageSize=10")).ReadAsync<PagedResponse>();

        body.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(SeedCount);
    }

    [Theory]
    [InlineData("?page=0&pageSize=10", "paging.page_invalid")]
    [InlineData("?page=1&pageSize=0", "paging.page_size_invalid")]
    [InlineData("?page=1&pageSize=100000", "paging.page_size_too_large")]
    public async Task 페이징_파라미터가_잘못되면_400을_반환한다(string query, string expectedCode)
    {
        var response = await _client.GetAsync($"/api/employee{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.ReadAsync<ProblemResponse>();
        problem.Errors!.Should().Contain(error => error.Code == expectedCode);
    }

    [Fact]
    public async Task 전화번호는_하이픈_표기로_내려온다()
    {
        var body = await (await _client.GetAsync("/api/employee?page=1&pageSize=1")).ReadAsync<PagedResponse>();

        body.Items[0].Tel.Should().Be("010-0000-0001");
        body.Items[0].Joined.Should().Be(new DateOnly(2020, 1, 1));
    }

    [Fact]
    public async Task 등록_시각과_수정_시각이_함께_내려온다()
    {
        var body = await (await _client.GetAsync("/api/employee?page=1&pageSize=1")).ReadAsync<PagedResponse>();
        var employee = body.Items[0];

        employee.CreatedAt.Should().NotBe(default);
        // 이 클래스의 InitializeAsync 는 테스트마다 실행되고 같은 이메일이라 upsert 된다.
        // 즉 등록 시각은 최초 값이 유지되고 수정 시각만 앞으로 간다.
        employee.UpdatedAt.Should().BeOnOrAfter(employee.CreatedAt);
    }
}

public sealed class GetEmployeeByNameEndpointTests(EmployeeApiFactory factory)
    : IClassFixture<EmployeeApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        const string Csv = "김철수, charles@clovf.com, 01075312468, 2018.03.07";

        var response = await _client.PostRawAsync(Csv, "text/csv");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task 이름으로_상세를_조회한다()
    {
        var response = await _client.GetAsync("/api/employee/김철수");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsync<EmployeeResponse>();
        body.Name.Should().Be("김철수");
        body.Email.Should().Be("charles@clovf.com");
        body.Tel.Should().Be("010-7531-2468");
        body.Joined.Should().Be(new DateOnly(2018, 3, 7));
    }

    [Fact]
    public async Task 없는_이름은_404와_problem_json을_반환한다()
    {
        var response = await _client.GetAsync("/api/employee/없는사람");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.ReadAsync<ProblemResponse>();
        problem.Status.Should().Be(404);
        problem.Errors![0].Code.Should().Be("employee.not_found");
    }

    [Fact]
    public async Task 응답에_상관관계_ID_헤더가_포함된다()
    {
        var response = await _client.GetAsync("/api/employee/김철수");

        response.Headers.Should().ContainKey("X-Correlation-Id");
    }
}
