using System;
using InertiaCore.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Extensions;

/// <summary>
/// Extension methods for applying Inertia behaviours to Minimal API endpoints.
/// </summary>
public static class InertiaEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Enables Precognition validation for this endpoint. When a request carries
    /// <c>Precognition: true</c>, the bound arguments are validated and the endpoint
    /// responds with <c>204</c> (valid) or <c>422</c> (invalid) without invoking the
    /// handler.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder WithInertiaPrecognition(this RouteHandlerBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        return builder.AddEndpointFilter<InertiaPrecognitionEndpointFilter>();
    }
}
