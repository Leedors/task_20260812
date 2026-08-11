using EmployeeDirectory.Domain.Employees;

namespace EmployeeDirectory.UnitTests.Domain;

public sealed class EmployeeTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);

    [Fact]
    public void 모든_값이_유효하면_생성된다()
    {
        var result = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today);

        Assert.True(result.IsSuccess);
        Assert.Equal("김철수", result.Value.Name);
        Assert.Equal("charles@clovf.com", result.Value.Email.Value);
        Assert.Equal("010-7531-2468", result.Value.Tel.Formatted);
        Assert.Equal(new DateOnly(2018, 3, 7), result.Value.Joined);
    }

    [Fact]
    public void 이름_앞뒤_공백은_제거한다()
    {
        var result = Employee.Create("  홍길동  ", "kildong.hong@clovf.com", "01012345678", new DateOnly(2015, 8, 15), Today);

        Assert.True(result.IsSuccess);
        Assert.Equal("홍길동", result.Value.Name);
    }

    [Fact]
    public void 이름이_비어_있으면_실패한다()
    {
        var result = Employee.Create(" ", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "employee.name_required");
    }

    [Fact]
    public void 입사일이_미래이면_실패한다()
    {
        var result = Employee.Create("김철수", "charles@clovf.com", "01075312468", Today.AddDays(1), Today);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "employee.joined_in_future");
    }

    [Fact]
    public void 입사일이_오늘이면_허용한다()
    {
        var result = Employee.Create("김철수", "charles@clovf.com", "01075312468", Today, Today);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void 입사일이_없으면_실패한다()
    {
        var result = Employee.Create("김철수", "charles@clovf.com", "01075312468", joined: null, Today);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "employee.joined_required");
    }

    [Fact]
    public void 여러_항목이_동시에_틀리면_오류를_모두_모아_반환한다()
    {
        var result = Employee.Create(null, "bad-email", "123", Today.AddYears(1), Today);

        Assert.True(result.IsFailure);
        Assert.Equal(4, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Code == "employee.name_required");
        Assert.Contains(result.Errors, error => error.Code == "employee.email_invalid");
        Assert.Contains(result.Errors, error => error.Code == "employee.tel_invalid");
        Assert.Contains(result.Errors, error => error.Code == "employee.joined_in_future");
    }

    [Fact]
    public void 전체_교체는_이메일까지_바꾼다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;

        var result = employee.Replace("김철수", "new@clovf.com", "010-0000-1111", new DateOnly(2019, 1, 1), Today);

        Assert.True(result.IsSuccess);
        Assert.Equal("new@clovf.com", employee.Email.Value);
        Assert.Equal("010-0000-1111", employee.Tel.Formatted);
        Assert.Equal(new DateOnly(2019, 1, 1), employee.Joined);
    }

    [Fact]
    public void 전체_교체가_실패하면_기존_값이_그대로_남는다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;

        var result = employee.Replace("김철수", "이메일아님", "01075312468", new DateOnly(2018, 3, 7), Today);

        Assert.True(result.IsFailure);
        // 검증을 먼저 끝내고 한 번에 반영하므로 부분 적용된 상태가 남지 않는다.
        Assert.Equal("charles@clovf.com", employee.Email.Value);
    }

    [Fact]
    public void 제외_처리하면_삭제_시각이_기록된다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;
        var deletedAt = new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

        Assert.False(employee.IsDeleted);

        employee.MarkDeleted(deletedAt);

        Assert.True(employee.IsDeleted);
        Assert.Equal(deletedAt, employee.DeletedAt);
    }

    [Fact]
    public void 복구하면_삭제_표시가_사라진다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;
        employee.MarkDeleted(DateTimeOffset.UtcNow);

        employee.Restore();

        Assert.False(employee.IsDeleted);
        Assert.Null(employee.DeletedAt);
    }

    [Fact]
    public void 연락처_갱신시_이메일은_바뀌지_않는다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;
        var newTel = PhoneNumber.Create("010-0000-1111").Value;

        employee.UpdateContact("김철수(변경)", newTel, new DateOnly(2019, 1, 1));

        Assert.Equal("charles@clovf.com", employee.Email.Value);
        Assert.Equal("김철수(변경)", employee.Name);
        Assert.Equal("010-0000-1111", employee.Tel.Formatted);
        Assert.Equal(new DateOnly(2019, 1, 1), employee.Joined);
    }
}
