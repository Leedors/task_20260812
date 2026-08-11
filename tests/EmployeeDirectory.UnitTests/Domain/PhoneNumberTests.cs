using EmployeeDirectory.Domain.Employees;

namespace EmployeeDirectory.UnitTests.Domain;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("01075312468", "01075312468")]
    [InlineData("010-1111-2424", "01011112424")]
    [InlineData("010 8531 7942", "01085317942")]
    [InlineData("+82-10-1234-5678", "01012345678")]
    public void 표기가_달라도_같은_숫자열로_정규화한다(string input, string expected)
    {
        var result = PhoneNumber.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("01075312468", "010-7531-2468")]
    [InlineData("0111234567", "011-123-4567")]
    [InlineData("021234567", "02-123-4567")]
    [InlineData("0212345678", "02-1234-5678")]
    public void 표시용_형식은_하이픈_표기로_통일한다(string input, string expected)
    {
        var result = PhoneNumber.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Formatted.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void 값이_없으면_실패한다(string? input)
    {
        var result = PhoneNumber.Create(input);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("employee.tel_required");
    }

    [Theory]
    [InlineData("12345")]              // 너무 짧음
    [InlineData("010123456789")]       // 너무 김
    [InlineData("1075312468")]         // 0 으로 시작하지 않음
    [InlineData("tel-없음")]            // 숫자 없음
    public void 형식이_틀리면_실패한다(string input)
    {
        var result = PhoneNumber.Create(input);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("employee.tel_invalid");
    }

    [Fact]
    public void 하이픈_유무와_무관하게_동등하다()
    {
        var left = PhoneNumber.Create("010-1111-2424").Value;
        var right = PhoneNumber.Create("01011112424").Value;

        left.Should().Be(right);
    }

    [Theory]
    [InlineData("01075312468", "010-****-2468")]
    [InlineData("021234567", "02-***-4567")]
    public void 마스킹은_가운데_국번만_가린다(string input, string expected)
    {
        var tel = PhoneNumber.Create(input).Value;

        tel.Masked.Should().Be(expected);
    }

    [Fact]
    public void 마스킹된_값에는_국번이_남지_않는다()
    {
        var tel = PhoneNumber.Create("010-7531-2468").Value;

        tel.Masked.Should().NotContain("7531");
        // 뒤 네 자리는 사람을 구분하기 위해 남긴다.
        tel.Masked.Should().EndWith("2468");
    }

    [Theory]
    [InlineData("010-7531", "0107531")]
    [InlineData("010 7531", "0107531")]
    [InlineData("검색어", "")]
    [InlineData(null, "")]
    public void 검색어에서_숫자만_추출한다(string? input, string expected)
        => PhoneNumber.DigitsOf(input).Should().Be(expected);
}
