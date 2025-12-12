namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// DynamoDB index projection type.
/// </summary>
public enum ProjectionType
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
