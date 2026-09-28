using System;

namespace InertiaCore.SourceGenerators.Models;

internal sealed class PageInfo : IEquatable<PageInfo>
{
    public string ComponentName { get; }
    public string Identifier { get; }
    public string ContainingType { get; }
    public LocationInfo Location { get; }

    public PageInfo(string componentName, string identifier, string containingType, LocationInfo location)
    {
        ComponentName = componentName;
        Identifier = identifier;
        ContainingType = containingType;
        Location = location;
    }

    public bool Equals(PageInfo? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return ComponentName == other.ComponentName
            && Identifier == other.Identifier
            && ContainingType == other.ContainingType
            && Location == other.Location;
    }

    public override bool Equals(object? obj) => Equals(obj as PageInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (ComponentName?.GetHashCode() ?? 0);
            hash = hash * 31 + (Identifier?.GetHashCode() ?? 0);
            hash = hash * 31 + (ContainingType?.GetHashCode() ?? 0);
            hash = hash * 31 + (Location?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal sealed class LocationInfo : IEquatable<LocationInfo>
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }

    public LocationInfo(string filePath, int line, int column)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
    }

    public bool Equals(LocationInfo? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return FilePath == other.FilePath
            && Line == other.Line
            && Column == other.Column;
    }

    public override bool Equals(object? obj) => Equals(obj as LocationInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (FilePath?.GetHashCode() ?? 0);
            hash = hash * 31 + Line;
            hash = hash * 31 + Column;
            return hash;
        }
    }
}
