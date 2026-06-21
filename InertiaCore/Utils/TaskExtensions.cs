namespace InertiaCore.Utils;

/// <summary>
/// Safe task unwrapping utilities for resolving Inertia props.
/// </summary>
internal static class TaskExtensions
{
    /// <summary>
    /// Safely awaits a task and returns its result as <see cref="object?"/>,
    /// or <see langword="null"/> if the task has no result.
    /// </summary>
    /// <remarks>
    /// This method awaits the task first, then reads <c>Result</c> via reflection.
    /// Because the task has already completed, the reflection call is safe and
    /// will not block. This is the single acceptable use of reflection in the
    /// library for task unwrapping.
    /// </remarks>
    internal static async Task<object?> UnwrapResultAsync(this Task task)
    {
        await task.ConfigureAwait(false);

        Type taskType = task.GetType();

        // Non-generic Task has no result value.
        if (!taskType.IsGenericType)
            return null;

        // Use GetProperty("Result") rather than GetGenericTypeDefinition() == typeof(Task<>)
        // to handle compiler-generated async state machine types that inherit Task{T}
        // but fail the generic type definition equality check.
        var resultProperty = taskType.GetProperty("Result");

        if (resultProperty is null)
            return null;

        // Guard: if the Result property's type is VoidTaskResult (an internal struct used
        // by AsyncTaskMethodBuilder for non-generic Task completion), return null since
        // there is no meaningful result value.
        if (resultProperty.PropertyType.Name == "VoidTaskResult")
            return null;

        // Safe: task has already been awaited, so Result will not block.
        return resultProperty.GetValue(task);
    }
}
