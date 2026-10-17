using System.Globalization;
using System.Reflection;
using System.Text;

namespace InertiaCore.TypeGen.Internal;

/// <summary>
/// Maps CLR types to TypeScript type expressions and renders the interfaces for complex
/// record/class types.
/// </summary>
internal sealed class TypeMapper
{
    private const string EnumerableInterface = "System.Collections.Generic.IEnumerable`1";
    private const string DictionaryInterface = "System.Collections.Generic.IDictionary`2";
    private const string ReadOnlyDictionaryInterface = "System.Collections.Generic.IReadOnlyDictionary`2";

    private static readonly string DefaultIgnoreCondition =
        new System.Text.Json.Serialization.JsonIgnoreAttribute().Condition.ToString();

    private readonly TypeNameResolver _resolver;
    private readonly TypeScriptGeneratorOptions _options;
    private readonly Assembly _assembly;
    private readonly List<GenerationDiagnostic> _diagnostics;

    internal TypeMapper(
        TypeNameResolver resolver,
        TypeScriptGeneratorOptions options,
        Assembly assembly,
        List<GenerationDiagnostic> diagnostics)
    {
        _resolver = resolver;
        _options = options;
        _assembly = assembly;
        _diagnostics = diagnostics;
    }

    /// <summary>Maps a type reference to a TypeScript type expression.</summary>
    internal string Map(Type type, NullFlags flags, CustomAttributeData? enumConverter = null)
    {
        byte flag = flags.Next();

        if (IsNullableValueType(type, out Type? underlying))
        {
            string inner = Map(underlying!, flags);
            return inner + " | null";
        }

        if (type.IsGenericParameter || type.IsPointer || type.IsByRef)
        {
            return "unknown";
        }

        if (type.FullName == "System.Byte[]")
        {
            return ApplyNullability("string", type, flag);
        }

        if (type.IsArray)
        {
            string element = Map(type.GetElementType()!, flags);
            return ApplyNullability(AsArrayElement(element) + "[]", type, flag);
        }

        if (TryGetDictionaryValue(type, out Type? dictionaryValue))
        {
            // Consume the key's nullability flag even though keys are always strings.
            _ = flags.Next();
            string value = Map(dictionaryValue!, flags);
            return ApplyNullability($"Record<string, {value}>", type, flag);
        }

        if (TryGetEnumerableElement(type, out Type? elementType))
        {
            string element = Map(elementType!, flags);
            return ApplyNullability(AsArrayElement(element) + "[]", type, flag);
        }

        if (IsNonGenericEnumerable(type))
        {
            return ApplyNullability("unknown[]", type, flag);
        }

        if (type.IsEnum)
        {
            return RenderEnum(type, enumConverter);
        }

        string? known = MapKnownPrimitive(type);
        if (known is not null)
        {
            return ApplyNullability(known, type, flag);
        }

        if (type.IsValueType)
        {
            _diagnostics.Add(new GenerationDiagnostic(
                GenerationSeverity.Warning,
                $"Value type '{type.FullName}' is not mapped; emitted as 'unknown'."));
            return "unknown";
        }

        return ApplyNullability(_resolver.Reserve(type), type, flag);
    }

    /// <summary>Renders the TypeScript declaration for a reserved complex type.</summary>
    internal string RenderInterface(Type type, string name)
    {
        var builder = new StringBuilder();
        builder.Append("export interface ").Append(name).Append(" {\n");

        bool any = false;
        foreach (PropertyInfo property in GetSerializableProperties(type))
        {
            string? line;
            try
            {
                line = RenderProperty(property);
            }
            catch (Exception ex)
            {
                _diagnostics.Add(new GenerationDiagnostic(
                    GenerationSeverity.Warning,
                    $"Could not map '{property.Name}': {ex.Message}"));
                continue;
            }

            if (line is null)
            {
                continue;
            }

            builder.Append("  ").Append(line).Append('\n');
            any = true;
        }

        if (!any)
        {
            builder.Append("  [key: string]: unknown;\n");
        }

        builder.Append('}');
        return builder.ToString();
    }

    private string? RenderProperty(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length > 0)
        {
            return null;
        }

