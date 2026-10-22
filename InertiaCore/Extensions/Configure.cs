using System.Net;
using InertiaCore.Contracts;
using InertiaCore.DevServer;
using InertiaCore.Filters;
using InertiaCore.Middleware;
using InertiaCore.Models;
using InertiaCore.Resolvers;
using InertiaCore.Services;
using InertiaCore.Services.Version;
using InertiaCore.Ssr;
using InertiaCore.Utils;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
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
        app.Use(async (HttpContext context, Func<Task> next) =>
        {
            var resolver = context.RequestServices.GetRequiredService<IInertiaVersionResolver>();
            var serverVersion = resolver.GetVersion();

            // Per the protocol, only GET requests receive the mismatch 409; non-GET
            // requests proceed (the subsequent GET redirect may still 409).
            if (!string.IsNullOrEmpty(serverVersion)
                && context.IsInertiaRequest()
                && HttpMethods.IsGet(context.Request.Method)
                && context.Request.Headers[InertiaHeader.Version] != serverVersion)
            {
                await OnVersionChange(context, serverVersion);
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
        services.AddScoped<InertiaPrecognitionFilter>();

        // Flash persistence across requests (TempData-backed by default).
        services.TryAddScoped<IInertiaFlashProvider, InertiaFlashProvider>();

        // Size limit for cookie-backed flash payloads.
        services.AddOptions<InertiaFlashOptions>();

        // TryAddScoped so applications (and the validation epic) can replace the
        // default DataAnnotations-based validator with their own implementation.
        services.TryAddScoped<IInertiaRequestValidator, DataAnnotationsRequestValidator>();

        // Data protection backs the cookie-based flash store used when MVC TempData is absent.
        services.AddDataProtection();

        // Anti-forgery bridge: always register the service so [AutoValidateAntiforgeryToken]
        // and form endpoints resolve it, but only align the validation header with the
        // Inertia client when the bridge is enabled. The XSRF cookie itself is written by
        // UseInertiaAntiforgery().
        services.AddAntiforgery();
        services.AddOptions<AntiforgeryOptions>()
            .Configure<IOptions<InertiaOptions>>((antiforgery, inertia) =>
            {
                if (inertia.Value.Antiforgery.Enabled)
                    antiforgery.HeaderName = inertia.Value.Antiforgery.HeaderName;
            });

        // Status pages: wrap the default authorization result handler so a cookie-auth
        // challenge against an Inertia request becomes 409 + X-Inertia-Location instead
        // of a 302. The wrapper is inert unless InertiaOptions.StatusPages.Enabled is set.
        // Replace (rather than add) so exactly one handler is registered.
        services.AddAuthorization();
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler>(sp =>
            new InertiaAuthorizationMiddlewareResultHandler(
                new AuthorizationMiddlewareResultHandler(),
                sp.GetRequiredService<IOptions<InertiaOptions>>())));

#if NET10_0_OR_GREATER
        // The built-in minimal API validation reports failures through IProblemDetailsService.
        // Registering the Inertia writer first lets it turn those failures into Inertia
        // responses on endpoints that opted in with WithInertiaValidation.
        services.AddProblemDetails();
        services.Insert(0, ServiceDescriptor.Singleton<IProblemDetailsWriter, InertiaValidationProblemDetailsWriter>());
#endif

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

        // Shared data providers are resolved per request from RequestServices, in
        // registration order (see Response.ResolveSharedProps). The built-in claims provider
        // is registered here and contributes nothing unless ClaimsSharedData.Enabled is set.
        services.AddScoped<IInertiaSharedDataProvider, ClaimsSharedDataProvider>();

        // SSR typed client (IN-69): the standard resilience handler (total timeout, retry
        // with exponential backoff and jitter, circuit breaker and attempt timeout) wraps
        // every render call. The total budget is InertiaOptions.SsrTimeout; the remaining
        // values come from InertiaOptions.SsrResilience. The handler is registered
        // unconditionally but is only exercised when SSR is enabled (the gateway is the
        // only consumer), so applications that do not use SSR are unaffected. AddInertia
        // may be invoked more than once to layer options, but a second
        // AddStandardResilienceHandler would nest a second full resilience pipeline on the
        // same client (two breakers with independent timers), hence the marker guard.
        if (services.All(d => d.ServiceType != typeof(SsrHttpClientMarker)))
        {
            services.AddSingleton<SsrHttpClientMarker>();

            var ssrPipeline = services
                .AddHttpClient<InertiaSsrClient>(InertiaSsrClient.HttpClientName)
                .AddStandardResilienceHandler();

            services.AddOptions<HttpStandardResilienceOptions>(ssrPipeline.PipelineName)
                .Configure<IOptions<InertiaOptions>>((resilience, inertia) =>
                {
                    var ssr = inertia.Value;
                    var resilienceOptions = ssr.SsrResilience;

                    resilience.TotalRequestTimeout.Timeout = ssr.SsrTimeout;
                    resilience.AttemptTimeout.Timeout = resilienceOptions.AttemptTimeout ?? ssr.SsrTimeout;

                    resilience.Retry.MaxRetryAttempts = resilienceOptions.MaxRetryAttempts;
                    resilience.Retry.Delay = resilienceOptions.RetryBaseDelay;

                    resilience.CircuitBreaker.FailureRatio = resilienceOptions.CircuitBreakerFailureRatio;
                    resilience.CircuitBreaker.MinimumThroughput = resilienceOptions.CircuitBreakerMinimumThroughput;
                    resilience.CircuitBreaker.SamplingDuration = resilienceOptions.CircuitBreakerSamplingDuration;
                    resilience.CircuitBreaker.BreakDuration = resilienceOptions.CircuitBreakerBreakDuration;
                });
        }

        // Gateway is scoped so it can depend on the transient typed SSR client; HttpClient
        // lifetimes are managed by IHttpClientFactory.
        services.TryAddScoped<IGateway, Gateway>();

        // SSR health check (IN-69). Always registered (a no-op unless the application maps
        // health endpoints) and evaluated against the runtime options: reports Healthy when
        // SSR is disabled or the server answers, Degraded when the circuit breaker is open
        // (fail-fast, no probe) and Unhealthy when the SSR server cannot be reached.
        // AddInertia may be invoked more than once to layer options, but health-check
        // registrations are not idempotent (duplicate names throw), hence the marker guard.
        if (services.All(d => d.ServiceType != typeof(SsrHealthCheckMarker)))
        {
            services.AddSingleton<SsrHealthCheckMarker>();
            services.AddHealthChecks().AddCheck<InertiaSsrHealthCheck>(
                "inertia-ssr",
                tags: new[] { "ssr" });
        }

        // Managed SSR process (IN-68). Options validate on start. The launcher and the hosted
        // service are always registered but the service is a no-op unless
        // InertiaSsrOptions.Managed is enabled (and SSR is enabled, and the Vite dev server is
        // not running), so existing applications are unaffected. The Configure delegate below
        // runs after the caller's delegate (options delegates run in registration order) and
        // aligns SsrEnabled/SsrUrl with the managed port when the caller left the defaults.
        services.Configure<InertiaOptions>(inertia =>
        {
            if (!inertia.Ssr.Managed) return;

            if (!inertia.SsrEnabled) inertia.SsrEnabled = true;
            if (inertia.SsrUrl == new InertiaOptions().SsrUrl)
                inertia.SsrUrl = inertia.Ssr.RenderUrl;
        });

        services.AddOptions<InertiaOptions>().ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<InertiaOptions>, InertiaSsrOptionsValidator>());
        services.TryAddSingleton<IInertiaSsrProcessLauncher, ProcessLauncher>();
        services.AddHostedService<InertiaSsrHostedService>();

        services.Configure<MvcOptions>(mvcOptions =>
        {
            mvcOptions.Filters.Add<InertiaActionFilter>();
            mvcOptions.Filters.Add<InertiaValidationFilter>();

            // Order -3000 so precognitive requests are handled before MVC's automatic
            // 400-on-invalid-model filter (ModelStateInvalidFilter, order -2000).
            mvcOptions.Filters.Add<InertiaPrecognitionFilter>(-3000);
        });

        return services;
    }

    public static IServiceCollection AddViteHelper(this IServiceCollection services,
        Action<ViteOptions>? options = null)
    {
        services.AddSingleton<IViteBuilder, ViteBuilder>();

        // Dev-time helper launched by UseViteDevServer(). Registered here so a host can
        // swap in a test double; the extension falls back to a default when absent. The
        // HttpClient factory probes whether an already-running dev server is reachable.
        services.AddHttpClient();
        services.TryAddSingleton<IViteDevProcessLauncher, ViteDevProcessLauncher>();

        if (options != null) services.Configure(options);

        return services;
    }

    /// <summary>
    /// Registers Inertia and binds <see cref="InertiaOptions"/> from the <c>Inertia</c>
    /// configuration section before applying <paramref name="options"/>. This lets an
    /// orchestrator (for example .NET Aspire) supply values such as <c>SsrUrl</c> through
    /// environment variables named <c>Inertia__SsrUrl</c>.
    /// </summary>
    public static IServiceCollection AddInertia(this IServiceCollection services,
        IConfiguration configuration, Action<InertiaOptions>? options = null,
        string sectionName = "Inertia")
    {
        services.Configure<InertiaOptions>(configuration.GetSection(sectionName));
        return services.AddInertia(options);
    }

    /// <summary>
    /// Registers the Vite helper and binds <see cref="ViteOptions"/> from the <c>Vite</c>
    /// configuration section before applying <paramref name="options"/>. This lets an
    /// orchestrator (for example .NET Aspire) supply the dev server URL through an
    /// environment variable named <c>Vite__HotUrl</c>.
    /// </summary>
    public static IServiceCollection AddViteHelper(this IServiceCollection services,
        IConfiguration configuration, Action<ViteOptions>? options = null,
        string sectionName = "Vite")
    {
        services.Configure<ViteOptions>(configuration.GetSection(sectionName));
        return services.AddViteHelper(options);
    }

    private static async Task OnVersionChange(HttpContext context, string serverVersion)
    {
        // Reflash so flash data and validation errors survive the follow-up reload.
        context.RequestServices.GetService<IInertiaFlashProvider>()?.Reflash();

        // TempData is MVC-only; Minimal API hosts do not register it.
        var tempDataFactory = context.RequestServices.GetService<ITempDataDictionaryFactory>();
        if (tempDataFactory != null)
        {
            var tempData = tempDataFactory.GetTempData(context);
            if (tempData.Any()) tempData.Keep();
        }

        context.Response.Headers.Override(InertiaHeader.Location, context.RequestedLocation());
        context.Response.Headers.Override(InertiaHeader.Version, serverVersion);
        context.Response.StatusCode = (int)HttpStatusCode.Conflict;

        await context.Response.CompleteAsync();
    }

    private sealed class SsrHealthCheckMarker
    {
    }

    private sealed class SsrHttpClientMarker
    {
    }

    public static IServiceCollection AddInertiaPagination(this IServiceCollection services,
        Action<InertiaPaginationOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register IHttpContextAccessor for ambient request access
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Configure pagination options
        services.AddOptions<InertiaPaginationOptions>().Configure(options);

        return services;
    }

    public static IApplicationBuilder UseInertiaPagination(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var accessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
        var paginationOptions = app.ApplicationServices.GetRequiredService<IOptions<InertiaPaginationOptions>>().Value;

        InertiaPaginationAmbient.Configure(accessor, paginationOptions);

        return app;
    }
}

