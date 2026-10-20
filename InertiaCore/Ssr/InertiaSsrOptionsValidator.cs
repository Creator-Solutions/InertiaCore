using InertiaCore.Models;
using Microsoft.Extensions.Options;

namespace InertiaCore.Ssr;

/// <summary>
/// Validates the SSR-related sections of <see cref="InertiaOptions"/>: the managed process
/// options when <see cref="InertiaSsrOptions.Managed"/> is enabled, and the resilience and
/// health URL settings when <see cref="InertiaOptions.SsrEnabled"/> is enabled. Registered
/// with <c>ValidateOnStart</c> so a misconfiguration fails fast at application start.
/// </summary>
internal sealed class InertiaSsrOptionsValidator : IValidateOptions<InertiaOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, InertiaOptions options)
    {
        var failures = new List<string>();

        ValidateManaged(options.Ssr, failures);

        if (options.SsrEnabled)
            ValidateResilience(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateManaged(InertiaSsrOptions ssr, List<string> failures)
    {
        if (!ssr.Managed)
            return;

        if (string.IsNullOrWhiteSpace(ssr.NodePath))
            failures.Add($"{nameof(InertiaSsrOptions.NodePath)} must not be empty.");

        if (string.IsNullOrWhiteSpace(ssr.ScriptPath))
            failures.Add($"{nameof(InertiaSsrOptions.ScriptPath)} must not be empty.");

        if (ssr.Port is < 1 or > 65535)
            failures.Add($"{nameof(InertiaSsrOptions.Port)} must be between 1 and 65535 (was {ssr.Port}).");

        if (ssr.StartupTimeout <= TimeSpan.Zero)
            failures.Add($"{nameof(InertiaSsrOptions.StartupTimeout)} must be greater than zero.");

        if (ssr.ShutdownTimeout <= TimeSpan.Zero)
            failures.Add($"{nameof(InertiaSsrOptions.ShutdownTimeout)} must be greater than zero.");

        if (ssr.InitialBackoff <= TimeSpan.Zero)
            failures.Add($"{nameof(InertiaSsrOptions.InitialBackoff)} must be greater than zero.");

        if (ssr.MaxBackoff < ssr.InitialBackoff)
            failures.Add($"{nameof(InertiaSsrOptions.MaxBackoff)} must be greater than or equal to {nameof(InertiaSsrOptions.InitialBackoff)}.");

        if (ssr.MaxRestarts < -1)
            failures.Add($"{nameof(InertiaSsrOptions.MaxRestarts)} must be -1 (unlimited) or greater (was {ssr.MaxRestarts}).");
    }

    private static void ValidateResilience(InertiaOptions options, List<string> failures)
    {
        var resilience = options.SsrResilience;

        if (options.SsrTimeout <= TimeSpan.Zero)
            failures.Add($"{nameof(InertiaOptions.SsrTimeout)} must be greater than zero.");

        // The resilience handler requires at least one retry attempt (retries cannot be
        // disabled); cap at 10 so a misconfigured budget cannot hammer the SSR server.
        if (resilience.MaxRetryAttempts is < 1 or > 10)
            failures.Add($"{nameof(InertiaSsrResilienceOptions.MaxRetryAttempts)} must be between 1 and 10 (was {resilience.MaxRetryAttempts}).");

        if (resilience.RetryBaseDelay <= TimeSpan.Zero)
            failures.Add($"{nameof(InertiaSsrResilienceOptions.RetryBaseDelay)} must be greater than zero.");

        if (resilience.AttemptTimeout is { } attemptTimeout)
        {
            if (attemptTimeout <= TimeSpan.Zero)
                failures.Add($"{nameof(InertiaSsrResilienceOptions.AttemptTimeout)} must be greater than zero when set.");

            if (attemptTimeout > options.SsrTimeout)
                failures.Add($"{nameof(InertiaSsrResilienceOptions.AttemptTimeout)} must not exceed {nameof(InertiaOptions.SsrTimeout)} (the overall request budget).");
        }

        if (resilience.CircuitBreakerFailureRatio is <= 0 or > 1)
            failures.Add($"{nameof(InertiaSsrResilienceOptions.CircuitBreakerFailureRatio)} must be greater than 0 and at most 1 (was {resilience.CircuitBreakerFailureRatio}).");

        if (resilience.CircuitBreakerMinimumThroughput < 1)
            failures.Add($"{nameof(InertiaSsrResilienceOptions.CircuitBreakerMinimumThroughput)} must be at least 1 (was {resilience.CircuitBreakerMinimumThroughput}).");

        if (resilience.CircuitBreakerSamplingDuration <= TimeSpan.Zero)
            failures.Add($"{nameof(InertiaSsrResilienceOptions.CircuitBreakerSamplingDuration)} must be greater than zero.");

        // The resilience handler requires a break duration of at least 500 ms.
        if (resilience.CircuitBreakerBreakDuration < TimeSpan.FromMilliseconds(500))
            failures.Add($"{nameof(InertiaSsrResilienceOptions.CircuitBreakerBreakDuration)} must be at least 500 ms.");

        if (string.IsNullOrWhiteSpace(options.SsrHealthUrl))
        {
            // The health URL is derived from SsrUrl, so the render URL must be usable too.
            if (!string.IsNullOrWhiteSpace(options.SsrUrl) && !IsValidHttpUrl(options.SsrUrl))
            {
                failures.Add($"{nameof(InertiaOptions.SsrUrl)} must be an absolute http/https URL when {nameof(InertiaOptions.SsrHealthUrl)} is not set (was '{options.SsrUrl}').");
            }
        }
        else if (!IsValidHttpUrl(options.SsrHealthUrl))
        {
            failures.Add($"{nameof(InertiaOptions.SsrHealthUrl)} must be an absolute http/https URL (was '{options.SsrHealthUrl}').");
        }
    }

    private static bool IsValidHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