        MethodInfo? getter = property.GetGetMethod();
        if (getter is null || getter.IsStatic)
        {
            return null;
        }

        IReadOnlyList<CustomAttributeData> attributes;
        try
        {
            attributes = property.GetCustomAttributesData().ToArray();
        }
        catch (Exception ex)
        {
            _diagnostics.Add(new GenerationDiagnostic(
                GenerationSeverity.Warning,
                $"Could not read attributes for '{property.DeclaringType?.FullName}.{property.Name}': {ex.Message}"));
            return null;
        }

        if (MetadataHelpers.HasAttribute(attributes, MetadataHelpers.JsonExtensionDataAttribute))
        {
            return "[key: string]: unknown;";
        }

        bool optional = false;
        CustomAttributeData? ignore = MetadataHelpers.FindAttribute(attributes, MetadataHelpers.JsonIgnoreAttribute);
        if (ignore is not null)
        {
            string condition = MetadataHelpers.GetNamedEnumArgument(ignore, "Condition") ?? DefaultIgnoreCondition;
            if (string.Equals(condition, "Always", StringComparison.Ordinal))
            {
                return null;
            }

            optional = string.Equals(condition, "WhenWritingNull", StringComparison.Ordinal) ||
                       string.Equals(condition, "WhenWritingDefault", StringComparison.Ordinal);
        }

        string name = MetadataHelpers.GetStringArgument(
            MetadataHelpers.FindAttribute(attributes, MetadataHelpers.JsonPropertyNameAttribute))
            ?? ApplyNamingPolicy(property.Name);

        Type propertyType = property.PropertyType;
        NullFlags flags = NullabilityReader.ForMember(property, property.DeclaringType, _assembly);
        CustomAttributeData? converter = MetadataHelpers.FindAttribute(attributes, MetadataHelpers.JsonConverterAttribute);

        if (TryGetWrapper(propertyType, out Type? wrappedType, out bool wrapperOptional))
        {
            optional = optional || wrapperOptional;
            propertyType = wrappedType ?? typeof(object);
            flags = NullFlags.Unknown;
            converter = null;
        }

