using System;
using InertiaCore.Extensions;
using InertiaCore.Precognition;
using InertiaCore.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InertiaCore.Filters;

/// <summary>
/// MVC action filter that short-circuits Precognition validation requests.
/// <para>
/// When the request carries <c>Precognition: true</c>, the filter validates the
/// request's model state, records the errors in the Inertia error bag, and sets a
/// <c>204</c> (success) or <c>422</c> (failure) result so the action body never runs.
/// Non-precognitive requests are left completely untouched.
/// </para>
/// <para>
/// The filter runs before MVC's automatic <c>400</c>-on-invalid-model filter
/// (<see cref="Order"/> is lower than that filter's), so an invalid precognitive
/// request is answered with <c>422</c> rather than <c>400</c>.
/// </para>
/// </summary>
public sealed class InertiaPrecognitionFilter : IActionFilter, IOrderedFilter
{
    private readonly IErrorBagService _errorBagService;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaPrecognitionFilter"/>.
    /// </summary>
    /// <param name="errorBagService">The error bag service.</param>
    public InertiaPrecognitionFilter(IErrorBagService errorBagService)
    {
        _errorBagService = errorBagService ?? throw new ArgumentNullException(nameof(errorBagService));
    }

    /// <summary>
    /// The filter order. Runs before MVC's <c>ModelStateInvalidFilter</c> (order
    /// <c>-2000</c>) so invalid precognitive requests produce <c>422</c>, not <c>400</c>.
    /// </summary>
    public int Order => -3000;

    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.IsPrecognitionRequest())
            return;

        context.ModelState.AddModelErrorsToBag(_errorBagService);

        context.Result = PrecognitionResponse.BuildActionResult(httpContext, _errorBagService);
    }

    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after the action; the result is set in OnActionExecuting.
    }
}
