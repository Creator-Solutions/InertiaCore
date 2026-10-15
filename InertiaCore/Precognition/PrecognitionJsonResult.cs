using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCore.Precognition;

/// <summary>
/// The <c>422</c> Precognition validation response.
/// <para>
/// Serializes and writes the body directly rather than using
/// <see cref="JsonResult"/> / <c>Results.Json</c>, matching the approach taken by
/// <see cref="InertiaCore.Response"/> so the response works identically under MVC,
/// Minimal APIs and the test host (whose response pipe writer does not support the
/// streaming <c>JsonSerializer</c> overload).
/// </para>
/// </summary>
internal sealed class PrecognitionJsonResult : IActionResult, IResult
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly object _body;

    public PrecognitionJsonResult(object body) => _body = body;

    /// <summary>The response body, exposed for tests.</summary>
    internal object Body => _body;

    Task IActionResult.ExecuteResultAsync(ActionContext context) => WriteAsync(context.HttpContext.Response);

    Task IResult.ExecuteAsync(HttpContext httpContext) => WriteAsync(httpContext.Response);

    private async Task WriteAsync(HttpResponse response)
    {
        response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        response.ContentType = "application/json; charset=utf-8";

        await response.WriteAsync(JsonSerializer.Serialize(_body, SerializerOptions));
    }
}
