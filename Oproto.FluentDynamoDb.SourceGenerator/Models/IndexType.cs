namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Type of DynamoDB secondary index.
/// </summary>
internal enum IndexType
{
    /// <summary>
    /// Global Secondary Index - can have different partition and sort keys from the base table.
    /// </summary>
    GlobalSecondaryIndex,

    /// <summary>
    /// Local Secondary Index - shares partition key with base table but has a different sort key.
    /// </summary>
    LocalSecondaryIndex
}
