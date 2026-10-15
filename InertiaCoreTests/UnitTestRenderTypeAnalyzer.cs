using System.Collections.Immutable;
using InertiaCore.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace InertiaCoreTests;

public partial class Tests
{
    private static async Task<ImmutableArray<Diagnostic>> RunRenderAnalyzerAsync(string source)
    {
        var parseOpts = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
        var tree = CSharpSyntaxTree.ParseText(source, parseOpts);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            GetDefaultRefs(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new RenderTypeArgumentAnalyzer()));

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Test]
    [Description("INERTIA006 is reported for Inertia.Render<T>/IInertia.Render<T> where T has no [InertiaPage]")]
    public async Task Analyzer_ReportsMissingInertiaPage()
    {
        var source = """
using InertiaCore;
using InertiaCore.Contracts;

namespace TestApp
{
    public sealed record UnknownProps(string Name);

    [InertiaPage("Home")]
    public sealed record HomeProps(string Name);

    public class Caller
    {
        public void StaticUnknown() => Inertia.Render(new UnknownProps("x"));
        public void InterfaceUnknown(IInertia inertia) => inertia.Render(new UnknownProps("x"));
        public void StaticKnown() => Inertia.Render(new HomeProps("x"));
        public void InterfaceKnown(IInertia inertia) => inertia.Render(new HomeProps("x"));
    }
}
""";
        var diagnostics = (await RunRenderAnalyzerAsync(source))
            .Where(d => d.Id == "INERTIA006")
            .ToArray();

        Assert.That(diagnostics, Has.Length.EqualTo(2));
    }

    [Test]
    [Description("INERTIA006 is not reported when every Render<T> type has [InertiaPage]")]
    public async Task Analyzer_NoDiagnosticWhenAllTyped()
    {
        var source = """
using InertiaCore;
using InertiaCore.Contracts;

namespace TestApp
{
    [InertiaPage("Home")]
    public sealed record HomeProps(string Name);

    public class Caller
    {
        public void Use(IInertia inertia)
        {
            Inertia.Render(new HomeProps("x"));
            inertia.Render(new HomeProps("y"));
        }
    }
}
""";
        var diagnostics = (await RunRenderAnalyzerAsync(source))
            .Where(d => d.Id == "INERTIA006")
            .ToArray();

        Assert.That(diagnostics, Is.Empty);
    }
}
