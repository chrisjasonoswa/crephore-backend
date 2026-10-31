namespace Crephore.Api.Common.Api;

public class ApiPaginationRequestDto
{
    public int Page { get; set; } = 1; //Default to 1

    public int PageSize { get; set; } = 10; //Default to 10

    public int Skip => Page < 1 ? 0 : (Page - 1) * PageSize;

    /* Filter */
    public string? SearchKey { get; set; }

    // Sorting — each entry is "field" or "field:asc" or "field:desc".
    // Supported fields are listed on each List<Feature>RequestDto.
    // Example: ?sort=name:asc&sort=startsAt:desc
    public List<string> Sort { get; set; } = [];
}

public class ApiPaginationResponseDto<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; } // Current page number
    public int PageSize { get; set; }
    public int TotalCount { get; set; } // Total number of items across all pages
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
}
