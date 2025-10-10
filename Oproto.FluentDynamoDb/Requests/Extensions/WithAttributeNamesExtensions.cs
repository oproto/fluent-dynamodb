using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for builders that implement IWithAttributeNames interface.
/// Provides fluent methods for adding attribute name mappings to DynamoDB expressions.
/// 
/// <para><strong>Migration Guide:</strong></para>
/// <para>These extension methods replace the interface methods that were previously implemented on each builder class.
/// No code changes are required - the same method signatures and behavior are preserved.</para>
/// 
/// <para><strong>When to Use Attribute Names:</strong></para>
/// <para>Attribute name mappings are essential when your table attributes conflict with DynamoDB reserved words
/// or when attribute names contain special characters. Common reserved words include: name, status, type, order, etc.</para>
/// 
/// <para><strong>Usage Examples:</strong></para>
/// <code>
/// // Handle reserved word conflicts
/// builder.WithAttribute("#name", "name")
///        .WithAttribute("#status", "status")
///        .Where("#name = :name AND #status = :status")
///        .WithValue(":name", "John Doe")
///        .WithValue(":status", "ACTIVE");
/// 
/// // Bulk attribute name mapping
/// builder.WithAttributes(new Dictionary&lt;string, string&gt;
/// {
///     ["#pk"] = "partition_key",
///     ["#sk"] = "sort_key", 
///     ["#data"] = "user_data"
/// });
/// </code>
/// </summary>
public static class WithAttributeNamesExtensions
{
    /// <summary>
    /// Adds multiple attribute name mappings for use in expressions.
    /// This is essential when attribute names conflict with DynamoDB reserved words.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeNames.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeNames">A dictionary mapping parameter names to actual attribute names.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static T WithAttributes<T>(this IWithAttributeNames<T> builder, Dictionary<string, string> attributeNames)
    {
        builder.GetAttributeNameHelper().WithAttributes(attributeNames);
        return builder.Self;
    }
    
    /// <summary>
    /// Adds multiple attribute name mappings using a configuration action.
    /// This is essential when attribute names conflict with DynamoDB reserved words.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeNames.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="attributeNameFunc">An action that configures the attribute name mappings.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static T WithAttributes<T>(this IWithAttributeNames<T> builder, Action<Dictionary<string, string>> attributeNameFunc)
    {
        builder.GetAttributeNameHelper().WithAttributes(attributeNameFunc);
        return builder.Self;
    }

    /// <summary>
    /// Adds a single attribute name mapping for use in expressions.
    /// This is essential when attribute names conflict with DynamoDB reserved words.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithAttributeNames.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="parameterName">The parameter name to use in expressions (e.g., "#name").</param>
    /// <param name="attributeName">The actual attribute name in the table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Common reserved word conflicts
    /// builder.WithAttribute("#name", "name")           // 'name' is reserved
    ///        .WithAttribute("#status", "status")       // 'status' is reserved  
    ///        .WithAttribute("#type", "type")           // 'type' is reserved
    ///        .WithAttribute("#order", "order")         // 'order' is reserved
    ///        .Where("#name = :name AND #status = :status");
    /// 
    /// // Special characters in attribute names
    /// builder.WithAttribute("#email", "user-email")    // Hyphen in name
    ///        .WithAttribute("#data", "user.data")      // Dot in name
    ///        .Where("#email = :email AND attribute_exists(#data)");
    /// 
    /// // Nested attribute access
    /// builder.WithAttribute("#addr", "address")
    ///        .WithAttribute("#city", "city")
    ///        .Where("#addr.#city = :city");
    /// </code>
    /// </example>
    public static T WithAttribute<T>(this IWithAttributeNames<T> builder, string parameterName, string attributeName)
    {
        builder.GetAttributeNameHelper().WithAttribute(parameterName, attributeName);
        return builder.Self;
    }
}