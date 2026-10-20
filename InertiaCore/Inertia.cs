using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Middleware;
using InertiaCore.Models;
using InertiaCore.Props;
using InertiaCore.Services;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("InertiaCoreTests")]
[assembly: InternalsVisibleTo("InertiaCore.FluentValidation")]

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

    /// <summary>
    /// Renders the page component associated with <typeparamref name="TProps"/>.
    /// The component name is resolved from <see cref="InertiaPageRegistry"/> by the
    /// type, which must be declared with <c>[InertiaPage("...")]</c>.
    /// </summary>
    /// <typeparam name="TProps">The props record/class type declared with <c>[InertiaPage]</c>.</typeparam>
    /// <param name="props">The props instance.</param>
    /// <returns>The Inertia response.</returns>
    public static Response Render<TProps>(TProps props) =>
        GetFactory().Render(InertiaPageRegistry.ResolveComponent(typeof(TProps)), props);

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

    /// <summary>
    /// Shares a flash value that is shown on the next Inertia render and cleared after it.
    /// Flash values are exposed under the <c>flash</c> prop, for example
    /// <c>page.props.flash.success</c>.
    /// </summary>
    /// <param name="key">The flash key (will be camelCased).</param>
    /// <param name="value">The value to flash.</param>
    /// <exception cref="InvalidOperationException">No HTTP request is active.</exception>
    public static void Flash(string key, object? value)
    {
        if (TryGetInertiaService(out var inertia))
        {
            inertia.Flash(key, value);
            return;
        }

        throw new InvalidOperationException(
            "Inertia.Flash cannot be used outside of an active HTTP request. " +
            "Inject InertiaCore.Contracts.IInertia via dependency injection instead of using the static Inertia facade.");
    }

    /// <summary>
    /// Discards the flash data stored for the current request, so nothing is shown on the
    /// next Inertia render.
    /// </summary>
    /// <exception cref="InvalidOperationException">No HTTP request is active.</exception>
    public static void ClearFlash()
    {
        if (RequestServices?.GetService(typeof(IInertiaFlashProvider)) is IInertiaFlashProvider provider)
        {
            provider.Clear();
            return;
        }

        throw new InvalidOperationException(
            "Inertia.ClearFlash cannot be used outside of an active HTTP request.");
    }

    /// <summary>
    /// Rotates the Inertia <c>XSRF-TOKEN</c> cookie for the current request. Call this
    /// after signing a user in or out so a token issued to the previous principal cannot
    /// be replayed against the new session.
    /// </summary>
    /// <remarks>
    /// Requires the anti-forgery bridge to be enabled
    /// (<c>InertiaOptions.Antiforgery.Enabled = true</c> and <c>UseInertiaAntiforgery()</c>).
    /// The new token is written when the response starts.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No HTTP request is active, or the anti-forgery bridge is not enabled.
    /// </exception>
    public static void RotateAntiforgery()
    {
        var context = _contextAccessor?.HttpContext;
        if (context is null)
        {
            throw new InvalidOperationException(
                "Inertia.RotateAntiforgery cannot be used outside of an active HTTP request.");
        }

        var options = context.RequestServices.GetService(typeof(IOptions<InertiaOptions>)) as IOptions<InertiaOptions>;
        if (options?.Value.Antiforgery.Enabled != true
            || context.RequestServices.GetService(typeof(IAntiforgery)) is not IAntiforgery antiforgery)
        {
            throw new InvalidOperationException(
                "Inertia.RotateAntiforgery requires the anti-forgery bridge. " +
                "Enable InertiaOptions.Antiforgery.Enabled and call UseInertiaAntiforgery().");
        }

        var settings = options.Value.Antiforgery;
        var token = InertiaAntiforgeryMiddleware.GetToken(context, antiforgery);

        // Replaces the pending token, so the single scheduled write emits the rotated value.
        InertiaAntiforgeryMiddleware.ScheduleCookie(context, settings, token);
    }

    public static AlwaysProp Always(string value) => GetFactory().Always(value);

    public static AlwaysProp Always(Func<string> callback) => GetFactory().Always(callback);

    public static AlwaysProp Always(Func<Task<object?>> callback) => GetFactory().Always(callback);

    public static LazyProp Lazy(Func<object?> callback) => GetFactory().Lazy(callback);

    public static LazyProp Lazy(Func<Task<object?>> callback) => GetFactory().Lazy(callback);

    /// <summary>
    /// Creates a deferred prop that is excluded from the initial page load and resolved
    /// only when requested. Deferred props are announced through <c>deferredProps</c>.
    /// See: https://inertiajs.com/deferred-props
    /// </summary>
    public static DeferredProp Defer(Func<object?> factory) => GetFactory().Defer(factory);

    /// <summary>
    /// Creates a deferred prop in the given group.
    /// </summary>
    public static DeferredProp Defer(Func<object?> factory, string group) => GetFactory().Defer(factory, group);

    /// <summary>
    /// Creates a deferred prop in the given group, optionally rescuing resolution errors.
    /// </summary>
    public static DeferredProp Defer(Func<object?> factory, string group, bool rescue) =>
        GetFactory().Defer(factory, group, rescue);

    /// <summary>
    /// Creates a deferred prop with an asynchronous factory.
    /// </summary>
    public static DeferredProp Defer(Func<Task<object?>> factory) => GetFactory().Defer(factory);

    /// <summary>
    /// Creates a deferred prop in the given group with an asynchronous factory.
    /// </summary>
    public static DeferredProp Defer(Func<Task<object?>> factory, string group) =>
        GetFactory().Defer(factory, group);

    /// <summary>
    /// Creates a deferred prop in the given group with an asynchronous factory,
    /// optionally rescuing resolution errors.
    /// </summary>
    public static DeferredProp Defer(Func<Task<object?>> factory, string group, bool rescue) =>
        GetFactory().Defer(factory, group, rescue);

    /// <summary>
    /// Creates a once prop that is resolved a single time and remembered by the client
    /// across subsequent navigations.
    /// See: https://inertiajs.com/once-props
    /// </summary>
    public static OnceProp Once(Func<object?> callback) => GetFactory().Once(callback);

    /// <summary>
    /// Creates a once prop with an asynchronous factory.
    /// </summary>
    public static OnceProp Once(Func<Task<object?>> callback) => GetFactory().Once(callback);

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

    /// <summary>
    /// Creates an infinite-scroll prop from a paginated value. The inner array is
    /// appended or prepended during partial reloads based on the
    /// <c>X-Inertia-Infinite-Scroll-Merge-Intent</c> request header, and the cursor
    /// is emitted under <c>scrollProps</c>.
    /// </summary>
    public static ScrollProp Scroll(object? items, ScrollMetadata metadata, string wrapper = "data") =>
        GetFactory().Scroll(items, metadata, wrapper);

    /// <summary>
    /// Creates an infinite-scroll prop from a synchronous factory.
    /// </summary>
    public static ScrollProp Scroll(Func<object?> items, ScrollMetadata metadata, string wrapper = "data") =>
        GetFactory().Scroll(items, metadata, wrapper);

    /// <summary>
    /// Creates an infinite-scroll prop from an asynchronous factory.
    /// </summary>
    public static ScrollProp Scroll(Func<Task<object?>> items, ScrollMetadata metadata, string wrapper = "data") =>
        GetFactory().Scroll(items, metadata, wrapper);

    /// <summary>
    /// Creates a prop whose value is resolved from the current request's services, so the
    /// data for a page can come from an application service instead of a route-handler
    /// lambda. The instance is resolved from <c>HttpContext.RequestServices</c> for every
    /// request and may depend on scoped services.
    /// </summary>
    /// <typeparam name="T">The service type whose instance is used as the prop value.</typeparam>
    public static IInertiaProp<T> Prop<T>() where T : class => GetFactory().Prop<T>();

    /// <summary>
    /// Creates a service prop that is excluded from the initial page load and only resolved
    /// on subsequent partial reloads.
    /// </summary>
    /// <typeparam name="T">The service type whose instance is used as the prop value.</typeparam>
    public static LazyProp Lazy<T>() where T : class => GetFactory().Lazy<T>();

    /// <summary>
    /// Creates a service prop that is excluded from the initial full page load and resolved
    /// only when requested. Deferred props are announced through <c>deferredProps</c>.
    /// </summary>
    /// <typeparam name="T">The service type whose instance is used as the prop value.</typeparam>
    /// <param name="group">The defer group. Defaults to <c>"default"</c>.</param>
    public static DeferredProp Defer<T>(string group = "default") where T : class => GetFactory().Defer<T>(group);

    /// <summary>
    /// Creates a service prop that is resolved a single time and remembered by the client
    /// across subsequent navigations.
    /// </summary>
    /// <typeparam name="T">The service type whose instance is used as the prop value.</typeparam>
    public static OnceProp Once<T>() where T : class => GetFactory().Once<T>();
}
