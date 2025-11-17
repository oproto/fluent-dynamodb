using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Translates C# lambda expressions to DynamoDB expression syntax.
/// AOT-safe implementation that analyzes expression trees without dynamic code generation.
/// </summary>
/// <remarks>
/// <para><strong>Overview:</strong></para>
/// <para>
/// The ExpressionTranslator converts C# lambda expressions into DynamoDB expression syntax,
/// enabling type-safe query building with compile-time checking. It supports all DynamoDB
/// operators and functions while maintaining AOT compatibility.
/// </para>
/// 
/// <para><strong>Supported Operators:</strong></para>
/// <list type="table">
/// <listheader><term>C# Operator</term><description>DynamoDB Syntax</description><description>Example</description></listheader>
/// <item><term>==</term><description>=</description><description>x => x.Id == "123"</description></item>
/// <item><term>!=</term><description>&lt;&gt;</description><description>x => x.Status != "DELETED"</description></item>
/// <item><term>&lt;</term><description>&lt;</description><description>x => x.Age &lt; 65</description></item>
/// <item><term>&gt;</term><description>&gt;</description><description>x => x.Score &gt; 100</description></item>
/// <item><term>&lt;=</term><description>&lt;=</description><description>x => x.Age &lt;= 18</description></item>
/// <item><term>&gt;=</term><description>&gt;=</description><description>x => x.Score &gt;= 50</description></item>
/// <item><term>&amp;&amp;</term><description>AND</description><description>x => x.Active &amp;&amp; x.Verified</description></item>
/// <item><term>||</term><description>OR</description><description>x => x.Type == "A" || x.Type == "B"</description></item>
/// <item><term>!</term><description>NOT</description><description>x => !x.Deleted</description></item>
/// </list>
/// 
/// <para><strong>Supported DynamoDB Functions:</strong></para>
/// <list type="table">
/// <listheader><term>C# Method</term><description>DynamoDB Function</description><description>Example</description></listheader>
/// <item><term>string.StartsWith()</term><description>begins_with()</description><description>x => x.Name.StartsWith("John")</description></item>
/// <item><term>string.Contains()</term><description>contains()</description><description>x => x.Email.Contains("@example.com")</description></item>
/// <item><term>Between()</term><description>BETWEEN</description><description>x => x.Age.Between(18, 65)</description></item>
/// <item><term>AttributeExists()</term><description>attribute_exists()</description><description>x => x.OptionalField.AttributeExists()</description></item>
/// <item><term>AttributeNotExists()</term><description>attribute_not_exists()</description><description>x => x.DeletedAt.AttributeNotExists()</description></item>
/// <item><term>Size()</term><description>size()</description><description>x => x.Items.Size() &gt; 0</description></item>
/// </list>
/// 
/// <para><strong>Valid Expression Patterns:</strong></para>
/// <list type="bullet">
/// <item><description>Property access: x => x.PropertyName</description></item>
/// <item><description>Constant values: x => x.Id == "USER#123"</description></item>
/// <item><description>Local variables: x => x.Id == userId</description></item>
/// <item><description>Closure captures: x => x.Id == user.Id</description></item>
/// <item><description>Method calls on captured values: x => x.Id == userId.ToString()</description></item>
/// <item><description>Complex conditions: x => (x.Active &amp;&amp; x.Score &gt; 50) || x.Premium</description></item>
/// </list>
/// 
/// <para><strong>Invalid Expression Patterns:</strong></para>
/// <list type="bullet">
/// <item><description>Assignment: x => x.Id = "123" (use == for comparison)</description></item>
/// <item><description>Method calls on entity properties: x => x.Name.ToUpper() == "JOHN"</description></item>
/// <item><description>Methods referencing entity parameter: x => x.Id == MyFunction(x)</description></item>
/// <item><description>LINQ operations on entity properties: x => x.Items.Select(i => i.Name)</description></item>
/// <item><description>Complex transformations: x => x.Items.Where(i => i.Active).Count() &gt; 0</description></item>
/// </list>
/// 
/// <para><strong>Validation Rules:</strong></para>
/// <list type="bullet">
/// <item><description>Query().Where() expressions can only reference partition key and sort key properties</description></item>
/// <item><description>WithFilter() expressions can reference any property</description></item>
/// <item><description>Properties must be mapped to DynamoDB attributes (via metadata or attributes)</description></item>
/// <item><description>Properties marked as non-queryable will be rejected</description></item>
/// </list>
/// 
/// <para><strong>Error Handling:</strong></para>
/// <list type="bullet">
/// <item><description><see cref="UnmappedPropertyException"/>: Property doesn't map to a DynamoDB attribute</description></item>
/// <item><description><see cref="InvalidKeyExpressionException"/>: Non-key property used in Query().Where()</description></item>
/// <item><description><see cref="UnsupportedExpressionException"/>: Unsupported operator, method, or pattern</description></item>
/// <item><description><see cref="ExpressionTranslationException"/>: General translation error</description></item>
/// </list>
/// 
/// <para><strong>Performance:</strong></para>
/// <para>
/// Expression translation is cached using <see cref="ExpressionCache"/> to avoid repeated analysis
/// of the same expression structure. The cache is thread-safe and stores expression templates
/// (not parameter values), so expressions with different values but the same structure benefit
/// from caching.
/// </para>
/// 
/// <para><strong>AOT Compatibility:</strong></para>
/// <para>
/// This implementation is fully AOT-compatible. It analyzes expression trees using static code
/// paths without any runtime code generation. Expression.Compile() is only used for evaluating
/// captured values (constants, variables, closures), not for entity property access.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple equality comparison
/// var translator = new ExpressionTranslator();
/// var context = new ExpressionContext(attributeValues, attributeNames, metadata, ExpressionValidationMode.KeysOnly);
/// var result = translator.Translate&lt;User&gt;(x => x.Id == "USER#123", context);
/// // Result: "#attr0 = :p0"
/// 
/// // Complex condition with multiple operators
/// result = translator.Translate&lt;User&gt;(
///     x => x.PartitionKey == userId &amp;&amp; x.SortKey.StartsWith("ORDER#") &amp;&amp; x.Amount &gt; 100,
///     context);
/// // Result: "(#attr0 = :p0) AND (begins_with(#attr1, :p1)) AND (#attr2 > :p2)"
/// 
/// // Using DynamoDB functions
/// result = translator.Translate&lt;User&gt;(
///     x => x.Age.Between(18, 65) &amp;&amp; x.Email.Contains("@example.com"),
///     context);
/// // Result: "(#attr0 BETWEEN :p0 AND :p1) AND (contains(#attr1, :p2))"
/// 
/// // With caching for repeated expressions
/// result = translator.TranslateWithCache&lt;User&gt;(x => x.Id == userId, context);
/// // First call: translates and caches
/// // Subsequent calls: returns cached result
/// </code>
/// </example>
public class ExpressionTranslator
{
    private static readonly ExpressionCache _cache = new();
    private readonly IDynamoDbLogger? _logger;
    private readonly Func<string, bool>? _isSensitiveField;

