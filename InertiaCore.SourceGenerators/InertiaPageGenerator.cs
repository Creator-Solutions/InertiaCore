using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using InertiaCore.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace InertiaCore.SourceGenerators;

[Generator]
public class InertiaPageGenerator : IIncrementalGenerator
{
    private const string AttributeFullyQualifiedName = "InertiaCore.InertiaPageAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pageInfos = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeFullyQualifiedName,
            predicate: static (node, _) => node is CompilationUnitSyntax or TypeDeclarationSyntax,
            transform: static (ctx, ct) => ExtractPages(ctx, ct))
            .SelectMany(static (p, _) => p);

        var collected = pageInfos.Collect()
            .Select(static (list, _) => ProcessPages(list));

        context.RegisterSourceOutput(collected, static (spc, result) =>
        {
            if (result.Pages.Length == 0 && result.Diagnostics.Length == 0)
                return;

            foreach (var diag in result.Diagnostics)
                spc.ReportDiagnostic(diag);

            EmitSourceResult(spc, result.Pages);
        });
    }

    private static ImmutableArray<PageInfo> ExtractPages(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (context.TargetSymbol is IAssemblySymbol assembly)
        {
            var builder = ImmutableArray.CreateBuilder<PageInfo>();

            foreach (var attr in context.Attributes)
            {
                if (!TryExtractComponentName(attr, out var componentName))
                    continue;

                var identifier = IdentifierSanitizer.ToIdentifier(componentName);
                builder.Add(new PageInfo(componentName, identifier, assembly.Name, GetLocation(attr)));
            }

            return builder.ToImmutable();
        }

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || typeSymbol.TypeKind != TypeKind.Class)
            return ImmutableArray<PageInfo>.Empty;

        var builder2 = ImmutableArray.CreateBuilder<PageInfo>();
        var typeLocation = typeSymbol.Locations.FirstOrDefault();
        foreach (var attr in context.Attributes)
        {
            if (!TryExtractComponentName(attr, out var componentName))
                continue;

            var identifier = IdentifierSanitizer.ToIdentifier(componentName);

            builder2.Add(new PageInfo(componentName, identifier, typeSymbol.ToDisplayString(), GetLocation(attr, typeLocation)));
        }

        return builder2.ToImmutable();
    }

    private static LocationInfo GetLocation(AttributeData attr, Location? fallback = null)
    {
        var location = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? fallback;

        if (location?.SourceTree == null)
            return new LocationInfo("", 0, 0);

        var span = location.GetLineSpan();
        return new LocationInfo(
            location.SourceTree.FilePath,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1);
    }

    private static Location? ToRoslynLocation(LocationInfo info)
    {
        if (string.IsNullOrEmpty(info.FilePath) || info.Line <= 0)
            return null;

        var position = new LinePosition(info.Line - 1, info.Column - 1);
        return Location.Create(info.FilePath, new TextSpan(0, 0), new LinePositionSpan(position, position));
    }

    private static bool TryExtractComponentName(AttributeData attr, out string componentName)
    {
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string name)
        {
            componentName = name;
            return true;
        }
        componentName = "";
        return false;
    }

    private static (ImmutableArray<PageInfo> Pages, ImmutableArray<Diagnostic> Diagnostics)
        ProcessPages(ImmutableArray<PageInfo> pages)
    {
        if (pages.Length == 0)
            return (ImmutableArray<PageInfo>.Empty, ImmutableArray<Diagnostic>.Empty);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var byComponent = new Dictionary<string, PageInfo>();
        var byIdentifier = new Dictionary<string, PageInfo>();
        var valid = new List<PageInfo>();

        foreach (var page in pages)
        {
            if (byComponent.TryGetValue(page.ComponentName, out var existing))
            {
                var diag = Diagnostic.Create(
                    DiagnosticDescriptors.Inertia002,
                    ToRoslynLocation(page.Location),
                    page.ComponentName,
                    existing.ContainingType,
                    page.ContainingType);
                diagnostics.Add(diag);
                continue;
            }
            byComponent[page.ComponentName] = page;

            if (byIdentifier.TryGetValue(page.Identifier, out var colliding))
            {
                var diag = Diagnostic.Create(
                    DiagnosticDescriptors.Inertia004,
                    ToRoslynLocation(page.Location),
                    colliding.ComponentName,
                    page.ComponentName,
                    page.Identifier);
                diagnostics.Add(diag);
                continue;
            }
            byIdentifier[page.Identifier] = page;

            valid.Add(page);
        }

        return (ImmutableArray.CreateRange(valid), diagnostics.ToImmutable());
    }

    private static void EmitSourceResult(SourceProductionContext spc, ImmutableArray<PageInfo> pages)
    {
        if (pages.Length == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#pragma warning disable CS0108");
        sb.AppendLine("namespace InertiaCore.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    public static partial class InertiaPages");
        sb.AppendLine("    {");

        foreach (var page in pages)
        {
            sb.AppendLine($"        public const string {page.Identifier} = \"{EscapeString(page.ComponentName)}\";");
        }

        sb.AppendLine();
        sb.AppendLine("        private static readonly System.Collections.Generic.HashSet<string> _knownPages = new System.Collections.Generic.HashSet<string>()");
        sb.AppendLine("        {");

        for (var i = 0; i < pages.Length; i++)
        {
            var comma = i < pages.Length - 1 ? "," : "";
            sb.AppendLine($"            {pages[i].Identifier}{comma}");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        public static bool IsValidPage(string componentName)");
        sb.AppendLine("            => _knownPages.Contains(componentName);");
        sb.AppendLine();
        sb.AppendLine("        [System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void RegisterPages()");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var page in _knownPages)");
        sb.AppendLine("                global::InertiaCore.InertiaPageRegistry.Register(page);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("#pragma warning restore CS0108");

        spc.AddSource("InertiaPages.g.cs", sb.ToString());
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
