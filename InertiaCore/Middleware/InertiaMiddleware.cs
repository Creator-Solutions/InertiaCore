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
            var isInertiaRequest = context.IsInertiaRequest();
            var isInertiaResponse = context.Items.ContainsKey("InertiaResponse") || context.Response.Headers.ContainsKey("X-Inertia");

            if (isInertiaRequest && !isInertiaResponse && 
                (context.Response.StatusCode == 200 || context.Response.StatusCode == 204) &&
                string.IsNullOrEmpty(context.Response.ContentType) &&
                (context.Response.ContentLength == null || context.Response.ContentLength == 0))
            {
                var referer = context.Request.Headers["Referer"].ToString();
                var backUrl = !string.IsNullOrEmpty(referer) ? referer : context.Request.Path + context.Request.QueryString;

                var isNonGet = new[] { "POST", "PUT", "PATCH", "DELETE" }.Contains(context.Request.Method);
                context.Response.StatusCode = isNonGet ? 303 : 302;
                context.Response.Headers["Location"] = backUrl;
            }
            else if (isInertiaRequest || context.Items.ContainsKey("InertiaResponse"))
            {
                context.Response.Headers["X-Inertia"] = "true";
                context.Response.Headers["X-Inertia-Version"] = currentVersion;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}