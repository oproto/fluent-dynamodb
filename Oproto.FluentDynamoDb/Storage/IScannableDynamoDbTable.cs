using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface extending IDynamoDbTable with scan operations.
/// 
/// Scan operations are intentionally separated from the core table interface to implement
/// a friction pattern that discourages accidental usage. Scan operations read every item
/// in a table or index and can be very expensive in terms of consumed capacity and latency.
/// 
/// Use scan operations only for legitimate use cases such as:
/// - Data migration or ETL processes
/// - Analytics on small tables
/// - Operations where you truly need to examine every item
/// 
/// For most use cases, Query operations are more efficient and should be preferred.
/// </summary>
public interface IScannableDynamoDbTable : IDynamoDbTable
{
    /// <summary>
    /// Gets a builder for Scan operations to examine every item in the table or index.
    /// 
    /// WARNING: Use with caution as scan operations can be expensive.
    /// - Scan operations consume read capacity for every item examined, not just returned items
    /// - Large tables will require multiple scan operations due to 1MB response limits
    /// - Consider using Query operations instead whenever possible
    /// </summary>
    ScanRequestBuilder Scan { get; }
    
    /// <summary>
    /// Gets access to the underlying table instance, allowing access to custom properties and methods
    /// that may have been defined in your table class implementation.
    /// </summary>
    DynamoDbTableBase UnderlyingTable { get; }
}