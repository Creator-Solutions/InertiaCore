using System.Net;
using InertiaCore.Contracts;
using InertiaCore.Filters;
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
#pragma warning disable CS0618 // Internal usage of the deprecated static facade is intentional
    public static IApplicationBuilder UseInertia(this IApplicationBuilder app)
    {
        // IResponseFactory is scoped (shares InertiaState with the per-request IInertia).
        // We create a scope here to resolve it without triggering the "scoped from root"
        // validation. The scope lives for the app lifetime since the factory is held
        // by the static Inertia facade.
        var startupScope = app.ApplicationServices.CreateScope();
        var factory = startupScope.ServiceProvider.GetRequiredService<IResponseFactory>();
        var contextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
        Inertia.UseFactory(factory, contextAccessor);

        var viteBuilder = app.ApplicationServices.GetService<IViteBuilder>();
        if (viteBuilder != null)
        {
            Vite.UseBuilder(viteBuilder);
            Inertia.Version(Vite.GetManifestHash);
        }

        app.Use(async (context, next) =>
        {
            var resolver = context.RequestServices.GetRequiredService<IInertiaVersionResolver>();
            var serverVersion = resolver.GetVersion();
            
            if (!string.IsNullOrEmpty(serverVersion)
                && context.IsInertiaRequest()
                && context.Request.Headers[InertiaHeader.Version] != serverVersion)
            {
                await OnVersionChange(context, app);
                return;
            }
            await next();
        });

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
            if (opt.Version != null)
            {
                return new DefaultInertiaVersionProvider(opt.Version);
            }
            throw new InvalidOperationException("Inertia version or version resolver must be configured in InertiaOptions.");
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
            var version = sp.GetRequiredService<IOptions<InertiaOptions>>().Value.Version;
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

    private static async Task OnVersionChange(HttpContext context, IApplicationBuilder app)
    {
        var tempData = context.RequestServices.GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(context);

        if (tempData.Any()) tempData.Keep();

        context.Response.Headers.Override(InertiaHeader.Location, context.RequestedUri());
        context.Response.StatusCode = (int)HttpStatusCode.Conflict;

        await context.Response.CompleteAsync();
    }
}
#pragma warning restore CS0618
