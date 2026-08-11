namespace EmployeeDirectory.Application.Employees.Dtos;

/// <summary>
/// 페이징된 조회 결과.
/// </summary>
/// <remarks>
/// 과제 요구("전체 데이터를 보여주고, 페이징 가능하도록")에 따라
/// 현재 페이지 항목뿐 아니라 <see cref="TotalCount"/> 와 파생 정보를 함께 내려준다.
/// Front-end 가 페이지네이션 UI를 그리는 데 추가 호출이 필요 없도록 하기 위해서다.
/// </remarks>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new(Array.Empty<T>(), page, pageSize, 0);
}
