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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, ScrollPropEntry>? ScrollProps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<string>>? DeferredProps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, OncePropEntry>? OnceProps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RescuedProps { get; set; }

    /// <summary>Top-level prop keys registered via <c>Inertia.Share</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SharedProps { get; set; }

    /// <summary>Whether to encrypt the current page's history state. Emitted only when true.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EncryptHistory { get; set; }

    /// <summary>Whether to clear any encrypted history state. Emitted only when true.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ClearHistory { get; set; }

    /// <summary>Whether to preserve the URL fragment from the original request across a redirect. Emitted only when true.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PreserveFragment { get; set; }
}
