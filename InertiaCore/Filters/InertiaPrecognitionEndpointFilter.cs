using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InertiaCore.Extensions;
using InertiaCore.Precognition;
using InertiaCore.Services;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Filters;

/// <summary>
/// Minimal API endpoint filter that short-circuits Precognition validation requests.
/// <para>
/// When the request carries <c>Precognition: true</c>, the filter validates the bound
/// arguments with <see cref="IInertiaRequestValidator"/>, records the errors in the
/// Inertia error bag, and returns a <c>204</c> (success) or <c>422</c> (failure)
/// without invoking the endpoint delegate. Non-precognitive requests call through
/// unchanged.
/// </para>
/// <para>
/// Endpoint filters run after argument binding, so any work performed by the
/// endpoint's parameters is already done; keep side effects inside the handler body,
/// which is not invoked for precognitive requests.
/// </para>
/// </summary>
public sealed class InertiaPrecognitionEndpointFilter : IEndpointFilter
{
    private readonly IErrorBagService _errorBagService;
    private readonly IInertiaRequestValidator _validator;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaPrecognitionEndpointFilter"/>.
    /// </summary>
    /// <param name="errorBagService">The error bag service.</param>
    /// <param name="validator">The request validator used to validate the bound arguments.</param>
    public InertiaPrecognitionEndpointFilter(IErrorBagService errorBagService, IInertiaRequestValidator validator)
    {
        _errorBagService = errorBagService ?? throw new ArgumentNullException(nameof(errorBagService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.IsPrecognitionRequest())
            return await next(context);

        var errors = await _validator.ValidateAsync(httpContext, context.Arguments, httpContext.RequestAborted);

        _errorBagService.AddErrors(
            _errorBagService.CurrentBagName,
            errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

        return PrecognitionResponse.BuildResult(httpContext, _errorBagService);
    }
}
