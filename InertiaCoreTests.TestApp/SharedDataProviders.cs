using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore;
using InertiaCore.Contracts;
using InertiaCore.Services;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests.TestApp;

/// <summary>
/// A scoped service with a per-scope identifier, used to prove a provider resolves scoped
/// dependencies (the equivalent of a scoped <c>DbContext</c>) without captive dependency errors.
/// </summary>
public sealed class TestSharedScope
{
    /// <summary>A value unique to this scope.</summary>
    public string Id { get; } = System.Guid.NewGuid().ToString("N");
}

/// <summary>First registered provider: contributes a key and depends on a scoped service.</summary>
public sealed class FirstSharedDataProvider : IInertiaSharedDataProvider
{
    private readonly TestSharedScope _scope;

    /// <summary>Initializes a new instance of <see cref="FirstSharedDataProvider"/>.</summary>
    /// <param name="scope">The scoped service the provider depends on.</param>
    public FirstSharedDataProvider(TestSharedScope scope) => _scope = scope;

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, object?>> GetSharedDataAsync(
        HttpContext context, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, object?> props = new Dictionary<string, object?>
        {
            ["providerOne"] = "one",
            ["scopeId"] = _scope.Id,
        };

        return new ValueTask<IReadOnlyDictionary<string, object?>>(props);
    }
}

/// <summary>
/// Second registered provider: overrides a key from the first (proving registration order) and
/// contributes a lazy prop resolved by the standard pipeline.
/// </summary>
public sealed class SecondSharedDataProvider : IInertiaSharedDataProvider
{
    private readonly IInertia _inertia;

    /// <summary>Initializes a new instance of <see cref="SecondSharedDataProvider"/>.</summary>
    /// <param name="inertia">The Inertia service, used to build a lazy prop.</param>
    public SecondSharedDataProvider(IInertia inertia) => _inertia = inertia;

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, object?>> GetSharedDataAsync(
        HttpContext context, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, object?> props = new Dictionary<string, object?>
        {
            ["providerOne"] = "two",
            ["lazy"] = _inertia.Lazy(() => "resolved-lazily"),
        };

        return new ValueTask<IReadOnlyDictionary<string, object?>>(props);
    }
}

/// <summary>A provider that always throws, used to verify the error names the provider type.</summary>
public sealed class FailingSharedDataProvider : IInertiaSharedDataProvider
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, object?>> GetSharedDataAsync(
        HttpContext context, CancellationToken cancellationToken)
        => throw new System.InvalidOperationException("boom from the failing provider");
}
