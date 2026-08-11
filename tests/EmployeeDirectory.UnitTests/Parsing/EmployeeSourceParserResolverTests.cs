using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Infrastructure.Parsing;

namespace EmployeeDirectory.UnitTests.Parsing;

public sealed class EmployeeSourceParserResolverTests
{
    // 실제 애플리케이션과 같은 등록 순서(json 우선, csv 폴백)를 사용한다.
    private readonly EmployeeSourceParserResolver _resolver =
        new([new JsonEmployeeParser(), new CsvEmployeeParser()]);

    [Fact]
    public void 선언된_형식이_있으면_그대로_사용한다()
    {
        // 내용은 csv 처럼 보이지만 json 으로 선언된 경우 → 선언을 신뢰하고 json 파서를 고른다.
        var result = _resolver.Resolve(new EmployeePayload("a,b,c,d", EmployeeSourceFormat.Json));

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeSourceFormat.Json, result.Value.Format);
    }

    [Fact]
    public void 선언이_없으면_내용으로_json을_판별한다()
    {
        var result = _resolver.Resolve(new EmployeePayload("""[{"name":"김클로"}]"""));

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeSourceFormat.Json, result.Value.Format);
    }

    [Fact]
    public void 선언이_없고_json이_아니면_csv로_판별한다()
    {
        var result = _resolver.Resolve(new EmployeePayload("김철수, charles@clovf.com, 01075312468, 2018.03.07"));

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeSourceFormat.Csv, result.Value.Format);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void 본문이_비어_있으면_실패한다(string content)
    {
        var result = _resolver.Resolve(new EmployeePayload(content));

        Assert.True(result.IsFailure);
        Assert.Equal("payload.empty", result.FirstError.Code);
    }

    [Fact]
    public void 해당_형식_파서가_등록되지_않았으면_실패한다()
    {
        var resolver = new EmployeeSourceParserResolver([new CsvEmployeeParser()]);

        var result = resolver.Resolve(new EmployeePayload("{}", EmployeeSourceFormat.Json));

        Assert.True(result.IsFailure);
        Assert.Equal("payload.format_unsupported", result.FirstError.Code);
    }
}
