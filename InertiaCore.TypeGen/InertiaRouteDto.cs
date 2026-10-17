using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InertiaCore.TypeGen;

/// <summary>
/// The JSON document produced by the InertiaCore Development route dump
/// (<c>/_inertia/routes</c>) and consumed by <c>inertiatsgen routes</c>.
/// </summary>
public sealed record InertiaRoutesDocument
{
    /// <summary>Gets the collected named routes.</summary>
    [JsonPropertyName("routes")]
    public IReadOnlyList<InertiaRouteDto> Routes { get; init; } = new List<InertiaRouteDto>();
}

/// <summary>Describes a single named route for code generation.</summary>
public sealed record InertiaRouteDto
{
    /// <summary>Gets the route name, as assigned with <c>.WithName(...)</c>.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the clean route pattern such as <c>/users/{id}</c>.</summary>
    [JsonPropertyName("pattern")]
    public string Pattern { get; init; } = string.Empty;

    /// <summary>Gets the HTTP methods the endpoint accepts.</summary>
    [JsonPropertyName("httpMethods")]
    public IReadOnlyList<string> HttpMethods { get; init; } = new List<string>();

    /// <summary>Gets the parameters declared in the route template.</summary>
    [JsonPropertyName("parameters")]
    public IReadOnlyList<InertiaRouteParameterDto> Parameters { get; init; } = new List<InertiaRouteParameterDto>();
}

/// <summary>Describes a single route parameter for code generation.</summary>
public sealed record InertiaRouteParameterDto
{
    /// <summary>Gets the parameter name (without the catch-all or optional markers).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the parameter may be omitted.</summary>
    [JsonPropertyName("isOptional")]
    public bool IsOptional { get; init; }

    /// <summary>Gets a value indicating whether the parameter is a catch-all parameter.</summary>
    [JsonPropertyName("isCatchAll")]
    public bool IsCatchAll { get; init; }

    /// <summary>Gets the inline route constraint, or <see langword="null"/>.</summary>
    [JsonPropertyName("constraint")]
    public string? Constraint { get; init; }
}
