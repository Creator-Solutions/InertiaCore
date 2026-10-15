using System.ComponentModel.DataAnnotations;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Validation;

/// <summary>
/// Default <see cref="IInertiaRequestValidator"/> that validates endpoint arguments
/// using <see cref="System.ComponentModel.DataAnnotations"/> (including
/// <see cref="IValidatableObject"/>).
/// </summary>
public sealed class DataAnnotationsRequestValidator : IInertiaRequestValidator
{
    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string[]>> ValidateAsync(
        HttpContext context,
        IList<object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simple/bound framework values (strings, primitives, HttpContext, tokens)
            // carry no DataAnnotations metadata; skip them rather than treat as models.
            if (argument is null || IsSimple(argument.GetType()))
                continue;

            var results = new List<ValidationResult>();

            if (Validator.TryValidateObject(argument, new ValidationContext(argument), results, validateAllProperties: true))
                continue;

            foreach (var result in results)
            {
                var members = result.MemberNames.Any()
                    ? result.MemberNames
                    : new[] { string.Empty };

                foreach (var member in members)
                {
                    var key = member.ToCamelCase();
                    if (!errors.TryGetValue(key, out var messages))
                        errors[key] = messages = new List<string>();

                    messages.Add(result.ErrorMessage ?? "The value is invalid.");
                }
            }
        }

        IReadOnlyDictionary<string, string[]> mapped = errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);

        return Task.FromResult(mapped);
    }

    private static bool IsSimple(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(TimeSpan)
        || type == typeof(Guid);
}
