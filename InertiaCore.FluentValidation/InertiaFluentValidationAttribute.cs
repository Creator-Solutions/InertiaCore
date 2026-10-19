using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InertiaCore.FluentValidation;

/// <summary>
/// Validates the action argument of type <typeparamref name="TRequest"/> with its registered
/// FluentValidation <c>IValidator&lt;TRequest&gt;</c> before the action runs. Failures follow the
/// same rules as the Minimal API <c>WithInertiaFluentValidation</c>.
/// </summary>
/// <typeparam name="TRequest">The argument type to validate.</typeparam>
/// <example>
/// <code>
/// [InertiaFluentValidation&lt;OrderInput&gt;]
/// public IActionResult Store([FromBody] OrderInput input) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class InertiaFluentValidationAttribute<TRequest> : Attribute, IFilterFactory
    where TRequest : class
{
    /// <summary>
    /// Optional FluentValidation rule sets to run. When null or empty, the default rules run.
    /// </summary>
    public string[]? RuleSets { get; set; }

    /// <inheritdoc />
    public bool IsReusable => false;

    /// <inheritdoc />
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new InertiaFluentValidationActionFilter<TRequest>(RuleSets);
}
