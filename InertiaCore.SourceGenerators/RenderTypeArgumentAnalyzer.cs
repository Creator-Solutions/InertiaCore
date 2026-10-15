using System.Collections.Immutable;
using InertiaCore.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace InertiaCore.SourceGenerators;

/// <summary>
/// Reports <c>INERTIA006</c> when <c>Inertia.Render&lt;T&gt;</c> or
/// <c>IInertia.Render&lt;T&gt;</c> is called with a props type that is not declared
/// with <c>[InertiaPage]</c>, since the component name cannot then be resolved.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RenderTypeArgumentAnalyzer : DiagnosticAnalyzer
{
    private const string InertiaPageAttribute = "InertiaCore.InertiaPageAttribute";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.Inertia006);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var attribute = startContext.Compilation.GetTypeByMetadataName(InertiaPageAttribute);
            if (attribute is null)
                return;

            startContext.RegisterOperationAction(operationContext =>
            {
                var invocation = (IInvocationOperation)operationContext.Operation;
                var method = invocation.TargetMethod;

                if (method.Name != "Render" || method.TypeArguments.Length != 1)
                    return;

                if (!IsInertiaRender(method))
                    return;

                if (method.TypeArguments[0] is not INamedTypeSymbol typeArgument
                    || typeArgument.TypeKind == TypeKind.Error)
                    return;

                if (HasInertiaPageAttribute(typeArgument, attribute))
                    return;

                operationContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.Inertia006,
                    invocation.Syntax.GetLocation(),
                    typeArgument.ToDisplayString()));
            }, OperationKind.Invocation);
        });
    }

    private static bool IsInertiaRender(IMethodSymbol method)
    {
        var containing = method.ContainingType?.ToDisplayString();
        return containing == "InertiaCore.Inertia"
            || containing == "InertiaCore.Contracts.IInertia";
    }

    private static bool HasInertiaPageAttribute(INamedTypeSymbol type, INamedTypeSymbol attributeType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                    return true;
            }
        }

        return false;
    }
}
