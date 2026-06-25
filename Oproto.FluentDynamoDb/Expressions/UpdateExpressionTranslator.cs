using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.Requests;

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
    /// <param name="fieldEncryptor">Optional field encryptor for encrypted properties. Used to mark parameters that require encryption.</param>
    /// <param name="encryptionContextId">Optional encryption context identifier. Used when encrypting field values.</param>
    /// <remarks>
    /// <para>
    /// The logger parameter enables diagnostic logging of expression translation steps, including
    /// parameter captures and operation classifications. Sensitive fields (as determined by isSensitiveField)
    /// are automatically redacted in log output.
    /// </para>
    /// 
    /// <para><strong>Encryption Behavior:</strong></para>
    /// <para>
    /// When a property marked with <c>[Encrypted]</c> is updated via an update expression, the translator
    /// uses a deferred encryption approach to handle the asynchronous nature of the IFieldEncryptor interface:
    /// </para>
    /// <list type="number">
    /// <item><description><strong>Parameter Metadata Tracking:</strong> The translator marks parameters that require encryption
    /// by adding entries to the ExpressionContext.ParameterMetadata collection. These entries include the parameter name,
    /// property name, attribute name, and a flag indicating encryption is required.</description></item>
    /// <item><description><strong>Deferred Encryption:</strong> The actual encryption is deferred to the request builder layer
    /// (e.g., UpdateItemRequestBuilder, TransactUpdateBuilder) where async operations are natural. The request builder
    /// checks for marked parameters before sending the request to DynamoDB.</description></item>
    /// <item><description><strong>Async Encryption:</strong> The request builder calls IFieldEncryptor.EncryptAsync for each
    /// marked parameter, properly awaiting the encryption operation without blocking.</description></item>
    /// <item><description><strong>Value Replacement:</strong> After encryption, the request builder replaces the plaintext
    /// AttributeValue in ExpressionAttributeValues with the encrypted (base64-encoded) value.</description></item>
    /// </list>
    /// <para>
    /// This architectural approach ensures proper async handling, maintains separation of concerns (translator builds
    /// expressions, request builder handles I/O), and avoids performance compromises from blocking async calls.
    /// </para>
    /// 
    /// <para><strong>Encryption Requirements:</strong></para>
    /// <list type="bullet">
    /// <item><description>If an encrypted property is updated but no IFieldEncryptor is configured, the request builder
    /// will throw an InvalidOperationException with guidance on configuring the encryptor.</description></item>
    /// <item><description>The IFieldEncryptor must be configured in the DynamoDbOperationContext passed to the table instance.</description></item>
    /// <item><description>Encryption is applied automatically - no manual encryption calls are needed in update expressions.</description></item>
    /// </list>
    /// 
    /// <para><strong>Example with Encrypted Property:</strong></para>
    /// <code>
    /// // Entity with encrypted property
    /// public class User
    /// {
    ///     [DynamoDbAttribute("ssn")]
    ///     [Encrypted]
    ///     public string SocialSecurityNumber { get; set; }
    /// }
    /// 
    /// // Update expression - encryption happens automatically
    /// await table.Update
    ///     .WithKey("pk", userId)
    ///     .Set(x => new UserUpdateModel 
    ///     { 
    ///         SocialSecurityNumber = newSsn  // Marked for encryption, encrypted by request builder
    ///     })
    ///     .ExecuteAsync();
    /// </code>
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
        
        // Track pending computed field source property assignments for later recomputation
        var pendingComputedAssignments = new Dictionary<string, object?>();
        
        // Track properties that went through the normal SET/ADD/REMOVE/DELETE flow
        // (used to detect mixed direct + source assignment for computed fields)
        var directlyAssignedProperties = new HashSet<string>();

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

            // Special handling for DynamicFields property
            if (propertyName == "DynamicFields")
            {
                var dynamicFieldOperations = TranslateDynamicFieldsAssignment(valueExpression, context);
                foreach (var op in dynamicFieldOperations)
                {
                    switch (op.Type)
                    {
                        case OperationType.Set:
                            setOperations.Add(op.Expression);
                            break;
                        case OperationType.Remove:
                            removeOperations.Add(op.Expression);
                            break;
                    }
                }
                continue;
            }

            // Check if this is a source/extracted property of a computed field
            if (context.EntityMetadata != null && IsComputedSourceProperty(propertyName, context))
            {
                // FDDB071: Validate the value does not reference the entity parameter.
                // Computed fields are evaluated client-side and require known values at translation time.
                if (ReferencesEntityParameter(valueExpression, parameter))
                {
                    ComputedFieldDiagnostics.ThrowEntityParameterReference(propertyName);
                }

                // Evaluate and store for later recomputation — do NOT generate a SET
                var evaluatedValue = EvaluateExpression(valueExpression);
                pendingComputedAssignments[propertyName] = evaluatedValue;
                continue; // Skip normal operation classification
            }

            // Determine operation type and translate
            var operation = ClassifyOperation(valueExpression, parameter, propertyName, context);
            
            // Track this property as directly assigned (for computed field mixed-assignment detection)
            directlyAssignedProperties.Add(propertyName);
            
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
                case OperationType.Skip:
                    // Property should be skipped - do not add any operation
                    break;
            }
        }

        // Post-processing: Validate and process computed field assignments
        // This is called after all bindings have been processed to check completeness
        // and generate the recomputed SET expression for computed fields.
        if (context.EntityMetadata != null && pendingComputedAssignments.Count > 0)
        {
            ValidateAndProcessComputedFields(pendingComputedAssignments, directlyAssignedProperties, context, setOperations);
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
        return ClassifyOperationWithPath(valueExpression, parameter, propertyName, context, Array.Empty<string>());
    }

    /// <summary>
    /// Classifies an operation with support for nested property paths.
    /// </summary>
    /// <param name="valueExpression">The value expression to classify.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="propertyName">The property name being updated.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="pathPrefix">The path prefix for nested properties (e.g., ["Address"] for Address.City).</param>
    /// <returns>An operation representing the update.</returns>
    private Operation ClassifyOperationWithPath(
        Expression valueExpression,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context,
        string[] pathPrefix)
    {
        // Unwrap Convert expressions (e.g., when assigning int to int?)
        var unwrapped = valueExpression;
        while (unwrapped is UnaryExpression unary && 
               (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            unwrapped = unary.Operand;
        }

        // Handle conditional expressions (ternary operator)
        if (unwrapped is ConditionalExpression conditional)
        {
            return HandleConditionalUpdateWithPath(conditional, parameter, propertyName, context, pathPrefix);
        }

        // Check for nested MemberInitExpression (nested object initializer)
        // e.g., ShippingAddress = new AddressUpdateModel { City = "Portland" }
        if (unwrapped is MemberInitExpression nestedInit)
        {
            return TranslateNestedMemberInit(nestedInit, parameter, propertyName, context, pathPrefix);
        }

        // Check for method calls (Add, Remove, Delete, IfNotExists, NoUpdate, etc.)
        if (unwrapped is MethodCallExpression methodCall)
        {
            // Check for NoUpdate() first - this signals the property should be skipped
            if (IsNoUpdateMethodCall(methodCall))
            {
                return new Operation
                {
                    Type = OperationType.Skip,
                    Expression = string.Empty
                };
            }
            
            return TranslateMethodCallWithPath(methodCall, parameter, propertyName, context, pathPrefix);
        }

        // Check for binary operations (arithmetic)
        if (unwrapped is BinaryExpression binary)
        {
            return TranslateBinaryOperationWithPath(binary, parameter, propertyName, context, pathPrefix);
        }

        // Simple value assignment - SET operation
        return TranslateSimpleSetWithPath(valueExpression, parameter, propertyName, context, pathPrefix);
    }

    /// <summary>
    /// Handles conditional expressions (ternary operator) in update expressions.
    /// </summary>
    /// <param name="conditional">The conditional expression.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="propertyName">The property being updated.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>An operation representing the update, or Skip if the property should be skipped via NoUpdate().</returns>
    /// <remarks>
    /// <para>
    /// This method handles patterns like:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>Property = flag ? value : null</c> - SET NULL when flag is false (consistent null handling)</description></item>
    /// <item><description><c>Property = flag ? value : x.Property.NoUpdate()</c> - Skip property when flag is false</description></item>
    /// <item><description><c>Property = flag ? valueA : valueB</c> - Use appropriate value based on flag</description></item>
    /// </list>
    /// <para>
    /// The condition must not reference the entity parameter - it must be evaluable at translation time.
    /// Null values in either branch will generate SET NULL operations. Use NoUpdate() to skip updates.
    /// </para>
    /// </remarks>
    private Operation HandleConditionalUpdate(
        ConditionalExpression conditional,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        return HandleConditionalUpdateWithPath(conditional, parameter, propertyName, context, Array.Empty<string>());
    }

    /// <summary>
    /// Handles conditional expressions (ternary operator) in update expressions with path support.
    /// </summary>
    private Operation HandleConditionalUpdateWithPath(
        ConditionalExpression conditional,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context,
        string[] pathPrefix)
    {
        // The condition must not reference the entity parameter - it must be evaluable at translation time
        if (ReferencesEntityParameter(conditional.Test, parameter))
        {
            throw new UnsupportedExpressionException(
                "Conditional test cannot reference entity properties. " +
                "Use captured variables or constants for the condition. " +
                "Example: 'Property = flag ? value : null' is valid, " +
                "but 'Property = x.SomeProperty ? valueA : valueB' is not.",
                conditional);
        }

        // Evaluate the test condition at translation time
        bool testResult;
        try
        {
            var testValue = EvaluateExpression(conditional.Test);
            testResult = testValue is bool b ? b : Convert.ToBoolean(testValue);
        }
        catch (Exception ex)
        {
            throw new ExpressionTranslationException(
                $"Failed to evaluate conditional test expression for property '{propertyName}': {ex.Message}",
                conditional);
        }

        // Process the appropriate branch based on the condition result
        // Note: null values in either branch will generate SET NULL operations (consistent null handling)
        var branchToProcess = testResult ? conditional.IfTrue : conditional.IfFalse;
        return ClassifyOperationWithPath(branchToProcess, parameter, propertyName, context, pathPrefix);
    }

    /// <summary>
    /// Translates a nested MemberInitExpression to multiple SET operations.
    /// </summary>
    /// <param name="nestedInit">The nested MemberInitExpression.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="propertyName">The property name being updated.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="pathPrefix">The path prefix for nested properties.</param>
    /// <returns>An operation containing all nested SET expressions combined.</returns>
    /// <remarks>
    /// <para>
    /// This method handles nested object initializers like:
    /// </para>
    /// <code>
    /// ShippingAddress = new AddressUpdateModel { City = "Portland", State = "OR" }
    /// </code>
    /// <para>
    /// Which generates: SET #address.#city = :v0, #address.#state = :v1
    /// </para>
    /// <para>
    /// Multi-level nesting is also supported:
    /// </para>
    /// <code>
    /// ShippingAddress = new AddressUpdateModel { Country = new CountryUpdateModel { Code = "US" } }
    /// </code>
    /// <para>
    /// Which generates: SET #address.#country.#code = :v0
    /// </para>
    /// </remarks>
    private Operation TranslateNestedMemberInit(
        MemberInitExpression nestedInit,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context,
        string[] pathPrefix)
    {
        // Build the new path prefix including this property
        var currentPath = pathPrefix.Append(propertyName).ToArray();
        
        // Collect all SET operations from nested bindings
        var setOperations = new List<string>();
        
        foreach (var binding in nestedInit.Bindings)
        {
            if (binding is not MemberAssignment assignment)
            {
                throw new UnsupportedExpressionException(
                    $"Only property assignments are supported in nested update expressions. Found: {binding.BindingType}",
                    nestedInit);
            }
            
            var nestedPropertyName = assignment.Member.Name;
            var nestedValueExpression = assignment.Expression;
            
            // Recursively classify the operation with the updated path
            var operation = ClassifyOperationWithPath(nestedValueExpression, parameter, nestedPropertyName, context, currentPath);
            
            // Only SET operations are supported for nested updates
            // ADD, REMOVE, DELETE operations on nested properties would require different handling
            if (operation.Type == OperationType.Set)
            {
                setOperations.Add(operation.Expression);
            }
            else if (operation.Type == OperationType.Skip)
            {
                // Skip this property
                continue;
            }
            else
            {
                throw new UnsupportedExpressionException(
                    $"Only SET operations are supported for nested property updates. " +
                    $"Property '{nestedPropertyName}' in path '{string.Join(".", currentPath)}' uses operation type '{operation.Type}'.",
                    nestedInit);
            }
        }
        
        // If no operations were generated (all skipped), return Skip
        if (setOperations.Count == 0)
        {
            return new Operation
            {
                Type = OperationType.Skip,
                Expression = string.Empty
            };
        }
        
        // Return a combined SET operation
        return new Operation
        {
            Type = OperationType.Set,
            Expression = string.Join(", ", setOperations)
        };
    }

    /// <summary>
    /// Checks if a method call expression is a call to the NoUpdate() extension method.
    /// </summary>
    /// <param name="methodCall">The method call expression to check.</param>
    /// <returns>True if the method call is NoUpdate(), false otherwise.</returns>
    /// <remarks>
    /// <para>
    /// The NoUpdate() method is an extension method on UpdateExpressionProperty&lt;T&gt; that signals
    /// the property should not be updated. When detected, the translator returns a Skip operation,
    /// leaving the existing value unchanged in DynamoDB.
    /// </para>
    /// </remarks>
    private bool IsNoUpdateMethodCall(MethodCallExpression methodCall)
    {
        return methodCall.Method.Name == "NoUpdate" &&
               methodCall.Method.DeclaringType == typeof(UpdateExpressionPropertyExtensions);
    }

    /// <summary>
    /// Checks if an expression references the entity parameter.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <param name="entityParameter">The entity parameter to look for.</param>
    /// <returns>True if the expression references the entity parameter, false otherwise.</returns>
    private bool ReferencesEntityParameter(Expression expression, ParameterExpression entityParameter)
    {
        var visitor = new EntityParameterReferenceVisitor(entityParameter);
        visitor.Visit(expression);
        return visitor.ContainsReference;
    }

    private class EntityParameterReferenceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _entityParameter;
        public bool ContainsReference { get; private set; }

        public EntityParameterReferenceVisitor(ParameterExpression entityParameter)
        {
            _entityParameter = entityParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == _entityParameter)
            {
                ContainsReference = true;
            }
            return base.VisitParameter(node);
        }
    }

    /// <summary>
    /// Determines whether a property is a source property or extracted property of a non-key computed field.
    /// </summary>
    /// <remarks>
    /// A property is considered a computed source property if any of these conditions are true:
    /// <list type="bullet">
    /// <item><description>Its <see cref="PropertyMetadata.ComputedFieldTarget"/> is set (it is a direct source of a computed field)</description></item>
    /// <item><description>Its <see cref="PropertyMetadata.ExtractedField"/> is set and the extracted source property is a non-key computed field</description></item>
    /// </list>
    /// <para>
    /// This check is used to intercept source property assignments in the binding loop so that their
    /// values are stored for later recomputation of the computed field, rather than generating
    /// individual SET operations. It also enables FDDB071 validation: source properties of computed
    /// fields must be assigned constant or local values and cannot reference the entity lambda parameter.
    /// </para>
    /// </remarks>
    private static bool IsComputedSourceProperty(string propertyName, ExpressionContext context)
    {
        if (context.EntityMetadata == null)
            return false;

        var propertyMetadata = context.EntityMetadata.Properties
            .FirstOrDefault(p => p.PropertyName == propertyName);

        if (propertyMetadata == null)
            return false;

        // Check if this property is a direct source of a computed field
        if (propertyMetadata.ComputedFieldTarget != null)
            return true;

        // Check if this property is an extracted property targeting a non-key computed field
        if (propertyMetadata.ExtractedField != null)
        {
            var sourceProperty = context.EntityMetadata.Properties
                .FirstOrDefault(p => p.PropertyName == propertyMetadata.ExtractedField.SourceProperty);

            // If the source is a non-key computed field, this extracted property is a computed source
            if (sourceProperty?.ComputedField != null && !sourceProperty.IsPartitionKey && !sourceProperty.IsSortKey)
                return true;
        }

        return false;
    }

    private Operation TranslateSimpleSet(
        Expression valueExpression,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        return TranslateSimpleSetWithPath(valueExpression, parameter, propertyName, context, Array.Empty<string>());
    }

    private Operation TranslateSimpleSetWithPath(
        Expression valueExpression,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context,
        string[] pathPrefix)
    {
        // Validate property is not a key (only for top-level properties)
        if (pathPrefix.Length == 0)
        {
            ValidateNotKeyProperty(propertyName, context, valueExpression);
        }

        // Get property metadata (only available for top-level properties)
        PropertyMetadata? propertyMetadata = null;
        if (pathPrefix.Length == 0)
        {
            propertyMetadata = GetPropertyMetadata(propertyName, context);
        }
        
        // Get attribute name with path support
        var attributeName = GetAttributeNameWithPath(propertyName, context, pathPrefix, valueExpression);
        
        // Evaluate the value expression
        var value = EvaluateExpression(valueExpression);
        
        // Apply format if specified
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormat(value, propertyMetadata.Format, propertyName);
        }
        
        // Capture the value (encryption will be marked in CaptureValue if needed)
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

        // Check if left side is an IfNotExists method call - common pattern for counters with non-zero defaults
        // e.g., x.Count.IfNotExists(100) + 1 => SET #count = if_not_exists(#count, :default) + :increment
        if (IsIfNotExistsMethodCall(binary.Left, parameter))
        {
            return TranslateIfNotExistsWithArithmetic(binary, parameter, propertyName, context);
        }

        // Check if left side is UpdateExpressionProperty access
        if (!IsUpdateExpressionPropertyAccess(binary.Left, parameter))
        {
            throw new UnsupportedExpressionException(
                $"Left side of arithmetic operation must be an UpdateExpressionProperty access (e.g., x.PropertyName) " +
                $"or an IfNotExists call (e.g., x.PropertyName.IfNotExists(0)). " +
                $"Found: {binary.Left.NodeType}. " +
                $"Examples: x.Count + 5, x.Count.IfNotExists(0) + 1",
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

    /// <summary>
    /// Checks if an expression is an IfNotExists method call on an UpdateExpressionProperty.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <returns>True if the expression is x.Property.IfNotExists(defaultValue); otherwise, false.</returns>
    private bool IsIfNotExistsMethodCall(Expression expression, ParameterExpression parameter)
    {
        // Unwrap Convert expressions
        var unwrapped = expression;
        while (unwrapped is UnaryExpression unary && 
               (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            unwrapped = unary.Operand;
        }

        if (unwrapped is not MethodCallExpression methodCall)
            return false;

        if (methodCall.Method.Name != "IfNotExists")
            return false;

        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        if (methodCall.Arguments.Count < 1)
            return false;

        // Check that the 'this' argument is a property access on the parameter
        return IsUpdateExpressionPropertyAccess(methodCall.Arguments[0], parameter);
    }

    /// <summary>
    /// Translates an IfNotExists call combined with arithmetic to DynamoDB syntax.
    /// </summary>
    /// <param name="binary">The binary expression (e.g., x.Count.IfNotExists(0) + 1).</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="propertyName">The property name being updated.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>An operation representing SET #attr = if_not_exists(#attr, :default) +/- :value.</returns>
    /// <remarks>
    /// <para>
    /// This method handles the common counter pattern where you want to initialize a counter
    /// to a non-zero default value if it doesn't exist, then perform arithmetic on it.
    /// </para>
    /// <para>
    /// Example: <c>x.Count.IfNotExists(100) + 1</c> generates:
    /// <c>SET #count = if_not_exists(#count, :p0) + :p1</c>
    /// where :p0 = 100 (default) and :p1 = 1 (increment)
    /// </para>
    /// <para>
    /// For simple zero-default counters, consider using <c>x.Count.Add(1)</c> instead,
    /// which generates a DynamoDB ADD operation that automatically initializes to 0.
    /// </para>
    /// </remarks>
    private Operation TranslateIfNotExistsWithArithmetic(
        BinaryExpression binary,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        // Unwrap Convert expressions from the left side
        var leftUnwrapped = binary.Left;
        while (leftUnwrapped is UnaryExpression unary && 
               (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            leftUnwrapped = unary.Operand;
        }

        var methodCall = (MethodCallExpression)leftUnwrapped;

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
        
        // Get the default value from IfNotExists (Arguments[1] is the default value)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"IfNotExists() method requires a default value argument. " +
                $"Example: x.Count.IfNotExists(0) + 1",
                "IfNotExists",
                methodCall);
        }
        
        var defaultValueArg = methodCall.Arguments[1];
        var defaultValue = EvaluateExpression(defaultValueArg);
        
        // Validate the default value is numeric
        if (defaultValue != null && !IsNumericType(defaultValue.GetType()))
        {
            throw new UnsupportedExpressionException(
                $"Default value in IfNotExists() must be numeric when used with arithmetic. " +
                $"Found type: {defaultValue.GetType().Name}.",
                binary);
        }
        
        // Apply format if specified
        if (propertyMetadata?.Format != null && defaultValue != null)
        {
            defaultValue = ApplyFormat(defaultValue, propertyMetadata.Format, propertyName);
        }
        
        // Capture the default value
        var defaultPlaceholder = CaptureValue(defaultValue, context, propertyMetadata);
        
        // Evaluate the right side value (the arithmetic operand)
        var incrementValue = EvaluateExpression(binary.Right);
        
        // Validate the increment value is numeric
        if (incrementValue != null && !IsNumericType(incrementValue.GetType()))
        {
            throw new UnsupportedExpressionException(
                $"Right side of arithmetic operation must evaluate to a numeric value. " +
                $"Found type: {incrementValue.GetType().Name}.",
                binary);
        }
        
        // Apply format if specified
        if (propertyMetadata?.Format != null && incrementValue != null)
        {
            incrementValue = ApplyFormat(incrementValue, propertyMetadata.Format, propertyName);
        }
        
        // Capture the increment value
        var incrementPlaceholder = CaptureValue(incrementValue, context, propertyMetadata);
        
        // Build SET expression: #attr = if_not_exists(#attr, :default) +/- :increment
        var op = binary.NodeType == ExpressionType.Add ? "+" : "-";
        var expression = $"{attributeName} = if_not_exists({attributeName}, {defaultPlaceholder}) {op} {incrementPlaceholder}";
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = expression
        };
    }

    private Operation TranslateBinaryOperationWithPath(
        BinaryExpression binary,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context,
        string[] pathPrefix)
    {
        // For nested properties, arithmetic operations are not supported
        // (would require tracking the nested property reference in the expression)
        if (pathPrefix.Length > 0)
        {
            throw new UnsupportedExpressionException(
                $"Arithmetic operations are not supported for nested properties. " +
                $"Property path: '{string.Join(".", pathPrefix)}.{propertyName}'. " +
                $"Use simple value assignment instead.",
                binary);
        }
        
        // Delegate to the non-path version for top-level properties
        return TranslateBinaryOperation(binary, parameter, propertyName, context);
    }

    private Operation TranslateMethodCall(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context)
    {
        return TranslateMethodCallWithPath(methodCall, parameter, propertyName, context, Array.Empty<string>());
    }

    private Operation TranslateMethodCallWithPath(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        string propertyName,
        ExpressionContext context,
        string[] pathPrefix)
    {
        var methodName = methodCall.Method.Name;
        
        // Check if this is a list operation extension method from UpdateExpressionPropertyExtensions
        // These methods are called on the UpdateExpressionProperty<List<T>>, so we need to extract the path from Arguments[0]
        if (IsListOperationExtensionMethodOnList(methodCall))
        {
            return TranslateListOperationExtensionMethod(methodCall, parameter, context, pathPrefix, propertyName);
        }
        
        // Check if the method call references the entity parameter
        // If it doesn't, it's a local method call that can be evaluated at translation time
        // Examples: TransactionStatus.Active.ToString(), myVar.Trim().ToUpper(), guid.ToString()
        if (!ReferencesEntityParameter(methodCall, parameter))
        {
            // Evaluate the method call and treat it as a simple value assignment
            return TranslateSimpleSetWithPath(methodCall, parameter, propertyName, context, pathPrefix);
        }
        
        // For nested properties, only certain methods are supported
        if (pathPrefix.Length > 0)
        {
            throw new UnsupportedExpressionException(
                $"Method '{methodName}' is not supported for nested properties. " +
                $"Property path: '{string.Join(".", pathPrefix)}.{propertyName}'. " +
                $"Use simple value assignment for nested properties.",
                methodName,
                methodCall);
        }
        
        return methodName switch
        {
            "Add" => TranslateAddOperation(methodCall, parameter, propertyName, context),
            "Remove" => TranslateRemoveOperation(methodCall, parameter, propertyName, context),
            "Delete" => TranslateDeleteOperation(methodCall, parameter, propertyName, context),
            "IfNotExists" => TranslateIfNotExistsFunction(methodCall, parameter, propertyName, context),
            "ListAppend" => TranslateListAppendFunction(methodCall, parameter, propertyName, context),
            "ListPrepend" => TranslateListPrependFunction(methodCall, parameter, propertyName, context),
            "Append" => TranslateAppendFunction(methodCall, parameter, propertyName, context),
            "Prepend" => TranslatePrependFunction(methodCall, parameter, propertyName, context),
            "AppendRange" => TranslateAppendRangeFunction(methodCall, parameter, propertyName, context),
            "PrependRange" => TranslatePrependRangeFunction(methodCall, parameter, propertyName, context),
            "SetDynamicField" => TranslateSetDynamicFieldOperation(methodCall, parameter, context),
            "RemoveDynamicField" => TranslateRemoveDynamicFieldOperation(methodCall, parameter, context),
            _ => throw new UnsupportedExpressionException(
                $"Method '{methodName}' is not supported in update expressions. " +
                $"Supported methods: Add, Remove, Delete, IfNotExists, ListAppend, ListPrepend, Append, Prepend, AppendRange, PrependRange, SetDynamicField, RemoveDynamicField.",
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
        
        // Apply format if specified (for set element types)
        // Note: Format strings are typically not used for set elements, but we support it for consistency
        if (propertyMetadata?.Format != null && value != null)
        {
            value = ApplyFormatToSetElements(value, propertyMetadata.Format, propertyName);
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
        
        // Capture the value (encryption will be marked in CaptureValue if needed)
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

    /// <summary>
    /// Translates an Append method call on UpdateExpressionProperty&lt;List&lt;T&gt;&gt; to DynamoDB list_append.
    /// </summary>
    private Operation TranslateAppendFunction(
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
        
        // Get the item to append
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the item to append)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"Append() method requires an item to append. " +
                $"Example: x.Tags.Append(\"new-tag\").",
                "Append",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // Wrap single item in a list for list_append
        value = WrapInList(value);
        
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

    /// <summary>
    /// Translates a Prepend method call on UpdateExpressionProperty&lt;List&lt;T&gt;&gt; to DynamoDB list_append.
    /// </summary>
    private Operation TranslatePrependFunction(
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
        
        // Get the item to prepend
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the item to prepend)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"Prepend() method requires an item to prepend. " +
                $"Example: x.Tags.Prepend(\"priority-tag\").",
                "Prepend",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // Wrap single item in a list for list_append
        value = WrapInList(value);
        
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

    /// <summary>
    /// Translates an AppendRange method call on UpdateExpressionProperty&lt;List&lt;T&gt;&gt; to DynamoDB list_append.
    /// </summary>
    private Operation TranslateAppendRangeFunction(
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
        
        // Get the items to append
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the items to append)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"AppendRange() method requires items to append. " +
                $"Example: x.Tags.AppendRange(new[] {{ \"tag1\", \"tag2\" }}).",
                "AppendRange",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // Convert to list if needed
        value = ConvertToList(value);
        
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

    /// <summary>
    /// Translates a PrependRange method call on UpdateExpressionProperty&lt;List&lt;T&gt;&gt; to DynamoDB list_append.
    /// </summary>
    private Operation TranslatePrependRangeFunction(
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
        
        // Get the items to prepend
        // For extension methods, Arguments[0] is the 'this' parameter (the property itself)
        // and Arguments[1] is the actual first argument (the items to prepend)
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"PrependRange() method requires items to prepend. " +
                $"Example: x.Tags.PrependRange(new[] {{ \"tag1\", \"tag2\" }}).",
                "PrependRange",
                methodCall);
        }
        
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // Convert to list if needed
        value = ConvertToList(value);
        
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

    /// <summary>
    /// Checks if a method call is a list operation extension method.
    /// </summary>
    /// <param name="methodCall">The method call expression to check.</param>
    /// <returns>True if the method is from UpdateExpressionPropertyExtensions or ListOperationExtensions.</returns>
    /// <remarks>
    /// <para>
    /// The first operation in a chain uses UpdateExpressionPropertyExtensions (on UpdateExpressionProperty&lt;List&lt;T&gt;&gt;).
    /// Subsequent chained operations use ListOperationExtensions (on List&lt;T&gt;, the return type).
    /// </para>
    /// </remarks>
    private static bool IsListOperationExtensionMethodOnList(MethodCallExpression methodCall)
    {
        var methodName = methodCall.Method.Name;
        if (methodName is not ("Append" or "Prepend" or "AppendRange" or "PrependRange" or "SetAt" or "RemoveAt"))
            return false;
        
        // Check if the method is from UpdateExpressionPropertyExtensions (first operation in chain)
        // or ListOperationExtensions (subsequent chained operations on List<T>)
        var declaringType = methodCall.Method.DeclaringType;
        return declaringType?.Name == nameof(UpdateExpressionPropertyExtensions) ||
               declaringType?.Name == nameof(ListOperationExtensions);
    }

    /// <summary>
    /// Checks if a method name is a list operation extension method.
    /// </summary>
    /// <param name="methodName">The method name to check.</param>
    /// <returns>True if the method is a list operation extension method.</returns>
    private static bool IsListOperationExtensionMethod(string methodName)
    {
        return methodName is "Append" or "Prepend" or "AppendRange" or "PrependRange" or "SetAt" or "RemoveAt";
    }

    /// <summary>
    /// Validates that a list operation is not chained with an incompatible operation.
    /// DynamoDB does not allow multiple operations on overlapping document paths in a single update expression.
    /// </summary>
    /// <param name="listExpression">The expression representing the list (Arguments[0] of the method call).</param>
    /// <param name="currentOperation">The name of the current operation being translated.</param>
    /// <param name="sourceExpression">The source expression for error reporting.</param>
    /// <exception cref="UnsupportedExpressionException">Thrown when overlapping operations are detected.</exception>
    /// <remarks>
    /// <para><strong>Allowed Chaining:</strong></para>
    /// <list type="bullet">
    /// <item><description>Multiple SetAt calls with different indices: x.Tags.SetAt(0, "a").SetAt(1, "b")</description></item>
    /// </list>
    /// <para><strong>Disallowed Chaining:</strong></para>
    /// <list type="bullet">
    /// <item><description>SetAt + Append/Prepend: overlapping paths (index access + whole list)</description></item>
    /// <item><description>SetAt + RemoveAt: overlapping paths (SET + REMOVE on same attribute)</description></item>
    /// <item><description>Append/Prepend + RemoveAt: overlapping paths</description></item>
    /// <item><description>Any combination that mixes index operations with whole-list operations</description></item>
    /// </list>
    /// </remarks>
    private static void ValidateNoOverlappingListOperations(
        Expression listExpression,
        string currentOperation,
        Expression sourceExpression)
    {
        // Check if the list expression is another list operation method call
        if (listExpression is not MethodCallExpression chainedCall)
            return;

        // Check if it's a list operation extension method (from either class)
        var declaringType = chainedCall.Method.DeclaringType;
        if (declaringType?.Name != nameof(ListOperationExtensions) &&
            declaringType?.Name != nameof(UpdateExpressionPropertyExtensions))
            return;

        var chainedMethodName = chainedCall.Method.Name;
        
        // SetAt can only be chained with other SetAt calls (handled separately in CollectChainedSetAtOperations)
        // All other combinations are disallowed
        
        // Determine the type of the current operation
        var isCurrentIndexOperation = currentOperation is "SetAt" or "RemoveAt";
        var isCurrentWholeListOperation = currentOperation is "Append" or "Prepend" or "AppendRange" or "PrependRange";
        
        // Determine the type of the chained operation
        var isChainedIndexOperation = chainedMethodName is "SetAt" or "RemoveAt";
        var isChainedWholeListOperation = chainedMethodName is "Append" or "Prepend" or "AppendRange" or "PrependRange";
        
        // SetAt chained with SetAt is allowed (handled by CollectChainedSetAtOperations)
        if (currentOperation == "SetAt" && chainedMethodName == "SetAt")
            return;
        
        // All other combinations are disallowed due to DynamoDB's overlapping document path restriction
        string errorMessage;
        
        if (isCurrentIndexOperation && isChainedWholeListOperation)
        {
            // e.g., x.Tags.Append("a").SetAt(0, "b") or x.Tags.Append("a").RemoveAt(0)
            errorMessage = $"Cannot chain {currentOperation}() with {chainedMethodName}() on the same list. " +
                           "DynamoDB does not allow multiple operations on overlapping document paths. " +
                           $"The {chainedMethodName}() operation modifies the entire list while {currentOperation}() targets a specific index. " +
                           "Use separate update operations instead.";
        }
        else if (isCurrentWholeListOperation && isChainedIndexOperation)
        {
            // e.g., x.Tags.SetAt(0, "a").Append("b") or x.Tags.RemoveAt(0).Append("b")
            errorMessage = $"Cannot chain {currentOperation}() with {chainedMethodName}() on the same list. " +
                           "DynamoDB does not allow multiple operations on overlapping document paths. " +
                           $"The {currentOperation}() operation modifies the entire list while {chainedMethodName}() targets a specific index. " +
                           "Use separate update operations instead.";
        }
        else if (isCurrentWholeListOperation && isChainedWholeListOperation)
        {
            // e.g., x.Tags.Append("a").Prepend("b")
            errorMessage = $"Cannot chain {currentOperation}() with {chainedMethodName}() on the same list. " +
                           "DynamoDB does not allow multiple operations on overlapping document paths. " +
                           "Both operations modify the entire list. " +
                           "Use separate update operations instead.";
        }
        else if (currentOperation == "RemoveAt" && chainedMethodName == "SetAt")
        {
            // e.g., x.Tags.SetAt(0, "a").RemoveAt(1)
            errorMessage = "Cannot chain RemoveAt() with SetAt() on the same list. " +
                           "DynamoDB does not allow SET and REMOVE operations on overlapping document paths. " +
                           "Use separate update operations instead.";
        }
        else if (currentOperation == "SetAt" && chainedMethodName == "RemoveAt")
        {
            // e.g., x.Tags.RemoveAt(0).SetAt(1, "a")
            errorMessage = "Cannot chain SetAt() with RemoveAt() on the same list. " +
                           "DynamoDB does not allow SET and REMOVE operations on overlapping document paths. " +
                           "Use separate update operations instead.";
        }
        else
        {
            // Generic fallback for any other combination
            errorMessage = $"Cannot chain {currentOperation}() with {chainedMethodName}() on the same list. " +
                           "DynamoDB does not allow multiple operations on overlapping document paths. " +
                           "Use separate update operations instead.";
        }
        
        throw new UnsupportedExpressionException(errorMessage, sourceExpression);
    }

    /// <summary>
    /// Translates a list operation extension method call (Append, Prepend, AppendRange, PrependRange).
    /// These methods are called on the list property itself, so we need to extract the path from Arguments[0].
    /// </summary>
    /// <param name="methodCall">The method call expression.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="pathPrefix">The path prefix for nested properties.</param>
    /// <param name="propertyName">The property name (may be overridden by extracting from the method call).</param>
    /// <returns>An operation representing the list operation.</returns>
    /// <remarks>
    /// <para>
    /// List operation extension methods are called on the list property itself:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>x.Tags.Append("item")</c> - top-level list</description></item>
    /// <item><description><c>x.Metadata.Keywords.Append("sale")</c> - nested list</description></item>
    /// </list>
    /// <para>
    /// For extension methods, Arguments[0] is the 'this' parameter (the list property)
    /// and Arguments[1] is the item(s) to append/prepend.
    /// </para>
    /// </remarks>
    private Operation TranslateListOperationExtensionMethod(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        ExpressionContext context,
        string[] pathPrefix,
        string propertyName)
    {
        var methodName = methodCall.Method.Name;
        
        // Handle SetAt and RemoveAt separately as they have different argument patterns
        if (methodName == "SetAt")
        {
            return TranslateSetAtOperation(methodCall, parameter, context, pathPrefix);
        }
        
        if (methodName == "RemoveAt")
        {
            return TranslateRemoveAtOperation(methodCall, parameter, context, pathPrefix);
        }
        
        // For extension methods, Arguments[0] is the 'this' parameter (the list property)
        // and Arguments[1] is the item(s) to append/prepend
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                $"{methodName}() method requires an item to {methodName.ToLowerInvariant()}. " +
                $"Example: x.Tags.{methodName}(\"item\").",
                methodName,
                methodCall);
        }
        
        // Extract the list property path from Arguments[0]
        var listExpression = methodCall.Arguments[0];
        
        // Validate no overlapping list operations (e.g., x.Tags.SetAt(0, "a").Append("b") is not allowed)
        ValidateNoOverlappingListOperations(listExpression, methodName, methodCall);
        
        var (listPath, listPropertyName) = ExtractListPropertyPath(listExpression, parameter, context);
        
        // Combine the path prefix with the extracted path
        var fullPath = pathPrefix.Concat(listPath).ToArray();
        
        // Get the attribute name with the full path
        var attributeName = GetAttributeNameWithPath(listPropertyName, context, fullPath, methodCall);
        
        // Get the value argument
        var valueArg = methodCall.Arguments[1];
        var value = EvaluateExpression(valueArg);
        
        // For single item methods (Append, Prepend), wrap the value in a list
        if (methodName is "Append" or "Prepend")
        {
            value = WrapInList(value);
        }
        else
        {
            // For range methods (AppendRange, PrependRange), convert to list if needed
            value = ConvertToList(value);
        }
        
        // Capture the value
        var valuePlaceholder = CaptureValue(value, context, null);
        
        // Build SET expression with list_append function
        // For Append/AppendRange: list_append(#attr, :val) - adds to end
        // For Prepend/PrependRange: list_append(:val, #attr) - adds to beginning
        string expression;
        if (methodName is "Append" or "AppendRange")
        {
            expression = $"{attributeName} = list_append({attributeName}, {valuePlaceholder})";
        }
        else // Prepend or PrependRange
        {
            expression = $"{attributeName} = list_append({valuePlaceholder}, {attributeName})";
        }
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = expression
        };
    }

    /// <summary>
    /// Translates a SetAt method call to a DynamoDB SET expression with list index.
    /// Supports chained SetAt calls: x.Tags.SetAt(0, "a").SetAt(1, "b")
    /// </summary>
    /// <param name="methodCall">The SetAt method call expression.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="pathPrefix">The path prefix for nested properties.</param>
    /// <returns>An operation representing the SET expression(s).</returns>
    /// <remarks>
    /// <para>
    /// SetAt translates to: SET #attr[index] = :val
    /// </para>
    /// <para>
    /// For extension methods:
    /// - Arguments[0] is the 'this' parameter (the list property or another SetAt call)
    /// - Arguments[1] is the index
    /// - Arguments[2] is the value to set
    /// </para>
    /// <para>
    /// Chained SetAt calls are supported:
    /// x.Tags.SetAt(0, "a").SetAt(1, "b") generates: SET #tags[0] = :v0, #tags[1] = :v1
    /// </para>
    /// <para>
    /// Duplicate indices in a chain will throw UnsupportedExpressionException.
    /// </para>
    /// </remarks>
    private Operation TranslateSetAtOperation(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        ExpressionContext context,
        string[] pathPrefix)
    {
        // SetAt has 3 arguments: list (this), index, value
        if (methodCall.Arguments.Count < 3)
        {
            throw new UnsupportedExpressionException(
                "SetAt() method requires an index and a value. " +
                "Example: x.Tags.SetAt(0, \"updated\").",
                "SetAt",
                methodCall);
        }
        
        // Collect all SetAt operations from the chain
        var setAtOperations = CollectChainedSetAtOperations(methodCall, parameter, context);
        
        // Validate no duplicate indices
        var indices = setAtOperations.Select(op => op.Index).ToList();
        var duplicateIndices = indices.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIndices.Any())
        {
            throw new UnsupportedExpressionException(
                $"Chained SetAt operations cannot have duplicate indices. " +
                $"Duplicate index found: {duplicateIndices[0]}. " +
                "Each SetAt in a chain must target a different index.",
                methodCall);
        }
        
        // Get the list property path from the base of the chain
        var baseListExpression = setAtOperations[0].BaseListExpression;
        var (listPath, listPropertyName) = ExtractListPropertyPath(baseListExpression, parameter, context);
        
        // Combine the path prefix with the extracted path
        var fullPath = pathPrefix.Concat(listPath).ToArray();
        
        // Get the attribute name with the full path
        var attributeName = GetAttributeNameWithPath(listPropertyName, context, fullPath, methodCall);
        
        // Build SET expressions for all operations in the chain
        var expressions = new List<string>();
        foreach (var op in setAtOperations)
        {
            // Capture the value
            var valuePlaceholder = CaptureValue(op.Value, context, null);
            
            // Build SET expression: #attr[index] = :val
            expressions.Add($"{attributeName}[{op.Index}] = {valuePlaceholder}");
        }
        
        // Combine all expressions with comma separator
        var combinedExpression = string.Join(", ", expressions);
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = combinedExpression
        };
    }

    /// <summary>
    /// Represents a single SetAt operation in a chain.
    /// </summary>
    private class SetAtOperationInfo
    {
        public int Index { get; set; }
        public object? Value { get; set; }
        public Expression BaseListExpression { get; set; } = null!;
    }

    /// <summary>
    /// Collects all SetAt operations from a chained expression.
    /// Walks the chain from outermost to innermost, collecting index/value pairs.
    /// </summary>
    /// <param name="methodCall">The outermost SetAt method call.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>A list of SetAt operations with the base list expression.</returns>
    private List<SetAtOperationInfo> CollectChainedSetAtOperations(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        ExpressionContext context)
    {
        var operations = new List<SetAtOperationInfo>();
        var current = methodCall;
        Expression? baseListExpression = null;
        
        while (current != null)
        {
            // Validate SetAt has correct number of arguments
            if (current.Arguments.Count < 3)
            {
                throw new UnsupportedExpressionException(
                    "SetAt() method requires an index and a value. " +
                    "Example: x.Tags.SetAt(0, \"updated\").",
                    "SetAt",
                    current);
            }
            
            // Get the index argument and evaluate it
            var indexArg = current.Arguments[1];
            var index = EvaluateIndexExpression(indexArg, parameter, current);
            
            // Validate index is non-negative
            ValidateListIndex(index, indexArg);
            
            // Get the value argument
            var valueArg = current.Arguments[2];
            var value = EvaluateExpression(valueArg);
            
            // Add this operation to the list
            operations.Add(new SetAtOperationInfo
            {
                Index = index,
                Value = value
            });
            
            // Check if Arguments[0] is another SetAt call (chained)
            var listExpression = current.Arguments[0];
            if (listExpression is MethodCallExpression chainedCall && 
                chainedCall.Method.Name == "SetAt" &&
                (chainedCall.Method.DeclaringType?.Name == nameof(ListOperationExtensions) ||
                 chainedCall.Method.DeclaringType?.Name == nameof(UpdateExpressionPropertyExtensions)))
            {
                // Continue walking the chain
                current = chainedCall;
            }
            else
            {
                // Validate no overlapping list operations before accepting as base expression
                // This catches cases like x.Tags.Append("a").SetAt(0, "b")
                ValidateNoOverlappingListOperations(listExpression, "SetAt", current);
                
                // This is the base list expression (e.g., x.Tags)
                baseListExpression = listExpression;
                current = null;
            }
        }
        
        // Set the base list expression on all operations
        foreach (var op in operations)
        {
            op.BaseListExpression = baseListExpression!;
        }
        
        // Reverse the list so operations are in the order they appear in the chain
        // (innermost first, which is the natural order for DynamoDB)
        operations.Reverse();
        
        return operations;
    }

    /// <summary>
    /// Translates a RemoveAt method call to a DynamoDB REMOVE expression with list index.
    /// </summary>
    /// <param name="methodCall">The RemoveAt method call expression.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="pathPrefix">The path prefix for nested properties.</param>
    /// <returns>An operation representing the REMOVE expression.</returns>
    /// <remarks>
    /// <para>
    /// RemoveAt translates to: REMOVE #attr[index]
    /// </para>
    /// <para>
    /// For extension methods:
    /// - Arguments[0] is the 'this' parameter (the list property)
    /// - Arguments[1] is the index
    /// </para>
    /// </remarks>
    private Operation TranslateRemoveAtOperation(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        ExpressionContext context,
        string[] pathPrefix)
    {
        // RemoveAt has 2 arguments: list (this), index
        if (methodCall.Arguments.Count < 2)
        {
            throw new UnsupportedExpressionException(
                "RemoveAt() method requires an index. " +
                "Example: x.Tags.RemoveAt(2).",
                "RemoveAt",
                methodCall);
        }
        
        // Extract the list property path from Arguments[0]
        var listExpression = methodCall.Arguments[0];
        
        // Validate no overlapping list operations (e.g., x.Tags.SetAt(0, "a").RemoveAt(1) is not allowed)
        ValidateNoOverlappingListOperations(listExpression, "RemoveAt", methodCall);
        
        var (listPath, listPropertyName) = ExtractListPropertyPath(listExpression, parameter, context);
        
        // Combine the path prefix with the extracted path
        var fullPath = pathPrefix.Concat(listPath).ToArray();
        
        // Get the attribute name with the full path
        var attributeName = GetAttributeNameWithPath(listPropertyName, context, fullPath, methodCall);
        
        // Get the index argument and evaluate it
        var indexArg = methodCall.Arguments[1];
        var index = EvaluateIndexExpression(indexArg, parameter, methodCall);
        
        // Validate index is non-negative
        ValidateListIndex(index, indexArg);
        
        // Build REMOVE expression: #attr[index]
        var expression = $"{attributeName}[{index}]";
        
        return new Operation
        {
            Type = OperationType.Remove,
            Expression = expression
        };
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
        if (ReferencesEntityParameter(indexExpr, entityParameter))
        {
            throw new UnsupportedExpressionException(
                "List index cannot reference the entity parameter. " +
                "Use a local variable, property, or method call that doesn't depend on the entity. " +
                "Example: int idx = GetIndex(); .Set(x => x.Tags.SetAt(idx, \"value\"))",
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
    /// Extracts the property path from a list expression.
    /// </summary>
    /// <param name="expression">The expression representing the list property.</param>
    /// <param name="parameter">The update expressions parameter.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>A tuple containing the path segments and the final property name.</returns>
    private (string[] Path, string PropertyName) ExtractListPropertyPath(
        Expression expression,
        ParameterExpression parameter,
        ExpressionContext context)
    {
        var pathSegments = new List<string>();
        var current = expression;
        
        // Walk up the expression tree to collect all member accesses
        while (current is MemberExpression memberExpr)
        {
            pathSegments.Insert(0, memberExpr.Member.Name);
            current = memberExpr.Expression;
        }
        
        // The last segment is the property name, the rest are the path
        if (pathSegments.Count == 0)
        {
            throw new UnsupportedExpressionException(
                "Could not extract property path from list expression. " +
                "Expected a member access expression (e.g., x.Tags or x.Metadata.Keywords).",
                expression);
        }
        
        var propertyName = pathSegments[^1];
        var path = pathSegments.Take(pathSegments.Count - 1).ToArray();
        
        return (path, propertyName);
    }

    /// <summary>
    /// Wraps a single value in a list for list_append operations.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A list containing the single value.</returns>
    private static object? WrapInList(object? value)
    {
        if (value == null)
            return new List<object?> { null };
        
        var list = new List<object> { value };
        return list;
    }

    /// <summary>
    /// Converts a value to a list for list_append operations.
    /// </summary>
    /// <param name="value">The value to convert (array or enumerable).</param>
    /// <returns>A list containing the values.</returns>
    private static object? ConvertToList(object? value)
    {
        if (value == null)
            return new List<object?>();
        
        if (value is Array array)
        {
            var list = new List<object>();
            foreach (var item in array)
            {
                list.Add(item);
            }
            return list;
        }
        
        // If it's already a list or enumerable, convert to List<object>
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var list = new List<object>();
            foreach (var item in enumerable)
            {
                list.Add(item);
            }
            return list;
        }
        
        // If it's a single value, wrap it in a list
        return new List<object> { value };
    }

    private Operation TranslateSetDynamicFieldOperation(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        ExpressionContext context)
    {
        // SetDynamicField can be called as:
        // - Instance method: x.DynamicFields.SetDynamicField(fieldName, value) - Arguments has 2 elements
        // - Extension method: SetDynamicField(accessor, fieldName, value) - Arguments has 3 elements
        
        Expression fieldNameArg;
        Expression valueArg;
        
        if (methodCall.Object != null)
        {
            // Instance method call: x.DynamicFields.SetDynamicField(fieldName, value)
            if (methodCall.Arguments.Count < 2)
            {
                throw new UnsupportedExpressionException(
                    $"SetDynamicField() method requires a field name and value. " +
                    $"Example: x.DynamicFields.SetDynamicField(\"customField\", value).",
                    "SetDynamicField",
                    methodCall);
            }
            fieldNameArg = methodCall.Arguments[0];
            valueArg = methodCall.Arguments[1];
        }
        else
        {
            // Extension method call: SetDynamicField(accessor, fieldName, value)
            if (methodCall.Arguments.Count < 3)
            {
                throw new UnsupportedExpressionException(
                    $"SetDynamicField() method requires a field name and value. " +
                    $"Example: x.DynamicFields.SetDynamicField(\"customField\", value).",
                    "SetDynamicField",
                    methodCall);
            }
            fieldNameArg = methodCall.Arguments[1];
            valueArg = methodCall.Arguments[2];
        }
        
        var fieldName = EvaluateExpression(fieldNameArg) as string;
        
        if (string.IsNullOrEmpty(fieldName))
        {
            throw new UnsupportedExpressionException(
                $"SetDynamicField() requires a non-empty field name. " +
                $"Example: x.DynamicFields.SetDynamicField(\"customField\", value).",
                "SetDynamicField",
                methodCall);
        }
        
        var value = EvaluateExpression(valueArg);
        
        // Generate attribute name placeholder for the dynamic field
        var attributeNamePlaceholder = GetDynamicFieldAttributeName(fieldName, context);
        
        // Capture the value
        var valuePlaceholder = CaptureValue(value, context, null);
        
        // Build SET expression
        var expression = $"{attributeNamePlaceholder} = {valuePlaceholder}";
        
        return new Operation
        {
            Type = OperationType.Set,
            Expression = expression
        };
    }

    private Operation TranslateRemoveDynamicFieldOperation(
        MethodCallExpression methodCall,
        ParameterExpression parameter,
        ExpressionContext context)
    {
        // RemoveDynamicField can be called as:
        // - Instance method: x.DynamicFields.RemoveDynamicField(fieldName) - Arguments has 1 element
        // - Extension method: RemoveDynamicField(accessor, fieldName) - Arguments has 2 elements
        
        Expression fieldNameArg;
        
        if (methodCall.Object != null)
        {
            // Instance method call: x.DynamicFields.RemoveDynamicField(fieldName)
            if (methodCall.Arguments.Count < 1)
            {
                throw new UnsupportedExpressionException(
                    $"RemoveDynamicField() method requires a field name. " +
                    $"Example: x.DynamicFields.RemoveDynamicField(\"customField\").",
                    "RemoveDynamicField",
                    methodCall);
            }
            fieldNameArg = methodCall.Arguments[0];
        }
        else
        {
            // Extension method call: RemoveDynamicField(accessor, fieldName)
            if (methodCall.Arguments.Count < 2)
            {
                throw new UnsupportedExpressionException(
                    $"RemoveDynamicField() method requires a field name. " +
                    $"Example: x.DynamicFields.RemoveDynamicField(\"customField\").",
                    "RemoveDynamicField",
                    methodCall);
            }
            fieldNameArg = methodCall.Arguments[1];
        }
        
        var fieldName = EvaluateExpression(fieldNameArg) as string;
        
        if (string.IsNullOrEmpty(fieldName))
        {
            throw new UnsupportedExpressionException(
                $"RemoveDynamicField() requires a non-empty field name. " +
                $"Example: x.DynamicFields.RemoveDynamicField(\"customField\").",
                "RemoveDynamicField",
                methodCall);
        }
        
        // Generate attribute name placeholder for the dynamic field
        var attributeNamePlaceholder = GetDynamicFieldAttributeName(fieldName, context);
        
        // Build REMOVE expression (no value needed)
        return new Operation
        {
            Type = OperationType.Remove,
            Expression = attributeNamePlaceholder
        };
    }

    /// <summary>
    /// Gets or creates an attribute name placeholder for a dynamic field.
    /// Dynamic fields use the #dynField prefix to distinguish them from mapped properties.
    /// </summary>
    private string GetDynamicFieldAttributeName(string fieldName, ExpressionContext context)
    {
        // Generate attribute name placeholder for dynamic field
        var count = context.AttributeNames.AttributeNames.Count;
        var attributeNamePlaceholder = count < 10 
            ? string.Concat("#dynField", count.ToString()) 
            : $"#dynField{count}";
        
        context.AttributeNames.WithAttribute(attributeNamePlaceholder, fieldName);
        return attributeNamePlaceholder;
    }

    /// <summary>
    /// Translates a DynamicFields property assignment to SET and REMOVE operations.
    /// </summary>
    /// <param name="valueExpression">The expression representing the DynamicFieldCollection value.</param>
    /// <param name="context">The expression context for parameter and attribute name tracking.</param>
    /// <returns>A list of operations (SET for each field, REMOVE for each removed field).</returns>
    /// <remarks>
    /// <para>
    /// When a DynamicFields property is assigned a DynamicFieldCollection in an update model expression,
    /// this method generates:
    /// </para>
    /// <list type="bullet">
    /// <item><description>SET clauses for each field in the collection</description></item>
    /// <item><description>REMOVE clauses for each field in the RemovedFields set</description></item>
    /// </list>
    /// <para>
    /// If the DynamicFieldCollection is null, no operations are generated.
    /// </para>
    /// </remarks>
    private List<Operation> TranslateDynamicFieldsAssignment(
        Expression valueExpression,
        ExpressionContext context)
    {
        var operations = new List<Operation>();
        
        // Evaluate the expression to get the DynamicFieldCollection
        var value = EvaluateExpression(valueExpression);
        
        // If null, skip processing (no dynamic field changes)
        if (value == null)
        {
            return operations;
        }
        
        // Verify the value is a DynamicFieldCollection
        if (value is not DynamicFieldCollection collection)
        {
            throw new UnsupportedExpressionException(
                $"DynamicFields property must be assigned a DynamicFieldCollection or null. " +
                $"Found type: {value.GetType().Name}.",
                valueExpression);
        }
        
        // Generate SET clauses for each field in the collection
        foreach (var kvp in collection)
        {
            var fieldName = kvp.Key;
            var attributeValue = kvp.Value;
            
            // Generate attribute name placeholder for the dynamic field
            var attributeNamePlaceholder = GetDynamicFieldAttributeName(fieldName, context);
            
            // Generate value placeholder and add to context
            var valuePlaceholder = context.ParameterGenerator.GenerateParameterName();
            context.AttributeValues.AttributeValues.Add(valuePlaceholder, attributeValue);
            
            // Build SET expression
            operations.Add(new Operation
            {
                Type = OperationType.Set,
                Expression = $"{attributeNamePlaceholder} = {valuePlaceholder}"
            });
        }
        
        // Generate REMOVE clauses for each field in RemovedFields
        foreach (var removedFieldName in collection.RemovedFields)
        {
            // Generate attribute name placeholder for the removed field
            var attributeNamePlaceholder = GetDynamicFieldAttributeName(removedFieldName, context);
            
            // Build REMOVE expression
            operations.Add(new Operation
            {
                Type = OperationType.Remove,
                Expression = attributeNamePlaceholder
            });
        }
        
        return operations;
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

    /// <summary>
    /// Validates and processes computed field assignments after all bindings have been collected.
    /// Checks FDDB072 (partial assignment) and FDDB073 (mixed assignment), then recomputes
    /// the concatenated value and generates a SET expression for the computed field.
    /// Each computed field is validated independently (Req 5.5).
    /// </summary>
    /// <param name="pendingComputedAssignments">Dictionary of source property names to their evaluated values.</param>
    /// <param name="directlyAssignedProperties">Set of property names that were assigned in the normal SET/ADD/REMOVE/DELETE flow.</param>
    /// <param name="context">The expression context containing entity metadata.</param>
    /// <param name="setOperations">The list of SET operations to append the recomputed value to.</param>
    private void ValidateAndProcessComputedFields(
        Dictionary<string, object?> pendingComputedAssignments,
        HashSet<string> directlyAssignedProperties,
        ExpressionContext context,
        List<string> setOperations)
    {
        var computedProperties = context.EntityMetadata!.Properties
            .Where(p => p.ComputedField != null)
            .ToList();

        foreach (var computedProp in computedProperties)
        {
            var cf = computedProp.ComputedField!;
            var computedFieldName = computedProp.PropertyName;

            // Gather which source properties have been assigned
            var assignedSources = new Dictionary<string, object?>();

            // Check direct source properties (those with ComputedFieldTarget pointing to this field)
            foreach (var sourceName in cf.SourceProperties)
            {
                if (pendingComputedAssignments.ContainsKey(sourceName))
                {
                    assignedSources[sourceName] = pendingComputedAssignments[sourceName];
                }
            }

            // Also check extracted properties targeting this computed field
            var extractedProps = context.EntityMetadata.Properties
                .Where(p => p.ExtractedField?.SourceProperty == computedFieldName);
            foreach (var extracted in extractedProps)
            {
                if (pendingComputedAssignments.ContainsKey(extracted.PropertyName))
                {
                    // Map extracted property to its corresponding source property by index
                    var sourceIndex = extracted.ExtractedField!.Index;
                    if (sourceIndex < cf.SourceProperties.Length)
                    {
                        var sourceName = cf.SourceProperties[sourceIndex];
                        assignedSources[sourceName] = pendingComputedAssignments[extracted.PropertyName];
                    }
                }
            }

            bool directlyAssigned = directlyAssignedProperties.Contains(computedFieldName);
            bool anySourceAssigned = assignedSources.Count > 0;

            // FDDB073: Mixed direct + source assignment
            if (directlyAssigned && anySourceAssigned)
            {
                ComputedFieldDiagnostics.ThrowMixedAssignment(computedFieldName);
            }

            if (anySourceAssigned)
            {
                // FDDB072: Partial source assignment
                var missingSources = cf.SourceProperties
                    .Where(s => !assignedSources.ContainsKey(s))
                    .ToList();
                if (missingSources.Count > 0)
                {
                    ComputedFieldDiagnostics.ThrowPartialSourceAssignment(computedFieldName, missingSources);
                }

                // Recompute: concatenate values in order
                var parts = cf.SourceProperties
                    .Select(s => assignedSources[s]?.ToString() ?? string.Empty)
                    .ToArray();
                var recomputedValue = string.Join(cf.Separator, parts);

                // Apply prefix if configured
                if (!string.IsNullOrEmpty(cf.Prefix))
                {
                    var prefixSep = cf.PrefixSeparator ?? cf.Separator;
                    recomputedValue = cf.Prefix + prefixSep + recomputedValue;
                }

                // Generate SET for the computed field's DynamoDB attribute
                var attributeName = GetAttributeName(computedFieldName, context);
                var paramName = CaptureValue(recomputedValue, context, computedProp);
                setOperations.Add($"{attributeName} = {paramName}");

                // Also generate SET for source properties that have their own DynamoDB attribute.
                // A source property with a non-empty AttributeName has an independent column in DynamoDB
                // and must be updated alongside the computed field.
                foreach (var sourceName in cf.SourceProperties)
                {
                    var sourcePropertyMetadata = context.EntityMetadata.Properties
                        .FirstOrDefault(p => p.PropertyName == sourceName);
                    if (sourcePropertyMetadata != null &&
                        !string.IsNullOrEmpty(sourcePropertyMetadata.AttributeName))
                    {
                        var sourceAttrName = GetAttributeName(sourceName, context);
                        var sourceValue = assignedSources[sourceName];
                        var sourceParamName = CaptureValue(sourceValue, context, sourcePropertyMetadata);
                        setOperations.Add($"{sourceAttrName} = {sourceParamName}");
                    }
                }

                // Also generate SET for extracted properties that have their own DynamoDB attribute
                // and were assigned (they map to a source property by index but may have their own column)
                foreach (var extracted in extractedProps)
                {
                    if (pendingComputedAssignments.ContainsKey(extracted.PropertyName) &&
                        !string.IsNullOrEmpty(extracted.AttributeName))
                    {
                        var extractedAttrName = GetAttributeName(extracted.PropertyName, context);
                        var extractedValue = pendingComputedAssignments[extracted.PropertyName];
                        var extractedParamName = CaptureValue(extractedValue, context, extracted);
                        setOperations.Add($"{extractedAttrName} = {extractedParamName}");
                    }
                }
            }
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

    /// <summary>
    /// Gets the attribute name with path support for nested properties.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="context">The expression context.</param>
    /// <param name="pathPrefix">The path prefix for nested properties.</param>
    /// <param name="expression">The expression for error reporting.</param>
    /// <returns>The DynamoDB document path (e.g., "#address.#city").</returns>
    private string GetAttributeNameWithPath(string propertyName, ExpressionContext context, string[] pathPrefix, Expression? expression = null)
    {
        // If no path prefix, use the simple version
        if (pathPrefix.Length == 0)
        {
            return GetAttributeName(propertyName, context, expression);
        }

        // Build document path using DocumentPathBuilder
        var pathBuilder = new DocumentPathBuilder(context.AttributeNames);
        
        // Add all path prefix segments
        foreach (var segment in pathPrefix)
        {
            // For nested properties, we use the property name as the attribute name
            // since we don't have metadata for nested types
            // Convert to lowercase for DynamoDB convention (camelCase)
            var attributeName = GetNestedAttributeName(segment, context);
            pathBuilder.AddProperty(segment, attributeName);
        }
        
        // Add the final property
        var finalAttributeName = GetNestedAttributeName(propertyName, context);
        pathBuilder.AddProperty(propertyName, finalAttributeName);
        
        return pathBuilder.Build();
    }

    /// <summary>
    /// Gets the DynamoDB attribute name for a nested property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The DynamoDB attribute name.</returns>
    /// <remarks>
    /// For nested properties, we first check if the property exists in the entity metadata
    /// (for the root property). If not found, we convert the property name to lowercase
    /// following DynamoDB naming conventions.
    /// </remarks>
    private string GetNestedAttributeName(string propertyName, ExpressionContext context)
    {
        // First, check if this property exists in entity metadata (for root properties)
        if (context.EntityMetadata != null)
        {
            var propertyMetadata = context.EntityMetadata.Properties
                .FirstOrDefault(p => p.PropertyName == propertyName);
            
            if (propertyMetadata != null)
            {
                return propertyMetadata.AttributeName;
            }
        }
        
        // For nested properties without metadata, convert to lowercase (camelCase convention)
        // This matches the typical DynamoDB attribute naming convention
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;
        
        // Convert first character to lowercase
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
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

    /// <summary>
    /// Determines whether a property requires encryption based on its metadata.
    /// </summary>
    /// <param name="propertyMetadata">The property metadata to check.</param>
    /// <returns>True if the property is marked as encrypted; otherwise, false.</returns>
    /// <remarks>
    /// This method checks the PropertyMetadata.IsEncrypted flag to determine if a property
    /// requires encryption. When true, the CaptureValue method will mark the parameter
    /// for deferred encryption by the request builder.
    /// </remarks>
    private bool IsEncryptedProperty(PropertyMetadata propertyMetadata)
    {
        return propertyMetadata.IsEncrypted;
    }

    private object? ApplyEncryption(object? value, string propertyName, string attributeName, Expression expression)
    {
        if (value == null)
            return null;

        if (_fieldEncryptor == null)
        {
            throw new EncryptionRequiredException(
                $"Property '{propertyName}' (DynamoDB attribute: '{attributeName}') is marked as encrypted but no IFieldEncryptor is configured. " +
                $"To fix this issue: " +
                $"1. Implement the IFieldEncryptor interface (e.g., using AWS KMS or another encryption provider). " +
                $"2. Pass the encryptor via FluentDynamoDbOptions when creating the table, or " +
                $"3. Set it in the DynamoDbOperationContext before executing update operations. " +
                $"Example: new FluentDynamoDbOptions().WithEncryption(fieldEncryptor). " +
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
            // Use AOT-compatible array creation for common types
            if (expression is NewArrayExpression newArray)
            {
                var elementType = newArray.Type.GetElementType()!;
                var count = newArray.Expressions.Count;
                
                // Evaluate all elements first
                var elements = new object?[count];
                for (int i = 0; i < count; i++)
                {
                    elements[i] = EvaluateExpression(newArray.Expressions[i]);
                }
                
                // Create typed array for common types (AOT-compatible)
                return CreateTypedArray(elementType, elements);
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
                    formattedList.Add(item!);
                }
            }
            return formattedList;
        }
        
        // If it's not a List<object>, return as-is
        // The format will be applied during AttributeValue conversion if needed
        return value;
    }

    private object ApplyFormatToSetElements(object value, string format, string propertyName)
    {
        // Apply format to each element in a set or array
        // DynamoDB only supports: HashSet<string> (SS), HashSet<numeric> (NS), HashSet<byte[]> (BS)
        // Format strings only make sense for numeric types
        
        // Handle string arrays (params string[])
        if (value is string[] stringArray)
        {
            // Strings don't need formatting, return as-is
            return stringArray;
        }
        
        // Handle numeric arrays with formatting
        if (value is int[] intArray)
        {
            var result = new string[intArray.Length];
            for (int i = 0; i < intArray.Length; i++)
                result[i] = intArray[i].ToString(format, CultureInfo.InvariantCulture);
            return result;
        }
        
        if (value is long[] longArray)
        {
            var result = new string[longArray.Length];
            for (int i = 0; i < longArray.Length; i++)
                result[i] = longArray[i].ToString(format, CultureInfo.InvariantCulture);
            return result;
        }
        
        if (value is decimal[] decimalArray)
        {
            var result = new string[decimalArray.Length];
            for (int i = 0; i < decimalArray.Length; i++)
                result[i] = decimalArray[i].ToString(format, CultureInfo.InvariantCulture);
            return result;
        }
        
        if (value is double[] doubleArray)
        {
            var result = new string[doubleArray.Length];
            for (int i = 0; i < doubleArray.Length; i++)
                result[i] = doubleArray[i].ToString(format, CultureInfo.InvariantCulture);
            return result;
        }
        
        if (value is float[] floatArray)
        {
            var result = new string[floatArray.Length];
            for (int i = 0; i < floatArray.Length; i++)
                result[i] = floatArray[i].ToString(format, CultureInfo.InvariantCulture);
            return result;
        }
        
        // Handle HashSet types - DynamoDB only supports string, numeric, and binary sets
        if (value is HashSet<string> stringSet)
        {
            // Strings don't need formatting, return as-is
            return stringSet;
        }
        
        if (value is HashSet<int> intSet)
        {
            var result = new HashSet<string>();
            foreach (var item in intSet)
                result.Add(item.ToString(format, CultureInfo.InvariantCulture));
            return result;
        }
        
        if (value is HashSet<long> longSet)
        {
            var result = new HashSet<string>();
            foreach (var item in longSet)
                result.Add(item.ToString(format, CultureInfo.InvariantCulture));
            return result;
        }
        
        if (value is HashSet<decimal> decimalSet)
        {
            var result = new HashSet<string>();
            foreach (var item in decimalSet)
                result.Add(item.ToString(format, CultureInfo.InvariantCulture));
            return result;
        }
        
        if (value is HashSet<double> doubleSet)
        {
            var result = new HashSet<string>();
            foreach (var item in doubleSet)
                result.Add(item.ToString(format, CultureInfo.InvariantCulture));
            return result;
        }
        
        if (value is HashSet<float> floatSet)
        {
            var result = new HashSet<string>();
            foreach (var item in floatSet)
                result.Add(item.ToString(format, CultureInfo.InvariantCulture));
            return result;
        }
        
        // If it's not a supported set type, return as-is
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

        // Check if this parameter requires encryption
        if (propertyMetadata != null && IsEncryptedProperty(propertyMetadata))
        {
            // Mark this parameter for encryption by the request builder
            // Do NOT encrypt inline - encryption is deferred to the request builder layer
            // Note: Even null/empty values are marked for encryption, but the request builder
            // will skip actual encryption for them since there's nothing to encrypt
            context.ParameterMetadata.Add(new ParameterMetadata
            {
                ParameterName = parameterName,
                Value = attributeValue,
                RequiresEncryption = true,
                PropertyName = propertyMetadata.PropertyName,
                AttributeName = propertyMetadata.AttributeName
            });
        }

        // Log parameter capture with sensitive data redaction
        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            var attributeName = propertyMetadata?.AttributeName;
            var isSensitive = attributeName != null && _isSensitiveField != null && _isSensitiveField(attributeName);
            
            var valueToLog = isSensitive ? "[REDACTED]" : (value?.ToString() ?? "null");
            
            _logger.LogDebug(
                LogEventIds.ExpressionTranslation,
                "Update expression parameter {ParameterName} = {Value} (Property: {PropertyName}, RequiresEncryption: {RequiresEncryption})",
                parameterName,
                valueToLog,
                propertyMetadata?.PropertyName ?? "unknown",
                propertyMetadata?.IsEncrypted ?? false);
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
            DateOnly d => new AttributeValue { S = d.ToString("O", CultureInfo.InvariantCulture) },
            TimeOnly t => new AttributeValue { S = t.ToString("O", CultureInfo.InvariantCulture) },
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

        // Handle HashSet - DynamoDB only supports string, numeric, and binary sets
        // Use explicit type checks for AOT compatibility (no dynamic)
        if (value is HashSet<string> stringSet)
        {
            if (stringSet.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { SS = stringSet.ToList() };
        }
        
        if (value is HashSet<int> intSet)
        {
            if (intSet.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { NS = intSet.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList() };
        }
        
        if (value is HashSet<long> longSet)
        {
            if (longSet.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { NS = longSet.Select(l => l.ToString(CultureInfo.InvariantCulture)).ToList() };
        }
        
        if (value is HashSet<decimal> decimalSet)
        {
            if (decimalSet.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { NS = decimalSet.Select(d => d.ToString(CultureInfo.InvariantCulture)).ToList() };
        }
        
        if (value is HashSet<double> doubleSet)
        {
            if (doubleSet.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { NS = doubleSet.Select(d => d.ToString(CultureInfo.InvariantCulture)).ToList() };
        }
        
        if (value is HashSet<float> floatSet)
        {
            if (floatSet.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { NS = floatSet.Select(f => f.ToString(CultureInfo.InvariantCulture)).ToList() };
        }

        // Handle List - use explicit type checks for common types, fall back for others
        if (value is List<string> stringList)
        {
            if (stringList.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { L = stringList.Select(s => new AttributeValue { S = s }).ToList() };
        }
        
        if (value is List<int> intList)
        {
            if (intList.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { L = intList.Select(i => new AttributeValue { N = i.ToString(CultureInfo.InvariantCulture) }).ToList() };
        }
        
        if (value is List<object> objectList)
        {
            if (objectList.Count == 0)
                return new AttributeValue { NULL = true };
            return new AttributeValue { L = objectList.Select(ConvertToAttributeValue).ToList() };
        }
        
        // For other IEnumerable types, try to enumerate
        if (value is System.Collections.IEnumerable enumerable)
        {
            var list = new List<AttributeValue>();
            foreach (var item in enumerable)
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

    /// <summary>
    /// Creates a typed array from evaluated elements in an AOT-compatible way.
    /// Handles common types used in DynamoDB update expressions (strings, numbers).
    /// </summary>
    private static Array CreateTypedArray(Type elementType, object?[] elements)
    {
        // Handle common types explicitly for AOT compatibility
        if (elementType == typeof(string))
        {
            var result = new string?[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] as string;
            return result;
        }
        
        if (elementType == typeof(int))
        {
            var result = new int[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] is int v ? v : Convert.ToInt32(elements[i], CultureInfo.InvariantCulture);
            return result;
        }
        
        if (elementType == typeof(long))
        {
            var result = new long[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] is long v ? v : Convert.ToInt64(elements[i], CultureInfo.InvariantCulture);
            return result;
        }
        
        if (elementType == typeof(double))
        {
            var result = new double[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] is double v ? v : Convert.ToDouble(elements[i], CultureInfo.InvariantCulture);
            return result;
        }
        
        if (elementType == typeof(decimal))
        {
            var result = new decimal[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] is decimal v ? v : Convert.ToDecimal(elements[i], CultureInfo.InvariantCulture);
            return result;
        }
        
        if (elementType == typeof(float))
        {
            var result = new float[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] is float v ? v : Convert.ToSingle(elements[i], CultureInfo.InvariantCulture);
            return result;
        }
        
        if (elementType == typeof(bool))
        {
            var result = new bool[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] is bool v ? v : Convert.ToBoolean(elements[i], CultureInfo.InvariantCulture);
            return result;
        }
        
        if (elementType == typeof(byte))
        {
            var result = new byte[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] is byte v ? v : Convert.ToByte(elements[i], CultureInfo.InvariantCulture);
            return result;
        }
        
        if (elementType == typeof(short))
        {
            var result = new short[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i] is short v ? v : Convert.ToInt16(elements[i], CultureInfo.InvariantCulture);
            return result;
        }
        
        // For object arrays or unknown types, use object array
        // This is still AOT-compatible as we're not using Array.CreateInstance
        var objectResult = new object?[elements.Length];
        Array.Copy(elements, objectResult, elements.Length);
        return objectResult;
    }
}

enum OperationType
{
    Set,
    Add,
    Remove,
    Delete,
    Skip
}

class Operation
{
    public OperationType Type { get; set; }
    public string Expression { get; set; } = string.Empty;
}
