using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using InertiaCore.Models;
using InertiaCore.Props;
using InertiaCore.Services;
using InertiaCore.Ssr;
using InertiaCore.Extensions;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InertiaCore;

/// <summary>
/// Internal factory contract for creating Inertia responses.
/// Consumers should prefer <c>IInertia</c> over this interface.
/// </summary>
public interface IResponseFactory
{
    Response Render(string component, object? props = null);
    Task<IHtmlContent> Head(object model);
    Task<IHtmlContent> Html(object model);
    void Version(string? version);
    void Version(Func<string?> version);
    string? GetVersion();
    LocationResult Location(string url);
    void Share(string key, object? value);
    void Share(IDictionary<string, object?> data);
    void ClearHistory(bool clear = true);
    void EncryptHistory(bool encrypt = true);
    AlwaysProp Always(object? value);
    AlwaysProp Always(Func<object?> callback);
    AlwaysProp Always(Func<Task<object?>> callback);
    LazyProp Lazy(Func<object?> callback);
    LazyProp Lazy(Func<Task<object?>> callback);
}

internal class ResponseFactory : IResponseFactory
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IGateway _gateway;
    private readonly IOptions<InertiaOptions> _options;
    private readonly InertiaState _state;
    private readonly JsonSerializerOptions _jsonOptions;

    private bool _clearHistory;
    private bool? _encryptHistory;

    public ResponseFactory(
        IHttpContextAccessor contextAccessor,
        IGateway gateway,
        IOptions<InertiaOptions> options,
        InertiaState state,
        IOptions<JsonOptions> jsonOptions)
    {
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _state = state ?? throw new ArgumentNullException(nameof(state));

        // Start from the application's configured JsonSerializerOptions, then ensure
        // camelCase and cycle-handling defaults that Inertia.js requires.
        JsonSerializerOptions hostOptions = jsonOptions?.Value?.JsonSerializerOptions ?? new JsonSerializerOptions();
        _jsonOptions = new JsonSerializerOptions(hostOptions)
        {
            PropertyNamingPolicy = hostOptions.PropertyNamingPolicy ?? JsonNamingPolicy.CamelCase,
            ReferenceHandler = hostOptions.ReferenceHandler ?? ReferenceHandler.IgnoreCycles
        };
    }

    public Response Render(string component, object? props = null)
    {
        props ??= new { };
        var dictProps = props switch
        {
            Dictionary<string, object?> dict => dict,
            _ => SerializePropsToDictionary(props)
        };

        return new Response(
            component,
            dictProps,
            _options.Value.RootView,
            GetVersion(),
            _encryptHistory ?? _options.Value.EncryptHistory,
            _clearHistory,
            _state,
            _jsonOptions);
    }
    
    public IResult RenderResult(string component, object? props = null)
        => (IResult)Render(component, props);

    public async Task<IHtmlContent> Head(object model)
    {
        if (!_options.Value.SsrEnabled)
            return new HtmlString("");

        var context = _contextAccessor.HttpContext!;

        // Migrated from HttpContext.Features — see InertiaState
        var response = _state.SsrResponse;
        response ??= await _gateway.Dispatch(model, _options.Value.SsrUrl);

        if (response == null)
            return new HtmlString("");

        _state.SsrResponse = response;
        return response.GetHead();
    }

    public async Task<IHtmlContent> Html(object model)
    {
        if (_options.Value.SsrEnabled)
        {
            var context = _contextAccessor.HttpContext!;

            // Migrated from HttpContext.Features — see InertiaState
            var response = _state.SsrResponse;
            response ??= await _gateway.Dispatch(model, _options.Value.SsrUrl);

            if (response != null)
            {
                _state.SsrResponse = response;
                return response.GetBody();
            }
        }

        var data = JsonSerializer.Serialize(model, _jsonOptions);
        // v3 protocol: escape forward slashes to avoid premature </script> termination
        var escaped = data.Replace("/", "\\/");

        return new HtmlString(
            $"<script data-page=\"app\" type=\"application/json\">{escaped}</script>\n" +
            $"<div id=\"app\"></div>"
        );
    }

    public void Version(string? version) => _state.Version = version;

    public void Version(Func<string?> version) => _state.VersionFunc = version;

    public string? GetVersion() =>
        _state.VersionFunc is not null
            ? _state.VersionFunc.Invoke()
            : _state.Version;

    public LocationResult Location(string url) => new(url);

    public void Share(string key, object? value)
    {
        _state.SharedProps[key.ToCamelCase()] = value;
    }

    public void Share(IDictionary<string, object?> data)
    {
        foreach (var (key, value) in data)
        {
            _state.SharedProps[key.ToCamelCase()] = value;
        }
    }

    public void ClearHistory(bool clear = true) => _clearHistory = clear;

    public void EncryptHistory(bool encrypt = true) => _encryptHistory = encrypt;

    public LazyProp Lazy(Func<object?> callback) => new(callback);
    public LazyProp Lazy(Func<Task<object?>> callback) => new(callback);
    public AlwaysProp Always(object? value) => new(value);
    public AlwaysProp Always(Func<object?> callback) => new(callback);
    public AlwaysProp Always(Func<Task<object?>> callback) => new(callback);

    /// <summary>
    /// Converts an arbitrary props object to a dictionary.
    /// Prefers JSON round-trip for AOT compatibility, with reflection fallback
    /// for objects that contain delegates or other non-serializable members.
    /// </summary>
    private Dictionary<string, object?> SerializePropsToDictionary(object props)
    {
        if (props is IDictionary<string, object?> dict)
            return new Dictionary<string, object?>(dict);

        var reflectedProps = props.GetType().GetProperties();

        // If any property value is an InvokableProp (DeferredProp, LazyProp, AlwaysProp),
        // the JSON round-trip path cannot be used because it serializes these as {}
        // and destroys type information needed for downstream type checks.
        if (reflectedProps.Any(p => p.GetValue(props) is InvokableProp))
            return reflectedProps.ToDictionary(o => o.Name, o => o.GetValue(props));

        // First attempt: JSON round-trip — AOT-compatible and respects serializer options.
        try
        {
            var json = JsonSerializer.Serialize(props, _jsonOptions);
            var document = JsonSerializer.Deserialize<JsonDocument>(json, _jsonOptions);

            if (document != null)
                return JsonElementToDictionary(document.RootElement);
        }
        catch (NotSupportedException)
        {
            // JSON serialization of delegates is not supported.
            // Fall through to reflection-based conversion below.
        }

        // Fallback: reflection-based property enumeration for objects containing delegates
        // or other types that cannot be JSON-serialized (e.g. Func<>, Task, LazyProp).
        return reflectedProps.ToDictionary(o => o.Name, o => o.GetValue(props));
    }

    private static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = JsonElementToValue(property.Value);
        }

        return result;
    }

    private static object? JsonElementToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToValue).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
