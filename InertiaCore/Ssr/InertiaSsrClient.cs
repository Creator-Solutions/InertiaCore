using System.Net.Http;

namespace InertiaCore.Ssr;

/// <summary>
/// Typed <see cref="HttpClient"/> for calls to the SSR server. Registered by <c>AddInertia()</c>
/// under <see cref="HttpClientName"/> and wrapped in the standard resilience handler (total
/// timeout, retry with exponential backoff and jitter, circuit breaker and attempt timeout).
/// When the circuit breaker is open, calls fail fast and the adapter renders the response
/// client-side instead of waiting on the SSR server.
/// </summary>
internal sealed class InertiaSsrClient
{
    /// <summary>
    /// The name the SSR <see cref="HttpClient"/> is registered under. The health check uses
    /// the same name so probes flow through the identical resilience pipeline.
    /// </summary>
    internal const string HttpClientName = "inertia-ssr";

    /// <summary>
    /// Initializes a new instance of the <see cref="InertiaSsrClient"/> class.
    /// </summary>
    /// <param name="httpClient">The typed client's <see cref="HttpClient"/>.</param>
    public InertiaSsrClient(HttpClient httpClient)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// The <see cref="HttpClient"/> used for SSR render requests.
    /// </summary>
    public HttpClient HttpClient { get; }
}
