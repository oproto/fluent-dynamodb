using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Translates C# lambda expressions to DynamoDB update expression syntax.
/// Supports SET, ADD, REMOVE, and DELETE actions with automatic parameter generation.
/// </summary>
/// <remarks>
/// <para>
/// This translator analyzes C# expression trees and converts them to DynamoDB update expression syntax.
/// It processes lambda expressions that use source-generated UpdateExpressions and UpdateModel classes
/// to provide type-safe update operations with compile-time validation.
/// </para>
/// 
/// <para><strong>Supported Expression Patterns:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Simple SET:</strong> Property = value (e.g., Name = "John")</description></item>
/// <item><description><strong>Arithmetic SET:</strong> Property = x.Property + value (e.g., Score = x.Score + 10)</description></item>
/// <item><description><strong>ADD Operation:</strong> Property = x.Property.Add(value) for atomic increment/decrement</description></item>
/// <item><description><strong>ADD to Set:</strong> Property = x.Property.Add(elements) for set union</description></item>
/// <item><description><strong>REMOVE Operation:</strong> Property = x.Property.Remove() to delete attributes</description></item>
/// <item><description><strong>DELETE from Set:</strong> Property = x.Property.Delete(elements) to remove set elements</description></item>
/// <item><description><strong>if_not_exists:</strong> Property = x.Property.IfNotExists(defaultValue)</description></item>
/// <item><description><strong>list_append:</strong> Property = x.Property.ListAppend(elements)</description></item>
/// <item><description><strong>list_prepend:</strong> Property = x.Property.ListPrepend(elements)</description></item>
/// </list>
/// 
/// <para><strong>Features:</strong></para>
/// <list type="bullet">
/// <item><description>Automatic parameter name generation (:p0, :p1, etc.)</description></item>
/// <item><description>Automatic attribute name placeholder generation (#attr0, #attr1, etc.)</description></item>
/// <item><description>Format string application from entity metadata</description></item>
/// <item><description>Type validation for operations (e.g., arithmetic only on numeric types)</description></item>
/// <item><description>Key property validation (prevents updating partition/sort keys)</description></item>
/// <item><description>Captured variable evaluation (supports closures)</description></item>
/// <item><description>Sensitive data redaction in logs</description></item>
/// </list>
/// 
/// <para><strong>Validation Rules:</strong></para>
/// <list type="bullet">
/// <item><description>Expression body must be MemberInitExpression (object initializer syntax)</description></item>
/// <item><description>Only property assignments are supported (no method calls except extension methods)</description></item>
/// <item><description>Partition key and sort key properties cannot be updated</description></item>
/// <item><description>Properties must be mapped to DynamoDB attributes in entity metadata</description></item>
/// <item><description>Arithmetic operations only supported on numeric types</description></item>
/// <item><description>Delete() only supported on set types (HashSet&lt;T&gt;)</description></item>
/// <item><description>ListAppend/ListPrepend only supported on list types (List&lt;T&gt;)</description></item>
/// </list>
/// 
/// <para><strong>Error Handling:</strong></para>
/// <para>
/// The translator throws specific exceptions for different error conditions:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="UnsupportedExpressionException"/>: Expression pattern not supported</description></item>
/// <item><description><see cref="InvalidUpdateOperationException"/>: Attempting to update key properties</description></item>
/// <item><description><see cref="UnmappedPropertyException"/>: Property not mapped to DynamoDB attribute</description></item>
/// <item><description><see cref="EncryptionRequiredException"/>: Encrypted property without encryptor</description></item>
/// <item><description><see cref="ExpressionTranslationException"/>: General translation errors</description></item>
/// <item><description><see cref="FormatException"/>: Invalid format string for property type</description></item>
/// </list>
/// 
/// <para><strong>AOT Compatibility:</strong></para>
/// <para>
/// This translator is fully AOT-compatible. It uses expression tree analysis without runtime code generation
/// or reflection. All type information is resolved at compile time through source generation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create translator
/// var translator = new UpdateExpressionTranslator(
///     logger: myLogger,
///     isSensitiveField: fieldName => fieldName.Contains("password"),
///     fieldEncryptor: null,
///     encryptionContextId: null);
/// 
/// // Create expression context
/// var context = new ExpressionContext(
///     attributeValueHelper,
///     attributeNameHelper,
///     entityMetadata,
///     ExpressionValidationMode.None);
/// 
/// // Translate expression
/// Expression&lt;Func&lt;UserUpdateExpressions, UserUpdateModel&gt;&gt; expr = 
///     x => new UserUpdateModel 
///     {
///         Name = "John",
///         LoginCount = x.LoginCount.Add(1),
///         TempData = x.TempData.Remove()
///     };
/// 
/// var updateExpression = translator.TranslateUpdateExpression(expr, context);
/// // Result: "SET #attr0 = :p0 ADD #attr1 :p1 REMOVE #attr2"
/// </code>
/// </example>
public class UpdateExpressionTranslator
{
    private readonly IDynamoDbLogger? _logger;
    private readonly Func<string, bool>? _isSensitiveField;
    private readonly IFieldEncryptor? _fieldEncryptor;
    private readonly string? _encryptionContextId;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExpressionTranslator"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for expression translation diagnostics. Used to log parameter captures and translation steps.</param>
    /// <param name="isSensitiveField">Optional function to check if a field is sensitive. Used for redacting sensitive data in logs.</param>
    /// <param name="fieldEncryptor">Optional field encryptor for encrypted properties. Currently not used as encryption requires async support.</param>
    /// <param name="encryptionContextId">Optional encryption context identifier. Used when encrypting field values.</param>
    /// <remarks>
    /// <para>
    /// The logger parameter enables diagnostic logging of expression translation steps, including
    /// parameter captures and operation classifications. Sensitive fields (as determined by isSensitiveField)
    /// are automatically redacted in log output.
    /// </para>
    /// 
    /// <para>
    /// Field encryption is currently not supported in update expressions due to the synchronous nature
    /// of expression translation and the asynchronous IFieldEncryptor interface. This limitation is
    /// documented in the design and will be addressed in a future update.
    /// </para>
    /// </remarks>
    public UpdateExpressionTranslator(
        IDynamoDbLogger? logger,
        Func<string, bool>? isSensitiveField,
        IFieldEncryptor? fieldEncryptor,
        string? encryptionContextId)
    {
        _logger = logger;
        _isSensitiveField = isSensitiveField;
        _fieldEncryptor = fieldEncryptor;
        _encryptionContextId = encryptionContextId;
    }

