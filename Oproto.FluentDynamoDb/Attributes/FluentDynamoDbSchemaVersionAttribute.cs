using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Declares the schema version of generated code that this assembly targets.
/// The source generator uses this to determine which code shape to emit.
/// </summary>
/// <remarks>
/// Schema versions are independent of NuGet package versions. Multiple package
/// versions may support the same schema version. Bump the schema version only
/// when you're ready to adopt new generated code shapes.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class FluentDynamoDbSchemaVersionAttribute : Attribute
{
    /// <summary>Gets the major schema version component.</summary>
    public int Major { get; }

    /// <summary>Gets the minor schema version component.</summary>
    public int Minor { get; }

    /// <summary>
    /// Initializes a new instance targeting the specified schema version.
    /// </summary>
    /// <param name="major">Major version (must be >= 1).</param>
    /// <param name="minor">Minor version (must be >= 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="major"/> is less than 1 or
    /// <paramref name="minor"/> is less than 0.
    /// </exception>
    public FluentDynamoDbSchemaVersionAttribute(int major, int minor)
    {
        if (major < 1)
            throw new ArgumentOutOfRangeException(nameof(major), major, "Major version must be at least 1.");
        if (minor < 0)
            throw new ArgumentOutOfRangeException(nameof(minor), minor, "Minor version must be at least 0.");

        Major = major;
        Minor = minor;
    }
}
