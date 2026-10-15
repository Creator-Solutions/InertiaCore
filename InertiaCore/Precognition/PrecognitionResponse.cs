using InertiaCore.Extensions;
using InertiaCore.Services;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCore.Precognition;

/// <summary>
/// Builds the shared Precognition responses (204 success / 422 failure, headers and
/// <c>Vary</c>) so the MVC action filter and the Minimal API endpoint filter do not
/// duplicate protocol logic.
/// </summary>
internal static class PrecognitionResponse
{
    private const string VaryValue = "Precognition";

    /// <summary>
    /// Reads the current request's errors from the error bag, filtered by the
    /// <c>Precognition-Validate-Only</c> header when present.
    /// </summary>
    internal static IReadOnlyDictionary<string, string[]> GetFilteredErrors(
        HttpContext context,
        IErrorBagService errorBagService)
    {
        var errors = errorBagService.GetErrors(errorBagService.CurrentBagName)
                     ?? new Dictionary<string, string[]>();

        var validateOnly = context.GetPrecognitionValidateOnly();
        if (validateOnly.Count == 0)
            return errors;

        return errors
            .Where(pair => validateOnly.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Builds the 422 body in the same shape as the Inertia <c>errors</c> prop:
    /// flattened for the default bag, nested under the bag name otherwise.
    /// </summary>
    internal static object BuildErrorBody(IErrorBagService errorBagService, IReadOnlyDictionary<string, string[]> errors)
    {
        if (errorBagService.CurrentBagName == ErrorBagService.DefaultBagName)
        {
            return new Dictionary<string, object?>
            {
                ["errors"] = errors.ToDictionary(pair => pair.Key, pair => (object?)pair.Value)
            };
        }

        return new Dictionary<string, object?>
        {
            ["errors"] = new Dictionary<string, object?>
            {
                [errorBagService.CurrentBagName] = errors
            }
        };
    }

    /// <summary>
    /// Applies the <c>Precognition</c> (and, on success, <c>Precognition-Success</c>)
    /// response headers and appends <c>Vary: Precognition</c>.
    /// </summary>
    internal static void ApplyHeaders(HttpContext context, bool success)
    {
        var response = context.Response;
        response.Headers[InertiaHeader.Precognition] = "true";

        if (success)
            response.Headers[InertiaHeader.PrecognitionSuccess] = "true";

        AppendVary(response);
    }

    /// <summary>
    /// Appends <c>Precognition</c> to the <c>Vary</c> header without removing existing
    /// values (for example <c>X-Inertia</c> or <c>X-Inertia-Partial-Data</c>).
    /// </summary>
    private static void AppendVary(HttpResponse response)
    {
        if (response.Headers.TryGetValue("Vary", out var existing) && existing.Count > 0)
        {
            var current = existing.ToString();
            var alreadyPresent = current
                .Split(',')
                .Any(value => string.Equals(value.Trim(), VaryValue, StringComparison.OrdinalIgnoreCase));

            if (alreadyPresent)
                return;

            response.Headers["Vary"] = current + ", " + VaryValue;
            return;
        }

        response.Headers["Vary"] = VaryValue;
    }

    /// <summary>
    /// Builds the MVC <see cref="IActionResult"/> for a Precognition request.
    /// </summary>
    internal static IActionResult BuildActionResult(HttpContext context, IErrorBagService errorBagService)
    {
        var errors = GetFilteredErrors(context, errorBagService);
        var success = errors.Count == 0;
        ApplyHeaders(context, success);

        if (success)
            return new StatusCodeResult(StatusCodes.Status204NoContent);

        return new PrecognitionJsonResult(BuildErrorBody(errorBagService, errors));
    }

    /// <summary>
    /// Builds the Minimal API <see cref="IResult"/> for a Precognition request.
    /// </summary>
    internal static IResult BuildResult(HttpContext context, IErrorBagService errorBagService)
    {
        var errors = GetFilteredErrors(context, errorBagService);
        var success = errors.Count == 0;
        ApplyHeaders(context, success);

        return success
            ? Results.StatusCode(StatusCodes.Status204NoContent)
            : new PrecognitionJsonResult(BuildErrorBody(errorBagService, errors));
    }
}
