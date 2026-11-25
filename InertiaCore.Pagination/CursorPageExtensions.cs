using System.Security.Cryptography;
using System.Text.Json;
using InertiaCore;
using InertiaCore.Contracts;
using InertiaCore.Props;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Extensions;

/// <summary>
/// Keyset pagination for Inertia infinite scroll.
/// </summary>
public static class CursorPageExtensions
{
    /// <summary>
    /// Pages a materialized sequence with an opaque, tamper-proof cursor and returns an
    /// infinite-scroll prop. The sequence is ordered in memory, so filter it before calling
    /// this method. When a sort value can repeat, set <see cref="Pagination.OrderBy.TieBreakerProperty"/>
    /// to a unique key such as the primary key, and add a matching composite index.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="inertia">The current Inertia response builder.</param>
    /// <param name="source">The rows to page. The sequence is materialized.</param>
    /// <param name="orderBy">One or more sort keys. A unique tie-breaker is required when values can repeat.</param>
    /// <param name="cursor">The cursor query value, or <see langword="null"/> for the first page. The value <c>1</c> is also the first page.</param>
    /// <param name="size">The maximum number of rows in the page. Must be greater than zero.</param>
    /// <param name="pageName">The query-string parameter that carries the cursor.</param>
    /// <param name="wrapper">The property name of the row array inside the scroll prop.</param>
    /// <param name="cancellationToken">A token that cancels before the page is built.</param>
    /// <returns>A scroll prop whose metadata contains the previous, next, and current cursors.</returns>
    /// <exception cref="InertiaCursorException">The cursor was altered or cannot be read. <c>UseInertia()</c> converts this into a validation response.</exception>
    public static Task<ScrollProp> ToInertiaCursorPageAsync<T>(
        this IInertia inertia,
        IEnumerable<T> source,
        IReadOnlyList<Pagination.OrderBy> orderBy,
        string? cursor,
        int size,
        string pageName = "cursor",
        string wrapper = "data",
        CancellationToken cancellationToken = default)
    {
        var services = Inertia.CurrentRequestServices
            ?? throw new InvalidOperationException("ToInertiaCursorPageAsync requires an active HTTP request. Pass IDataProtectionProvider to the other overload outside a request.");
        var provider = services.GetService<IDataProtectionProvider>()
            ?? throw new InvalidOperationException("Cursor pagination requires Data Protection. Call AddInertia(), which registers it.");

        return ToInertiaCursorPageAsync(inertia, source, orderBy, cursor, size, pageName, wrapper, provider, cancellationToken);
    }

    /// <summary>
    /// Pages a materialized sequence with an opaque, tamper-proof cursor and returns an
    /// infinite-scroll prop.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="inertia">The current Inertia response builder.</param>
    /// <param name="source">The rows to page. The sequence is materialized.</param>
    /// <param name="orderBy">One or more sort keys. A unique tie-breaker is required when values can repeat.</param>
    /// <param name="cursor">The cursor query value, or <see langword="null"/> for the first page. The value <c>1</c> is also the first page.</param>
    /// <param name="size">The maximum number of rows in the page. Must be greater than zero.</param>
    /// <param name="pageName">The query-string parameter that carries the cursor.</param>
    /// <param name="wrapper">The property name of the row array inside the scroll prop.</param>
    /// <param name="dataProtectionProvider">The provider used to seal cursor tokens. <c>AddInertia()</c> registers one.</param>
    /// <param name="cancellationToken">A token that cancels before the page is built.</param>
    /// <returns>A scroll prop whose metadata contains the previous, next, and current cursors.</returns>
    /// <exception cref="InertiaCursorException">The cursor was altered or cannot be read. <c>UseInertia()</c> converts this into a validation response.</exception>
    public static Task<ScrollProp> ToInertiaCursorPageAsync<T>(
        this IInertia inertia,
        IEnumerable<T> source,
        IReadOnlyList<Pagination.OrderBy> orderBy,
        string? cursor,
        int size,
        string pageName,
        string wrapper,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inertia);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (string.IsNullOrWhiteSpace(pageName))
            throw new ArgumentException("pageName is required.", nameof(pageName));
        if (string.IsNullOrWhiteSpace(wrapper))
            throw new ArgumentException("wrapper is required.", nameof(wrapper));

        cancellationToken.ThrowIfCancellationRequested();

        var protector = new Pagination.CursorTokenProtector(dataProtectionProvider);
        var keys = Pagination.CursorPaginator.BuildKeys<T>(orderBy);
        var ordered = Pagination.CursorPaginator.Order(source, keys);
        Pagination.CursorPaginator.EnsureUnique(ordered, keys);

        var start = 0;
        var end = Math.Min(size, ordered.Count);
        if (!IsFirstPage(cursor))
        {
            var token = ReadCursor(protector, cursor!);
            if (token.PointsToNext)
            {
                start = ordered.FindIndex(row => Pagination.CursorPaginator.CompareToBoundary(row!, keys, token) > 0);
                if (start < 0)
                    start = ordered.Count;
                end = Math.Min(start + size, ordered.Count);
            }
            else
            {
                var before = 0;
                for (var i = 0; i < ordered.Count; i++)
                {
                    if (Pagination.CursorPaginator.CompareToBoundary(ordered[i]!, keys, token) < 0)
                        before++;
                    else
                        break;
                }

                end = before;
                start = Math.Max(0, before - size);
            }
        }

        var page = ordered.Skip(start).Take(end - start).ToList();
        string? previous = start > 0
            ? protector.Protect(Pagination.CursorPaginator.TokenFor(ordered[start]!, keys, pointsToNext: false))
            : null;
        object? next = end >= ordered.Count
            ? null
            : page.Count > 0
                ? protector.Protect(Pagination.CursorPaginator.TokenFor(ordered[end - 1]!, keys, pointsToNext: true))
                : 1;

        object? current = IsFirstPage(cursor) ? 1 : cursor;
        var metadata = new ScrollMetadata(pageName, previous, next, current);
        var value = new Dictionary<string, object?> { [wrapper] = page };
        return Task.FromResult(inertia.Scroll(value, metadata, wrapper));
    }

    private static bool IsFirstPage(string? cursor) =>
        string.IsNullOrEmpty(cursor) || cursor == "1";

    private static Pagination.CursorToken ReadCursor(Pagination.CursorTokenProtector protector, string cursor)
    {
        Pagination.CursorToken token;
        try
        {
            token = protector.Unprotect(cursor);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            throw new InertiaCursorException();
        }

        if (token.Version != Pagination.CursorToken.CurrentVersion || token.Values == null)
            throw new InertiaCursorException();

        return token;
    }
}
