using EmployeeDirectory.Domain.Employees;

namespace EmployeeDirectory.UnitTests.Domain;

public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("charles@clovf.com")]
    [InlineData("kildong.hong@clovf.com")]
    [InlineData("a+tag@sub.example.co.kr")]
    public void 유효한_이메일이면_성공한다(string input)
    {
        var result = EmailAddress.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.ToLowerInvariant());
    }

    [Fact]
    public void 앞뒤_공백은_제거하고_대문자는_소문자로_정규화한다()
    {
        var result = EmailAddress.Create("  Charles@CLOVF.com  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("charles@clovf.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 값이_없으면_실패한다(string? input)
    {
        var result = EmailAddress.Create(input);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("employee.email_required");
    }

    [Theory]
    [InlineData("charles")]
    [InlineData("charles@")]
    [InlineData("@clovf.com")]
    [InlineData("charles@clovf")]
    [InlineData("char les@clovf.com")]
    [InlineData("charles@@clovf.com")]
    public void 형식이_틀리면_실패한다(string input)
    {
        var result = EmailAddress.Create(input);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("employee.email_invalid");
    }

    [Fact]
    public void 최대_길이를_넘으면_실패한다()
    {
        var tooLong = new string('a', 250) + "@clovf.com";

        var result = EmailAddress.Create(tooLong);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("employee.email_too_long");
    }

    [Fact]
    public void 같은_주소는_같은_값으로_취급한다()
    {
        var left = EmailAddress.Create("clo@clovf.com").Value;
        var right = EmailAddress.Create("CLO@clovf.com").Value;

        left.Should().Be(right);
    }

    [Theory]
    [InlineData("charles@clovf.com", "ch***@clovf.com")]
    [InlineData("kildong.hong@clovf.com", "ki***@clovf.com")]
    [InlineData("ab@clovf.com", "a***@clovf.com")]
    [InlineData("a@clovf.com", "a***@clovf.com")]
    public void 마스킹은_계정부_앞부분만_남긴다(string input, string expected)
    {
        var email = EmailAddress.Create(input).Value;

        email.Masked.Should().Be(expected);
    }

    [Fact]
    public void 마스킹된_값에는_원본_계정부가_남지_않는다()
    {
        var email = EmailAddress.Create("kildong.hong@clovf.com").Value;

        email.Masked.Should().NotContain("kildong.hong");
    }
}
