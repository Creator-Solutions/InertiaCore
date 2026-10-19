using FluentValidation;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.FluentValidation;

/// <summary>
/// Action filter behind <see cref="InertiaFluentValidationAttribute{TRequest}"/>. Runs before
/// MVC's automatic <c>400</c> filter so failures are answered by the Inertia response.
/// </summary>
internal sealed class InertiaFluentValidationActionFilter<TRequest> : IAsyncActionFilter, IOrderedFilter
    where TRequest : class
{
    private readonly string[]? _ruleSets;

    public InertiaFluentValidationActionFilter(string[]? ruleSets) => _ruleSets = ruleSets;

    /// <summary>Runs before <c>ModelStateInvalidFilter</c> (order <c>-2000</c>).</summary>
    public int Order => -2500;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.ActionArguments.Values.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            await next();
            return;
        }

        var httpContext = context.HttpContext;
        var validator = httpContext.RequestServices.GetService<IValidator<TRequest>>()
                        ?? throw FluentValidationSupport.MissingValidator(typeof(TRequest));

        var errors = await FluentValidationSupport.ValidateAsync(validator, request, _ruleSets, httpContext.RequestAborted);

        if (errors.Count == 0 && !httpContext.IsPrecognitionRequest())
        {
            await next();
            return;
        }

        context.Result = new ResultActionResult(FluentValidationSupport.Respond(httpContext, errors));
    }

    /// <summary>Adapts a minimal <see cref="IResult"/> to an <see cref="IActionResult"/>.</summary>
    private sealed class ResultActionResult : IActionResult
    {
        private readonly IResult _result;

        public ResultActionResult(IResult result) => _result = result;

        public Task ExecuteResultAsync(ActionContext context) => _result.ExecuteAsync(context.HttpContext);
    }
}
