using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for builders that implement IWithFilterExpression interface.
/// Provides fluent methods for setting filter expressions with support for format strings.
/// 
/// <para><strong>Migration Guide:</strong></para>
/// <para>The existing <c>WithFilter(string)</c> method works exactly as before. The new <c>WithFilter(string, params object[])</c> 
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
/// builder.WithFilter("#status = :status AND #amount > :minAmount")
///        .WithAttribute("#status", "status")
///        .WithAttribute("#amount", "amount")
///        .WithValue(":status", "ACTIVE")
///        .WithValue(":minAmount", 100);
/// 
/// // New format string style
/// builder.WithFilter("#status = {0} AND #amount > {1}", "ACTIVE", 100);
/// 
/// // Mixed usage - both styles in same builder
/// builder.WithFilter("#status = {0} AND #customField = :customValue", "ACTIVE")
///        .WithValue(":customValue", "custom");
/// 
/// // DateTime formatting
/// builder.WithFilter("#status = {0} AND #created > {1:o}", "ACTIVE", DateTime.Now);
/// 
/// // Numeric filtering with formatting
/// builder.WithFilter("#status = {0} AND #amount BETWEEN {1:F2} AND {2:F2}", "ACTIVE", 10.999m, 99.999m);
/// </code>
/// </summary>
public static class WithFilterExpressionExtensions
{
    /// <summary>
    /// Specifies the filter expression for the operation.
    /// This is the existing method that accepts a pre-formatted filter expression.
    /// Filter expressions are applied after items are retrieved, so they reduce data transfer but not consumed capacity.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithFilterExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="filterExpression">The filter expression (e.g., "#status = :status" or "#amount BETWEEN :low AND :high").</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple filter
    /// .WithFilter("#status = :status")
    /// .WithAttribute("#status", "status")
    /// .WithValue(":status", "ACTIVE")
    /// 
    /// // Complex filter with multiple conditions
    /// .WithFilter("#status = :status AND #amount > :minAmount AND begins_with(#name, :prefix)")
    /// .WithAttribute("#status", "status")
    /// .WithAttribute("#amount", "amount")
    /// .WithAttribute("#name", "name")
    /// .WithValue(":status", "ACTIVE")
    /// .WithValue(":minAmount", 100)
    /// .WithValue(":prefix", "John")
    /// 
    /// // Range filter
    /// .WithFilter("#created BETWEEN :start AND :end")
    /// .WithAttribute("#created", "createdDate")
    /// .WithValue(":start", "2024-01-01T00:00:00.000Z")
    /// .WithValue(":end", "2024-12-31T23:59:59.999Z")
    /// </code>
    /// </example>
    public static T WithFilter<T>(this IWithFilterExpression<T> builder, string filterExpression)
    {
        return builder.SetFilterExpression(filterExpression);
    }

