using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using InertiaCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InertiaCore.Extensions;

/// <summary>
/// Collects named Minimal API endpoint metadata from <see cref="EndpointDataSource"/> and
/// exposes it so that it can be dumped (for example by the Development-only
/// <c>/_inertia/routes</c> endpoint) and consumed by <c>inertiatsgen routes</c>.
/// </summary>
public static class InertiaRouteExtensions
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Collects every named route that has been mapped on this endpoint route builder.
    /// Endpoints without a name (for example those mapped with <c>.WithName(...)</c>
    /// omitted) are ignored. Routes are returned ordered by name for deterministic output.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder holding the mapped data sources.</param>
    /// <returns>The collected named routes.</returns>
    public static IReadOnlyList<InertiaRoute> GetInertiaRoutes(this IEndpointRouteBuilder endpoints)
    {
        if (endpoints is null) throw new ArgumentNullException(nameof(endpoints));

        var routes = new List<InertiaRoute>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (EndpointDataSource dataSource in endpoints.DataSources)
        {
            foreach (Endpoint endpoint in dataSource.Endpoints)
            {
                if (endpoint is not RouteEndpoint routeEndpoint)
                {
                    continue;
                }

                string? name = routeEndpoint.Metadata.GetMetadata<RouteNameMetadata>()?.RouteName;
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                {
                    continue;
                }

                routes.Add(new InertiaRoute
                {
                    Name = name,
                    Pattern = FormatPattern(routeEndpoint.RoutePattern),
                    HttpMethods = routeEndpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                                  ?? Array.Empty<string>(),
                    Parameters = ExtractParameters(routeEndpoint.RoutePattern),
                });
            }
        }

        return routes
            .OrderBy(static r => r.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Maps a Development-only endpoint that returns the collected route metadata as JSON.
    /// This is the runtime-dump option for feeding <c>inertiatsgen routes</c>: run the app
    /// in Development, fetch the endpoint, and pass the JSON to the tool. Requests outside
    /// the Development environment return <c>404</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern to map. Defaults to <c>/_inertia/routes</c>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapInertiaRoutes(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/_inertia/routes")
    {
        if (endpoints is null) throw new ArgumentNullException(nameof(endpoints));

        endpoints.MapGet(pattern, (HttpContext context) =>
        {
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            if (!environment.IsDevelopment())
            {
                return Results.NotFound();
            }

            string json = JsonSerializer.Serialize(new { Routes = endpoints.GetInertiaRoutes() }, JsonOptions);
            return Results.Text(json, "application/json");
        });

        return endpoints;
    }

    /// <summary>
    /// Rebuilds a clean route pattern (constraints stripped, catch-all and optional
    /// markers preserved) from the structured route pattern parts.
    /// </summary>
    private static string FormatPattern(RoutePattern pattern)
    {
        var builder = new StringBuilder();

        foreach (RoutePatternPathSegment segment in pattern.PathSegments)
        {
            builder.Append('/');

            foreach (RoutePatternPart part in segment.Parts)
            {
                if (part is RoutePatternLiteralPart literal)
                {
                    builder.Append(literal.Content);
                }
                else if (part is RoutePatternParameterPart parameter)
                {
                    builder.Append('{');
                    if (parameter.IsCatchAll)
                    {
                        builder.Append('*');
                    }

                    builder.Append(parameter.Name);
                    if (parameter.IsOptional && !parameter.IsCatchAll)
                    {
                        builder.Append('?');
                    }

                    builder.Append('}');
                }
                else if (part is RoutePatternSeparatorPart separator)
                {
                    builder.Append(separator.Content);
                }
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<InertiaRouteParameter> ExtractParameters(RoutePattern pattern)
    {
        var parameters = new List<InertiaRouteParameter>();

        foreach (RoutePatternParameterPart parameter in pattern.Parameters)
        {
            string? constraint = null;
            foreach (RoutePatternParameterPolicyReference policy in parameter.ParameterPolicies)
            {
                // Inline constraints (for example "int" or "guid") are the policies that
                // do not carry a "key=value" form (used by default-value style policies).
                if (!string.IsNullOrEmpty(policy.Content) && !policy.Content.Contains('='))
                {
                    constraint = policy.Content;
                    break;
                }
            }

            parameters.Add(new InertiaRouteParameter
            {
                Name = parameter.Name,
                IsOptional = parameter.IsOptional || parameter.IsCatchAll,
                IsCatchAll = parameter.IsCatchAll,
                Constraint = constraint,
            });
        }

        return parameters;
    }
}
