using System.Net;

namespace EmployeeDirectory.IntegrationTests;

/// <summary>
/// 과제 필수 조건인 네 가지 입력 경로를 모두 실제 HTTP 요청으로 검증한다.
/// </summary>
public sealed class RegisterEmployeesEndpointTests(EmployeeApiFactory factory) : IClassFixture<EmployeeApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task csv_파일_업로드시_201을_반환한다()
    {
        const string Csv = """
                           파일씨에스브이, file-csv-1@clovf.com, 01011110001, 2018.03.07
                           파일씨에스브이2, file-csv-2@clovf.com, 01011110002, 2021.04.28
                           """;

        var response = await _client.PostFileAsync(Csv, "employees.csv", "text/csv");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsync<RegisterResponse>();
        body.Format.Should().Be("csv");
        body.Created.Should().Be(2);
        body.Updated.Should().Be(0);

        // 실제로 조회까지 되어야 "동작"이라고 할 수 있다.
        var detail = await _client.GetAsync("/api/employee/파일씨에스브이");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.ReadAsync<EmployeeResponse>()).Tel.Should().Be("010-1111-0001");
    }

    [Fact]
    public async Task json_파일_업로드시_201을_반환한다()
    {
        const string Json = """
                            [
                              {"name":"파일제이슨","email":"file-json-1@clovf.com","tel":"010-2222-0001","joined":"2012-01-05"}
                            ]
                            """;

        var response = await _client.PostFileAsync(Json, "employees.json", "application/json");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsync<RegisterResponse>();
        body.Format.Should().Be("json");
        body.Created.Should().Be(1);
    }

    [Fact]
    public async Task body에_csv를_직접_입력해도_201을_반환한다()
    {
        const string Csv = "본문씨에스브이, body-csv-1@clovf.com, 010-3333-0001, 2015.08.15";

        var response = await _client.PostRawAsync(Csv, "text/csv");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadAsync<RegisterResponse>()).Created.Should().Be(1);
    }

    [Fact]
    public async Task body에_json을_직접_입력해도_201을_반환한다()
    {
        const string Json = """[{"name":"본문제이슨","email":"body-json-1@clovf.com","tel":"010-4444-0001","joined":"2019-12-05"}]""";

        var response = await _client.PostRawAsync(Json, "application/json");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadAsync<RegisterResponse>()).Created.Should().Be(1);
    }

    [Fact]
    public async Task ContentType이_text_plain이어도_내용으로_형식을_추론한다()
    {
        const string Json = """[{"name":"추론제이슨","email":"sniff-json@clovf.com","tel":"010-5555-0001","joined":"2019-12-05"}]""";

        var response = await _client.PostRawAsync(Json, "text/plain");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadAsync<RegisterResponse>()).Format.Should().Be("json");
    }

    [Fact]
    public async Task 같은_이메일을_다시_올리면_신규가_아니라_갱신된다()
    {
        const string First = "중복확인, upsert@clovf.com, 010-6666-0001, 2018.03.07";
        const string Second = "중복확인변경, upsert@clovf.com, 010-6666-9999, 2018.03.07";

        var created = await _client.PostRawAsync(First, "text/csv");
        (await created.ReadAsync<RegisterResponse>()).Created.Should().Be(1);

        var updated = await _client.PostRawAsync(Second, "text/csv");
        var body = await updated.ReadAsync<RegisterResponse>();

        updated.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Created.Should().Be(0);
        body.Updated.Should().Be(1);

        var detail = await (await _client.GetAsync("/api/employee/중복확인변경")).ReadAsync<EmployeeResponse>();
        detail.Tel.Should().Be("010-6666-9999");
    }

    [Fact]
    public async Task 유효하지_않은_행이_있으면_400과_함께_아무것도_저장하지_않는다()
    {
        const string Csv = """
                           정상직원, rollback-ok@clovf.com, 010-7777-0001, 2018.03.07
                           잘못된직원, 이메일아님, 010-7777-0002, 2021.04.28
                           """;

        var response = await _client.PostRawAsync(Csv, "text/csv");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.ReadAsync<ProblemResponse>();
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Should().Contain(error => error.Code == "employee.email_invalid");

        // 같은 요청의 정상 행도 저장되지 않아야 한다(부분 성공 금지).
        var detail = await _client.GetAsync("/api/employee/정상직원");
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task 본문이_비어_있으면_400을_반환한다()
    {
        var response = await _client.PostRawAsync("   ", "text/csv");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ProblemResponse>()).Errors![0].Code.Should().Be("payload.empty");
    }

    [Fact]
    public async Task 형식이_깨진_json은_400을_반환한다()
    {
        var response = await _client.PostRawAsync("""[{"name":"깨짐",]""", "application/json");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ProblemResponse>()).Errors![0].Code.Should().Be("json.malformed");
    }
}
