using InertiaCore.Extensions;
using InertiaCore.Precognition;
using InertiaCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Validation;

/// <summary>
/// Answers framework validation failures on endpoints that opted in with
/// <c>WithInertiaValidation</c> or <c>WithInertiaPrecognition</c>. The framework writes these
/// failures through <see cref="IProblemDetailsService"/> (for example when <c>AddValidation()</c>
/// is active on .NET 10 and later), so this writer is the point where they can become an Inertia
/// response.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Precognition requests get the Precognition response (422 with the errors).</item>
/// <item>Inertia requests on <c>WithInertiaValidation</c> endpoints get the redirect or 422.</item>
/// <item>Other requests on <c>WithInertiaValidation</c> endpoints get a 422 written by the other
/// registered writers, so their customizations still apply.</item>
/// </list>
/// Problems on any other endpoint are left to the default writers.
/// </remarks>
internal sealed class InertiaValidationProblemDetailsWriter : IProblemDetailsWriter
{
    /// <inheritdoc />
    public bool CanWrite(ProblemDetailsContext context)
    {
        if (context.ProblemDetails is not HttpValidationProblemDetails)
            return false;

        var httpContext = context.HttpContext;
        var metadata = httpContext.GetEndpoint()?.Metadata;
        var precognitionEndpoint = metadata?.GetMetadata<InertiaPrecognitionMetadata>() is not null;
        var validationEndpoint = metadata?.GetMetadata<InertiaValidationMetadata>() is not null;

        return validationEndpoint || (precognitionEndpoint && httpContext.IsPrecognitionRequest());
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var httpContext = context.HttpContext;
        var problem = (HttpValidationProblemDetails)context.ProblemDetails;

        var errors = problem.Errors.ToDictionary(
            pair => InertiaValidationResponse.NormalizeKey(pair.Key),
            pair => pair.Value,
            StringComparer.Ordinal);

        // The framework has already set 400 and problem+json on the response. Reset both so the
        // Inertia response replaces them cleanly.
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = null;

        var errorBagService = httpContext.RequestServices.GetRequiredService<IErrorBagService>();

        var metadata = httpContext.GetEndpoint()?.Metadata;
        var precognitionEndpoint = metadata?.GetMetadata<InertiaPrecognitionMetadata>() is not null;

        if (precognitionEndpoint && httpContext.IsPrecognitionRequest())
        {
            errorBagService.AddErrors(errorBagService.CurrentBagName, errors.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
            await PrecognitionResponse.BuildResult(httpContext, errorBagService).ExecuteAsync(httpContext);
            return;
        }

        if (httpContext.IsInertiaRequest())
        {
            await InertiaValidationResponse.Create(httpContext, errorBagService, errors).ExecuteAsync(httpContext);
            return;
        }

        await WriteWithOtherWritersAsync(context, errors);
    }

    /// <summary>
    /// Writes a 422 through the registered writers other than this one, so their customizations
    /// apply. Falls back to a direct write when none of them can handle the problem.
    /// </summary>
    private static async Task WriteWithOtherWritersAsync(ProblemDetailsContext context, IReadOnlyDictionary<string, string[]> errors)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        var problemContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = ValidationProblemResult.CreateProblem(errors),
            AdditionalMetadata = context.AdditionalMetadata,
            Exception = context.Exception
        };

        var others = httpContext.RequestServices.GetServices<IProblemDetailsWriter>()
            .Where(writer => writer is not InertiaValidationProblemDetailsWriter);

        foreach (var writer in others)
        {
            if (writer.CanWrite(problemContext))
            {
                await writer.WriteAsync(problemContext);
                return;
            }
        }

        await ValidationProblemResult.WriteDirectAsync(httpContext, errors);
    }
}
