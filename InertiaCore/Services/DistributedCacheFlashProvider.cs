using System.Text.Json;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Services;

/// <summary>
/// An <see cref="IInertiaFlashProvider"/> that stores flash data in
/// <see cref="IDistributedCache"/> instead of a cookie, for payloads that can exceed
/// <see cref="InertiaCore.Models.InertiaFlashOptions.MaxCookiePayloadBytes"/>.
/// <para>
/// The payload lives in the cache under a random key; only that key travels in a small
/// cookie, which is protected with Data Protection. Reading the cookie consumes the entry.
/// </para>
/// </summary>
/// <remarks>
/// Register with <c>services.AddInertiaDistributedCacheFlash()</c> after an
/// <c>IDistributedCache</c> registration such as <c>AddDistributedMemoryCache()</c>.
/// </remarks>
public sealed class DistributedCacheFlashProvider : IInertiaFlashProvider
{
    private const string Key = "InertiaFlashCacheId";
    private const string Purpose = "InertiaCore.Flash.CacheId";

    /// <summary>
    /// How long a flashed payload stays in the cache. Flash is intended to survive one
    /// redirect, so this is only a safety net for payloads that are never read.
    /// </summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private static readonly IReadOnlyDictionary<string, object?> Empty =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IDistributedCache _cache;
    private readonly IDataProtectionProvider? _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="DistributedCacheFlashProvider"/>.
    /// </summary>
    /// <param name="contextAccessor">The HTTP context accessor.</param>
    /// <param name="cache">The distributed cache that stores flash payloads.</param>
    /// <param name="dataProtectionProvider">
    /// Protects the cache key cookie. Defaults to null when Data Protection is not
    /// registered, in which case the key is stored unprotected.
    /// </param>
    public DistributedCacheFlashProvider(
        IHttpContextAccessor contextAccessor,
        IDistributedCache cache,
        IDataProtectionProvider? dataProtectionProvider = null)
    {
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _dataProtectionProvider = dataProtectionProvider;
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

        var id = ReadId(context);
        if (id is null)
            return Empty;

        var json = _cache.GetString(id);
        _cache.Remove(id);
        ExpireIdCookie(context);

        if (string.IsNullOrEmpty(json))
            return Empty;

        var bag = Deserialize(json);
        return bag is null || bag.Count == 0 ? Empty : new Dictionary<string, object?>(bag, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public void Flash(string key, object? value)
    {
        var context = _contextAccessor.HttpContext;
        if (context is null)
            return;

        var id = ReadId(context);
        if (id is null)
        {
            id = NewId(context);
            WriteIdCookie(context, id);
        }

        var bag = Load(context, id) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        bag[key] = value;

        _cache.SetString(id, JsonSerializer.Serialize(bag), EntryOptions());
    }

    /// <inheritdoc />
    public void Reflash()
    {
        var context = _contextAccessor.HttpContext;
        if (context is null)
            return;

        // The payload is only consumed by Pull(), so there is normally nothing to keep.
        // Refresh it anyway so its expiry starts from the follow-up request.
        var id = ReadId(context);
        if (id is not null)
            _cache.Refresh(id);
    }

    /// <inheritdoc />
    public void Clear()
    {
        var context = _contextAccessor.HttpContext;
        if (context is null)
            return;

        var id = ReadId(context);
        if (id is not null)
            _cache.Remove(id);

        ExpireIdCookie(context);
    }

    private Dictionary<string, object?>? Load(HttpContext context, string id)
    {
        var json = _cache.GetString(id);
        return string.IsNullOrEmpty(json) ? null : Deserialize(json);
    }

    private static DistributedCacheEntryOptions EntryOptions() =>
        new() { AbsoluteExpirationRelativeToNow = CacheLifetime };

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

    private string? ReadId(HttpContext context)
    {
        var raw = context.Request.Cookies[Key];
        if (string.IsNullOrEmpty(raw))
            return null;

        var id = _dataProtectionProvider?.CreateProtector(Purpose).Unprotect(raw) ?? raw;
        return string.IsNullOrEmpty(id) ? null : id;
    }

    private string NewId(HttpContext context) => Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private void WriteIdCookie(HttpContext context, string id)
    {
        var value = _dataProtectionProvider?.CreateProtector(Purpose).Protect(id) ?? id;

        context.Response.Cookies.Append(Key, value, CookieOptions(context));
    }

    private void ExpireIdCookie(HttpContext context) =>
        context.Response.Cookies.Delete(Key, CookieOptions(context));

    private static CookieOptions CookieOptions(HttpContext context)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.Add(CacheLifetime),
        };

        return options;
    }
}
