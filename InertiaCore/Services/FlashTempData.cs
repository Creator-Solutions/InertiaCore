using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Services;

/// <summary>
/// Resolves the TempData dictionary used for Inertia flash data and fragment preservation.
/// </summary>
/// <remarks>
/// MVC's TempData factory is used when it is registered. Otherwise a cookie-backed dictionary
/// is used (see <see cref="FlashCookieStore"/>). Either way the dictionary is saved when the
/// response starts. Minimal API endpoints never run MVC's save filter, so without this hook
/// data written before a redirect would be lost.
/// </remarks>
internal static class FlashTempData
{
    private const string CacheKey = "InertiaFlashTempData";

    /// <summary>
    /// Returns the request's TempData, or null when no store is available (callers then fall back
    /// to <see cref="HttpContext.Items"/>).
    /// </summary>
    internal static ITempDataDictionary? Get(HttpContext context)
    {
        // One dictionary per request: every reader and writer must share it, or a write
        // would land in a copy that the save hook never persists.
        if (context.Items.TryGetValue(CacheKey, out var cached) && cached is ITempDataDictionary existing)
            return existing;

        var factory = context.RequestServices?.GetService<ITempDataDictionaryFactory>();
        var tempData = factory?.GetTempData(context) ?? FlashCookieStore.Create(context);
        if (tempData is null)
            return null;

        context.Items[CacheKey] = tempData;
        context.Response.OnStarting(() =>
        {
            tempData.Save();
            return Task.CompletedTask;
        });

        return tempData;
    }
}
