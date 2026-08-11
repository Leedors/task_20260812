using EmployeeDirectory.Domain.Employees;

namespace EmployeeDirectory.UnitTests.Domain;

public sealed class EmployeeTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);

    [Fact]
    public void 모든_값이_유효하면_생성된다()
    {
        var result = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("김철수");
        result.Value.Email.Value.Should().Be("charles@clovf.com");
        result.Value.Tel.Formatted.Should().Be("010-7531-2468");
        result.Value.Joined.Should().Be(new DateOnly(2018, 3, 7));
    }

    [Fact]
    public void 이름_앞뒤_공백은_제거한다()
    {
        var result = Employee.Create("  홍길동  ", "kildong.hong@clovf.com", "01012345678", new DateOnly(2015, 8, 15), Today);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("홍길동");
    }

    [Fact]
    public void 이름이_비어_있으면_실패한다()
    {
        var result = Employee.Create(" ", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(error => error.Code == "employee.name_required");
    }

    [Fact]
    public void 입사일이_미래이면_실패한다()
    {
        var result = Employee.Create("김철수", "charles@clovf.com", "01075312468", Today.AddDays(1), Today);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(error => error.Code == "employee.joined_in_future");
    }

    [Fact]
    public void 입사일이_오늘이면_허용한다()
    {
        var result = Employee.Create("김철수", "charles@clovf.com", "01075312468", Today, Today);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void 입사일이_없으면_실패한다()
    {
        var result = Employee.Create("김철수", "charles@clovf.com", "01075312468", joined: null, Today);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(error => error.Code == "employee.joined_required");
    }

    [Fact]
    public void 여러_항목이_동시에_틀리면_오류를_모두_모아_반환한다()
    {
        var result = Employee.Create(null, "bad-email", "123", Today.AddYears(1), Today);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(4);
        result.Errors.Select(error => error.Code).Should().BeEquivalentTo(
        [
            "employee.name_required",
            "employee.email_invalid",
            "employee.tel_invalid",
            "employee.joined_in_future"
        ]);
    }

    [Fact]
    public void 전체_교체는_이메일까지_바꾼다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;

        var result = employee.Replace("김철수", "new@clovf.com", "010-0000-1111", new DateOnly(2019, 1, 1), Today);

        result.IsSuccess.Should().BeTrue();
        employee.Email.Value.Should().Be("new@clovf.com");
        employee.Tel.Formatted.Should().Be("010-0000-1111");
        employee.Joined.Should().Be(new DateOnly(2019, 1, 1));
    }

    [Fact]
    public void 전체_교체가_실패하면_기존_값이_그대로_남는다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;

        var result = employee.Replace("김철수", "이메일아님", "01075312468", new DateOnly(2018, 3, 7), Today);

        result.IsFailure.Should().BeTrue();
        // 검증을 먼저 끝내고 한 번에 반영하므로 부분 적용된 상태가 남지 않는다.
        employee.Email.Value.Should().Be("charles@clovf.com");
    }

    [Fact]
    public void 제외_처리하면_삭제_시각이_기록된다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;
        var deletedAt = new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

        employee.IsDeleted.Should().BeFalse();

        employee.MarkDeleted(deletedAt);

        employee.IsDeleted.Should().BeTrue();
        employee.DeletedAt.Should().Be(deletedAt);
    }

    [Fact]
    public void 복구하면_삭제_표시가_사라진다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;
        employee.MarkDeleted(DateTimeOffset.UtcNow);

        employee.Restore();

        employee.IsDeleted.Should().BeFalse();
        employee.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void 연락처_갱신시_이메일은_바뀌지_않는다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value;
        var newTel = PhoneNumber.Create("010-0000-1111").Value;

        employee.UpdateContact("김철수(변경)", newTel, new DateOnly(2019, 1, 1));

        employee.Email.Value.Should().Be("charles@clovf.com");
        employee.Name.Should().Be("김철수(변경)");
        employee.Tel.Formatted.Should().Be("010-0000-1111");
        employee.Joined.Should().Be(new DateOnly(2019, 1, 1));
    }
}
