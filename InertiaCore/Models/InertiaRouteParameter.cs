namespace InertiaCore.Models;

/// <summary>
/// Describes a single parameter in a Minimal API route template, used to describe the
/// shape of a route for code generation (for example <c>inertiatsgen routes</c>).
/// </summary>
public sealed record InertiaRouteParameter
{
    /// <summary>
    /// Gets the parameter name as it appears in the route template (without the
    /// catch-all <c>*</c> prefix or optional <c>?</c> suffix).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the parameter may be omitted
    /// (optional, catch-all or optional catch-all parameters).
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter is a catch-all parameter
    /// (for example <c>{*path}</c>), which matches the remainder of the URL.
    /// </summary>
    public bool IsCatchAll { get; init; }

    /// <summary>
    /// Gets the inline route constraint for the parameter (for example <c>int</c> or
    /// <c>guid</c>), or <see langword="null"/> when the parameter has no constraint.
    /// </summary>
    public string? Constraint { get; init; }
}
