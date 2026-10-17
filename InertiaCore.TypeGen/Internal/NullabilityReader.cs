using System.Reflection;

namespace InertiaCore.TypeGen.Internal;

/// <summary>
/// Decodes the C# nullable-reference metadata (<c>NullableAttribute</c> /
/// <c>NullableContextAttribute</c>) that the compiler embeds in the assembly.
/// </summary>
internal static class NullabilityReader
{
    internal const byte NotNull = 1;
    internal const byte Nullable = 2;

    /// <summary>
    /// Builds a <see cref="NullFlags"/> cursor for the given member. The member-level
    /// <c>NullableAttribute</c> (a single byte or a byte array emitted in pre-order) is
    /// used when present, otherwise the nearest nullable context applies.
    /// </summary>
    internal static NullFlags ForMember(ICustomAttributeProvider provider, Type? declaringType, Assembly assembly)
    {
        byte[]? values = GetNullableFlags(provider);
        byte fallback = GetContext(declaringType, assembly);
        return new NullFlags(values, fallback);
    }

    /// <summary>Reads the raw <c>NullableAttribute</c> payload, or null when absent.</summary>
    internal static byte[]? GetNullableFlags(ICustomAttributeProvider provider)
    {
        CustomAttributeData? attribute = MetadataHelpers.FindAttribute(
            MetadataHelpers.GetAttributes(provider), MetadataHelpers.NullableAttribute);

        if (attribute is null || attribute.ConstructorArguments.Count == 0)
        {
            return null;
        }

        CustomAttributeTypedArgument argument = attribute.ConstructorArguments[0];

        if (argument.Value is byte single)
        {
            return new[] { single };
        }

        if (argument.Value is IReadOnlyList<CustomAttributeTypedArgument> elements)
        {
            var values = new byte[elements.Count];
            for (int i = 0; i < elements.Count; i++)
            {
                values[i] = elements[i].Value is byte value ? value : NotNull;
            }

            return values;
        }

        return null;
    }

    /// <summary>
    /// Resolves the nullable context flag from the declaring type, falling back to the
    /// assembly. Returns <see cref="NotNull"/> when no context is present.
    /// </summary>
    internal static byte GetContext(Type? declaringType, Assembly assembly)
    {
        if (declaringType is not null)
        {
            byte? fromType = ReadContext(MetadataHelpers.GetAttributes(declaringType));
            if (fromType.HasValue)
            {
                return fromType.Value;
            }
        }

        byte? fromAssembly = ReadContext(MetadataHelpers.GetAttributes(assembly));
        return fromAssembly ?? NotNull;
    }

    private static byte? ReadContext(IReadOnlyList<CustomAttributeData> attributes)
    {
        CustomAttributeData? attribute = MetadataHelpers.FindAttribute(attributes, MetadataHelpers.NullableContextAttribute);
        if (attribute is null || attribute.ConstructorArguments.Count == 0)
        {
            return null;
        }

        return attribute.ConstructorArguments[0].Value is byte value ? value : null;
    }
}
