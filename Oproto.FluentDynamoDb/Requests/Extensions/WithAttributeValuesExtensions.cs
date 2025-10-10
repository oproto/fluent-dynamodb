using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for builders that implement IWithAttributeValues interface.
/// Provides fluent methods for adding attribute values to DynamoDB expressions.
/// 
/// <para><strong>Migration Guide:</strong></para>
/// <para>These extension methods replace the interface methods that were previously implemented on each builder class.
/// No code changes are required - the same method signatures and behavior are preserved.
/// Simply ensure you have the appropriate using statement: <c>using Oproto.FluentDynamoDb.Requests.Extensions;</c></para>
/// 
/// <para><strong>Usage Examples:</strong></para>
/// <code>
/// // Basic usage - same as before
/// builder.WithValue(":userId", "USER#123")
///        .WithValue(":active", true)
///        .WithValue(":amount", 99.99m);
/// 
/// // Bulk operations
/// builder.WithValues(new Dictionary&lt;string, AttributeValue&gt; 
/// {
///     [":userId"] = new AttributeValue { S = "USER#123" },
///     [":status"] = new AttributeValue { S = "ACTIVE" }
/// });
/// 
/// // Configuration action approach
/// builder.WithValues(values => 
/// {
///     values[":userId"] = new AttributeValue { S = "USER#123" };
///     values[":timestamp"] = new AttributeValue { N = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() };
/// });
/// </code>
/// </summary>
public static class WithAttributeValuesExtensions
{
    /// <summary>
    /// Adds multiple attribute values for use in expressions.
    /// These values are referenced in expressions using parameter names (e.g., ":value").
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeValues.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeValues">A dictionary mapping parameter names to AttributeValue objects.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Define multiple values at once
    /// var values = new Dictionary&lt;string, AttributeValue&gt;
    /// {
    ///     [":pk"] = new AttributeValue { S = "USER#123" },
    ///     [":sk"] = new AttributeValue { S = "PROFILE" },
    ///     [":active"] = new AttributeValue { BOOL = true },
    ///     [":score"] = new AttributeValue { N = "95.5" }
    /// };
    /// 
    /// builder.WithValues(values)
    ///        .Where("pk = :pk AND sk = :sk AND active = :active AND score > :score");
    /// </code>
    /// </example>
    public static T WithValues<T>(this IWithAttributeValues<T> builder, Dictionary<string, AttributeValue> attributeValues)
    {
        builder.GetAttributeValueHelper().WithValues(attributeValues);
        return builder.Self;
    }
    
    /// <summary>
    /// Adds multiple attribute values using a configuration action.
    /// These values are referenced in expressions using parameter names (e.g., ":value").
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeValues.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeValueFunc">An action that configures the attribute value mappings.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Configure values using an action - useful for conditional logic
    /// builder.WithValues(values => 
    /// {
    ///     values[":pk"] = new AttributeValue { S = "USER#123" };
    ///     
    ///     if (includeTimestamp)
    ///         values[":timestamp"] = new AttributeValue { N = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() };
    ///     
    ///     if (filterByStatus)
    ///         values[":status"] = new AttributeValue { S = "ACTIVE" };
    /// });
    /// </code>
    /// </example>
    public static T WithValues<T>(this IWithAttributeValues<T> builder, Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        builder.GetAttributeValueHelper().WithValues(attributeValueFunc);
        return builder.Self;
    }
    
    /// <summary>
    /// Adds a string attribute value for use in expressions.
    /// The value is automatically converted to a DynamoDB string type.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeValues.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeName">The parameter name to use in expressions (e.g., ":value").</param>
    /// <param name="attributeValue">The string value to associate with the parameter.</param>
    /// <param name="conditionalUse">If false, the value is not added when null. Defaults to true.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Basic string value
    /// builder.WithValue(":userId", "USER#123")
    ///        .Where("pk = :userId");
    /// 
    /// // Conditional usage - value not added if null
    /// builder.WithValue(":optionalField", nullableString, conditionalUse: false);
    /// 
    /// // Multiple string values
    /// builder.WithValue(":pk", "USER#123")
    ///        .WithValue(":sk", "PROFILE")
    ///        .WithValue(":name", "John Doe");
    /// </code>
    /// </example>
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        builder.GetAttributeValueHelper().WithValue(attributeName, attributeValue, conditionalUse);
        return builder.Self;
    }
    
    /// <summary>
    /// Adds a boolean attribute value for use in expressions.
    /// The value is automatically converted to a DynamoDB boolean type.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeValues.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeName">The parameter name to use in expressions (e.g., ":active").</param>
    /// <param name="attributeValue">The boolean value to associate with the parameter.</param>
    /// <param name="conditionalUse">If false, the value is not added when null. Defaults to true.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        builder.GetAttributeValueHelper().WithValue(attributeName, attributeValue, conditionalUse);
        return builder.Self;
    }
    
    /// <summary>
    /// Adds a numeric attribute value for use in expressions.
    /// The value is automatically converted to a DynamoDB number type.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeValues.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeName">The parameter name to use in expressions (e.g., ":amount").</param>
    /// <param name="attributeValue">The decimal value to associate with the parameter.</param>
    /// <param name="conditionalUse">If false, the value is not added when null. Defaults to true.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        builder.GetAttributeValueHelper().WithValue(attributeName, attributeValue, conditionalUse);
        return builder.Self;
    }
    
    /// <summary>
    /// Adds a map attribute value (string dictionary) for use in expressions.
    /// The dictionary is automatically converted to a DynamoDB map type with string values.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeValues.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeName">The parameter name to use in expressions (e.g., ":metadata").</param>
    /// <param name="attributeValue">The string dictionary to associate with the parameter.</param>
    /// <param name="conditionalUse">If false, the value is not added when null. Defaults to true.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, Dictionary<string, string> attributeValue, bool conditionalUse = true)
    {
        builder.GetAttributeValueHelper().WithValue(attributeName, attributeValue, conditionalUse);
        return builder.Self;
    }
    
    /// <summary>
    /// Adds a map attribute value (AttributeValue dictionary) for use in expressions.
    /// This provides full control over the DynamoDB map structure and types.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeValues.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeName">The parameter name to use in expressions (e.g., ":complex").</param>
    /// <param name="attributeValue">The AttributeValue dictionary to associate with the parameter.</param>
    /// <param name="conditionalUse">If false, the value is not added when null. Defaults to true.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        builder.GetAttributeValueHelper().WithValue(attributeName, attributeValue, conditionalUse);
        return builder.Self;
    }
}