using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Abstractions;
using InertiaCore.Extensions;
using InertiaCore.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Options;

namespace InertiaCore.Utils;

public interface IViteBuilder
{
    HtmlString ReactRefresh();
    Task<HtmlString> ReactRefreshAsync();
    HtmlString Input(params string[] entryPoints);
    Task<HtmlString> InputAsync(params string[] entryPoints);
    string? GetManifest();
    string? GetManifestHash();
    bool IsRunningHot();
    string Asset(string entryPoint);
    string Content(string entryPoint);
}

internal class ViteBuilder : IViteBuilder
{
    private IFileSystem _fileSystem;
    private readonly IOptions<ViteOptions> _options;
    private readonly object _cacheLock = new();
    private string? _cachedManifest;
    private DateTime _lastManifestRead = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private static readonly Regex CssPathRegex =
        new(@"\.(css|less|sass|scss|styl|stylus|pcss|postcss)(\?[^\.]*)?$", RegexOptions.IgnoreCase);

    public ViteBuilder(IOptions<ViteOptions> options) => (_fileSystem, _options) = (new FileSystem(), options);

    protected internal void UseFileSystem(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    private ViteOptions Options => _options.Value;

    private string GetPublicPathForFile(string path) =>
        string.Join("/", Options.PublicDirectory, path);

    private string GetBuildPathForFile(string path)
    {
        var pieces = new List<string> { Options.PublicDirectory };
        if (!string.IsNullOrEmpty(Options.BuildDirectory))
        {
            pieces.Add(Options.BuildDirectory);
        }

        pieces.Add(path);
        return string.Join("/", pieces);
    }

    private string? Nonce => Options.NonceResolver?.Invoke() ?? Options.CspNonce;

    /// <summary>
    /// Generates various tags from the given input files.
    /// </summary>
    public HtmlString Input(params string[] entryPoints)
    {
        if (IsRunningHot())
        {
            return RenderHot(entryPoints);
        }

        return ProcessManifest(entryPoints, ReadManifest());
    }

    /// <summary>
    /// Asynchronously generates various tags from the given input files, using a
    /// cached manifest read for production builds.
    /// </summary>
    public async Task<HtmlString> InputAsync(params string[] entryPoints)
    {
        if (IsRunningHot())
        {
            return RenderHot(entryPoints);
        }

        var manifest = await ReadManifestWithCacheAsync().ConfigureAwait(false);
        return ProcessManifest(entryPoints, manifest);
    }

    private HtmlString RenderHot(string[] entryPoints)
    {
        var entries = new List<string> { "@vite/client" };
        entries.AddRange(entryPoints);

        var html = new StringBuilder();
        foreach (var entry in entries)
        {
            html.Append(MakeScriptTag(entry, AssetUrl(entry), null));
        }

        return new HtmlString(html.ToString());
    }

    /// <summary>
    /// Generate React refresh runtime script.
    /// </summary>
    public HtmlString ReactRefresh()
    {
        if (!IsRunningHot())
        {
            return new HtmlString("<!-- no hot -->");
        }

        var nonce = Nonce;
        var nonceAttribute = string.IsNullOrEmpty(nonce) ? "" : $" nonce=\"{nonce}\"";

        var inner = $"import RefreshRuntime from '{AssetUrl("@react-refresh")}';" +
                    "RefreshRuntime.injectIntoGlobalHook(window);" +
                    "window.$RefreshReg$ = () => { };" +
                    "window.$RefreshSig$ = () => (type) => type;" +
                    "window.__vite_plugin_react_preamble_installed__ = true;";

        return new HtmlString($"<script type=\"module\"{nonceAttribute}>{inner}</script>");
    }

    /// <summary>
    /// Asynchronously generate React refresh runtime script.
    /// </summary>
    public Task<HtmlString> ReactRefreshAsync() => Task.FromResult(ReactRefresh());

    private HtmlString ProcessManifest(string[] entryPoints, Dictionary<string, ViteChunk> manifest)
    {
        var tagSet = new HashSet<string>();
        var tags = new List<string>();
        var preloadSet = new HashSet<string>();
        var preloads = new List<(bool IsCss, string Html)>();

        foreach (var entry in entryPoints)
        {
            var chunk = GetChunk(manifest, entry);

            AddPreload(entry, AssetUrl(chunk.File!), chunk, preloads, preloadSet);

            foreach (var import in chunk.Imports)
            {
                var importChunk = GetChunk(manifest, import);
                AddPreload(import, AssetUrl(importChunk.File!), importChunk, preloads, preloadSet);

                foreach (var css in importChunk.Css)
                {
                    var (cssSrc, cssChunk) = FindByFile(manifest, css);
                    AddPreload(cssSrc, AssetUrl(css), cssChunk, preloads, preloadSet);
                    AddTag(MakeTagForChunk(cssSrc, AssetUrl(css), cssChunk), tags, tagSet);
                }
            }

            AddTag(MakeTagForChunk(entry, AssetUrl(chunk.File!), chunk), tags, tagSet);

            foreach (var css in chunk.Css)
            {
                var (cssSrc, cssChunk) = FindByFile(manifest, css);
                AddPreload(cssSrc, AssetUrl(css), cssChunk, preloads, preloadSet);
                AddTag(MakeTagForChunk(cssSrc, AssetUrl(css), cssChunk), tags, tagSet);
            }
        }

        var stylesheets = tags.Where(t => t.StartsWith("<link")).ToList();
        var scripts = tags.Where(t => !t.StartsWith("<link")).ToList();

        var orderedPreloads = preloads
            .OrderByDescending(p => p.IsCss)
            .Select(p => p.Html);

        var html = new StringBuilder();
        foreach (var preload in orderedPreloads) html.Append(preload);
        foreach (var stylesheet in stylesheets) html.Append(stylesheet);
        foreach (var script in scripts) html.Append(script);

        var prefetch = BuildPrefetchScript(entryPoints, manifest);
        if (prefetch != null) html.Append(prefetch);

        return new HtmlString(html.ToString());
    }

    private void AddPreload(string src, string url, ViteChunk? chunk,
        List<(bool IsCss, string Html)> preloads, HashSet<string> seen)
    {
        var html = MakePreloadTagForChunk(src, url, chunk);
        if (html == null || !seen.Add(url)) return;

        preloads.Add((IsCssPath(url), html));
    }

    private static void AddTag(string html, List<string> tags, HashSet<string> seen)
    {
        if (seen.Add(html)) tags.Add(html);
    }

    private static ViteChunk GetChunk(Dictionary<string, ViteChunk> manifest, string key)
    {
        if (!manifest.TryGetValue(key, out var chunk) || string.IsNullOrEmpty(chunk.File))
        {
            throw new Exception("Asset not found in manifest: " + key);
        }

        return chunk;
    }

    private static (string Src, ViteChunk Chunk) FindByFile(Dictionary<string, ViteChunk> manifest, string file)
    {
        foreach (var (key, chunk) in manifest)
        {
            if (chunk.File == file) return (key, chunk);
        }

        return (file, new ViteChunk { File = file });
    }

    private string MakeTagForChunk(string src, string url, ViteChunk? chunk) =>
        IsCssPath(url) ? MakeStylesheetTag(src, url, chunk) : MakeScriptTag(src, url, chunk);

    private string? MakePreloadTagForChunk(string src, string url, ViteChunk? chunk)
    {
        var attributes = ResolvePreloadTagAttributes(src, url, chunk);
        if (attributes == null) return null;

        return $"<link {ParseAttributes(attributes)} />\n\t";
    }

    private string MakeScriptTag(string src, string url, ViteChunk? chunk) =>
        $"<script {ParseAttributes(ResolveScriptTagAttributes(src, url, chunk))}></script>\n\t";

    private string MakeStylesheetTag(string src, string url, ViteChunk? chunk) =>
        $"<link {ParseAttributes(ResolveStylesheetTagAttributes(src, url, chunk))} />\n\t";

    private string? IntegrityValue(ViteChunk? chunk)
    {
        if (Options.IntegrityKey == null || chunk == null) return null;
        return Options.IntegrityKey == "integrity"
            ? chunk.Integrity
            : null;
    }

    private List<KeyValuePair<string, object?>> ResolveScriptTagAttributes(string src, string url, ViteChunk? chunk)
    {
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("type", "module"),
            new("src", url),
            new("nonce", Nonce),
        };

        if (IntegrityValue(chunk) is { } integrity) attributes.Add(new("integrity", integrity));

        foreach (var resolver in Options.ScriptTagAttributes)
            foreach (var attribute in resolver(src, url, chunk))
                attributes.Add(attribute);

        return attributes;
    }

