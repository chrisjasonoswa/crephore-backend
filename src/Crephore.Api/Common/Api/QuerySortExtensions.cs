using System.Linq.Expressions;

namespace Crephore.Api.Common.Api;

public static class QuerySortExtensions
{
    /// <summary>
    /// Applies multi-field sorting to a query from a list of sort entries.
    /// Each entry is "field" or "field:asc" or "field:desc".
    /// Unrecognized field names fall back to <paramref name="defaultSort"/>.
    /// If <paramref name="sortEntries"/> is empty, <paramref name="defaultSort"/> is applied
    /// according to <paramref name="defaultDescending"/>. <paramref name="tieBreaker"/> is applied last.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        List<string> sortEntries,
        Dictionary<string, Expression<Func<T, object?>>> fieldMap,
        Expression<Func<T, object?>> defaultSort,
        bool defaultDescending = false,
        Expression<Func<T, object?>>? tieBreaker = null)
    {
        IOrderedQueryable<T>? ordered = null;

        foreach (var entry in sortEntries)
        {
            var parts = entry.Split(':', 2);
            var field = parts[0].Trim().ToLowerInvariant();
            bool desc = parts.Length == 2 && string.Equals(parts[1].Trim(), "desc", StringComparison.OrdinalIgnoreCase);

            var selector = fieldMap.GetValueOrDefault(field) ?? defaultSort;

            ordered = ordered is null
                ? (desc ? query.OrderByDescending(selector) : query.OrderBy(selector))
                : (desc ? ordered.ThenByDescending(selector) : ordered.ThenBy(selector));
        }

        ordered ??= defaultDescending ? query.OrderByDescending(defaultSort) : query.OrderBy(defaultSort);

        // A unique tie-breaker (usually the id) keeps paging deterministic when sort values repeat.
        return tieBreaker is null ? ordered : ordered.ThenBy(tieBreaker);
    }
}
