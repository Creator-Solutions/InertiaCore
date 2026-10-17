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

    /// <summary>
    /// Gets or sets a value indicating whether a React helper section is emitted alongside
    /// the declarations. When <see langword="true"/>, the output imports
    /// <c>usePage</c> from <c>@inertiajs/react</c> and exports a typed
    /// <c>usePageProps</c> helper keyed by <c>InertiaPageProps</c>. Defaults to
    /// <see langword="false"/> so projects that do not use React hooks are unaffected.
    /// </summary>
    public bool EmitReactHelper { get; set; }
}
