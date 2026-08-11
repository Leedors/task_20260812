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

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var first = result.Value[0];
        Assert.Equal("김클로", first.Name);
        Assert.Equal("clo@clovf.com", first.Email);
        Assert.Equal("010-1111-2424", first.Tel);
        Assert.Equal("2012-01-05", first.Joined);
        Assert.Equal(1, first.Position);
    }

    [Fact]
    public void 최상위가_객체이면_한_건으로_처리한다()
    {
        const string Json = """{"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05"}""";

        var result = _parser.Parse(new EmployeePayload(Json));

        Assert.True(result.IsSuccess);
        Assert.Equal("김클로", Assert.Single(result.Value).Name);
    }

    [Fact]
    public void 프로퍼티_대소문자를_구분하지_않는다()
    {
        const string Json = """[{"Name":"김클로","EMAIL":"clo@clovf.com","Tel":"010-1111-2424","Joined":"2012-01-05"}]""";

        var result = _parser.Parse(new EmployeePayload(Json));

        Assert.True(result.IsSuccess);
        Assert.Equal("김클로", Assert.Single(result.Value).Name);
    }

    [Fact]
    public void 필수_필드_외의_프로퍼티는_무시한다()
    {
        const string Json = """[{"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05","team":"플랫폼","extra":{"a":1}}]""";

        var result = _parser.Parse(new EmployeePayload(Json));

        Assert.True(result.IsSuccess);
        Assert.Equal("clo@clovf.com", Assert.Single(result.Value).Email);
    }

    [Fact]
    public void 숫자로_표기된_전화번호도_문자열로_읽는다()
    {
        const string Json = """[{"name":"김클로","email":"clo@clovf.com","tel":1011112424,"joined":"2012-01-05"}]""";

        var result = _parser.Parse(new EmployeePayload(Json));

        Assert.True(result.IsSuccess);
        Assert.Equal("1011112424", Assert.Single(result.Value).Tel);
    }

    [Fact]
    public void 누락된_필드는_null로_읽고_검증은_도메인에_맡긴다()
    {
        const string Json = """[{"name":"김클로","joined":"2012-01-05"}]""";

        var result = _parser.Parse(new EmployeePayload(Json));

        Assert.True(result.IsSuccess);
        var record = Assert.Single(result.Value);
        Assert.Null(record.Email);
        Assert.Null(record.Tel);
    }

    [Fact]
    public void 형식이_깨진_json은_실패한다()
    {
        var result = _parser.Parse(new EmployeePayload("""[{"name":"김클로",]"""));

        Assert.True(result.IsFailure);
        Assert.Equal("json.malformed", result.FirstError.Code);
    }

    [Fact]
    public void 배열_요소가_객체가_아니면_위치와_함께_실패한다()
    {
        var result = _parser.Parse(new EmployeePayload("""["김클로", 123]"""));

        Assert.True(result.IsFailure);
        Assert.Equal(2, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal("json.unexpected_element", error.Code));
        Assert.Contains("[1번째 항목]", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 최상위가_배열이나_객체가_아니면_실패한다()
    {
        var result = _parser.Parse(new EmployeePayload("\"문자열\""));

        Assert.True(result.IsFailure);
        Assert.Equal("json.unexpected_root", result.FirstError.Code);
    }

    [Theory]
    [InlineData("[{\"name\":\"x\"}]", true)]
    [InlineData("  {\"name\":\"x\"}", true)]
    [InlineData("김철수, a@b.com, 01011112222, 2020.01.01", false)]
    public void 내용으로_json_여부를_판별한다(string content, bool expected)
        => Assert.Equal(expected, _parser.CanParse(content));
}
