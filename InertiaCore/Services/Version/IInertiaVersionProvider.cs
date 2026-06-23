namespace InertiaCore.Services.Version;

/// <summary>
/// Defines a provider for resolving the current Inertia asset version.
/// </summary>
public interface IInertiaVersionProvider
{
    /// <summary>
    /// Gets the current Inertia asset version.
    /// </summary>
    /// <returns>The current version string.</returns>
    string GetVersion();
}
