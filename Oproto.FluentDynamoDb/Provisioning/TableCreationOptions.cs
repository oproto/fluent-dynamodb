using Amazon.DynamoDBv2;

namespace Oproto.FluentDynamoDb.Provisioning;

/// <summary>
/// Options for table creation behavior.
/// </summary>
public class TableCreationOptions
{
    /// <summary>
    /// Gets or sets the billing mode. Default is PAY_PER_REQUEST.
    /// </summary>
    public BillingMode BillingMode { get; set; } = BillingMode.PAY_PER_REQUEST;
    
    /// <summary>
    /// Gets or sets the provisioned throughput for the table.
    /// Only used when BillingMode is PROVISIONED.
    /// </summary>
    public ProvisionedThroughputConfig? ProvisionedThroughput { get; set; }
    
    /// <summary>
    /// Gets or sets the provisioned throughput for GSIs.
    /// Only used when BillingMode is PROVISIONED.
    /// If not specified, uses the same values as the table.
    /// </summary>
    public ProvisionedThroughputConfig? GsiProvisionedThroughput { get; set; }
    
    /// <summary>
    /// Gets or sets whether to enable TTL if the entity metadata defines a TTL attribute.
    /// Default is false.
    /// </summary>
    public bool EnableTtl { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether to wait for the table to become ACTIVE before returning.
    /// Default is true.
    /// </summary>
    public bool WaitForActive { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the timeout for waiting for the table to become ACTIVE.
    /// Default is 60 seconds.
    /// </summary>
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(60);
    
    /// <summary>
    /// Gets or sets the polling interval when waiting for the table to become ACTIVE.
    /// Default is 1 second.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Provisioned throughput configuration for DynamoDB tables and indexes.
/// </summary>
public class ProvisionedThroughputConfig
{
    /// <summary>
    /// Gets or sets the read capacity units.
    /// </summary>
    public long ReadCapacityUnits { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets the write capacity units.
    /// </summary>
    public long WriteCapacityUnits { get; set; } = 5;
}
