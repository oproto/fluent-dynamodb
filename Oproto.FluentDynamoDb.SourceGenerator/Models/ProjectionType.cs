namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// DynamoDB index projection type.
/// This is a source generator copy of Oproto.FluentDynamoDb.Metadata.ProjectionType.
/// </summary>
internal enum ProjectionType
{
    /// <summary>
    /// All attributes are projected into the index.
    /// </summary>
    All,

    /// <summary>
    /// Only key attributes are projected into the index.
    /// </summary>
    KeysOnly,

    /// <summary>
    /// Specific non-key attributes are projected into the index.
    /// </summary>
    Include
}
