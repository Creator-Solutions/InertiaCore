using System.Text.Json;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Services;

/// <summary>
/// Default <see cref="IInertiaFlashProvider"/>. Stores flash data in
/// <see cref="ITempDataDictionary"/> when it is available (so it survives the next
/// request), and falls back to <see cref="HttpContext.Items"/> otherwise.
/// </summary>
public sealed class InertiaFlashProvider : IInertiaFlashProvider
{
    private const string Key = "InertiaFlash";
    private static readonly Dictionary<string, object?> Empty = new();

    private readonly IHttpContextAccessor _contextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaFlashProvider"/>.
    /// </summary>
    /// <param name="contextAccessor">The HTTP context accessor.</param>
    public InertiaFlashProvider(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Pull()
    {
        var context = _contextAccessor.HttpContext;
        if (context is null)
            return Empty;

        // Prefetch requests must not consume flash data.
        if (context.IsPrefetchRequest())
            return Empty;

        var bag = GetBag(context);
        if (bag is null || bag.Count == 0)
            return Empty;

        RemoveBag(context);
        return new Dictionary<string, object?>(bag);
    }

    /// <inheritdoc />
    public void Flash(string key, object? value)
    {
        var context = _contextAccessor.HttpContext;
        if (context is null)
            return;

        var bag = GetBag(context) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        bag[key] = value;
        SetBag(context, bag);
    }

    /// <inheritdoc />
    public void Reflash()
    {
        var context = _contextAccessor.HttpContext;
        if (context is null)
            return;

        // Keep the persisted flash for the next request (TempData removes read keys
        // otherwise). The Items fallback only lasts the current request.
        var tempData = GetTempData(context);
        if (tempData is not null && tempData.ContainsKey(Key))
            tempData.Keep(Key);
    }

    private static IDictionary<string, object?>? GetBag(HttpContext context)
    {
        var tempData = GetTempData(context);
        if (tempData is not null)
        {
            // TempData is serialized between requests and only supports scalar and string
            // values, so the bag is kept as JSON there (see SetBag).
            return tempData.TryGetValue(Key, out var value) && value is string json
                ? Deserialize(json)
                : null;
        }

        return context.Items.TryGetValue(Key, out var item) && item is Dictionary<string, object?> itemBag
            ? itemBag
            : null;
    }

    private static void SetBag(HttpContext context, IDictionary<string, object?> bag)
    {
        var tempData = GetTempData(context);
        if (tempData is not null)
        {
            tempData[Key] = JsonSerializer.Serialize(bag);
            return;
        }

        context.Items[Key] = bag;
    }

    private static Dictionary<string, object?>? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void RemoveBag(HttpContext context)
    {
        var tempData = GetTempData(context);
        if (tempData is not null)
        {
            tempData.Remove(Key);
            return;
        }

        context.Items.Remove(Key);
    }

    private static ITempDataDictionary? GetTempData(HttpContext context)
    {
        return FlashTempData.Get(context);
    }
}
