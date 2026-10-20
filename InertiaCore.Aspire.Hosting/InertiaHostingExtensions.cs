using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;

namespace InertiaCore.Aspire.Hosting;

/// <summary>
/// Extension methods that model an Inertia web application as .NET Aspire resources.
/// The Vite dev server and the Node SSR process are added as separate resources so
/// their endpoints, health and logs appear in the Aspire dashboard, and the web
/// project is wired to them through environment variables.
/// </summary>
public static class InertiaHostingExtensions
{
    /// <summary>
    /// The app setting that points the Vite helper at a running dev server. Bound to
    /// <c>ViteOptions.HotUrl</c> by <c>AddViteHelper(IConfiguration)</c>.
    /// </summary>
    public const string ViteHotUrlSetting = "Vite__HotUrl";

    /// <summary>
    /// The app setting that enables server-side rendering. Bound to
    /// <c>InertiaOptions.SsrEnabled</c> by <c>AddInertia(IConfiguration)</c>.
    /// </summary>
    public const string SsrEnabledSetting = "Inertia__SsrEnabled";

    /// <summary>
    /// The app setting that points the render gateway at the Node SSR process. Bound
    /// to <c>InertiaOptions.SsrUrl</c> by <c>AddInertia(IConfiguration)</c>.
    /// </summary>
    public const string SsrUrlSetting = "Inertia__SsrUrl";

    /// <summary>
    /// Adds a Vite dev server resource for the given <paramref name="appDirectory"/>.
    /// Aspire allocates an HTTP endpoint and exposes it to the dashboard.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="appDirectory">The directory containing the Vite app.</param>
    /// <param name="runScriptName">The npm script that starts the dev server. Defaults to <c>dev</c>.</param>
    /// <returns>The Vite app resource builder.</returns>
    public static IResourceBuilder<ViteAppResource> AddInertiaViteApp(
        this IDistributedApplicationBuilder builder,
        string name,
        string appDirectory,
        string runScriptName = "dev")
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddViteApp(name, appDirectory, runScriptName);
    }

    /// <summary>
    /// Adds the Node SSR process for the given <paramref name="appDirectory"/>. An HTTP
    /// endpoint is allocated and a health check is registered against
    /// <paramref name="healthPath"/> so the dashboard reports readiness.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="appDirectory">The directory containing the SSR entry script.</param>
    /// <param name="scriptPath">The SSR script relative to <paramref name="appDirectory"/>.</param>
    /// <param name="healthPath">The health endpoint served by the SSR process.</param>
    /// <returns>The Node app resource builder.</returns>
    /// <remarks>
    /// The Inertia node gateway reads the port from <c>PORT</c>. Registering the
    /// endpoint with <c>env: "PORT"</c> lets Aspire assign a free port and inject it
    /// into the child process.
    /// </remarks>
    public static IResourceBuilder<NodeAppResource> AddInertiaSsrApp(
        this IDistributedApplicationBuilder builder,
        string name,
        string appDirectory,
        string scriptPath = "ssr.mjs",
        string healthPath = "/health")
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddNodeApp(name, appDirectory, scriptPath)
            .WithHttpEndpoint(env: "PORT")
            .WithHttpHealthCheck(healthPath);
    }

    /// <summary>
    /// Wires an Inertia web project to the Vite dev server and SSR resources added by
    /// <see cref="AddInertiaViteApp"/> and <see cref="AddInertiaSsrApp"/>. The web
    /// project waits for both resources to become healthy and receives the settings the
    /// Inertia core reads when registered with the configuration-aware overloads.
    /// </summary>
    /// <typeparam name="T">The web project resource type.</typeparam>
    /// <param name="web">The web project resource builder.</param>
    /// <param name="vite">The Vite dev server resource.</param>
    /// <param name="ssr">The Node SSR resource.</param>
    /// <returns>The web project resource builder.</returns>
    /// <remarks>
    /// The Vite dev server URL is exposed as <see cref="ViteHotUrlSetting"/> and the SSR
    /// gateway as <see cref="SsrUrlSetting"/> (the SSR endpoint plus <c>/render</c>).
    /// Register the web application with
    /// <c>AddInertia(IConfiguration)</c> and <c>AddViteHelper(IConfiguration)</c> so
    /// those settings are applied.
    /// </remarks>
    public static IResourceBuilder<T> WithInertiaReferences<T>(
        this IResourceBuilder<T> web,
        IResourceBuilder<ViteAppResource> vite,
        IResourceBuilder<NodeAppResource> ssr)
        where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(vite);
        ArgumentNullException.ThrowIfNull(ssr);

        var viteEndpoint = vite.GetEndpoint("http");
        var ssrEndpoint = ssr.GetEndpoint("http");

        return web
            .WithEnvironment(ViteHotUrlSetting, ReferenceExpression.Create($"{viteEndpoint}"))
            .WithEnvironment(SsrEnabledSetting, "true")
            .WithEnvironment(SsrUrlSetting, ReferenceExpression.Create($"{ssrEndpoint}/render"))
            .WithReference(vite)
            .WithReference(ssr)
            .WaitFor(vite)
            .WaitFor(ssr);
    }
}
