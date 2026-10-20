using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.Extensions;
using InertiaCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InertiaCore.Services;

/// <summary>
/// Built-in shared-data provider that shares <c>auth.user</c> derived from the current
/// <see cref="System.Security.Claims.ClaimsPrincipal"/>.
/// <para>
/// The value is an object built from <see cref="InertiaClaimsSharedDataOptions.ClaimMapping"/>
/// and nested under <see cref="InertiaClaimsSharedDataOptions.Key"/> (default <c>auth</c>).
/// The provider contributes nothing when the user is not authenticated or no claims map to
/// a value.
/// </para>
/// </summary>
public sealed class ClaimsSharedDataProvider : IInertiaSharedDataProvider
{
    private readonly IOptions<InertiaOptions> _options;

    /// <summary>
    /// Initializes a new instance of <see cref="ClaimsSharedDataProvider"/>.
    /// </summary>
    /// <param name="options">The Inertia options carrying the claims mapping.</param>
    public ClaimsSharedDataProvider(IOptions<InertiaOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, object?>> GetSharedDataAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var settings = _options.Value.ClaimsSharedData;
        var user = context.User;

        if (!settings.Enabled || user?.Identity?.IsAuthenticated != true || settings.ClaimMapping.Count == 0)
            return new ValueTask<IReadOnlyDictionary<string, object?>>(Empty);

        var mapped = new Dictionary<string, object?>();

        foreach (var (prop, claimType) in settings.ClaimMapping)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (value is not null)
                mapped[prop.ToCamelCase()] = value;
        }

        if (mapped.Count == 0)
            return new ValueTask<IReadOnlyDictionary<string, object?>>(Empty);

        var result = new Dictionary<string, object?>
        {
            [settings.Key.ToCamelCase()] = new Dictionary<string, object?> { ["user"] = mapped },
        };

        return new ValueTask<IReadOnlyDictionary<string, object?>>(result);
    }

    private static readonly IReadOnlyDictionary<string, object?> Empty =
        new Dictionary<string, object?>();
}
