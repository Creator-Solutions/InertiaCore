using System;
using System.Threading.Tasks;
using InertiaCore.Extensions;
using InertiaCore.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InertiaCore.Middleware;

/// <summary>
/// Issues the Inertia <c>XSRF-TOKEN</c> cookie so the Inertia client can copy it into the
/// <c>X-XSRF-TOKEN</c> request header that ASP.NET Core anti-forgery validates.
/// <para>
/// The cookie is written only when the request actually needs a token: a mutating request
/// (anything other than GET/HEAD/OPTIONS) or an Inertia page render. Static files, probes
/// and other plain GETs are left untouched.
/// </para>
/// </summary>
public class InertiaAntiforgeryMiddleware
{
    /// <summary>
    /// <see cref="HttpContext.Items"/> key holding the token scheduled to be written when
    /// the response starts. Storing the token (rather than closing over it) lets
    /// <c>Inertia.RotateAntiforgery()</c> replace it without scheduling a second cookie
    /// write.
    /// </summary>
    internal const string PendingTokenKey = "InertiaCore.Antiforgery.PendingToken";

    /// <summary>
    /// <see cref="HttpContext.Items"/> key marking that a cookie write has been scheduled
    /// for this response, so a single <c>OnStarting</c> callback is registered.
    /// </summary>
    internal const string ScheduledKey = "InertiaCore.Antiforgery.Scheduled";

    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS"];

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaAntiforgeryMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public InertiaAntiforgeryMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// Issues the XSRF cookie when the request needs a token, then continues the pipeline.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="antiforgery">The anti-forgery service that generates the token.</param>
    /// <param name="options">The Inertia options carrying the anti-forgery configuration.</param>
    public async Task InvokeAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IOptions<InertiaOptions> options)
    {
        var settings = options.Value.Antiforgery;

        if (!settings.Enabled || !NeedsToken(context))
        {
            await _next(context);
            return;
        }

        var token = GetToken(context, antiforgery);
        ScheduleCookie(context, settings, token);

        await _next(context);
    }

    /// <summary>
    /// Whether the request should receive a token cookie. A mutating request always needs
    /// one; a safe request only when it renders an Inertia page.
    /// </summary>
    internal static bool NeedsToken(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method))
            return context.IsInertiaRequest();

        return true;
    }

    /// <summary>
    /// Generates (if needed) and returns the request token for the current request.
    /// </summary>
    internal static string? GetToken(HttpContext context, IAntiforgery antiforgery)
        => antiforgery.GetAndStoreTokens(context).RequestToken;

    /// <summary>
    /// Schedules the token cookie to be written when the response starts. A single
    /// <c>OnStarting</c> callback is registered per request; it reads the token from
    /// <see cref="PendingTokenKey"/> so <c>Inertia.RotateAntiforgery()</c> can replace the
    /// value before it is written.
    /// </summary>
    internal static void ScheduleCookie(HttpContext context, InertiaAntiforgeryOptions settings, string? token)
    {
        if (string.IsNullOrEmpty(token))
            return;

        context.Items[PendingTokenKey] = token;

        if (context.Items.ContainsKey(ScheduledKey))
            return;

        context.Items[ScheduledKey] = true;
        context.Response.OnStarting(() =>
        {
            if (context.Items[PendingTokenKey] is string pending && !string.IsNullOrEmpty(pending))
                WriteCookie(context, settings, pending);

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Writes the JavaScript-readable token cookie using the configured secure policy,
    /// <c>SameSite</c> and path.
    /// </summary>
    internal static void WriteCookie(HttpContext context, InertiaAntiforgeryOptions settings, string token)
    {
        context.Response.Cookies.Append(settings.CookieName, token, new CookieOptions
        {
            // The client must read this cookie to echo it into the request header.
            HttpOnly = false,
            IsEssential = true,
            Path = settings.CookiePath,
            SameSite = settings.SameSite,
            Secure = settings.SecurePolicy == CookieSecurePolicy.Always
                || (settings.SecurePolicy == CookieSecurePolicy.SameAsRequest && context.Request.IsHttps),
        });
    }
}
