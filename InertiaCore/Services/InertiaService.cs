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

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaService"/>.
    /// </summary>
    public InertiaService(IResponseFactory factory, InertiaState state, IOptions<InertiaOptions> options)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<IActionResult> Render(string component, object? props = null)
    {
        Response response = _factory.Render(component, props);
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
        _state.FlashProps[key.ToCamelCase()] = value;
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
    public DeferredProp Defer(Func<Task<object?>> factory) => new(factory);

    /// <inheritdoc />
    public void EncryptHistory(bool encrypt = true) => _factory.EncryptHistory(encrypt);

    /// <inheritdoc />
    public void ClearHistory(bool clear = true) => _factory.ClearHistory(clear);

}
