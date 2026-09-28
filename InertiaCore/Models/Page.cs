using System.Text.Json.Serialization;

namespace InertiaCore.Models;

internal class Page
{
    public Dictionary<string, object?> Props { get; set; } = default!;
    public string Component { get; set; } = default!;
    public string? Version { get; set; }
    public string Url { get; set; } = default!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MergeProps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PrependProps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? DeepMergeProps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MatchPropsOn { get; set; }

    [JsonIgnore]
    public bool EncryptHistory { get; set; }

    [JsonIgnore]
    public bool ClearHistory { get; set; }
}
