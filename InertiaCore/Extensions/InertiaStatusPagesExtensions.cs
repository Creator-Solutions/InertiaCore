using System;
using InertiaCore.Middleware;
using Microsoft.AspNetCore.Builder;

namespace InertiaCore.Extensions;

/// <summary>
/// Extension methods for Inertia error pages.
/// </summary>
public static class InertiaStatusPagesExtensions
{
    /// <summary>
    /// Adds middleware that renders the configured Inertia error component for handled
    /// status codes (401, 403, 404, 419, 500, 503 by default) when the request is an
    /// Inertia request.
    /// </summary>
    /// <remarks>
    /// Requires <c>InertiaOptions.StatusPages.Enabled = true</c> (configured through
    /// <c>AddInertia</c>). Register this <b>after</b> <c>UseRouting</c> and <b>before</b>
    /// <c>UseAuthentication</c> and <c>UseAuthorization</c>: authorization short-circuits
    /// with the final status and does not call further middleware, so the status-page
    /// middleware has to wrap it to observe a <c>401</c>/<c>403</c>.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder, for chaining.</returns>
    public static IApplicationBuilder UseInertiaStatusPages(this IApplicationBuilder app)
    {
        if (app is null) throw new ArgumentNullException(nameof(app));

        app.UseMiddleware<InertiaStatusPagesMiddleware>();
        return app;
    }
}
