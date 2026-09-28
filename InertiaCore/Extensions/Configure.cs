using System.Net;
using InertiaCore.Contracts;
using InertiaCore.Filters;
using InertiaCore.Middleware;
using InertiaCore.Models;
using InertiaCore.Resolvers;
using InertiaCore.Services;
using InertiaCore.Services.Version;
using InertiaCore.Ssr;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace InertiaCore.Extensions;

public static class Configure
{
    public static IApplicationBuilder UseInertia(this IApplicationBuilder app)
    {
        // The static facade resolves IResponseFactory/IInertia from the current
        // request scope. We only capture the accessor here, never a scoped service.
        var contextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
        Inertia.UseContextAccessor(contextAccessor);

        var viteBuilder = app.ApplicationServices.GetService<IViteBuilder>();
        if (viteBuilder != null)
        {
            Vite.UseBuilder(viteBuilder);

            // Only fall back to the Vite manifest hash when the application has not
            // configured its own version or version resolver.
            var options = app.ApplicationServices.GetRequiredService<IOptions<InertiaOptions>>().Value;
            if (options.VersionResolver == null && string.IsNullOrEmpty(options.Version))
            {
                options.VersionResolver = _ => Vite.GetManifestHash() ?? "";
            }
        }

        // Version-mismatch short-circuit (runs before the response-shaping middleware).
        app.Use(async (context, next) =>
        {
            var resolver = context.RequestServices.GetRequiredService<IInertiaVersionResolver>();
            var serverVersion = resolver.GetVersion();

            if (!string.IsNullOrEmpty(serverVersion)
                && context.IsInertiaRequest()
                && context.Request.Headers[InertiaHeader.Version] != serverVersion)
            {
                await OnVersionChange(context);
                return;
            }
            await next();
        });

        // Response-shaping middleware: empty-response redirects, X-Inertia headers, error bag.
        app.UseMiddleware<InertiaMiddleware>();

        return app;
    }

    public static IServiceCollection AddInertia(this IServiceCollection services,
        Action<InertiaOptions>? options = null)
    {
        if (options != null) services.Configure(options);

        services.AddHttpContextAccessor();
        services.AddHttpClient();

        services.AddScoped<IErrorBagService, ErrorBagService>();
        services.AddScoped<InertiaValidationFilter>();

        services.AddScoped<IInertiaVersionProvider>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<InertiaOptions>>().Value;
            if (opt.VersionResolver != null)
            {
                return new DelegateInertiaVersionProvider(() => opt.VersionResolver(sp));
            }

            // An unset version is valid: the protocol treats an empty version as
            // "this server does not track asset versions".
            return new DefaultInertiaVersionProvider(opt.Version ?? "");
        });

        services.AddScoped<IInertiaVersionResolver>(sp =>
        {
            var provider = sp.GetRequiredService<IInertiaVersionProvider>();
            return new DelegateInertiaVersionResolver(providerSp => providerSp.GetRequiredService<IInertiaVersionProvider>().GetVersion(), sp);
        });

        // Per-request state container — seed Version from InertiaOptions so that
        // the asset version is available consistently across the middleware check
        // and response body without requiring middleware wiring or static facade calls.
        services.AddScoped<InertiaState>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<InertiaOptions>>().Value;
            var version = opt.VersionResolver?.Invoke(sp) ?? opt.Version;
            return new InertiaState { Version = version };
        });

        // InertiaService is the public API that consumers should inject via IInertia.
        // Registered as Scoped — one instance per HTTP request.
        services.AddScoped<IInertia, InertiaService>();

        // Internal factory — scoped so it shares the same per-request InertiaState
        // as InertiaService, ensuring Share(), Flash(), and Version calls via IInertia
        // are visible to the response pipeline.
        services.AddScoped<IResponseFactory, ResponseFactory>();

        // Gateway is safe as Singleton since IHttpClientFactory manages HttpClient lifetimes.
        services.AddSingleton<IGateway, Gateway>();

        services.Configure<MvcOptions>(mvcOptions =>
        {
            mvcOptions.Filters.Add<InertiaActionFilter>();
            mvcOptions.Filters.Add<InertiaValidationFilter>();
        });

        return services;
    }

    public static IServiceCollection AddViteHelper(this IServiceCollection services,
        Action<ViteOptions>? options = null)
    {
        services.AddSingleton<IViteBuilder, ViteBuilder>();
        if (options != null) services.Configure(options);

        return services;
    }

    private static async Task OnVersionChange(HttpContext context)
    {
        // TempData is MVC-only; Minimal API hosts do not register it.
        var tempDataFactory = context.RequestServices.GetService<ITempDataDictionaryFactory>();
        if (tempDataFactory != null)
        {
            var tempData = tempDataFactory.GetTempData(context);
            if (tempData.Any()) tempData.Keep();
        }

        context.Response.Headers.Override(InertiaHeader.Location, context.RequestedUri());
        context.Response.StatusCode = (int)HttpStatusCode.Conflict;

        await context.Response.CompleteAsync();
    }
}
