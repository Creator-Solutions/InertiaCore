using InertiaCore.TypeGen;

namespace InertiaCoreTests;

/// <summary>
/// Tests for <see cref="InertiaRouteScriptGenerator"/> and <see cref="InertiaRouteUrlBuilder"/>.
/// The generator emits a typed <c>routes.ts</c>; the URL builder mirrors the generated
/// <c>route()</c> helper so that generated definitions can be checked in unit tests.
/// </summary>
[TestFixture]
public class UnitTestRouteScriptGenerator
{
    private static InertiaRouteDto Route(
        string name,
        string pattern,
        string[]? methods = null,
        InertiaRouteParameterDto[]? parameters = null) => new()
        {
            Name = name,
            Pattern = pattern,
            HttpMethods = methods ?? new[] { "GET" },
            Parameters = parameters ?? Array.Empty<InertiaRouteParameterDto>(),
        };

    private static InertiaRouteParameterDto Param(
        string name,
        bool optional = false,
        bool catchAll = false,
        string? constraint = null) => new()
        {
            Name = name,
            IsOptional = optional,
            IsCatchAll = catchAll,
            Constraint = constraint,
        };

    [Test]
    [Description("The generated module exposes a sorted literal union of route names.")]
    public void EmitsSortedRouteNameUnion()
    {
        var routes = new[]
        {
            Route("users.show", "/users/{id}", parameters: new[] { Param("id", constraint: "int") }),
            Route("users.index", "/users"),
        };

        string content = new InertiaRouteScriptGenerator().Generate(routes);

        Assert.That(content, Does.Contain("export type InertiaRouteName = 'users.index' | 'users.show';"));
    }

    [Test]
    [Description("Zero-parameter routes emit an empty object so 'keyof ... extends never' holds.")]
    public void ZeroParameterRouteEmitsEmptyObject()
    {
        string content = new InertiaRouteScriptGenerator().Generate(new[] { Route("users.index", "/users") });

        Assert.That(content, Does.Contain("'users.index': {};"));
    }

    [Test]
    [Description("Parameter members map numeric constraints to string | number and mark optional params.")]
    public void MapsParameterConstraintsAndOptionality()
    {
        var routes = new[]
        {
            Route(
                "posts.show",
                "/users/{user}/posts/{slug?}",
                parameters: new[]
                {
                    Param("user", constraint: "int"),
                    Param("slug", optional: true, constraint: "guid"),
                }),
        };

        string content = new InertiaRouteScriptGenerator().Generate(routes);

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("'posts.show': { user: string | number; slug?: string; };"));
        });
    }

    [Test]
    [Description("Catch-all parameters accept a scalar or an array of path segments.")]
    public void MapsCatchAllParameterAsArray()
    {
        var routes = new[]
        {
            Route("search", "/search/{*rest}", parameters: new[] { Param("rest", optional: true, catchAll: true) }),
        };

        string content = new InertiaRouteScriptGenerator().Generate(routes);

        Assert.That(content, Does.Contain("'search': { rest?: string | number | (string | number)[]; };"));
    }

    [Test]
    [Description("Route definitions carry the pattern and HTTP methods.")]
    public void EmitsRouteDefinitions()
    {
        var routes = new[] { Route("users.update", "/users/{id}", methods: new[] { "PUT", "PATCH" }) };

        string content = new InertiaRouteScriptGenerator().Generate(routes);

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("'users.update': { pattern: '/users/{id}', methods: ['PUT', 'PATCH'] },"));
            Assert.That(content, Does.Contain("export function route<K extends InertiaRouteName>(name: K, ...args: InertiaRouteArgs<K>): string {"));
        });
    }

    [Test]
    [Description("Route names and patterns are escaped for single-quoted TypeScript strings.")]
    public void EscapesQuotesAndBackslashes()
    {
        string content = new InertiaRouteScriptGenerator().Generate(new[] { Route("weird'name", "/a\\b") });

        Assert.That(content, Does.Contain("'weird\\'name'"));
    }

    [Test]
    [Description("BuildUrl substitutes path parameters and appends leftover values as a query string.")]
    public void BuildUrl_SubstitutesAndAppendsQueryString()
    {
        var route = Route("users.show", "/api/users/{id}", parameters: new[] { Param("id", constraint: "int") });

        string url = InertiaRouteUrlBuilder.BuildUrl(route, new Dictionary<string, object?>
        {
            ["id"] = 5,
            ["page"] = 2,
        });

        Assert.That(url, Is.EqualTo("/api/users/5?page=2"));
    }

    [Test]
    [Description("BuildUrl joins array values for a catch-all parameter with slashes.")]
    public void BuildUrl_JoinsCatchAllArray()
    {
        var route = Route("search", "/api/search/{*rest}", parameters: new[] { Param("rest", optional: true, catchAll: true) });

        string url = InertiaRouteUrlBuilder.BuildUrl(route, new Dictionary<string, object?>
        {
            ["rest"] = new[] { "a", "b" },
        });

        Assert.That(url, Is.EqualTo("/api/search/a/b"));
    }

    [Test]
    [Description("BuildUrl percent-encodes values.")]
    public void BuildUrl_EncodesValues()
    {
        var route = Route("users.show", "/api/users/{id}", parameters: new[] { Param("id") });

        string url = InertiaRouteUrlBuilder.BuildUrl(route, new Dictionary<string, object?>
        {
            ["id"] = "a b",
        });

        Assert.That(url, Is.EqualTo("/api/users/a%20b"));
    }
}
