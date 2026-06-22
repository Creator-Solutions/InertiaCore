using InertiaCore.Contracts;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Middleware;

public class InertiaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IInertiaVersionResolver _versionResolver;

    public InertiaMiddleware(RequestDelegate next, IInertiaVersionResolver versionResolver)
    {
        _next = next;
        _versionResolver = versionResolver;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string currentVersion = _versionResolver.GetVersion();
        
        context.Items["InertiaVersion"] = currentVersion;
        
        context.Response.OnStarting(() =>
        {
            if (context.Items.ContainsKey("InertiaResponse"))
            {
                context.Response.Headers["X-Inertia-Version"] = currentVersion;
            }
            return Task.CompletedTask;
        });


        if (context.Items.TryGetValue("InertiaPageData", out var pageDataObj) && 
            pageDataObj is IDictionary<string, object> pageData)
        {
            pageData["version"] = currentVersion;
        }

        await _next(context);
    }
}