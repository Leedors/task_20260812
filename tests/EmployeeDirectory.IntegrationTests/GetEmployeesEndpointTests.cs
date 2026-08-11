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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task 페이지와_전체_건수를_함께_반환한다()
    {
        var response = await _client.GetAsync("/api/employee?page=2&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.ReadAsync<PagedResponse>();
        Assert.Equal(10, body.Items.Count);
        Assert.Equal(2, body.Page);
        Assert.Equal(10, body.PageSize);
        Assert.Equal(SeedCount, body.TotalCount);
        Assert.Equal(3, body.TotalPages);
        Assert.True(body.HasPreviousPage);
        Assert.True(body.HasNextPage);
        Assert.Equal("직원11", body.Items[0].Name);
    }

    [Fact]
    public async Task 파라미터를_생략하면_기본값으로_조회한다()
    {
        var body = await (await _client.GetAsync("/api/employee")).ReadAsync<PagedResponse>();

        Assert.Equal(1, body.Page);
        Assert.Equal(20, body.PageSize);
        Assert.Equal(20, body.Items.Count);
    }

    [Fact]
    public async Task 마지막_페이지에는_다음_페이지가_없다()
    {
        var body = await (await _client.GetAsync("/api/employee?page=3&pageSize=10")).ReadAsync<PagedResponse>();

        Assert.Equal(5, body.Items.Count);
        Assert.False(body.HasNextPage);
        Assert.True(body.HasPreviousPage);
    }

    [Fact]
    public async Task 범위를_벗어난_페이지는_빈_목록과_전체_건수를_반환한다()
    {
        var body = await (await _client.GetAsync("/api/employee?page=999&pageSize=10")).ReadAsync<PagedResponse>();

        Assert.Empty(body.Items);
        Assert.Equal(SeedCount, body.TotalCount);
    }

    [Theory]
    [InlineData("?page=0&pageSize=10", "paging.page_invalid")]
    [InlineData("?page=1&pageSize=0", "paging.page_size_invalid")]
    [InlineData("?page=1&pageSize=100000", "paging.page_size_too_large")]
    public async Task 페이징_파라미터가_잘못되면_400을_반환한다(string query, string expectedCode)
    {
        var response = await _client.GetAsync($"/api/employee{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<ProblemResponse>();
        Assert.Contains(problem.Errors!, error => error.Code == expectedCode);
    }

    [Fact]
    public async Task 전화번호는_하이픈_표기로_내려온다()
    {
        var body = await (await _client.GetAsync("/api/employee?page=1&pageSize=1")).ReadAsync<PagedResponse>();

        Assert.Equal("010-0000-0001", body.Items[0].Tel);
        Assert.Equal(new DateOnly(2020, 1, 1), body.Items[0].Joined);
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task 이름으로_상세를_조회한다()
    {
        var response = await _client.GetAsync("/api/employee/김철수");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.ReadAsync<EmployeeResponse>();
        Assert.Equal("김철수", body.Name);
        Assert.Equal("charles@clovf.com", body.Email);
        Assert.Equal("010-7531-2468", body.Tel);
        Assert.Equal(new DateOnly(2018, 3, 7), body.Joined);
    }

    [Fact]
    public async Task 없는_이름은_404와_problem_json을_반환한다()
    {
        var response = await _client.GetAsync("/api/employee/없는사람");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.ReadAsync<ProblemResponse>();
        Assert.Equal(404, problem.Status);
        Assert.Equal("employee.not_found", problem.Errors![0].Code);
    }

    [Fact]
    public async Task 응답에_상관관계_ID_헤더가_포함된다()
    {
        var response = await _client.GetAsync("/api/employee/김철수");

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }
}
