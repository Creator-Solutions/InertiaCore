namespace InertiaCore.TypeGen;

/// <summary>
/// The severity of a <see cref="GenerationDiagnostic"/>.
/// </summary>
public enum GenerationSeverity
{
    /// <summary>An informational message.</summary>
    Info = 0,

    /// <summary>A non-fatal issue; generation still completes.</summary>
    Warning = 1,
}

/// <summary>
/// A diagnostic produced while generating TypeScript declarations. Diagnostics never
/// abort generation; unresolved members degrade to <c>unknown</c>.
/// </summary>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Message">The human-readable message.</param>
public sealed record GenerationDiagnostic(GenerationSeverity Severity, string Message);

/// <summary>
/// The result of a declaration-generation run.
/// </summary>
public sealed class GenerationResult
{
    /// <summary>
    /// Gets the generated TypeScript declaration file content.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets the diagnostics produced during generation.
    /// </summary>
    public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; init; } = Array.Empty<GenerationDiagnostic>();

    /// <summary>
    /// Gets the number of <c>[InertiaPage]</c> types that were discovered.
    /// </summary>
    public int PageCount { get; init; }
}
