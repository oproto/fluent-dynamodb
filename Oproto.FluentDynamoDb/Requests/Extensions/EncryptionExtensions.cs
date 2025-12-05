using Oproto.FluentDynamoDb.Context;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for adding encryption context to request builders.
/// Encryption context is used by IFieldEncryptor implementations to determine
/// which encryption key to use for field-level encryption.
/// </summary>
/// <remarks>
/// <para>
/// The encryption context (e.g., tenant ID, customer ID, region) is passed to
/// IKmsKeyResolver at runtime to determine the appropriate encryption key.
/// The context identifier is NOT the encryption key itself.
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// // Option 1: Per-operation context (recommended - most explicit)
/// await userTable.PutItem(user)
///     .WithEncryptionContext("tenant-123")
///     .ExecuteAsync();
/// 
/// // Option 2: Ambient context (for middleware scenarios)
/// DynamoDbOperationContext.EncryptionContextId = "tenant-123";
/// await userTable.PutItem(user).ExecuteAsync();
/// </code>
/// </para>
/// <para>
/// When both per-operation and ambient context are set, the per-operation
/// context takes precedence.
/// </para>
/// </remarks>
public static class EncryptionExtensions
{
    // Thread-local storage for per-operation encryption context
    // This allows the context to flow through the request builder chain
    // without modifying the builder classes themselves
    private static readonly AsyncLocal<string?> _operationContext = new();

    /// <summary>
    /// Sets the encryption context for a PutItem operation.
    /// This context is used to determine which encryption key to use for encrypted fields.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being put.</typeparam>
    /// <param name="builder">The PutItemRequestBuilder instance.</param>
    /// <param name="context">The encryption context identifier (e.g., tenant ID, customer ID).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// await table.Put&lt;MyEntity&gt;()
    ///     .WithItem(item)
    ///     .WithEncryptionContext("tenant-123")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static PutItemRequestBuilder<TEntity> WithEncryptionContext<TEntity>(
        this PutItemRequestBuilder<TEntity> builder,
        string context)
        where TEntity : class
    {
        _operationContext.Value = context;
        return builder;
    }

    /// <summary>
    /// Sets the encryption context for a GetItem operation.
    /// This context is used to determine which encryption key to use for decrypting encrypted fields.
    /// </summary>
    /// <param name="builder">The GetItemRequestBuilder instance.</param>
    /// <param name="context">The encryption context identifier (e.g., tenant ID, customer ID).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// await table.GetItem()
    ///     .WithKey("id", "123")
    ///     .WithEncryptionContext("tenant-123")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static GetItemRequestBuilder<TEntity> WithEncryptionContext<TEntity>(
        this GetItemRequestBuilder<TEntity> builder,
        string context)
        where TEntity : class
    {
        _operationContext.Value = context;
        return builder;
    }

    /// <summary>
    /// Sets the encryption context for a Query operation.
    /// This context is used to determine which encryption key to use for decrypting encrypted fields.
    /// </summary>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="context">The encryption context identifier (e.g., tenant ID, customer ID).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// await table.Query()
    ///     .Where("pk = :pk")
    ///     .WithValue(":pk", "USER#123")
    ///     .WithEncryptionContext("tenant-123")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static QueryRequestBuilder<TEntity> WithEncryptionContext<TEntity>(
        this QueryRequestBuilder<TEntity> builder,
        string context)
        where TEntity : class
    {
        _operationContext.Value = context;
        return builder;
    }

    /// <summary>
    /// Sets the encryption context for an UpdateItem operation.
    /// This context is used to determine which encryption key to use for encrypted fields.
    /// </summary>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
    /// <param name="context">The encryption context identifier (e.g., tenant ID, customer ID).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// await table.UpdateItem()
    ///     .WithKey("id", "123")
    ///     .Set("SET #name = :name")
    ///     .WithValue(":name", "John")
    ///     .WithEncryptionContext("tenant-123")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static UpdateItemRequestBuilder<TEntity> WithEncryptionContext<TEntity>(
        this UpdateItemRequestBuilder<TEntity> builder,
        string context)
        where TEntity : class
    {
        _operationContext.Value = context;
        return builder;
    }

    /// <summary>
    /// Sets the encryption context for a DeleteItem operation.
    /// This context is used to determine which encryption key to use for decrypting encrypted fields
    /// when returning old values.
    /// </summary>
    /// <param name="builder">The DeleteItemRequestBuilder instance.</param>
    /// <param name="context">The encryption context identifier (e.g., tenant ID, customer ID).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// await table.DeleteItem()
    ///     .WithKey("id", "123")
    ///     .WithEncryptionContext("tenant-123")
    ///     .ReturnAllOldValues()
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static DeleteItemRequestBuilder<TEntity> WithEncryptionContext<TEntity>(
        this DeleteItemRequestBuilder<TEntity> builder,
        string context)
        where TEntity : class
    {
        _operationContext.Value = context;
        return builder;
    }

    /// <summary>
    /// Sets the encryption context for a Scan operation.
    /// This context is used to determine which encryption key to use for decrypting encrypted fields.
    /// </summary>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="context">The encryption context identifier (e.g., tenant ID, customer ID).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// await table.AsScannable().Scan()
    ///     .WithFilter("#status = :active")
    ///     .WithValue(":active", "ACTIVE")
    ///     .WithEncryptionContext("tenant-123")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static ScanRequestBuilder<TEntity> WithEncryptionContext<TEntity>(
        this ScanRequestBuilder<TEntity> builder,
        string context)
        where TEntity : class
    {
        _operationContext.Value = context;
        return builder;
    }



    /// <summary>
    /// Gets the current operation-specific encryption context.
    /// This is used internally by the library to retrieve the context set via WithEncryptionContext.
    /// </summary>
    /// <returns>The operation-specific encryption context, or null if not set.</returns>
    internal static string? GetOperationContext()
    {
        return _operationContext.Value;
    }

    /// <summary>
    /// Clears the operation-specific encryption context.
    /// This is used internally by the library after an operation completes.
    /// </summary>
    internal static void ClearOperationContext()
    {
        _operationContext.Value = null;
    }
}
