using System;
using System.IO.Abstractions;
using System.Net.Http;
using InertiaCore.DevServer;
using InertiaCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InertiaCore.Extensions;

/// <summary>
/// Development-time helper that starts the Vite dev server alongside the application.
/// </summary>
public static class ViteDevServerExtensions
{
    /// <summary>
    /// Starts the configured package-manager script (for example <c>npm run dev</c>),
    /// waits for Vite's hot file to appear and stops the process when the application
    /// shuts down. This lets <c>dotnet run</c> bring up the frontend with HMR without a
    /// second terminal.
    /// </summary>
    /// <remarks>
    /// The helper is a no-op outside the Development environment and when the dev server
    /// is already running (the hot file exists and the URL it contains is reachable).
    /// Options are bound from the <c>Vite:DevServer</c> configuration section when
    /// present, then the <paramref name="configure"/> delegate is applied.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">An optional delegate that overrides the bound options.</param>
    /// <returns>The application builder, for chaining.</returns>
    public static IApplicationBuilder UseViteDevServer(
        this IApplicationBuilder app,
        Action<ViteDevServerOptions>? configure = null)
    {
        var services = app.ApplicationServices;

        var environment = services.GetService<IHostEnvironment>();
        if (environment is null || !environment.IsDevelopment())
        {
            return app;
        }

        var logger = services
            .GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(ViteDevServerExtensions))
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        var options = new ViteDevServerOptions();
        services.GetService<IConfiguration>()?.GetSection("Vite:DevServer").Bind(options);
        configure?.Invoke(options);

        var viteOptions = services.GetService<IOptions<ViteOptions>>();
        if (viteOptions is null)
        {
            logger.LogDebug(
                "Vite options are not registered; call AddViteHelper() so the hot file path can be resolved.");
        }

        var launcher = services.GetService<IViteDevProcessLauncher>() ?? new ViteDevProcessLauncher();
        var fileSystem = services.GetService<IFileSystem>() ?? new FileSystem();
        var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();

        var contentRoot = environment.ContentRootPath;

        var serverLogger = services.GetService<ILogger<ViteDevServer>>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ViteDevServer>.Instance;

        var server = new ViteDevServer(
            launcher,
            fileSystem,
            httpClientFactory,
            Options.Create(options),
            viteOptions ?? Options.Create(new ViteOptions()),
            serverLogger,
            contentRoot);

        var lifetime = services.GetService<IHostApplicationLifetime>();
        if (lifetime is not null)
        {
            lifetime.ApplicationStopping.Register(() => server.DisposeAsync().AsTask().GetAwaiter().GetResult());
        }
        else
        {
            logger.LogWarning(
                "IHostApplicationLifetime is not registered; the Vite dev server process may not be stopped on shutdown.");
        }

        server.StartAsync(lifetime?.ApplicationStopping ?? default).GetAwaiter().GetResult();

        return app;
    }
}