    /// <summary>
    /// Gets the global expression cache instance.
    /// </summary>
    public static ExpressionCache Cache => _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionTranslator"/> class.
    /// </summary>
    public ExpressionTranslator()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionTranslator"/> class with logging and security metadata.
    /// </summary>
    /// <param name="logger">Optional logger for expression translation diagnostics.</param>
    /// <param name="isSensitiveField">Optional function to check if a field is sensitive (typically from generated SecurityMetadata).</param>
    public ExpressionTranslator(IDynamoDbLogger? logger, Func<string, bool>? isSensitiveField = null)
    {
        _logger = logger;
        _isSensitiveField = isSensitiveField;
    }

    /// <summary>
    /// Translates a lambda expression to DynamoDB expression syntax.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried</typeparam>
    /// <param name="expression">The lambda expression to translate</param>
    /// <param name="context">The translation context</param>
    /// <returns>The DynamoDB expression string</returns>
    /// <exception cref="ArgumentNullException">Thrown when expression or context is null</exception>
    /// <exception cref="ExpressionTranslationException">Thrown when the expression cannot be translated</exception>
    public string Translate<TEntity>(
        Expression<Func<TEntity, bool>> expression,
        ExpressionContext context)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        // Get the entity parameter (the 'x' in 'x => x.Id == value')
        var entityParameter = expression.Parameters[0];

