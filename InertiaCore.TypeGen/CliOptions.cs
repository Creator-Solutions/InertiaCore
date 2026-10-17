namespace InertiaCore.TypeGen;

/// <summary>Parsed command-line options for the <c>inertiatsgen</c> tool.</summary>
internal sealed class CliOptions
{
    internal const string DefaultOutput = "Client/types/inertia.generated.d.ts";

    public string? AssemblyPath { get; private set; }

    public string OutputPath { get; private set; } = DefaultOutput;

    public bool Check { get; private set; }

    public bool Watch { get; private set; }

    public bool UseCamelCase { get; private set; } = true;

    public EnumSerializationMode EnumMode { get; private set; } = EnumSerializationMode.Auto;

    public bool EmitReactHelper { get; private set; }

    public List<string> ReferencePaths { get; } = new();

    public bool ShowHelp { get; private set; }

    public string? Error { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;

                case "--check":
                    options.Check = true;
                    break;

                case "--watch":
                    options.Watch = true;
                    break;

                case "--no-camel-case":
                    options.UseCamelCase = false;
                    break;

                case "--emit-react-helper":
                    options.EmitReactHelper = true;
                    break;

                case "--assembly":
                    if (!TryTakeValue(args, ref i, out string? assembly))
                    {
                        options.Error = "--assembly requires a value.";
                        return options;
                    }

                    options.AssemblyPath = assembly;
                    break;

                case "--output":
                    if (!TryTakeValue(args, ref i, out string? output))
                    {
                        options.Error = "--output requires a value.";
                        return options;
                    }

                    options.OutputPath = output!;
                    break;

                case "--reference":
                    if (!TryTakeValue(args, ref i, out string? reference))
                    {
                        options.Error = "--reference requires a value.";
                        return options;
                    }

                    options.ReferencePaths.Add(reference!);
                    break;

                case "--enum-mode":
                    if (!TryTakeValue(args, ref i, out string? mode))
                    {
                        options.Error = "--enum-mode requires a value.";
                        return options;
                    }

                    if (!TryParseEnumMode(mode!, out EnumSerializationMode enumMode))
                    {
                        options.Error = "--enum-mode must be one of: auto, string, number.";
                        return options;
                    }

                    options.EnumMode = enumMode;
                    break;

                default:
                    options.Error = $"Unknown argument '{arg}'.";
                    return options;
            }
        }

        return options;
    }

    public static string HelpText =>
        """
        inertiatsgen - generate TypeScript declarations for InertiaCore [InertiaPage] props.

        Usage:
          inertiatsgen --assembly <path.dll> [options]

        Options:
          --assembly <path>    Compiled assembly to read. Required.
          --output <path>      Output file. Default: Client/types/inertia.generated.d.ts
          --reference <path>   Additional assembly used to resolve references. Repeatable.
          --enum-mode <mode>   auto | string | number. Default: auto.
          --no-camel-case      Disable camelCase property naming.
          --emit-react-helper  Emit a typed usePageProps helper for @inertiajs/react.
          --check              Exit non-zero if the output file is out of date. No write.
          --watch              Regenerate whenever the assembly changes.
          -h, --help           Show this help.
        """;

    private static bool TryTakeValue(string[] args, ref int index, out string? value)
    {
        if (index + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryParseEnumMode(string value, out EnumSerializationMode mode)
    {
        switch (value.ToLowerInvariant())
        {
            case "auto":
                mode = EnumSerializationMode.Auto;
                return true;
            case "string":
                mode = EnumSerializationMode.AsString;
                return true;
            case "number":
                mode = EnumSerializationMode.AsNumber;
                return true;
            default:
                mode = EnumSerializationMode.Auto;
                return false;
        }
    }
}
