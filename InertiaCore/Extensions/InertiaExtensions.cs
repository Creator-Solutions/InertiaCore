using System.Text.Json;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace InertiaCore.Extensions;

internal static class InertiaExtensions
{
    /// <summary>
    /// Whether this is an Inertia partial reload for the given component. A partial
    /// reload is only valid for an Inertia request (the client always sends
    /// <c>X-Inertia: true</c> alongside the partial headers); requiring it stops a
    /// plain full-page request from being treated as a partial reload.
    /// </summary>
    internal static bool IsInertiaPartialComponent(this ActionContext context, string component) =>
        context.HttpContext.IsInertiaRequest()
        && context.HttpContext.Request.Headers[InertiaHeader.PartialComponent] == component;

    internal static string RequestedUri(this HttpContext context) =>
        Uri.UnescapeDataString(context.Request.GetEncodedPathAndQuery());

    /// <summary>
    /// The request path and query exactly as they were received (still percent-encoded),
    /// with any CR/LF stripped as a defence against header injection. Use this for
    /// values written to response headers such as <c>X-Inertia-Location</c>; use
    /// <see cref="RequestedUri"/> only where a decoded URL is required.
    /// </summary>
    internal static string RequestedLocation(this HttpContext context) =>
        context.Request.GetEncodedPathAndQuery()
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

    internal static string RequestedUri(this ActionContext context) => context.HttpContext.RequestedUri();
    
    internal static bool IsInertiaRequest(this HttpContext context) =>
        context.Request.Headers[InertiaHeader.Inertia] == "true";

    internal static bool IsInertiaRequest(this ActionContext context) => context.HttpContext.IsInertiaRequest();

    internal static string ToCamelCase(this string s) => JsonNamingPolicy.CamelCase.ConvertName(s);

    internal static string ToCamelCasePath(this string s) =>
        string.Join('.', s.Split('.').Select(segment => segment.ToCamelCase()));

    internal static bool Override<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.TryAdd(key, value)) return false;
        dictionary[key] = value;

        return true;
    }

    internal static async Task<object?> ResolveAsync(this Func<object?> func)
    {
        var result = func();

        if (result is Task task)
        {
            return await task.UnwrapResultAsync();
        }

        return result;
    }

    internal static Task<object?> ResolveResult(this Task task)
    {
        return task.UnwrapResultAsync();
    }

    internal static string MD5(this string s)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(s);
        var hashBytes = md5.ComputeHash(inputBytes);

        var sb = new StringBuilder();
        foreach (var t in hashBytes)
            sb.Append(t.ToString("x2"));

        return sb.ToString();
    }
}
