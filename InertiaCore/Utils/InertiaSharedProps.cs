using InertiaCore.Extensions;

namespace InertiaCore.Utils;

/// <summary>
/// Stores shared Inertia props per-request.
/// <para>
/// This class is deprecated. Use <c>InertiaCore.Services.InertiaState</c> instead.
/// </para>
/// </summary>
[Obsolete("Use InertiaCore.Services.InertiaState instead.")]
internal class InertiaSharedProps
{
    private IDictionary<string, object?>? Data { get; set; }

    public Dictionary<string, object?> GetMerged(IDictionary<string, object?> with)
    {
        var result = new Dictionary<string, object?>();

        if (Data != null)
            foreach (var (key, value) in Data)
                result[key.ToCamelCase()] = value;

        foreach (var (key, value) in with) result[key] = value;

        return result;
    }

    public void Merge(IDictionary<string, object?> with) => Data = GetMerged(with);

    public void Set(string key, object? value)
    {
        Data ??= new Dictionary<string, object?>();
        Data[key.ToCamelCase()] = value;
    }
}
