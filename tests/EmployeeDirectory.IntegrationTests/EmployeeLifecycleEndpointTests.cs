using System.Net;
using System.Net.Http.Json;

namespace EmployeeDirectory.IntegrationTests;

/// <summary>
/// 단건 수정/제외(soft delete)와 복구까지의 수명주기를 실제 HTTP 요청으로 검증한다.
/// </summary>
public sealed class EmployeeLifecycleEndpointTests(EmployeeApiFactory factory) : IClassFixture<EmployeeApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<EmployeeResponse> RegisterAsync(string name, string email, string tel = "010-1111-2222")
    {
        var response = await _client.PostRawAsync($"{name}, {email}, {tel}, 2018.03.07", "text/csv");
        response.StatusCode.Should().Be(HttpStatusCode.Created);

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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.ReadAsync<EmployeeResponse>();
        updated.Name.Should().Be("수정완료");
        updated.Tel.Should().Be("010-3333-4444");
        updated.Joined.Should().Be(new DateOnly(2019, 5, 5));

        // 등록 시각은 그대로, 수정 시각만 앞으로 간다.
        updated.CreatedAt.Should().Be(created.CreatedAt);
        updated.UpdatedAt.Should().BeAfter(created.UpdatedAt);
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

        updated.Tel.Should().Be("010-8888-7777");

        // BeOnOrAfter 로 두면 갱신이 아예 안 돼도 통과한다.
        // 이 테스트의 목적은 "owned type 만 바뀌어도 갱신되는가" 이므로 반드시 BeAfter 여야 한다.
        updated.UpdatedAt.Should().BeAfter(created.UpdatedAt);
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadAsync<ProblemResponse>()).Errors![0].Code.Should().Be("employee.not_found");
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

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadAsync<ProblemResponse>()).Errors![0].Code.Should().Be("employee.email_taken");
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

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.ReadAsync<ProblemResponse>();
        problem.Errors!.Should().Contain(error => error.Code == "employee.name_required");
        problem.Errors!.Should().Contain(error => error.Code == "employee.tel_invalid");
    }

    [Fact]
    public async Task 제외하면_204를_반환하고_조회에서_사라진다()
    {
        var created = await RegisterAsync("제외대상", "delete-target@clovf.com");

        var deleted = await _client.DeleteAsync($"/api/employee/{created.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await _client.GetAsync("/api/employee/제외대상");
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await (await _client.GetAsync("/api/employee?page=1&pageSize=200&q=delete-target"))
            .ReadAsync<PagedResponse>();
        list.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task 이미_제외된_직원을_다시_제외하면_404다()
    {
        var created = await RegisterAsync("이중제외", "double-delete@clovf.com");

        await _client.DeleteAsync($"/api/employee/{created.Id}");
        var second = await _client.DeleteAsync($"/api/employee/{created.Id}");

        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task 제외된_직원의_이메일로_다시_업로드하면_복구된다()
    {
        var created = await RegisterAsync("복구대상", "restore-target@clovf.com");
        await _client.DeleteAsync($"/api/employee/{created.Id}");

        var response = await _client.PostRawAsync(
            "복구대상, restore-target@clovf.com, 010-5555-6666, 2018.03.07",
            "text/csv");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsync<RegisterResponse>();
        body.Created.Should().Be(0);
        body.Updated.Should().Be(0);
        body.Restored.Should().Be(1);

        var detail = await (await _client.GetAsync("/api/employee/복구대상")).ReadAsync<EmployeeResponse>();
        detail.Tel.Should().Be("010-5555-6666");
    }
}
