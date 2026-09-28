namespace Domolov.Application.Common;

/// <summary>A page of results with the total count.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Clamps paging input.</summary>
public static class Paging
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize, int Skip) Normalize(int? page, int? pageSize)
    {
        var p = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        return (p, size, (p - 1) * size);
    }
}
