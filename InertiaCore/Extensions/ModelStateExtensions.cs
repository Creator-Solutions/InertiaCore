using System;
using System.Collections.Generic;
using System.Linq;
using InertiaCore.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InertiaCore.Extensions;

/// <summary>
/// Extension methods for <see cref="ModelStateDictionary"/> to support error bags.
/// </summary>
public static class ModelStateExtensions
{
    /// <summary>
    /// Adds validation errors from the model state to the specified error bag.
    /// </summary>
    /// <param name="modelState">The model state dictionary containing validation errors.</param>
    /// <param name="errorBagService">The error bag service.</param>
    /// <param name="bagName">The name of the target error bag. If null, the current bag name is used.</param>
    public static void AddModelErrorsToBag(
        this ModelStateDictionary modelState,
        IErrorBagService errorBagService,
        string? bagName = null)
    {
        if (modelState == null) throw new ArgumentNullException(nameof(modelState));
        if (errorBagService == null) throw new ArgumentNullException(nameof(errorBagService));

        var targetBagName = bagName ?? errorBagService.CurrentBagName;

        if (!modelState.IsValid)
        {
            var errors = modelState
                .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key.ToCamelCase(),
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            errorBagService.AddErrors(targetBagName, errors);
        }
        else
        {
            errorBagService.ClearErrors(targetBagName);
        }
    }
}
