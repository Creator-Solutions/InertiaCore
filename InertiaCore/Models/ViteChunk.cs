using System.Text.Json.Serialization;

namespace InertiaCore.Models;

/// <summary>
/// A single entry from a Vite manifest (<c>manifest.json</c>). Mirrors the
/// fields Laravel's Vite helper reads when resolving tags, preloads and
/// prefetch assets.
/// </summary>
public sealed class ViteChunk
{
    [JsonPropertyName("file")] public string? File { get; set; }

    [JsonPropertyName("src")] public string? Src { get; set; }

    [JsonPropertyName("imports")] public List<string> Imports { get; set; } = new();

    [JsonPropertyName("dynamicImports")] public List<string> DynamicImports { get; set; } = new();

    [JsonPropertyName("css")] public List<string> Css { get; set; } = new();

    [JsonPropertyName("integrity")] public string? Integrity { get; set; }
}

/// <summary>Resolves extra attributes for generated script tags.</summary>
public delegate IDictionary<string, object?> ViteScriptTagAttributesResolver(string src, string url, ViteChunk? chunk);

/// <summary>Resolves extra attributes for generated stylesheet tags.</summary>
public delegate IDictionary<string, object?> ViteStyleTagAttributesResolver(string src, string url, ViteChunk? chunk);

/// <summary>
/// Resolves extra attributes for generated preload tags. Returning <c>null</c>
/// skips the tag entirely (Laravel returns <c>false</c>).
/// </summary>
public delegate IDictionary<string, object?>? VitePreloadTagAttributesResolver(string src, string url, ViteChunk? chunk);
