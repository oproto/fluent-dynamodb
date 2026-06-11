using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Metadata;

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
    private readonly FluentDynamoDbOptions? _options;
    
    /// <summary>
    /// Sentinel value indicating "skip due to true in OR pattern".
    /// This is absorbing for OR (true OR x = true) but identity for AND (true AND x = x).
    /// </summary>
    private const string SkipDueToTrueInOr = "\0SKIP_TRUE_OR\0";
    
    /// <summary>
    /// Sentinel value indicating "skip due to false in AND pattern".
    /// This is identity for OR (false OR x = x) but absorbing for AND (false AND x = false).
    /// </summary>
    private const string SkipDueToFalseInAnd = "\0SKIP_FALSE_AND\0";

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
    /// Initializes a new instance of the <see cref="ExpressionTranslator"/> class with FluentDynamoDbOptions.
    /// </summary>
    /// <param name="options">The FluentDynamoDb configuration options. If null, uses default options.</param>
    /// <param name="isSensitiveField">Optional function to check if a field is sensitive (typically from generated SecurityMetadata).</param>
    public ExpressionTranslator(FluentDynamoDbOptions? options, Func<string, bool>? isSensitiveField = null)
    {
        _options = options;
        _logger = options?.Logger;
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

        // Check if the body is a bare boolean member expression (e.g., x => x.IsActive)
        var body = expression.Body;
        // Strip potential Convert/ConvertChecked wrapper
        var actualBody = body;
        if (actualBody.NodeType == ExpressionType.Convert || actualBody.NodeType == ExpressionType.ConvertChecked)
        {
            actualBody = ((UnaryExpression)actualBody).Operand;
        }
        
        if (actualBody is MemberExpression
            && actualBody.Type == typeof(bool)
            && IsEntityPropertyAccess(actualBody, entityParameter))
        {
            // Visit the body to get the attribute path, then translate as equality with true
            var attributePath = Visit(body, entityParameter, context);
            return TranslateBooleanMemberAsCondition(attributePath, true, context);
        }

        // Visit the body of the lambda expression
        var result = Visit(expression.Body, entityParameter, context);
        
        // Convert sentinel values to empty string for the final result
        if (result == SkipDueToTrueInOr || result == SkipDueToFalseInAnd)
        {
            return string.Empty;
        }
        
        return result;
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
        // Check for dynamic field indexer access: x.DynamicFields["fieldName"]
        if (IsDynamicFieldIndexerAccess(node, entityParameter, out var dynamicFieldName))
        {
            return TranslateDynamicFieldAccess(dynamicFieldName!, context);
        }

        // Check for list indexer access via MethodCallExpression: x.Tags[0] or x.Metadata.Keywords[0]
        if (IsListIndexerMethodCall(node, entityParameter, out var listExpr, out var listIndex))
        {
            return BuildListIndexPath(listExpr!, listIndex!.Value, entityParameter, context);
        }

        return node switch
        {
            BinaryExpression binary => VisitBinary(binary, entityParameter, context),
            MemberExpression member => VisitMember(member, entityParameter, context),
            ConstantExpression constant => VisitConstant(constant, context),
            UnaryExpression unary => VisitUnary(unary, entityParameter, context),
            MethodCallExpression methodCall => VisitMethodCall(methodCall, entityParameter, context),
            IndexExpression index => VisitIndex(index, entityParameter, context),
            ConditionalExpression conditional => VisitConditional(conditional, entityParameter, context),
            _ => throw new UnsupportedExpressionException(
                $"Expression type '{node.NodeType}' is not supported in DynamoDB expressions. " +
                $"Supported types: Binary, Member, Constant, Unary, MethodCall, Index, Conditional.",
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
            var leftReferencesEntity = ReferencesEntityParameter(node.Left, entityParameter);
            var rightReferencesEntity = ReferencesEntityParameter(node.Right, entityParameter);
            
            // Case 1: Neither side references entity - evaluate entire expression
            if (!leftReferencesEntity && !rightReferencesEntity)
            {
                return EvaluateAndHandleLocalBooleanExpression(node, context);
            }
            
            // Case 2: Only one side references entity - conditional filter pattern
            // But NOT if the non-entity side is a ConditionalExpression (ternary) - those should be visited normally
            // because they may contain entity references in their branches
            if (leftReferencesEntity != rightReferencesEntity)
            {
                var localOperand = leftReferencesEntity ? node.Right : node.Left;
                
                // If the local operand is a ConditionalExpression (ternary), let normal flow handle it
                // The ternary may have entity references in its branches that need to be processed
                if (localOperand is not ConditionalExpression)
                {
                    return HandleConditionalFilterPattern(node, entityParameter, context, 
                        leftReferencesEntity, rightReferencesEntity);
                }
            }
            
            // Case 3: Both sides reference entity (or one side is a ternary that references entity)
            // Only throw for key expressions (KeysOnly mode) - filter expressions support OR between entity conditions
            if (node.NodeType == ExpressionType.OrElse && context.ValidationMode == ExpressionValidationMode.KeysOnly)
            {
                // Check if this is actually a valid OR pattern (one side is a ternary that will evaluate to empty)
                // If both sides truly reference entity properties directly, throw
                var leftIsDirectEntityRef = IsDirectEntityPropertyComparison(node.Left, entityParameter);
                var rightIsDirectEntityRef = IsDirectEntityPropertyComparison(node.Right, entityParameter);
                
                if (leftIsDirectEntityRef && rightIsDirectEntityRef)
                {
                    // OR between two entity conditions is not supported in DynamoDB key expressions
                    throw new UnsupportedExpressionException(
                        "OR operator between two entity property conditions is not supported in DynamoDB key expressions. " +
                        "Use separate queries or restructure your data model.",
                        node);
                }
            }
            
            // AND/OR between entity conditions or with ternary expressions - visit both sides
            // Check if either operand is a bare boolean member expression
            var left = VisitBinaryOperandWithBooleanCheck(node.Left, entityParameter, context);
            var right = VisitBinaryOperandWithBooleanCheck(node.Right, entityParameter, context);
            var op = node.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
            
            // Handle sentinel values from conditional expressions
            // SkipDueToTrueInOr: came from (true || entityFilter) - means "skip this clause"
            // SkipDueToFalseInAnd: came from (false && entityFilter) - means "skip this clause"
            // Both sentinels mean "this clause doesn't contribute to the filter"
            // For AND: skip clause is identity (x AND skip = x)
            // For OR: SkipDueToTrueInOr is absorbing (true OR x = true), SkipDueToFalseInAnd is identity (false OR x = x)
            
            bool leftIsSkip = left == SkipDueToTrueInOr || left == SkipDueToFalseInAnd || string.IsNullOrEmpty(left);
            bool rightIsSkip = right == SkipDueToTrueInOr || right == SkipDueToFalseInAnd || string.IsNullOrEmpty(right);
            
            if (node.NodeType == ExpressionType.AndAlso)
            {
                // For AND: skip is identity (x AND skip = x)
                if (leftIsSkip && rightIsSkip)
                {
                    return string.Empty;
                }
                if (leftIsSkip)
                {
                    return right;
                }
                if (rightIsSkip)
                {
                    return left;
                }
            }
            else // OrElse
            {
                // For OR: 
                // - SkipDueToTrueInOr is absorbing (true OR x = true)
                // - SkipDueToFalseInAnd is identity (false OR x = x)
                // - empty string is treated as identity for backward compatibility
                if (left == SkipDueToTrueInOr || right == SkipDueToTrueInOr)
                {
                    // true OR x = true, return empty to skip the filter
                    return string.Empty;
                }
                // SkipDueToFalseInAnd and empty are identity for OR
                if (left == SkipDueToFalseInAnd || string.IsNullOrEmpty(left))
                {
                    if (right == SkipDueToFalseInAnd || string.IsNullOrEmpty(right))
                    {
                        return string.Empty;
                    }
                    return right;
                }
                if (right == SkipDueToFalseInAnd || string.IsNullOrEmpty(right))
                {
                    return left;
                }
            }
            
            // Use StringBuilder to minimize allocations
            var sb = new StringBuilder(left.Length + right.Length + op.Length + 6);
            sb.Append('(').Append(left).Append(") ").Append(op).Append(" (").Append(right).Append(')');
            return sb.ToString();
        }

        // Special handling for GeoLocation comparisons
        // Detects patterns like: x.Location == cell or x.Location.SpatialIndex == cell
        // These use the implicit cast operator or explicit SpatialIndex property access
        if (IsGeoLocationComparison(node, entityParameter, out var geoLocationExpr, out var valueExpr, out var isLeftSide))
        {
            return TranslateGeoLocationComparison(node, geoLocationExpr!, valueExpr!, isLeftSide, entityParameter, context);
        }

        // Special handling for string comparison patterns:
        // 1. string.CompareOrdinal(x.Property, value) >= 0 (static method)
        // 2. x.Property.CompareTo(value) >= 0 (instance method - more intuitive)
        // Both translate to: #attr0 >= :p0
        
        // Pattern 1: string.CompareOrdinal(x.Property, value) >= 0
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
        
        // Pattern 2: x.Property.CompareTo(value) >= 0 (instance method)
        // More intuitive syntax: x.SortKey.CompareTo("2024-01-01") >= 0
        if (node.Left is MethodCallExpression compareToCall &&
            compareToCall.Method.Name == "CompareTo" &&
            compareToCall.Method.DeclaringType == typeof(string) &&
            compareToCall.Arguments.Count == 1 &&
            compareToCall.Object != null &&
            IsEntityPropertyAccess(compareToCall.Object, entityParameter))
        {
            // Extract the property (x.SortKey) and value ("2024-01-01") from CompareTo
            var comparePropertyMetadata = GetPropertyMetadata(compareToCall.Object, entityParameter, context);
            var attributeName = Visit(compareToCall.Object, entityParameter, context);
            var compareValue = VisitWithPropertyMetadata(compareToCall.Arguments[0], entityParameter, context, comparePropertyMetadata);
            
            // The right side should be 0 (the comparison result)
            // Map the comparison operator: CompareTo(...) >= 0 means attr >= value
            var compareOperator = node.NodeType switch
            {
                ExpressionType.Equal => "=",           // CompareTo(...) == 0 -> attr = value
                ExpressionType.NotEqual => "<>",       // CompareTo(...) != 0 -> attr <> value
                ExpressionType.LessThan => "<",        // CompareTo(...) < 0 -> attr < value
                ExpressionType.LessThanOrEqual => "<=", // CompareTo(...) <= 0 -> attr <= value
                ExpressionType.GreaterThan => ">",     // CompareTo(...) > 0 -> attr > value
                ExpressionType.GreaterThanOrEqual => ">=", // CompareTo(...) >= 0 -> attr >= value
                _ => throw new UnsupportedExpressionException(
                    $"Binary operator '{node.NodeType}' is not supported with string.CompareTo.",
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
    /// Checks if a binary expression is comparing a GeoLocation property to a string value.
    /// Detects both implicit cast (x.Location == cell) and explicit property access (x.Location.SpatialIndex == cell).
    /// Only supports equality and inequality operators, as spatial indices (S2, H3) are not lexicographically ordered.
    /// </summary>
    /// <param name="node">The binary expression to check.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="geoLocationExpr">The GeoLocation property expression if detected.</param>
    /// <param name="valueExpr">The string value expression if detected.</param>
    /// <param name="isLeftSide">True if GeoLocation is on the left side, false if on the right.</param>
    /// <returns>True if this is a GeoLocation comparison, false otherwise.</returns>
    private bool IsGeoLocationComparison(
        BinaryExpression node,
        ParameterExpression entityParameter,
        out Expression? geoLocationExpr,
        out Expression? valueExpr,
        out bool isLeftSide)
    {
        geoLocationExpr = null;
        valueExpr = null;
        isLeftSide = false;

        // Only handle equality and inequality operators
        // Note: S2 and H3 spatial indices are NOT lexicographically ordered,
        // so comparison operators (<, >, <=, >=) don't make semantic sense.
        // GeoHash BETWEEN queries are handled by WithinDistance/WithinBoundingBox methods.
        if (node.NodeType != ExpressionType.Equal &&
            node.NodeType != ExpressionType.NotEqual)
        {
            return false;
        }

        // Check left side for GeoLocation
        if (IsGeoLocationPropertyAccess(node.Left, entityParameter))
        {
            geoLocationExpr = node.Left;
            valueExpr = node.Right;
            isLeftSide = true;
            return true;
        }

        // Check right side for GeoLocation
        if (IsGeoLocationPropertyAccess(node.Right, entityParameter))
        {
            geoLocationExpr = node.Right;
            valueExpr = node.Left;
            isLeftSide = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if an expression is accessing a GeoLocation property or its SpatialIndex property.
    /// Detects: x.Location (GeoLocation property) or x.Location.SpatialIndex (explicit property access).
    /// </summary>
    private bool IsGeoLocationPropertyAccess(Expression expr, ParameterExpression entityParameter)
    {
        if (expr is not MemberExpression member)
            return false;

        // Check for x.Location.SpatialIndex pattern
        if (member.Member.Name == "SpatialIndex" &&
            member.Expression is MemberExpression parentMember &&
            parentMember.Expression == entityParameter)
        {
            // Check if the parent property is of type GeoLocation
            // Use MemberExpression.Member directly - it's already captured at compile time (AOT-safe)
            var propertyType = parentMember.Member is System.Reflection.PropertyInfo parentPropInfo 
                ? parentPropInfo.PropertyType 
                : null;
            return IsGeoLocationType(propertyType);
        }

        // Check for x.Location pattern (direct GeoLocation property access)
        if (member.Expression == entityParameter)
        {
            // Use MemberExpression.Member directly - it's already captured at compile time (AOT-safe)
            var propertyType = member.Member is System.Reflection.PropertyInfo propInfo 
                ? propInfo.PropertyType 
                : null;
            return IsGeoLocationType(propertyType);
        }

        return false;
    }

    /// <summary>
    /// Checks if a type is GeoLocation.
    /// </summary>
    private bool IsGeoLocationType(Type? type)
    {
        if (type == null)
            return false;

        // Check for exact type match
        if (type.FullName == "Oproto.FluentDynamoDb.Geospatial.GeoLocation")
            return true;

        // Check for nullable GeoLocation
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
            type.GetGenericArguments()[0].FullName == "Oproto.FluentDynamoDb.Geospatial.GeoLocation")
            return true;

        return false;
    }

    /// <summary>
    /// Translates a GeoLocation comparison to a DynamoDB expression.
    /// Handles both x.Location == cell and x.Location.SpatialIndex == cell patterns.
    /// Only supports equality (==) and inequality (!=) operators.
    /// </summary>
    private string TranslateGeoLocationComparison(
        BinaryExpression node,
        Expression geoLocationExpr,
        Expression valueExpr,
        bool isLeftSide,
        ParameterExpression entityParameter,
        ExpressionContext context)
    {
        // Extract the base GeoLocation property (x.Location)
        MemberExpression baseProperty;
        if (geoLocationExpr is MemberExpression member && member.Member.Name == "SpatialIndex")
        {
            // x.Location.SpatialIndex - get the parent (x.Location)
            baseProperty = (MemberExpression)member.Expression!;
        }
        else
        {
            // x.Location - already the base property
            baseProperty = (MemberExpression)geoLocationExpr;
        }

        // Get the property metadata for the GeoLocation property
        var propertyMetadata = GetPropertyMetadata(baseProperty, entityParameter, context);

        // Translate the GeoLocation property to its DynamoDB attribute name
        var attributeName = Visit(baseProperty, entityParameter, context);

        // Evaluate and capture the string value
        var value = VisitWithPropertyMetadata(valueExpr, entityParameter, context, propertyMetadata);

        // Map the comparison operator (only == and != are supported)
        var dynamoDbOperator = node.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            _ => throw new UnsupportedExpressionException(
                $"Binary operator '{node.NodeType}' is not supported for GeoLocation comparisons. " +
                $"Only equality (==) and inequality (!=) operators are supported because spatial indices " +
                $"(S2, H3) are not lexicographically ordered. Use WithinDistance or WithinBoundingBox methods " +
                $"for range-based spatial queries.",
                node)
        };

        // Build the expression: attribute operator value
        // Note: We always put the attribute on the left side in DynamoDB expressions
        var builder = new StringBuilder(attributeName.Length + value.Length + dynamoDbOperator.Length + 2);
        builder.Append(attributeName).Append(' ').Append(dynamoDbOperator).Append(' ').Append(value);
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
    /// Visits a member expression node (property access like x.PropertyName or x.Address.City).
    /// </summary>
    private string VisitMember(MemberExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        // Check if this is entity property access (x.PropertyName or x.Address.City)
        if (IsEntityPropertyAccess(node, entityParameter))
        {
            // Check if this is nested property access (x.Address.City)
            if (IsNestedPropertyAccess(node, entityParameter))
            {
                // Nested property access is only valid in filter expressions and condition expressions
                // Not valid in key condition expressions (KeysOnly mode)
                if (context.ValidationMode == ExpressionValidationMode.KeysOnly)
                {
                    throw new InvalidKeyExpressionException(
                        $"Nested property access '{GetNestedPropertyPath(node)}' is not supported in key condition expressions. " +
                        "DynamoDB key conditions only support partition key and sort key attributes. " +
                        "Use nested property access in filter expressions (.WithFilter()) or condition expressions (.Where() on Put/Update/Delete) instead.",
                        node);
                }

                // Build document path for nested access
                return BuildDocumentPathFromMemberChain(node, entityParameter, context);
            }

            // Direct property access (x.PropertyName)
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
                // Skip key validation for DynamicEntity - it uses DynamicFields indexer for key access
                // and doesn't have typed key properties to validate against
                if (context.ValidationMode == ExpressionValidationMode.KeysOnly && 
                    context.EntityMetadata?.IsDynamicEntity != true)
                {
                    var isKey = IsKeyProperty(propertyName, propertyMetadata, context);
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
    /// Gets a human-readable path string for a nested property access (for error messages).
    /// </summary>
    /// <param name="node">The member expression.</param>
    /// <returns>A string like "Address.City".</returns>
    private string GetNestedPropertyPath(MemberExpression node)
    {
        var parts = new List<string>();
        Expression? current = node;
        while (current is MemberExpression member)
        {
            parts.Add(member.Member.Name);
            current = member.Expression;
        }
        parts.Reverse();
        return string.Join(".", parts);
    }

    /// <summary>
    /// Determines if a property is a key property for the current query context.
    /// When querying a GSI (IndexName is set), checks if the property is the GSI's partition or sort key.
    /// Otherwise, checks if the property is the main table's partition or sort key.
    /// </summary>
    /// <param name="propertyName">The C# property name to check.</param>
    /// <param name="propertyMetadata">The property metadata from entity metadata.</param>
    /// <param name="context">The expression context containing index information.</param>
    /// <returns>True if the property is a key property for the current query context.</returns>
    private bool IsKeyProperty(string propertyName, PropertyMetadata propertyMetadata, ExpressionContext context)
    {
        // If querying a GSI, check if the property is a key in that GSI
        if (!string.IsNullOrEmpty(context.IndexName) && context.EntityMetadata?.Indexes != null)
        {
            var indexMetadata = context.EntityMetadata.Indexes
                .FirstOrDefault(i => string.Equals(i.IndexName, context.IndexName, StringComparison.OrdinalIgnoreCase));
            
            if (indexMetadata != null)
            {
                // Check if this property is the GSI partition key or sort key
                var isGsiKey = string.Equals(indexMetadata.PartitionKeyProperty, propertyName, StringComparison.Ordinal) ||
                               string.Equals(indexMetadata.SortKeyProperty, propertyName, StringComparison.Ordinal);
                return isGsiKey;
            }
        }
        
        // Fall back to main table key validation
        return propertyMetadata.IsPartitionKey || propertyMetadata.IsSortKey;
    }

    /// <summary>
    /// Visits a constant expression node.
    /// </summary>
    private string VisitConstant(ConstantExpression node, ExpressionContext context)
    {
        return CaptureValue(node.Value, context, propertyMetadata: null);
    }

    /// <summary>
    /// Translates a boolean member expression into a valid DynamoDB equality condition.
    /// Converts a bare boolean attribute path (e.g., <c>#attr0</c>) into an equality comparison
    /// against a boolean literal value (e.g., <c>#attr0 = :p0</c>).
    /// </summary>
    /// <param name="attributePath">The already-visited attribute path string (e.g., <c>#attr0</c> or <c>#attr0.#attr1</c>).</param>
    /// <param name="boolValue">The boolean literal value to compare against (true or false).</param>
    /// <param name="context">The expression context for registering attribute values.</param>
    /// <returns>A valid DynamoDB condition string in the form <c>{attributePath} = :pN</c>.</returns>
    private string TranslateBooleanMemberAsCondition(string attributePath, bool boolValue, ExpressionContext context)
    {
        // Register the BOOL attribute value
        var attributeValue = new AttributeValue { BOOL = boolValue, IsBOOLSet = true };
        var parameterName = context.ParameterGenerator.GenerateParameterName();
        context.AttributeValues.AttributeValues.Add(parameterName, attributeValue);

        // Build the equality condition string: "{attributePath} = {parameterName}"
        var sb = new StringBuilder(attributePath.Length + parameterName.Length + 3);
        sb.Append(attributePath).Append(" = ").Append(parameterName);
        return sb.ToString();
    }

    /// <summary>
    /// Visits a binary operand, detecting bare boolean member expressions and translating
    /// them as equality comparisons with <c>true</c> instead of returning just the attribute path.
    /// </summary>
    /// <param name="operand">The operand expression from a binary AND/OR node.</param>
    /// <param name="entityParameter">The entity lambda parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>A translated DynamoDB expression string for the operand.</returns>
    private string VisitBinaryOperandWithBooleanCheck(Expression operand, ParameterExpression entityParameter, ExpressionContext context)
    {
        // Strip potential Convert/ConvertChecked wrapper
        var actualOperand = operand;
        if (actualOperand.NodeType == ExpressionType.Convert || actualOperand.NodeType == ExpressionType.ConvertChecked)
        {
            actualOperand = ((UnaryExpression)actualOperand).Operand;
        }

        // Check if operand is a bare boolean member expression
        if (actualOperand is MemberExpression
            && actualOperand.Type == typeof(bool)
            && IsEntityPropertyAccess(actualOperand, entityParameter))
        {
            var attributePath = Visit(operand, entityParameter, context);
            return TranslateBooleanMemberAsCondition(attributePath, true, context);
        }

        // Also handle negated boolean: !x.BoolProp in AND/OR
        // This is already handled by VisitUnary (task 3.2), so normal Visit will produce the correct result

        return Visit(operand, entityParameter, context);
    }

    /// <summary>
    /// Visits a unary expression node (operators like !).
    /// </summary>
    private string VisitUnary(UnaryExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            // Check if operand is a bare boolean member expression (entity property access)
            // Strip potential Convert/ConvertChecked wrapper to get the actual operand
            var actualOperand = node.Operand;
            if (actualOperand.NodeType == ExpressionType.Convert || actualOperand.NodeType == ExpressionType.ConvertChecked)
            {
                actualOperand = ((UnaryExpression)actualOperand).Operand;
            }

            if (actualOperand is MemberExpression
                && actualOperand.Type == typeof(bool)
                && IsEntityPropertyAccess(actualOperand, entityParameter))
            {
                // Visit the operand to get the attribute path (e.g., "#attr0" or "#attr0.#attr1")
                var attributePath = Visit(node.Operand, entityParameter, context);
                // Translate as equality comparison with false
                return TranslateBooleanMemberAsCondition(attributePath, false, context);
            }

            // Non-boolean operands (comparisons, method calls) get NOT(...) wrapping
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
    /// Visits a conditional expression node (ternary operator: condition ? trueValue : falseValue).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Conditional expressions allow dynamic filter inclusion based on runtime flags.
    /// The condition is evaluated at translation time (not in DynamoDB), so it must not
    /// reference the entity parameter.
    /// </para>
    /// <para><strong>Supported Patterns:</strong></para>
    /// <list type="bullet">
    /// <item><description>x => flag ? x.Field == value : true - omits filter when flag is false</description></item>
    /// <item><description>x => flag ? x.FieldA == valueA : x.FieldB == valueB - selects branch based on flag</description></item>
    /// </list>
    /// <para><strong>Unsupported Patterns:</strong></para>
    /// <list type="bullet">
    /// <item><description>x => x.SomeProperty ? trueExpr : falseExpr - condition references entity</description></item>
    /// </list>
    /// </remarks>
    /// <param name="node">The conditional expression node.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The translated DynamoDB expression string, or empty string if the filter should be omitted.</returns>
    /// <exception cref="UnsupportedExpressionException">Thrown when the condition references the entity parameter or evaluates to constant false.</exception>
    private string VisitConditional(ConditionalExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        // The condition must not reference the entity parameter - it must be evaluable at translation time
        if (ReferencesEntityParameter(node.Test, entityParameter))
        {
            throw new UnsupportedExpressionException(
                "Conditional test cannot reference entity properties. " +
                "Use captured variables or constants for the condition. " +
                "Example: 'x => someFlag ? x.Field == value : true' is valid, " +
                "but 'x => x.SomeProperty ? trueExpr : falseExpr' is not.",
                node);
        }

        // Evaluate the test condition at translation time
        bool testResult;
        try
        {
            var testValue = EvaluateExpression(node.Test);
            testResult = testValue is bool b ? b : Convert.ToBoolean(testValue);
        }
        catch (Exception ex)
        {
            throw new ExpressionTranslationException(
                $"Failed to evaluate conditional test expression: {ex.Message}",
                node);
        }

        if (testResult)
        {
            // Condition is true - check if true branch is constant true (skip filter)
            if (IsConstantTrue(node.IfTrue))
            {
                // Return empty string to signal that this part of the filter should be omitted
                return string.Empty;
            }
            
            // Check if true branch is constant false (would return no results)
            if (IsConstantFalse(node.IfTrue))
            {
                throw new UnsupportedExpressionException(
                    "Filter expression evaluates to constant false, which would return no results. " +
                    "Remove the filter or fix the condition.",
                    node);
            }
            
            // Process the true branch
            return Visit(node.IfTrue, entityParameter, context);
        }
        else
        {
            // Condition is false - check if false branch is constant true (skip filter)
            if (IsConstantTrue(node.IfFalse))
            {
                // Return empty string to signal that this part of the filter should be omitted
                return string.Empty;
            }
            
            // Check if false branch is constant false (would return no results)
            if (IsConstantFalse(node.IfFalse))
            {
                throw new UnsupportedExpressionException(
                    "Filter expression evaluates to constant false, which would return no results. " +
                    "Remove the filter or fix the condition.",
                    node);
            }

            // Process the false branch
            return Visit(node.IfFalse, entityParameter, context);
        }
    }

    /// <summary>
    /// Checks if an expression is a constant true value.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <returns>True if the expression is constant true, false otherwise.</returns>
    private bool IsConstantTrue(Expression expression)
    {
        if (expression is ConstantExpression constant && constant.Value is bool b)
        {
            return b;
        }
        return false;
    }

    /// <summary>
    /// Checks if an expression is a constant false value.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <returns>True if the expression is constant false, false otherwise.</returns>
    private bool IsConstantFalse(Expression expression)
    {
        if (expression is ConstantExpression constant && constant.Value is bool b)
        {
            return !b;
        }
        return false;
    }

    /// <summary>
    /// Handles conditional filter patterns where one operand is a local boolean condition
    /// and the other references entity properties.
    /// </summary>
    /// <remarks>
    /// <para><strong>Supported Patterns:</strong></para>
    /// <list type="bullet">
    /// <item><description>OR with local condition: (localCondition || x.Property == value) - skip filter when local is true</description></item>
    /// <item><description>AND with local condition: (localCondition &amp;&amp; x.Property == value) - include filter only when local is true</description></item>
    /// </list>
    /// </remarks>
    /// <param name="node">The binary expression node.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="leftReferencesEntity">Whether the left operand references the entity parameter.</param>
    /// <param name="rightReferencesEntity">Whether the right operand references the entity parameter.</param>
    /// <returns>The translated DynamoDB expression string, or empty string if the filter should be omitted.</returns>
    /// <exception cref="ExpressionTranslationException">Thrown when the local condition cannot be evaluated.</exception>
    private string HandleConditionalFilterPattern(
        BinaryExpression node,
        ParameterExpression entityParameter,
        ExpressionContext context,
        bool leftReferencesEntity,
        bool rightReferencesEntity)
    {
        var localOperand = leftReferencesEntity ? node.Right : node.Left;
        var entityOperand = leftReferencesEntity ? node.Left : node.Right;

        // Evaluate the local operand
        bool localValue;
        try
        {
            var evaluated = EvaluateExpression(localOperand);
            localValue = evaluated is bool b ? b : Convert.ToBoolean(evaluated);
        }
        catch (Exception ex)
        {
            throw new ExpressionTranslationException(
                $"Failed to evaluate local condition in filter expression: {ex.Message}",
                node);
        }

        if (node.NodeType == ExpressionType.OrElse)
        {
            // OR pattern: (localCondition || entityFilter)
            // If local is true → skip filter (return sentinel for "true in OR")
            // If local is false → apply entity filter
            if (localValue)
            {
                return SkipDueToTrueInOr;
            }
            return Visit(entityOperand, entityParameter, context);
        }
        else // AndAlso
        {
            // AND pattern: (localCondition && entityFilter)
            // If local is true → apply entity filter
            // If local is false → skip filter (return sentinel for "false in AND")
            if (localValue)
            {
                return Visit(entityOperand, entityParameter, context);
            }
            return SkipDueToFalseInAnd;
        }
    }

    /// <summary>
    /// Evaluates and handles a fully local boolean expression where neither operand references the entity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When both operands of a logical expression don't reference the entity parameter,
    /// the entire expression can be evaluated at translation time.
    /// </para>
    /// <para>
    /// If the expression evaluates to true, a sentinel is returned to indicate the skip reason.
    /// If the expression evaluates to false, an exception is thrown because a constant false
    /// filter would return no results.
    /// </para>
    /// </remarks>
    /// <param name="node">The binary expression node.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>Sentinel value if the expression evaluates to true.</returns>
    /// <exception cref="UnsupportedExpressionException">Thrown when the expression evaluates to constant false.</exception>
    /// <exception cref="ExpressionTranslationException">Thrown when the expression cannot be evaluated.</exception>
    private string EvaluateAndHandleLocalBooleanExpression(
        BinaryExpression node,
        ExpressionContext context)
    {
        bool result;
        try
        {
            var evaluated = EvaluateExpression(node);
            result = evaluated is bool b ? b : Convert.ToBoolean(evaluated);
        }
        catch (Exception ex)
        {
            throw new ExpressionTranslationException(
                $"Failed to evaluate local boolean expression: {ex.Message}",
                node);
        }

        if (result)
        {
            // Expression evaluates to true - return sentinel based on operator
            // For OR: true is absorbing (true OR x = true)
            // For AND: true is identity (true AND x = x)
            return node.NodeType == ExpressionType.OrElse ? SkipDueToTrueInOr : SkipDueToTrueInOr;
        }
        else
        {
            // Expression evaluates to false - this would filter out everything
            throw new UnsupportedExpressionException(
                "Filter expression evaluates to constant false, which would return no results. " +
                "Remove the filter or fix the condition.",
                node);
        }
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

        // Check if this is a dynamic field existence check (Exists/NotExists)
        if (IsDynamicFieldExistenceCheck(node, entityParameter, context, out dynamoDbFunction))
        {
            return true;
        }

        // Check if this is a string function on a dynamic field (StartsWith/Contains)
        if (IsDynamicFieldStringFunction(node, entityParameter, context, out dynamoDbFunction))
        {
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

        // string.CompareOrdinal(str1, str2) and string.CompareTo(str) - used for string comparisons
        // These are NOT DynamoDB functions - they're handled specially in VisitBinary
        // to translate patterns like x.SortKey.CompareTo("value") >= 0 to: #attr >= :p0

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
            // Handle indexer expressions like x.DynamicFields["fieldName"]
            IndexExpression index => ReferencesEntityParameter(index.Object!, entityParameter),
            _ => false
        };
    }

    /// <summary>
    /// Checks if a member expression is accessing an entity property (direct or nested).
    /// Also handles property access after list index (e.g., x.LineItems[0].ProductId).
    /// </summary>
    private bool IsEntityPropertyAccess(Expression node, ParameterExpression entityParameter)
    {
        if (node is not MemberExpression member)
            return false;

        // Check if the member is directly on the entity parameter (x.PropertyName)
        if (member.Expression == entityParameter)
            return true;

        // Check for nested property access (x.Address.City)
        // Also handles property access after list index (x.LineItems[0].ProductId)
        return IsNestedPropertyAccess(member, entityParameter);
    }

    /// <summary>
    /// Checks if a member expression is a nested property access (e.g., x.Address.City).
    /// Also handles property access after list index (e.g., x.LineItems[0].ProductId).
    /// </summary>
    /// <param name="node">The member expression to check.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <returns>True if this is a nested property access, false otherwise.</returns>
    private bool IsNestedPropertyAccess(MemberExpression node, ParameterExpression entityParameter)
    {
        // Walk up the expression tree to find if we eventually reach the entity parameter
        Expression? current = node.Expression;
        while (current != null)
        {
            if (current == entityParameter)
            {
                // We reached the entity parameter through a chain of member accesses
                // This is nested if we had at least one intermediate member
                return node.Expression != entityParameter;
            }

            if (current is MemberExpression member)
            {
                current = member.Expression;
            }
            else if (current is MethodCallExpression methodCall && methodCall.Method.Name == "get_Item")
            {
                // Handle list indexer access (x.LineItems[0].ProductId)
                // The list indexer appears as a MethodCallExpression with method name "get_Item"
                current = methodCall.Object;
            }
            else if (current is IndexExpression indexExpr)
            {
                // Handle IndexExpression (alternative representation of list indexer)
                current = indexExpr.Object;
            }
            else
            {
                // Not a member expression chain leading to entity parameter
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Builds a DynamoDB document path from a chained member expression (e.g., x.Address.City -> #address.#city).
    /// Also handles property access after list index (e.g., x.LineItems[0].ProductId -> #lineItems[0].#productId).
    /// Supports dynamic indices (variables, method calls, property access) as long as they
    /// don't reference the entity parameter.
    /// </summary>
    /// <param name="node">The member expression representing the nested property access.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The DynamoDB document path string.</returns>
    private string BuildDocumentPathFromMemberChain(MemberExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        var pathBuilder = new DocumentPathBuilder(context.AttributeNames);
        
        // Use a list to collect segments that can include both properties and indices
        var segments = new List<object>(); // Either (string PropertyName, string AttributeName) or int index

        // Collect all segments from leaf to root
        Expression? current = node;
        while (current != null && current != entityParameter)
        {
            if (current is MemberExpression member)
            {
                var propertyName = member.Member.Name;
                var attributeName = GetDynamoDbAttributeName(member, entityParameter, context);
                segments.Add((propertyName, attributeName));
                current = member.Expression;
            }
            else if (current is MethodCallExpression methodCall && methodCall.Method.Name == "get_Item")
            {
                // Handle list indexer access (x.LineItems[0].ProductId)
                // Extract the index value - supports dynamic indices
                if (methodCall.Arguments.Count == 1)
                {
                    var indexValue = EvaluateIndexExpression(methodCall.Arguments[0], entityParameter, methodCall);
                    ValidateListIndex(indexValue, methodCall.Arguments[0]);
                    segments.Add(indexValue);
                }
                current = methodCall.Object;
            }
            else if (current is IndexExpression indexExpr)
            {
                // Handle IndexExpression (alternative representation of list indexer)
                // Supports dynamic indices
                if (indexExpr.Arguments.Count == 1)
                {
                    var indexValue = EvaluateIndexExpression(indexExpr.Arguments[0], entityParameter, indexExpr);
                    ValidateListIndex(indexValue, indexExpr.Arguments[0]);
                    segments.Add(indexValue);
                }
                current = indexExpr.Object;
            }
            else
            {
                break;
            }
        }

        // Reverse to build path from root to leaf
        segments.Reverse();

        // Build path
        foreach (var segment in segments)
        {
            if (segment is ValueTuple<string, string> propSegment)
            {
                pathBuilder.AddProperty(propSegment.Item1, propSegment.Item2);
            }
            else if (segment is int indexSegment)
            {
                pathBuilder.AddIndex(indexSegment);
            }
        }

        return pathBuilder.Build();
    }

    /// <summary>
    /// Gets the DynamoDB attribute name for a member expression.
    /// </summary>
    /// <param name="member">The member expression.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The DynamoDB attribute name.</returns>
    private string GetDynamoDbAttributeName(MemberExpression member, ParameterExpression entityParameter, ExpressionContext context)
    {
        var propertyName = member.Member.Name;

        // If we have entity metadata and this is a direct property on the entity, use the metadata
        if (context.EntityMetadata != null && member.Expression == entityParameter)
        {
            var propertyMetadata = context.EntityMetadata.Properties
                .FirstOrDefault(p => p.PropertyName == propertyName);
            if (propertyMetadata != null)
            {
                return propertyMetadata.AttributeName;
            }
        }

        // For nested properties or when no metadata is available, check for DynamoDbAttribute
        // Use reflection to get the attribute (AOT-safe since we're reading compile-time metadata)
        if (member.Member is System.Reflection.PropertyInfo propInfo)
        {
            var dynamoDbAttr = propInfo.GetCustomAttributes(typeof(DynamoDbAttributeAttribute), true)
                .FirstOrDefault() as DynamoDbAttributeAttribute;
            if (dynamoDbAttr != null)
            {
                return dynamoDbAttr.AttributeName;
            }
        }

        // Default to property name
        return propertyName;
    }

    /// <summary>
    /// Checks if an expression is a direct entity property comparison (e.g., x.Property == value).
    /// This excludes conditional expressions (ternary) and other complex patterns.
    /// </summary>
    /// <param name="node">The expression to check.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <returns>True if this is a direct entity property comparison, false otherwise.</returns>
    private bool IsDirectEntityPropertyComparison(Expression node, ParameterExpression entityParameter)
    {
        // Conditional expressions (ternary) are not direct comparisons
        if (node is ConditionalExpression)
            return false;

        // Binary comparisons (==, !=, <, >, etc.) with entity property access
        if (node is BinaryExpression binary)
        {
            // Check if this is a comparison operator (not AND/OR)
            if (binary.NodeType != ExpressionType.AndAlso && binary.NodeType != ExpressionType.OrElse)
            {
                return IsEntityPropertyAccess(binary.Left, entityParameter) ||
                       IsEntityPropertyAccess(binary.Right, entityParameter);
            }
            
            // For AND/OR, recursively check both sides
            return IsDirectEntityPropertyComparison(binary.Left, entityParameter) ||
                   IsDirectEntityPropertyComparison(binary.Right, entityParameter);
        }

        // Method calls on entity properties (e.g., x.Name.StartsWith("value"))
        if (node is MethodCallExpression methodCall)
        {
            if (methodCall.Object != null && IsEntityPropertyAccess(methodCall.Object, entityParameter))
                return true;
            
            // Extension methods like Between, AttributeExists
            if (methodCall.Arguments.Count > 0 && IsEntityPropertyAccess(methodCall.Arguments[0], entityParameter))
                return true;
        }

        // Unary expressions (e.g., !x.IsActive)
        if (node is UnaryExpression unary)
        {
            return IsDirectEntityPropertyComparison(unary.Operand, entityParameter);
        }

        // Direct entity property access (e.g., x.IsActive as boolean)
        if (IsEntityPropertyAccess(node, entityParameter))
            return true;

        return false;
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
            // Check if property is sensitive using PropertyMetadata.IsSensitive or the fallback function
            var isSensitive = propertyMetadata?.IsSensitive == true;
            if (!isSensitive && _isSensitiveField != null && propertyMetadata?.AttributeName != null)
            {
                isSensitive = _isSensitiveField(propertyMetadata.AttributeName);
            }
            
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
            DateOnly d => new AttributeValue { S = d.ToString("O", CultureInfo.InvariantCulture) },
            TimeOnly t => new AttributeValue { S = t.ToString("O", CultureInfo.InvariantCulture) },
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
            // Use MemberExpression.Member directly - it's already captured at compile time (AOT-safe)
            if (expression is MemberExpression member && member.Expression is ConstantExpression memberConstant)
            {
                // member.Member is already the FieldInfo/PropertyInfo from the expression tree
                if (member.Member is System.Reflection.FieldInfo field)
                {
                    return (T)field.GetValue(memberConstant.Value)!;
                }

                if (member.Member is System.Reflection.PropertyInfo property)
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

        // Ensure geospatial provider is configured
        var geospatialProvider = _options?.GeospatialProvider;
        if (geospatialProvider == null)
        {
            throw new InvalidOperationException(
                "Geospatial features require configuration. " +
                "Add the Oproto.FluentDynamoDb.Geospatial package and call " +
                "options.AddGeospatial() when creating your ExpressionTranslator. " +
                "Example: new ExpressionTranslator(new FluentDynamoDbOptions().AddGeospatial())");
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

        // Extract latitude and longitude from the center GeoLocation
        var (centerLatitude, centerLongitude) = ExtractGeoLocationCoordinates(center, node);

        // Create bounding box using the provider (no reflection)
        var bbox = geospatialProvider.CreateBoundingBox(centerLatitude, centerLongitude, distanceMeters);

        // Get GeoHash range from bounding box using the provider (no reflection)
        var (minHash, maxHash) = geospatialProvider.GetGeoHashRange(bbox, precision);

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
    /// Extracts latitude and longitude from a GeoLocation object using the configured geospatial provider.
    /// This method is AOT-safe as it delegates to the provider instead of using reflection.
    /// </summary>
    /// <param name="geoLocation">The GeoLocation object.</param>
    /// <param name="node">The expression node for error reporting.</param>
    /// <returns>A tuple containing latitude and longitude.</returns>
    private (double Latitude, double Longitude) ExtractGeoLocationCoordinates(object geoLocation, Expression node)
    {
        // Use the geospatial provider for AOT-safe coordinate extraction
        var geospatialProvider = _options?.GeospatialProvider;
        if (geospatialProvider != null)
        {
            try
            {
                return geospatialProvider.ExtractGeoLocationCoordinates(geoLocation);
            }
            catch (ArgumentException ex)
            {
                throw new ExpressionTranslationException(
                    $"Unable to extract coordinates from GeoLocation object: {ex.Message}",
                    node);
            }
        }
        
        throw new ExpressionTranslationException(
            $"Unable to extract coordinates from GeoLocation object of type {geoLocation.GetType().FullName}. " +
            "Geospatial provider is not configured. Add the Oproto.FluentDynamoDb.Geospatial package and call " +
            "options.AddGeospatial() when creating your ExpressionTranslator.",
            node);
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

        // Ensure geospatial provider is configured
        var geospatialProvider = _options?.GeospatialProvider;
        if (geospatialProvider == null)
        {
            throw new InvalidOperationException(
                "Geospatial features require configuration. " +
                "Add the Oproto.FluentDynamoDb.Geospatial package and call " +
                "options.AddGeospatial() when creating your ExpressionTranslator. " +
                "Example: new ExpressionTranslator(new FluentDynamoDbOptions().AddGeospatial())");
        }

        GeoBoundingBoxResult bbox;

        if (node.Arguments.Count == 2)
        {
            // Single GeoBoundingBox parameter
            var bboxExpr = node.Arguments[1];
            var bboxObj = EvaluateConstantExpression<object>(bboxExpr);
            
            // Extract coordinates from the bounding box object
            var (swLat, swLon, neLat, neLon) = ExtractBoundingBoxCoordinates(bboxObj, node);
            bbox = geospatialProvider.CreateBoundingBox(swLat, swLon, neLat, neLon);
        }
        else
        {
            // Two GeoLocation parameters (southwest, northeast)
            var southwestExpr = node.Arguments[1];
            var northeastExpr = node.Arguments[2];

            var southwest = EvaluateConstantExpression<object>(southwestExpr);
            var northeast = EvaluateConstantExpression<object>(northeastExpr);

            // Extract coordinates from GeoLocation objects
            var (swLat, swLon) = ExtractGeoLocationCoordinates(southwest, node);
            var (neLat, neLon) = ExtractGeoLocationCoordinates(northeast, node);

            // Create bounding box using the provider (no reflection)
            bbox = geospatialProvider.CreateBoundingBox(swLat, swLon, neLat, neLon);
        }

        // Get the precision for this property
        var precision = GetPrecisionForProperty(locationExpr, entityParameter, context);

        // Get GeoHash range from bounding box using the provider (no reflection)
        var (minHash, maxHash) = geospatialProvider.GetGeoHashRange(bbox, precision);

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
    /// Extracts coordinates from a GeoBoundingBox object using the configured geospatial provider.
    /// This method is AOT-safe as it delegates to the provider instead of using reflection.
    /// </summary>
    /// <param name="boundingBox">The bounding box object.</param>
    /// <param name="node">The expression node for error reporting.</param>
    /// <returns>A tuple containing southwest and northeast coordinates.</returns>
    private (double SwLatitude, double SwLongitude, double NeLatitude, double NeLongitude) ExtractBoundingBoxCoordinates(object boundingBox, Expression node)
    {
        // Use the geospatial provider for AOT-safe coordinate extraction
        var geospatialProvider = _options?.GeospatialProvider;
        if (geospatialProvider != null)
        {
            try
            {
                return geospatialProvider.ExtractBoundingBoxCoordinates(boundingBox);
            }
            catch (ArgumentException ex)
            {
                throw new ExpressionTranslationException(
                    $"Unable to extract coordinates from bounding box object: {ex.Message}",
                    node);
            }
        }
        
        throw new ExpressionTranslationException(
            $"Unable to extract coordinates from bounding box object of type {boundingBox.GetType().FullName}. " +
            "Geospatial provider is not configured. Add the Oproto.FluentDynamoDb.Geospatial package and call " +
            "options.AddGeospatial() when creating your ExpressionTranslator.",
            node);
    }

    #region Dynamic Field Support

    /// <summary>
    /// Visits an index expression node (indexer access like x.DynamicFields["fieldName"] or x.Tags[0]).
    /// </summary>
    private string VisitIndex(IndexExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        // Check if this is a dynamic field indexer access
        if (IsDynamicFieldIndexExpression(node, entityParameter, out var fieldName))
        {
            return TranslateDynamicFieldAccess(fieldName!, context);
        }

        // Check if this is a list index access (x.Tags[0] or x.Metadata.Keywords[0])
        if (IsListIndexAccess(node, entityParameter, out var listIndex))
        {
            return BuildListIndexPath(node.Object!, listIndex!.Value, entityParameter, context);
        }

        throw new UnsupportedExpressionException(
            $"Index expression is not supported in DynamoDB expressions. " +
            $"Supported patterns: DynamicFields indexer (x.DynamicFields[\"fieldName\"]) and list index access (x.Tags[0]).",
            node);
    }

    /// <summary>
    /// Checks if an IndexExpression is a list index access pattern (e.g., x.Tags[0]).
    /// Supports dynamic indices (variables, method calls, property access) as long as they
    /// don't reference the entity parameter.
    /// </summary>
    /// <param name="node">The index expression to check.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="index">The list index if this is a list index access.</param>
    /// <returns>True if this is a list index access, false otherwise.</returns>
    private bool IsListIndexAccess(IndexExpression node, ParameterExpression entityParameter, out int? index)
    {
        index = null;

        // The object must be an entity property access (direct or nested)
        if (node.Object == null || !IsEntityPropertyAccess(node.Object, entityParameter))
            return false;

        // Must have exactly one argument (the index)
        if (node.Arguments.Count != 1)
            return false;

        // Evaluate the index expression (supports constants, variables, method calls, property access)
        var indexValue = EvaluateIndexExpression(node.Arguments[0], entityParameter, node);
        ValidateListIndex(indexValue, node.Arguments[0]);
        index = indexValue;
        return true;
    }

    /// <summary>
    /// Evaluates an index expression to get the integer value.
    /// Supports constants, variables, property access, and method calls.
    /// Throws if the expression references the entity parameter.
    /// </summary>
    /// <param name="indexExpr">The index expression to evaluate.</param>
    /// <param name="entityParameter">The entity parameter to check for references.</param>
    /// <param name="sourceExpression">The source expression for error reporting.</param>
    /// <returns>The evaluated integer index value.</returns>
    private int EvaluateIndexExpression(Expression indexExpr, ParameterExpression entityParameter, Expression sourceExpression)
    {
        // Fast path: constant expression
        if (indexExpr is ConstantExpression constant && constant.Value is int constIndex)
        {
            return constIndex;
        }

        // Check if expression references entity parameter
        if (ParameterReferenceVisitor.ContainsReference(indexExpr, entityParameter))
        {
            throw new UnsupportedExpressionException(
                "List index cannot reference the entity parameter. " +
                "Use a local variable, property, or method call that doesn't depend on the entity. " +
                "Example: int idx = GetIndex(); .WithFilter(x => x.Tags[idx] == \"value\")",
                sourceExpression);
        }

        // Evaluate the expression
        try
        {
            var lambda = Expression.Lambda<Func<int>>(indexExpr);
            var compiled = lambda.Compile();
            return compiled();
        }
        catch (Exception ex)
        {
            throw new UnsupportedExpressionException(
                $"Failed to evaluate list index expression: {ex.Message}. " +
                "Ensure the index is a constant, variable, property, or method call that can be evaluated at translation time.",
                sourceExpression);
        }
    }

    /// <summary>
    /// Validates that a list index is non-negative.
    /// </summary>
    /// <param name="index">The index value to validate.</param>
    /// <param name="sourceExpr">The source expression for error reporting.</param>
    private void ValidateListIndex(int index, Expression sourceExpr)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(
                "index",
                index,
                $"List index must be non-negative. Got: {index}");
        }
    }

    /// <summary>
    /// Builds a document path for list index access (e.g., x.Tags[0] -> #tags[0], x.Metadata.Keywords[0] -> #metadata.#keywords[0]).
    /// </summary>
    /// <param name="listExpression">The expression representing the list (e.g., x.Tags or x.Metadata.Keywords).</param>
    /// <param name="index">The list index.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The DynamoDB document path string.</returns>
    private string BuildListIndexPath(Expression listExpression, int index, ParameterExpression entityParameter, ExpressionContext context)
    {
        // List index access is only valid in filter expressions and condition expressions
        // Not valid in key condition expressions (KeysOnly mode)
        if (context.ValidationMode == ExpressionValidationMode.KeysOnly)
        {
            throw new InvalidKeyExpressionException(
                $"List index access is not supported in key condition expressions. " +
                "DynamoDB key conditions only support partition key and sort key attributes. " +
                "Use list index access in filter expressions (.WithFilter()) or condition expressions (.Where() on Put/Update/Delete) instead.",
                listExpression);
        }

        var pathBuilder = new DocumentPathBuilder(context.AttributeNames);

        // Build the path for the list property (may be nested like x.Metadata.Keywords)
        if (listExpression is MemberExpression memberExpr)
        {
            BuildMemberPathSegments(memberExpr, entityParameter, context, pathBuilder);
        }

        // Add the index segment
        pathBuilder.AddIndex(index);

        return pathBuilder.Build();
    }

    /// <summary>
    /// Builds path segments for a member expression chain into a DocumentPathBuilder.
    /// </summary>
    /// <param name="memberExpr">The member expression.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="pathBuilder">The path builder to add segments to.</param>
    private void BuildMemberPathSegments(MemberExpression memberExpr, ParameterExpression entityParameter, ExpressionContext context, DocumentPathBuilder pathBuilder)
    {
        var segments = new Stack<(string PropertyName, string AttributeName)>();

        // Collect all segments from leaf to root
        Expression? current = memberExpr;
        while (current is MemberExpression member)
        {
            var propertyName = member.Member.Name;
            var attributeName = GetDynamoDbAttributeName(member, entityParameter, context);
            segments.Push((propertyName, attributeName));
            current = member.Expression;
        }

        // Build path from root to leaf
        while (segments.Count > 0)
        {
            var (propName, attrName) = segments.Pop();
            pathBuilder.AddProperty(propName, attrName);
        }
    }

    /// <summary>
    /// Checks if a MethodCallExpression is a list indexer access pattern (e.g., x.Tags[0] via get_Item).
    /// In C# expression trees, list[index] is represented as a MethodCallExpression with method name "get_Item".
    /// Supports dynamic indices (variables, method calls, property access) as long as they
    /// don't reference the entity parameter.
    /// </summary>
    /// <param name="node">The expression to check.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="listExpression">The expression representing the list if this is a list indexer access.</param>
    /// <param name="index">The list index if this is a list indexer access.</param>
    /// <returns>True if this is a list indexer access, false otherwise.</returns>
    private bool IsListIndexerMethodCall(Expression node, ParameterExpression entityParameter, out Expression? listExpression, out int? index)
    {
        listExpression = null;
        index = null;

        // List indexer access appears as a MethodCallExpression with method name "get_Item"
        if (node is not MethodCallExpression methodCall)
            return false;

        // Check if this is an indexer call (get_Item)
        if (methodCall.Method.Name != "get_Item")
            return false;

        // The object must be an entity property access (direct or nested)
        if (methodCall.Object == null || !IsEntityPropertyAccess(methodCall.Object, entityParameter))
            return false;

        // Exclude DynamicFields indexer - that's handled separately
        if (methodCall.Object is MemberExpression memberExpr && memberExpr.Member.Name == "DynamicFields")
            return false;

        // Must have exactly one argument (the index)
        if (methodCall.Arguments.Count != 1)
            return false;

        // Check if the declaring type is a list/collection type (List<T>, IList<T>, etc.)
        var declaringType = methodCall.Method.DeclaringType;
        if (declaringType == null)
            return false;

        // Accept List<T>, IList<T>, IReadOnlyList<T>, and array types
        var isListType = declaringType.IsGenericType && (
            declaringType.GetGenericTypeDefinition() == typeof(List<>) ||
            declaringType.GetGenericTypeDefinition() == typeof(IList<>) ||
            declaringType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
        var isArrayType = declaringType.IsArray;

        if (!isListType && !isArrayType)
            return false;

        // Evaluate the index expression (supports constants, variables, method calls, property access)
        var indexValue = EvaluateIndexExpression(methodCall.Arguments[0], entityParameter, node);
        ValidateListIndex(indexValue, methodCall.Arguments[0]);
        listExpression = methodCall.Object;
        index = indexValue;
        return true;
    }

    /// <summary>
    /// Checks if an IndexExpression is a dynamic field indexer access pattern.
    /// </summary>
    private bool IsDynamicFieldIndexExpression(IndexExpression node, ParameterExpression entityParameter, out string? fieldName)
    {
        fieldName = null;

        // Check if the object is a DynamicFields property access
        if (node.Object is not MemberExpression memberExpr)
            return false;

        // Check if the member is "DynamicFields" on the entity parameter
        if (memberExpr.Member.Name != "DynamicFields")
            return false;

        // Check if the DynamicFields property is on the entity parameter
        if (memberExpr.Expression != entityParameter)
            return false;

        // Extract the field name from the indexer argument
        if (node.Arguments.Count != 1)
            return false;

        // Evaluate the field name argument
        try
        {
            fieldName = EvaluateConstantExpression<string>(node.Arguments[0]);
            return fieldName != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if an expression is a dynamic field indexer access pattern: x.DynamicFields["fieldName"]
    /// </summary>
    /// <param name="node">The expression to check.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="fieldName">The dynamic field name if this is a dynamic field access.</param>
    /// <returns>True if this is a dynamic field indexer access, false otherwise.</returns>
    private bool IsDynamicFieldIndexerAccess(Expression node, ParameterExpression entityParameter, out string? fieldName)
    {
        fieldName = null;

        // Handle IndexExpression (from Expression.MakeIndex)
        if (node is IndexExpression indexExpr)
        {
            return IsDynamicFieldIndexExpression(indexExpr, entityParameter, out fieldName);
        }

        // Pattern: x.DynamicFields["fieldName"] or x.DynamicFields[variable]
        // This appears as a MethodCallExpression for the indexer (get_Item)
        if (node is not MethodCallExpression methodCall)
            return false;

        // Check if this is an indexer call (get_Item)
        if (methodCall.Method.Name != "get_Item")
            return false;

        // Check if the object is a DynamicFields property access
        if (methodCall.Object is not MemberExpression memberExpr)
            return false;

        // Check if the member is "DynamicFields" on the entity parameter
        if (memberExpr.Member.Name != "DynamicFields")
            return false;

        // Check if the DynamicFields property is on the entity parameter
        if (memberExpr.Expression != entityParameter)
            return false;

        // Check if the declaring type is DynamicFieldCollection or DynamicFieldAccessor
        var declaringType = methodCall.Method.DeclaringType;
        if (declaringType?.Name != "DynamicFieldCollection" && declaringType?.Name != "DynamicFieldAccessor")
            return false;

        // Extract the field name from the indexer argument
        if (methodCall.Arguments.Count != 1)
            return false;

        // Evaluate the field name argument
        try
        {
            fieldName = EvaluateConstantExpression<string>(methodCall.Arguments[0]);
            return fieldName != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if an expression is a DynamicFields property access: x.DynamicFields
    /// </summary>
    /// <param name="node">The expression to check.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <returns>True if this is a DynamicFields property access, false otherwise.</returns>
    private bool IsDynamicFieldsPropertyAccess(Expression node, ParameterExpression entityParameter)
    {
        if (node is not MemberExpression memberExpr)
            return false;

        return memberExpr.Member.Name == "DynamicFields" && memberExpr.Expression == entityParameter;
    }

    /// <summary>
    /// Translates a dynamic field access to a DynamoDB attribute name placeholder.
    /// </summary>
    /// <param name="fieldName">The dynamic field name.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The attribute name placeholder.</returns>
    private string TranslateDynamicFieldAccess(string fieldName, ExpressionContext context)
    {
        // Generate attribute name placeholder for the dynamic field
        var count = context.AttributeNames.AttributeNames.Count;
        var attributeNamePlaceholder = count < 10 
            ? string.Concat("#dynField", count.ToString()) 
            : $"#dynField{count}";
        
        context.AttributeNames.WithAttribute(attributeNamePlaceholder, fieldName);
        return attributeNamePlaceholder;
    }

    /// <summary>
    /// Checks if a method call is a DynamicFields.Exists or DynamicFields.NotExists call.
    /// </summary>
    /// <param name="node">The method call expression.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="dynamoDbFunction">The translated DynamoDB function string.</param>
    /// <returns>True if this is a dynamic field existence check, false otherwise.</returns>
    private bool IsDynamicFieldExistenceCheck(MethodCallExpression node, ParameterExpression entityParameter, ExpressionContext context, out string? dynamoDbFunction)
    {
        dynamoDbFunction = null;

        // Check if this is Exists or NotExists method
        if (node.Method.Name != "Exists" && node.Method.Name != "NotExists")
            return false;

        // Check if the object is a DynamicFields property access
        if (node.Object is not MemberExpression memberExpr)
            return false;

        if (!IsDynamicFieldsPropertyAccess(memberExpr, entityParameter))
            return false;

        // Check if the declaring type is DynamicFieldCollection or DynamicFieldAccessor
        var declaringType = node.Method.DeclaringType;
        if (declaringType?.Name != "DynamicFieldCollection" && declaringType?.Name != "DynamicFieldAccessor")
            return false;

        // Extract the field name from the argument
        if (node.Arguments.Count != 1)
            return false;

        string? fieldName;
        try
        {
            fieldName = EvaluateConstantExpression<string>(node.Arguments[0]);
            if (fieldName == null)
                return false;
        }
        catch
        {
            return false;
        }

        // Translate to attribute name placeholder
        var attributeName = TranslateDynamicFieldAccess(fieldName, context);

        // Generate the appropriate DynamoDB function
        var functionName = node.Method.Name == "Exists" ? "attribute_exists" : "attribute_not_exists";
        var sb = new StringBuilder(functionName.Length + attributeName.Length + 3);
        sb.Append(functionName).Append('(').Append(attributeName).Append(')');
        dynamoDbFunction = sb.ToString();
        return true;
    }

    /// <summary>
    /// Checks if a method call is a string function on a dynamic field (StartsWith, Contains).
    /// </summary>
    /// <param name="node">The method call expression.</param>
    /// <param name="entityParameter">The entity parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="dynamoDbFunction">The translated DynamoDB function string.</param>
    /// <returns>True if this is a string function on a dynamic field, false otherwise.</returns>
    private bool IsDynamicFieldStringFunction(MethodCallExpression node, ParameterExpression entityParameter, ExpressionContext context, out string? dynamoDbFunction)
    {
        dynamoDbFunction = null;

        // Check if this is StartsWith or Contains method on string
        if (node.Method.Name != "StartsWith" && node.Method.Name != "Contains")
            return false;

        if (node.Method.DeclaringType != typeof(string))
            return false;

        if (node.Arguments.Count != 1)
            return false;

        // Check if the object is a dynamic field indexer access
        if (node.Object == null)
            return false;

        if (!IsDynamicFieldIndexerAccess(node.Object, entityParameter, out var fieldName))
            return false;

        // Translate the dynamic field to attribute name
        var attributeName = TranslateDynamicFieldAccess(fieldName!, context);

        // Evaluate the argument value
        var value = EvaluateExpression(node.Arguments[0]);
        var valueParam = CaptureValue(value, context, propertyMetadata: null);

        // Generate the appropriate DynamoDB function
        var functionName = node.Method.Name == "StartsWith" ? "begins_with" : "contains";
        var sb = new StringBuilder(functionName.Length + attributeName.Length + valueParam.Length + 5);
        sb.Append(functionName).Append('(').Append(attributeName).Append(", ").Append(valueParam).Append(')');
        dynamoDbFunction = sb.ToString();
        return true;
    }

    #endregion
}
