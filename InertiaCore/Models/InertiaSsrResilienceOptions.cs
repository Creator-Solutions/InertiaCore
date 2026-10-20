using System;

namespace InertiaCore.Models;

/// <summary>
/// Resilience settings for the SSR render client. Applied by <c>AddInertia()</c> as the
/// standard resilience handler (total timeout, retry with exponential backoff and jitter,
/// circuit breaker and attempt timeout) on the typed SSR <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
/// <remarks>
/// The total request budget is <see cref="InertiaOptions.SsrTimeout"/>; it covers every
/// attempt, retry delay and the circuit-breaker interaction of a single render dispatch.
/// When the circuit breaker is open the adapter skips the SSR call entirely and renders the
/// response client-side without waiting.
/// </remarks>
public class InertiaSsrResilienceOptions
{
    /// <summary>
    /// Maximum number of retry attempts for transient failures (connection errors and
    /// transient HTTP status codes) within the overall <see cref="InertiaOptions.SsrTimeout"/>
    /// budget. Timeouts and an open circuit breaker are never retried. Must be between 1
    /// and 10; the underlying resilience handler does not support disabling retries, and
    /// retry delays are always bounded by <see cref="InertiaOptions.SsrTimeout"/>.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// Base delay between retry attempts. The actual delay grows exponentially per attempt
    /// with added jitter. Retries are also bounded by <see cref="InertiaOptions.SsrTimeout"/>,
    /// so a small base delay keeps the fallback fast for local sidecar processes.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Timeout applied to a single render attempt. When <see langword="null"/> the overall
    /// <see cref="InertiaOptions.SsrTimeout"/> is used for every attempt as well.
    /// </summary>
    public TimeSpan? AttemptTimeout { get; set; }

    /// <summary>
    /// Proportion of outcomes within the sampling window that, when failing, open the circuit
    /// breaker. Must be greater than 0 and at most 1.
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum number of outcomes within <see cref="CircuitBreakerSamplingDuration"/> required
    /// before the failure ratio can open the circuit breaker.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Window over which failures are counted for the circuit breaker.
    /// </summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the circuit breaker stays open before a trial call is allowed through.
    /// A successful trial call closes the circuit; a failed one re-opens it. The
    /// resilience handler requires at least 500 ms.
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
