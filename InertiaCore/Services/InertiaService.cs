using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Props;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InertiaCore.Services;

/// <summary>
/// Scoped implementation of <see cref="IInertia"/> that delegates all operations
/// to the internal <see cref="IResponseFactory"/> and <see cref="InertiaState"/>.
/// </summary>
public sealed class InertiaService : IInertia
{
    private readonly IResponseFactory _factory;
    private readonly InertiaState _state;
    private readonly IOptions<InertiaOptions> _options;
    private readonly IInertiaFlashProvider? _flashProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaService"/>.
    /// </summary>
    public InertiaService(IResponseFactory factory, InertiaState state, IOptions<InertiaOptions> options)
        : this(factory, state, options, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaService"/>.
    /// </summary>
    /// <param name="factory">The response factory.</param>
    /// <param name="state">The per-request state.</param>
    /// <param name="options">The Inertia options.</param>
    /// <param name="flashProvider">The flash provider, used to persist flash across requests.</param>
    public InertiaService(
        IResponseFactory factory,
        InertiaState state,
        IOptions<InertiaOptions> options,
        IInertiaFlashProvider? flashProvider)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _flashProvider = flashProvider;
    }

    /// <inheritdoc />
    public Task<IActionResult> Render(string component, object? props = null)
    {
        Response response = _factory.Render(component, props);
        return Task.FromResult<IActionResult>(response);
    }

    /// <inheritdoc />
    public Task<IActionResult> Render<TProps>(TProps props)
    {
        Response response = _factory.Render(InertiaPageRegistry.ResolveComponent(typeof(TProps)), props);
        return Task.FromResult<IActionResult>(response);
    }

    /// <inheritdoc />
    public IActionResult Location(string url) => _factory.Location(url);

    /// <inheritdoc />
    public void Share(string key, object? value) => _factory.Share(key, value);

    /// <inheritdoc />
    public void Share(IDictionary<string, object?> data) => _factory.Share(data);

    /// <inheritdoc />
    public void Flash(string key, object? value)
    {
        var camelCased = key.ToCamelCase();

        // Persist for the next request when a provider is registered (so flash set
        // before a redirect is shown on the target page); otherwise fall back to the
        // current-request-only behaviour.
        if (_flashProvider is not null)
            _flashProvider.Flash(camelCased, value);
        else
            _state.FlashProps[camelCased] = value;
    }

    /// <inheritdoc />
    public void Version(string? version) => _factory.Version(version);

    /// <inheritdoc />
    public void Version(Func<string?> versionFunc) => _factory.Version(versionFunc);

    /// <inheritdoc />
    public string? GetVersion() => _factory.GetVersion();

    /// <inheritdoc />
    public AlwaysProp Always(object? value) => _factory.Always(value);

    /// <inheritdoc />
    public AlwaysProp Always(Func<object?> callback) => _factory.Always(callback);

    /// <inheritdoc />
    public AlwaysProp Always(Func<Task<object?>> callback) => _factory.Always(callback);

    /// <inheritdoc />
    public LazyProp Lazy(Func<object?> callback) => _factory.Lazy(callback);

    /// <inheritdoc />
    public LazyProp Lazy(Func<Task<object?>> callback) => _factory.Lazy(callback);

    /// <inheritdoc />
    public DeferredProp Defer(Func<object?> factory) => new(factory);

    /// <inheritdoc />
    public DeferredProp Defer(Func<object?> factory, string group) => new(factory, group);

    /// <inheritdoc />
    public DeferredProp Defer(Func<object?> factory, string group, bool rescue) => new(factory, group, rescue);

    /// <inheritdoc />
    public DeferredProp Defer(Func<Task<object?>> factory) => new(factory);

    /// <inheritdoc />
    public DeferredProp Defer(Func<Task<object?>> factory, string group) => new(factory, group);

    /// <inheritdoc />
    public DeferredProp Defer(Func<Task<object?>> factory, string group, bool rescue) => new(factory, group, rescue);

    /// <inheritdoc />
    public OnceProp Once(Func<object?> callback) => _factory.Once(callback);

    /// <inheritdoc />
    public OnceProp Once(Func<Task<object?>> callback) => _factory.Once(callback);

    /// <inheritdoc />
    public MergeProp Merge(object? value) => _factory.Merge(value);

    /// <inheritdoc />
    public MergeProp Merge(Func<object?> callback) => _factory.Merge(callback);

    /// <inheritdoc />
    public MergeProp Merge(Func<Task<object?>> callback) => _factory.Merge(callback);

    /// <inheritdoc />
    public MergeProp DeepMerge(object? value) => _factory.DeepMerge(value);

    /// <inheritdoc />
    public MergeProp DeepMerge(Func<object?> callback) => _factory.DeepMerge(callback);

    /// <inheritdoc />
    public MergeProp DeepMerge(Func<Task<object?>> callback) => _factory.DeepMerge(callback);

    /// <inheritdoc />
    public ScrollProp Scroll(object? items, ScrollMetadata metadata, string wrapper = "data") =>
        _factory.Scroll(items, metadata, wrapper);

    /// <inheritdoc />
    public ScrollProp Scroll(Func<object?> items, ScrollMetadata metadata, string wrapper = "data") =>
        _factory.Scroll(items, metadata, wrapper);

    /// <inheritdoc />
    public ScrollProp Scroll(Func<Task<object?>> items, ScrollMetadata metadata, string wrapper = "data") =>
        _factory.Scroll(items, metadata, wrapper);

    /// <inheritdoc />
    public void EncryptHistory(bool encrypt = true) => _factory.EncryptHistory(encrypt);

    /// <inheritdoc />
    public void ClearHistory(bool clear = true) => _factory.ClearHistory(clear);

}
