using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore;
using InertiaCore.Contracts;
using InertiaCore.Props;

namespace InertiaCoreTests.TestApp;

/// <summary>
/// A scoped service with a per-request identity, used to prove that DI-resolved props
/// are resolved from the request scope (equivalent to a scoped <c>DbContext</c>).
/// </summary>
public sealed class ScopedCounter
{
    /// <summary>A value unique to this request scope.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Counts how many prop classes resolved this instance in the current scope.</summary>
    public int Resolved { get; set; }
}

/// <summary>
/// A prop class whose value is computed from a constructor-injected application service.
/// Registering it with the container and using <c>Inertia.Prop&lt;UsersProp&gt;()</c> keeps
/// the endpoint free of data-access code.
/// </summary>
public sealed class UsersProp
{
    /// <summary>Initializes a new instance of <see cref="UsersProp"/>.</summary>
    /// <param name="counter">The scoped service proving per-request resolution.</param>
    public UsersProp(ScopedCounter counter)
    {
        counter.Resolved++;
        ScopeId = counter.Id;
        ResolvedInScope = counter.Resolved;
        Users = new[] { "Ada", "Grace" };
    }

    /// <summary>The users returned for the page.</summary>
    public IReadOnlyList<string> Users { get; }

    /// <summary>The current request scope id.</summary>
    public string ScopeId { get; }

    /// <summary>How many prop classes resolved within this scope.</summary>
    public int ResolvedInScope { get; }
}

/// <summary>
/// A prop class whose resolved value is a dictionary containing a nested invokable prop,
/// proving nested resolution keeps working through the DI path.
/// </summary>
public sealed class NestedProp : Dictionary<string, object?>
{
    /// <summary>Initializes a new instance of <see cref="NestedProp"/>.</summary>
    /// <param name="inertia">The Inertia service used to build the nested prop.</param>
    public NestedProp(IInertia inertia)
    {
        this["label"] = "nested";
        this["inner"] = inertia.Lazy(() => "inner-value");
    }
}
