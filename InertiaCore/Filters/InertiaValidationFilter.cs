using System;
using InertiaCore.Extensions;
using InertiaCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InertiaCore.Filters;

/// <summary>
/// Action filter to automatically add controller ModelState validation errors to the Inertia error bag.
/// </summary>
public class InertiaValidationFilter : IActionFilter
{
    private readonly IErrorBagService _errorBagService;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaValidationFilter"/>.
    /// </summary>
    /// <param name="errorBagService">The error bag service.</param>
    public InertiaValidationFilter(IErrorBagService errorBagService)
    {
        _errorBagService = errorBagService ?? throw new ArgumentNullException(nameof(errorBagService));
    }

    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // No action needed before executing the action.
    }

    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Controller is ControllerBase controller)
        {
            controller.ModelState.AddModelErrorsToBag(_errorBagService);
        }
    }
}
