using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Utils;

/// <summary>
/// Stores and consumes the "preserve fragment" flag used by
/// <c>preserveFragment</c> across a redirect.
/// <para>
/// The flag is written to <see cref="ITempDataDictionary"/> (which survives the
/// next request) when available, and falls back to <see cref="HttpContext.Items"/>
/// otherwise. Mirrors Laravel's session-based <c>preserveFragment()</c>.
/// </para>
/// </summary>
internal static class FragmentPreservation
{
    /// <summary>The TempData/Items key used to carry the flag.</summary>
    internal const string Key = "InertiaPreserveFragment";

    /// <summary>Marks the current request's redirect target as preserving the request fragment.</summary>
    internal static void Set(HttpContext context)
    {
        var tempData = GetTempData(context);
        if (tempData != null)
        {
            tempData[Key] = true;
            return;
        }

        context.Items[Key] = true;
    }

    /// <summary>Reads and clears the flag, returning whether it was set.</summary>
    internal static bool Consume(HttpContext? context)
    {
        if (context == null)
            return false;

        var tempData = GetTempData(context);
        if (tempData != null)
        {
            if (tempData.TryGetValue(Key, out var value) && value is true)
            {
                tempData.Remove(Key);
                return true;
            }

            return false;
        }

        if (context.Items.TryGetValue(Key, out var item) && item is true)
        {
            context.Items.Remove(Key);
            return true;
        }

        return false;
    }

    private static ITempDataDictionary? GetTempData(HttpContext context)
    {
        var factory = context.RequestServices?.GetService<ITempDataDictionaryFactory>();
        return factory?.GetTempData(context);
    }
}
