using System.Text;

namespace InertiaCore.TypeGen;

internal static class Program
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static int Main(string[] args)
    {
        CliOptions options = CliOptions.Parse(args);

        if (options.ShowHelp)
        {
            Console.Out.WriteLine(CliOptions.HelpText);
            return 0;
        }

        if (options.Error is not null)
        {
            Console.Error.WriteLine(options.Error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return 2;
        }

        if (string.IsNullOrWhiteSpace(options.AssemblyPath))
        {
            Console.Error.WriteLine("--assembly is required.");
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return 2;
        }

        try
        {
            if (options.Watch)
            {
                return RunWatch(options);
            }

            return RunOnce(options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    private static int RunOnce(CliOptions options)
    {
        GenerationResult result = Generate(options);

        foreach (GenerationDiagnostic diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Severity.ToString().ToLowerInvariant()}: {diagnostic.Message}");
        }

        if (options.Check)
        {
            string outputPath = Path.GetFullPath(options.OutputPath);
            if (!File.Exists(outputPath))
            {
                Console.Error.WriteLine($"error: '{outputPath}' is missing. Run without --check to generate it.");
                return 1;
            }

            string existing = File.ReadAllText(outputPath);
            if (!string.Equals(Normalize(existing), Normalize(result.Content), StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"error: '{outputPath}' is out of date. Re-run the generator.");
                return 1;
            }

            Console.Out.WriteLine($"{outputPath} is up to date ({result.PageCount} page(s)).");
            return 0;
        }

        WriteFile(options.OutputPath, result.Content);
        Console.Out.WriteLine($"Generated {Path.GetFullPath(options.OutputPath)} ({result.PageCount} page(s)).");
        return 0;
    }

    private static int RunWatch(CliOptions options)
    {
        string assemblyPath = Path.GetFullPath(options.AssemblyPath!);
        string directory = Path.GetDirectoryName(assemblyPath) ?? ".";
        string fileName = Path.GetFileName(assemblyPath);

        RunOnce(options);

        using var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };

        using var signal = new AutoResetEvent(false);
        watcher.Changed += (_, _) => signal.Set();
        watcher.Created += (_, _) => signal.Set();
        watcher.Renamed += (_, _) => signal.Set();

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
            signal.Set();
        };

        Console.Out.WriteLine($"Watching {assemblyPath} for changes. Press Ctrl+C to stop.");

        while (!cancellation.IsCancellationRequested)
        {
            if (!signal.WaitOne(TimeSpan.FromMilliseconds(500)))
            {
                continue;
            }

            Thread.Sleep(300);

            if (cancellation.IsCancellationRequested)
            {
                break;
            }

            try
            {
                RunOnce(options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
            }
        }

        return 0;
    }

    private static GenerationResult Generate(CliOptions options)
    {
        var generatorOptions = new TypeScriptGeneratorOptions
        {
            AssemblyPath = options.AssemblyPath!,
            UseCamelCase = options.UseCamelCase,
            EnumMode = options.EnumMode,
        };

        foreach (string reference in options.ReferencePaths)
        {
            generatorOptions.ReferencePaths.Add(reference);
        }

        return new TypeScriptDeclarationGenerator().Generate(generatorOptions);
    }

    private static void WriteFile(string path, string content)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content, Utf8NoBom);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
}
