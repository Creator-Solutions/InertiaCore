namespace InertiaCore.Models;

public class ViteOptions
{
    // The path to the "hot" file.
    public string HotFile { get; set; } = "hot";

    // The base URL of a running Vite dev server, supplied by an external
    // orchestrator (for example .NET Aspire). When set, the asset resolver runs in
    // hot mode and uses this URL directly instead of reading HotFile. Set to null
    // (the default) to keep the file-based detection.
    public string? HotUrl { get; set; }

    // The path to the build directory.
    public string? BuildDirectory { get; set; } = "build";

    // The name of the manifest file.
    public string ManifestFilename { get; set; } = "manifest.json";

    // The path to the public directory.
    public string PublicDirectory { get; set; } = "wwwroot";

    // The Content-Security-Policy nonce applied to every generated tag. Prefer
    // NonceResolver when the nonce is generated per request.
    public string? CspNonce { get; set; }

    // Resolves the CSP nonce at render time (for example from IHttpContextAccessor).
    // Takes precedence over CspNonce when it returns a non-null value.
    public Func<string?>? NonceResolver { get; set; }

    // The manifest key used to detect Subresource Integrity hashes. Set to null
    // to disable integrity attributes.
    public string? IntegrityKey { get; set; } = "integrity";

    // The prefetch strategy applied to dynamic imports. None disables prefetching.
    public PrefetchStrategy PrefetchStrategy { get; set; } = PrefetchStrategy.None;

    // The number of assets loaded concurrently by the waterfall strategy.
    public int PrefetchConcurrency { get; set; } = 3;

    // The window event that triggers prefetching.
    public string PrefetchEvent { get; set; } = "load";

    // Extra attributes applied to generated script tags.
    public IList<ViteScriptTagAttributesResolver> ScriptTagAttributes { get; set; } =
        new List<ViteScriptTagAttributesResolver>();

    // Extra attributes applied to generated stylesheet tags.
    public IList<ViteStyleTagAttributesResolver> StyleTagAttributes { get; set; } =
        new List<ViteStyleTagAttributesResolver>();

    // Extra attributes applied to generated preload/modulepreload tags.
    public IList<VitePreloadTagAttributesResolver> PreloadTagAttributes { get; set; } =
        new List<VitePreloadTagAttributesResolver>();
}
