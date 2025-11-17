using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Utility;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for builders that implement IWithConditionExpression interface.
/// Provides fluent methods for setting condition expressions with support for format strings.
/// 
/// <para><strong>Migration Guide:</strong></para>
/// <para>The existing <c>Where(string)</c> method works exactly as before. The new <c>Where(string, params object[])</c> 
/// overload provides enhanced functionality with automatic parameter generation and formatting.</para>
/// 
/// <para><strong>Format String Syntax:</strong></para>
/// <list type="bullet">
/// <item><c>{0}</c> - Simple parameter substitution</item>
/// <item><c>{0:format}</c> - Parameter with format specifier</item>
/// <item><c>{1:o}</c> - DateTime with ISO 8601 format</item>
/// <item><c>{2:F2}</c> - Decimal with 2 decimal places</item>
/// <item><c>{3:X}</c> - Integer as hexadecimal</item>
/// </list>
/// 
/// <para><strong>Supported Format Specifiers:</strong></para>
/// <list type="table">
/// <listheader><term>Format</term><description>Description</description><description>Example</description></listheader>
/// <item><term>o</term><description>ISO 8601 DateTime</description><description>2024-01-15T10:30:00.000Z</description></item>
/// <item><term>F2</term><description>Fixed-point with 2 decimals</description><description>123.45</description></item>
/// <item><term>X</term><description>Hexadecimal uppercase</description><description>FF</description></item>
/// <item><term>x</term><description>Hexadecimal lowercase</description><description>ff</description></item>
/// <item><term>D</term><description>Decimal integer</description><description>123</description></item>
/// </list>
/// 
/// <para><strong>Usage Examples:</strong></para>
/// <code>
/// // Old style (still supported)
/// builder.Where("pk = :pk AND created > :date")
///        .WithValue(":pk", "USER#123")
///        .WithValue(":date", "2024-01-01T00:00:00.000Z");
/// 
/// // New format string style
/// builder.Where("pk = {0} AND created > {1:o}", "USER#123", DateTime.Now);
/// 
/// // Mixed usage - both styles in same builder
/// builder.Where("pk = {0} AND sk = :customSk", "USER#123")
///        .WithValue(":customSk", "PROFILE");
/// </code>
/// </summary>
public static class WithConditionExpressionExtensions
{
    /// <summary>
    /// Specifies the condition expression for the operation.
    /// This is the existing method that accepts a pre-formatted condition expression.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithConditionExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="conditionExpression">The condition expression (e.g., "pk = :pk" or "pk = :pk AND begins_with(sk, :prefix)").</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple condition
    /// .Where("pk = :pk")
    /// .WithValue(":pk", "USER#123")
    /// 
    /// // Complex condition with multiple parameters
    /// .Where("pk = :pk AND begins_with(sk, :prefix)")
    /// .WithValue(":pk", "USER#123")
    /// .WithValue(":prefix", "ORDER#")
    /// </code>
    /// </example>
    [GenerateWrapper]
    public static T Where<T>(this IWithConditionExpression<T> builder, string conditionExpression)
    {
        return builder.SetConditionExpression(conditionExpression);
    }

    /// <summary>
    /// Specifies the condition expression using format string syntax with automatic parameter generation.
    /// This method allows you to use {0}, {1}, {2:format} syntax instead of manual parameter naming.
    /// Values are automatically converted to appropriate AttributeValue types and parameters are generated.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithConditionExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="format">The format string with {0}, {1}, etc. placeholders (e.g., "pk = {0} AND created > {1:o}").</param>
    /// <param name="args">The values to substitute into the format string.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when format string is invalid or parameter count doesn't match.</exception>
    /// <exception cref="FormatException">Thrown when format specifiers are invalid for the given value type.</exception>
    /// <example>
    /// <code>
    /// // Simple format string usage - eliminates manual parameter naming
    /// .Where("pk = {0}", "USER#123")
    /// // Equivalent to: .Where("pk = :p0").WithValue(":p0", "USER#123")
    /// 
    /// // DateTime formatting with ISO 8601
    /// .Where("pk = {0} AND created > {1:o}", "USER#123", DateTime.Now)
    /// // Automatically formats DateTime as "2024-01-15T10:30:00.000Z"
    /// 
    /// // Numeric formatting
    /// .Where("pk = {0} AND amount > {1:F2}", "USER#123", 99.999m)
    /// // Formats decimal as "99.99"
    /// 
    /// // Enum handling (automatic string conversion)
    /// .Where("pk = {0} AND #status = {1}", "USER#123", OrderStatus.Pending)
    /// // Converts enum to string: "Pending"
    /// 
    /// // Complex condition with mixed types
    /// .Where("pk = {0} AND begins_with(sk, {1}) AND created BETWEEN {2:o} AND {3:o}", 
    ///        "USER#123", "ORDER#", startDate, endDate)
    /// 
    /// // Boolean values
    /// .Where("pk = {0} AND active = {1}", "USER#123", true)
    /// 
    /// // Null handling - null values are converted appropriately
    /// .Where("pk = {0} AND optional_field = {1}", "USER#123", nullableValue)
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><strong>Parameter Generation:</strong> Parameters are automatically named as :p0, :p1, :p2, etc.</para>
    /// <para><strong>Type Conversion:</strong> Values are automatically converted to appropriate DynamoDB AttributeValue types.</para>
    /// <para><strong>Format Safety:</strong> All format operations are AOT-safe and don't use reflection.</para>
    /// <para><strong>Error Handling:</strong> Clear error messages are provided for invalid format strings or type mismatches.</para>
    /// </remarks>
    [GenerateWrapper]
    public static T Where<T>(this IWithConditionExpression<T> builder, string format, params object[] args)
    {
        var (processedExpression, _) = FormatStringProcessor.ProcessFormatString(format, args, builder.GetAttributeValueHelper());
        return builder.SetConditionExpression(processedExpression);
    }

