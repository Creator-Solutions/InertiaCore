namespace InertiaCore.Pagination;

/// <summary>
/// A sort key for keyset pagination, with an optional unique tie-breaker.
/// </summary>
/// <param name="Property">Public property name to order by.</param>
/// <param name="Direction">Sort direction for <paramref name="Property"/>.</param>
/// <param name="TieBreakerProperty">
/// Unique property applied ascending after <paramref name="Property"/> when that value can repeat.
/// Use the primary key. A composite index on <c>(Property, TieBreakerProperty)</c> matching these
/// directions keeps the keyset seek cheap in the database.
/// </param>
public sealed record OrderBy(
    string Property,
    SortDirection Direction,
    string? TieBreakerProperty = null);
