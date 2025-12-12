using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Provides expression-time access to dynamic fields for use in filter, condition, and update expressions.
/// This type is only used in expression trees and is never instantiated at runtime.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="DynamicFieldAccessor"/> class enables type-safe access to dynamic fields in lambda expressions.
/// It is designed to be analyzed by the expression translator and converted into DynamoDB expression syntax.
/// </para>
/// <para>
/// <strong>Usage Examples:</strong>
/// </para>
/// <code>
/// // Filter by dynamic field value
/// table.Query().WithFilter(x => x.DynamicFields["customField"] == "value");
/// 
/// // Check if dynamic field exists
/// table.Query().WithFilter(x => x.DynamicFields.Exists("customField"));
/// 
/// // Check if dynamic field does not exist
/// table.Query().WithFilter(x => x.DynamicFields.NotExists("customField"));
/// 
/// // Comparison operators
/// table.Query().WithFilter(x => x.DynamicFields["score"] > 100);
/// 
/// // String functions
/// table.Query().WithFilter(x => x.DynamicFields["name"].StartsWith("John"));
/// </code>
/// <para>
/// <strong>Important:</strong> Methods on this class should never be called directly at runtime.
/// They are designed to be analyzed by the expression translator and will throw exceptions if invoked.
/// </para>
/// </remarks>
public sealed class DynamicFieldAccessor
{
    /// <summary>
    /// Accesses a dynamic field by name. Used in expressions like: x.DynamicFields["customField"] == "value"
    /// </summary>
    /// <param name="fieldName">The name of the dynamic field to access.</param>
    /// <returns>The AttributeValue of the field (for expression analysis only).</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is for expression analysis only.</exception>
    /// <remarks>
    /// This indexer is designed to be used in lambda expressions for filter, condition, and update expressions.
    /// The expression translator will convert this access pattern into the appropriate DynamoDB expression syntax.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Equality comparison
    /// x => x.DynamicFields["status"] == "active"
    /// // Generates: #dynField0 = :p0
    /// 
    /// // Comparison operators
    /// x => x.DynamicFields["score"] > 100
    /// // Generates: #dynField0 > :p0
    /// </code>
    /// </example>
    public AttributeValue this[string fieldName]
    {
        get => throw new InvalidOperationException(
            $"DynamicFieldAccessor indexer cannot be called directly. " +
            $"It is only valid within expression trees for filter, condition, or update expressions. " +
            $"Example: table.Query().WithFilter(x => x.DynamicFields[\"{fieldName}\"] == \"value\")");
    }

    /// <summary>
    /// Checks if a dynamic field exists. Used in expressions like: x.DynamicFields.Exists("customField")
    /// </summary>
    /// <param name="fieldName">The name of the dynamic field to check.</param>
    /// <returns>True if the field exists (for expression analysis only).</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is for expression analysis only.</exception>
    /// <remarks>
    /// This method is translated to the DynamoDB <c>attribute_exists()</c> function.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check if field exists
    /// x => x.DynamicFields.Exists("optionalField")
    /// // Generates: attribute_exists(#dynField0)
    /// </code>
    /// </example>
    [ExpressionOnly]
    public bool Exists(string fieldName)
    {
        throw new InvalidOperationException(
            $"DynamicFieldAccessor.Exists cannot be called directly. " +
            $"It is only valid within expression trees for filter or condition expressions. " +
            $"Example: table.Query().WithFilter(x => x.DynamicFields.Exists(\"{fieldName}\"))");
    }

    /// <summary>
    /// Checks if a dynamic field does not exist. Used in expressions like: x.DynamicFields.NotExists("customField")
    /// </summary>
    /// <param name="fieldName">The name of the dynamic field to check.</param>
    /// <returns>True if the field does not exist (for expression analysis only).</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is for expression analysis only.</exception>
    /// <remarks>
    /// This method is translated to the DynamoDB <c>attribute_not_exists()</c> function.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check if field does not exist
    /// x => x.DynamicFields.NotExists("deletedAt")
    /// // Generates: attribute_not_exists(#dynField0)
    /// </code>
    /// </example>
    [ExpressionOnly]
    public bool NotExists(string fieldName)
    {
        throw new InvalidOperationException(
            $"DynamicFieldAccessor.NotExists cannot be called directly. " +
            $"It is only valid within expression trees for filter or condition expressions. " +
            $"Example: table.Query().WithFilter(x => x.DynamicFields.NotExists(\"{fieldName}\"))");
    }

    /// <summary>
    /// Sets a dynamic field value in update expressions. Used in expressions like: x.DynamicFields.SetDynamicField("customField", value)
    /// </summary>
    /// <param name="fieldName">The name of the dynamic field to set.</param>
    /// <param name="value">The value to set for the dynamic field.</param>
    /// <returns>The value being set (for expression analysis only).</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is for expression analysis only.</exception>
    /// <remarks>
    /// <para>
    /// This method is translated to a DynamoDB SET action: <c>SET #dynField0 = :p0</c>
    /// </para>
    /// <para>
    /// The field name is automatically escaped to handle reserved words and special characters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Set a dynamic field in an update expression
    /// .Set(x => new UpdateModel { DynamicFieldResult = x.DynamicFields.SetDynamicField("customStatus", "active") })
    /// // Generates: SET #dynField0 = :p0
    /// </code>
    /// </example>
    [ExpressionOnly]
    public object? SetDynamicField(string fieldName, object? value)
    {
        throw new InvalidOperationException(
            $"DynamicFieldAccessor.SetDynamicField cannot be called directly. " +
            $"It is only valid within expression trees for update expressions. " +
            $"Example: .Set(x => new UpdateModel {{ DynamicFieldResult = x.DynamicFields.SetDynamicField(\"{fieldName}\", value) }})");
    }

    /// <summary>
    /// Removes a dynamic field in update expressions. Used in expressions like: x.DynamicFields.RemoveDynamicField("customField")
    /// </summary>
    /// <param name="fieldName">The name of the dynamic field to remove.</param>
    /// <returns>Null (for expression analysis only).</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is for expression analysis only.</exception>
    /// <remarks>
    /// <para>
    /// This method is translated to a DynamoDB REMOVE action: <c>REMOVE #dynField0</c>
    /// </para>
    /// <para>
    /// The field name is automatically escaped to handle reserved words and special characters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Remove a dynamic field in an update expression
    /// .Set(x => new UpdateModel { DynamicFieldResult = x.DynamicFields.RemoveDynamicField("tempData") })
    /// // Generates: REMOVE #dynField0
    /// </code>
    /// </example>
    [ExpressionOnly]
    public object? RemoveDynamicField(string fieldName)
    {
        throw new InvalidOperationException(
            $"DynamicFieldAccessor.RemoveDynamicField cannot be called directly. " +
            $"It is only valid within expression trees for update expressions. " +
            $"Example: .Set(x => new UpdateModel {{ DynamicFieldResult = x.DynamicFields.RemoveDynamicField(\"{fieldName}\") }})");
    }
}
