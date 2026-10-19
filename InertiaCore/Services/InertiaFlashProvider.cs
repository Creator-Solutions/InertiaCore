using System.Text;
using System.Text.Json;
using InertiaCore.Extensions;
using InertiaCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    /// <inheritdoc />
    public void Clear()
    {
        var context = _contextAccessor.HttpContext;
        if (context is null)
            return;

        RemoveBag(context);
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
            var json = JsonSerializer.Serialize(bag);

            // Only the persisted payload is limited: the Items fallback lives for this
            // request alone and is never written to a cookie.
            EnsurePayloadWithinLimit(context, json);
            tempData[Key] = json;
            return;
        }

        context.Items[Key] = bag;
    }

    /// <summary>
    /// Throws <see cref="InertiaFlashPayloadTooLargeException"/> when the serialized payload
    /// would not fit the configured cookie limit.
    /// </summary>
    private static void EnsurePayloadWithinLimit(HttpContext context, string json)
    {
        var options = context.RequestServices?.GetService<IOptions<InertiaFlashOptions>>();
        var maxBytes = options?.Value.MaxCookiePayloadBytes ?? InertiaFlashOptions.DefaultMaxCookiePayloadBytes;
        var actualBytes = Encoding.UTF8.GetByteCount(json);

        if (actualBytes > maxBytes)
            throw new InertiaFlashPayloadTooLargeException(actualBytes, maxBytes);
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
