using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;
using System.IO.Abstractions;
using InertiaCore.Models;
using Microsoft.Extensions.Options;
using InertiaCore.Extensions;

namespace InertiaCore.Utils;

public interface IViteBuilder
{
    HtmlString ReactRefresh();
    HtmlString Input(string path);
    string? GetManifest();
}

internal class ViteBuilder : IViteBuilder
{
    private IFileSystem _fileSystem;
    private readonly IOptions<ViteOptions> _options;
    private readonly object _cacheLock = new();
    private string? _cachedManifest;
    private DateTime _lastManifestRead = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public ViteBuilder(IOptions<ViteOptions> options) => (_fileSystem, _options) = (new FileSystem(), options);

    protected internal void UseFileSystem(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    private string GetPublicPathForFile(string path)
    {
        var pieces = new List<string> {
            _options.Value.PublicDirectory,
            path
        };

        return string.Join("/", pieces);
    }

    private string GetBuildPathForFile(string path)
    {
        var pieces = new List<string> { _options.Value.PublicDirectory };
        if (!string.IsNullOrEmpty(_options.Value.BuildDirectory))
        {
            pieces.Add(_options.Value.BuildDirectory);
        }

        pieces.Add(path);
        return string.Join("/", pieces);
    }

    /// <summary>
    /// Generates various tags from a given input file path.
    /// </summary>
    [Obsolete("Use InputAsync instead. This method uses synchronous file I/O.")]
    public HtmlString Input(string path)
    {
        if (IsRunningHot())
        {
            return new HtmlString(MakeModuleTag("@vite/client").Value + MakeModuleTag(path).Value);
        }

        if (!_fileSystem.File.Exists(GetBuildPathForFile(_options.Value.ManifestFilename)))
        {
            throw new Exception("Vite Manifest is missing. Run `npm run build` and try again.");
        }

        var manifest = _fileSystem.File.ReadAllText(GetBuildPathForFile(_options.Value.ManifestFilename));
        return ProcessManifest(path, manifest);
    }

    /// <summary>
    /// Asynchronously generates various tags from a given input file path.
    /// Uses file I/O abstraction and caching for production builds.
    /// </summary>
    public async Task<HtmlString> InputAsync(string path, CancellationToken cancellationToken = default)
    {
        if (IsRunningHot())
        {
            return new HtmlString(MakeModuleTag("@vite/client").Value + MakeModuleTag(path).Value);
        }

        var manifestPath = GetBuildPathForFile(_options.Value.ManifestFilename);

        // Check cache before reading
        if (!_fileSystem.File.Exists(manifestPath))
        {
            throw new Exception("Vite Manifest is missing. Run `npm run build` and try again.");
        }

        var manifest = await ReadManifestWithCacheAsync(manifestPath, cancellationToken);
        return ProcessManifest(path, manifest);
    }

    /// <summary>
    /// Generate React refresh runtime script.
    /// </summary>
    [Obsolete("Use ReactRefreshAsync instead. This method uses synchronous file I/O.")]
    public HtmlString ReactRefresh()
    {
        if (!IsRunningHot())
        {
            return new HtmlString("<!-- no hot -->");
        }

        var builder = new TagBuilder("script");
        builder.Attributes.Add("type", "module");

        var inner = $"import RefreshRuntime from '{Asset("@react-refresh")}';" +
                    "RefreshRuntime.injectIntoGlobalHook(window);" +
                    "window.$RefreshReg$ = () => { };" +
                    "window.$RefreshSig$ = () => (type) => type;" +
                    "window.__vite_plugin_react_preamble_installed__ = true;";

        builder.InnerHtml.AppendHtml(inner);

        return new HtmlString(GetString(builder));
    }

    /// <summary>
    /// Asynchronously generate React refresh runtime script.
    /// </summary>
    public Task<HtmlString> ReactRefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunningHot())
        {
            return Task.FromResult(new HtmlString("<!-- no hot -->"));
        }

        var builder = new TagBuilder("script");
        builder.Attributes.Add("type", "module");

        var inner = $"import RefreshRuntime from '{Asset("@react-refresh")}';" +
                    "RefreshRuntime.injectIntoGlobalHook(window);" +
                    "window.$RefreshReg$ = () => { };" +
                    "window.$RefreshSig$ = () => (type) => type;" +
                    "window.__vite_plugin_react_preamble_installed__ = true;";

        builder.InnerHtml.AppendHtml(inner);

