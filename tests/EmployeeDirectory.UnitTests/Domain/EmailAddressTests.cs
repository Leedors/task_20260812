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

        Assert.True(result.IsSuccess);
        Assert.Equal(input.ToLowerInvariant(), result.Value.Value);
    }

    [Fact]
    public void 앞뒤_공백은_제거하고_대문자는_소문자로_정규화한다()
    {
        var result = EmailAddress.Create("  Charles@CLOVF.com  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("charles@clovf.com", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 값이_없으면_실패한다(string? input)
    {
        var result = EmailAddress.Create(input);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.email_required", result.FirstError.Code);
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

        Assert.True(result.IsFailure);
        Assert.Equal("employee.email_invalid", result.FirstError.Code);
    }

    [Fact]
    public void 최대_길이를_넘으면_실패한다()
    {
        var tooLong = new string('a', 250) + "@clovf.com";

        var result = EmailAddress.Create(tooLong);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.email_too_long", result.FirstError.Code);
    }

    [Fact]
    public void 같은_주소는_같은_값으로_취급한다()
    {
        var left = EmailAddress.Create("clo@clovf.com").Value;
        var right = EmailAddress.Create("CLO@clovf.com").Value;

        Assert.Equal(left, right);
    }
}
