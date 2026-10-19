using System;
using System.Linq;
using System.Threading.Tasks;
using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace InertiaCore.Middleware;

/// <summary>
/// Renders the configured Inertia error component for handled HTTP status codes when the
/// request is an Inertia request, and leaves every other response untouched.
/// <para>
/// A response is only rewritten when nothing has been written yet (an empty body and no
/// content type) and the response has not started. That preserves bodies produced
/// downstream — including the framework's developer exception page in Development — and
/// keeps non-Inertia clients on the host's default status handling.
/// </para>
/// </summary>
public class InertiaStatusPagesMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of <see cref="InertiaStatusPagesMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public InertiaStatusPagesMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="options">The Inertia options carrying the status-page configuration.</param>
    /// <param name="environment">The hosting environment, used to preserve the developer exception page.</param>
    public async Task InvokeAsync(
        HttpContext context,
        IOptions<InertiaOptions> options,
        IHostEnvironment environment)
    {
        await _next(context);

        var settings = options.Value.StatusPages;

        if (!ShouldRender(context, settings, environment))
            return;

        await RenderAsync(context, settings);
    }

    /// <summary>
    /// Whether the current response should be replaced with an Inertia error page.
    /// </summary>
    internal static bool ShouldRender(
        HttpContext context,
        InertiaStatusPagesOptions settings,
        IHostEnvironment environment)
    {
        if (!settings.Enabled)
            return false;

        // Only Inertia requests ever see an Inertia error page.
        if (!context.IsInertiaRequest())
            return false;

        var status = context.Response.StatusCode;

        if (!settings.StatusCodes.Contains(status))
            return false;

        // A control response (external location or fragment redirect) already instructs the
        // client to navigate and must not be turned into a page.
        if (IsControl409(context))
            return false;

        // In Development the developer exception page writes the error body; preserve it.
        if (settings.RespectDeveloperExceptionPage
            && status == StatusCodes.Status500InternalServerError
            && environment.IsDevelopment())
        {
            return false;
        }

        // The response must still be untouched, otherwise a handler already wrote a body
        // (for example the developer exception page, ProblemDetails, or a streamed file).
        if (context.Response.HasStarted)
            return false;

        if (context.Response.ContentLength is > 0)
            return false;

        return string.IsNullOrEmpty(context.Response.ContentType);
    }

    private static bool IsControl409(HttpContext context) =>
        context.Response.StatusCode == StatusCodes.Status409Conflict
        && (context.Response.Headers.ContainsKey(InertiaHeader.Location)
            || context.Response.Headers.ContainsKey(InertiaHeader.Redirect));

    private async Task RenderAsync(HttpContext context, InertiaStatusPagesOptions settings)
    {
        var status = context.Response.StatusCode;
        var factory = context.RequestServices.GetService(typeof(IResponseFactory)) as IResponseFactory;

        if (factory is null)
            return;

        var component = settings.ResolveComponent(status);
        var message = settings.ResolveMessage(context, status);

        IResult response = factory.Render(component, new { status, message });

        // GetJson() forces the status to 200 while it writes the envelope; re-assert the
        // error status once the response starts so the client sees it. OnStarting runs
        // before the first byte is flushed, so the override always wins.
        context.Response.OnStarting(() =>
        {
            context.Response.StatusCode = status;
            return Task.CompletedTask;
        });

        await response.ExecuteAsync(context);
    }
}
