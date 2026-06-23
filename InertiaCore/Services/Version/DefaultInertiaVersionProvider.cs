namespace InertiaCore.Services.Version;

/// <summary>
/// A version provider that returns a static version string.
/// </summary>
public class DefaultInertiaVersionProvider : IInertiaVersionProvider
{
    private readonly string _version;

    /// <summary>
    /// Initializes a new instance of <see cref="DefaultInertiaVersionProvider"/>.
    /// </summary>
    /// <param name="version">The static version string.</param>
    public DefaultInertiaVersionProvider(string version)
    {
        _version = version ?? throw new ArgumentNullException(nameof(version));
    }

    /// <inheritdoc />
    public string GetVersion() => _version;
}
