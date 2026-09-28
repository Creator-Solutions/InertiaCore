using System.Collections.Immutable;
using InertiaCore;
using InertiaCore.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InertiaCoreTests;

public partial class Tests
{
    private static MetadataReference[] GetDefaultRefs()
    {
        var refs = new List<MetadataReference>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
        }
        return refs.ToArray();
    }

    private static (string? GeneratedSource, ImmutableArray<Diagnostic> Diagnostics)
        RunGenerator(params string[] sources)
    {
        var parseOpts = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
        var trees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source, parseOpts))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            GetDefaultRefs(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new InertiaPageGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedComp, out var diagnostics);

        var genTree = updatedComp.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.EndsWith("InertiaPages.g.cs"));
        var genText = genTree?.ToString();
        return (genText, diagnostics);
    }

    [Test]
    [Description("Single page generates correct const and IsValidPage")]
    public void TestSinglePage()
    {
        var source = """
using InertiaCore;

[assembly: InertiaPage("Home/Index")]
namespace TestApp;
""";
        var (gen, diags) = RunGenerator(source);

        Assert.That(diags, Is.Empty);
        Assert.That(gen, Is.Not.Null);
        Assert.That(gen, Does.Contain("HomeIndex"));
        Assert.That(gen, Does.Contain("\"Home/Index\""));
        Assert.That(gen, Does.Contain("IsValidPage"));
    }

    [Test]
    [Description("Multiple distinct pages all appear in generated code")]
    public void TestMultiplePages()
    {
        var source = """
using InertiaCore;

[assembly: InertiaPage("Dashboard")]
[assembly: InertiaPage("Users/List")]
[assembly: InertiaPage("Users/Edit")]
namespace TestApp;
""";
        var (gen, diags) = RunGenerator(source);

        Assert.That(diags, Is.Empty);
        Assert.That(gen, Is.Not.Null);
        Assert.That(gen, Does.Contain("Dashboard"));
        Assert.That(gen, Does.Contain("UsersList"));
        Assert.That(gen, Does.Contain("UsersEdit"));
    }

    [Test]
    [Description("Duplicate component name produces INERTIA002 error")]
    public void TestDuplicateComponentName()
    {
        var source = """
using InertiaCore;

[assembly: InertiaPage("Home")]
[assembly: InertiaPage("Home")]
namespace TestApp;
""";
        var (gen, diags) = RunGenerator(source);
        var inertia002 = diags.FirstOrDefault(d => d.Id == "INERTIA002");

        Assert.That(inertia002, Is.Not.Null);
        Assert.That(inertia002!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
    }

    [Test]
    [Description("Identifier collision between two pages produces INERTIA004")]
    public void TestIdentifierCollision()
    {
        var source = """
using InertiaCore;

[assembly: InertiaPage("Foo/Bar")]
[assembly: InertiaPage("Foo.Bar")]
namespace TestApp;
""";
        var (gen, diags) = RunGenerator(source);
        var inertia004 = diags.FirstOrDefault(d => d.Id == "INERTIA004");

        Assert.That(inertia004, Is.Not.Null);
        Assert.That(inertia004!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
    }

    [Test]
    [Description("Special characters in component path are sanitized to PascalCase")]
    public void TestSanitization()
    {
        var source = """
using InertiaCore;

[assembly: InertiaPage("Users/Profile/Settings")]
[assembly: InertiaPage("Admin.Dashboard")]
[assembly: InertiaPage("Reports\\Annual")]
namespace TestApp;
""";
        var (gen, diags) = RunGenerator(source);

        Assert.That(diags, Is.Empty);
        Assert.That(gen, Is.Not.Null);
        Assert.That(gen, Does.Contain("UsersProfileSettings"));
        Assert.That(gen, Does.Contain("AdminDashboard"));
        Assert.That(gen, Does.Contain("ReportsAnnual"));
    }

    [Test]
    [Description("[InertiaPage] on a class is supported and generates the expected const")]
    public void TestClassTarget()
    {
        var source = """
using InertiaCore;

namespace TestApp
{
    [InertiaPage("Account/Settings")]
    public class AccountController { }
}
""";
        var (gen, diags) = RunGenerator(source);

        Assert.That(diags, Is.Empty);
        Assert.That(gen, Is.Not.Null);
        Assert.That(gen, Does.Contain("AccountSettings"));
        Assert.That(gen, Does.Contain("\"Account/Settings\""));
    }

    [Test]
    [Description("Assembly attributes spread across multiple files do not produce duplicate diagnostics")]
    public void TestAssemblyAttributesAcrossFiles()
    {
        var file1 = """
using InertiaCore;

[assembly: InertiaPage("Home")]
namespace TestApp;
""";
        var file2 = """
using InertiaCore;

[assembly: InertiaPage("Admin/Dashboard")]
namespace TestApp.Admin;
""";
        var (gen, diags) = RunGenerator(file1, file2);

        Assert.Multiple(() =>
        {
            Assert.That(diags, Is.Empty);
            Assert.That(gen, Does.Contain("Home"));
            Assert.That(gen, Does.Contain("AdminDashboard"));
        });
    }

    [Test]
    [Description("Attributes on multiple parts of a partial class do not produce duplicate diagnostics")]
    public void TestPartialClassAcrossFiles()
    {
        var file1 = """
using InertiaCore;

namespace TestApp
{
    [InertiaPage("Account/Settings")]
    public partial class AccountController { }
}
""";
        var file2 = """
using InertiaCore;

namespace TestApp
{
    [InertiaPage("Account/Profile")]
    public partial class AccountController { }
}
""";
        var (gen, diags) = RunGenerator(file1, file2);

        Assert.Multiple(() =>
        {
            Assert.That(diags, Is.Empty);
            Assert.That(gen, Does.Contain("AccountSettings"));
            Assert.That(gen, Does.Contain("AccountProfile"));
        });
    }

    [Test]
    [Description("[InertiaPage] on a record is supported and generates the expected const")]
    public void TestRecordTarget()
    {
        var source = """
using InertiaCore;

namespace TestApp
{
    [InertiaPage("Account/Settings")]
    public record AccountController;
}
""";
        var (gen, diags) = RunGenerator(source);

        Assert.Multiple(() =>
        {
            Assert.That(diags, Is.Empty);
            Assert.That(gen, Is.Not.Null);
            Assert.That(gen, Does.Contain("AccountSettings"));
        });
    }

    [Test]
    [Description("Generated code registers every page with InertiaPageRegistry at module load")]
    public void TestRegistersPagesAtModuleLoad()
    {
        var source = """
using InertiaCore;

[assembly: InertiaPage("Home/Index")]
namespace TestApp;
""";
        var (gen, diags) = RunGenerator(source);

        Assert.That(diags, Is.Empty);
        Assert.That(gen, Is.Not.Null);
        Assert.That(gen, Does.Contain("ModuleInitializer"));
        Assert.That(gen, Does.Contain("InertiaPageRegistry.Register"));
    }

    [Test]
    [Description("Generated source compiles without errors (assembly and class targets)")]
    public void TestGeneratedSourceCompiles()
    {
        var source = """
using InertiaCore;

[assembly: InertiaPage("Home/Index")]

namespace TestApp
{
    [InertiaPage("Account/Settings")]
    public class AccountController { }
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
    [Description("InertiaPageRegistry tracks registered component names")]
    public void Registry_TracksRegisteredPages()
    {
        InertiaPageRegistry.Register("Registry/Test");

        Assert.Multiple(() =>
        {
            Assert.That(InertiaPageRegistry.IsRegistered("Registry/Test"), Is.True);
            Assert.That(InertiaPageRegistry.IsRegistered("Registry/Missing"), Is.False);
            Assert.That(InertiaPageRegistry.RegisteredPages, Does.Contain("Registry/Test"));
        });
    }

    [Test]
    [Description("No pages compiled produces no generated output")]
    public void TestNoPages()
    {
        var source = """
namespace TestApp;
""";
        var (gen, diags) = RunGenerator(source);

        Assert.That(gen, Is.Null);
        Assert.That(diags, Is.Empty);
    }
}
