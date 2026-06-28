using System.Text.Json;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Props;
using InertiaCore.Services;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace InertiaCore;

public class Response : IActionResult, IResult
{
    private readonly string _component;
    private readonly Dictionary<string, object?> _props;
    private readonly string _rootView;
    private readonly string? _version;
    private readonly bool _encryptHistory;
    private readonly bool _clearHistory;
    private readonly InertiaState _state;
    private readonly JsonSerializerOptions _jsonOptions;

    private ActionContext? _context;
    private Page? _page;
    private IDictionary<string, object>? _viewData;

    internal Response(
        string component,
        Dictionary<string, object?> props,
        string rootView,
        string? version,
        bool encryptHistory,
        bool clearHistory,
        InertiaState state,
        JsonSerializerOptions jsonOptions)
    {
        _component = component;
        _props = props;
        _rootView = rootView;
        _version = version;
        _encryptHistory = encryptHistory;
        _clearHistory = clearHistory;
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
    }

    /// <summary>
    /// MVC / controller path — ActionContext is provided directly by the MVC pipeline.
    /// </summary>
    async Task IActionResult.ExecuteResultAsync(ActionContext context)
    {
        _context = context;
        await ProcessResponse();
        await GetResult().ExecuteResultAsync(_context);
    }

    /// <summary>
    /// Minimal API path — wraps the HttpContext in a minimal ActionContext so the
    /// existing processing pipeline works without modification.
    /// <para>
    /// For Inertia requests we write JSON directly to the response rather than going
    /// through <see cref="JsonResult.ExecuteResultAsync"/>, which requires
    /// <c>RequestServices</c> to be populated (not guaranteed in Minimal API hosts or
    /// unit tests using <c>DefaultHttpContext</c>).
    /// </para>
    /// <para>
    /// For non-Inertia (full page) requests we fall back to
    /// <see cref="ViewResult.ExecuteResultAsync"/> — the view engine is always available
    /// in a real host, and this path is never reached in unit tests since the view result
    /// type is asserted without executing it.
    /// </para>
    /// </summary>
    async Task IResult.ExecuteAsync(HttpContext httpContext)
    {
        var routeData = httpContext.GetRouteData() ?? new RouteData();
        _context = new ActionContext(
            httpContext,
            routeData,
            new ActionDescriptor(),
            new ModelStateDictionary()
        );

        await ProcessResponse();

        if (_context.IsInertiaRequest())
        {
            // Set headers via the shared GetJson() path so protocol behaviour
            // (X-Inertia, Vary, status 200) is identical to the MVC path.
            var jsonResult = GetJson();

            httpContext.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(jsonResult.Value, _jsonOptions);
            await httpContext.Response.WriteAsync(json);
        }
        else
        {
            // Full page load — delegate to MVC ViewResult which needs the view engine.
            // In a real Minimal API host, AddControllersWithViews / AddRazorPages will
            // have registered the required services. Unit tests assert on GetResult()
            // directly and never reach this branch via ExecuteAsync.
            await GetView().ExecuteResultAsync(_context);
        }
    }

    protected internal async Task ProcessResponse()
    {
        var props = await ResolveProperties();

        var page = new Page
        {
            Component = _component,
            Version = _version,
            Url = _context!.RequestedUri(),
            Props = props,
            EncryptHistory = _encryptHistory,
            ClearHistory = _clearHistory,
        };

        page.Props["errors"] = GetErrors();

        SetPage(page);
    }

    /// <summary>
    /// Resolve the properties for the response.
    /// </summary>
    private async Task<Dictionary<string, object?>> ResolveProperties()
    {
        var props = _props;

        props = ResolveSharedProps(props);
        props = ResolveFlashProps(props);
        props = ResolvePartialProperties(props);
        props = ResolveAlways(props);
        props = await ResolvePropertyInstances(props);

        return props;
    }

    /// <summary>
    /// Resolve `shared` props stored in InertiaState (migrated from HttpContext.Features).
    /// </summary>
    private Dictionary<string, object?> ResolveSharedProps(Dictionary<string, object?> props)
    {
        if (_state.SharedProps.Count == 0)
            return props;

        var result = new Dictionary<string, object?>(props.Count + _state.SharedProps.Count);
        foreach (var (key, value) in _state.SharedProps)
            result[key] = value;

        foreach (var (key, value) in props)
            result[key] = value;

        return result;
    }

    /// <summary>
    /// Resolve flash props stored in InertiaState.
    /// Flash props are merged after shared props but before component props.
    /// </summary>
    private Dictionary<string, object?> ResolveFlashProps(Dictionary<string, object?> props)
    {
        if (_state.FlashProps.Count == 0)
            return props;

        var result = new Dictionary<string, object?>(props.Count + _state.FlashProps.Count);
        foreach (var (key, value) in props)
            result[key] = value;

        foreach (var (key, value) in _state.FlashProps)
            result[key] = value;

        return result;
    }

    /// <summary>
    /// Resolve the `only` and `except` partial request props.
    /// </summary>
    private Dictionary<string, object?> ResolvePartialProperties(Dictionary<string, object?> props)
    {
        var isPartial = _context!.IsInertiaPartialComponent(_component);

        if (!isPartial)
            return props
                .Where(kv => kv.Value is not LazyProp and not DeferredProp)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

        props = props.ToDictionary(kv => kv.Key, kv => kv.Value);

        if (_context!.HttpContext.Request.Headers.ContainsKey(InertiaHeader.PartialOnly))
            props = ResolveOnly(props);

        if (_context!.HttpContext.Request.Headers.ContainsKey(InertiaHeader.PartialExcept))
            props = ResolveExcept(props);

        return props;
    }

