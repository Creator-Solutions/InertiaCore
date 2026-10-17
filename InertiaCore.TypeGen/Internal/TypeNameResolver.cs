using System.Text;

namespace InertiaCore.TypeGen.Internal;

/// <summary>
/// Assigns stable, deterministic TypeScript interface names to the .NET types that are
/// discovered while walking the page graph. Complex types are queued for emission the
/// first time they are referenced; cycles are therefore handled naturally.
/// </summary>
internal sealed class TypeNameResolver
{
    private readonly Dictionary<string, string> _namesByFullName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _fullNameByTsName = new(StringComparer.Ordinal);
    private readonly List<Type> _queue = new();

    /// <summary>Gets the types queued for emission, in discovery order.</summary>
    internal IReadOnlyList<Type> Queue => _queue;

    /// <summary>Determines whether a type has already been assigned a name.</summary>
    internal bool IsReserved(Type type) => _namesByFullName.ContainsKey(FullNameOf(type));

    /// <summary>
    /// Reserves a TypeScript name for the type if necessary and returns it. The type is
    /// enqueued for emission when it is first referenced.
    /// </summary>
    internal string Reserve(Type type)
    {
        string fullName = FullNameOf(type);
        if (_namesByFullName.TryGetValue(fullName, out string? existing))
        {
            return existing;
        }

        string candidate = Mangle(type);
        if (_fullNameByTsName.TryGetValue(candidate, out string? owner) && owner != fullName)
        {
            candidate = Qualify(type);
            int suffix = 2;
            while (_fullNameByTsName.TryGetValue(candidate, out owner) && owner != fullName)
            {
                candidate = Qualify(type) + suffix++;
            }
        }

        _namesByFullName[fullName] = candidate;
        _fullNameByTsName[candidate] = fullName;
        _queue.Add(type);
        return candidate;
    }

    /// <summary>
    /// Reserves a specific TypeScript name for a type and enqueues it for emission. Used
    /// for well-known declarations such as the shared props interface whose name is fixed
    /// regardless of the underlying CLR type name.
    /// </summary>
    internal void Pin(Type type, string name)
    {
        string fullName = FullNameOf(type);
        _namesByFullName[fullName] = name;
        _fullNameByTsName[name] = fullName;
        _queue.Add(type);
    }

    private static string FullNameOf(Type type) =>
        type.FullName ?? type.AssemblyQualifiedName ?? type.Name;

    private static string Qualify(Type type)
    {
        string ns = type.Namespace ?? string.Empty;
        string prefix = ns.Replace('.', '_');
        return prefix.Length == 0 ? Mangle(type) : prefix + "_" + Mangle(type);
    }

    /// <summary>Builds a PascalCase TypeScript identifier for a type.</summary>
    internal static string Mangle(Type type)
    {
        if (type.IsArray)
        {
            return Mangle(type.GetElementType()!) + "Array";
        }

        if (type.IsGenericParameter)
        {
            return Sanitize(type.Name);
        }

        var builder = new StringBuilder();

        if (type.IsNested && type.DeclaringType is not null)
        {
            builder.Append(Mangle(type.DeclaringType));
            builder.Append('_');
        }

        string name = type.Name;
        int backtick = name.IndexOf('`');
        if (backtick >= 0)
        {
            name = name[..backtick];
        }

        builder.Append(Sanitize(name));

        if (type.IsGenericType)
        {
            Type[] arguments = type.GetGenericArguments();
            builder.Append("_Of");
            foreach (Type argument in arguments)
            {
                builder.Append('_');
                builder.Append(Mangle(argument));
            }
        }

        return builder.ToString();
    }

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        return builder.Length == 0 ? "Type" : builder.ToString();
    }
}
