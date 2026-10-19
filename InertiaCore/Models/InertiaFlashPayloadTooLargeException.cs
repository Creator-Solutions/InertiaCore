using System;
using System.Globalization;

namespace InertiaCore.Models;

/// <summary>
/// Thrown when serialized flash data exceeds the size a cookie-backed store can carry.
/// <para>
/// Register <c>AddInertiaDistributedCacheFlash()</c> so oversized payloads are stored in
/// <c>IDistributedCache</c> instead, or raise
/// <see cref="InertiaFlashOptions.MaxCookiePayloadBytes"/>.
/// </para>
/// </summary>
public class InertiaFlashPayloadTooLargeException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="InertiaFlashPayloadTooLargeException"/>.
    /// </summary>
    /// <param name="actualBytes">The size of the payload that was rejected.</param>
    /// <param name="maxBytes">The configured maximum size.</param>
    public InertiaFlashPayloadTooLargeException(int actualBytes, int maxBytes)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "The flash payload is {0} bytes, which exceeds the {1} byte limit for a cookie-backed flash store. " +
            "Register InertiaCore's distributed-cache flash store with services.AddInertiaDistributedCacheFlash() " +
            "(after services.AddDistributedMemoryCache() or your own IDistributedCache registration), or raise " +
            "InertiaFlashOptions.MaxCookiePayloadBytes.",
            actualBytes,
            maxBytes))
    {
        ActualBytes = actualBytes;
        MaxBytes = maxBytes;
    }

    /// <summary>
    /// The size of the rejected payload, in bytes.
    /// </summary>
    public int ActualBytes { get; }

    /// <summary>
    /// The configured maximum payload size, in bytes.
    /// </summary>
    public int MaxBytes { get; }
}
