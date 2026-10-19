using System;
using System.Linq;
using FluentValidation;
using InertiaCore.Extensions;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.FluentValidation;

/// <summary>
/// Extension methods that apply FluentValidation to Minimal API endpoints.
/// </summary>
public static class InertiaFluentValidationEndpointExtensions
{
    /// <summary>
    /// Validates the bound <typeparamref name="TRequest"/> argument with its registered
    /// <see cref="IValidator{T}"/> before the handler runs. Failures follow the same rules as
    /// <c>WithInertiaValidation</c>: Inertia requests get the errors in the bag named by
    /// <c>X-Inertia-Error-Bag</c> (or <c>default</c>) and a 303 back, other requests get a 422, and
    /// precognitive requests get 204 or 422 with <c>Precognition-Validate-Only</c> applied.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="InvalidOperationException"/> when no validator is registered. The check
    /// runs when the endpoint is built where the host allows it, and otherwise on the first request.
    /// </remarks>
    /// <typeparam name="TRequest">The request type to validate.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="ruleSets">Optional FluentValidation rule sets to run. When omitted, the default rules run.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder WithInertiaFluentValidation<TRequest>(
        this RouteHandlerBuilder builder,
        string[]? ruleSets = null)
        where TRequest : class
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        Apply<TRequest>(builder, ruleSets);
        return builder;
    }

    /// <summary>
    /// Applies <see cref="WithInertiaFluentValidation{TRequest}(RouteHandlerBuilder, string[]?)"/>
    /// to every endpoint in the route group.
    /// </summary>
    /// <typeparam name="TRequest">The request type to validate.</typeparam>
    /// <param name="builder">The route group builder.</param>
    /// <param name="ruleSets">Optional FluentValidation rule sets to run.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteGroupBuilder WithInertiaFluentValidation<TRequest>(
        this RouteGroupBuilder builder,
        string[]? ruleSets = null)
        where TRequest : class
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        Apply<TRequest>(builder, ruleSets);
        return builder;
    }

    /// <summary>
    /// Marks the endpoint as validated (so the .NET 10 problem-details writer answers framework
    /// failures the same way) and adds the endpoint filter. The registration check runs when the
    /// endpoint is built.
    /// </summary>
    private static void Apply<TRequest>(IEndpointConventionBuilder builder, string[]? ruleSets)
        where TRequest : class
    {
        builder.WithMetadata(new InertiaValidationMetadata());
        builder.Add(endpoint => endpoint.FilterFactories.Add((factoryContext, next) =>
        {
            FluentValidationSupport.EnsureRegistered(factoryContext.ApplicationServices, typeof(TRequest));
            return invocation => InvokeAsync<TRequest>(invocation, next, ruleSets);
        }));
    }

    private static async ValueTask<object?> InvokeAsync<TRequest>(
        EndpointFilterInvocationContext invocation,
        EndpointFilterDelegate next,
        string[]? ruleSets)
        where TRequest : class
    {
        var httpContext = invocation.HttpContext;
        var request = invocation.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
            return await next(invocation);

        var validator = httpContext.RequestServices.GetService<IValidator<TRequest>>()
                        ?? throw FluentValidationSupport.MissingValidator(typeof(TRequest));

        var errors = await FluentValidationSupport.ValidateAsync(validator, request, ruleSets, httpContext.RequestAborted);

        // A precognitive request is answered without running the handler, even when it is valid.
        if (errors.Count == 0 && !httpContext.IsPrecognitionRequest())
            return await next(invocation);

        return FluentValidationSupport.Respond(httpContext, errors);
    }
}
