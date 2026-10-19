using System;
using InertiaCore.Middleware;
using Microsoft.AspNetCore.Builder;

namespace InertiaCore.Extensions;

/// <summary>
/// Extension methods for the Inertia anti-forgery (XSRF cookie) bridge.
/// </summary>
public static class InertiaAntiforgeryExtensions
{
    /// <summary>
    /// Adds middleware that issues the Inertia <c>XSRF-TOKEN</c> cookie and configures
    /// anti-forgery to validate the <c>X-XSRF-TOKEN</c> request header the Inertia client
    /// sends automatically.
    /// </summary>
    /// <remarks>
    /// Requires <c>InertiaOptions.Antiforgery.Enabled = true</c> (configured through
    /// <c>AddInertia</c>). Register this <b>after</b> <c>UseRouting</c>, <c>UseAuthentication</c>
    /// and <c>UseAuthorization</c>, and before the endpoints that call
    /// <c>RequireAntiforgery()</c> or carry <c>[AutoValidateAntiforgeryToken]</c>.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder, for chaining.</returns>
    public static IApplicationBuilder UseInertiaAntiforgery(this IApplicationBuilder app)
    {
        if (app is null) throw new ArgumentNullException(nameof(app));

        app.UseMiddleware<InertiaAntiforgeryMiddleware>();
        return app;
    }
}
