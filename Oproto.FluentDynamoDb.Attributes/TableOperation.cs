using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Specifies DynamoDB table operations that can be generated for entity accessors.
/// This is a flags enumeration, allowing multiple operations to be combined.
/// </summary>
[Flags]
public enum TableOperation
{
    /// <summary>
    /// GetItem operation - retrieves a single item by its primary key.
    /// </summary>
    Get = 1,

    /// <summary>
    /// Query operation - retrieves items matching a partition key and optional sort key condition.
    /// </summary>
    Query = 2,

    /// <summary>
    /// Scan operation - retrieves all items in the table or index, optionally filtered.
    /// </summary>
    Scan = 4,

    /// <summary>
    /// PutItem operation - creates or replaces an item.
    /// </summary>
    Put = 8,

    /// <summary>
    /// DeleteItem operation - removes an item by its primary key.
    /// </summary>
    Delete = 16,

    /// <summary>
    /// UpdateItem operation - modifies attributes of an existing item.
    /// </summary>
    Update = 32,

    /// <summary>
    /// All operations - includes Get, Query, Scan, Put, Delete, and Update.
    /// </summary>
    All = Get | Query | Scan | Put | Delete | Update
}
