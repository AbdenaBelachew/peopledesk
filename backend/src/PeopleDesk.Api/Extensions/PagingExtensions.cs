using Microsoft.AspNetCore.Mvc;

namespace PeopleDesk.Api.Extensions;

/// <summary>
/// Explicit primitive binding for list endpoints — complex [FromQuery] model binding of
/// PagedRequest proved unreliable, so controllers bind primitives and assemble it here.
/// </summary>
public static class PagingExtensions
{
    public static Application.Common.PagedRequest ToPagedRequest(
        int page = 1, int pageSize = 20, string? search = null, string? sortBy = null, bool sortDesc = false)
        => new()
        {
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            SortBy = sortBy,
            SortDesc = sortDesc
        };
}
