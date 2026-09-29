using System.Text.Json.Serialization;

namespace InertiaCore.Models;

/// <summary>
/// Serialized shape of a single entry in the page object's <c>scrollProps</c>.
/// Token fields are always emitted (even when <see langword="null"/>) so the client
/// can distinguish "no previous page" from a missing value.
/// </summary>
internal sealed record ScrollPropEntry(
    string PageName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] object? PreviousPage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] object? NextPage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] object? CurrentPage,
    bool Reset);
