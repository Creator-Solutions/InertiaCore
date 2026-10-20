using System;

namespace InertiaCore.Models;

/// <summary>
/// Options for the development-time Vite dev server helper started by
/// <c>UseViteDevServer()</c>. The helper runs the configured package-manager script,
/// waits for Vite's hot file to appear and stops the process when the application
/// shuts down.
/// </summary>
public class ViteDevServerOptions
{
    /// <summary>
    /// The package manager used to launch the script. Defaults to
    /// <see cref="VitePackageManager.Npm"/>.
    /// </summary>
    public VitePackageManager PackageManager { get; set; } = VitePackageManager.Npm;

    /// <summary>
    /// The directory the script runs in. When <see langword="null"/> the helper
    /// resolves the directory that contains the project's <c>package.json</c> while
    /// walking up from the content root, falling back to the content root itself.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// The package-manager script to run. Defaults to <c>dev</c>.
    /// </summary>
    public string ScriptName { get; set; } = "dev";

    /// <summary>
    /// How long to wait for the hot file to appear before giving up. Defaults to
    /// 60 seconds. The process is stopped when the timeout elapses.
    /// </summary>
    public TimeSpan ReadyTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The hot file whose appearance marks the dev server as ready. When
    /// <see langword="null"/> the value is taken from
    /// <see cref="ViteOptions.HotFile"/> (resolved under
    /// <see cref="ViteOptions.PublicDirectory"/>).
    /// </summary>
    public string? HotFile { get; set; }
}
