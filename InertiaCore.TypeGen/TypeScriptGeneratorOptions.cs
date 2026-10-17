using System.Collections.Generic;

namespace InertiaCore.TypeGen;

/// <summary>
/// Options that control a single declaration-generation run.
/// </summary>
public sealed class TypeScriptGeneratorOptions
{
    /// <summary>
    /// Gets or sets the path to the compiled assembly that contains the
    /// <c>[InertiaPage]</c> types. Required.
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional assembly paths used to resolve referenced types.
    /// The directory of <see cref="AssemblyPath"/> and the current runtime's
    /// framework assemblies are always considered.
    /// </summary>
    public IList<string> ReferencePaths { get; } = new List<string>();

    /// <summary>
    /// Gets or sets a value indicating whether property names use the camelCase
    /// policy. Mirrors the default InertiaCore serializer behaviour. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    public bool UseCamelCase { get; set; } = true;

    /// <summary>
    /// Gets or sets the enum projection mode. Defaults to
    /// <see cref="EnumSerializationMode.Auto"/>.
    /// </summary>
    public EnumSerializationMode EnumMode { get; set; } = EnumSerializationMode.Auto;
}
