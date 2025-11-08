namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Extension methods for DynamoDB expression support.
/// These methods are markers for expression translation and should not be called directly.
/// They will be recognized by the ExpressionTranslator and converted to DynamoDB syntax.
/// </summary>
/// <remarks>
/// <para><strong>Important:</strong></para>
/// <para>
/// These extension methods are designed exclusively for use within lambda expressions passed to
/// Query().Where(), WithFilter(), and similar methods. They are not meant to be called directly
/// in regular C# code and will throw <see cref="InvalidOperationException"/> if invoked.
/// </para>
/// 
/// <para><strong>How It Works:</strong></para>
/// <para>
/// When you use these methods in a lambda expression, the <see cref="ExpressionTranslator"/> 
/// recognizes them and translates them to the corresponding DynamoDB expression syntax. The methods
/// themselves are never actually executed - they serve as markers in the expression tree.
/// </para>
/// 
/// <para><strong>Comparison with String-Based Expressions:</strong></para>
/// <code>
/// // String-based approach (manual parameter management)
/// table.Query
///     .Where("pk = :pk AND sk BETWEEN :low AND :high")
///     .WithValue(":pk", userId)
///     .WithValue(":low", "2024-01-01")
///     .WithValue(":high", "2024-12-31")
///     .ExecuteAsync();
/// 
/// // Expression-based approach (type-safe, automatic parameters)
/// table.Query
///     .Where&lt;UserEntity&gt;(x => x.PartitionKey == userId &amp;&amp; x.SortKey.Between("2024-01-01", "2024-12-31"))
///     .ExecuteAsync();
/// 
/// // String-based filter with attribute_exists
/// table.Query
///     .Where("pk = :pk")
///     .WithFilter("attribute_exists(#optional) AND contains(#tags, :tag)")
///     .WithAttribute("#optional", "optionalField")
///     .WithAttribute("#tags", "tags")
///     .WithValue(":pk", userId)
///     .WithValue(":tag", "important")
///     .ExecuteAsync();
/// 
/// // Expression-based filter (cleaner, type-safe)
/// table.Query
///     .Where&lt;UserEntity&gt;(x => x.PartitionKey == userId)
///     .WithFilter&lt;UserEntity&gt;(x => x.OptionalField.AttributeExists() &amp;&amp; x.Tags.Contains("important"))
///     .ExecuteAsync();
/// </code>
/// 
/// <para><strong>Benefits of Expression-Based Approach:</strong></para>
/// <list type="bullet">
/// <item><description>Compile-time type checking - catch errors before runtime</description></item>
/// <item><description>IntelliSense support - discover available properties and methods</description></item>
/// <item><description>Automatic parameter generation - no manual :p0, :p1 naming</description></item>
/// <item><description>Refactoring safety - property renames are automatically reflected</description></item>
/// <item><description>No string concatenation errors or typos</description></item>
/// </list>
/// </remarks>
public static class DynamoDbExpressionExtensions
{
    /// <summary>
    /// Generates a BETWEEN condition in DynamoDB expressions.
    /// This method is only for use in lambda expressions and will be translated to DynamoDB syntax.
    /// The BETWEEN operator is inclusive on both bounds.
    /// </summary>
    /// <typeparam name="T">The type of the value being compared. Must implement IComparable&lt;T&gt;.</typeparam>
    /// <param name="value">The value to check (typically an entity property).</param>
    /// <param name="low">The lower bound (inclusive).</param>
    /// <param name="high">The upper bound (inclusive).</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>Valid Usage:</strong></para>
    /// <list type="bullet">
    /// <item><description>Numeric ranges: x => x.Age.Between(18, 65)</description></item>
    /// <item><description>String ranges: x => x.SortKey.Between("A", "Z")</description></item>
    /// <item><description>Date ranges: x => x.CreatedDate.Between(startDate, endDate)</description></item>
    /// <item><description>With variables: x => x.Score.Between(minScore, maxScore)</description></item>
    /// </list>
    /// 
    /// <para><strong>String-Based Equivalent:</strong></para>
    /// <code>
    /// // Expression-based
    /// .Where&lt;User&gt;(x => x.Age.Between(18, 65))
    /// 
    /// // String-based equivalent
    /// .Where("#age BETWEEN :low AND :high")
    /// .WithAttribute("#age", "age")
    /// .WithValue(":low", 18)
    /// .WithValue(":high", 65)
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Numeric range query
    /// table.Query
    ///     .Where&lt;User&gt;(x => x.PartitionKey == userId &amp;&amp; x.Age.Between(18, 65))
    ///     .ExecuteAsync();
    /// // Translates to: #attr0 = :p0 AND #attr1 BETWEEN :p1 AND :p2
    /// 
    /// // Date range filter
    /// var startDate = DateTime.Parse("2024-01-01");
    /// var endDate = DateTime.Parse("2024-12-31");
    /// table.Scan
    ///     .WithFilter&lt;Order&gt;(x => x.CreatedDate.Between(startDate, endDate))
    ///     .ExecuteAsync();
    /// 
    /// // String range on sort key
    /// table.Query
    ///     .Where&lt;User&gt;(x => x.PartitionKey == userId &amp;&amp; x.SortKey.Between("ORDER#2024-01", "ORDER#2024-12"))
    ///     .ExecuteAsync();
    /// 
    /// // Combined with other conditions
    /// table.Query
    ///     .Where&lt;User&gt;(x => x.PartitionKey == userId)
    ///     .WithFilter&lt;User&gt;(x => x.Score.Between(50, 100) &amp;&amp; x.Active)
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static bool Between<T>(this T value, T low, T high) where T : IComparable<T>
        => throw new InvalidOperationException("This method is only for use in expressions and should not be called directly.");
    
