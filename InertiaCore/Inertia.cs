using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Props;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

[assembly: InternalsVisibleTo("InertiaCoreTests")]

namespace InertiaCore;

/// <summary>
/// Static convenience facade over the Inertia services.
/// <para>
/// All members resolve their dependencies from the <b>current request scope</b>.
/// They must be called from within an active HTTP request (for example from a
/// Razor view or controller). Prefer injecting <see cref="IInertia"/> via DI where
/// possible.
/// </para>
/// </summary>
public static class Inertia
{
    private static IHttpContextAccessor? _contextAccessor;

    /// <summary>
    /// Startup-only global shared props. This registry is thread-safe and is meant
    /// to be populated before the application starts. Request-scoped sharing via
    /// <see cref="IInertia.Share(string, object?)"/> never touches this registry.
    /// </summary>
    private static readonly ConcurrentDictionary<string, object?> SharedData = new();

    internal static void UseContextAccessor(IHttpContextAccessor? contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    internal static void ClearSharedData() => SharedData.Clear();

    internal static Dictionary<string, object?> GetSharedData() => new(SharedData);

    private static IServiceProvider? RequestServices => _contextAccessor?.HttpContext?.RequestServices;

    private static IResponseFactory GetFactory()
    {
        if (RequestServices?.GetService(typeof(IResponseFactory)) is IResponseFactory factory)
            return factory;

        throw new InvalidOperationException(
            "Inertia cannot be used outside of an active HTTP request. " +
            "Inject InertiaCore.Contracts.IInertia via dependency injection instead of using the static Inertia facade.");
    }

    private static bool TryGetInertiaService(out IInertia inertia)
    {
        if (RequestServices?.GetService(typeof(IInertia)) is IInertia service)
        {
            inertia = service;
            return true;
        }

        inertia = null!;
        return false;
    }

    public static Response Render(string component, object? props = null) => GetFactory().Render(component, props);

    public static Task<IHtmlContent> Head(dynamic model) => GetFactory().Head(model);

    public static Task<IHtmlContent> Html(dynamic model) => GetFactory().Html(model);

    public static void Version(string? version) => GetFactory().Version(version);

    public static void Version(Func<string?> version) => GetFactory().Version(version);

    public static string? GetVersion() => GetFactory().GetVersion();

    public static LocationResult Location(string url) => GetFactory().Location(url);

    /// <summary>
    /// Shares a value. When called within a request, the value is scoped to that
    /// request. When called outside a request (for example during startup), the
    /// value is registered globally for the lifetime of the application.
    /// </summary>
    public static void Share(string key, object? value)
    {
        var camelCased = key.ToCamelCase();

        if (TryGetInertiaService(out var inertia))
        {
            inertia.Share(camelCased, value);
            return;
        }

        SharedData[camelCased] = value;
    }

    /// <summary>
    /// Shares multiple values. See <see cref="Share(string, object?)"/> for scoping rules.
    /// </summary>
    public static void Share(IDictionary<string, object?> data)
    {
        if (TryGetInertiaService(out var inertia))
        {
            inertia.Share(data);
            return;
        }

        foreach (var (key, value) in data)
            SharedData[key.ToCamelCase()] = value;
    }

    public static AlwaysProp Always(string value) => GetFactory().Always(value);

    public static AlwaysProp Always(Func<string> callback) => GetFactory().Always(callback);

    public static AlwaysProp Always(Func<Task<object?>> callback) => GetFactory().Always(callback);

    public static LazyProp Lazy(Func<object?> callback) => GetFactory().Lazy(callback);

    public static LazyProp Lazy(Func<Task<object?>> callback) => GetFactory().Lazy(callback);

    /// <summary>
    /// Creates a prop whose value is merged with the existing client-side value
    /// during partial reloads instead of replacing it.
    /// </summary>
    public static MergeProp Merge(object? value) => GetFactory().Merge(value);

    /// <summary>
    /// Creates a prop with a synchronous factory whose value is merged with the
    /// existing client-side value during partial reloads.
    /// </summary>
    public static MergeProp Merge(Func<object?> callback) => GetFactory().Merge(callback);

    /// <summary>
    /// Creates a prop with an asynchronous factory whose value is merged with the
    /// existing client-side value during partial reloads.
    /// </summary>
    public static MergeProp Merge(Func<Task<object?>> callback) => GetFactory().Merge(callback);

    /// <summary>
    /// Creates a prop that is deep merged with the existing client-side value.
    /// </summary>
    public static MergeProp DeepMerge(object? value) => GetFactory().DeepMerge(value);

    /// <summary>
    /// Creates a prop with a synchronous factory that is deep merged with the
    /// existing client-side value.
    /// </summary>
    public static MergeProp DeepMerge(Func<object?> callback) => GetFactory().DeepMerge(callback);

    /// <summary>
    /// Creates a prop with an asynchronous factory that is deep merged with the
    /// existing client-side value.
    /// </summary>
    public static MergeProp DeepMerge(Func<Task<object?>> callback) => GetFactory().DeepMerge(callback);
}
