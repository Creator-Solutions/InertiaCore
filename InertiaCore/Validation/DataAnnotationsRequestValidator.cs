using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Validation;

/// <summary>
/// Default <see cref="IInertiaRequestValidator"/> that validates endpoint arguments
/// using <see cref="System.ComponentModel.DataAnnotations"/> (including
/// <see cref="IValidatableObject"/>).
/// </summary>
/// <remarks>
/// <para>
/// Nested objects and collection elements are validated recursively. Error keys use
/// camelCase property names joined by dots, with numeric indexes for collection
/// elements, for example <c>address.street</c> or <c>items.0.name</c>.
/// </para>
/// <para>
/// A property whose own attributes fail is not descended into, so its children are
/// not reported in addition to the property error. Recursion stops at a depth of 32 and
/// on object cycles.
/// </para>
/// </remarks>
public sealed class DataAnnotationsRequestValidator : IInertiaRequestValidator
{
    private const int MaxDepth = 32;

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string[]>> ValidateAsync(
        HttpContext context,
        IList<object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var visiting = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simple/bound framework values (strings, primitives, HttpContext, tokens)
            // carry no DataAnnotations metadata; skip them rather than treat as models.
            if (argument is null || IsSimple(argument.GetType()))
                continue;

            ValidateGraph(argument, prefix: null, depth: 0, errors, visiting, cancellationToken);
        }

        IReadOnlyDictionary<string, string[]> mapped = errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);

        return Task.FromResult(mapped);
    }

    private static void ValidateGraph(
        object instance,
        string? prefix,
        int depth,
        Dictionary<string, List<string>> errors,
        HashSet<object> visiting,
        CancellationToken cancellationToken)
    {
        if (depth > MaxDepth || !visiting.Add(instance))
            return;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var results = new List<ValidationResult>();
            Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

            var failedMembers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in results)
            {
                var members = result.MemberNames.Any()
                    ? result.MemberNames
                    : new[] { string.Empty };

                foreach (var member in members)
                {
                    if (member.Length > 0)
                        failedMembers.Add(member);

                    var key = Join(prefix, member.Length == 0 ? string.Empty : member.ToCamelCase());
                    if (!errors.TryGetValue(key, out var messages))
                        errors[key] = messages = new List<string>();

                    messages.Add(result.ErrorMessage ?? "The value is invalid.");
                }
            }

            foreach (var property in GetProperties(instance.GetType()))
            {
                if (failedMembers.Contains(property.Name))
                    continue;

                var value = property.GetValue(instance);
                if (value is null)
                    continue;

                var propertyKey = Join(prefix, property.Name.ToCamelCase());

                if (value is string)
                    continue;

                if (value is IEnumerable enumerable && value is not IDictionary)
                {
                    var index = 0;
                    foreach (var element in enumerable)
                    {
                        if (element is not null && IsValidatableComplex(element.GetType()))
                        {
                            ValidateGraph(element, $"{propertyKey}.{index}", depth + 1, errors, visiting, cancellationToken);
                        }

                        index++;
                    }

                    continue;
                }

                if (IsValidatableComplex(value.GetType()))
                    ValidateGraph(value, propertyKey, depth + 1, errors, visiting, cancellationToken);
            }
        }
        finally
        {
            visiting.Remove(instance);
        }
    }

    private static string Join(string? prefix, string segment) =>
        string.IsNullOrEmpty(prefix) ? segment : $"{prefix}.{segment}";

    private static PropertyInfo[] GetProperties(Type type) =>
        PropertyCache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray());

    private static bool IsValidatableComplex(Type type) =>
        !IsSimple(type) && type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true;

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
