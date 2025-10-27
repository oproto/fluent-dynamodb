namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Provides thread-safe ambient context for DynamoDB operation metadata.
/// Uses AsyncLocal to ensure context flows through async calls without leaking across threads.
/// Context is automatically populated by Primary API methods (GetItemAsync, ToListAsync, PutAsync, etc.)
/// and accessible after operations complete.
/// </summary>
/// <example>
/// <code>
/// // Execute operation (context is populated automatically)
/// var items = await table.Query&lt;Transaction&gt;()
///     .Where("pk = :pk")
///     .WithValue(":pk", "USER#123")
///     .ReturnTotalConsumedCapacity()
///     .ToListAsync();
/// 
/// // Access metadata via context
/// var context = DynamoDbOperationContext.Current;
/// if (context?.ConsumedCapacity != null)
/// {
///     _logger.LogInformation(
///         "Query consumed {Capacity} RCUs",
///         context.ConsumedCapacity.CapacityUnits);
/// }
/// </code>
/// </example>
public static class DynamoDbOperationContext
{
    private static readonly AsyncLocal<OperationContextData?> _current = new();

    /// <summary>
    /// Gets the current operation context data, or null if no operation has executed.
    /// Context is automatically populated by Primary API methods and contains metadata
    /// from the most recently completed operation in the current async flow.
    /// </summary>
    public static OperationContextData? Current
    {
        get => _current.Value;
        internal set => _current.Value = value;
    }

    /// <summary>
    /// Clears the current operation context.
    /// This is typically not needed as context is automatically replaced on each operation.
    /// </summary>
    public static void Clear()
    {
        _current.Value = null;
    }

    /// <summary>
    /// Gets or sets the encryption context identifier for the current async flow.
    /// This is a convenience accessor that delegates to the unified context.
    /// Used for field-level encryption to associate encrypted data with a specific context (e.g., tenant ID).
    /// </summary>
    /// <example>
    /// <code>
    /// // Set encryption context before operations
    /// DynamoDbOperationContext.EncryptionContextId = "tenant-123";
    /// 
    /// // Execute operations - encrypted fields will use this context
    /// await table.Put&lt;SecureEntity&gt;()
    ///     .WithItem(entity)
    ///     .PutAsync();
    /// </code>
    /// </example>
    public static string? EncryptionContextId
    {
        get => _current.Value?.EncryptionContextId;
        set
        {
            var data = _current.Value ?? new OperationContextData();
            data.EncryptionContextId = value;
            _current.Value = data;
        }
    }
}
