using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Extensions;

/// <summary>
/// Endpoint filter that validates the anti-forgery request token on Minimal API endpoints.
/// </summary>
public sealed class InertiaAntiforgeryEndpointFilter : IEndpointFilter
{
    /// <summary>
    /// Validates the request token and returns <c>400</c> when it is missing or invalid.
    /// </summary>
    /// <param name="context">The endpoint filter context.</param>
    /// <param name="next">The next filter in the pipeline.</param>
    /// <returns>The endpoint result, or a <c>400</c> when validation fails.</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.StatusCode(StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}

/// <summary>
/// Minimal API extensions for the Inertia anti-forgery bridge.
/// </summary>
public static class InertiaAntiforgeryRouteExtensions
{
    /// <summary>
    /// Validates the anti-forgery request token for a Minimal API endpoint. The token is
    /// read from the header configured by <c>InertiaOptions.Antiforgery.HeaderName</c>
    /// (default <c>X-XSRF-TOKEN</c>), which the Inertia client fills from the cookie issued
    /// by <c>UseInertiaAntiforgery()</c>. Requests without a valid token get a <c>400</c>.
    /// </summary>
    /// <remarks>
    /// Minimal APIs have no <c>RequireAntiforgery()</c> equivalent; this filter is the
    /// supported way to opt an Inertia endpoint into token validation.
    /// </remarks>
    /// <param name="builder">The endpoint builder.</param>
    /// <returns>The endpoint builder, for chaining.</returns>
    public static RouteHandlerBuilder WithInertiaAntiforgery(this RouteHandlerBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        return builder.AddEndpointFilter<InertiaAntiforgeryEndpointFilter>();
    }

    /// <summary>
    /// Validates the anti-forgery request token for every endpoint in the group. See
    /// <see cref="WithInertiaAntiforgery(RouteHandlerBuilder)"/>.
    /// </summary>
    /// <param name="builder">The route group builder.</param>
    /// <returns>The route group builder, for chaining.</returns>
    public static RouteGroupBuilder WithInertiaAntiforgery(this RouteGroupBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        return builder.AddEndpointFilter<InertiaAntiforgeryEndpointFilter>();
    }
}