    /// <summary>
    /// Generates an attribute_exists() function in DynamoDB expressions.
    /// This method is only for use in lambda expressions and will be translated to DynamoDB syntax.
    /// Checks whether an attribute exists in the DynamoDB item, regardless of its value.
    /// </summary>
    /// <typeparam name="T">The type of the attribute.</typeparam>
    /// <param name="value">The attribute to check for existence (typically an entity property).</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Check if optional fields are present</description></item>
    /// <item><description>Filter items that have been updated with new attributes</description></item>
    /// <item><description>Validate data completeness</description></item>
    /// <item><description>Handle schema evolution scenarios</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>Returns true even if the attribute value is null</description></item>
    /// <item><description>Returns false only if the attribute is completely absent from the item</description></item>
    /// <item><description>Cannot be used in Query().Where() - only in WithFilter()</description></item>
    /// </list>
    /// 
    /// <para><strong>String-Based Equivalent:</strong></para>
    /// <code>
    /// // Expression-based
    /// .WithFilter&lt;User&gt;(x => x.OptionalField.AttributeExists())
    /// 
    /// // String-based equivalent
    /// .WithFilter("attribute_exists(#optional)")
    /// .WithAttribute("#optional", "optionalField")
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check if optional field exists
    /// table.Scan
    ///     .WithFilter&lt;User&gt;(x => x.PhoneNumber.AttributeExists())
    ///     .ExecuteAsync();
    /// // Translates to: attribute_exists(#attr0)
    /// 
    /// // Combined with other conditions
    /// table.Query
    ///     .Where&lt;User&gt;(x => x.PartitionKey == userId)
    ///     .WithFilter&lt;User&gt;(x => x.Email.AttributeExists() &amp;&amp; x.Active)
    ///     .ExecuteAsync();
    /// 
    /// // Check multiple optional fields
    /// table.Scan
    ///     .WithFilter&lt;User&gt;(x => x.PhoneNumber.AttributeExists() || x.Email.AttributeExists())
    ///     .ExecuteAsync();
    /// 
    /// // Validate data completeness
    /// table.Query
    ///     .Where&lt;Order&gt;(x => x.PartitionKey == customerId)
    ///     .WithFilter&lt;Order&gt;(x => x.ShippingAddress.AttributeExists() &amp;&amp; x.PaymentMethod.AttributeExists())
    ///     .ExecuteAsync();
    /// 
    /// // Schema evolution - find items with new attributes
    /// table.Scan
    ///     .WithFilter&lt;User&gt;(x => x.NewFeatureFlag.AttributeExists())
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static bool AttributeExists<T>(this T value)
        => throw new InvalidOperationException("This method is only for use in expressions and should not be called directly.");
    
    /// <summary>
    /// Generates an attribute_not_exists() function in DynamoDB expressions.
    /// This method is only for use in lambda expressions and will be translated to DynamoDB syntax.
    /// Checks whether an attribute does NOT exist in the DynamoDB item.
    /// </summary>
    /// <typeparam name="T">The type of the attribute.</typeparam>
    /// <param name="value">The attribute to check for non-existence (typically an entity property).</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Find items missing optional fields</description></item>
    /// <item><description>Identify incomplete records</description></item>
    /// <item><description>Filter out soft-deleted items (where DeletedAt exists)</description></item>
    /// <item><description>Condition checks for preventing overwrites</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>Returns true only if the attribute is completely absent from the item</description></item>
    /// <item><description>Returns false if the attribute exists, even if its value is null</description></item>
    /// <item><description>Cannot be used in Query().Where() - only in WithFilter() and WithCondition()</description></item>
    /// <item><description>Commonly used in condition expressions to prevent overwrites</description></item>
    /// </list>
    /// 
    /// <para><strong>String-Based Equivalent:</strong></para>
    /// <code>
    /// // Expression-based
    /// .WithFilter&lt;User&gt;(x => x.DeletedAt.AttributeNotExists())
    /// 
    /// // String-based equivalent
    /// .WithFilter("attribute_not_exists(#deleted)")
    /// .WithAttribute("#deleted", "deletedAt")
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Find active users (no DeletedAt attribute)
    /// table.Scan
    ///     .WithFilter&lt;User&gt;(x => x.DeletedAt.AttributeNotExists())
    ///     .ExecuteAsync();
    /// // Translates to: attribute_not_exists(#attr0)
    /// 
    /// // Find incomplete records
    /// table.Query
    ///     .Where&lt;User&gt;(x => x.PartitionKey == userId)
    ///     .WithFilter&lt;User&gt;(x => x.Email.AttributeNotExists() || x.PhoneNumber.AttributeNotExists())
    ///     .ExecuteAsync();
    /// 
    /// // Condition check to prevent overwrites
    /// table.PutItem(newUser)
    ///     .WithCondition&lt;User&gt;(x => x.Id.AttributeNotExists())
    ///     .ExecuteAsync();
    /// // Only succeeds if item doesn't already exist
    /// 
    /// // Find items not yet migrated to new schema
    /// table.Scan
    ///     .WithFilter&lt;User&gt;(x => x.NewSchemaVersion.AttributeNotExists())
    ///     .ExecuteAsync();
    /// 
    /// // Combined conditions
    /// table.Query
    ///     .Where&lt;Order&gt;(x => x.PartitionKey == customerId)
    ///     .WithFilter&lt;Order&gt;(x => x.DeletedAt.AttributeNotExists() &amp;&amp; x.Status == "PENDING")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static bool AttributeNotExists<T>(this T value)
        => throw new InvalidOperationException("This method is only for use in expressions and should not be called directly.");
    
