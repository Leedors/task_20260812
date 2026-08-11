using EmployeeDirectory.Application.Abstractions.Parsing;

namespace EmployeeDirectory.UnitTests.Application;

public sealed class SourceFormatHintTests
{
    [Theory]
    [InlineData("employees.csv", EmployeeSourceFormat.Csv)]
    [InlineData("EMPLOYEES.CSV", EmployeeSourceFormat.Csv)]
    [InlineData("employees.json", EmployeeSourceFormat.Json)]
    [InlineData("employees.txt", null)]
    [InlineData("employees", null)]
    [InlineData(null, null)]
    public void 파일명에서_형식을_추론한다(string? fileName, EmployeeSourceFormat? expected)
        => Assert.Equal(expected, SourceFormatHint.FromFileName(fileName));

    [Theory]
    [InlineData("text/csv", EmployeeSourceFormat.Csv)]
    [InlineData("text/csv; charset=utf-8", EmployeeSourceFormat.Csv)]
    [InlineData("application/json", EmployeeSourceFormat.Json)]
    [InlineData("application/json; charset=utf-8", EmployeeSourceFormat.Json)]
    [InlineData("text/plain", null)]
    [InlineData("application/octet-stream", null)]
    [InlineData(null, null)]
    public void ContentType에서_형식을_추론한다(string? contentType, EmployeeSourceFormat? expected)
        => Assert.Equal(expected, SourceFormatHint.FromContentType(contentType));
}
