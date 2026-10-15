using System.Collections.Immutable;
using InertiaCore.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("A props record with [InertiaPage] emits a type-to-component mapping")]
    public void TestTypedPageEmitsTypeMap()
    {
        var source = """
using InertiaCore;

namespace TestApp
{
    [InertiaPage("Users/Index")]
    public sealed record UsersIndexProps(System.Collections.Generic.IReadOnlyList<string> Users);
}
""";
        var (gen, diags) = RunGenerator(source);

        Assert.Multiple(() =>
        {
            Assert.That(diags, Is.Empty);
            Assert.That(gen, Is.Not.Null);
            Assert.That(gen, Does.Contain("_componentsByType"));
            Assert.That(gen, Does.Contain("typeof(global::TestApp.UsersIndexProps)"));
            Assert.That(gen, Does.Contain("\"Users/Index\""));
            Assert.That(gen, Does.Contain("InertiaPageRegistry.Register(pair.Key, pair.Value)"));
        });
    }

    [Test]
    [Description("Generated typed mapping compiles without errors")]
    public void TestTypedPageGeneratedSourceCompiles()
    {
        var source = """
using InertiaCore;

namespace TestApp
{
    [InertiaPage("Users/Index")]
    public sealed record UsersIndexProps(string Name);
}
""";
        var parseOpts = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
        var tree = CSharpSyntaxTree.ParseText(source, parseOpts);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            GetDefaultRefs(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new InertiaPageGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedComp, out _);

        var errors = updatedComp.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(errors, Is.Empty, string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Test]
    [Description("Two types declaring the same component name produce INERTIA005 (not INERTIA002)")]
    public void TestDuplicateComponentAcrossTypes()
    {
        var source = """
using InertiaCore;

namespace TestApp
{
    [InertiaPage("Home")]
    public class A { }

    [InertiaPage("Home")]
    public class B { }
}
""";
        var (_, diags) = RunGenerator(source);

        Assert.Multiple(() =>
        {
            Assert.That(diags.Any(d => d.Id == "INERTIA005"), Is.True);
            Assert.That(diags.Any(d => d.Id == "INERTIA002"), Is.False);
            Assert.That(diags.First(d => d.Id == "INERTIA005").Severity, Is.EqualTo(DiagnosticSeverity.Error));
        });
    }

    [Test]
    [Description("A type with an ambiguous (multiple) component mapping is omitted from the type map")]
    public void TestAmbiguousTypeOmittedFromMap()
    {
        var source = """
using InertiaCore;

namespace TestApp
{
    [InertiaPage("Account/Settings")]
    [InertiaPage("Account/Profile")]
    public sealed record AccountProps(string Name);
}
""";
        var (gen, diags) = RunGenerator(source);

        Assert.Multiple(() =>
        {
            Assert.That(diags, Is.Empty);
            Assert.That(gen, Is.Not.Null);
            Assert.That(gen, Does.Contain("AccountSettings"));
            Assert.That(gen, Does.Not.Contain("typeof(global::TestApp.AccountProps)"));
        });
    }

    [Test]
    [Description("The generator caches unchanged work when an unrelated tree is added")]
    public void TestGeneratorIsIncremental()
    {
        var parseOpts = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
        var pageTree = CSharpSyntaxTree.ParseText("""
using InertiaCore;

[assembly: InertiaPage("Home/Index")]
namespace TestApp;
""", parseOpts, path: "pages.cs");

        var compilation1 = CSharpCompilation.Create(
            "TestAssembly",
            new[] { pageTree },
            GetDefaultRefs(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new InertiaPageGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation1);
        var firstOutput = driver.GetRunResult().GeneratedTrees.First().ToString();

        var unrelatedTree = CSharpSyntaxTree.ParseText(
            "namespace TestApp { public class Unrelated { } }", parseOpts, path: "unrelated.cs");
        driver = driver.RunGenerators(compilation1.AddSyntaxTrees(unrelatedTree));

        var result = driver.GetRunResult().Results.Single();
        var reasons = result.TrackedSteps
            .SelectMany(step => step.Value)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(driver.GetRunResult().GeneratedTrees.First().ToString(), Is.EqualTo(firstOutput),
                "added an unrelated tree so the generated output must be unchanged");
            Assert.That(
                reasons.Any(r => r is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged),
                Is.True,
                "at least one pipeline step should be cached/unchanged");
        });
    }
}