    /// <summary>
    /// Specifies the condition expression using a C# lambda expression.
    /// This method provides type-safe query building with compile-time checking of property access.
    /// The expression is translated to DynamoDB expression syntax with automatic parameter generation.
    /// For Query operations, only partition key and sort key properties are allowed (KeysOnly validation).
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithConditionExpression.</typeparam>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="expression">The lambda expression representing the condition (e.g., x => x.PartitionKey == userId).</param>
    /// <param name="metadata">Optional entity metadata for property validation. If not provided, validation is skipped.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ExpressionTranslationException">Thrown when the expression cannot be translated to DynamoDB syntax.</exception>
    /// <exception cref="UnmappedPropertyException">Thrown when a property in the expression doesn't map to a DynamoDB attribute.</exception>
    /// <exception cref="InvalidKeyExpressionException">Thrown when a non-key property is used in Query().Where().</exception>
    /// <exception cref="UnsupportedExpressionException">Thrown when the expression uses unsupported operators or methods.</exception>
    /// <example>
    /// <code>
    /// // Simple partition key query
    /// .Where&lt;QueryRequestBuilder, UserEntity&gt;(x => x.PartitionKey == userId)
    /// 
    /// // Partition key + sort key with StartsWith
    /// .Where&lt;QueryRequestBuilder, UserEntity&gt;(x => x.PartitionKey == userId &amp;&amp; x.SortKey.StartsWith("ORDER#"))
    /// 
    /// // Using Between for range queries
    /// .Where&lt;QueryRequestBuilder, UserEntity&gt;(x => x.PartitionKey == userId &amp;&amp; x.SortKey.Between("2024-01-01", "2024-12-31"))
    /// 
    /// // Complex key condition with multiple operators
    /// .Where&lt;QueryRequestBuilder, UserEntity&gt;(x => x.PartitionKey == userId &amp;&amp; x.SortKey >= "ORDER#2024")
    /// 
    /// // With entity metadata for validation
    /// .Where&lt;QueryRequestBuilder, UserEntity&gt;(x => x.PartitionKey == userId, userEntityMetadata)
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><strong>Validation Mode:</strong> This overload uses KeysOnly validation, meaning only partition key and sort key properties are allowed.</para>
    /// <para><strong>Supported Operators:</strong> ==, !=, &lt;, &gt;, &lt;=, &gt;=, &amp;&amp;, ||, !</para>
    /// <para><strong>Supported Methods:</strong> StartsWith, Contains, Between, AttributeExists, AttributeNotExists, Size</para>
    /// <para><strong>Parameter Generation:</strong> Values are automatically captured and converted to DynamoDB AttributeValue types.</para>
    /// <para><strong>AOT Safety:</strong> This method is AOT-safe and doesn't use runtime code generation.</para>
    /// </remarks>
    [GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity generic parameter to the builder's entity type")]
    public static T Where<T, TEntity>(
        this IWithConditionExpression<T> builder,
        Expression<Func<TEntity, bool>> expression,
        EntityMetadata? metadata = null)
    {
        // If metadata is not provided, try to get it from the entity type's generated GetEntityMetadata() method
        if (metadata == null)
        {
            metadata = MetadataResolver.GetEntityMetadata<TEntity>();
        }
        
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            metadata,
            ExpressionValidationMode.KeysOnly);

        var translator = new ExpressionTranslator();
        var expressionString = translator.Translate(expression, context);

        return builder.SetConditionExpression(expressionString);
    }
    
    /// <summary>
    /// Specifies the condition expression using a C# lambda expression for QueryRequestBuilder.
    /// This overload provides better type inference for query operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="expression">The lambda expression representing the condition.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static QueryRequestBuilder<TEntity> Where<TEntity>(
        this QueryRequestBuilder<TEntity> builder,
        Expression<Func<TEntity, bool>> expression,
        EntityMetadata? metadata = null)
        where TEntity : class
    {
        return Where<QueryRequestBuilder<TEntity>, TEntity>(builder, expression, metadata);
    }

    /// <summary>
    /// Specifies the condition expression using a C# lambda expression for ConditionCheckBuilder.
    /// This overload provides better type inference for condition check operations in transactions.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being checked.</typeparam>
    /// <param name="builder">The ConditionCheckBuilder instance.</param>
    /// <param name="expression">The lambda expression representing the condition.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Use in a transaction with lambda expression
    /// await DynamoDbTransactions.Write
    ///     .Add(table.BasicPkEntitys.ConditionCheck("1234").Where(x => x.Name == "Test"))
    ///     .Add(table.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = 30 }))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static ConditionCheckBuilder<TEntity> Where<TEntity>(
        this ConditionCheckBuilder<TEntity> builder,
        Expression<Func<TEntity, bool>> expression,
        EntityMetadata? metadata = null)
        where TEntity : class
    {
        // If metadata is not provided, try to get it from the entity type's generated GetEntityMetadata() method
        if (metadata == null)
        {
            metadata = MetadataResolver.GetEntityMetadata<TEntity>();
        }
        
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            metadata,
            ExpressionValidationMode.None); // Use None for condition checks - they can reference any property

        var translator = new ExpressionTranslator();
        var expressionString = translator.Translate(expression, context);

        return builder.SetConditionExpression(expressionString);
    }

}