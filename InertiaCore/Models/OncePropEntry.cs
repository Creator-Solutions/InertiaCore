using System.Text.Json.Serialization;

namespace InertiaCore.Models;

/// <summary>
/// Serialized shape of a single entry in the page object's <c>onceProps</c>.
/// <c>expiresAt</c> is always emitted (even when <see langword="null"/>) so the client
/// can distinguish "no expiry" from a missing value.
/// </summary>
internal sealed record OncePropEntry(
    string Prop,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] long? ExpiresAt);
