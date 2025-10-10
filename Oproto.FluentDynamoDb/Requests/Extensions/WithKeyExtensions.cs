using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for builders that implement IWithKey interface.
/// Provides fluent methods for specifying key values in DynamoDB operations.
/// 
/// <para><strong>Migration Guide:</strong></para>
/// <para>These extension methods replace the interface methods that were previously implemented on each builder class.
/// No code changes are required - the same method signatures and behavior are preserved.</para>
/// 
/// <para><strong>Key Types Supported:</strong></para>
/// <list type="bullet">
/// <item>Simple primary key (partition key only)</item>
/// <item>Composite primary key (partition key + sort key)</item>
/// <item>String keys (automatically converted to DynamoDB string type)</item>
/// <item>AttributeValue keys (full control over type and value)</item>
/// </list>
/// 
/// <para><strong>Usage Examples:</strong></para>
/// <code>
/// // Simple string key
/// builder.WithKey("userId", "USER#123");
/// 
/// // Composite string keys  
/// builder.WithKey("pk", "USER#123", "sk", "PROFILE");
/// 
/// // Mixed types with AttributeValue
/// builder.WithKey("pk", new AttributeValue { S = "USER#123" }, 
///                  "timestamp", new AttributeValue { N = "1642694400" });
/// </code>
/// </summary>
public static class WithKeyExtensions
{
    /// <summary>
    /// Specifies the primary key of the item using AttributeValue objects.
    /// This method provides full control over the key specification and supports both simple and composite keys.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithKey.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="primaryKeyName">The name of the primary key attribute.</param>
    /// <param name="primaryKeyValue">The value of the primary key attribute.</param>
    /// <param name="sortKeyName">The name of the sort key attribute (optional for tables with composite keys).</param>
    /// <param name="sortKeyValue">The value of the sort key attribute (optional for tables with composite keys).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple primary key (partition key only)
    /// builder.WithKey("userId", new AttributeValue { S = "USER#123" });
    /// 
    /// // Composite primary key (partition + sort key)
    /// builder.WithKey("pk", new AttributeValue { S = "USER#123" },
    ///                  "sk", new AttributeValue { S = "PROFILE" });
    /// 
    /// // Numeric keys
    /// builder.WithKey("id", new AttributeValue { N = "12345" },
    ///                  "timestamp", new AttributeValue { N = "1642694400" });
    /// 
    /// // Binary keys
    /// builder.WithKey("binaryId", new AttributeValue { B = MemoryStream.From(bytes) });
    /// 
    /// // Only partition key for simple tables
    /// builder.WithKey("simpleKey", new AttributeValue { S = "VALUE" });
    /// </code>
    /// </example>
    public static T WithKey<T>(this IWithKey<T> builder, string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName = null, AttributeValue? sortKeyValue = null)
    {
        return builder.SetKey(keyDict =>
        {
            keyDict[primaryKeyName] = primaryKeyValue;
            if (sortKeyName != null && sortKeyValue != null)
            {
                keyDict[sortKeyName] = sortKeyValue;
            }
        });
    }

    /// <summary>
    /// Specifies a single key attribute using string values (automatically converted to DynamoDB string type).
    /// This is a convenience method for simple string-based keys.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithKey.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="keyName">The name of the key attribute.</param>
    /// <param name="keyValue">The string value of the key attribute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static T WithKey<T>(this IWithKey<T> builder, string keyName, string keyValue)
    {
        return builder.SetKey(keyDict =>
        {
            keyDict[keyName] = new AttributeValue { S = keyValue };
        });
    }
    
    /// <summary>
    /// Specifies both primary key and sort key using string values (automatically converted to DynamoDB string type).
    /// Use this method for tables with composite primary keys where both keys are strings.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithKey.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="primaryKeyName">The name of the primary key attribute.</param>
    /// <param name="primaryKeyValue">The string value of the primary key attribute.</param>
    /// <param name="sortKeyName">The name of the sort key attribute.</param>
    /// <param name="sortKeyValue">The string value of the sort key attribute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static T WithKey<T>(this IWithKey<T> builder, string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        return builder.SetKey(keyDict =>
        {
            keyDict[primaryKeyName] = new AttributeValue { S = primaryKeyValue };
            keyDict[sortKeyName] = new AttributeValue { S = sortKeyValue };
        });
    }
}