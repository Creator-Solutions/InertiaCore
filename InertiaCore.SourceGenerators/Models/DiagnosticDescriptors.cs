using Microsoft.CodeAnalysis;

namespace InertiaCore.SourceGenerators.Models;

internal static class DiagnosticDescriptors
{
    private const string Category = "InertiaCore.SourceGenerators";

    public static readonly DiagnosticDescriptor Inertia002 = new(
        id: "INERTIA002",
        title: "Duplicate component name",
        messageFormat: "The component name '{0}' is declared on multiple types ({1}, {2}). Each [InertiaPage] component name must be unique within a compilation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Inertia004 = new(
        id: "INERTIA004",
        title: "Identifier collision between component paths",
        messageFormat: "The component paths '{0}' and '{1}' both produce the C# identifier '{2}'. Rename one of the components to avoid the collision.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
