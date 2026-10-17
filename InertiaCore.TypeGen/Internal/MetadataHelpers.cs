using System.Reflection;

namespace InertiaCore.TypeGen.Internal;

/// <summary>
/// Low-level helpers for reading metadata through a <see cref="MetadataLoadContext"/>.
/// All attribute access is done via <see cref="CustomAttributeData"/> so that user code
/// is never executed.
/// </summary>
internal static class MetadataHelpers
{
    internal const string InertiaPageAttribute = "InertiaCore.InertiaPageAttribute";
    internal const string InertiaSharedPropsAttribute = "InertiaCore.InertiaSharedPropsAttribute";
    internal const string JsonPropertyNameAttribute = "System.Text.Json.Serialization.JsonPropertyNameAttribute";
    internal const string JsonIgnoreAttribute = "System.Text.Json.Serialization.JsonIgnoreAttribute";
    internal const string JsonConverterAttribute = "System.Text.Json.Serialization.JsonConverterAttribute";
    internal const string JsonExtensionDataAttribute = "System.Text.Json.Serialization.JsonExtensionDataAttribute";
    internal const string NullableAttribute = "System.Runtime.CompilerServices.NullableAttribute";
    internal const string NullableContextAttribute = "System.Runtime.CompilerServices.NullableContextAttribute";

    /// <summary>Returns the custom attributes declared on a provider, tolerant of failures.</summary>
    internal static IReadOnlyList<CustomAttributeData> GetAttributes(ICustomAttributeProvider provider) =>
        provider switch
        {
            MemberInfo member => member.GetCustomAttributesData().ToArray(),
            Assembly assembly => assembly.GetCustomAttributesData().ToArray(),
            _ => Array.Empty<CustomAttributeData>(),
        };

    /// <summary>Determines whether any attribute matches the given full name.</summary>
    internal static bool HasAttribute(IReadOnlyList<CustomAttributeData> attributes, string fullName)
    {
        foreach (CustomAttributeData attribute in attributes)
        {
            if (attribute.AttributeType.FullName == fullName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the first attribute with the given full name, or null.</summary>
    internal static CustomAttributeData? FindAttribute(IReadOnlyList<CustomAttributeData> attributes, string fullName)
    {
        foreach (CustomAttributeData attribute in attributes)
        {
            if (attribute.AttributeType.FullName == fullName)
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>Reads the first constructor argument as a string, if present.</summary>
    internal static string? GetStringArgument(CustomAttributeData? attribute)
    {
        if (attribute is null || attribute.ConstructorArguments.Count == 0)
        {
            return null;
        }

        return attribute.ConstructorArguments[0].Value as string;
    }

    /// <summary>Reads the first constructor argument as a <see cref="Type"/>, if present.</summary>
    internal static Type? GetTypeArgument(CustomAttributeData attribute)
    {
        if (attribute.ConstructorArguments.Count == 0)
        {
            return null;
        }

        return attribute.ConstructorArguments[0].Value as Type;
    }

    /// <summary>
    /// Reads a named enum argument by its underlying value and returns the matching
    /// enum member name. Returns null when the argument is absent or not an enum.
    /// </summary>
    internal static string? GetNamedEnumArgument(CustomAttributeData attribute, string name)
    {
        foreach (CustomAttributeNamedArgument argument in attribute.NamedArguments)
        {
            if (!string.Equals(argument.MemberName, name, StringComparison.Ordinal))
            {
                continue;
            }

            Type? argumentType = argument.TypedValue.ArgumentType;
            object? value = argument.TypedValue.Value;
            return ResolveEnumName(argumentType, value);
        }

        return null;
    }

    /// <summary>
    /// Resolves the enum member name for a boxed underlying value. Metadata argument
    /// values are exposed as the underlying primitive, so the enum's literal fields are
    /// inspected directly.
    /// </summary>
    internal static string? ResolveEnumName(Type? enumType, object? value)
    {
        if (enumType is null || value is null || !enumType.IsEnum)
        {
            return null;
        }

        object? normalized = NormalizeInteger(value);
        foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            object? constant = field.GetRawConstantValue();
            if (constant is not null && Equals(NormalizeInteger(constant), normalized))
            {
                return field.Name;
            }
        }

        return value.ToString();
    }

    private static object NormalizeInteger(object value)
    {
        return value switch
        {
            byte b => (long)b,
            sbyte sb => (long)sb,
            short s => (long)s,
            ushort us => (long)us,
            int i => (long)i,
            uint ui => (long)ui,
            long l => l,
            ulong ul => unchecked((long)ul),
            _ => value,
        };
    }
}
