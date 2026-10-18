using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InertiaCore.Services;

/// <summary>
/// Provides a request-scoped TempData dictionary for hosts that do not register MVC's
/// TempData factory (Minimal API hosts). The dictionary is backed by an encrypted cookie
/// and is saved when the response starts, so flash data written before a redirect is
/// available on the next request.
/// </summary>
/// <remarks>
/// MVC's own TempData services are not replaced. This store is used only when
/// <see cref="ITempDataDictionaryFactory"/> is not registered, so MVC applications keep
/// their existing serializer and provider.
/// </remarks>
internal static class FlashCookieStore
{
    /// <summary>
    /// Creates a cookie-backed TempData dictionary for this request. Returns null when data
    /// protection is not available. Persistence is handled by <see cref="FlashTempData"/>.
    /// </summary>
    internal static ITempDataDictionary? Create(HttpContext context)
    {
        var services = context.RequestServices;
        var protectionProvider = services?.GetService<IDataProtectionProvider>();
        if (protectionProvider is null)
            return null;

        var loggerFactory = services!.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var options = services!.GetService<IOptions<CookieTempDataProviderOptions>>()
                      ?? Options.Create(new CookieTempDataProviderOptions());

        var provider = new CookieTempDataProvider(protectionProvider, loggerFactory, options, new FlashSerializer());
        return new TempDataDictionary(context, provider);
    }

    /// <summary>
    /// Serializes TempData values as typed JSON so that booleans and strings round-trip
    /// without becoming <see cref="JsonElement"/> values.
    /// </summary>
    private sealed class FlashSerializer : TempDataSerializer
    {
        private sealed record Entry(string Type, string? Value);

        public override bool CanSerializeType(Type type) =>
            type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(long)
            || type == typeof(double);

        public override byte[] Serialize(IDictionary<string, object?> values)
        {
            var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var (key, value) in values)
            {
                entries[key] = value switch
                {
                    null => new Entry("n", null),
                    string s => new Entry("s", s),
                    bool b => new Entry("b", b ? "true" : "false"),
                    int i => new Entry("i", i.ToString(CultureInfo.InvariantCulture)),
                    long l => new Entry("l", l.ToString(CultureInfo.InvariantCulture)),
                    double d => new Entry("d", d.ToString("R", CultureInfo.InvariantCulture)),
                    _ => throw new InvalidOperationException(
                        $"TempData value '{key}' of type '{value.GetType().FullName}' cannot be stored in the flash cookie.")
                };
            }

            return JsonSerializer.SerializeToUtf8Bytes(entries);
        }

        public override IDictionary<string, object?> Deserialize(byte[] unprotectedData)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            var entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(unprotectedData);
            if (entries is null)
                return result;

            foreach (var (key, entry) in entries)
            {
                result[key] = entry.Type switch
                {
                    "s" => entry.Value,
                    "b" => entry.Value == "true",
                    "i" => int.Parse(entry.Value!, CultureInfo.InvariantCulture),
                    "l" => long.Parse(entry.Value!, CultureInfo.InvariantCulture),
                    "d" => double.Parse(entry.Value!, CultureInfo.InvariantCulture),
                    _ => null
                };
            }

            return result;
        }
    }
}