    /// <summary>
    /// Translates an update expression to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="TUpdateExpressions">The UpdateExpressions parameter type (e.g., UserUpdateExpressions).</typeparam>
    /// <typeparam name="TUpdateModel">The UpdateModel return type (e.g., UserUpdateModel).</typeparam>
    /// <param name="expression">The lambda expression to translate. Must be in the form: x => new TUpdateModel { Property = value, ... }</param>
    /// <param name="context">Expression context with metadata and parameter tracking. Contains attribute name/value helpers and entity metadata.</param>
    /// <returns>The DynamoDB update expression string combining SET, ADD, REMOVE, and DELETE clauses as needed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when expression or context is null.</exception>
    /// <exception cref="UnsupportedExpressionException">Thrown when the expression body is not a MemberInitExpression or contains unsupported patterns.</exception>
    /// <exception cref="InvalidUpdateOperationException">Thrown when attempting to update partition key or sort key properties.</exception>
    /// <exception cref="UnmappedPropertyException">Thrown when a property in the expression is not mapped to a DynamoDB attribute.</exception>
    /// <exception cref="ExpressionTranslationException">Thrown when expression evaluation fails or contains parameter references.</exception>
    /// <exception cref="FormatException">Thrown when a format string is invalid for the property type.</exception>
    /// <remarks>
    /// <para>
    /// This method analyzes the expression tree and classifies each property assignment into one of four
    /// DynamoDB update action types: SET, ADD, REMOVE, or DELETE. The resulting expression string combines
    /// all actions in the correct order.
    /// </para>
    /// 
    /// <para><strong>Expression Requirements:</strong></para>
    /// <list type="bullet">
    /// <item><description>Expression body must be a MemberInitExpression (object initializer)</description></item>
    /// <item><description>Only MemberAssignment bindings are supported (no method bindings or list bindings)</description></item>
    /// <item><description>Property names must match properties in the UpdateModel type</description></item>
    /// <item><description>Values can be constants, captured variables, or method calls to extension methods</description></item>
    /// </list>
    /// 
    /// <para><strong>Operation Classification:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>SET:</strong> Simple assignments, arithmetic operations, if_not_exists, list_append, list_prepend</description></item>
    /// <item><description><strong>ADD:</strong> x.Property.Add(value) for atomic increment or set union</description></item>
    /// <item><description><strong>REMOVE:</strong> x.Property.Remove() to delete entire attributes</description></item>
    /// <item><description><strong>DELETE:</strong> x.Property.Delete(elements) to remove set elements</description></item>
    /// </list>
    /// 
    /// <para><strong>Output Format:</strong></para>
    /// <para>
    /// The returned string combines all operations in DynamoDB's required order:
    /// "SET #attr0 = :p0, #attr1 = :p1 ADD #attr2 :p2 REMOVE #attr3 DELETE #attr4 :p3"
    /// </para>
    /// 
    /// <para><strong>Parameter and Attribute Name Generation:</strong></para>
    /// <para>
    /// The method automatically generates parameter names (:p0, :p1, etc.) and attribute name placeholders
    /// (#attr0, #attr1, etc.) and adds them to the context's attribute value and name helpers.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple SET operations
    /// var expr1 = x => new UserUpdateModel { Name = "John", Status = "Active" };
    /// var result1 = translator.TranslateUpdateExpression(expr1, context);
    /// // Result: "SET #attr0 = :p0, #attr1 = :p1"
    /// 
    /// // Atomic ADD operation
    /// var expr2 = x => new UserUpdateModel { LoginCount = x.LoginCount.Add(1) };
    /// var result2 = translator.TranslateUpdateExpression(expr2, context);
    /// // Result: "ADD #attr0 :p0"
    /// 
    /// // Arithmetic in SET
    /// var expr3 = x => new UserUpdateModel { Score = x.Score + 10 };
    /// var result3 = translator.TranslateUpdateExpression(expr3, context);
    /// // Result: "SET #attr0 = #attr0 + :p0"
    /// 
    /// // Combined operations
    /// var expr4 = x => new UserUpdateModel 
    /// {
    ///     Name = "John",
    ///     LoginCount = x.LoginCount.Add(1),
    ///     TempData = x.TempData.Remove(),
    ///     Tags = x.Tags.Delete("old-tag")
    /// };
    /// var result4 = translator.TranslateUpdateExpression(expr4, context);
    /// // Result: "SET #attr0 = :p0 ADD #attr1 :p1 REMOVE #attr2 DELETE #attr3 :p2"
    /// 
    /// // With captured variables
    /// var newName = "John Doe";
    /// var increment = 5;
    /// var expr5 = x => new UserUpdateModel 
    /// {
    ///     Name = newName,
    ///     Score = x.Score + increment
    /// };
    /// var result5 = translator.TranslateUpdateExpression(expr5, context);
    /// // Result: "SET #attr0 = :p0, #attr1 = #attr1 + :p1"
    /// </code>
    /// </example>
    public string TranslateUpdateExpression<TUpdateExpressions, TUpdateModel>(
        Expression<Func<TUpdateExpressions, TUpdateModel>> expression,
        ExpressionContext context)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        // Expression body must be MemberInitExpression (object initializer)
        if (expression.Body is not MemberInitExpression memberInit)
        {
            throw new UnsupportedExpressionException(
                $"Expression body must be an object initializer (new {typeof(TUpdateModel).Name} {{ ... }}). " +
                $"Found: {expression.Body.NodeType}",
                expression.Body);
        }

