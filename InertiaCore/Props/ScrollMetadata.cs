namespace InertiaCore.Props;

/// <summary>
/// Cursor metadata for an infinite-scroll prop, describing the query-string page
/// parameter and the previous, next and current page tokens.
/// <para>
/// Tokens are typed as <see cref="object"/> because page identifiers may be either
/// numbers (offset pagination) or opaque strings (cursor pagination), and either may
/// be <see langword="null"/> at the start or end of the result set.
/// </para>
/// See: https://inertiajs.com/infinite-scroll
/// </summary>
/// <param name="PageName">The query-string parameter that carries the page token.</param>
/// <param name="PreviousPage">The previous page token, or <see langword="null"/> when there is none.</param>
/// <param name="NextPage">The next page token, or <see langword="null"/> when there is none.</param>
/// <param name="CurrentPage">The current page token.</param>
public sealed record ScrollMetadata(
    string PageName,
    object? PreviousPage = null,
    object? NextPage = null,
    object? CurrentPage = null);
