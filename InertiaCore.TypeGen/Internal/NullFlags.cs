namespace InertiaCore.TypeGen.Internal;

/// <summary>
/// A cursor over the nullable flags that the C# compiler emits for a type reference,
/// together with the fallback flag taken from the nearest nullable context.
/// </summary>
internal sealed class NullFlags
{
    private readonly byte[] _values;
    private readonly byte _fallback;
    private readonly bool _single;
    private int _index;

    internal NullFlags(byte[]? values, byte fallback)
    {
        _values = values ?? Array.Empty<byte>();
        _fallback = fallback;
        // A single flag (the compiler's scalar NullableAttribute form) applies to the
        // type and every nested position, so it is never consumed.
        _single = _values.Length == 1;
    }

    /// <summary>A non-nullable flag sequence with no metadata information.</summary>
    internal static NullFlags Unknown { get; } = new(null, NullabilityReader.NotNull);

    /// <summary>Consumes and returns the next flag in pre-order traversal.</summary>
    internal byte Next()
    {
        if (_single)
        {
            return _values[0];
        }

        return _index < _values.Length ? _values[_index++] : _fallback;
    }
}
