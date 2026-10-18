using System;
using InertiaCore.Filters;
using InertiaCore.Precognition;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        return builder
            .WithMetadata(new InertiaPrecognitionMetadata())
            .AddEndpointFilter<InertiaPrecognitionEndpointFilter>();
    }

    /// <summary>
    /// Validates the arguments bound to this endpoint using DataAnnotations before the
    /// handler runs. Invalid Inertia requests have their errors written to the error bag
    /// named by <c>X-Inertia-Error-Bag</c> (or the default bag) and receive a
    /// <c>303 See Other</c> redirect back, with the errors flashed when a flash provider
    /// is configured. Other invalid requests receive a <c>422</c> validation problem.
    /// Valid requests reach the handler unchanged. Endpoints with no validation
    /// attributes are not affected.
    /// </summary>
    /// <remarks>
    /// On .NET 10 and later, when <c>AddValidation()</c> is active for the endpoint, the
    /// framework's validation results are used and the arguments are not validated twice.
    /// </remarks>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder WithInertiaValidation(this RouteHandlerBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        return builder
            .WithMetadata(new InertiaValidationMetadata())
            .AddEndpointFilter<InertiaValidationEndpointFilter>();
    }

    /// <summary>
    /// Applies <see cref="WithInertiaValidation(RouteHandlerBuilder)"/> to every endpoint
    /// in the route group, so the whole group opts in.
    /// </summary>
    /// <param name="builder">The route group builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteGroupBuilder WithInertiaValidation(this RouteGroupBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        return builder
            .WithMetadata(new InertiaValidationMetadata())
            .AddEndpointFilter<InertiaValidationEndpointFilter>();
    }
}
