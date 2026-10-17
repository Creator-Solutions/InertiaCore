using InertiaCore.TypeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InertiaCoreTests;

/// <summary>
/// Tests for <see cref="TypeScriptDeclarationGenerator"/>. Mapping rules are exercised
/// with small compiled snippets; the TestApp sample is compared against a checked-in
/// golden file.
/// </summary>
[TestFixture]
public class UnitTestTypeScriptGenerator
{
    private static string CompileToAssembly(string source, bool nullable = true)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(static assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(InertiaCore.InertiaPageAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Text.Json.Serialization.JsonConverterAttribute).Assembly.Location));

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: nullable ? NullableContextOptions.Enable : NullableContextOptions.Disable);

        var compilation = CSharpCompilation.Create(
            "TypeGenSample_" + Guid.NewGuid().ToString("N"),
            new[] { tree },
            references.Distinct(),
            compilationOptions);

        string directory = Path.Combine(Path.GetTempPath(), "inertia-typegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "TypeGenSample.dll");

        var result = compilation.Emit(path);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error)));

        return path;
    }

    private static GenerationResult Generate(string source, Action<TypeScriptGeneratorOptions>? configure = null, bool nullable = true)
    {
        string assemblyPath = CompileToAssembly(source, nullable);
        var options = new TypeScriptGeneratorOptions { AssemblyPath = assemblyPath };
        options.ReferencePaths.Add(AppContext.BaseDirectory);
        configure?.Invoke(options);
        return new TypeScriptDeclarationGenerator().Generate(options);
    }

    [Test]
    [Description("Mapping rules: primitives, nullables, collections, enums and nested records.")]
    public void MapsNestedRecordsEnumsNullableAndCollections()
    {
        var source = """
            using System;
            using System.Collections.Generic;
            using InertiaCore;

            namespace Sample;

            public enum Role { Guest = 0, Admin = 2 }

            public sealed record Address(string Street, string? Unit);

            [InertiaPage("Sample/Index")]
            public sealed record SampleProps(
                string Name,
                int Count,
                bool Active,
                Guid Id,
                DateTime When,
                string? Note,
                int? MaybeCount,
                IReadOnlyList<Address> Addresses,
                string[] Tags,
                Dictionary<string, int> Scores,
                Role Role);
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.PageCount, Is.EqualTo(1));
            Assert.That(result.Content, Does.Contain("export interface Address {"));
            Assert.That(result.Content, Does.Contain("street: string;"));
            Assert.That(result.Content, Does.Contain("unit: string | null;"));
            Assert.That(result.Content, Does.Contain("name: string;"));
            Assert.That(result.Content, Does.Contain("count: number;"));
            Assert.That(result.Content, Does.Contain("active: boolean;"));
            Assert.That(result.Content, Does.Contain("id: string;"));
            Assert.That(result.Content, Does.Contain("when: string;"));
            Assert.That(result.Content, Does.Contain("note: string | null;"));
            Assert.That(result.Content, Does.Contain("maybeCount: number | null;"));
            Assert.That(result.Content, Does.Contain("addresses: Address[];"));
            Assert.That(result.Content, Does.Contain("tags: string[];"));
            Assert.That(result.Content, Does.Contain("scores: Record<string, number>;"));
            Assert.That(result.Content, Does.Contain("role: 0 | 2;"));
            Assert.That(result.Content, Does.Contain("'Sample/Index': SampleProps;"));
        });
    }

    [Test]
    [Description("Nullable collection elements and dictionary values keep their nullability.")]
    public void MapsNullableCollectionElements()
    {
        var source = """
            using System.Collections.Generic;
            using InertiaCore;

            namespace Nested;

            [InertiaPage("Nested/Index")]
            public sealed record NestedProps(
                IReadOnlyList<string?> Names,
                Dictionary<string, string?> Map);
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("names: (string | null)[];"));
            Assert.That(result.Content, Does.Contain("map: Record<string, string | null>;"));
        });
    }

    [Test]
    [Description("Mutually recursive types are emitted without crashing or looping.")]
    public void HandlesCyclicReferences()
    {
        var source = """
            using InertiaCore;

            namespace Cyc;

            public sealed record A(string Name, B? B);
            public sealed record B(string Name, A? A);

            [InertiaPage("Cyc/Index")]
            public sealed record CycProps(A Root);
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("export interface A {"));
            Assert.That(result.Content, Does.Contain("b: B | null;"));
            Assert.That(result.Content, Does.Contain("export interface B {"));
            Assert.That(result.Content, Does.Contain("a: A | null;"));
            Assert.That(result.Content, Does.Contain("root: A;"));
        });
    }

    [Test]
    [Description("Self-referential records do not crash generation.")]
    public void HandlesSelfReferentialTypes()
    {
        var source = """
            using System.Collections.Generic;
            using InertiaCore;

            namespace Self;

            public sealed record Node(string Name, IReadOnlyList<Node> Children);

            [InertiaPage("Self/Index")]
            public sealed record SelfProps(Node Root);
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("children: Node[];"));
            Assert.That(result.Content, Does.Contain("root: Node;"));
        });
    }

    [Test]
    [Description("[JsonPropertyName] wins and [JsonIgnore] members are omitted.")]
    public void RespectsSerializerAttributes()
    {
        var source = """
            using System.Text.Json.Serialization;
            using InertiaCore;

            namespace Attrs;

            [InertiaPage("Attrs/Index")]
            public sealed record AttrsProps([property: JsonPropertyName("renamed")] string Original)
            {
                [JsonIgnore]
                public string? Hidden { get; init; }

                [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                public string? Sometimes { get; init; }
            }
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("renamed: string;"));
            Assert.That(result.Content, Does.Not.Contain("hidden"));
            Assert.That(result.Content, Does.Contain("sometimes?: string | null;"));
        });
    }

    [Test]
    [Description("Lazy and Deferred props are emitted as optional properties.")]
    public void MarksWrapperPropsAsOptional()
    {
        var source = """
            using InertiaCore;
            using InertiaCore.Props;

            namespace Wrap;

            [InertiaPage("Wrap/Index")]
            public sealed record WrapProps(string Name)
            {
                public LazyProp? Lazy { get; init; }
                public DeferredProp? Deferred { get; init; }
            }
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("lazy?: unknown;"));
            Assert.That(result.Content, Does.Contain("deferred?: unknown;"));
        });
    }

    [Test]
    [Description("Enums use a numeric union unless a string converter is configured.")]
    public void MapsEnumsUsingConfiguredConverter()
    {
        var source = """
            using System.Text.Json.Serialization;
            using InertiaCore;

            namespace Enums;

            public enum Status { Active, Suspended }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            public enum Kind { First, Second }

            [InertiaPage("Enums/Index")]
            public sealed record EnumProps(Status Status, Kind Kind);
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("status: 0 | 1;"));
            Assert.That(result.Content, Does.Contain("kind: 'First' | 'Second';"));
        });
    }

    [Test]
    [Description("The enum mode can be forced to string or number.")]
    public void EnumModeOverride()
    {
        var source = """
            using InertiaCore;

            namespace Enums;

            public enum Status { Active, Suspended }

            [InertiaPage("Enums/Index")]
            public sealed record EnumProps(Status Status);
            """;

        GenerationResult asNumber = Generate(source, o => o.EnumMode = EnumSerializationMode.AsNumber);
        GenerationResult asString = Generate(source, o => o.EnumMode = EnumSerializationMode.AsString);

        Assert.Multiple(() =>
        {
            Assert.That(asNumber.Content, Does.Contain("status: 0 | 1;"));
            Assert.That(asString.Content, Does.Contain("status: 'Active' | 'Suspended';"));
        });
    }

    [Test]
    [Description("camelCase naming can be disabled.")]
    public void CamelCaseCanBeDisabled()
    {
        var source = """
            using InertiaCore;

            namespace Case;

            [InertiaPage("Case/Index")]
            public sealed record CaseProps(string SomeName);
            """;

        GenerationResult result = Generate(source, o => o.UseCamelCase = false);

        Assert.That(result.Content, Does.Contain("SomeName: string;"));
    }

    [Test]
    [Description("An assembly without pages emits a placeholder and no interfaces.")]
    public void EmptyAssemblyEmitsPlaceholder()
    {
        var source = """
            namespace Empty;
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.PageCount, Is.EqualTo(0));
            Assert.That(result.Content, Does.Contain("No [InertiaPage] types were discovered."));
        });
    }

    [Test]
    [Description("A generated file always starts with the auto-generated header.")]
    public void EmitsAutoGeneratedHeader()
    {
        GenerationResult result = Generate("""
            using InertiaCore;
            namespace H;
            [InertiaPage("H/Index")]
            public sealed record HProps(string Name);
            """);

        Assert.That(result.Content, Does.StartWith("// <auto-generated />"));
    }

    [Test]
    [Description("Generating twice yields byte-identical output.")]
    public void GenerationIsDeterministic()
    {
        string assemblyPath = CompileToAssembly("""
            using System.Collections.Generic;
            using InertiaCore;
            namespace D;
            public sealed record Item(string Name, int Value);
            public enum Kind { A, B }
            [InertiaPage("D/Index")]
            public sealed record DProps(IReadOnlyList<Item> Items, Kind Kind);
            """);

        var options = new TypeScriptGeneratorOptions { AssemblyPath = assemblyPath };
        options.ReferencePaths.Add(AppContext.BaseDirectory);

        var generator = new TypeScriptDeclarationGenerator();
        string first = generator.Generate(options).Content;
        string second = generator.Generate(options).Content;

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    [Description("The TestApp sample matches the checked-in golden declaration file.")]
    public void MatchesGoldenFile()
    {
        string assemblyPath = typeof(InertiaCoreTests.TestApp.TypedSampleProps).Assembly.Location;
        var options = new TypeScriptGeneratorOptions { AssemblyPath = assemblyPath };
        options.ReferencePaths.Add(AppContext.BaseDirectory);

        string actual = new TypeScriptDeclarationGenerator().Generate(options).Content;

        string goldenPath = Path.Combine(AppContext.BaseDirectory, "Golden", "inertia.generated.d.ts");
        Assert.That(File.Exists(goldenPath), Is.True, $"Golden file not found at {goldenPath}.");
        string expected = File.ReadAllText(goldenPath);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    [Description("A missing assembly path is rejected.")]
    public void MissingAssemblyThrows()
    {
        var options = new TypeScriptGeneratorOptions { AssemblyPath = "/does/not/exist.dll" };

        Assert.That(
            () => new TypeScriptDeclarationGenerator().Generate(options),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    [Description("A type marked [InertiaSharedProps] is emitted as an InertiaSharedProps interface.")]
    public void EmitsSharedPropsInterface()
    {
        var source = """
            using System.Collections.Generic;
            using InertiaCore;

            namespace Shared;

            [InertiaSharedProps]
            public sealed record SharedProps(string AppName, IReadOnlyList<string> Flash);

            [InertiaPage("Shared/Index")]
            public sealed record SharedPageProps(string Name);
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("export interface InertiaSharedProps {"));
            Assert.That(result.Content, Does.Contain("appName: string;"));
            Assert.That(result.Content, Does.Contain("flash: string[];"));
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }

    [Test]
    [Description("Shared props that reference a nested record emit both interfaces.")]
    public void SharedPropsReferencingNestedRecordEmitsBoth()
    {
        var source = """
            using InertiaCore;

            namespace Shared;

            public sealed record Profile(string DisplayName);

            [InertiaSharedProps]
            public sealed record SharedProps(string AppName, Profile? Auth);

            [InertiaPage("Shared/Index")]
            public sealed record SharedPageProps(string Name);
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("export interface InertiaSharedProps {"));
            Assert.That(result.Content, Does.Contain("auth: Profile | null;"));
            Assert.That(result.Content, Does.Contain("export interface Profile {"));
        });
    }

    [Test]
    [Description("Without an [InertiaSharedProps] type no shared interface is emitted.")]
    public void NoSharedPropsAttributeOmitsInterface()
    {
        GenerationResult result = Generate("""
            using InertiaCore;
            namespace NoShared;
            [InertiaPage("NoShared/Index")]
            public sealed record P(string Name);
            """);

        Assert.That(result.Content, Does.Not.Contain("InertiaSharedProps"));
    }

    [Test]
    [Description("Multiple [InertiaSharedProps] types warn and pick the first deterministically.")]
    public void MultipleSharedPropsWarnAndPickFirst()
    {
        var source = """
            using InertiaCore;

            namespace Shared;

            [InertiaSharedProps]
            public sealed record ZetaProps(string Z);

            [InertiaSharedProps]
            public sealed record AlphaProps(string A);

            [InertiaPage("Shared/Index")]
            public sealed record SharedPageProps(string Name);
            """;

        GenerationResult result = Generate(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Severity, Is.EqualTo(GenerationSeverity.Warning));
            Assert.That(result.Content, Does.Contain("a: string;"));
            Assert.That(result.Content, Does.Not.Contain("z: string;"));
        });
    }

    [Test]
    [Description("The React helper is omitted by default.")]
    public void ReactHelperOmittedByDefault()
    {
        GenerationResult result = Generate("""
            using InertiaCore;
            namespace H;
            [InertiaPage("H/Index")]
            public sealed record P(string Name);
            """);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Not.Contain("usePageProps"));
            Assert.That(result.Content, Does.Not.Contain("@inertiajs/react"));
        });
    }

    [Test]
    [Description("The React helper is emitted behind the option and imports usePage.")]
    public void ReactHelperEmittedWhenEnabled()
    {
        GenerationResult result = Generate(
            """
            using InertiaCore;
            namespace H;
            [InertiaPage("H/Index")]
            public sealed record P(string Name);
            """,
            o => o.EmitReactHelper = true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.Contain("import { usePage } from '@inertiajs/react';"));
            Assert.That(
                result.Content,
                Does.Contain("export function usePageProps<K extends keyof InertiaPageProps>(component: K): InertiaPageProps[K]"));
            Assert.That(result.Content, Does.Contain("return usePage().props as InertiaPageProps[K]"));
        });
    }

    [Test]
    [Description("The React helper intersects shared props when a shared record exists.")]
    public void ReactHelperIntersectsSharedProps()
    {
        GenerationResult result = Generate(
            """
            using InertiaCore;
            namespace H;

            [InertiaSharedProps]
            public sealed record SharedProps(string AppName);

            [InertiaPage("H/Index")]
            public sealed record P(string Name);
            """,
            o => o.EmitReactHelper = true);

        Assert.That(
            result.Content,
            Does.Contain("component: K): InertiaPageProps[K] & InertiaSharedProps"));
    }

    [Test]
    [Description("Deterministic output holds when the React helper is enabled.")]
    public void ReactHelperIsDeterministic()
    {
        string assemblyPath = CompileToAssembly("""
            using InertiaCore;
            namespace D;

            [InertiaSharedProps]
            public sealed record SharedProps(string AppName);

            [InertiaPage("D/Index")]
            public sealed record DProps(string Name);
            """);

        var options = new TypeScriptGeneratorOptions { AssemblyPath = assemblyPath, EmitReactHelper = true };
        options.ReferencePaths.Add(AppContext.BaseDirectory);

        var generator = new TypeScriptDeclarationGenerator();
        Assert.That(generator.Generate(options).Content, Is.EqualTo(generator.Generate(options).Content));
    }
}
