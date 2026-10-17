namespace InertiaCore.TypeGen;

/// <summary>
/// Controls how enums are projected into TypeScript.
/// </summary>
public enum EnumSerializationMode
{
    /// <summary>
    /// Use a string-literal union when the enum (or the member) is decorated with a
    /// <c>JsonStringEnumConverter</c>; otherwise emit a numeric union. This mirrors the
    /// runtime serializer behaviour.
    /// </summary>
    Auto = 0,

    /// <summary>Always emit a string-literal union.</summary>
    AsString = 1,

    /// <summary>Always emit a numeric union.</summary>
    AsNumber = 2,
}
