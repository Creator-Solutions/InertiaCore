using System.Collections.Concurrent;

namespace InertiaCore;

/// <summary>
/// Thread-safe registry of Inertia page component names known to the application.
/// <para>
/// Pages declared with <c>[assembly: InertiaPage("...")]</c> (or on a class) are
/// registered automatically by the InertiaCore source generator through a module
/// initializer. The registry can also be populated manually via <see cref="Register"/>.
/// </para>
/// </summary>
public static class InertiaPageRegistry
{
    private static readonly ConcurrentDictionary<string, byte> Pages = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a page component name. Duplicate registrations are ignored.
    /// </summary>
    public static void Register(string componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName)) return;
        Pages.TryAdd(componentName, 0);
    }

    /// <summary>
    /// Returns whether the given component name has been registered.
    /// </summary>
    public static bool IsRegistered(string componentName) => Pages.ContainsKey(componentName);

    /// <summary>
    /// A snapshot of all registered component names.
    /// </summary>
    public static IReadOnlyCollection<string> RegisteredPages => Pages.Keys.ToArray();
}