    /// <summary>
    /// Generates a size() function in DynamoDB expressions.
    /// This method is only for use in lambda expressions and will be translated to DynamoDB syntax.
    /// Returns the size of a string, binary, list, map, or set attribute.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to get the size of (typically an entity property).</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>Supported Attribute Types:</strong></para>
    /// <list type="bullet">
    /// <item><description>String (S): Returns the length of the string</description></item>
    /// <item><description>Binary (B): Returns the number of bytes</description></item>
    /// <item><description>List (L): Returns the number of elements</description></item>
    /// <item><description>Map (M): Returns the number of key-value pairs</description></item>
    /// <item><description>String Set (SS): Returns the number of elements</description></item>
    /// <item><description>Number Set (NS): Returns the number of elements</description></item>
    /// <item><description>Binary Set (BS): Returns the number of elements</description></item>
    /// </list>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Filter by collection length: x => x.Items.Size() > 0</description></item>
    /// <item><description>Find items with many tags: x => x.Tags.Size() >= 5</description></item>
    /// <item><description>Validate string length: x => x.Description.Size() > 100</description></item>
    /// <item><description>Check for empty collections: x => x.Items.Size() == 0</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>Cannot be used in Query().Where() - only in WithFilter()</description></item>
    /// <item><description>For strings, returns character count (not byte count)</description></item>
    /// <item><description>Returns 0 for empty collections</description></item>
    /// </list>
    /// 
    /// <para><strong>String-Based Equivalent:</strong></para>
    /// <code>
    /// // Expression-based
    /// .WithFilter&lt;User&gt;(x => x.Items.Size() > 5)
    /// 
    /// // String-based equivalent
    /// .WithFilter("size(#items) > :count")
    /// .WithAttribute("#items", "items")
    /// .WithValue(":count", 5)
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Filter by collection size
    /// table.Scan
    ///     .WithFilter&lt;User&gt;(x => x.Items.Size() > 5)
    ///     .ExecuteAsync();
    /// // Translates to: size(#attr0) > :p0
    /// 
    /// // Find users with many tags
    /// table.Query
    ///     .Where&lt;User&gt;(x => x.PartitionKey == userId)
    ///     .WithFilter&lt;User&gt;(x => x.Tags.Size() >= 10)
    ///     .ExecuteAsync();
    /// 
    /// // Check for non-empty collections
    /// table.Scan
    ///     .WithFilter&lt;Order&gt;(x => x.Items.Size() > 0 &amp;&amp; x.Status == "PENDING")
    ///     .ExecuteAsync();
    /// 
    /// // String length validation
    /// table.Query
    ///     .Where&lt;Post&gt;(x => x.PartitionKey == userId)
    ///     .WithFilter&lt;Post&gt;(x => x.Content.Size() > 100)
    ///     .ExecuteAsync();
    /// 
    /// // Range check on collection size
    /// table.Scan
    ///     .WithFilter&lt;User&gt;(x => x.Items.Size().Between(5, 20))
    ///     .ExecuteAsync();
    /// 
    /// // Find empty collections
    /// table.Query
    ///     .Where&lt;User&gt;(x => x.PartitionKey == userId)
    ///     .WithFilter&lt;User&gt;(x => x.Items.Size() == 0)
    ///     .ExecuteAsync();
    /// 
    /// // Combined with other conditions
    /// table.Scan
    ///     .WithFilter&lt;User&gt;(x => x.Items.Size() > 0 &amp;&amp; x.Tags.Size() > 0 &amp;&amp; x.Active)
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static int Size<T>(this IEnumerable<T> collection)
        => throw new InvalidOperationException("This method is only for use in expressions and should not be called directly.");
}
