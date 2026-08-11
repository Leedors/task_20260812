using EmployeeDirectory.Application.Employees;

namespace EmployeeDirectory.UnitTests.Application;

public sealed class JoinedDateTests
{
    [Theory]
    [InlineData("2018.03.07")]  // csv 예제 표기
    [InlineData("2018-03-07")]  // json 예제 표기
    [InlineData("2018/03/07")]
    [InlineData("20180307")]
    [InlineData("2018-3-7")]
    [InlineData("  2018.03.07  ")]
    public void 허용_포맷을_모두_해석한다(string input)
    {
        var parsed = JoinedDate.TryParse(input, out var value);

        Assert.True(parsed);
        Assert.Equal(2018, value.Year);
    }

    [Fact]
    public void ISO8601_전체_표기도_해석한다()
    {
        var parsed = JoinedDate.TryParse("2018-03-07T09:30:00Z", out var value);

        Assert.True(parsed);
        Assert.Equal(new DateOnly(2018, 3, 7), value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("07/03/2018")]   // 일/월/년 표기는 모호하므로 받지 않는다
    [InlineData("2018.13.07")]   // 존재하지 않는 월
    [InlineData("2018.02.30")]   // 존재하지 않는 일
    [InlineData("입사일")]
    public void 해석할_수_없으면_false를_반환한다(string? input)
    {
        var parsed = JoinedDate.TryParse(input, out _);

        Assert.False(parsed);
    }
}
