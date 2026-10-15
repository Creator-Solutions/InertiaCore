using Microsoft.AspNetCore.Http;

namespace InertiaCore.Validation;

/// <summary>
/// Validates the bound arguments of an endpoint for a Precognition request.
/// <para>
/// The default implementation (<see cref="DataAnnotationsRequestValidator"/>) uses
/// <see cref="System.ComponentModel.DataAnnotations"/>. Register a custom
/// implementation (for example one backed by FluentValidation) to integrate a
/// different validation engine; the Precognition endpoint filter resolves this
/// service from the request scope.
/// </para>
/// </summary>
public interface IInertiaRequestValidator
{
    /// <summary>
    /// Validates the given bound arguments and returns the validation errors keyed by
    /// field name. Returns an empty dictionary when every argument is valid.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="arguments">The bound endpoint arguments (see <c>EndpointFilterInvocationContext.Arguments</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The validation errors keyed by field name.</returns>
    Task<IReadOnlyDictionary<string, string[]>> ValidateAsync(
        HttpContext context,
        IList<object?> arguments,
        CancellationToken cancellationToken = default);
}