        // Visit the body of the lambda expression
        return Visit(expression.Body, entityParameter, context);
    }

    /// <summary>
    /// Translates a lambda expression to DynamoDB expression syntax with caching.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried</typeparam>
    /// <param name="expression">The lambda expression to translate</param>
    /// <param name="context">The translation context</param>
    /// <param name="useCache">Whether to use the expression cache</param>
    /// <returns>The DynamoDB expression string</returns>
    /// <exception cref="ArgumentNullException">Thrown when expression or context is null</exception>
    /// <exception cref="ExpressionTranslationException">Thrown when the expression cannot be translated</exception>
    public string TranslateWithCache<TEntity>(
        Expression<Func<TEntity, bool>> expression,
        ExpressionContext context,
        bool useCache = true)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (!useCache)
        {
            return Translate(expression, context);
        }

        // Use cache to avoid repeated translation of the same expression
        // Note: We cache the expression structure, not the parameter values
        return _cache.GetOrAdd(
            expression.Body,
            context.ValidationMode,
            () => Translate(expression, context));
    }

    /// <summary>
    /// Visits an expression node and dispatches to the appropriate handler.
    /// </summary>
    private string Visit(Expression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        return node switch
        {
            BinaryExpression binary => VisitBinary(binary, entityParameter, context),
            MemberExpression member => VisitMember(member, entityParameter, context),
            ConstantExpression constant => VisitConstant(constant, context),
            UnaryExpression unary => VisitUnary(unary, entityParameter, context),
            MethodCallExpression methodCall => VisitMethodCall(methodCall, entityParameter, context),
            _ => throw new UnsupportedExpressionException(
                $"Expression type '{node.NodeType}' is not supported in DynamoDB expressions. " +
                $"Supported types: Binary, Member, Constant, Unary, MethodCall.",
                node)
        };
    }

    /// <summary>
    /// Visits a binary expression node (operators like ==, <, >, &&, ||).
    /// </summary>
    private string VisitBinary(BinaryExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        // Handle logical operators (&&, ||)
        if (node.NodeType == ExpressionType.AndAlso || node.NodeType == ExpressionType.OrElse)
        {
            var left = Visit(node.Left, entityParameter, context);
            var right = Visit(node.Right, entityParameter, context);
            var op = node.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
            
            // Use StringBuilder to minimize allocations
            var sb = new StringBuilder(left.Length + right.Length + op.Length + 6);
            sb.Append('(').Append(left).Append(") ").Append(op).Append(" (").Append(right).Append(')');
            return sb.ToString();
        }

        // Special handling for string.CompareOrdinal(x.Property, value) >= 0 pattern
        // This allows string comparison operators in expressions
        // Example: string.CompareOrdinal(x.Type, "value") >= 0 translates to: #attr0 >= :p0
        if (node.Left is MethodCallExpression methodCall &&
            methodCall.Method.Name == "CompareOrdinal" &&
            methodCall.Method.DeclaringType == typeof(string) &&
            methodCall.Arguments.Count == 2 &&
            IsEntityPropertyAccess(methodCall.Arguments[0], entityParameter))
        {
            // Extract the property and value from CompareOrdinal
            var comparePropertyMetadata = GetPropertyMetadata(methodCall.Arguments[0], entityParameter, context);
            var attributeName = Visit(methodCall.Arguments[0], entityParameter, context);
            var compareValue = VisitWithPropertyMetadata(methodCall.Arguments[1], entityParameter, context, comparePropertyMetadata);
            
            // The right side should be 0 (the comparison result)
            // Map the comparison operator: CompareOrdinal(...) >= 0 means attr >= value
            var compareOperator = node.NodeType switch
            {
                ExpressionType.Equal => "=",           // CompareOrdinal(...) == 0 -> attr = value
                ExpressionType.NotEqual => "<>",       // CompareOrdinal(...) != 0 -> attr <> value
                ExpressionType.LessThan => "<",        // CompareOrdinal(...) < 0 -> attr < value
                ExpressionType.LessThanOrEqual => "<=", // CompareOrdinal(...) <= 0 -> attr <= value
                ExpressionType.GreaterThan => ">",     // CompareOrdinal(...) > 0 -> attr > value
                ExpressionType.GreaterThanOrEqual => ">=", // CompareOrdinal(...) >= 0 -> attr >= value
                _ => throw new UnsupportedExpressionException(
                    $"Binary operator '{node.NodeType}' is not supported with string.CompareOrdinal.",
                    node)
            };
            
            // Use StringBuilder to minimize allocations
            var compareBuilder = new StringBuilder(attributeName.Length + compareValue.Length + compareOperator.Length + 2);
            compareBuilder.Append(attributeName).Append(' ').Append(compareOperator).Append(' ').Append(compareValue);
            return compareBuilder.ToString();
        }

        // Handle comparison operators (==, !=, <, >, <=, >=)
        // For comparisons, we need to determine which side is the property and which is the value
        // to apply formatting correctly
        PropertyMetadata? propertyMetadata = null;
        
        // Check if left side is entity property access
        if (IsEntityPropertyAccess(node.Left, entityParameter) && context.EntityMetadata != null)
        {
            var propertyName = ((MemberExpression)node.Left).Member.Name;
            propertyMetadata = context.EntityMetadata.Properties
                .FirstOrDefault(p => p.PropertyName == propertyName);
        }
        // Check if right side is entity property access
        else if (IsEntityPropertyAccess(node.Right, entityParameter) && context.EntityMetadata != null)
        {
            var propertyName = ((MemberExpression)node.Right).Member.Name;
            propertyMetadata = context.EntityMetadata.Properties
                .FirstOrDefault(p => p.PropertyName == propertyName);
        }
        
        var leftSide = VisitWithPropertyMetadata(node.Left, entityParameter, context, propertyMetadata);
        var rightSide = VisitWithPropertyMetadata(node.Right, entityParameter, context, propertyMetadata);

        var dynamoDbOperator = node.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            _ => throw new UnsupportedExpressionException(
                $"Binary operator '{node.NodeType}' is not supported in DynamoDB expressions. " +
                $"Supported operators: ==, !=, <, >, <=, >=, &&, ||.",
                node)
        };

        // Use StringBuilder to minimize allocations
        var builder = new StringBuilder(leftSide.Length + rightSide.Length + dynamoDbOperator.Length + 2);
        builder.Append(leftSide).Append(' ').Append(dynamoDbOperator).Append(' ').Append(rightSide);
        return builder.ToString();
    }
    
    /// <summary>
    /// Visits an expression node with property metadata for format application.
    /// </summary>
    private string VisitWithPropertyMetadata(Expression node, ParameterExpression entityParameter, ExpressionContext context, PropertyMetadata? propertyMetadata)
    {
        // For entity property access, don't pass metadata (it's the attribute name, not a value)
        if (IsEntityPropertyAccess(node, entityParameter))
        {
            return Visit(node, entityParameter, context);
        }
        
        // Handle type conversions (like nullable to non-nullable) by unwrapping and continuing
        if (node is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            return VisitWithPropertyMetadata(unary.Operand, entityParameter, context, propertyMetadata);
        }
        
        // For value expressions, evaluate and capture with format
        if (node is ConstantExpression constant)
        {
            return CaptureValue(constant.Value, context, propertyMetadata);
        }
        
        if (node is MemberExpression member && !IsEntityPropertyAccess(member, entityParameter))
        {
            var value = EvaluateExpression(member);
            return CaptureValue(value, context, propertyMetadata);
        }
        
        if (node is MethodCallExpression methodCall && !ReferencesEntityParameter(methodCall, entityParameter))
        {
            var value = EvaluateExpression(methodCall);
            return CaptureValue(value, context, propertyMetadata);
        }
        
        // For other expressions, use standard visit
        return Visit(node, entityParameter, context);
    }

    /// <summary>
    /// Visits a member expression node (property access like x.PropertyName).
    /// </summary>
    private string VisitMember(MemberExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        // Check if this is entity property access (x.PropertyName)
        if (IsEntityPropertyAccess(node, entityParameter))
        {
            var propertyName = node.Member.Name;

            // Validate property against entity metadata if available
            if (context.EntityMetadata != null)
            {
                var propertyMetadata = context.EntityMetadata.Properties
                    .FirstOrDefault(p => p.PropertyName == propertyName);

                if (propertyMetadata == null)
                {
                    throw new UnmappedPropertyException(
                        propertyName,
                        entityParameter.Type,
                        node);
                }

                // Check if property is queryable (has supported operations)
                if (propertyMetadata.SupportedOperations != null && 
                    propertyMetadata.SupportedOperations.Length == 0)
                {
                    throw new UnsupportedExpressionException(
                        $"Property '{propertyName}' is marked as non-queryable and cannot be used in expressions. " +
                        $"The property has no supported DynamoDB operations defined.",
                        node);
                }

                // Validate key-only mode for Query().Where()
                if (context.ValidationMode == ExpressionValidationMode.KeysOnly)
                {
                    var isKey = propertyMetadata.IsPartitionKey || propertyMetadata.IsSortKey;
                    if (!isKey)
                    {
                        throw new InvalidKeyExpressionException(propertyName, node);
                    }
                }

                // Use the DynamoDB attribute name from metadata
                propertyName = propertyMetadata.AttributeName;
            }

            // Generate attribute name placeholder - minimize allocations
            var count = context.AttributeNames.AttributeNames.Count;
            var attributeNamePlaceholder = count < 10 
                ? string.Concat("#attr", count.ToString()) 
                : $"#attr{count}";
            
            context.AttributeNames.WithAttribute(attributeNamePlaceholder, propertyName);
            return attributeNamePlaceholder;
        }

        // This is value capture (accessing a variable or closure)
        // Evaluate the member expression to get its value
        var value = EvaluateExpression(node);
        return CaptureValue(value, context, propertyMetadata: null);
    }

    /// <summary>
    /// Visits a constant expression node.
    /// </summary>
    private string VisitConstant(ConstantExpression node, ExpressionContext context)
    {
        return CaptureValue(node.Value, context, propertyMetadata: null);
    }

    /// <summary>
    /// Visits a unary expression node (operators like !).
    /// </summary>
    private string VisitUnary(UnaryExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            var operand = Visit(node.Operand, entityParameter, context);
            
            // Use StringBuilder to minimize allocations
            var sb = new StringBuilder(operand.Length + 6);
            sb.Append("NOT (").Append(operand).Append(')');
            return sb.ToString();
        }

        // Handle type conversions (like nullable to non-nullable)
        if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked)
        {
            return Visit(node.Operand, entityParameter, context);
        }

        throw new UnsupportedExpressionException(
            $"Unary operator '{node.NodeType}' is not supported in DynamoDB expressions. " +
            $"Supported operators: ! (NOT).",
            node);
    }

    /// <summary>
    /// Visits a method call expression node.
    /// </summary>
    private string VisitMethodCall(MethodCallExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        // Check if this is a DynamoDB function call (string.StartsWith, string.Contains, Between, etc.)
        if (IsDynamoDbFunction(node, entityParameter, context, out var dynamoDbFunction))
        {
            return dynamoDbFunction!;
        }

        // Reject method calls that reference the entity parameter
        if (ReferencesEntityParameter(node, entityParameter))
        {
            throw new UnsupportedExpressionException(
                $"Method '{node.Method.Name}' cannot reference the entity parameter or its properties. " +
                $"DynamoDB expressions cannot execute C# methods with entity data. " +
                $"Only constants and captured variables are allowed on the right side of comparisons. " +
                $"Example: 'x => x.Id == userId' is valid, but 'x => x.Id == myFunction(x)' is not.",
                node);
        }

        // If the method doesn't reference the entity parameter, it's a value capture
        // Evaluate the method call and capture its result
        var value = EvaluateExpression(node);
        return CaptureValue(value, context, propertyMetadata: null);
    }

    /// <summary>
    /// Checks if a method call is a geospatial query method from GeoHashQueryExtensions.
    /// </summary>
    /// <param name="node">The method call expression.</param>
    /// <returns>True if this is a geospatial method, false otherwise.</returns>
    private bool IsGeospatialMethod(MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;
        return declaringType?.FullName == "Oproto.FluentDynamoDb.Geospatial.GeoHash.GeoHashQueryExtensions";
    }

    /// <summary>
    /// Checks if a method call is a DynamoDB function and translates it.
    /// </summary>
    /// <param name="node">The method call expression.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="dynamoDbFunction">The translated DynamoDB function string.</param>
    /// <returns>True if this is a DynamoDB function, false otherwise.</returns>
    private bool IsDynamoDbFunction(MethodCallExpression node, ParameterExpression entityParameter, ExpressionContext context, out string? dynamoDbFunction)
    {
        dynamoDbFunction = null;

        // Check if this is a geospatial method
        if (IsGeospatialMethod(node))
        {
            dynamoDbFunction = TranslateGeospatialMethod(node, entityParameter, context);
            return true;
        }

        // string.StartsWith(value) -> begins_with(attr, value)
        if (node.Method.Name == "StartsWith" && 
            node.Method.DeclaringType == typeof(string) &&
            node.Arguments.Count == 1)
        {
            // The object is the string property (x.Name)
            // The argument is the value to check
            if (node.Object != null && IsEntityPropertyAccess(node.Object, entityParameter))
            {
                var propertyMetadata = GetPropertyMetadata(node.Object, entityParameter, context);
                var attributeName = Visit(node.Object, entityParameter, context);
                var value = VisitWithPropertyMetadata(node.Arguments[0], entityParameter, context, propertyMetadata);
                
                // Use StringBuilder to minimize allocations
                var sb = new StringBuilder(attributeName.Length + value.Length + 16);
                sb.Append("begins_with(").Append(attributeName).Append(", ").Append(value).Append(')');
                dynamoDbFunction = sb.ToString();
                return true;
            }
        }

        // string.Contains(value) -> contains(attr, value)
        if (node.Method.Name == "Contains" && 
            node.Method.DeclaringType == typeof(string) &&
            node.Arguments.Count == 1)
        {
            // The object is the string property (x.Tags)
            // The argument is the value to check
            if (node.Object != null && IsEntityPropertyAccess(node.Object, entityParameter))
            {
                var propertyMetadata = GetPropertyMetadata(node.Object, entityParameter, context);
                var attributeName = Visit(node.Object, entityParameter, context);
                var value = VisitWithPropertyMetadata(node.Arguments[0], entityParameter, context, propertyMetadata);
                
                // Use StringBuilder to minimize allocations
                var sb = new StringBuilder(attributeName.Length + value.Length + 13);
                sb.Append("contains(").Append(attributeName).Append(", ").Append(value).Append(')');
                dynamoDbFunction = sb.ToString();
                return true;
            }
        }

        // string.CompareOrdinal(str1, str2) - used for string comparisons in expressions
        // This is NOT a DynamoDB function - it's handled specially in VisitBinary
        // We don't handle it here

        // Between(low, high) -> attr BETWEEN low AND high
        if (node.Method.Name == "Between" && 
            node.Method.DeclaringType == typeof(DynamoDbExpressionExtensions) &&
            node.Arguments.Count == 3)
        {
            // First argument is the value (x.Age)
            // Second and third arguments are low and high bounds
            if (IsEntityPropertyAccess(node.Arguments[0], entityParameter))
            {
                var propertyMetadata = GetPropertyMetadata(node.Arguments[0], entityParameter, context);
                var attributeName = Visit(node.Arguments[0], entityParameter, context);
                var low = VisitWithPropertyMetadata(node.Arguments[1], entityParameter, context, propertyMetadata);
                var high = VisitWithPropertyMetadata(node.Arguments[2], entityParameter, context, propertyMetadata);
                
                // Use StringBuilder to minimize allocations
                var sb = new StringBuilder(attributeName.Length + low.Length + high.Length + 17);
                sb.Append(attributeName).Append(" BETWEEN ").Append(low).Append(" AND ").Append(high);
                dynamoDbFunction = sb.ToString();
                return true;
            }
        }

        // AttributeExists() -> attribute_exists(attr)
        if (node.Method.Name == "AttributeExists" && 
            node.Method.DeclaringType == typeof(DynamoDbExpressionExtensions) &&
            node.Arguments.Count == 1)
        {
            // The argument is the property (x.OptionalField)
            if (IsEntityPropertyAccess(node.Arguments[0], entityParameter))
            {
                var attributeName = Visit(node.Arguments[0], entityParameter, context);
                
                // Use StringBuilder to minimize allocations
                var sb = new StringBuilder(attributeName.Length + 19);
                sb.Append("attribute_exists(").Append(attributeName).Append(')');
                dynamoDbFunction = sb.ToString();
                return true;
            }
        }

        // AttributeNotExists() -> attribute_not_exists(attr)
        if (node.Method.Name == "AttributeNotExists" && 
            node.Method.DeclaringType == typeof(DynamoDbExpressionExtensions) &&
            node.Arguments.Count == 1)
        {
            // The argument is the property (x.OptionalField)
            if (IsEntityPropertyAccess(node.Arguments[0], entityParameter))
            {
                var attributeName = Visit(node.Arguments[0], entityParameter, context);
                
                // Use StringBuilder to minimize allocations
                var sb = new StringBuilder(attributeName.Length + 23);
                sb.Append("attribute_not_exists(").Append(attributeName).Append(')');
                dynamoDbFunction = sb.ToString();
                return true;
            }
        }

        // Size() -> size(attr)
        if (node.Method.Name == "Size" && 
            node.Method.DeclaringType == typeof(DynamoDbExpressionExtensions) &&
            node.Arguments.Count == 1)
        {
            // The argument is the collection property (x.Items)
            if (IsEntityPropertyAccess(node.Arguments[0], entityParameter))
            {
                var attributeName = Visit(node.Arguments[0], entityParameter, context);
                
                // Use StringBuilder to minimize allocations
                var sb = new StringBuilder(attributeName.Length + 7);
                sb.Append("size(").Append(attributeName).Append(')');
                dynamoDbFunction = sb.ToString();
                return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// Gets property metadata for an entity property access expression.
    /// </summary>
    private PropertyMetadata? GetPropertyMetadata(Expression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        if (!IsEntityPropertyAccess(node, entityParameter) || context.EntityMetadata == null)
        {
            return null;
        }
        
        var propertyName = ((MemberExpression)node).Member.Name;
        return context.EntityMetadata.Properties
            .FirstOrDefault(p => p.PropertyName == propertyName);
    }

    /// <summary>
    /// Checks if an expression references the entity parameter.
    /// </summary>
    private bool ReferencesEntityParameter(Expression node, ParameterExpression entityParameter)
    {
        // Check if this node is the entity parameter itself
        if (node == entityParameter)
            return true;

        // Recursively check child nodes
        return node switch
        {
            MemberExpression member => ReferencesEntityParameter(member.Expression!, entityParameter),
            MethodCallExpression method => 
                (method.Object != null && ReferencesEntityParameter(method.Object, entityParameter)) ||
                method.Arguments.Any(arg => ReferencesEntityParameter(arg, entityParameter)),
            UnaryExpression unary => ReferencesEntityParameter(unary.Operand, entityParameter),
            BinaryExpression binary => 
                ReferencesEntityParameter(binary.Left, entityParameter) ||
                ReferencesEntityParameter(binary.Right, entityParameter),
            _ => false
        };
    }

    /// <summary>
    /// Checks if a member expression is accessing an entity property.
    /// </summary>
    private bool IsEntityPropertyAccess(Expression node, ParameterExpression entityParameter)
    {
        if (node is not MemberExpression member)
            return false;

        // Check if the member is directly on the entity parameter (x.PropertyName)
        return member.Expression == entityParameter;
    }

    /// <summary>
    /// Evaluates an expression to get its runtime value.
    /// This is used for value capture (constants, variables, closures).
    /// </summary>
    private object? EvaluateExpression(Expression expression)
    {
        try
        {
            // For constant expressions, just return the value
            if (expression is ConstantExpression constant)
                return constant.Value;

            // For other expressions, we need to compile and execute them
            // This is safe for AOT because we're only compiling value expressions,
            // not entity property access expressions
            var lambda = Expression.Lambda<Func<object?>>(
                Expression.Convert(expression, typeof(object)));
            var compiled = lambda.Compile();
            return compiled();
        }
        catch (Exception ex)
        {
            throw new ExpressionTranslationException(
                $"Failed to evaluate expression for value capture: {ex.Message}",
                expression);
        }
    }

    /// <summary>
    /// Captures a value and generates a parameter placeholder.
    /// </summary>
    /// <param name="value">The value to capture.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="propertyMetadata">Optional property metadata for format application.</param>
    private string CaptureValue(object? value, ExpressionContext context, PropertyMetadata? propertyMetadata)
    {
        // Apply format if specified
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormat(value, propertyMetadata.Format, propertyMetadata.PropertyName);
        }
        
        // Convert the value to an AttributeValue
        var attributeValue = ConvertToAttributeValue(value);

        // Generate a unique parameter name
        var parameterName = context.ParameterGenerator.GenerateParameterName();

        // Add to the context
        context.AttributeValues.AttributeValues.Add(parameterName, attributeValue);

        // Log parameter capture with sensitive data redaction
        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            var attributeName = propertyMetadata?.AttributeName;
            var isSensitive = attributeName != null && _isSensitiveField != null && _isSensitiveField(attributeName);
            
            var valueToLog = isSensitive ? "[REDACTED]" : (value?.ToString() ?? "null");
            
            _logger.LogDebug(
                LogEventIds.ExpressionTranslation,
                "Expression parameter {ParameterName} = {Value} (Property: {PropertyName})",
                parameterName,
                valueToLog,
                propertyMetadata?.PropertyName ?? "unknown");
        }

        return parameterName;
    }
    
    /// <summary>
    /// Applies a format string to a value.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">The format string to apply.</param>
    /// <param name="propertyName">The property name for error messages.</param>
    /// <returns>The formatted value.</returns>
    /// <exception cref="FormatException">Thrown when the format string is invalid for the value type.</exception>
    private object ApplyFormat(object value, string format, string propertyName)
    {
        try
        {
            return value switch
            {
                DateTime dt => dt.ToString(format, CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString(format, CultureInfo.InvariantCulture),
                decimal d => d.ToString(format, CultureInfo.InvariantCulture),
                double d => d.ToString(format, CultureInfo.InvariantCulture),
                float f => f.ToString(format, CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(format, CultureInfo.InvariantCulture),
                _ => value
            };
        }
        catch (FormatException ex)
        {
            throw new FormatException(
                $"Invalid format string '{format}' for property '{propertyName}' of type {value.GetType().Name}. " +
                $"Error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Converts a .NET value to a DynamoDB AttributeValue.
    /// </summary>
    private AttributeValue ConvertToAttributeValue(object? value)
    {
        if (value == null)
            return new AttributeValue { NULL = true };

        return value switch
        {
            string s => new AttributeValue { S = s },
            bool b => new AttributeValue { BOOL = b, IsBOOLSet = true },
            byte b => new AttributeValue { N = b.ToString(CultureInfo.InvariantCulture) },
            sbyte sb => new AttributeValue { N = sb.ToString(CultureInfo.InvariantCulture) },
            short s => new AttributeValue { N = s.ToString(CultureInfo.InvariantCulture) },
            ushort us => new AttributeValue { N = us.ToString(CultureInfo.InvariantCulture) },
            int i => new AttributeValue { N = i.ToString(CultureInfo.InvariantCulture) },
            uint ui => new AttributeValue { N = ui.ToString(CultureInfo.InvariantCulture) },
            long l => new AttributeValue { N = l.ToString(CultureInfo.InvariantCulture) },
            ulong ul => new AttributeValue { N = ul.ToString(CultureInfo.InvariantCulture) },
            float f => new AttributeValue { N = f.ToString(CultureInfo.InvariantCulture) },
            double d => new AttributeValue { N = d.ToString(CultureInfo.InvariantCulture) },
            decimal dec => new AttributeValue { N = dec.ToString(CultureInfo.InvariantCulture) },
            DateTime dt => new AttributeValue { S = dt.ToString("o", CultureInfo.InvariantCulture) },
            DateTimeOffset dto => new AttributeValue { S = dto.ToString("o", CultureInfo.InvariantCulture) },
            Guid g => new AttributeValue { S = g.ToString() },
            Enum e => new AttributeValue { S = e.ToString() },
            _ => new AttributeValue { S = value.ToString() ?? string.Empty }
        };
    }

    /// <summary>
    /// Evaluates a constant expression in an AOT-safe manner.
    /// This method handles constant expressions, member access on constants, and simple expressions
    /// that can be compiled without dynamic code generation.
    /// </summary>
    /// <typeparam name="T">The expected type of the result.</typeparam>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>The evaluated value.</returns>
    /// <exception cref="ExpressionTranslationException">Thrown when the expression cannot be evaluated.</exception>
    private T EvaluateConstantExpression<T>(Expression expression)
    {
        try
        {
            // Handle constant expressions directly
            if (expression is ConstantExpression constant)
            {
                return (T)constant.Value!;
            }

            // Handle member access on constants (e.g., variable references)
            if (expression is MemberExpression member && member.Expression is ConstantExpression memberConstant)
            {
                var field = memberConstant.Value?.GetType().GetField(member.Member.Name);
                if (field != null)
                {
                    return (T)field.GetValue(memberConstant.Value)!;
                }

                var property = memberConstant.Value?.GetType().GetProperty(member.Member.Name);
                if (property != null)
                {
                    return (T)property.GetValue(memberConstant.Value)!;
                }
            }

            // For other cases, compile and invoke (AOT-safe for simple expressions)
            var lambda = Expression.Lambda<Func<T>>(expression);
            var compiled = lambda.Compile();
            return compiled();
        }
        catch (Exception ex)
        {
            throw new ExpressionTranslationException(
                $"Failed to evaluate constant expression of type {typeof(T).Name}: {ex.Message}",
                expression);
        }
    }

    /// <summary>
    /// Gets the GeoHash precision for a property from its metadata.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The precision value, or 6 if not specified.</returns>
    private int GetPrecisionForProperty(Expression propertyExpression, ParameterExpression entityParameter, ExpressionContext context)
    {
        var propertyMetadata = GetPropertyMetadata(propertyExpression, entityParameter, context);
        return propertyMetadata?.GeoHashPrecision ?? 6;
    }

    /// <summary>
    /// Translates a geospatial method call to a DynamoDB BETWEEN expression.
    /// </summary>
    /// <param name="node">The method call expression.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The translated DynamoDB expression string.</returns>
    /// <exception cref="UnsupportedExpressionException">Thrown when the geospatial method is not supported.</exception>
    private string TranslateGeospatialMethod(MethodCallExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        var methodName = node.Method.Name;

        return methodName switch
        {
            "WithinDistanceMeters" => TranslateWithinDistance(node, entityParameter, context, distanceUnit: "meters"),
            "WithinDistanceKilometers" => TranslateWithinDistance(node, entityParameter, context, distanceUnit: "kilometers"),
            "WithinDistanceMiles" => TranslateWithinDistance(node, entityParameter, context, distanceUnit: "miles"),
            "WithinBoundingBox" => TranslateWithinBoundingBox(node, entityParameter, context),
            _ => throw new UnsupportedExpressionException(
                $"Geospatial method '{methodName}' is not supported. " +
                $"Supported methods: WithinDistanceMeters, WithinDistanceKilometers, WithinDistanceMiles, WithinBoundingBox.",
                methodName,
                node)
        };
    }

    /// <summary>
    /// Translates a WithinDistance method call to a DynamoDB BETWEEN expression.
    /// </summary>
    private string TranslateWithinDistance(MethodCallExpression node, ParameterExpression entityParameter, ExpressionContext context, string distanceUnit)
    {
        // WithinDistance methods have 3 arguments:
        // 0: this GeoLocation (the property being queried)
        // 1: GeoLocation center
        // 2: double distance
        if (node.Arguments.Count != 3)
        {
            throw new UnsupportedExpressionException(
                $"WithinDistance method expects 3 arguments but got {node.Arguments.Count}.",
                node.Method.Name,
                node);
        }

        var locationExpr = node.Arguments[0];
        var centerExpr = node.Arguments[1];
        var distanceExpr = node.Arguments[2];

        // Ensure the first argument is an entity property access
        if (!IsEntityPropertyAccess(locationExpr, entityParameter))
        {
            throw new UnsupportedExpressionException(
                "WithinDistance method must be called on an entity property (e.g., x.Location.WithinDistance(...)).",
                node.Method.Name,
                node);
        }

        // Evaluate center and distance - these must be constants or simple expressions
        var center = EvaluateConstantExpression<object>(centerExpr);
        var distance = EvaluateConstantExpression<double>(distanceExpr);

        // Convert distance to meters based on unit
        var distanceMeters = distanceUnit switch
        {
            "meters" => distance,
            "kilometers" => distance * 1000.0,
            "miles" => distance * 1609.344,
            _ => throw new ArgumentException($"Unknown distance unit: {distanceUnit}")
        };

        // Get the precision for this property
        var precision = GetPrecisionForProperty(locationExpr, entityParameter, context);

        // Create bounding box and get GeoHash range
        // We need to use reflection to call the GeoBoundingBox methods since we can't reference the geospatial package directly
        var geoBoundingBoxType = center.GetType().Assembly.GetType("Oproto.FluentDynamoDb.Geospatial.GeoBoundingBox");
        if (geoBoundingBoxType == null)
        {
            throw new ExpressionTranslationException(
                "GeoBoundingBox type not found. Ensure Oproto.FluentDynamoDb.Geospatial package is referenced.",
                node);
        }

        var fromCenterMethod = geoBoundingBoxType.GetMethod("FromCenterAndDistanceMeters");
        if (fromCenterMethod == null)
        {
            throw new ExpressionTranslationException(
                "GeoBoundingBox.FromCenterAndDistanceMeters method not found.",
                node);
        }

        var bbox = fromCenterMethod.Invoke(null, new[] { center, distanceMeters });
        if (bbox == null)
        {
            throw new ExpressionTranslationException(
                "Failed to create bounding box from center and distance.",
                node);
        }

        // Get GeoHash range from bounding box
        var geoHashBoundingBoxExtensionsType = center.GetType().Assembly.GetType("Oproto.FluentDynamoDb.Geospatial.GeoHash.GeoHashBoundingBoxExtensions");
        if (geoHashBoundingBoxExtensionsType == null)
        {
            throw new ExpressionTranslationException(
                "GeoHashBoundingBoxExtensions type not found.",
                node);
        }

        var getGeoHashRangeMethod = geoHashBoundingBoxExtensionsType.GetMethod("GetGeoHashRange");
        if (getGeoHashRangeMethod == null)
        {
            throw new ExpressionTranslationException(
                "GetGeoHashRange method not found.",
                node);
        }

        var rangeResult = getGeoHashRangeMethod.Invoke(null, new[] { bbox, precision });
        if (rangeResult == null)
        {
            throw new ExpressionTranslationException(
                "Failed to get GeoHash range from bounding box.",
                node);
        }

        // Extract minHash and maxHash from the tuple
        var rangeType = rangeResult.GetType();
        var minHashProperty = rangeType.GetProperty("Item1");
        var minHashField = rangeType.GetField("Item1");
        var maxHashProperty = rangeType.GetProperty("Item2");
        var maxHashField = rangeType.GetField("Item2");

        var minHash = (minHashProperty?.GetValue(rangeResult) ?? minHashField?.GetValue(rangeResult)) as string;
        var maxHash = (maxHashProperty?.GetValue(rangeResult) ?? maxHashField?.GetValue(rangeResult)) as string;

        if (minHash == null || maxHash == null)
        {
            throw new ExpressionTranslationException(
                "Failed to extract GeoHash range values.",
                node);
        }

        // Translate location property to attribute name
        var locationField = Visit(locationExpr, entityParameter, context);

        // Generate parameter names
        var minParam = context.ParameterGenerator.GenerateParameterName();
        var maxParam = context.ParameterGenerator.GenerateParameterName();

        // Add to parameter dictionary
        context.AttributeValues.AttributeValues[minParam] = new AttributeValue { S = minHash };
        context.AttributeValues.AttributeValues[maxParam] = new AttributeValue { S = maxHash };

        // Return BETWEEN expression
        var sb = new StringBuilder(locationField.Length + minParam.Length + maxParam.Length + 17);
        sb.Append(locationField).Append(" BETWEEN ").Append(minParam).Append(" AND ").Append(maxParam);
        return sb.ToString();
    }

    /// <summary>
    /// Translates a WithinBoundingBox method call to a DynamoDB BETWEEN expression.
    /// </summary>
    private string TranslateWithinBoundingBox(MethodCallExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        // WithinBoundingBox has two overloads:
        // 1. WithinBoundingBox(GeoBoundingBox boundingBox) - 2 arguments
        // 2. WithinBoundingBox(GeoLocation southwest, GeoLocation northeast) - 3 arguments
        if (node.Arguments.Count != 2 && node.Arguments.Count != 3)
        {
            throw new UnsupportedExpressionException(
                $"WithinBoundingBox method expects 2 or 3 arguments but got {node.Arguments.Count}.",
                node.Method.Name,
                node);
        }

        var locationExpr = node.Arguments[0];

        // Ensure the first argument is an entity property access
        if (!IsEntityPropertyAccess(locationExpr, entityParameter))
        {
            throw new UnsupportedExpressionException(
                "WithinBoundingBox method must be called on an entity property (e.g., x.Location.WithinBoundingBox(...)).",
                node.Method.Name,
                node);
        }

        object bbox;

        if (node.Arguments.Count == 2)
        {
            // Single GeoBoundingBox parameter
            var bboxExpr = node.Arguments[1];
            bbox = EvaluateConstantExpression<object>(bboxExpr);
        }
        else
        {
            // Two GeoLocation parameters (southwest, northeast)
            var southwestExpr = node.Arguments[1];
            var northeastExpr = node.Arguments[2];

            var southwest = EvaluateConstantExpression<object>(southwestExpr);
            var northeast = EvaluateConstantExpression<object>(northeastExpr);

            // Create GeoBoundingBox from the two corners
            var geoBoundingBoxType = southwest.GetType().Assembly.GetType("Oproto.FluentDynamoDb.Geospatial.GeoBoundingBox");
            if (geoBoundingBoxType == null)
            {
                throw new ExpressionTranslationException(
                    "GeoBoundingBox type not found. Ensure Oproto.FluentDynamoDb.Geospatial package is referenced.",
                    node);
            }

            var constructor = geoBoundingBoxType.GetConstructor(new[] { southwest.GetType(), northeast.GetType() });
            if (constructor == null)
            {
                throw new ExpressionTranslationException(
                    "GeoBoundingBox constructor not found.",
                    node);
            }

            bbox = constructor.Invoke(new[] { southwest, northeast });
            if (bbox == null)
            {
                throw new ExpressionTranslationException(
                    "Failed to create bounding box from southwest and northeast corners.",
                    node);
            }
        }

        // Get the precision for this property
        var precision = GetPrecisionForProperty(locationExpr, entityParameter, context);

        // Get GeoHash range from bounding box
        var geoHashBoundingBoxExtensionsType = bbox.GetType().Assembly.GetType("Oproto.FluentDynamoDb.Geospatial.GeoHash.GeoHashBoundingBoxExtensions");
        if (geoHashBoundingBoxExtensionsType == null)
        {
            throw new ExpressionTranslationException(
                "GeoHashBoundingBoxExtensions type not found.",
                node);
        }

        var getGeoHashRangeMethod = geoHashBoundingBoxExtensionsType.GetMethod("GetGeoHashRange");
        if (getGeoHashRangeMethod == null)
        {
            throw new ExpressionTranslationException(
                "GetGeoHashRange method not found.",
                node);
        }

        var rangeResult = getGeoHashRangeMethod.Invoke(null, new[] { bbox, precision });
        if (rangeResult == null)
        {
            throw new ExpressionTranslationException(
                "Failed to get GeoHash range from bounding box.",
                node);
        }

        // Extract minHash and maxHash from the tuple
        var rangeType = rangeResult.GetType();
        var minHashProperty = rangeType.GetProperty("Item1");
        var minHashField = rangeType.GetField("Item1");
        var maxHashProperty = rangeType.GetProperty("Item2");
        var maxHashField = rangeType.GetField("Item2");

        var minHash = (minHashProperty?.GetValue(rangeResult) ?? minHashField?.GetValue(rangeResult)) as string;
        var maxHash = (maxHashProperty?.GetValue(rangeResult) ?? maxHashField?.GetValue(rangeResult)) as string;

        if (minHash == null || maxHash == null)
        {
            throw new ExpressionTranslationException(
                "Failed to extract GeoHash range values.",
                node);
        }

        // Translate location property to attribute name
        var locationField = Visit(locationExpr, entityParameter, context);

        // Generate parameter names
        var minParam = context.ParameterGenerator.GenerateParameterName();
        var maxParam = context.ParameterGenerator.GenerateParameterName();

        // Add to parameter dictionary
        context.AttributeValues.AttributeValues[minParam] = new AttributeValue { S = minHash };
        context.AttributeValues.AttributeValues[maxParam] = new AttributeValue { S = maxHash };

        // Return BETWEEN expression
        var sb = new StringBuilder(locationField.Length + minParam.Length + maxParam.Length + 17);
        sb.Append(locationField).Append(" BETWEEN ").Append(minParam).Append(" AND ").Append(maxParam);
        return sb.ToString();
    }
}
