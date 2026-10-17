using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace InertiaCore.TypeGen;

/// <summary>
/// Builds a URL for a route from its pattern and a set of parameter values. This is the
/// server-side equivalent of the generated <c>route()</c> helper and is used by unit tests
/// to assert that the generated route definitions produce the correct URLs.
/// </summary>
public static partial class InertiaRouteUrlBuilder
{
    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex ParameterToken();

    /// <summary>
    /// Builds the URL for <paramref name="route"/> by substituting path parameters from
    /// <paramref name="values"/> and emitting any remaining values as a query string.
    /// </summary>
    /// <param name="route">The route definition (its pattern is used for substitution).</param>
    /// <param name="values">The parameter values. May be <see langword="null"/>.</param>
    /// <returns>The resulting URL (path plus optional query string).</returns>
    public static string BuildUrl(InertiaRouteDto route, IReadOnlyDictionary<string, object?>? values = null)
    {
        if (route is null) throw new ArgumentNullException(nameof(route));

        IReadOnlyDictionary<string, object?> supplied = values ?? Empty;
        var pathKeys = new List<string>();

        string path = ParameterToken().Replace(route.Pattern, match =>
        {
            string token = match.Groups[1].Value;
            string key = (token.StartsWith('*') ? token[1..] : token).TrimEnd('?');
            pathKeys.Add(key);

            if (!supplied.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return IsMultiValue(value)
                ? string.Join("/", Enumerate(value).Select(static item => Encode(item)))
                : Encode(value);
        });

        var query = new List<string>();
        foreach (KeyValuePair<string, object?> pair in supplied)
        {
            if (pathKeys.Contains(pair.Key) || pair.Value is null)
            {
                continue;
            }

            if (IsMultiValue(pair.Value))
            {
                foreach (object? item in Enumerate(pair.Value))
                {
                    query.Add(Encode(pair.Key) + "=" + Encode(item));
                }
            }
            else
            {
                query.Add(Encode(pair.Key) + "=" + Encode(pair.Value));
            }
        }

        return query.Count > 0 ? path + "?" + string.Join("&", query) : path;
    }

    private static readonly IReadOnlyDictionary<string, object?> Empty =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private static bool IsMultiValue(object value) =>
        value is not string && value is IEnumerable;

    private static IEnumerable<object?> Enumerate(object value) =>
        ((IEnumerable)value).Cast<object?>();

    private static string Encode(object? value) =>
        Uri.EscapeDataString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
}
