namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a schema version as an immutable major.minor pair.
/// Implements IComparable for version ordering.
/// </summary>
internal readonly struct SchemaVersion : IEquatable<SchemaVersion>, IComparable<SchemaVersion>
{
    public int Major { get; }
    public int Minor { get; }

    public SchemaVersion(int major, int minor)
    {
        Major = major;
        Minor = minor;
    }

    public int CompareTo(SchemaVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }

    public bool Equals(SchemaVersion other) => Major == other.Major && Minor == other.Minor;
    public override bool Equals(object obj) => obj is SchemaVersion other && Equals(other);
    public override int GetHashCode() => (Major * 397) ^ Minor;
    public override string ToString() => $"{Major}.{Minor}";

    public static bool operator <(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) >= 0;
    public static bool operator ==(SchemaVersion left, SchemaVersion right) => left.Equals(right);
    public static bool operator !=(SchemaVersion left, SchemaVersion right) => !left.Equals(right);
}
