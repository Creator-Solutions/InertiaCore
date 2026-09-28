using System;

namespace InertiaCore.Models;

public class InertiaOptions
{
    public string RootView { get; set; } = "~/Views/App.cshtml";

    public bool SsrEnabled { get; set; } = false;
    public string SsrUrl { get; set; } = "http://127.0.0.1:13714/render";
    public bool EncryptHistory { get; set; } = false;

    /// <summary>
    /// The current asset version. Defaults to an empty string, which tells the
    /// Inertia client that this server does not track asset versions.
    /// </summary>
    public string? Version { get; set; } = "";

    public Func<IServiceProvider, string>? VersionResolver { get; set; }

    /// <summary>
    /// When enabled, rendering a component that has not been declared with
    /// <c>[assembly: InertiaPage("...")]</c> throws an <see cref="InvalidOperationException"/>.
    /// Disabled by default so existing applications are unaffected.
    /// </summary>
    public bool ValidatePages { get; set; } = false;
}
