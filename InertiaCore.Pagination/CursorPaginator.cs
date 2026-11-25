using System.Globalization;
using System.Reflection;
using InertiaCore;

namespace InertiaCore.Pagination;

internal sealed class CursorKey
{
    public CursorKey(string property, SortDirection direction, Type propertyType, Func<object, object?> accessor)
    {
        Property = property;
        Direction = direction;
        PropertyType = propertyType;
        Accessor = accessor;
    }

    public string Property { get; }
    public SortDirection Direction { get; }
    public Type PropertyType { get; }
    public Func<object, object?> Accessor { get; }
}

internal static class CursorPaginator
{
    internal static List<CursorKey> BuildKeys<T>(IReadOnlyList<OrderBy> orderBy)
    {
        if (orderBy == null || orderBy.Count == 0)
            throw new ArgumentException("At least one ordering key is required.", nameof(orderBy));

        var keys = new List<CursorKey>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in orderBy)
        {
            AddKey<T>(keys, seen, item.Property, item.Direction, nameof(orderBy));
            if (!string.IsNullOrWhiteSpace(item.TieBreakerProperty))
                AddKey<T>(keys, seen, item.TieBreakerProperty, SortDirection.Ascending, nameof(orderBy));
        }

        return keys;
    }

    internal static List<T> Order<T>(IEnumerable<T> source, List<CursorKey> keys)
    {
        IOrderedEnumerable<T>? ordered = null;
        foreach (var key in keys)
        {
            Func<T, object?> selector = item => key.Accessor(item!);
            ordered = ordered == null
                ? key.Direction == SortDirection.Ascending
                    ? source.OrderBy(selector, CursorComparer.Instance)
                    : source.OrderByDescending(selector, CursorComparer.Instance)
                : key.Direction == SortDirection.Ascending
                    ? ordered.ThenBy(selector, CursorComparer.Instance)
                    : ordered.ThenByDescending(selector, CursorComparer.Instance);
        }

        return ordered!.ToList();
    }

    internal static void EnsureUnique<T>(IReadOnlyList<T> ordered, List<CursorKey> keys)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in ordered)
        {
            if (!seen.Add(Fingerprint(row!, keys)))
            {
                throw new InvalidOperationException(
                    "Keyset pagination requires a unique ordering. Add a unique tie-breaker, such as the primary key, via OrderBy.TieBreakerProperty, and index (sort columns, tie-breaker) in that direction.");
            }
        }
    }

    internal static int CompareToBoundary(object row, List<CursorKey> keys, CursorToken cursor)
    {
        foreach (var key in keys)
        {
            if (!cursor.Values.TryGetValue(key.Property, out var raw))
                throw new InertiaCursorException();

            var value = key.Accessor(row);
            var boundary = Deserialize(raw, key.PropertyType);
            var comparison = CursorComparer.Instance.Compare(value, boundary);
            if (comparison == 0)
                continue;

            return key.Direction == SortDirection.Ascending ? comparison : -comparison;
        }

        return 0;
    }

    internal static CursorToken TokenFor(object row, List<CursorKey> keys, bool pointsToNext)
    {
        var token = new CursorToken { PointsToNext = pointsToNext };
        foreach (var key in keys)
            token.Values[key.Property] = Serialize(key.Accessor(row));
        return token;
    }

    private static void AddKey<T>(List<CursorKey> keys, HashSet<string> seen, string property, SortDirection direction, string paramName)
    {
        if (string.IsNullOrWhiteSpace(property))
            throw new ArgumentException("Ordering key property is required.", paramName);

        var prop = typeof(T).GetProperty(property, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"Ordering property '{property}' was not found on '{typeof(T).Name}'.", paramName);

        if (!seen.Add(prop.Name))
            return;

        keys.Add(new CursorKey(prop.Name, direction, prop.PropertyType, prop.GetValue));
    }

    private static string Fingerprint(object row, List<CursorKey> keys) =>
        string.Join('\u001f', keys.Select(key => Serialize(key.Accessor(row)) ?? "\0"));

    private static string? Serialize(object? value) => value switch
    {
        null => null,
        string text => text,
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString("D"),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static object? Deserialize(string? raw, Type propertyType)
    {
        if (raw == null)
            return null;

        var target = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (target == typeof(string))
            return raw;
        if (target == typeof(DateTime))
            return DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (target == typeof(DateTimeOffset))
            return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (target == typeof(Guid))
            return Guid.Parse(raw);
        if (target.IsEnum)
            return Enum.Parse(target, raw);

        return Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
    }

    internal sealed class CursorComparer : IComparer<object?>
    {
        public static readonly CursorComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;
            if (x is IComparable comparable && x.GetType().IsInstanceOfType(y))
                return comparable.CompareTo(y);
            if (y is IComparable other && y.GetType().IsInstanceOfType(x))
                return -other.CompareTo(x);
            return string.CompareOrdinal(x.ToString(), y.ToString());
        }
    }
}
