using System.Linq;
using InertiaCore.Extensions;
using InertiaCore.Middleware;
using InertiaCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Validation;

/// <summary>
/// Builds the response for failed endpoint validation. Shared by the endpoint filter
/// and the .NET 10 problem-details writer so both produce the same result.
/// </summary>
internal static class InertiaValidationResponse
{
    /// <summary>
    /// Records the errors in the current error bag and returns the result to send.
    /// Inertia requests get a <c>303 See Other</c> back to the referrer, with the errors
    /// flashed, when a flash provider is registered. Everything else gets a <c>422</c>.
    /// </summary>
    internal static IResult Create(HttpContext httpContext, IErrorBagService errorBagService, IReadOnlyDictionary<string, string[]> errors)
    {
        var bagName = errorBagService.CurrentBagName;
        var errorDictionary = errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        errorBagService.AddErrors(bagName, errorDictionary);

        if (httpContext.IsInertiaRequest())
        {
            var flashProvider = httpContext.RequestServices.GetService<IInertiaFlashProvider>();
            if (flashProvider is not null)
            {
                ValidationErrorFlash.Store(flashProvider, bagName, errorDictionary);
                httpContext.Response.Headers.Location = InertiaMiddleware.GetSafeBackUrl(httpContext);
                return Results.StatusCode(StatusCodes.Status303SeeOther);
            }
        }

        // Non-Inertia requests go through IProblemDetailsService so registered writers apply.
        // Inertia requests are written directly: the Inertia writer itself calls this method, so
        // routing back through the service would recurse.
        return new ValidationProblemResult(errorDictionary, viaProblemDetailsService: !httpContext.IsInertiaRequest());
    }

    /// <summary>
    /// Converts framework keys (for example <c>Customer.Name</c> or <c>Items[0].Name</c>) to
    /// the Inertia key format: camelCase segments joined by dots, numeric indexes kept.
    /// </summary>
    internal static string NormalizeKey(string key)
    {
        var segments = key
            .Replace('[', '.')
            .Replace(']', '.')
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join('.', segments.Select(segment =>
            int.TryParse(segment, out _) ? segment : segment.ToCamelCase()));
    }
}
