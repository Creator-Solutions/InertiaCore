using System.Collections.Generic;

namespace InertiaCore.Models;

/// <summary>
/// Describes a named Minimal API endpoint for code generation. The metadata is collected
/// from <c>EndpointDataSource</c> once the application has built and mapped its routes,
/// and is the input to <c>inertiatsgen routes</c>.
/// </summary>
public sealed record InertiaRoute
{
    /// <summary>Gets the route name, as assigned with <c>.WithName(...)</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the clean route pattern (constraints stripped) such as <c>/users/{id}</c> or
    /// <c>/search/{*rest}</c>.
    /// </summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>Gets the HTTP methods the endpoint accepts (for example <c>GET</c>).</summary>
    public IReadOnlyList<string> HttpMethods { get; init; } = new List<string>();

    /// <summary>Gets the parameters declared in the route template.</summary>
    public IReadOnlyList<InertiaRouteParameter> Parameters { get; init; } = new List<InertiaRouteParameter>();
}
