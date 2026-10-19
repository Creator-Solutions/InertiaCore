using FluentValidation;
using InertiaCore.Extensions;
using FluentValidation.Results;
using InertiaCore.Precognition;
using InertiaCore.Services;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.FluentValidation;

/// <summary>
/// Shared logic for the endpoint filter and the MVC action filter: validation, error-key
/// mapping, the precognition-aware response, and the missing-validator error.
/// </summary>
internal static class FluentValidationSupport
{
    /// <summary>
    /// Runs the validator for the request with the request's cancellation token. When rule
    /// sets are given, only those sets (and the rules outside any set) run.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<string, string[]>> ValidateAsync<T>(
        IValidator<T> validator,
        T instance,
        string[]? ruleSets,
        CancellationToken cancellationToken)
    {
        var result = ruleSets is { Length: > 0 }
            ? await validator.ValidateAsync(instance, options => options.IncludeRuleSets(ruleSets), cancellationToken)
            : await validator.ValidateAsync(instance, cancellationToken);

        return ToErrors(result.Errors);
    }

    /// <summary>
    /// Maps failures to Inertia keys: camelCase segments, dotted paths and numeric indexes,
    /// the same format the DataAnnotations path produces. Class-level failures (no property
    /// name) use the empty key, as DataAnnotations does.
    /// </summary>
    internal static IReadOnlyDictionary<string, string[]> ToErrors(IEnumerable<ValidationFailure> failures)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var failure in failures)
        {
            var key = InertiaValidationResponse.NormalizeKey(failure.PropertyName ?? string.Empty);
            if (!errors.TryGetValue(key, out var messages))
                errors[key] = messages = new List<string>();

            messages.Add(failure.ErrorMessage);
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Builds the response for failed validation. A precognitive request always gets the
    /// Precognition response (204 or 422), filtered by <c>Precognition-Validate-Only</c>.
    /// Other requests get the Inertia response: a 303 with flashed errors for Inertia requests
    /// when a flash provider exists, otherwise a 422.
    /// </summary>
    internal static IResult Respond(HttpContext httpContext, IReadOnlyDictionary<string, string[]> errors)
    {
        var errorBagService = httpContext.RequestServices.GetRequiredService<IErrorBagService>();

        if (httpContext.IsPrecognitionRequest())
        {
            if (errors.Count > 0)
                errorBagService.AddErrors(errorBagService.CurrentBagName, errors.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));

            return PrecognitionResponse.BuildResult(httpContext, errorBagService);
        }

        return InertiaValidationResponse.Create(httpContext, errorBagService, errors);
    }

    /// <summary>
    /// Throws a clear error when no <see cref="IValidator{T}"/> is registered for the request type.
    /// </summary>
    internal static InvalidOperationException MissingValidator(Type requestType) =>
        new(
            $"No FluentValidation validator is registered for '{requestType.FullName}'. " +
            $"Register IValidator<{requestType.Name}>, for example: " +
            $"services.AddScoped<IValidator<{requestType.Name}>, {requestType.Name}Validator>(), " +
            "or add all validators with AddValidatorsFromAssemblyContaining.");

    /// <summary>
    /// Checks at endpoint build time that a validator is registered. Skipped when the host does
    /// not expose <see cref="IServiceProviderIsService"/>; the request-time check still applies.
    /// </summary>
    internal static void EnsureRegistered(IServiceProvider services, Type requestType)
    {
        var isService = services.GetService<IServiceProviderIsService>();
        if (isService is not null && !isService.IsService(typeof(IValidator<>).MakeGenericType(requestType)))
            throw MissingValidator(requestType);
    }
}
