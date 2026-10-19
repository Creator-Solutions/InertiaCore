using System;

namespace InertiaCore.Models;

/// <summary>
/// Options controlling how Inertia flash data is stored between requests.
/// </summary>
public class InertiaFlashOptions
{
    /// <summary>
    /// Default size cap, in bytes, for a single serialized flash payload.
    /// </summary>
    public const int DefaultMaxCookiePayloadBytes = 4096;

    /// <summary>
    /// Maximum size, in bytes, of the serialized flash payload stored in a cookie-backed
    /// store. A payload larger than this throws <see cref="InertiaFlashPayloadTooLargeException"/>.
    /// Register the distributed-cache flash store instead for payloads that can exceed it.
    /// </summary>
    public int MaxCookiePayloadBytes { get; set; } = DefaultMaxCookiePayloadBytes;
}