    /// <summary>
    /// Specifies the filter expression using format string syntax with automatic parameter generation.
    /// This method allows you to use {0}, {1}, {2:format} syntax instead of manual parameter naming.
    /// Values are automatically converted to appropriate AttributeValue types and parameters are generated.
    /// Filter expressions are applied after items are retrieved, so they reduce data transfer but not consumed capacity.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithFilterExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="format">The format string with {0}, {1}, etc. placeholders (e.g., "#status = {0} AND #amount > {1:F2}").</param>
    /// <param name="args">The values to substitute into the format string.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when format string is invalid or parameter count doesn't match.</exception>
    /// <exception cref="FormatException">Thrown when format specifiers are invalid for the given value type.</exception>
    /// <example>
    /// <code>
    /// // Simple filter - eliminates manual parameter naming
    /// .WithFilter("#status = {0}", "ACTIVE")
    /// // Equivalent to: .WithFilter("#status = :p0").WithValue(":p0", "ACTIVE")
    /// 
    /// // DateTime filtering with ISO 8601 formatting
    /// .WithFilter("#status = {0} AND #created > {1:o}", "ACTIVE", DateTime.Now)
    /// // Automatically formats DateTime as "2024-01-15T10:30:00.000Z"
    /// 
    /// // Numeric filtering with formatting
    /// .WithFilter("#status = {0} AND #amount BETWEEN {1:F2} AND {2:F2}", "ACTIVE", 10.999m, 99.999m)
    /// // Formats decimals as "10.99" and "99.99"
    /// 
    /// // Complex filter with mixed types
    /// .WithFilter("#status = {0} AND begins_with(#name, {1}) AND #created BETWEEN {2:o} AND {3:o}", 
    ///            "ACTIVE", "John", startDate, endDate)
    /// 
    /// // Boolean and enum filtering
    /// .WithFilter("#active = {0} AND #status = {1}", true, OrderStatus.Pending)
    /// // Converts enum to string: "Pending"
    /// 
    /// // Range filtering with contains
    /// .WithFilter("contains(#tags, {0}) AND #score > {1}", "important", 85)
    /// 
    /// // Null value handling
    /// .WithFilter("#status = {0} AND attribute_exists(#optional) = {1}", "ACTIVE", nullableValue)
    /// 
    /// // Size and type functions
    /// .WithFilter("size(#items) > {0} AND attribute_type(#data, {1})", 5, "S")
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><strong>Parameter Generation:</strong> Parameters are automatically named as :p0, :p1, :p2, etc.</para>
    /// <para><strong>Type Conversion:</strong> Values are automatically converted to appropriate DynamoDB AttributeValue types.</para>
    /// <para><strong>Format Safety:</strong> All format operations are AOT-safe and don't use reflection.</para>
    /// <para><strong>Error Handling:</strong> Clear error messages are provided for invalid format strings or type mismatches.</para>
    /// <para><strong>Performance Note:</strong> Filter expressions are applied after items are read from DynamoDB, so they reduce data transfer but not consumed read capacity.</para>
    /// </remarks>
    public static T WithFilter<T>(this IWithFilterExpression<T> builder, string format, params object[] args)
    {
        var (processedExpression, _) = FormatStringProcessor.ProcessFormatString(format, args, builder.GetAttributeValueHelper());
        return builder.SetFilterExpression(processedExpression);
    }

    /// <summary>
    /// Specifies the filter expression using a C# lambda expression.
    /// This method provides type-safe filter building with compile-time checking of property access.
    /// The expression is translated to DynamoDB expression syntax with automatic parameter generation.
    /// Filter expressions can reference any property (no key-only restriction).
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithFilterExpression.</typeparam>
    /// <typeparam name="TEntity">The entity type being filtered.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="expression">The lambda expression representing the filter (e.g., x => x.Status == "ACTIVE").</param>
    /// <param name="metadata">Optional entity metadata for property validation. If not provided, validation is skipped.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ExpressionTranslationException">Thrown when the expression cannot be translated to DynamoDB syntax.</exception>
    /// <exception cref="UnmappedPropertyException">Thrown when a property in the expression doesn't map to a DynamoDB attribute.</exception>
    /// <exception cref="UnsupportedExpressionException">Thrown when the expression uses unsupported operators or methods.</exception>
    /// <example>
    /// <code>
    /// // Simple filter on non-key attribute
    /// .WithFilter&lt;QueryRequestBuilder&lt;UserEntity&gt;, UserEntity&gt;(x => x.Status == "ACTIVE")
    /// 
    /// // Complex filter with multiple conditions
    /// .WithFilter&lt;QueryRequestBuilder&lt;UserEntity&gt;, UserEntity&gt;(x => x.Status == "ACTIVE" &amp;&amp; x.Age >= 18)
    /// 
    /// // Using string methods
    /// .WithFilter&lt;QueryRequestBuilder&lt;UserEntity&gt;, UserEntity&gt;(x => x.Name.StartsWith("John") &amp;&amp; x.Email.Contains("@example.com"))
    /// 
    /// // Using DynamoDB functions
    /// .WithFilter&lt;QueryRequestBuilder&lt;UserEntity&gt;, UserEntity&gt;(x => x.Age.Between(18, 65) &amp;&amp; x.Tags.Size() > 0)
    /// 
    /// // Checking attribute existence
    /// .WithFilter&lt;QueryRequestBuilder&lt;UserEntity&gt;, UserEntity&gt;(x => x.OptionalField.AttributeExists())
    /// 
    /// // With entity metadata for validation
    /// .WithFilter&lt;QueryRequestBuilder&lt;UserEntity&gt;, UserEntity&gt;(x => x.Status == "ACTIVE", userEntityMetadata)
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><strong>Validation Mode:</strong> This overload uses None validation, meaning any property can be referenced (not just keys).</para>
    /// <para><strong>Supported Operators:</strong> ==, !=, &lt;, &gt;, &lt;=, &gt;=, &amp;&amp;, ||, !</para>
    /// <para><strong>Supported Methods:</strong> StartsWith, Contains, Between, AttributeExists, AttributeNotExists, Size</para>
    /// <para><strong>Parameter Generation:</strong> Values are automatically captured and converted to DynamoDB AttributeValue types.</para>
    /// <para><strong>Performance Note:</strong> Filter expressions are applied after items are read from DynamoDB, so they reduce data transfer but not consumed read capacity.</para>
    /// <para><strong>AOT Safety:</strong> This method is AOT-safe and doesn't use runtime code generation.</para>
    /// </remarks>
    public static T WithFilter<T, TEntity>(
        this IWithFilterExpression<T> builder,
        Expression<Func<TEntity, bool>> expression,
        EntityMetadata? metadata = null)
        where TEntity : IEntityMetadataProvider
    {
        // If metadata is not provided, get it from the entity type's generated GetEntityMetadata() method
        metadata ??= MetadataResolver.GetEntityMetadata<TEntity>();
        
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            metadata,
            ExpressionValidationMode.None);

