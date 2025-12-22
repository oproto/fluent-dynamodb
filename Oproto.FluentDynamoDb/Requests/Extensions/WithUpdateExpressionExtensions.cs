using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for builders that implement IWithUpdateExpression interface.
/// Provides fluent methods for setting update expressions with support for format strings.
/// 
/// <para><strong>Migration Guide:</strong></para>
/// <para>The existing <c>Set(string)</c> method works exactly as before. The new <c>Set(string, params object[])</c> 
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
/// builder.Set("SET #name = :name, #status = :status")
///        .WithAttribute("#name", "name")
///        .WithAttribute("#status", "status")
///        .WithValue(":name", "John Doe")
///        .WithValue(":status", "ACTIVE");
/// 
/// // New format string style
/// builder.Set("SET #name = {0}, #status = {1}", "John Doe", "ACTIVE");
/// 
/// // Mixed usage - both styles in same builder
/// builder.Set("SET #name = {0}, #customField = :customValue", "John Doe")
///        .WithValue(":customValue", "custom");
/// 
/// // DateTime formatting
/// builder.Set("SET #name = {0}, #updated = {1:o}", "John Doe", DateTime.Now);
/// 
/// // Numeric operations with formatting
/// builder.Set("ADD #count {0}, #amount {1:F2}", 1, 99.999m);
/// </code>
/// </summary>
public static class WithUpdateExpressionExtensions
{
    /// <summary>
    /// Specifies the update expression for the operation.
    /// This is the existing method that accepts a pre-formatted update expression.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="updateExpression">The update expression (e.g., "SET #name = :name" or "ADD #count :inc").</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // SET operation
    /// .Set("SET #name = :name, #status = :status")
    /// .WithAttribute("#name", "name")
    /// .WithAttribute("#status", "status")
    /// .WithValue(":name", "John Doe")
    /// .WithValue(":status", "ACTIVE")
    /// 
    /// // ADD operation
    /// .Set("ADD #count :inc, #tags :newTags")
    /// .WithAttribute("#count", "count")
    /// .WithAttribute("#tags", "tags")
    /// .WithValue(":inc", 1)
    /// .WithValue(":newTags", new[] { "tag1", "tag2" })
    /// 
    /// // REMOVE operation
    /// .Set("REMOVE #oldField, #tempData")
    /// .WithAttribute("#oldField", "oldField")
    /// .WithAttribute("#tempData", "tempData")
    /// 
    /// // Combined operations
    /// .Set("SET #name = :name ADD #count :inc REMOVE #oldField")
    /// </code>
    /// </example>
    [GenerateWrapper]
    public static T Set<T>(this IWithUpdateExpression<T> builder, string updateExpression)
    {
        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.StringBased);
    }

    /// <summary>
    /// Specifies the update expression using format string syntax with automatic parameter generation.
    /// This method allows you to use {0}, {1}, {2:format} syntax instead of manual parameter naming.
    /// Values are automatically converted to appropriate AttributeValue types and parameters are generated.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="format">The format string with {0}, {1}, etc. placeholders (e.g., "SET #name = {0}, #updated = {1:o}").</param>
    /// <param name="args">The values to substitute into the format string.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when format string is invalid or parameter count doesn't match.</exception>
    /// <exception cref="FormatException">Thrown when format specifiers are invalid for the given value type.</exception>
    /// <example>
    /// <code>
    /// // Simple SET operation - eliminates manual parameter naming
    /// .Set("SET #name = {0}, #status = {1}", "John Doe", "ACTIVE")
    /// // Equivalent to: .Set("SET #name = :p0, #status = :p1").WithValue(":p0", "John Doe").WithValue(":p1", "ACTIVE")
    /// 
    /// // DateTime formatting with ISO 8601
    /// .Set("SET #name = {0}, #updated = {1:o}", "John Doe", DateTime.Now)
    /// // Automatically formats DateTime as "2024-01-15T10:30:00.000Z"
    /// 
    /// // Numeric operations with formatting
    /// .Set("ADD #count {0}, #amount {1:F2}", 1, 99.999m)
    /// // Formats decimal as "99.99"
    /// 
    /// // REMOVE operations (no parameters needed)
    /// .Set("REMOVE #oldField, #tempData")
    /// 
    /// // Complex combined operations
    /// .Set("SET #name = {0}, #updated = {1:o} ADD #count {2} REMOVE #oldField", 
    ///      "John Doe", DateTime.Now, 1)
    /// 
    /// // Boolean and enum values
    /// .Set("SET #active = {0}, #status = {1}", true, OrderStatus.Pending)
    /// 
    /// // Null handling - null values are converted appropriately
    /// .Set("SET #name = {0}, #optional = {1}", "John Doe", nullableValue)
    /// 
    /// // List and set operations
    /// .Set("ADD #tags {0}, #scores {1}", new[] { "tag1", "tag2" }, new[] { 100, 200 })
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><strong>Parameter Generation:</strong> Parameters are automatically named as :p0, :p1, :p2, etc.</para>
    /// <para><strong>Type Conversion:</strong> Values are automatically converted to appropriate DynamoDB AttributeValue types.</para>
    /// <para><strong>Format Safety:</strong> All format operations are AOT-safe and don't use reflection.</para>
    /// <para><strong>Error Handling:</strong> Clear error messages are provided for invalid format strings or type mismatches.</para>
    /// <para><strong>Update Expression Types:</strong> Supports SET, ADD, REMOVE, and DELETE operations with format strings.</para>
    /// </remarks>
    [GenerateWrapper]
    public static T Set<T>(this IWithUpdateExpression<T> builder, string format, params object[] args)
    {
        var (processedExpression, _) = FormatStringProcessor.ProcessFormatString(format, args, builder.GetAttributeValueHelper());
        return builder.SetUpdateExpression(processedExpression, UpdateExpressionSource.StringBased);
    }

    /// <summary>
    /// Specifies update operations using a type-safe C# lambda expression.
    /// This method provides compile-time checking, IntelliSense support, and automatic parameter generation
    /// for update expressions. The expression uses source-generated UpdateExpressions and UpdateModel classes
    /// to enable type-safe operations like Add(), Remove(), Delete(), and DynamoDB functions.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TUpdateExpressions">The source-generated UpdateExpressions type (e.g., UserUpdateExpressions).</typeparam>
    /// <typeparam name="TUpdateModel">The source-generated UpdateModel type (e.g., UserUpdateModel).</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="expression">Lambda expression returning an UpdateModel with property assignments.</param>
    /// <param name="metadata">Optional entity metadata. If not provided, attempts to resolve from entity type.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when expression is null.</exception>
    /// <exception cref="UnsupportedExpressionException">Thrown when the expression pattern is not supported.</exception>
    /// <exception cref="InvalidUpdateOperationException">Thrown when attempting to update key properties.</exception>
    /// <exception cref="UnmappedPropertyException">Thrown when a property doesn't map to a DynamoDB attribute.</exception>
    /// <example>
    /// <code>
    /// // Simple SET operations
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         Name = "John",
    ///         Status = "Active"
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // Atomic ADD operation
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         LoginCount = x.LoginCount.Add(1)
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // Arithmetic in SET
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         Score = x.Score + 10
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // REMOVE operation
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         TempData = x.TempData.Remove()
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // DELETE from set
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         Tags = x.Tags.Delete("old-tag")
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // if_not_exists function
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         ViewCount = x.ViewCount.IfNotExists(0)
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // list_append function
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         History = x.History.ListAppend("new-event")
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // Combined operations
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         Name = "John",
    ///         LoginCount = x.LoginCount.Add(1),
    ///         TempData = x.TempData.Remove(),
    ///         Tags = x.Tags.Delete("old-tag")
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // With captured variables
    /// var newName = "John Doe";
    /// var increment = 5;
    /// table.Users.Update(userId)
    ///     .Set&lt;User, UserUpdateExpressions, UserUpdateModel&gt;(x => new UserUpdateModel 
    ///     {
    ///         Name = newName,
    ///         Score = x.Score + increment
    ///     })
    ///     .UpdateAsync();
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><strong>Source Generation:</strong> This method requires source-generated UpdateExpressions and UpdateModel classes.
    /// The source generator creates these classes for entities marked with [DynamoDbTable] attribute.</para>
    /// 
    /// <para><strong>Supported Operations:</strong></para>
    /// <list type="bullet">
    /// <item><description>SET: Simple value assignments (Name = "John")</description></item>
    /// <item><description>SET with arithmetic: x.Score + 10, x.Count - 5</description></item>
    /// <item><description>ADD: x.LoginCount.Add(1) for atomic increment/decrement</description></item>
    /// <item><description>ADD: x.Tags.Add("tag1", "tag2") for set union</description></item>
    /// <item><description>REMOVE: x.TempData.Remove() to delete attributes</description></item>
    /// <item><description>DELETE: x.Tags.Delete("old-tag") to remove set elements</description></item>
    /// <item><description>if_not_exists: x.ViewCount.IfNotExists(0) for conditional initialization</description></item>
    /// <item><description>list_append: x.History.ListAppend("event") to append to lists</description></item>
    /// <item><description>list_prepend: x.History.ListPrepend("event") to prepend to lists</description></item>
    /// </list>
    /// 
    /// <para><strong>Format Strings:</strong> Format strings defined in entity metadata are automatically applied to values.</para>
    /// 
    /// <para><strong>Encryption:</strong> Properties marked with [Encrypted] attribute will have values encrypted automatically
    /// if an IFieldEncryptor is configured in the operation context.</para>
    /// 
    /// <para><strong>Validation:</strong> Key properties (partition key, sort key) cannot be updated and will throw InvalidUpdateOperationException.</para>
    /// 
    /// <para><strong>Type Safety:</strong> Extension methods are only available for appropriate property types:
    /// Add() for numeric and set types, Delete() for set types, ListAppend/ListPrepend for list types.</para>
    /// 
    /// <para><strong>AOT Compatibility:</strong> This method is fully AOT-compatible and doesn't use runtime code generation.</para>
    /// 
    /// <para><strong>Mixing with String-Based Methods:</strong> This method cannot be mixed with string-based Set() methods
    /// in the same builder. Attempting to do so will throw an InvalidOperationException. Use only one approach consistently
    /// throughout the builder chain. If you need to combine multiple update operations, use multiple property assignments
    /// within a single expression-based Set() call.</para>
    /// </remarks>
    [GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity and TUpdateExpressions generic parameters to the builder's entity type")]
    public static T Set<T, TEntity, TUpdateExpressions, TUpdateModel>(
        this IWithUpdateExpression<T> builder,
        Expression<Func<TUpdateExpressions, TUpdateModel>> expression,
        EntityMetadata? metadata = null)
        where TEntity : class, IEntityMetadataProvider
        where TUpdateExpressions : new()
        where TUpdateModel : new()
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));

        // Resolve metadata if not provided
        metadata ??= MetadataResolver.GetEntityMetadata<TEntity>();

        // Create expression context
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            metadata,
            ExpressionValidationMode.None);

        // Create translator (no encryption support for now as it requires async)
        var translator = new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);

        // Translate the expression
        var updateExpression = translator.TranslateUpdateExpression(expression, context);

        // Apply to builder and store context for encryption
        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Specifies update operations using a type-safe C# lambda expression for UpdateItemRequestBuilder.
    /// This overload provides better type inference for update operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TUpdateExpressions">The source-generated UpdateExpressions type.</typeparam>
    /// <typeparam name="TUpdateModel">The source-generated UpdateModel type.</typeparam>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
    /// <param name="expression">Lambda expression returning an UpdateModel with property assignments.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static UpdateItemRequestBuilder<TEntity> Set<TEntity, TUpdateExpressions, TUpdateModel>(
        this UpdateItemRequestBuilder<TEntity> builder,
        Expression<Func<TUpdateExpressions, TUpdateModel>> expression,
        EntityMetadata? metadata = null)
        where TEntity : class, IDynamoDbEntity, IEntityMetadataProvider
        where TUpdateExpressions : new()
        where TUpdateModel : new()
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));

        // Resolve metadata if not provided
        metadata ??= MetadataResolver.GetEntityMetadata<TEntity>();

        // Create expression context
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            metadata,
            ExpressionValidationMode.None);

        // Create translator (no encryption support for now as it requires async)
        var translator = new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);

        // Translate the expression
        var updateExpression = translator.TranslateUpdateExpression(expression, context);

        // Store context in builder for encryption support
        builder.SetExpressionContext(context);

        // Apply to builder
        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    #region Shared Helper Methods

    /// <summary>
    /// Gets the DynamoDB attribute name from a member expression.
    /// </summary>
    /// <param name="member">The member expression.</param>
    /// <returns>The DynamoDB attribute name.</returns>
    private static string GetAttributeNameFromMember(MemberExpression member)
    {
        // Check for [DynamoDbAttribute] attribute
        var dynamoDbAttr = member.Member.GetCustomAttributes(typeof(DynamoDbAttributeAttribute), true)
            .FirstOrDefault() as DynamoDbAttributeAttribute;

        if (dynamoDbAttr != null && !string.IsNullOrEmpty(dynamoDbAttr.AttributeName))
        {
            return dynamoDbAttr.AttributeName;
        }

        // Default to lowercase property name (camelCase convention)
        var propertyName = member.Member.Name;
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    #endregion

    #region Set Operations (Requirements 5.1, 5.2, 5.3, 5.4, 5.5)

    /// <summary>
    /// Adds a single element to a set using DynamoDB's ADD action.
    /// Generates an ADD expression (e.g., ADD #categories :v0).
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="setSelector">Lambda expression selecting the set property (e.g., x => x.Categories).</param>
    /// <param name="value">The value to add to the set.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when setSelector is null.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>
    /// Translates to: ADD #attr :val
    /// Where :val is a set containing the single element.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>If the attribute doesn't exist, it is created with the specified element</description></item>
    /// <item><description>If the attribute exists, the element is added to the existing set</description></item>
    /// <item><description>Duplicate elements are automatically handled by DynamoDB (sets don't allow duplicates)</description></item>
    /// </list>
    /// 
    /// <para><strong>Supported Set Types:</strong></para>
    /// <list type="bullet">
    /// <item><description>HashSet&lt;string&gt; - String sets (SS)</description></item>
    /// <item><description>HashSet&lt;int&gt;, HashSet&lt;long&gt;, HashSet&lt;decimal&gt;, HashSet&lt;double&gt; - Number sets (NS)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Add single category
    /// await table.Items.Update(itemId)
    ///     .AddToSet(x => x.Categories, "electronics")
    ///     .UpdateAsync();
    /// // Translates to: ADD #categories :v0
    /// // Where :v0 = { SS: ["electronics"] }
    /// 
    /// // Add to numeric set
    /// await table.Items.Update(itemId)
    ///     .AddToSet(x => x.Scores, 100)
    ///     .UpdateAsync();
    /// // Translates to: ADD #scores :v0
    /// // Where :v0 = { NS: ["100"] }
    /// </code>
    /// </example>
    [GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity generic parameter to the builder's entity type")]
    public static T AddToSet<T, TEntity, TElement>(
        this IWithUpdateExpression<T> builder,
        Expression<Func<TEntity, HashSet<TElement>>> setSelector,
        TElement value,
        EntityMetadata? metadata = null)
        where TEntity : class, IEntityMetadataProvider
    {
        if (setSelector == null)
            throw new ArgumentNullException(nameof(setSelector));

        // Extract the document path from the set selector
        var documentPath = ExtractPropertyPath(setSelector.Body, builder.GetAttributeNameHelper());

        // Capture the value as a set
        var valuePlaceholder = CaptureSetValueForAdd(new[] { value }, builder.GetAttributeValueHelper());

        // Build ADD expression
        var updateExpression = $"ADD {documentPath} {valuePlaceholder}";

        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Adds a single element to a set using DynamoDB's ADD action for UpdateItemRequestBuilder.
    /// This overload provides better type inference for update operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
    /// <param name="setSelector">Lambda expression selecting the set property (e.g., x => x.Categories).</param>
    /// <param name="value">The value to add to the set.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static UpdateItemRequestBuilder<TEntity> AddToSet<TEntity, TElement>(
        this UpdateItemRequestBuilder<TEntity> builder,
        Expression<Func<TEntity, HashSet<TElement>>> setSelector,
        TElement value,
        EntityMetadata? metadata = null)
        where TEntity : class, IDynamoDbEntity, IEntityMetadataProvider
    {
        if (setSelector == null)
            throw new ArgumentNullException(nameof(setSelector));

        // Extract the document path from the set selector
        var documentPath = ExtractPropertyPath(setSelector.Body, builder.GetAttributeNameHelper());

        // Capture the value as a set
        var valuePlaceholder = CaptureSetValueForAdd(new[] { value }, builder.GetAttributeValueHelper());

        // Build ADD expression
        var updateExpression = $"ADD {documentPath} {valuePlaceholder}";

        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Adds multiple elements to a set using DynamoDB's ADD action.
    /// Generates an ADD expression (e.g., ADD #categories :v0).
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="setSelector">Lambda expression selecting the set property (e.g., x => x.Categories).</param>
    /// <param name="values">The values to add to the set.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when setSelector or values is null.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>
    /// Translates to: ADD #attr :val
    /// Where :val is a set containing all the specified elements.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>If the attribute doesn't exist, it is created with the specified elements</description></item>
    /// <item><description>If the attribute exists, the elements are added to the existing set (set union)</description></item>
    /// <item><description>Duplicate elements are automatically handled by DynamoDB</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Add multiple categories
    /// await table.Items.Update(itemId)
    ///     .AddToSet(x => x.Categories, new[] { "electronics", "sale" })
    ///     .UpdateAsync();
    /// // Translates to: ADD #categories :v0
    /// // Where :v0 = { SS: ["electronics", "sale"] }
    /// 
    /// // Add multiple scores
    /// await table.Items.Update(itemId)
    ///     .AddToSet(x => x.Scores, new[] { 100, 200, 300 })
    ///     .UpdateAsync();
    /// // Translates to: ADD #scores :v0
    /// // Where :v0 = { NS: ["100", "200", "300"] }
    /// </code>
    /// </example>
    [GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity generic parameter to the builder's entity type")]
    public static T AddToSet<T, TEntity, TElement>(
        this IWithUpdateExpression<T> builder,
        Expression<Func<TEntity, HashSet<TElement>>> setSelector,
        IEnumerable<TElement> values,
        EntityMetadata? metadata = null)
        where TEntity : class, IEntityMetadataProvider
    {
        if (setSelector == null)
            throw new ArgumentNullException(nameof(setSelector));
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        // Extract the document path from the set selector
        var documentPath = ExtractPropertyPath(setSelector.Body, builder.GetAttributeNameHelper());

        // Capture the values as a set
        var valuePlaceholder = CaptureSetValueForAdd(values, builder.GetAttributeValueHelper());

        // Build ADD expression
        var updateExpression = $"ADD {documentPath} {valuePlaceholder}";

        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Adds multiple elements to a set using DynamoDB's ADD action for UpdateItemRequestBuilder.
    /// This overload provides better type inference for update operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
    /// <param name="setSelector">Lambda expression selecting the set property (e.g., x => x.Categories).</param>
    /// <param name="values">The values to add to the set.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static UpdateItemRequestBuilder<TEntity> AddToSet<TEntity, TElement>(
        this UpdateItemRequestBuilder<TEntity> builder,
        Expression<Func<TEntity, HashSet<TElement>>> setSelector,
        IEnumerable<TElement> values,
        EntityMetadata? metadata = null)
        where TEntity : class, IDynamoDbEntity, IEntityMetadataProvider
    {
        if (setSelector == null)
            throw new ArgumentNullException(nameof(setSelector));
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        // Extract the document path from the set selector
        var documentPath = ExtractPropertyPath(setSelector.Body, builder.GetAttributeNameHelper());

        // Capture the values as a set
        var valuePlaceholder = CaptureSetValueForAdd(values, builder.GetAttributeValueHelper());

        // Build ADD expression
        var updateExpression = $"ADD {documentPath} {valuePlaceholder}";

        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Removes a single element from a set using DynamoDB's DELETE action.
    /// Generates a DELETE expression (e.g., DELETE #categories :v0).
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="setSelector">Lambda expression selecting the set property (e.g., x => x.Categories).</param>
    /// <param name="value">The value to remove from the set.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when setSelector is null.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>
    /// Translates to: DELETE #attr :val
    /// Where :val is a set containing the single element to remove.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>Only works with set types (SS, NS, BS)</description></item>
    /// <item><description>Removes only the specified element from the set</description></item>
    /// <item><description>If the element doesn't exist in the set, the operation succeeds without error</description></item>
    /// <item><description>If the attribute doesn't exist, the operation succeeds without error</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Remove single category
    /// await table.Items.Update(itemId)
    ///     .DeleteFromSet(x => x.Categories, "clearance")
    ///     .UpdateAsync();
    /// // Translates to: DELETE #categories :v0
    /// // Where :v0 = { SS: ["clearance"] }
    /// 
    /// // Remove from numeric set
    /// await table.Items.Update(itemId)
    ///     .DeleteFromSet(x => x.Scores, 50)
    ///     .UpdateAsync();
    /// // Translates to: DELETE #scores :v0
    /// // Where :v0 = { NS: ["50"] }
    /// </code>
    /// </example>
    [GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity generic parameter to the builder's entity type")]
    public static T DeleteFromSet<T, TEntity, TElement>(
        this IWithUpdateExpression<T> builder,
        Expression<Func<TEntity, HashSet<TElement>>> setSelector,
        TElement value,
        EntityMetadata? metadata = null)
        where TEntity : class, IEntityMetadataProvider
    {
        if (setSelector == null)
            throw new ArgumentNullException(nameof(setSelector));

        // Extract the document path from the set selector
        var documentPath = ExtractPropertyPath(setSelector.Body, builder.GetAttributeNameHelper());

        // Capture the value as a set
        var valuePlaceholder = CaptureSetValueForDelete(new[] { value }, builder.GetAttributeValueHelper());

        // Build DELETE expression
        var updateExpression = $"DELETE {documentPath} {valuePlaceholder}";

        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Removes a single element from a set using DynamoDB's DELETE action for UpdateItemRequestBuilder.
    /// This overload provides better type inference for update operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
    /// <param name="setSelector">Lambda expression selecting the set property (e.g., x => x.Categories).</param>
    /// <param name="value">The value to remove from the set.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static UpdateItemRequestBuilder<TEntity> DeleteFromSet<TEntity, TElement>(
        this UpdateItemRequestBuilder<TEntity> builder,
        Expression<Func<TEntity, HashSet<TElement>>> setSelector,
        TElement value,
        EntityMetadata? metadata = null)
        where TEntity : class, IDynamoDbEntity, IEntityMetadataProvider
    {
        if (setSelector == null)
            throw new ArgumentNullException(nameof(setSelector));

        // Extract the document path from the set selector
        var documentPath = ExtractPropertyPath(setSelector.Body, builder.GetAttributeNameHelper());

        // Capture the value as a set
        var valuePlaceholder = CaptureSetValueForDelete(new[] { value }, builder.GetAttributeValueHelper());

        // Build DELETE expression
        var updateExpression = $"DELETE {documentPath} {valuePlaceholder}";

        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Removes multiple elements from a set using DynamoDB's DELETE action.
    /// Generates a DELETE expression (e.g., DELETE #categories :v0).
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="setSelector">Lambda expression selecting the set property (e.g., x => x.Categories).</param>
    /// <param name="values">The values to remove from the set.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when setSelector or values is null.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>
    /// Translates to: DELETE #attr :val
    /// Where :val is a set containing all the elements to remove.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>Only works with set types (SS, NS, BS)</description></item>
    /// <item><description>Removes all specified elements from the set (set difference)</description></item>
    /// <item><description>Elements that don't exist in the set are ignored</description></item>
    /// <item><description>If the attribute doesn't exist, the operation succeeds without error</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Remove multiple categories
    /// await table.Items.Update(itemId)
    ///     .DeleteFromSet(x => x.Categories, new[] { "clearance", "discontinued" })
    ///     .UpdateAsync();
    /// // Translates to: DELETE #categories :v0
    /// // Where :v0 = { SS: ["clearance", "discontinued"] }
    /// 
    /// // Remove multiple scores
    /// await table.Items.Update(itemId)
    ///     .DeleteFromSet(x => x.Scores, new[] { 50, 75 })
    ///     .UpdateAsync();
    /// // Translates to: DELETE #scores :v0
    /// // Where :v0 = { NS: ["50", "75"] }
    /// </code>
    /// </example>
    [GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity generic parameter to the builder's entity type")]
    public static T DeleteFromSet<T, TEntity, TElement>(
        this IWithUpdateExpression<T> builder,
        Expression<Func<TEntity, HashSet<TElement>>> setSelector,
        IEnumerable<TElement> values,
        EntityMetadata? metadata = null)
        where TEntity : class, IEntityMetadataProvider
    {
        if (setSelector == null)
            throw new ArgumentNullException(nameof(setSelector));
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        // Extract the document path from the set selector
        var documentPath = ExtractPropertyPath(setSelector.Body, builder.GetAttributeNameHelper());

        // Capture the values as a set
        var valuePlaceholder = CaptureSetValueForDelete(values, builder.GetAttributeValueHelper());

        // Build DELETE expression
        var updateExpression = $"DELETE {documentPath} {valuePlaceholder}";

        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Removes multiple elements from a set using DynamoDB's DELETE action for UpdateItemRequestBuilder.
    /// This overload provides better type inference for update operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
    /// <param name="setSelector">Lambda expression selecting the set property (e.g., x => x.Categories).</param>
    /// <param name="values">The values to remove from the set.</param>
    /// <param name="metadata">Optional entity metadata for property validation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static UpdateItemRequestBuilder<TEntity> DeleteFromSet<TEntity, TElement>(
        this UpdateItemRequestBuilder<TEntity> builder,
        Expression<Func<TEntity, HashSet<TElement>>> setSelector,
        IEnumerable<TElement> values,
        EntityMetadata? metadata = null)
        where TEntity : class, IDynamoDbEntity, IEntityMetadataProvider
    {
        if (setSelector == null)
            throw new ArgumentNullException(nameof(setSelector));
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        // Extract the document path from the set selector
        var documentPath = ExtractPropertyPath(setSelector.Body, builder.GetAttributeNameHelper());

        // Capture the values as a set
        var valuePlaceholder = CaptureSetValueForDelete(values, builder.GetAttributeValueHelper());

        // Build DELETE expression
        var updateExpression = $"DELETE {documentPath} {valuePlaceholder}";

        return builder.SetUpdateExpression(updateExpression, UpdateExpressionSource.ExpressionBased);
    }

    /// <summary>
    /// Extracts the document path from a property expression.
    /// </summary>
    /// <param name="expression">The expression body (e.g., x.Categories or x.Metadata.Tags).</param>
    /// <param name="attributeNames">The attribute name helper for registering placeholders.</param>
    /// <returns>The document path string.</returns>
    private static string ExtractPropertyPath(
        Expression expression,
        AttributeNameInternal attributeNames)
    {
        var pathBuilder = new DocumentPathBuilder(attributeNames);
        var segments = new List<string>();

        // Walk the expression tree from leaf to root
        var current = expression;
        while (current != null)
        {
            switch (current)
            {
                case MemberExpression member:
                    // Property access: x.Categories or x.Metadata
                    var attributeName = GetAttributeNameFromMember(member);
                    segments.Insert(0, attributeName);
                    current = member.Expression;
                    break;

                case ParameterExpression:
                    // Reached the root parameter (x)
                    current = null;
                    break;

                default:
                    throw new UnsupportedExpressionException(
                        $"Unsupported expression type in set property path: {current.NodeType}. " +
                        "Expected property access.",
                        current);
            }
        }

        // Build the document path from segments
        foreach (var segment in segments)
        {
            pathBuilder.AddProperty(segment, segment);
        }

        return pathBuilder.Build();
    }

    /// <summary>
    /// Captures values for ADD set operations.
    /// </summary>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="values">The values to capture.</param>
    /// <param name="attributeValues">The attribute value helper.</param>
    /// <returns>The value placeholder (e.g., ":v0").</returns>
    private static string CaptureSetValueForAdd<TElement>(IEnumerable<TElement> values, AttributeValueInternal attributeValues)
    {
        var placeholder = $":v{attributeValues.AttributeValues.Count}";
        var attributeValue = ConvertToSetAttributeValue(values);
        attributeValues.AttributeValues[placeholder] = attributeValue;
        return placeholder;
    }

    /// <summary>
    /// Captures values for DELETE set operations.
    /// </summary>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="values">The values to capture.</param>
    /// <param name="attributeValues">The attribute value helper.</param>
    /// <returns>The value placeholder (e.g., ":v0").</returns>
    private static string CaptureSetValueForDelete<TElement>(IEnumerable<TElement> values, AttributeValueInternal attributeValues)
    {
        var placeholder = $":v{attributeValues.AttributeValues.Count}";
        var attributeValue = ConvertToSetAttributeValue(values);
        attributeValues.AttributeValues[placeholder] = attributeValue;
        return placeholder;
    }

    /// <summary>
    /// Converts values to a DynamoDB set AttributeValue (SS or NS).
    /// </summary>
    /// <typeparam name="TElement">The type of the set element.</typeparam>
    /// <param name="values">The values to convert.</param>
    /// <returns>The AttributeValue representing the set.</returns>
    private static Amazon.DynamoDBv2.Model.AttributeValue ConvertToSetAttributeValue<TElement>(IEnumerable<TElement> values)
    {
        var valueList = values.ToList();
        
        if (valueList.Count == 0)
        {
            throw new ArgumentException("Cannot create a set with zero elements. DynamoDB sets must contain at least one element.", nameof(values));
        }

        var elementType = typeof(TElement);

        // String set
        if (elementType == typeof(string))
        {
            return new Amazon.DynamoDBv2.Model.AttributeValue
            {
                SS = valueList.Cast<string>().ToList()
            };
        }

        // Numeric sets
        if (elementType == typeof(int) || elementType == typeof(long) || 
            elementType == typeof(decimal) || elementType == typeof(double) ||
            elementType == typeof(float) || elementType == typeof(short) ||
            elementType == typeof(byte) || elementType == typeof(uint) ||
            elementType == typeof(ulong) || elementType == typeof(ushort) ||
            elementType == typeof(sbyte))
        {
            return new Amazon.DynamoDBv2.Model.AttributeValue
            {
                NS = valueList.Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!).ToList()
            };
        }

        // For other types, try to convert to string set
        return new Amazon.DynamoDBv2.Model.AttributeValue
        {
            SS = valueList.Select(v => v?.ToString() ?? string.Empty).ToList()
        };
    }

    #endregion

}
