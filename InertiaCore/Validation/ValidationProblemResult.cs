using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Validation;

/// <summary>
/// The <c>422 Unprocessable Entity</c> validation problem returned to non-Inertia clients.
/// </summary>
/// <remarks>
/// When <c>viaProblemDetailsService</c> is true and <c>IProblemDetailsService</c>
/// is registered, the problem is written through it so registered writers and customizations
/// apply. Otherwise the body is written directly as a string. Direct writing avoids the
/// streaming serializer, which the test host's response pipe does not support, and it is the
/// same approach <c>PrecognitionJsonResult</c> takes.
/// </remarks>
internal sealed class ValidationProblemResult : IResult
{
    private const string Title = "One or more validation errors occurred.";
    private const string ProblemType = "https://tools.ietf.org/html/rfc9110#section-15.5.21";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyDictionary<string, string[]> _errors;
    private readonly bool _viaProblemDetailsService;

    public ValidationProblemResult(IReadOnlyDictionary<string, string[]> errors, bool viaProblemDetailsService)
    {
        _errors = errors;
        _viaProblemDetailsService = viaProblemDetailsService;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        if (_viaProblemDetailsService)
        {
            var service = httpContext.RequestServices.GetService<IProblemDetailsService>();
            if (service is not null)
            {
                // Set the status before writing: on .NET 10 the writer that handles
                // HttpValidationProblemDetails keeps the response's current status code, so an
                // unset 200 would leave a validation failure reported as success.
                httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

                if (await service.TryWriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = httpContext,
                        ProblemDetails = CreateProblem(_errors)
                    }))
                {
                    return;
                }
            }
        }

        await WriteDirectAsync(httpContext, _errors);
    }

    /// <summary>Builds the 422 problem details for the given errors.</summary>
    internal static HttpValidationProblemDetails CreateProblem(IReadOnlyDictionary<string, string[]> errors) =>
        new(errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal))
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = Title,
            Type = ProblemType
        };

    /// <summary>Writes the 422 body without involving <c>IProblemDetailsService</c>.</summary>
    internal static Task WriteDirectAsync(HttpContext httpContext, IReadOnlyDictionary<string, string[]> errors)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = ProblemType,
            ["title"] = Title,
            ["status"] = StatusCodes.Status422UnprocessableEntity,
            ["errors"] = errors
        };

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        httpContext.Response.ContentType = "application/problem+json; charset=utf-8";

        return httpContext.Response.WriteAsync(JsonSerializer.Serialize(body, SerializerOptions));
    }
}
