using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Infrastructure.Parsing;

namespace EmployeeDirectory.UnitTests.Parsing;

public sealed class CsvEmployeeParserTests
{
    private readonly CsvEmployeeParser _parser = new();

    [Fact]
    public void 과제_예제_csv를_그대로_파싱한다()
    {
        const string Csv = """
                           김철수, charles@clovf.com, 01075312468, 2018.03.07
                           박영희, matilda@clovf.com, 01087654321, 2021.04.28
                           홍길동, kildong.hong@clovf.com, 01012345678, 2015.08.15
                           """;

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);

        result.Value[0].Should().BeEquivalentTo(
            new EmployeeRecord("김철수", "charles@clovf.com", "01075312468", "2018.03.07", 1));
    }

    [Fact]
    public void 헤더가_있으면_컬럼명으로_매핑한다()
    {
        const string Csv = """
                           joined,tel,email,name
                           2018.03.07,01075312468,charles@clovf.com,김철수
                           """;

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new EmployeeRecord("김철수", "charles@clovf.com", "01075312468", "2018.03.07", 2));
    }

    [Fact]
    public void 한글_헤더도_인식한다()
    {
        const string Csv = """
                           이름,이메일,전화번호,입사일
                           김철수,charles@clovf.com,01075312468,2018.03.07
                           """;

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Name.Should().Be("김철수");
    }

    [Fact]
    public void 필수_4개_외의_추가_컬럼은_무시한다()
    {
        const string Csv = "김철수, charles@clovf.com, 01075312468, 2018.03.07, 개발팀, 서울";

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Joined.Should().Be("2018.03.07");
    }

    [Fact]
    public void 빈_줄과_주석은_건너뛴다()
    {
        const string Csv = """
                           # 긴급 연락망

                           김철수, charles@clovf.com, 01075312468, 2018.03.07

                           박영희, matilda@clovf.com, 01087654321, 2021.04.28
                           """;

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        // 건너뛴 줄을 포함한 실제 파일 행 번호가 유지되어야 오류 추적이 가능하다.
        result.Value.Select(record => record.Position).Should().Equal(3, 5);
    }

    [Fact]
    public void 따옴표로_감싼_필드_안의_쉼표를_보존한다()
    {
        const string Csv = "\"김, 철수\", charles@clovf.com, 01075312468, 2018.03.07";

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Name.Should().Be("김, 철수");
    }

    [Fact]
    public void 두_개의_따옴표는_따옴표_한_개로_해석한다()
    {
        const string Csv = "\"김\"\"철수\", charles@clovf.com, 01075312468, 2018.03.07";

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Name.Should().Be("김\"철수");
    }

    [Fact]
    public void CRLF_줄바꿈과_BOM을_처리한다()
    {
        const string Csv = "﻿김철수, charles@clovf.com, 01075312468, 2018.03.07\r\n박영희, matilda@clovf.com, 01087654321, 2021.04.28\r\n";

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Name.Should().Be("김철수");
    }

    [Fact]
    public void 컬럼이_모자라면_행_번호와_함께_실패한다()
    {
        const string Csv = """
                           김철수, charles@clovf.com, 01075312468, 2018.03.07
                           박영희, matilda@clovf.com
                           """;

        var result = _parser.Parse(new EmployeePayload(Csv));

        result.IsFailure.Should().BeTrue();
        var error = result.Errors.Should().ContainSingle().Which;
        error.Code.Should().Be("csv.column_missing");
        error.Message.Should().Contain("[2행]");
    }

    [Fact]
    public void 읽을_행이_없으면_실패한다()
    {
        var result = _parser.Parse(new EmployeePayload("\n\n   \n"));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("csv.empty");
    }

    [Fact]
    public void 헤더만_있고_데이터가_없으면_실패한다()
    {
        var result = _parser.Parse(new EmployeePayload("name,email,tel,joined"));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("csv.header_only");
    }
}
