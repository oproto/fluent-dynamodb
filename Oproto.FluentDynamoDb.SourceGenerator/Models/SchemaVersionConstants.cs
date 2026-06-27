namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Defines the schema versions the current generator supports.
/// Update these when introducing breaking changes to generated code shapes.
/// </summary>
internal static class SchemaVersionConstants
{
    /// <summary>The latest schema version this generator can emit.</summary>
    public static readonly SchemaVersion Current = new(1, 0);

    /// <summary>The oldest schema version this generator still supports.</summary>
    public static readonly SchemaVersion MinimumSupported = new(1, 0);

    /// <summary>The default version assumed when no attribute is declared.</summary>
    public static readonly SchemaVersion Default = new(1, 0);

    /// <summary>URL for the schema version migration guide.</summary>
    public const string MigrationGuideUrl = "https://fluentdynamodb.dev/guides/schema-migration";

    /// <summary>URL for the schema version upgrade guide.</summary>
    public const string UpgradeGuideUrl = "https://fluentdynamodb.dev/guides/schema-upgrade";
}
