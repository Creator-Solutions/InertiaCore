using System;

namespace InertiaCore.Services.Version;

/// <summary>
/// A version provider that evaluates a delegate to get the version string.
/// </summary>
public class DelegateInertiaVersionProvider : IInertiaVersionProvider
{
    private readonly Func<string> _versionFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="DelegateInertiaVersionProvider"/>.
    /// </summary>
    /// <param name="versionFactory">The delegate factory that returns the version string.</param>
    public DelegateInertiaVersionProvider(Func<string> versionFactory)
    {
        _versionFactory = versionFactory ?? throw new ArgumentNullException(nameof(versionFactory));
    }

    /// <inheritdoc />
    public string GetVersion() => _versionFactory();
}
