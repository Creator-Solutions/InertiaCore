using InertiaCore.Extensions;
using InertiaCore.Services;
using InertiaCore.Services.Version;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InertiaCore.Middleware;

public class InertiaMiddleware
{
    private static readonly string[] NonGetMethods = ["POST", "PUT", "PATCH", "DELETE"];

    private readonly RequestDelegate _next;

    public InertiaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IInertiaVersionProvider versionResolver,
        IErrorBagService errorBagService)
    {
        string currentVersion = versionResolver.GetVersion();

        var headerValue = context.Request.Headers["X-Inertia-Error-Bag"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerValue))
        {
            errorBagService.CurrentBagName = headerValue;
        }

        context.Items["InertiaVersion"] = currentVersion;
        context.Items["InertiaErrorBag"] = errorBagService.CurrentBagName;

        if (context.Items.TryGetValue("InertiaPageData", out var pageDataObj) &&
            pageDataObj is IDictionary<string, object> pageData)
        {
            pageData.TryAdd("version", currentVersion);
        }

        context.Response.OnStarting(() =>
        {
            var isInertiaRequest = context.IsInertiaRequest();
            var isInertiaResponse = context.Items.ContainsKey("InertiaResponse") || context.Response.Headers.ContainsKey("X-Inertia");

            if (isInertiaRequest && !isInertiaResponse && IsEmptyResponse(context))
            {
                var backUrl = GetSafeBackUrl(context);

                var isNonGet = NonGetMethods.Contains(context.Request.Method);
                context.Response.StatusCode = isNonGet ? 303 : 302;
                context.Response.Headers["Location"] = backUrl;
            }
            else if (isInertiaRequest || context.Items.ContainsKey("InertiaResponse"))
            {
                context.Response.Headers["X-Inertia"] = "true";
                context.Response.Headers["X-Inertia-Version"] = currentVersion;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }

    /// <summary>
    /// Determines whether the current response carries no payload. A <c>204</c> is
    /// always empty; a <c>200</c> counts when it has no content type and either a
    /// null or zero content length. An empty body written without an explicit
    /// content length is therefore treated as empty and redirected.
    /// </summary>
    private static bool IsEmptyResponse(HttpContext context)
    {
        var status = context.Response.StatusCode;

        if (status == 204)
            return string.IsNullOrEmpty(context.Response.ContentType);

        if (status != 200)
            return false;

        return string.IsNullOrEmpty(context.Response.ContentType)
            && (context.Response.ContentLength is null or 0);
    }

    /// <summary>
    /// Resolves the URL to redirect back to. The <c>Referer</c> header is only
    /// honoured when it is a safe relative path or points at the current scheme,
    /// host and port; otherwise the request path is used. This prevents the header
    /// from being abused as an open redirect, including protocol-relative
    /// (<c>//host</c>) and backslash-prefixed forms.
    /// </summary>
    private static string GetSafeBackUrl(HttpContext context)
    {
        var referer = context.Request.Headers["Referer"].ToString();
        var fallback = context.Request.Path + context.Request.QueryString;

        if (string.IsNullOrEmpty(referer) ||
            !Uri.TryCreate(referer, UriKind.RelativeOrAbsolute, out var uri))
        {
            return fallback;
        }

        if (!uri.IsAbsoluteUri)
        {
            // Browsers resolve "//host" and "\host" (and "/\host") off-site, so reject
            // those while still allowing ordinary relative paths.
            if (referer.StartsWith("//", StringComparison.Ordinal) ||
                referer.StartsWith("/\\", StringComparison.Ordinal) ||
                referer.StartsWith("\\", StringComparison.Ordinal))
            {
                return fallback;
            }

            return referer;
        }

        var isHttp = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        var sameScheme = string.Equals(uri.Scheme, context.Request.Scheme, StringComparison.OrdinalIgnoreCase);
        var sameHost = string.Equals(uri.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase);
        var requestPort = context.Request.Host.Port
            ?? (string.Equals(context.Request.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80);
        var samePort = uri.Port == requestPort;

        return isHttp && sameScheme && sameHost && samePort ? uri.PathAndQuery + uri.Fragment : fallback;
    }
}