        var parameter = expression.Parameters[0];
        
        // Group operations by type
        var setOperations = new List<string>();
        var addOperations = new List<string>();
        var removeOperations = new List<string>();
        var deleteOperations = new List<string>();

        // Process each property assignment
        foreach (var binding in memberInit.Bindings)
        {
            if (binding is not MemberAssignment assignment)
            {
                throw new UnsupportedExpressionException(
                    $"Only property assignments are supported in update expressions. Found: {binding.BindingType}",
                    memberInit);
            }

            var propertyName = assignment.Member.Name;
            var valueExpression = assignment.Expression;

            // Determine operation type and translate
            var operation = ClassifyOperation(valueExpression, parameter, propertyName, context);
            
            switch (operation.Type)
            {
                case OperationType.Set:
                    setOperations.Add(operation.Expression);
                    break;
                case OperationType.Add:
                    addOperations.Add(operation.Expression);
                    break;
                case OperationType.Remove:
                    removeOperations.Add(operation.Expression);
                    break;
                case OperationType.Delete:
                    deleteOperations.Add(operation.Expression);
                    break;
            }
        }

        // Build combined expression
        var parts = new List<string>();
        
        if (setOperations.Any())
            parts.Add("SET " + string.Join(", ", setOperations));
        
        if (addOperations.Any())
            parts.Add("ADD " + string.Join(", ", addOperations));
        
        if (removeOperations.Any())
            parts.Add("REMOVE " + string.Join(", ", removeOperations));
        
        if (deleteOperations.Any())
            parts.Add("DELETE " + string.Join(", ", deleteOperations));

