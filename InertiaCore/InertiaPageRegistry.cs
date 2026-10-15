using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace InertiaCore;

/// <summary>
/// Thread-safe registry of Inertia page component names known to the application.
/// <para>
/// Pages declared with <c>[assembly: InertiaPage("...")]</c> (or on a class or props
/// record) are registered automatically by the InertiaCore source generator through a
/// module initializer. The registry can also be populated manually via
/// <see cref="Register(string)"/> / <see cref="Register(Type, string)"/>.
/// </para>
/// </summary>
public static class InertiaPageRegistry
{
    private static readonly ConcurrentDictionary<string, byte> Pages = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<Type, string> ComponentsByType = new();

    /// <summary>
    /// Registers a page component name. Duplicate registrations are ignored.
    /// </summary>
    public static void Register(string componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName)) return;
        Pages.TryAdd(componentName, 0);
    }

    /// <summary>
    /// Associates a props type with a page component name, so the component can be
    /// resolved by type via <see cref="GetComponent"/>. Duplicate registrations keep
    /// the first mapping.
    /// </summary>
    /// <param name="type">The props type declared with <c>[InertiaPage]</c>.</param>
    /// <param name="componentName">The component name to resolve for the type.</param>
    public static void Register(Type? type, string componentName)
    {
        if (type is null || string.IsNullOrWhiteSpace(componentName)) return;
        ComponentsByType.TryAdd(type, componentName);
    }

    /// <summary>
    /// Returns whether the given component name has been registered.
    /// </summary>
    public static bool IsRegistered(string componentName) => Pages.ContainsKey(componentName);

    /// <summary>
    /// Resolves the component name registered for the given props type, or
    /// <see langword="null"/> when the type has no mapping.
    /// </summary>
    /// <param name="type">The props type to resolve.</param>
    /// <returns>The component name, or <see langword="null"/>.</returns>
    public static string? GetComponent(Type type) =>
        type is not null && ComponentsByType.TryGetValue(type, out var component) ? component : null;

    /// <summary>
    /// Attempts to resolve the component name registered for the given props type.
    /// </summary>
    /// <param name="type">The props type to resolve.</param>
    /// <param name="componentName">The resolved component name, when found.</param>
    /// <returns><see langword="true"/> when a mapping exists.</returns>
    public static bool TryGetComponent(Type type, [NotNullWhen(true)] out string? componentName)
    {
        if (type is not null && ComponentsByType.TryGetValue(type, out var component))
        {
            componentName = component;
            return true;
        }

        componentName = null;
        return false;
    }

    /// <summary>
    /// A snapshot of all registered component names.
    /// </summary>
    public static IReadOnlyCollection<string> RegisteredPages => Pages.Keys.ToArray();

    /// <summary>
    /// Resolves the component name for a props type, throwing when no mapping exists.
    /// </summary>
    internal static string ResolveComponent(Type type) =>
        GetComponent(type)
        ?? throw new InvalidOperationException(
            $"No [InertiaPage] component is registered for the props type '{type.FullName}'. " +
            $"Declare the type with [InertiaPage(\"...\")] so it can be resolved by Render<{type.Name}>(...).");
}
