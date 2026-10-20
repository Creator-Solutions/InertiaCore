using System;
using System.Collections.Generic;

namespace InertiaCore.Models;

/// <summary>
/// Options for the built-in claims shared-data provider, which shares
/// <c>auth.user</c> derived from the current <c>ClaimsPrincipal</c>. Enable
/// <see cref="Enabled"/> to register it automatically through <c>AddInertia</c>.
/// </summary>
public class InertiaClaimsSharedDataOptions
{
    /// <summary>
    /// Default prop key the user object is shared under (rendered as <c>auth.user</c>).
    /// </summary>
    public const string DefaultKey = "auth";

    /// <summary>
    /// When <see langword="true"/>, <c>AddInertia</c> registers the built-in
    /// <c>ClaimsSharedDataProvider</c>. Disabled by default so existing applications are
    /// unaffected.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The prop key the user object is nested under. The user object is exposed as
    /// <c>{Key}.user</c> (default <c>auth.user</c>).
    /// </summary>
    public string Key { get; set; } = DefaultKey;

    /// <summary>
    /// Maps the props of the shared user object to claim types. The key is the prop name
    /// (camelCased on merge), the value is the claim type to read.
    /// <para>
    /// When empty, no user props are shared and the provider contributes nothing.
    /// </para>
    /// </summary>
    public IDictionary<string, string> ClaimMapping { get; set; } = new Dictionary<string, string>();
}