        // Get options from the builder if it implements IHasDynamoDbClient
        var options = (builder as IHasDynamoDbClient)?.GetOptions();
        var translator = new ExpressionTranslator(options);
        var expressionString = translator.Translate(expression, context);

        return builder.SetFilterExpression(expressionString);
    }

}

/// <summary>
/// Extension methods for QueryRequestBuilder with automatic type inference for filter expressions.
/// </summary>
public static class QueryRequestBuilderFilterExtensions
{
    /// <summary>
    /// Specifies the filter expression using a C# lambda expression with automatic type inference.
    /// This overload is specifically for QueryRequestBuilder and doesn't require explicit type parameters.
    /// The expression is translated to DynamoDB expression syntax with automatic parameter generation.
    /// Filter expressions can reference any property (no key-only restriction).
    /// </summary>
    /// <typeparam name="TEntity">The entity type being filtered (inferred from QueryRequestBuilder).</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="expression">The lambda expression representing the filter (e.g., x => x.Status == "ACTIVE").</param>
    /// <param name="metadata">Optional entity metadata for property validation. If not provided, it's resolved from the entity type.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple filter - no type parameters needed!
    /// table.Query&lt;UserEntity&gt;()
    ///     .Where("pk = :pk")
    ///     .WithValue(":pk", userId)
    ///     .WithFilter(x => x.Status == "ACTIVE")
    ///     .ExecuteAsync();
    /// 
    /// // Complex filter with multiple conditions
    /// table.Query&lt;UserEntity&gt;()
    ///     .Where("pk = :pk")
    ///     .WithValue(":pk", userId)
    ///     .WithFilter(x => x.Status == "ACTIVE" &amp;&amp; x.Age >= 18)
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static QueryRequestBuilder<TEntity> WithFilter<TEntity>(
        this QueryRequestBuilder<TEntity> builder,
        Expression<Func<TEntity, bool>> expression,
        EntityMetadata? metadata = null)
        where TEntity : class, IEntityMetadataProvider
    {
        return WithFilterExpressionExtensions.WithFilter<QueryRequestBuilder<TEntity>, TEntity>(
            builder, expression, metadata);
    }
}

/// <summary>
/// Extension methods for ScanRequestBuilder with automatic type inference for filter expressions.
/// </summary>
public static class ScanRequestBuilderFilterExtensions
{
    /// <summary>
    /// Specifies the filter expression using a C# lambda expression with automatic type inference.
    /// This overload is specifically for ScanRequestBuilder and doesn't require explicit type parameters.
    /// The expression is translated to DynamoDB expression syntax with automatic parameter generation.
    /// Filter expressions can reference any property.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being filtered (inferred from ScanRequestBuilder).</typeparam>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="expression">The lambda expression representing the filter (e.g., x => x.Status == "ACTIVE").</param>
    /// <param name="metadata">Optional entity metadata for property validation. If not provided, it's resolved from the entity type.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Scan with filter - no type parameters needed!
    /// table.Scan&lt;UserEntity&gt;()
    ///     .WithFilter(x => x.CreatedDate > DateTime.Now.AddDays(-30))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static ScanRequestBuilder<TEntity> WithFilter<TEntity>(
        this ScanRequestBuilder<TEntity> builder,
        Expression<Func<TEntity, bool>> expression,
        EntityMetadata? metadata = null)
        where TEntity : class, IEntityMetadataProvider
    {
        return WithFilterExpressionExtensions.WithFilter<ScanRequestBuilder<TEntity>, TEntity>(
            builder, expression, metadata);
    }
}

