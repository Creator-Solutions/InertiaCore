using System.Text.Json.Serialization;
using InertiaCore;
using InertiaCore.Props;

namespace InertiaCoreTests.TestApp;

/// <summary>Numeric enum (no converter) used by the TypeGen golden sample.</summary>
public enum UserRole
{
    Guest = 0,
    Member = 1,
    Admin = 2,
}

/// <summary>String enum (StringEnumConverter) used by the TypeGen golden sample.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserStatus
{
    Active,
    Suspended,
    Invited,
}

/// <summary>Nested record referenced by <see cref="TypedSampleUser"/>.</summary>
public sealed record Address(string Street, string? Unit);

/// <summary>Self-referential record used to prove cycles do not break generation.</summary>
public sealed record TreeNode(string Name, IReadOnlyList<TreeNode> Children);

/// <summary>Shared props shape registered for the TypeGen golden sample.</summary>
[InertiaSharedProps]
public sealed record TestAppSharedProps(
    string AppName,
    IReadOnlyList<string> Flash,
    TypedSampleUser? Auth);

/// <summary>Record with scalars, enums, nullables and collections.</summary>
public sealed record TypedSampleUser(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    UserRole Role,
    UserStatus Status,
    int? ManagerId,
    IReadOnlyList<string> Tags,
    Address Address,
    Dictionary<string, int> Scores);

/// <summary>Rich props contract that exercises every mapping rule.</summary>
[InertiaPage("TypedSample/Index")]
public sealed record TypedSampleProps(
    IReadOnlyList<TypedSampleUser> Users,
    TypedSampleUser? Primary,
    string[] Labels,
    [property: JsonPropertyName("total_count")] int TotalCount,
    IReadOnlyList<TreeNode> Tree)
{
    [JsonIgnore]
    public string? InternalNote { get; init; }

    public LazyProp? LazyStats { get; init; }

    public DeferredProp? DeferredActivity { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MaybeShown { get; init; }

    public double? OptionalMetric { get; init; }

    public object? Extra { get; init; }
}
