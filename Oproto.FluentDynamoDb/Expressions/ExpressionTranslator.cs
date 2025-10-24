using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Translates C# lambda expressions to DynamoDB expression syntax.
/// AOT-safe implementation that analyzes expression trees without dynamic code generation.
/// </summary>
public class ExpressionTranslator
{
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
            
            // Add parentheses to ensure correct precedence
            return $"({left}) {op} ({right})";
        }

        // Handle comparison operators (==, !=, <, >, <=, >=)
        var leftSide = Visit(node.Left, entityParameter, context);
        var rightSide = Visit(node.Right, entityParameter, context);

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

        return $"{leftSide} {dynamoDbOperator} {rightSide}";
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

            // Generate attribute name placeholder
            var attributeNamePlaceholder = $"#attr{context.AttributeNames.AttributeNames.Count}";
            context.AttributeNames.WithAttribute(attributeNamePlaceholder, propertyName);
            return attributeNamePlaceholder;
        }

        // This is value capture (accessing a variable or closure)
        // Evaluate the member expression to get its value
        var value = EvaluateExpression(node);
        return CaptureValue(value, context);
    }

    /// <summary>
    /// Visits a constant expression node.
    /// </summary>
    private string VisitConstant(ConstantExpression node, ExpressionContext context)
    {
        return CaptureValue(node.Value, context);
    }

    /// <summary>
    /// Visits a unary expression node (operators like !).
    /// </summary>
    private string VisitUnary(UnaryExpression node, ParameterExpression entityParameter, ExpressionContext context)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            var operand = Visit(node.Operand, entityParameter, context);
            return $"NOT ({operand})";
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
        return CaptureValue(value, context);
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
    private string CaptureValue(object? value, ExpressionContext context)
    {
        // Convert the value to an AttributeValue
        var attributeValue = ConvertToAttributeValue(value);

        // Generate a unique parameter name
        var parameterName = context.ParameterGenerator.GenerateParameterName();

        // Add to the context
        context.AttributeValues.AttributeValues.Add(parameterName, attributeValue);

        return parameterName;
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
}
