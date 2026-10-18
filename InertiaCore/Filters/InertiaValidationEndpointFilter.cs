using System;
using System.Threading.Tasks;
using InertiaCore.Extensions;
using InertiaCore.Precognition;
using InertiaCore.Services;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
#if NET10_0_OR_GREATER
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
#endif

namespace InertiaCore.Filters;

/// <summary>
/// Endpoint filter that validates the arguments bound to a Minimal API endpoint and
/// reports failures through Inertia.
/// </summary>
/// <remarks>
/// <para>
/// For Inertia requests, errors are written to <see cref="IErrorBagService"/> under the
/// bag named by <c>X-Inertia-Error-Bag</c>. When an <see cref="IInertiaFlashProvider"/>
/// is registered, the errors are flashed and the response is a <c>303 See Other</c>
/// redirect back to the referring page. Without a flash provider the errors cannot
/// survive the redirect, so a <c>422</c> validation problem is returned instead.
/// Non-Inertia requests always receive a <c>422</c> validation problem.
/// </para>
/// <para>
/// On .NET 10 and later, when <c>AddValidation()</c> is active for the endpoint, the
/// framework validates the arguments before this filter runs and this filter does not
/// validate again. The framework's failure is answered by the problem-details writer,
/// which applies the same Inertia rules.
/// </para>
/// </remarks>
public sealed class InertiaValidationEndpointFilter : IEndpointFilter
{
    private readonly IErrorBagService _errorBagService;
    private readonly IInertiaRequestValidator _validator;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaValidationEndpointFilter"/>.
    /// </summary>
    /// <param name="errorBagService">The error bag service.</param>
    /// <param name="validator">The request validator that checks the bound arguments.</param>
    public InertiaValidationEndpointFilter(IErrorBagService errorBagService, IInertiaRequestValidator validator)
    {
        _errorBagService = errorBagService ?? throw new ArgumentNullException(nameof(errorBagService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // A precognition endpoint answers precognitive requests itself, whatever the filter order.
        if (httpContext.IsPrecognitionRequest() && httpContext.GetEndpoint()?.Metadata.GetMetadata<InertiaPrecognitionMetadata>() is not null)
            return await next(context);

#if NET10_0_OR_GREATER
        if (IsBuiltInValidationActive(httpContext))
        {
            // The framework validated the arguments before this filter ran. Validating again
            // would double the work, so pass the request through.
            return await next(context);
        }
#endif

        var errors = await _validator.ValidateAsync(httpContext, context.Arguments, httpContext.RequestAborted);

        if (errors.Count == 0)
            return await next(context);

        return InertiaValidationResponse.Create(httpContext, _errorBagService, errors);
    }

#if NET10_0_OR_GREATER
    /// <summary>
    /// Mirrors the framework's own activation rule: validation resolvers are registered and
    /// the endpoint has not opted out with <c>DisableValidation()</c>.
    /// </summary>
    private static bool IsBuiltInValidationActive(HttpContext httpContext)
    {
#pragma warning disable ASP0029 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        var options = httpContext.RequestServices.GetService<IOptions<ValidationOptions>>()?.Value;
        if (options is null || options.Resolvers.Count == 0)
            return false;
#pragma warning restore ASP0029

        return httpContext.GetEndpoint()?.Metadata.GetMetadata<IDisableValidationMetadata>() is null;
    }
#endif
}
