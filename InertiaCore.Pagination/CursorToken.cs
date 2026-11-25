using System.Text.Json.Serialization;

namespace InertiaCore.Pagination;

internal sealed class CursorToken
{
    public const int CurrentVersion = 1;

    [JsonPropertyName("v")]
    public int Version { get; set; } = CurrentVersion;

    [JsonPropertyName("n")]
    public bool PointsToNext { get; set; } = true;

    [JsonPropertyName("d")]
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.Ordinal);
}
