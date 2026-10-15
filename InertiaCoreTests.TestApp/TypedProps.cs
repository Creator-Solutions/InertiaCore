using InertiaCore;

namespace InertiaCoreTests.TestApp;

/// <summary>Typed props page contract used by the IN-54 integration tests.</summary>
[InertiaPage("Users/Index")]
public sealed record UsersIndexProps(IReadOnlyList<string> Users, int Page);