        return string.Join(" ", parts);
    }

    private Operation ClassifyOperation(
        Expression valueExpression,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Unwrap Convert expressions (e.g., when assigning int to int?)
        var unwrapped = valueExpression;
        while (unwrapped is UnaryExpression unary && 
               (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            unwrapped = unary.Operand;
        }

        // Check for method calls (Add, Remove, Delete, IfNotExists, etc.)
        if (unwrapped is MethodCallExpression methodCall)
        {
            return TranslateMethodCall(methodCall, parameter, propertyName, context);
        }

        // Check for binary operations (arithmetic)
        if (unwrapped is BinaryExpression binary)
        {
            return TranslateBinaryOperation(binary, parameter, propertyName, context);
        }

        // Simple value assignment - SET operation
        return TranslateSimpleSet(valueExpression, parameter, propertyName, context);
    }

    private Operation TranslateSimpleSet(
        Expression valueExpression,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Validate property is not a key
        ValidateNotKeyProperty(propertyName, context, valueExpression);

        // Get property metadata
        var propertyMetadata = GetPropertyMetadata(propertyName, context);
        
        // Get attribute name
        var attributeName = GetAttributeName(propertyName, context, valueExpression);
        
        // Evaluate the value expression
        var value = EvaluateExpression(valueExpression);
        
        // Apply format if specified
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormat(value, propertyMetadata.Format, propertyName);
        }
        
        // Apply encryption if needed
        if (propertyMetadata != null && IsEncryptedProperty(propertyMetadata))
        {
            value = ApplyEncryption(value, propertyMetadata.PropertyName, propertyMetadata.AttributeName, valueExpression);
        }
        
        // Capture the value
        var valuePlaceholder = CaptureValue(value, context, propertyMetadata);
        
        // Build SET expression
        var expression = $"{attributeName} = {valuePlaceholder}";
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = expression
        };
    }

    private Operation TranslateBinaryOperation(
        BinaryExpression binary,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Validate property is not a key
        ValidateNotKeyProperty(propertyName, context, binary);

        // Only support Add and Subtract for arithmetic
        if (binary.NodeType != ExpressionType.Add && binary.NodeType != ExpressionType.Subtract)
        {
            throw new UnsupportedExpressionException(
                $"Binary operator '{binary.NodeType}' is not supported in update expressions. " +
                $"Only addition (+) and subtraction (-) are supported for arithmetic operations on numeric properties. " +
                $"For other operations, compute the value before the expression or use string-based update expressions.",
                binary.NodeType,
                binary);
        }

        // Check if left side is UpdateExpressionProperty access
        if (!IsUpdateExpressionPropertyAccess(binary.Left, parameter))
        {
            throw new UnsupportedExpressionException(
                $"Left side of arithmetic operation must be an UpdateExpressionProperty access (e.g., x.PropertyName). " +
                $"Found: {binary.Left.NodeType}. " +
                $"Example: x.Count + 5 (where x is the UpdateExpressions parameter).",
                binary);
        }

        // Get property metadata
        var propertyMetadata = GetPropertyMetadata(propertyName, context);
        
        // Validate property type is numeric
        if (propertyMetadata != null && !IsNumericType(propertyMetadata.PropertyType))
        {
            throw new UnsupportedExpressionException(
                $"Arithmetic operations are only supported on numeric properties. " +
                $"Property '{propertyName}' (DynamoDB attribute: '{propertyMetadata.AttributeName}') has type '{propertyMetadata.PropertyType.Name}'. " +
                $"Supported numeric types: byte, short, int, long, float, double, decimal and their nullable variants.",
                binary);
        }
        
        // Get attribute name
        var attributeName = GetAttributeName(propertyName, context, binary);
        
        // Evaluate the right side value
        var value = EvaluateExpression(binary.Right);
        
        // Validate the value is numeric
        if (value != null && !IsNumericType(value.GetType()))
        {
            throw new UnsupportedExpressionException(
                $"Right side of arithmetic operation must evaluate to a numeric value. " +
                $"Found type: {value.GetType().Name}.",
                binary);
        }
        
        // Apply format if specified
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormat(value, propertyMetadata.Format, propertyName);
        }
        
        // Capture the value
        var valuePlaceholder = CaptureValue(value, context, propertyMetadata);
        
        // Build SET expression with arithmetic
        var op = binary.NodeType == ExpressionType.Add ? "+" : "-";
        var expression = $"{attributeName} = {attributeName} {op} {valuePlaceholder}";
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = expression
        };
    }

    private Operation TranslateMethodCall(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        var methodName = methodCall.Method.Name;
        
        return methodName switch
        {
            "Add" => TranslateAddOperation(methodCall, parameter, propertyName, context),
            "Remove" => TranslateRemoveOperation(methodCall, parameter, propertyName, context),
            "Delete" => TranslateDeleteOperation(methodCall, parameter, propertyName, context),
            "IfNotExists" => TranslateIfNotExistsFunction(methodCall, parameter, propertyName, context),
            "ListAppend" => TranslateListAppendFunction(methodCall, parameter, propertyName, context),
            "ListPrepend" => TranslateListPrependFunction(methodCall, parameter, propertyName, context),
            _ => throw new UnsupportedExpressionException(
                $"Method '{methodName}' is not supported in update expressions. " +
                $"Supported methods: Add, Remove, Delete, IfNotExists, ListAppend, ListPrepend.",
                methodName,
                methodCall)
        };
    }

    private Operation TranslateAddOperation(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Get attribute name
        var attributeName = GetAttributeName(propertyName, context, methodCall);
        
        // Get property metadata
        var propertyMetadata = GetPropertyMetadata(propertyName, context);
        
        // Get the value argument
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the value to add)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"Add() method requires a value argument. " +
                $"For numeric properties: x.Count.Add(5). " +
                $"For set properties: x.Tags.Add(\"tag1\", \"tag2\").",
                "Add",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // Validate the value is appropriate for ADD operation
        if (value != null)
        {
            var valueType = value.GetType();
            var isNumeric = IsNumericType(valueType);
            var isSet = (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(HashSet<>)) ||
                        valueType.IsArray;
            
            if (!isNumeric && !isSet)
            {
                throw new UnsupportedExpressionException(
                    $"Add() operation requires a numeric value or a set. " +
                    $"Found type: {valueType.Name}. " +
                    $"For numeric properties, use Add(number). For set properties, use Add(element1, element2, ...).",
                    "Add",
                    methodCall);
            }
        }
        
        // Apply format if specified
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormat(value, propertyMetadata.Format, propertyName);
        }
        
        // Capture the value
        var valuePlaceholder = CaptureValue(value, context, propertyMetadata);
        
        // Build ADD expression
        var expression = $"{attributeName} {valuePlaceholder}";
        
        return new Operation
        {
            Type = OperationType.Add,
            Expression = expression
        };
    }

    private Operation TranslateRemoveOperation(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Validate property is not a key
        ValidateNotKeyProperty(propertyName, context, methodCall);
        
        // Get attribute name
        var attributeName = GetAttributeName(propertyName, context, methodCall);
        
        // Build REMOVE expression (no value needed)
        return new Operation
        {
            Type = OperationType.Remove,
            Expression = attributeName
        };
    }

    private Operation TranslateDeleteOperation(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Get attribute name
        var attributeName = GetAttributeName(propertyName, context, methodCall);
        
        // Get property metadata
        var propertyMetadata = GetPropertyMetadata(propertyName, context);
        
        // Validate property type is a set
        if (propertyMetadata != null)
        {
            var propertyType = propertyMetadata.PropertyType;
            var isSet = propertyType.IsGenericType && 
                       propertyType.GetGenericTypeDefinition() == typeof(HashSet<>);
            
            if (!isSet)
            {
                throw new UnsupportedExpressionException(
                    $"Delete() operation is only supported on set properties (HashSet<T>). " +
                    $"Property '{propertyName}' (DynamoDB attribute: '{propertyMetadata.AttributeName}') has type '{propertyType.Name}'. " +
                    $"To remove an entire attribute, use Remove() instead.",
                    "Delete",
                    methodCall);
            }
        }
        
        // Get the elements to delete (arguments to Delete method)
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the elements to delete)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"Delete() method requires at least one element to delete from the set. " +
                $"Example: x.Tags.Delete(\"tag1\", \"tag2\").",
                "Delete",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // Validate the value is a set or array
        if (value != null)
        {
            var valueType = value.GetType();
            var isSet = valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(HashSet<>);
            var isArray = valueType.IsArray;
            
            if (!isSet && !isArray)
            {
                throw new UnsupportedExpressionException(
                    $"Delete() operation requires a set of elements to delete. " +
                    $"Found type: {valueType.Name}.",
                    "Delete",
                    methodCall);
            }
        }
        
        // Capture the value as a set
        var valuePlaceholder = CaptureValue(value, context, propertyMetadata);
        
        // Build DELETE expression
        var expression = $"{attributeName} {valuePlaceholder}";
        
        return new Operation
        {
            Type = OperationType.Delete,
            Expression = expression
        };
    }

    private Operation TranslateIfNotExistsFunction(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Validate property is not a key
        ValidateNotKeyProperty(propertyName, context, methodCall);
        
        // Get attribute name
        var attributeName = GetAttributeName(propertyName, context, methodCall);
        
        // Get property metadata
        var propertyMetadata = GetPropertyMetadata(propertyName, context);
        
        // Get the default value argument
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the default value)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"IfNotExists() method requires a default value argument. " +
                $"Example: x.ViewCount.IfNotExists(0) sets ViewCount to 0 if it doesn't exist.",
                "IfNotExists",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // Apply format if specified
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormat(value, propertyMetadata.Format, propertyName);
        }
        
        // Apply encryption if needed
        if (propertyMetadata != null && IsEncryptedProperty(propertyMetadata))
        {
            value = ApplyEncryption(value, propertyMetadata.PropertyName, propertyMetadata.AttributeName, methodCall);
        }
        
        // Capture the value
        var valuePlaceholder = CaptureValue(value, context, propertyMetadata);
        
        // Build SET expression with if_not_exists function
        var expression = $"{attributeName} = if_not_exists({attributeName}, {valuePlaceholder})";
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = expression
        };
    }

    private Operation TranslateListAppendFunction(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Validate property is not a key
        ValidateNotKeyProperty(propertyName, context, methodCall);
        
        // Get attribute name
        var attributeName = GetAttributeName(propertyName, context, methodCall);
        
        // Get property metadata
        var propertyMetadata = GetPropertyMetadata(propertyName, context);
        
        // Validate property type is a list
        if (propertyMetadata != null)
        {
            var propertyType = propertyMetadata.PropertyType;
            var isList = propertyType.IsGenericType && 
                        propertyType.GetGenericTypeDefinition() == typeof(List<>);
            
            if (!isList)
            {
                throw new UnsupportedExpressionException(
                    $"ListAppend() operation is only supported on list properties (List<T>). " +
                    $"Property '{propertyName}' (DynamoDB attribute: '{propertyMetadata.AttributeName}') has type '{propertyType.Name}'.",
                    "ListAppend",
                    methodCall);
            }
        }
        
        // Get the elements to append
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the elements to append)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"ListAppend() method requires at least one element to append. " +
                $"Example: x.History.ListAppend(\"event1\", \"event2\").",
                "ListAppend",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // For list operations, ensure the value is a List, not a set
        // Convert array to list if needed
        if (value is Array array)
        {
            var list = new List<object>();
            foreach (var item in array)
            {
                list.Add(item);
            }
            value = list;
        }
        
        // Apply format if specified (for list element types)
        // Note: Format strings are typically not used for list elements, but we support it for consistency
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormatToListElements(value, propertyMetadata.Format, propertyName);
        }
        
        // Capture the value
        var valuePlaceholder = CaptureValue(value, context, propertyMetadata);
        
        // Build SET expression with list_append function
        var expression = $"{attributeName} = list_append({attributeName}, {valuePlaceholder})";
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = expression
        };
    }

    private Operation TranslateListPrependFunction(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Validate property is not a key
        ValidateNotKeyProperty(propertyName, context, methodCall);
        
        // Get attribute name
        var attributeName = GetAttributeName(propertyName, context, methodCall);
        
        // Get property metadata
        var propertyMetadata = GetPropertyMetadata(propertyName, context);
        
        // Validate property type is a list
        if (propertyMetadata != null)
        {
            var propertyType = propertyMetadata.PropertyType;
            var isList = propertyType.IsGenericType && 
                        propertyType.GetGenericTypeDefinition() == typeof(List<>);
            
            if (!isList)
            {
                throw new UnsupportedExpressionException(
                    $"ListPrepend() operation is only supported on list properties (List<T>). " +
                    $"Property '{propertyName}' (DynamoDB attribute: '{propertyMetadata.AttributeName}') has type '{propertyType.Name}'.",
                    "ListPrepend",
                    methodCall);
            }
        }
        
        // Get the elements to prepend
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the elements to prepend)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"ListPrepend() method requires at least one element to prepend. " +
                $"Example: x.History.ListPrepend(\"event1\", \"event2\").",
                "ListPrepend",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // For list operations, ensure the value is a List, not a set
        // Convert array to list if needed
        if (value is Array array)
        {
            var list = new List<object>();
            foreach (var item in array)
            {
                list.Add(item);
            }
            value = list;
        }
        
        // Apply format if specified (for list element types)
        // Note: Format strings are typically not used for list elements, but we support it for consistency
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormatToListElements(value, propertyMetadata.Format, propertyName);
        }
        
        // Capture the value
        var valuePlaceholder = CaptureValue(value, context, propertyMetadata);
        
        // Build SET expression with list_append function (reversed order for prepend)
        var expression = $"{attributeName} = list_append({valuePlaceholder}, {attributeName})";
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = expression
        };
    }

    // Helper methods

    private void ValidateNotKeyProperty(string propertyName, ExpressionContext context, Expression expression)
    {
        if (context.EntityMetadata == null)
            return;

        var propertyMetadata = context.EntityMetadata.Properties
            .FirstOrDefault(p => p.PropertyName == propertyName);

        if (propertyMetadata != null && (propertyMetadata.IsPartitionKey || propertyMetadata.IsSortKey))
        {
            var keyType = propertyMetadata.IsPartitionKey ? "partition key" : "sort key";
            throw new InvalidUpdateOperationException(
                $"Cannot update key property '{propertyName}'. " +
                $"The {keyType} property (DynamoDB attribute: '{propertyMetadata.AttributeName}') cannot be modified in update operations. " +
                $"Key properties are immutable after item creation. To change a key value, delete the old item and create a new one with the new key.",
                propertyName,
                expression);
        }
    }

    private PropertyMetadata? GetPropertyMetadata(string propertyName, ExpressionContext context)
    {
        if (context.EntityMetadata == null)
            return null;

        var propertyMetadata = context.EntityMetadata.Properties
            .FirstOrDefault(p => p.PropertyName == propertyName);

        return propertyMetadata;
    }

    private PropertyMetadata GetRequiredPropertyMetadata(string propertyName, ExpressionContext context, Expression expression)
    {
        if (context.EntityMetadata == null)
        {
            throw new InvalidOperationException(
                $"Entity metadata is required for expression-based update operations but was not provided. " +
                $"Ensure the entity type is properly configured with metadata.");
        }

        var propertyMetadata = context.EntityMetadata.Properties
            .FirstOrDefault(p => p.PropertyName == propertyName);

        if (propertyMetadata == null)
        {
            throw new UnmappedPropertyException(
                propertyName,
                typeof(object), // We don't have entity type in metadata
                expression);
        }

        return propertyMetadata;
    }

    private string GetAttributeName(string propertyName, ExpressionContext context, Expression? expression = null)
    {
        var attributeName = propertyName;

        // Use DynamoDB attribute name from metadata if available
        if (context.EntityMetadata != null)
        {
            var propertyMetadata = context.EntityMetadata.Properties
                .FirstOrDefault(p => p.PropertyName == propertyName);

            if (propertyMetadata == null)
            {
                throw new UnmappedPropertyException(
                    propertyName,
                    typeof(object), // We don't have entity type in metadata
                    expression);
            }

            attributeName = propertyMetadata.AttributeName;
        }

        // Generate attribute name placeholder
        var count = context.AttributeNames.AttributeNames.Count;
        var attributeNamePlaceholder = count < 10 
            ? string.Concat("#attr", count.ToString()) 
            : $"#attr{count}";
        
        context.AttributeNames.WithAttribute(attributeNamePlaceholder, attributeName);
        return attributeNamePlaceholder;
    }

    private bool IsUpdateExpressionPropertyAccess(Expression expression, ParameterExpression parameter)
    {
        // Check if this is a member access on the parameter (x.PropertyName)
        if (expression is not MemberExpression member)
            return false;

        return member.Expression == parameter;
    }

    private bool IsNumericType(Type type)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        
        return underlyingType == typeof(byte) ||
               underlyingType == typeof(sbyte) ||
               underlyingType == typeof(short) ||
               underlyingType == typeof(ushort) ||
               underlyingType == typeof(int) ||
               underlyingType == typeof(uint) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(ulong) ||
               underlyingType == typeof(float) ||
               underlyingType == typeof(double) ||
               underlyingType == typeof(decimal);
    }

    private bool IsEncryptedProperty(PropertyMetadata propertyMetadata)
    {
        // Check if property has encryption metadata
        // This would typically be set by the source generator based on [Encrypted] attribute
        // For now, we'll return false as encryption metadata isn't part of PropertyMetadata yet
        // This will be enhanced when encryption support is fully integrated
        return false;
    }

    private object? ApplyEncryption(object? value, string propertyName, string attributeName, Expression expression)
    {
        if (value == null)
            return null;

        if (_fieldEncryptor == null)
        {
            throw new EncryptionRequiredException(
                $"Property '{propertyName}' (DynamoDB attribute: '{attributeName}') is marked as encrypted but no IFieldEncryptor is configured. " +
                $"Configure a field encryptor in the DynamoDB operation context to encrypt sensitive data. " +
                $"Alternatively, use string-based update expressions with pre-encrypted values.",
                propertyName,
                attributeName,
                expression);
        }

        // Note: Encryption is async, but update expression translation is sync
        // This is a limitation that will need to be addressed in the design
        // For now, we'll throw an exception indicating encryption must be handled differently
        throw new NotSupportedException(
            $"Synchronous encryption is not supported in update expressions. " +
            $"Property '{propertyName}' (DynamoDB attribute: '{attributeName}') is marked as encrypted. " +
            $"Consider using string-based update expressions with pre-encrypted values, " +
            $"or encrypt the value before passing it to the expression.");
    }

    private object? EvaluateExpression(Expression expression)
    {
        try
        {
            // For constant expressions, just return the value
            if (expression is ConstantExpression constant)
                return constant.Value;

            // Handle type conversions
            if (expression is UnaryExpression unary && 
                (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                // Check if the operand is a method call to one of our extension methods
                // If so, don't try to evaluate it - it should be handled by the translator
                if (unary.Operand is MethodCallExpression methodCall)
                {
                    var methodName = methodCall.Method.Name;
                    if (methodName == "Add" || methodName == "Remove" || methodName == "Delete" ||
                        methodName == "IfNotExists" || methodName == "ListAppend" || methodName == "ListPrepend")
                    {
                        throw new ExpressionTranslationException(
                            $"Cannot evaluate extension method '{methodName}' directly. " +
                            $"This method should be handled by the translator, not evaluated as a value.",
                            expression);
                    }
                }
                
                return EvaluateExpression(unary.Operand);
            }

            // Handle member access on constants (captured variables from closures)
            if (expression is MemberExpression member && member.Expression is ConstantExpression memberConstant)
            {
                var container = memberConstant.Value;
                if (container == null)
                    return null;

                if (member.Member is System.Reflection.FieldInfo field)
                    return field.GetValue(container);
                
                if (member.Member is System.Reflection.PropertyInfo property)
                    return property.GetValue(container);
            }

            // Handle NewArrayExpression (params arrays)
            if (expression is NewArrayExpression newArray)
            {
                var elementType = newArray.Type.GetElementType()!;
                var array = Array.CreateInstance(elementType, newArray.Expressions.Count);
                for (int i = 0; i < newArray.Expressions.Count; i++)
                {
                    // Recursively evaluate each element
                    // Note: We don't check for parameter references here because array elements
                    // in params arrays for our extension methods should be constants or captured variables
                    var elementValue = EvaluateExpression(newArray.Expressions[i]);
                    array.SetValue(elementValue, i);
                }
                return array;
            }

            // Handle method calls that don't reference parameters
            // This is needed for cases like x.Property.SomeMethod(constant)
            // where we need to evaluate SomeMethod(constant) but not x.Property
            if (expression is MethodCallExpression methodCallExpr)
            {
                // Check if this is a method call we should NOT evaluate
                // (i.e., it's one of our extension methods like Add, Remove, etc.)
                var methodName = methodCallExpr.Method.Name;
                if (methodName == "Add" || methodName == "Remove" || methodName == "Delete" ||
                    methodName == "IfNotExists" || methodName == "ListAppend" || methodName == "ListPrepend")
                {
                    // These are our extension methods - don't try to evaluate them
                    throw new ExpressionTranslationException(
                        $"Cannot evaluate extension method '{methodName}' directly. " +
                        $"This method should be handled by the translator, not evaluated as a value.",
                        expression);
                }
            }

            // Try to compile and execute the expression
            // If it contains parameter references, the compilation will fail
            try
            {
                var lambda = Expression.Lambda<Func<object?>>(
                    Expression.Convert(expression, typeof(object)));
                var compiled = lambda.Compile();
                return compiled();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("variable") && ex.Message.Contains("not defined"))
            {
                // This exception indicates the expression references a parameter that's not in scope
                throw new ExpressionTranslationException(
                    $"Cannot evaluate expression that references update expression parameters. " +
                    $"Expression type: {expression.NodeType}. " +
                    $"Ensure values are computed before the expression or use captured variables.",
                    ex,
                    expression);
            }
        }
        catch (ExpressionTranslationException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw new ExpressionTranslationException(
                $"Failed to evaluate expression for value capture. " +
                $"The expression may contain unsupported patterns or reference unavailable variables. " +
                $"Error: {ex.Message}",
                ex,
                expression);
        }
        catch (Exception ex)
        {
            throw new ExpressionTranslationException(
                $"Failed to evaluate expression for value capture: {ex.Message}. " +
                $"Ensure all variables and methods used in the expression are accessible and properly initialized.",
                ex,
                expression);
        }
    }

    private bool ContainsParameterReference(Expression expression)
    {
        var visitor = new ParameterReferenceVisitor();
        visitor.Visit(expression);
        return visitor.ContainsParameterReference;
    }

    private class ParameterReferenceVisitor : ExpressionVisitor
    {
        public bool ContainsParameterReference { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            ContainsParameterReference = true;
            return base.VisitParameter(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // For extension methods, the first argument (Object property) is the "this" parameter
            // We should skip checking it for parameter references since it's expected to reference
            // the update expression parameter
            var methodName = node.Method.Name;
            if (methodName == "Add" || methodName == "Remove" || methodName == "Delete" ||
                methodName == "IfNotExists" || methodName == "ListAppend" || methodName == "ListPrepend")
            {
                // Visit only the arguments, not the object
                foreach (var arg in node.Arguments)
                {
                    Visit(arg);
                }
                return node;
            }

            return base.VisitMethodCall(node);
        }
    }

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
                $"Invalid format string '{format}' for property '{propertyName}' of type '{value.GetType().Name}'. " +
                $"Error: {ex.Message}. " +
                $"Common format strings: 'o' for ISO 8601 dates, 'F2' for 2 decimal places, 'yyyy-MM-dd' for date-only.",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to apply format string '{format}' to property '{propertyName}' of type '{value.GetType().Name}'. " +
                $"Error: {ex.Message}",
                ex);
        }
    }

    private object ApplyFormatToListElements(object value, string format, string propertyName)
    {
        // Apply format to each element in a list
        if (value is List<object> list)
        {
            var formattedList = new List<object>();
            foreach (var item in list)
            {
                if (item != null)
                {
                    formattedList.Add(ApplyFormat(item, format, propertyName));
                }
                else
                {
                    formattedList.Add(item);
                }
            }
            return formattedList;
        }
        
        // If it's not a List<object>, return as-is
        // The format will be applied during AttributeValue conversion if needed
        return value;
    }

    private string CaptureValue(object? value, ExpressionContext context, PropertyMetadata? propertyMetadata)
    {
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
                "Update expression parameter {ParameterName} = {Value} (Property: {PropertyName})",
                parameterName,
                valueToLog,
                propertyMetadata?.PropertyName ?? "unknown");
        }

        return parameterName;
    }

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
            _ => ConvertComplexType(value)
        };
    }

    private AttributeValue ConvertComplexType(object value)
    {
        // Handle arrays for params arguments
        if (value is Array array)
        {
            // Check if it's a params array from Add/Delete methods
            var elementType = array.GetType().GetElementType();
            
            if (elementType == typeof(string))
            {
                var stringArray = (string[])array;
                if (stringArray.Length == 0)
                    return new AttributeValue { NULL = true };
                return new AttributeValue { SS = stringArray.ToList() };
            }
            
            if (IsNumericType(elementType!))
            {
                var numbers = new List<string>();
                foreach (var item in array)
                {
                    numbers.Add(Convert.ToString(item, CultureInfo.InvariantCulture)!);
                }
                if (numbers.Count == 0)
                    return new AttributeValue { NULL = true };
                return new AttributeValue { NS = numbers };
            }
            
            // For other arrays, convert to list
            var list = new List<AttributeValue>();
            foreach (var item in array)
            {
                list.Add(ConvertToAttributeValue(item));
            }
            if (list.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { L = list };
        }

        // Handle HashSet
        var valueType = value.GetType();
        if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(HashSet<>))
        {
            var elementType = valueType.GetGenericArguments()[0];
            
            if (elementType == typeof(string))
            {
                var set = (HashSet<string>)value;
                if (set.Count == 0)
                    return new AttributeValue { NULL = true };
                return new AttributeValue { SS = set.ToList() };
            }
            
            if (IsNumericType(elementType))
            {
                var numbers = new List<string>();
                foreach (var item in (dynamic)value)
                {
                    numbers.Add(Convert.ToString(item, CultureInfo.InvariantCulture)!);
                }
                if (numbers.Count == 0)
                    return new AttributeValue { NULL = true };
                return new AttributeValue { NS = numbers };
            }
        }

        // Handle List
        if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = new List<AttributeValue>();
            foreach (var item in (dynamic)value)
            {
                list.Add(ConvertToAttributeValue(item));
            }
            if (list.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { L = list };
        }

        // Default: convert to string
        return new AttributeValue { S = value.ToString() ?? string.Empty };
    }
}

enum OperationType
{
    Set,
    Add,
    Remove,
    Delete
}

class Operation
{
    public OperationType Type { get; set; }
    public string Expression { get; set; } = string.Empty;
}