        string typeScriptType = Map(propertyType, flags, converter);
        return name + (optional ? "?" : string.Empty) + ": " + typeScriptType + ";";
    }

    private string ApplyNamingPolicy(string name) =>
        _options.UseCamelCase ? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name) : name;

    private string RenderEnum(Type enumType, CustomAttributeData? converter)
    {
        bool asString = _options.EnumMode switch
        {
            EnumSerializationMode.AsString => true,
            EnumSerializationMode.AsNumber => false,
            _ => IsStringEnumConverter(converter) || IsStringEnumConverter(enumType),
        };

        var members = new List<(string Name, object? Value)>();
        foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            members.Add((field.Name, field.GetRawConstantValue()));
        }

        if (members.Count == 0)
        {
            return asString ? "string" : "number";
        }

        if (asString)
        {
            members.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
            return string.Join(" | ", members.Select(m => "'" + m.Name.Replace("'", "\\'") + "'"));
        }

        members.Sort(static (a, b) => CompareValues(a.Value, b.Value));
        return string.Join(" | ", members.Select(m => Convert.ToString(m.Value, CultureInfo.InvariantCulture) ?? "0"));
    }

    private static int CompareValues(object? a, object? b)
    {
        try
        {
            return Comparer<object>.Default.Compare(
                Convert.ToDecimal(a, CultureInfo.InvariantCulture),
                Convert.ToDecimal(b, CultureInfo.InvariantCulture));
        }
        catch (Exception)
        {
            return string.CompareOrdinal(a?.ToString(), b?.ToString());
        }
    }

    private static bool IsStringEnumConverter(Type enumType)
    {
        CustomAttributeData? attribute = MetadataHelpers.FindAttribute(
            MetadataHelpers.GetAttributes(enumType), MetadataHelpers.JsonConverterAttribute);
        return IsStringEnumConverter(attribute);
    }

    private static bool IsStringEnumConverter(CustomAttributeData? attribute)
    {
        if (attribute is null)
        {
            return false;
        }

        Type? converterType = MetadataHelpers.GetTypeArgument(attribute);
        string converterName = converterType?.FullName ?? converterType?.Name ?? string.Empty;
        return converterName.Contains("StringEnumConverter", StringComparison.Ordinal);
    }

    private static bool TryGetWrapper(Type type, out Type? wrapped, out bool optional)
    {
        wrapped = null;
        optional = false;

        if (type.IsGenericType)
        {
            string? definition = type.GetGenericTypeDefinition().FullName;
            if (definition is "InertiaCore.Props.Lazy`1" or "InertiaCore.Props.Defer`1" or "InertiaCore.Props.Optional`1")
            {
                wrapped = type.GetGenericArguments()[0];
                optional = true;
                return true;
            }
        }

        for (Type? candidate = type; candidate is not null; candidate = candidate.BaseType)
        {
            string? fullName = candidate.FullName;
            if (fullName is "InertiaCore.Props.LazyProp" or "InertiaCore.Props.DeferredProp")
            {
                optional = true;
                return true;
            }

            if (fullName == "InertiaCore.Props.InvokableProp")
            {
                return true;
            }
        }

        return false;
    }

    private static string ApplyNullability(string typeScript, Type type, byte flag) =>
        flag == NullabilityReader.Nullable && !type.IsValueType
            ? typeScript + " | null"
            : typeScript;

    /// <summary>Parenthesizes an element type whose union would otherwise bind too loosely.</summary>
    private static string AsArrayElement(string typeScript) =>
        typeScript.Contains(" | ", StringComparison.Ordinal) ? "(" + typeScript + ")" : typeScript;

    private static bool IsNullableValueType(Type type, out Type? underlying)
    {
        underlying = null;
        if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "System.Nullable`1")
        {
            underlying = type.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    private static string? MapKnownPrimitive(Type type) => type.FullName switch
    {
        "System.String" => "string",
        "System.Char" => "string",
        "System.Guid" => "string",
        "System.DateTime" => "string",
        "System.DateTimeOffset" => "string",
        "System.TimeSpan" => "string",
        "System.DateOnly" => "string",
        "System.TimeOnly" => "string",
        "System.Uri" => "string",
        "System.Boolean" => "boolean",
        "System.Byte" => "number",
        "System.SByte" => "number",
        "System.Int16" => "number",
        "System.UInt16" => "number",
        "System.Int32" => "number",
        "System.UInt32" => "number",
        "System.Int64" => "number",
        "System.UInt64" => "number",
        "System.Single" => "number",
        "System.Double" => "number",
        "System.Decimal" => "number",
        "System.Object" => "unknown",
        _ => null,
    };

    private static bool TryGetDictionaryValue(Type type, out Type? value)
    {
        value = null;
        foreach (Type candidate in EnumerateSelfAndInterfaces(type))
        {
            if (!candidate.IsGenericType)
            {
                continue;
            }

            string? definition = candidate.GetGenericTypeDefinition().FullName;
            if (definition is DictionaryInterface or ReadOnlyDictionaryInterface)
            {
                value = candidate.GetGenericArguments()[1];
                return true;
            }
        }

        return false;
    }

    private static bool TryGetEnumerableElement(Type type, out Type? element)
    {
        element = null;
        if (type.FullName == "System.String")
        {
            return false;
        }

        foreach (Type candidate in EnumerateSelfAndInterfaces(type))
        {
            if (candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition().FullName == EnumerableInterface)
            {
                element = candidate.GetGenericArguments()[0];
                return true;
            }
        }

        return false;
    }

    private static bool IsNonGenericEnumerable(Type type) =>
        type.FullName == "System.Collections.IEnumerable";

    private static IEnumerable<Type> EnumerateSelfAndInterfaces(Type type)
    {
        yield return type;

        Type[] interfaces;
        try
        {
            interfaces = type.GetInterfaces();
        }
        catch (Exception)
        {
            interfaces = Array.Empty<Type>();
        }

        foreach (Type candidate in interfaces)
        {
            yield return candidate;
        }
    }

    private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
    {
        PropertyInfo[] properties;
        try
        {
            properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (PropertyInfo property in properties)
        {
            yield return property;
        }
    }
}
