using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Models;

/// <summary>
/// Options for Inertia error pages. When enabled, the middleware registered by
/// <c>UseInertiaStatusPages()</c> renders the configured error component for the status
/// codes it handles when the request is an Inertia request, and converts a cookie-auth
/// login redirect into an Inertia <c>409</c> location response.
/// <para>
/// Non-Inertia requests are never affected: they keep the host's default status handling
/// (an empty body, or the developer exception page in Development).
/// </para>
/// </summary>
public class InertiaStatusPagesOptions
{
    /// <summary>
    /// Component rendered for an error status when no per-status component is configured.
    /// </summary>
    public const string DefaultComponent = "Error";

    /// <summary>
    /// Default message shown for <c>401</c>.
    /// </summary>
    public const string DefaultMessage401 = "Unauthorized";

    /// <summary>
    /// Default message shown for <c>403</c>.
    /// </summary>
    public const string DefaultMessage403 = "Forbidden";

    /// <summary>
    /// Default message shown for <c>404</c>.
    /// </summary>
    public const string DefaultMessage404 = "Not Found";

    /// <summary>
    /// Default message shown for <c>419</c>.
    /// </summary>
    public const string DefaultMessage419 = "Page Expired";

    /// <summary>
    /// Default message shown for <c>500</c>.
    /// </summary>
    public const string DefaultMessage500 = "Server Error";

    /// <summary>
    /// Default message shown for <c>503</c>.
    /// </summary>
    public const string DefaultMessage503 = "Service Unavailable";

    /// <summary>
    /// The status codes handled out of the box.
    /// </summary>
    public static readonly IReadOnlyList<int> DefaultStatusCodes =
        new[] { 401, 403, 404, 419, 500, 503 };

    /// <summary>
    /// When <see langword="true"/>, <c>UseInertiaStatusPages()</c> renders the error
    /// component for handled statuses and <c>AddInertia()</c> registers the authorization
    /// result handler that converts cookie-auth login redirects. Disabled by default so
    /// existing applications are unaffected.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Component used for a handled status when <see cref="Components"/> does not contain
    /// an entry for it. Defaults to <see cref="DefaultComponent"/>.
    /// </summary>
    public string Component { get; set; } = DefaultComponent;

    /// <summary>
    /// Per-status component overrides. For example, map <c>503</c> to a maintenance page.
    /// </summary>
    public IDictionary<int, string> Components { get; set; } = new Dictionary<int, string>();

    /// <summary>
    /// The status codes that are rendered through Inertia. Defaults to
    /// <see cref="DefaultStatusCodes"/>. Assign a new collection to change the set.
    /// </summary>
    public IReadOnlyCollection<int> StatusCodes { get; set; } = DefaultStatusCodes;

    /// <summary>
    /// Per-status message overrides. When a status is absent the built-in default
    /// (<see cref="DefaultMessage401"/> and so on) is used.
    /// </summary>
    public IDictionary<int, string> Messages { get; set; } = new Dictionary<int, string>();

    /// <summary>
    /// When set, resolves the message for a status, taking precedence over
    /// <see cref="Messages"/>. Receives the current <see cref="HttpContext"/> and the
    /// status code.
    /// </summary>
    public Func<HttpContext, int, string>? MessageFactory { get; set; }

    /// <summary>
    /// When <see langword="true"/> (the default) the developer exception page put in place
    /// by the framework in the Development environment is preserved for <c>500</c>
    /// responses, instead of rendering the Inertia error page. Set to
    /// <see langword="false"/> to always render the Inertia error page.
    /// </summary>
    public bool RespectDeveloperExceptionPage { get; set; } = true;

    /// <summary>
    /// Resolves the component for a status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The component name.</returns>
    public string ResolveComponent(int statusCode) =>
        Components.TryGetValue(statusCode, out var component) && !string.IsNullOrEmpty(component)
            ? component
            : Component;

    /// <summary>
    /// Resolves the message for a status code, applying the default messages.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The message to show.</returns>
    public string ResolveMessage(HttpContext context, int statusCode)
    {
        if (MessageFactory != null)
            return MessageFactory(context, statusCode);

        if (Messages.TryGetValue(statusCode, out var message) && message != null)
            return message;

        return DefaultMessage(statusCode);
    }

    /// <summary>
    /// Returns the built-in message for a status code, or an empty string when none is
    /// known.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The built-in message.</returns>
    public static string DefaultMessage(int statusCode) => statusCode switch
    {
        401 => DefaultMessage401,
        403 => DefaultMessage403,
        404 => DefaultMessage404,
        419 => DefaultMessage419,
        500 => DefaultMessage500,
        503 => DefaultMessage503,
        _ => string.Empty,
    };
}
