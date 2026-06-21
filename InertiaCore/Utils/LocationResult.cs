using System.Net;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCore.Utils;

public class LocationResult : IActionResult, IResult
{
    private readonly string _url;
    public LocationResult(string url) => _url = url;

    public Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        SetHeaders(response);
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        SetHeaders(httpContext.Response);
        return Task.CompletedTask;
    }

    private void SetHeaders(HttpResponse response)
    {
        response.Headers["X-Inertia-Location"] = _url;
        response.StatusCode = 409;
    }
}
