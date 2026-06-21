using System.Runtime.CompilerServices;
using InertiaCore.Contracts;
using InertiaCore.Props;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

[assembly: InternalsVisibleTo("InertiaCoreTests")]

namespace InertiaCore;

/// <summary>
/// Static facade for Inertia.js operations.
/// <para>
/// This class is deprecated. Inject <see cref="IInertia"/> via constructor injection
/// instead. This static facade will be removed in a future major version.
/// </para>
/// <code>
/// public class MyController : Controller
/// {
///     private readonly IInertia _inertia;
///     public MyController(IInertia inertia) => _inertia = inertia;
///     public IActionResult Index() => await _inertia.Render("Page", new { });
/// }
/// </code>
/// </summary>
[Obsolete("The static Inertia facade is deprecated. Inject IInertia via constructor injection instead.")]
public static class Inertia
{
    private static IResponseFactory _factory = default!;
    private static IHttpContextAccessor? _contextAccessor;

    internal static void UseFactory(IResponseFactory factory, IHttpContextAccessor? contextAccessor = null)
    {
        _factory = factory;
        _contextAccessor = contextAccessor;
    }

    private static IInertia ResolveService()
    {
        if (_contextAccessor?.HttpContext != null)
        {
            var service = _contextAccessor.HttpContext.RequestServices.GetService(typeof(IInertia));
            if (service is IInertia inertia)
                return inertia;
        }

        // Fallback: wrap the internal factory
        var state = new Services.InertiaState();
        var options = Microsoft.Extensions.Options.Options.Create(new Models.InertiaOptions());
        return new Services.InertiaService(_factory, state, options);
    }

    /// <summary>
    /// Render an Inertia page component.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static Response Render(string component, object? props = null) => _factory.Render(component, props);

    /// <summary>
    /// Render the SSR head content.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static Task<IHtmlContent> Head(dynamic model) => _factory.Head(model);

    /// <summary>
    /// Render the SSR body / non-SSR placeholder HTML.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static Task<IHtmlContent> Html(dynamic model) => _factory.Html(model);

    /// <summary>
    /// Set the asset version to a fixed string.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static void Version(string? version) => _factory.Version(version);

    /// <summary>
    /// Set the asset version via a lazily-evaluated function.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static void Version(Func<string?> version) => _factory.Version(version);

    /// <summary>
    /// Get the currently configured asset version.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static string? GetVersion() => _factory.GetVersion();

    /// <summary>
    /// Return an external redirect via Inertia Location response.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static LocationResult Location(string url) => _factory.Location(url);

    /// <summary>
    /// Share a value across all Inertia responses for the current request.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static void Share(string key, object? value) => _factory.Share(key, value);

    /// <summary>
    /// Share multiple values across all Inertia responses for the current request.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static void Share(IDictionary<string, object?> data) => _factory.Share(data);

    /// <summary>
    /// Create an AlwaysProp.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static AlwaysProp Always(string value) => _factory.Always(value);

    /// <summary>
    /// Create an AlwaysProp with a synchronous callback.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static AlwaysProp Always(Func<string> callback) => _factory.Always(callback);

    /// <summary>
    /// Create an AlwaysProp with an asynchronous callback.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static AlwaysProp Always(Func<Task<object?>> callback) => _factory.Always(callback);

    /// <summary>
    /// Create a LazyProp.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static LazyProp Lazy(Func<object?> callback) => _factory.Lazy(callback);

    /// <summary>
    /// Create a LazyProp with an asynchronous callback.
    /// </summary>
    [Obsolete("Inject IInertia via constructor injection instead.")]
    public static LazyProp Lazy(Func<Task<object?>> callback) => _factory.Lazy(callback);
}
