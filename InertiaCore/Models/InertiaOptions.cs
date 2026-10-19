using System;

namespace InertiaCore.Models;

public class InertiaOptions
{
    public string RootView { get; set; } = "~/Views/App.cshtml";

    public bool SsrEnabled { get; set; } = false;
    public string SsrUrl { get; set; } = "http://127.0.0.1:13714/render";

    /// <summary>
    /// Timeout applied to a single SSR render request. When the SSR server does not
    /// respond in time the response falls back to client-side rendering.
    /// </summary>
    public TimeSpan SsrTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum size, in bytes, of an SSR response body that will be read and parsed.
    /// Larger responses are treated as a failure and fall back to client-side rendering.
    /// </summary>
    public long SsrMaxResponseBytes { get; set; } = 10 * 1024 * 1024;

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

    /// <summary>
    /// Options for the Inertia <c>XSRF-TOKEN</c> cookie bridge. Enable
    /// <see cref="InertiaAntiforgeryOptions.Enabled"/> and call <c>UseInertiaAntiforgery()</c>
    /// to issue the token cookie and validate the request header.
    /// </summary>
    public InertiaAntiforgeryOptions Antiforgery { get; set; } = new();

    /// <summary>
    /// Options for Inertia error pages and cookie-auth redirect conversion. Enable
    /// <see cref="InertiaStatusPagesOptions.Enabled"/> and call <c>UseInertiaStatusPages()</c>
    /// to render the error component for handled status codes.
    /// </summary>
    public InertiaStatusPagesOptions StatusPages { get; set; } = new();
}
