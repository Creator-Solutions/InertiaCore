using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Services;
using InertiaCore.Services.Version;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InertiaCore.Middleware;

public class InertiaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IInertiaVersionProvider _versionResolver;

    public InertiaMiddleware(RequestDelegate next, IInertiaVersionProvider versionResolver)
    {
        _next = next;
        _versionResolver = versionResolver;
    }

    public async Task InvokeAsync(HttpContext context, IErrorBagService errorBagService)
    {
        string currentVersion = _versionResolver.GetVersion();

        var headerValue = context.Request.Headers["X-Inertia-Error-Bag"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerValue))
        {
            errorBagService.CurrentBagName = headerValue;
        }

        context.Items["InertiaVersion"] = currentVersion;
        context.Items["InertiaErrorBag"] = errorBagService.CurrentBagName;

        if (context.Items.TryGetValue("InertiaPageData", out var pageDataObj) && 
            pageDataObj is IDictionary<string, object> pageData)
        {
            pageData.TryAdd("version", currentVersion);
        }

        context.Response.OnStarting(() =>
        {
            if (context.IsInertiaRequest() || context.Items.ContainsKey("InertiaResponse"))
            {
                context.Response.Headers["X-Inertia"] = "true";
                context.Response.Headers["X-Inertia-Version"] = currentVersion;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}