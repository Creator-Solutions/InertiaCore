using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace InertiaCore.Pagination;

internal sealed class CursorTokenProtector
{
    internal const string Purpose = "InertiaCore.Cursor.PageToken";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly IDataProtector _protector;

    public CursorTokenProtector(IDataProtectionProvider provider)
    {
        _protector = (provider ?? throw new ArgumentNullException(nameof(provider))).CreateProtector(Purpose);
    }

    public string Protect(CursorToken token)
    {
        var json = JsonSerializer.Serialize(token, Options);
        return _protector.Protect(json);
    }

    public CursorToken Unprotect(string token)
    {
        var json = _protector.Unprotect(token);
        return JsonSerializer.Deserialize<CursorToken>(json, Options)
            ?? throw new JsonException("Empty cursor token.");
    }
}
