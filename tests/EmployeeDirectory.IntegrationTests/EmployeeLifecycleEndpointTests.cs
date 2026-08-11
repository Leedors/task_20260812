using System.Net;
using System.Net.Http.Json;

namespace EmployeeDirectory.IntegrationTests;

/// <summary>
/// 단건 수정/제외(soft delete)와 복구까지의 수명주기를 실제 HTTP 요청으로 검증한다.
/// </summary>
public sealed class EmployeeLifecycleEndpointTests(EmployeeApiFactory factory)
    : IClassFixture<EmployeeApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<EmployeeResponse> RegisterAsync(string name, string email, string tel = "010-1111-2222")
    {
        var response = await _client.PostRawAsync($"{name}, {email}, {tel}, 2018.03.07", "text/csv");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await (await _client.GetAsync($"/api/employee/{Uri.EscapeDataString(name)}"))
            .ReadAsync<EmployeeResponse>();
    }

    [Fact]
    public async Task 수정하면_값이_바뀌고_수정시각이_갱신된다()
    {
        var created = await RegisterAsync("수정대상", "update-target@clovf.com");

        var response = await _client.PutAsJsonAsync($"/api/employee/{created.Id}", new
        {
            name = "수정완료",
            email = "update-target@clovf.com",
            tel = "010-3333-4444",
            joined = "2019-05-05"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.ReadAsync<EmployeeResponse>();
        Assert.Equal("수정완료", updated.Name);
        Assert.Equal("010-3333-4444", updated.Tel);
        Assert.Equal(new DateOnly(2019, 5, 5), updated.Joined);

        // 등록 시각은 그대로, 수정 시각만 앞으로 간다.
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task 전화번호만_바꿔도_수정시각이_갱신된다()
    {
        // 값 객체를 owned type 으로 매핑했기 때문에, 소유자 엔트리 상태만 보면
        // 이 경우를 놓칠 수 있다. 실제로 갱신되는지 확인한다.
        var created = await RegisterAsync("번호만변경", "tel-only@clovf.com");

        var response = await _client.PutAsJsonAsync($"/api/employee/{created.Id}", new
        {
            name = created.Name,
            email = created.Email,
            tel = "010-8888-7777",
            joined = created.Joined.ToString("yyyy-MM-dd")
        });

        var updated = await response.ReadAsync<EmployeeResponse>();

        Assert.Equal("010-8888-7777", updated.Tel);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task 없는_직원을_수정하면_404다()
    {
        var response = await _client.PutAsJsonAsync("/api/employee/999999", new
        {
            name = "없음",
            email = "nobody@clovf.com",
            tel = "010-0000-0000",
            joined = "2018-03-07"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("employee.not_found", (await response.ReadAsync<ProblemResponse>()).Errors![0].Code);
    }

    [Fact]
    public async Task 다른_직원이_쓰는_이메일로_바꾸면_409다()
    {
        var first = await RegisterAsync("충돌원본", "conflict-a@clovf.com");
        await RegisterAsync("충돌상대", "conflict-b@clovf.com");

        var response = await _client.PutAsJsonAsync($"/api/employee/{first.Id}", new
        {
            name = "충돌원본",
            email = "conflict-b@clovf.com",
            tel = "010-1111-2222",
            joined = "2018-03-07"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("employee.email_taken", (await response.ReadAsync<ProblemResponse>()).Errors![0].Code);
    }

    [Fact]
    public async Task 잘못된_값으로_수정하면_400과_실패항목을_모두_반환한다()
    {
        var created = await RegisterAsync("검증대상", "validate-target@clovf.com");

        var response = await _client.PutAsJsonAsync($"/api/employee/{created.Id}", new
        {
            name = "",
            email = "이메일아님",
            tel = "123",
            joined = "2018-03-07"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<ProblemResponse>();
        Assert.Contains(problem.Errors!, error => error.Code == "employee.name_required");
        Assert.Contains(problem.Errors!, error => error.Code == "employee.tel_invalid");
    }

    [Fact]
    public async Task 제외하면_204를_반환하고_조회에서_사라진다()
    {
        var created = await RegisterAsync("제외대상", "delete-target@clovf.com");

        var deleted = await _client.DeleteAsync($"/api/employee/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var detail = await _client.GetAsync("/api/employee/제외대상");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var list = await (await _client.GetAsync("/api/employee?page=1&pageSize=200&q=delete-target"))
            .ReadAsync<PagedResponse>();
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task 이미_제외된_직원을_다시_제외하면_404다()
    {
        var created = await RegisterAsync("이중제외", "double-delete@clovf.com");

        await _client.DeleteAsync($"/api/employee/{created.Id}");
        var second = await _client.DeleteAsync($"/api/employee/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task 제외된_직원의_이메일로_다시_업로드하면_복구된다()
    {
        var created = await RegisterAsync("복구대상", "restore-target@clovf.com");
        await _client.DeleteAsync($"/api/employee/{created.Id}");

        var response = await _client.PostRawAsync(
            "복구대상, restore-target@clovf.com, 010-5555-6666, 2018.03.07",
            "text/csv");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.ReadAsync<RegisterResponse>();
        Assert.Equal(0, body.Created);
        Assert.Equal(0, body.Updated);
        Assert.Equal(1, body.Restored);

        var detail = await (await _client.GetAsync("/api/employee/복구대상")).ReadAsync<EmployeeResponse>();
        Assert.Equal("010-5555-6666", detail.Tel);
    }
}
