using InertiaCore.Props;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCore.Contracts;

/// <summary>
/// Main Inertia.js service interface for server-side adapter operations.
/// Inject this interface into your controllers and services via constructor injection.
/// </summary>
public interface IInertia
{
    /// <summary>
    /// Render an Inertia page component with the given props.
    /// </summary>
    /// <param name="component">The JavaScript component name (e.g. "Admin/Users").</param>
    /// <param name="props">Optional props object, anonymous type, or dictionary.</param>
    /// <returns>An <see cref="IActionResult"/> that returns either a JSON envelope or a full page view.</returns>
    Task<IActionResult> Render(string component, object? props = null);

    /// <summary>
    /// Return an external redirect via an Inertia Location response (409 + X-Inertia-Location).
    /// </summary>
    IActionResult Location(string url);

    /// <summary>
    /// Share a value across all Inertia responses for the current request.
    /// </summary>
    /// <param name="key">The prop key (will be camelCased).</param>
    /// <param name="value">The value to share.</param>
    void Share(string key, object? value);

    /// <summary>
    /// Share multiple values across all Inertia responses for the current request.
    /// </summary>
    void Share(IDictionary<string, object?> data);

    /// <summary>
    /// Share a flash prop that will only be available for the current request.
    /// Flash props are merged after shared props but before component props.
    /// </summary>
    void Flash(string key, object? value);

    /// <summary>
    /// Set the asset version to a fixed string.
    /// </summary>
    void Version(string? version);

    /// <summary>
    /// Set the asset version to a lazily-evaluated function.
    /// </summary>
    void Version(Func<string?> versionFunc);

    /// <summary>
    /// Get the currently configured asset version.
    /// </summary>
    string? GetVersion();

    /// <summary>
    /// Create an AlwaysProp that will always be included in all responses,
    /// even during partial reloads where its key would otherwise be excluded.
    /// </summary>
    AlwaysProp Always(object? value);

    /// <summary>
    /// Create an AlwaysProp with a synchronous factory.
    /// </summary>
    AlwaysProp Always(Func<object?> callback);

    /// <summary>
    /// Create an AlwaysProp with an asynchronous factory.
    /// </summary>
    AlwaysProp Always(Func<Task<object?>> callback);

    /// <summary>
    /// Create a LazyProp that is excluded from the initial page load
    /// and only resolved on subsequent partial reloads.
    /// </summary>
    LazyProp Lazy(Func<object?> callback);

    /// <summary>
    /// Create a LazyProp with an asynchronous factory.
    /// </summary>
    LazyProp Lazy(Func<Task<object?>> callback);

    /// <summary>
    /// Create a DeferredProp that is excluded from the initial full page load
    /// and resolved only when explicitly requested via X-Inertia-Partial-Data.
    /// </summary>
    DeferredProp Defer(Func<object?> factory);

    /// <summary>
    /// Create a DeferredProp with an asynchronous factory.
    /// </summary>
    DeferredProp Defer(Func<Task<object?>> factory);

    /// <summary>
    /// Set whether to encrypt history state on the client side.
    /// </summary>
    void EncryptHistory(bool encrypt = true);

    /// <summary>
    /// Set whether to clear history state on the client side.
    /// </summary>
    void ClearHistory(bool clear = true);
}
