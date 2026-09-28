namespace InertiaCore.Props;

/// <summary>
/// A prop whose value is merged with the existing client-side value during partial
/// reloads instead of replacing it. Created via <c>Inertia.Merge(...)</c> or
/// <c>Inertia.DeepMerge(...)</c>.
/// See: https://inertiajs.com/merging-props
/// </summary>
public class MergeProp : InvokableProp
{
    internal MergeProp(object? value) : base(value) => Merge();

    internal MergeProp(Func<object?> value) : base(value) => Merge();

    internal MergeProp(Func<Task<object?>> value) : base(value) => Merge();
}