        return Task.FromResult(new HtmlString(GetString(builder)));
    }

    private HtmlString ProcessManifest(string path, string manifest)
    {
        var manifestJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(manifest);

        if (manifestJson == null)
        {
            throw new Exception("Vite Manifest is invalid. Run `npm run build` and try again.");
        }

        if (!manifestJson.TryGetValue(path, out var obj))
        {
            throw new Exception("Asset not found in manifest: " + path);
        }

        var filePath = obj.GetProperty("file");

        if (IsCssPath(filePath.ToString()))
        {
            return MakeTag(filePath.ToString());
        }

        var html = MakeTag(filePath.ToString());

        try
        {
            var css = obj.GetProperty("css");
            return css.EnumerateArray().Aggregate(html,
                (current, item) => new HtmlString(current.Value + MakeTag(item.ToString()).Value));
        }
        catch (Exception)
        {
            // ignored
        }

        return html;
    }

    private async Task<string> ReadManifestWithCacheAsync(string manifestPath, CancellationToken cancellationToken)
    {
        // Simple time-based cache to avoid reading the manifest file on every request.
        var now = DateTime.UtcNow;
        lock (_cacheLock)
        {
            if (_cachedManifest != null && now - _lastManifestRead < CacheDuration)
            {
                return _cachedManifest;
            }
        }

        // Read with async file I/O via the IFileSystem abstraction.
        var manifest = await Task.Run(() => _fileSystem.File.ReadAllText(manifestPath), cancellationToken);

        lock (_cacheLock)
        {
            _cachedManifest = manifest;
            _lastManifestRead = now;
        }

        return manifest;
    }

    private HtmlString MakeModuleTag(string path)
    {
        var builder = new TagBuilder("script");
        builder.Attributes.Add("type", "module");
        builder.Attributes.Add("src", Asset(path));

        return new HtmlString(GetString(builder) + "\n\t");
    }

    private HtmlString MakeTag(string url)
    {
        return IsCssPath(url) ? MakeStylesheetTag(url) : MakeModuleTag(url);
    }

    private HtmlString MakeStylesheetTag(string filePath)
    {
        var builder = new TagBuilder("link");
        builder.Attributes.Add("rel", "stylesheet");
        builder.Attributes.Add("href", Asset(filePath));
        return new HtmlString(GetString(builder).Replace("></link>", " />") + "\n\t");
    }

    private static bool IsCssPath(string path)
    {
        return Regex.IsMatch(path, @".\.(css|less|sass|scss|styl|stylus|pcss|postcss)", RegexOptions.IgnoreCase);
    }

    private string HotAsset(string path)
    {
        var hotFilePath = GetPublicPathForFile(_options.Value.HotFile);
        var hotContents = _fileSystem.File.ReadAllText(hotFilePath);

        return hotContents + "/" + path;
    }

    private string Asset(string path)
    {
        if (IsRunningHot())
        {
            return HotAsset(path);
        }

        var pieces = new List<string>();
        if (!string.IsNullOrEmpty(_options.Value.BuildDirectory))
        {
            pieces.Add(_options.Value.BuildDirectory);
        }

        pieces.Add(path);
        return "/" + string.Join("/", pieces);
    }

    private bool IsRunningHot()
    {
        return _fileSystem.File.Exists(GetPublicPathForFile(_options.Value.HotFile));
    }

    private static string GetString(IHtmlContent content)
    {
        var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    internal void ClearManifestCache()
    {
        lock (_cacheLock)
        {
            _cachedManifest = null;
            _lastManifestRead = DateTime.MinValue;
        }
    }

    public string? GetManifest()
    {
        return _fileSystem.File.Exists(GetBuildPathForFile(_options.Value.ManifestFilename))
            ? _fileSystem.File.ReadAllText(GetBuildPathForFile(_options.Value.ManifestFilename))
            : null;
    }
}

public static class Vite
{
    private static IViteBuilder _instance = default!;

    internal static void UseBuilder(IViteBuilder instance) => _instance = instance;

    /// <summary>
    /// Generates various tags from a given input file path.
    /// </summary>
    public static HtmlString Input(string path) => _instance.Input(path);

    /// <summary>
    /// Generate React refresh runtime script.
    /// </summary>
    public static HtmlString ReactRefresh() => _instance.ReactRefresh();

    /// <summary>
    /// Generate the Manifest hash.
    /// </summary>
    public static string? GetManifestHash() => _instance.GetManifest()?.MD5();
}