    private List<KeyValuePair<string, object?>> ResolveStylesheetTagAttributes(string src, string url, ViteChunk? chunk)
    {
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("rel", "stylesheet"),
            new("href", url),
            new("nonce", Nonce),
        };

        if (IntegrityValue(chunk) is { } integrity) attributes.Add(new("integrity", integrity));

        foreach (var resolver in Options.StyleTagAttributes)
            foreach (var attribute in resolver(src, url, chunk))
                attributes.Add(attribute);

        return attributes;
    }

    private List<KeyValuePair<string, object?>>? ResolvePreloadTagAttributes(string src, string url, ViteChunk? chunk)
    {
        var isCss = IsCssPath(url);

        var attributes = new List<KeyValuePair<string, object?>>();
        if (isCss)
        {
            attributes.Add(new("rel", "preload"));
            attributes.Add(new("as", "style"));
            attributes.Add(new("href", url));
            attributes.Add(new("nonce", Nonce));
            if (CrossoriginOf(ResolveStylesheetTagAttributes(src, url, chunk)) is { } co) attributes.Add(new("crossorigin", co));
        }
        else
        {
            attributes.Add(new("rel", "modulepreload"));
            attributes.Add(new("as", "script"));
            attributes.Add(new("href", url));
            attributes.Add(new("nonce", Nonce));
            if (CrossoriginOf(ResolveScriptTagAttributes(src, url, chunk)) is { } co) attributes.Add(new("crossorigin", co));
        }

        if (IntegrityValue(chunk) is { } integrity) attributes.Add(new("integrity", integrity));

        foreach (var resolver in Options.PreloadTagAttributes)
        {
            var resolved = resolver(src, url, chunk);
            if (resolved == null) return null;
            foreach (var attribute in resolved) attributes.Add(attribute);
        }

        return attributes;
    }

    private static object? CrossoriginOf(IEnumerable<KeyValuePair<string, object?>> attributes)
    {
        foreach (var (key, value) in attributes)
        {
            if (key == "crossorigin" && value is not null and not false) return value;
        }

        return null;
    }

    private static string ParseAttributes(IEnumerable<KeyValuePair<string, object?>> attributes)
    {
        var parts = new List<string>();

        foreach (var (key, value) in attributes)
        {
            if (value is null || value is false) continue;

            if (value is true) parts.Add(key);
            else parts.Add($"{key}=\"{value}\"");
        }

        return string.Join(" ", parts);
    }

    private string? BuildPrefetchScript(string[] entryPoints, Dictionary<string, ViteChunk> manifest)
    {
        if (Options.PrefetchStrategy == PrefetchStrategy.None || IsRunningHot())
        {
            return null;
        }

        var discovered = new HashSet<string>();
        var chunks = new List<ViteChunk>();

        foreach (var entry in entryPoints)
        {
            if (!manifest.TryGetValue(entry, out var chunk)) continue;
            foreach (var import in chunk.DynamicImports) Visit(import);
        }

        var allChunks = new List<ViteChunk>(chunks);
        foreach (var chunk in chunks)
        {
            foreach (var css in chunk.Css)
            {
                var (_, cssChunk) = FindByFile(manifest, css);
                allChunks.Add(cssChunk);
            }
        }

        var assets = new List<Dictionary<string, string>>();
        var seenHref = new HashSet<string>();

        foreach (var chunk in allChunks)
        {
            if (string.IsNullOrEmpty(chunk.File)) continue;
            if (!(chunk.File.EndsWith(".js") || chunk.File.EndsWith(".css"))) continue;

            var asset = BuildPrefetchAsset(chunk);
            if (asset == null || !seenHref.Add(asset["href"])) continue;
            assets.Add(asset);
        }

        if (assets.Count == 0) return null;

        var json = JsonSerializer.Serialize(assets);
        var nonce = Nonce;
        var nonceAttribute = string.IsNullOrEmpty(nonce) ? "" : $" nonce=\"{nonce}\"";
        var prefetchEvent = Options.PrefetchEvent;

        string script;
        if (Options.PrefetchStrategy == PrefetchStrategy.Aggressive)
        {
            script =
                $"<script{nonceAttribute}>" +
                $"window.addEventListener('{prefetchEvent}', () => window.setTimeout(() => {{" +
                "const makeLink = (asset) => {" +
                "const link = document.createElement('link');" +
                "Object.keys(asset).forEach((attribute) => {" +
                "link.setAttribute(attribute, asset[attribute]);" +
                "});" +
                "return link;" +
                "};" +
                "const fragment = new DocumentFragment;" +
                $"{json}.forEach((asset) => fragment.append(makeLink(asset)));" +
                "document.head.append(fragment);" +
                "}))</script>";
        }
        else
        {
            script =
                $"<script{nonceAttribute}>" +
                $"window.addEventListener('{prefetchEvent}', () => window.setTimeout(() => {{" +
                "const makeLink = (asset) => {" +
                "const link = document.createElement('link');" +
                "Object.keys(asset).forEach((attribute) => {" +
                "link.setAttribute(attribute, asset[attribute]);" +
                "});" +
                "return link;" +
                "};" +
                "const loadNext = (assets, count) => window.setTimeout(() => {" +
                "if (count > assets.length) { count = assets.length; if (count === 0) { return; } }" +
                "const fragment = new DocumentFragment;" +
                "while (count > 0) {" +
                "const link = makeLink(assets.shift());" +
                "fragment.append(link);" +
                "count--;" +
                "if (assets.length) { link.onload = () => loadNext(assets, 1); link.onerror = () => loadNext(assets, 1); }" +
                "}" +
                "document.head.append(fragment);" +
                "});" +
                $"loadNext({json}, {Options.PrefetchConcurrency});" +
                "}))</script>";
        }

        return script;

        void Visit(string key)
        {
            if (!discovered.Add(key)) return;
            if (!manifest.TryGetValue(key, out var current) || string.IsNullOrEmpty(current.File)) return;

            chunks.Add(current);
            foreach (var import in current.Imports) Visit(import);
            foreach (var dynamicImport in current.DynamicImports) Visit(dynamicImport);
        }
    }

    private Dictionary<string, string>? BuildPrefetchAsset(ViteChunk chunk)
    {
        var url = AssetUrl(chunk.File!);
        var attributes = ResolvePreloadTagAttributes(chunk.Src ?? "", url, chunk);
        if (attributes == null) return null;

        var asset = new Dictionary<string, string>();
        foreach (var (key, value) in attributes)
        {
            if (value is null || value is false) continue;
            asset[key] = value is true ? key : value.ToString()!;
        }

        asset["rel"] = "prefetch";
        asset["fetchpriority"] = "low";
        asset["href"] = url;

        return asset;
    }

    /// <summary>
    /// Get the content of the given asset from the manifest.
    /// </summary>
    public string Content(string entryPoint)
    {
        var chunk = GetChunk(ReadManifest(), entryPoint);
        var relative = string.IsNullOrEmpty(Options.BuildDirectory)
            ? chunk.File!
            : $"{Options.BuildDirectory}/{chunk.File}";
        var path = GetPublicPathForFile(relative);

        if (!_fileSystem.File.Exists(path))
        {
            throw new Exception("Asset not found: " + path);
        }

        return _fileSystem.File.ReadAllText(path);
    }

    /// <summary>
    /// Resolve the public URL for the given manifest entry point.
    /// </summary>
    public string Asset(string entryPoint)
    {
        if (IsRunningHot()) return HotAsset(entryPoint);

        var chunk = GetChunk(ReadManifest(), entryPoint);
        return AssetUrl(chunk.File!);
    }

    private static bool IsCssPath(string path) => CssPathRegex.IsMatch(path);

    private string HotAsset(string path)
    {
        var hotFilePath = GetPublicPathForFile(Options.HotFile);
        var hotContents = _fileSystem.File.ReadAllText(hotFilePath);

        return hotContents + "/" + path;
    }

    private string AssetUrl(string path)
    {
        if (IsRunningHot()) return HotAsset(path);

        var pieces = new List<string>();
        if (!string.IsNullOrEmpty(Options.BuildDirectory))
        {
            pieces.Add(Options.BuildDirectory);
        }

        pieces.Add(path);
        return "/" + string.Join("/", pieces);
    }

    public bool IsRunningHot() => _fileSystem.File.Exists(GetPublicPathForFile(Options.HotFile));

    private Dictionary<string, ViteChunk> ReadManifest()
    {
        var path = GetBuildPathForFile(Options.ManifestFilename);
        if (!_fileSystem.File.Exists(path))
        {
            throw new Exception("Vite Manifest is missing. Run `npm run build` and try again.");
        }

        return DeserializeManifest(_fileSystem.File.ReadAllText(path));
    }

    private async Task<Dictionary<string, ViteChunk>> ReadManifestWithCacheAsync()
    {
        var manifestPath = GetBuildPathForFile(Options.ManifestFilename);
        if (!_fileSystem.File.Exists(manifestPath))
        {
            throw new Exception("Vite Manifest is missing. Run `npm run build` and try again.");
        }

        var manifest = await ReadManifestStringWithCacheAsync(manifestPath).ConfigureAwait(false);
        return DeserializeManifest(manifest);
    }

    private static Dictionary<string, ViteChunk> DeserializeManifest(string json)
    {
        Dictionary<string, ViteChunk>? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Dictionary<string, ViteChunk>>(json);
        }
        catch (Exception)
        {
            throw new Exception("Vite Manifest is invalid. Run `npm run build` and try again.");
        }

        if (manifest == null)
        {
            throw new Exception("Vite Manifest is invalid. Run `npm run build` and try again.");
        }

        return manifest;
    }

    private async Task<string> ReadManifestStringWithCacheAsync(string manifestPath)
    {
        var now = DateTime.UtcNow;
        lock (_cacheLock)
        {
            if (_cachedManifest != null && now - _lastManifestRead < CacheDuration)
            {
                return _cachedManifest;
            }
        }

        var manifest = await Task.Run(() => _fileSystem.File.ReadAllText(manifestPath)).ConfigureAwait(false);

        lock (_cacheLock)
        {
            _cachedManifest = manifest;
            _lastManifestRead = now;
        }

        return manifest;
    }

    internal void ClearManifestCache()
    {
        lock (_cacheLock)
        {
            _cachedManifest = null;
            _lastManifestRead = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Get the raw manifest JSON, or null when no manifest exists.
    /// </summary>
    public string? GetManifest()
    {
        var path = GetBuildPathForFile(Options.ManifestFilename);
        return _fileSystem.File.Exists(path)
            ? _fileSystem.File.ReadAllText(path)
            : null;
    }

    /// <summary>
    /// Get a hash of the manifest file, or null when running hot or missing.
    /// </summary>
    public string? GetManifestHash()
    {
        if (IsRunningHot()) return null;
        return GetManifest()?.MD5();
    }
}

public static class Vite
{
    private static IViteBuilder _instance = default!;

    internal static void UseBuilder(IViteBuilder instance) => _instance = instance;

    private static IViteBuilder Instance => _instance
        ?? throw new InvalidOperationException(
            "Vite has not been configured. Call builder.Services.AddViteHelper() and app.UseInertia() first.");

    /// <summary>
    /// Generates various tags from the given input files.
    /// </summary>
    public static HtmlString Input(params string[] entryPoints) => Instance.Input(entryPoints);

    /// <summary>
    /// Asynchronously generates various tags from the given input files.
    /// </summary>
    public static Task<HtmlString> InputAsync(params string[] entryPoints) => Instance.InputAsync(entryPoints);

    /// <summary>
    /// Generate React refresh runtime script.
    /// </summary>
    public static HtmlString ReactRefresh() => Instance.ReactRefresh();

    /// <summary>
    /// Asynchronously generate React refresh runtime script.
    /// </summary>
    public static Task<HtmlString> ReactRefreshAsync() => Instance.ReactRefreshAsync();

    /// <summary>
    /// Determine whether the Vite dev server is running.
    /// </summary>
    public static bool IsRunningHot() => Instance.IsRunningHot();

    /// <summary>
    /// Resolve the public URL for a manifest entry point.
    /// </summary>
    public static string Asset(string entryPoint) => Instance.Asset(entryPoint);

    /// <summary>
    /// Get the content of a manifest entry point.
    /// </summary>
    public static string Content(string entryPoint) => Instance.Content(entryPoint);

    /// <summary>
    /// Generate the Manifest hash, or null when running hot.
    /// </summary>
    public static string? GetManifestHash() => Instance.GetManifestHash();
}
