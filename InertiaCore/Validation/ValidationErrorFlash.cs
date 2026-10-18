using System.Text.Json;
using InertiaCore.Services;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Validation;

/// <summary>
/// Carries validation errors across a redirect through <see cref="IInertiaFlashProvider"/>.
/// The payload is stored as a JSON string so it survives any TempData serializer.
/// </summary>
internal static class ValidationErrorFlash
{
    /// <summary>The reserved flash key holding the serialized errors.</summary>
    internal const string Key = "inertiaValidationErrors";

    private sealed record Payload(string Bag, Dictionary<string, string[]> Errors);

    /// <summary>Flashes the errors for the given bag so the next page render can restore them.</summary>
    internal static void Store(IInertiaFlashProvider flashProvider, string bagName, IReadOnlyDictionary<string, string[]> errors)
    {
        var payload = new Payload(bagName, errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        flashProvider.Flash(Key, JsonSerializer.Serialize(payload));
    }

    /// <summary>
    /// Attempts to read a flashed payload. Returns false when the value is missing or
    /// malformed.
    /// </summary>
    internal static bool TryRead(object? value, out string bagName, out IReadOnlyDictionary<string, string[]> errors)
    {
        bagName = ErrorBagService.DefaultBagName;
        errors = new Dictionary<string, string[]>();

        // Within a request the value is the string written by Store; after a TempData
        // round trip it is the JsonElement the flash provider deserialized.
        var json = value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null
        };

        if (json is null)
            return false;

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(json);
            if (payload is null || string.IsNullOrEmpty(payload.Bag) || payload.Errors is null)
                return false;

            bagName = payload.Bag;
            errors = payload.Errors;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
