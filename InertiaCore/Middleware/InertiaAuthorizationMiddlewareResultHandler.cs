using System;
using System.Threading.Tasks;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InertiaCore.Middleware;

/// <summary>
/// Wraps the default <see cref="IAuthorizationMiddlewareResultHandler"/> so that an
/// authorization challenge against an Inertia request is turned into an Inertia
/// <c>409</c> location response instead of a bare <c>302</c> redirect.
/// <para>
/// The Inertia client follows a <c>302</c> as an XHR redirect rather than a full
/// navigation, which surfaces as an empty error page. Emitting
/// <c>409 + X-Inertia-Location</c> tells the client to perform a full-page visit to the
/// login URL instead.
/// </para>
/// <para>
/// Forbid outcomes (<c>403</c>) are left untouched so <c>UseInertiaStatusPages()</c> can
/// render the error component. Non-Inertia requests are always delegated unchanged.
/// </para>
/// </summary>
public class InertiaAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly IAuthorizationMiddlewareResultHandler _inner;
    private readonly IOptions<InertiaOptions> _options;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaAuthorizationMiddlewareResultHandler"/>.
    /// </summary>
    /// <param name="inner">The handler that performs the actual challenge/forbid work.</param>
    /// <param name="options">The Inertia options carrying the status-page configuration.</param>
    public InertiaAuthorizationMiddlewareResultHandler(
        IAuthorizationMiddlewareResultHandler inner,
        IOptions<InertiaOptions> options)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        await _inner.HandleAsync(next, context, policy, authorizeResult);

        if (!_options.Value.StatusPages.Enabled)
            return;

        ConvertChallenge(context, authorizeResult);
    }

    /// <summary>
    /// Converts a redirect produced by a challenge for an Inertia request into an
    /// Inertia <c>409</c> location response.
    /// </summary>
    private static void ConvertChallenge(HttpContext context, PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Challenged)
            return;

        if (!context.IsInertiaRequest())
            return;

        if (context.Response.HasStarted)
            return;

        // Only a redirect challenge (the cookie handler's login redirect) is converted;
        // a challenge that already resolved to a 401 is left for the status-page middleware.
        var status = context.Response.StatusCode;
        if (status is not (StatusCodes.Status301MovedPermanently
            or StatusCodes.Status302Found
            or StatusCodes.Status303SeeOther
            or StatusCodes.Status307TemporaryRedirect
            or StatusCodes.Status308PermanentRedirect))
        {
            return;
        }

        var location = context.Response.Headers.Location.ToString();
        if (string.IsNullOrEmpty(location))
            return;

        context.Response.Headers.Remove("Location");
        context.Response.Headers[InertiaHeader.Location] = location;
        context.Response.StatusCode = StatusCodes.Status409Conflict;
    }
}

