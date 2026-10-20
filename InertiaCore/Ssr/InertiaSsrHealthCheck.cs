using System;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace InertiaCore.Ssr;

/// <summary>
/// Reports the health of the SSR server. Registered by <c>AddInertia()</c> as the
/// <c>inertia-ssr</c> health check (tagged <c>ssr</c>). The probe flows through the same
/// typed client and resilience pipeline as render requests, so the result reflects what
/// render traffic actually experiences.
/// </summary>
/// <remarks>
/// <para>
/// Results:
/// <list type="bullet">
/// <item><description><see cref="HealthStatus.Healthy"/> — SSR is disabled, or the circuit is
/// closed and the health endpoint responds successfully.</description></item>
/// <item><description><see cref="HealthStatus.Degraded"/> — the circuit breaker is open;
/// the probe fails fast without contacting the server.</description></item>
/// <item><description><see cref="HealthStatus.Unhealthy"/> — the circuit is closed but the
/// health endpoint cannot be reached or returned a non-success status.</description></item>
/// </list>
/// </para>
/// <para>
/// The probed URL is <see cref="InertiaOptions.SsrHealthUrl"/> when set; otherwise it is
/// derived from <see cref="InertiaOptions.SsrUrl"/> by taking its scheme, host and port and
/// appending <c>/health</c>.
/// </para>
/// </remarks>
public sealed class InertiaSsrHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<InertiaOptions> _options;
    private readonly ILogger<InertiaSsrHealthCheck>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InertiaSsrHealthCheck"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The client factory used to create the resilient SSR client.</param>
    /// <param name="options">The Inertia options that describe the SSR server and budget.</param>
    /// <param name="logger">Optional logger.</param>
    public InertiaSsrHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<InertiaOptions> options,
        ILogger<InertiaSsrHealthCheck>? logger = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        if (!options.SsrEnabled)
            return HealthCheckResult.Healthy("SSR is disabled.");

        var healthUrl = ResolveHealthUrl(options);
        if (healthUrl is null)
        {
            return HealthCheckResult.Unhealthy(
                $"The SSR health URL '{options.SsrHealthUrl ?? options.SsrUrl}' is not a valid absolute http/https URL.");
        }

        // The probe shares the render client (and its resilience pipeline): an open circuit
        // fails fast as Degraded, everything else behaves exactly like a render request.
        var client = _httpClientFactory.CreateClient(InertiaSsrClient.HttpClientName);

        using var timeout = new CancellationTokenSource(options.SsrTimeout);

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using var response = await client.GetAsync(healthUrl, linked.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return HealthCheckResult.Healthy($"The SSR server at {healthUrl} is reachable.");

            return HealthCheckResult.Unhealthy(
                $"The SSR health endpoint {healthUrl} responded with status {(int)response.StatusCode}.");
        }
        catch (BrokenCircuitException)
        {
            return HealthCheckResult.Degraded("The SSR circuit breaker is open.");
        }
        catch (TimeoutRejectedException)
        {
            return HealthCheckResult.Unhealthy($"The SSR health probe to {healthUrl} timed out after {options.SsrTimeout}.");
        }
        catch (RateLimiterRejectedException)
        {
            return HealthCheckResult.Unhealthy($"The SSR health probe to {healthUrl} was rejected by the client rate limiter.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"The SSR health probe to {healthUrl} timed out after {options.SsrTimeout}.");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogDebug(ex, "Inertia SSR health probe to {Url} failed.", healthUrl);
            return HealthCheckResult.Unhealthy($"The SSR health endpoint {healthUrl} could not be reached: {ex.Message}");
        }
    }

    private string? ResolveHealthUrl(InertiaOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SsrHealthUrl))
        {
            return TryGetValidHttpUrl(options.SsrHealthUrl) ? options.SsrHealthUrl : null;
        }

        if (!Uri.TryCreate(options.SsrUrl, UriKind.Absolute, out var ssrUri)
            || (ssrUri.Scheme != Uri.UriSchemeHttp && ssrUri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return new UriBuilder(ssrUri.Scheme, ssrUri.Host, ssrUri.Port, "/health").Uri.ToString();
    }

    private static bool TryGetValidHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
