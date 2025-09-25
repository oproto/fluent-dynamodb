using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface extending IDynamoDbTable with scan operations.
/// Scan operations are intentionally separated to discourage accidental usage.
/// </summary>
public interface IScannableDynamoDbTable : IDynamoDbTable
{
    /// <summary>
    /// Scan request builder. Use with caution as scan operations can be expensive.
    /// </summary>
    ScanRequestBuilder Scan { get; }
    
    /// <summary>
    /// Access to the underlying table instance for custom properties and methods
    /// </summary>
    DynamoDbTableBase UnderlyingTable { get; }
}