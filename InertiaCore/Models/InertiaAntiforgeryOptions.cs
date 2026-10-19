using System;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Models;

/// <summary>
/// Options for the Inertia <c>XSRF-TOKEN</c> cookie bridge. When enabled, the middleware
/// registered by <c>UseInertiaAntiforgery()</c> issues a request token through
/// <c>IAntiforgery</c>, exposes it to the Inertia client in a JavaScript-readable cookie,
/// and configures anti-forgery to validate the matching request header.
/// </summary>
public class InertiaAntiforgeryOptions
{
    /// <summary>
    /// Default name of the JavaScript-readable cookie that carries the XSRF token.
    /// Matches the cookie the Inertia (axios) client reads by default.
    /// </summary>
    public const string DefaultCookieName = "XSRF-TOKEN";

    /// <summary>
    /// Default request header the Inertia (axios) client sends the XSRF token in.
    /// </summary>
    public const string DefaultHeaderName = "X-XSRF-TOKEN";

    /// <summary>
    /// When <see langword="true"/>, <c>AddInertia()</c> registers anti-forgery and
    /// <c>UseInertiaAntiforgery()</c> issues the XSRF cookie. Disabled by default so
    /// existing applications are unaffected.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Name of the cookie that carries the request token. The cookie is readable by
    /// JavaScript (not <c>HttpOnly</c>), because the client has to copy its value into
    /// the request header.
    /// </summary>
    public string CookieName { get; set; } = DefaultCookieName;

    /// <summary>
    /// Request header the token is validated from. Configured on <c>IAntiforgery</c> so
    /// validation and issuance agree.
    /// </summary>
    public string HeaderName { get; set; } = DefaultHeaderName;

    /// <summary>
    /// When the token cookie is sent over HTTPS. <see cref="CookieSecurePolicy.SameAsRequest"/>
    /// matches the scheme of the current request; use <see cref="CookieSecurePolicy.Always"/>
    /// in production.
    /// </summary>
    public CookieSecurePolicy SecurePolicy { get; set; } = CookieSecurePolicy.SameAsRequest;

    /// <summary>
    /// <c>SameSite</c> policy of the token cookie. The default allows the cookie to be sent
    /// on top-level navigations while still limiting cross-site requests.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    /// <summary>
    /// Path the token cookie is scoped to.
    /// </summary>
    public string CookiePath { get; set; } = "/";
}
