using Amazon.DynamoDBv2;

namespace Oproto.FluentDynamoDb.Provisioning;

/// <summary>
/// Result of a table creation operation.
/// </summary>
public class TableCreationResult
{
    /// <summary>
    /// Gets the name of the created table.
    /// </summary>
    public string TableName { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the ARN of the created table.
    /// </summary>
    public string TableArn { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the current status of the table.
    /// </summary>
    public TableStatus TableStatus { get; init; } = TableStatus.CREATING;
    
    /// <summary>
    /// Gets whether TTL was enabled on the table.
    /// </summary>
    public bool TtlEnabled { get; init; }
}
