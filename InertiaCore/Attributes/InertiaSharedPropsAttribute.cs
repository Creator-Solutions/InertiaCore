namespace InertiaCore;

/// <summary>
/// Marks a record/class as the shape of the application's shared props so that
/// <c>InertiaCore.TypeGen</c> can emit a dedicated <c>InertiaSharedProps</c> TypeScript
/// interface for it.
/// <para>
/// This attribute is a code-generation marker only. It does not register or share any
/// values at runtime; continue to use <c>Inertia.Share(...)</c> to populate shared props.
/// At most one type in an assembly should be annotated.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class InertiaSharedPropsAttribute : Attribute
{
}
