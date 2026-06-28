using System.Runtime.CompilerServices;
using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Props;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

[assembly: InternalsVisibleTo("InertiaCoreTests")]

namespace InertiaCore;

public static class Inertia
{
    private static IResponseFactory _factory = default!;
    private static IHttpContextAccessor? _contextAccessor;
    private static readonly Dictionary<string, object?> _sharedData = new();

    internal static void UseFactory(IResponseFactory factory, IHttpContextAccessor? contextAccessor = null)
    {
        _factory = factory;
        _contextAccessor = contextAccessor;
    }

    internal static void ClearSharedData() => _sharedData.Clear();

    internal static Dictionary<string, object?> GetSharedData() => new(_sharedData);

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
    
    public static Response Render(string component, object? props = null) => _factory.Render(component, props);
    
    public static Task<IHtmlContent> Head(dynamic model) => _factory.Head(model);
    
    public static Task<IHtmlContent> Html(dynamic model) => _factory.Html(model);
    
    public static void Version(string? version) => _factory.Version(version);
    
    public static void Version(Func<string?> version) => _factory.Version(version);
    
    public static string? GetVersion() => _factory.GetVersion();
    
    public static LocationResult Location(string url) => _factory.Location(url);
    
    public static void Share(string key, object? value) => _sharedData[key.ToCamelCase()] = value;
    
    public static void Share(IDictionary<string, object?> data)
    {
        foreach (var (key, value) in data)
            _sharedData[key.ToCamelCase()] = value;
    }
    
    public static AlwaysProp Always(string value) => _factory.Always(value);
    
    public static AlwaysProp Always(Func<string> callback) => _factory.Always(callback);
    
    public static AlwaysProp Always(Func<Task<object?>> callback) => _factory.Always(callback);
    
    public static LazyProp Lazy(Func<object?> callback) => _factory.Lazy(callback);
    
    public static LazyProp Lazy(Func<Task<object?>> callback) => _factory.Lazy(callback);
}
