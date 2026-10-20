using InertiaCore.Models;
using Microsoft.Extensions.Options;

namespace InertiaCore.Ssr;

/// <summary>
/// Validates <see cref="InertiaSsrOptions"/> (as nested on <see cref="InertiaOptions"/>) when the
/// managed SSR process is enabled. Registered with <c>ValidateOnStart</c> so a misconfiguration
/// fails fast at application start.
/// </summary>
internal sealed class InertiaSsrOptionsValidator : IValidateOptions<InertiaOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, InertiaOptions options)
    {
        var ssr = options.Ssr;

        if (!ssr.Managed)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

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

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
