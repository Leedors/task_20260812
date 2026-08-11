using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Infrastructure.Parsing;

namespace EmployeeDirectory.UnitTests.Parsing;

public sealed class JsonEmployeeParserTests
{
    private readonly JsonEmployeeParser _parser = new();

    [Fact]
    public void 과제_예제_json을_그대로_파싱한다()
    {
        const string Json = """
                            [
                            {"name":"김클로", "email":"clo@clovf.com", "tel":"010-1111-2424","joined":"2012-01-05"},
                            {"name":"박마블", "email":"md@clovf.com", "tel":"010-3535-7979","joined":"2013-07-01" },
                            {"name":"홍커넥", "email":"connect@clovf.com","tel":"010-8531-7942","joined":"2019-12-05"}
                            ]
                            """;

        var result = _parser.Parse(new EmployeePayload(Json));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].Should().BeEquivalentTo(
            new EmployeeRecord("김클로", "clo@clovf.com", "010-1111-2424", "2012-01-05", 1));
    }

    [Fact]
    public void 최상위가_객체이면_한_건으로_처리한다()
    {
        const string Json = """{"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05"}""";

        var result = _parser.Parse(new EmployeePayload(Json));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Name.Should().Be("김클로");
    }

    [Fact]
    public void 프로퍼티_대소문자를_구분하지_않는다()
    {
        const string Json = """[{"Name":"김클로","EMAIL":"clo@clovf.com","Tel":"010-1111-2424","Joined":"2012-01-05"}]""";

        var result = _parser.Parse(new EmployeePayload(Json));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Name.Should().Be("김클로");
    }

    [Fact]
    public void 필수_필드_외의_프로퍼티는_무시한다()
    {
        const string Json = """[{"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05","team":"플랫폼","extra":{"a":1}}]""";

        var result = _parser.Parse(new EmployeePayload(Json));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Email.Should().Be("clo@clovf.com");
    }

    [Fact]
    public void 숫자로_표기된_전화번호도_문자열로_읽는다()
    {
        const string Json = """[{"name":"김클로","email":"clo@clovf.com","tel":1011112424,"joined":"2012-01-05"}]""";

        var result = _parser.Parse(new EmployeePayload(Json));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Tel.Should().Be("1011112424");
    }

    [Fact]
    public void 누락된_필드는_null로_읽고_검증은_도메인에_맡긴다()
    {
        const string Json = """[{"name":"김클로","joined":"2012-01-05"}]""";

        var result = _parser.Parse(new EmployeePayload(Json));

        result.IsSuccess.Should().BeTrue();
        var record = result.Value.Should().ContainSingle().Which;
        record.Email.Should().BeNull();
        record.Tel.Should().BeNull();
    }

    [Fact]
    public void 형식이_깨진_json은_실패한다()
    {
        var result = _parser.Parse(new EmployeePayload("""[{"name":"김클로",]"""));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("json.malformed");
    }

    [Fact]
    public void 배열_요소가_객체가_아니면_위치와_함께_실패한다()
    {
        var result = _parser.Parse(new EmployeePayload("""["김클로", 123]"""));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().AllSatisfy(error => error.Code.Should().Be("json.unexpected_element"));
        result.Errors[0].Message.Should().Contain("[1번째 항목]");
    }

    [Fact]
    public void 최상위가_배열이나_객체가_아니면_실패한다()
    {
        var result = _parser.Parse(new EmployeePayload("\"문자열\""));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("json.unexpected_root");
    }

    [Theory]
    [InlineData("[{\"name\":\"x\"}]", true)]
    [InlineData("  {\"name\":\"x\"}", true)]
    [InlineData("김철수, a@b.com, 01011112222, 2020.01.01", false)]
    public void 내용으로_json_여부를_판별한다(string content, bool expected)
        => _parser.CanParse(content).Should().Be(expected);
}