    /// <summary>
    /// Resolve the `only` partial request props.
    /// </summary>
    private Dictionary<string, object?> ResolveOnly(Dictionary<string, object?> props)
    {
        var onlyKeys = _context!.HttpContext.Request.Headers[InertiaHeader.PartialOnly]
            .ToString().Split(',')
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrEmpty(k))
            .ToList();

        return props.Where(kv => onlyKeys.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Resolve the `except` partial request props.
    /// </summary>
    private Dictionary<string, object?> ResolveExcept(Dictionary<string, object?> props)
    {
        var exceptKeys = _context!.HttpContext.Request.Headers[InertiaHeader.PartialExcept]
            .ToString().Split(',')
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrEmpty(k))
            .ToList();

        return props.Where(kv => exceptKeys.Contains(kv.Key, StringComparer.OrdinalIgnoreCase) == false)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Resolve `always` properties that should always be included on all visits,
    /// regardless of "only" or "except" requests.
    /// </summary>
    private Dictionary<string, object?> ResolveAlways(Dictionary<string, object?> props)
    {
        var alwaysProps = _props.Where(o => o.Value is AlwaysProp);

        return props
            .Where(kv => kv.Value is not AlwaysProp)
            .Concat(alwaysProps).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Resolve all necessary class instances in the given props.
    /// </summary>
    private static async Task<Dictionary<string, object?>> ResolvePropertyInstances(Dictionary<string, object?> props)
    {
        return (await Task.WhenAll(props.Select(async pair =>
        {
            var key = pair.Key.ToCamelCase();

            var value = pair.Value switch
            {
                Func<object?> f => (key, await f.ResolveAsync()),
                Task t => (key, await t.ResolveResult()),
                InvokableProp p => (key, await p.Invoke()),
                _ => (key, pair.Value)
            };

            if (value.Item2 is Dictionary<string, object?> dict)
            {
                value = (key, await ResolvePropertyInstances(dict));
            }

            return value;
        }))).ToDictionary(pair => pair.key, pair => pair.Item2);
    }

    protected internal JsonResult GetJson()
    {
        _context!.HttpContext.Response.Headers.Override(InertiaHeader.Inertia, "true");
        _context!.HttpContext.Response.Headers.Override("Vary", InertiaHeader.Inertia);

        // Per the Inertia.js protocol, partial reload responses should include
        // Vary: X-Inertia-Partial-Data so that CDNs differentiate between full
        // page responses and partial reload responses for caching purposes.
        // See: https://inertiajs.com/the-protocol#asset-versioning
        if (_context!.IsInertiaPartialComponent(_component))
            _context.HttpContext.Response.Headers["Vary"] += ", " + InertiaHeader.PartialOnly;

        _context!.HttpContext.Response.StatusCode = 200;

        return new JsonResult(_page, _jsonOptions);
    }

    private ViewResult GetView()
    {
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), _context!.ModelState)
        {
            Model = _page
        };

        if (_viewData == null) return new ViewResult { ViewName = _rootView, ViewData = viewData };

        foreach (var (key, value) in _viewData)
            viewData[key] = value;

        return new ViewResult { ViewName = _rootView, ViewData = viewData };
    }

    protected internal IActionResult GetResult() => _context!.IsInertiaRequest() ? GetJson() : GetView();

    /// <summary>
    /// Gets validation errors from IErrorBagService, falling back to ModelState.
    /// If the bag name is "default", errors are flattened (matching Laravel's behavior).
    /// Named bags are nested under their bag name.
    /// See: https://inertiajs.com/error-handling#error-bags
    /// </summary>
    private Dictionary<string, object?> GetErrors()
    {
        var errorBagService = _context?.HttpContext?.RequestServices?.GetService(typeof(IErrorBagService)) as IErrorBagService;
        if (errorBagService != null)
        {
            // If the controller/action didn't run the filter (e.g. Minimal APIs or test paths),
            // extract validation errors from ModelState and add them to the bag.
            if (_context?.ModelState != null && !_context.ModelState.IsValid)
            {
                _context.ModelState.AddModelErrorsToBag(errorBagService, errorBagService.CurrentBagName);
            }

            var currentBagName = errorBagService.CurrentBagName;
            var errors = errorBagService.GetErrors(currentBagName);

            if (errors != null && errors.Count > 0)
            {
                if (currentBagName == ErrorBagService.DefaultBagName)
                {
                    return errors.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
                }

                return new Dictionary<string, object?>
                {
                    [currentBagName] = errors
                };
            }
        }
        else if (_context?.ModelState != null && !_context.ModelState.IsValid)
        {
            // Fallback if IErrorBagService is not registered (unlikely, but good for backward compatibility/unit tests)
            var errors = _context.ModelState
                .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key.ToCamelCase(),
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            var errorBag = _context.HttpContext?.Request?.Headers?[InertiaHeader.ErrorBag].ToString();
            var bagName = !string.IsNullOrEmpty(errorBag) ? errorBag : ErrorBagService.DefaultBagName;

            if (bagName == ErrorBagService.DefaultBagName)
            {
                return errors.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
            }

            return new Dictionary<string, object?>
            {
                [bagName] = errors
            };
        }

        return new Dictionary<string, object?>();
    }

    protected internal void SetContext(ActionContext context) => _context = context;

    private void SetPage(Page page) => _page = page;

    public Response WithViewData(IDictionary<string, object> viewData)
    {
        _viewData = viewData;
        return this;
    }
}