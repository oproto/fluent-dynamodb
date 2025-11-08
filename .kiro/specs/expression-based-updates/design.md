# Design Document

## Overview

This design introduces type-safe, expression-based update operations for DynamoDB UpdateItem requests. The implementation uses source-generated helper classes to provide a clean API that works within C# expression tree limitations while maintaining full type safety, IntelliSense support, and AOT compatibility.

The key innovation is using a parameter object with `UpdateExpressionProperty<T>` wrapper types that enable extension methods scoped by generic type constraints. This allows operations like `Add()`, `Remove()`, and `Delete()` to only be available on appropriate property types.

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    UpdateItemRequestBuilder                      │
│  - Existing string-based Set() methods                          │
│  - New expression-based Set() extension method                  │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ├─────────────────────────────────────────────┐
                     │                                             │
          ┌──────────▼──────────┐                    ┌────────────▼────────────┐
          │ UpdateExpression    │                    │  Source Generator       │
          │ Extensions          │                    │  - {Entity}UpdateModel  │
          │ - Set<TEntity>()    │                    │  - {Entity}Update       │
          │                     │                    │    Expressions          │
          └──────────┬──────────┘                    │  - Extension methods    │
                     │                                └─────────────────────────┘
          ┌──────────▼──────────┐
          │ UpdateExpression    │
          │ Translator          │
          │ - SET actions       │
          │ - ADD actions       │
          │ - REMOVE actions    │
          │ - DELETE actions    │
          │ - Functions         │
          └──────────┬──────────┘
                     │
          ┌──────────▼──────────┐
          │ ExpressionContext   │
          │ - AttributeValues   │
          │ - AttributeNames    │
          │ - EntityMetadata    │
          │ - FieldEncryptor    │
          └─────────────────────┘
```

### Data Flow

```
Developer writes:
  table.Users.Update(userId)
    .Set(x => new UserUpdateModel 
    {
        Name = "John",
        LoginCount = x.LoginCount.Add(1)
    })
    .UpdateAsync()

↓

Extension method captures expression:
  Expression<Func<UserUpdateExpressions, UserUpdateModel>> expr

↓

UpdateExpressionTranslator analyzes MemberInitExpression:
  - Name = "John" → SET #name = :p0
  - LoginCount = x.LoginCount.Add(1) → ADD #loginCount :p1
  - Applies format strings
  - Encrypts sensitive values
  - Validates key properties not updated

↓

AttributeValues and AttributeNames populated:
  #name → "name"
  #loginCount → "login_count"
  :p0 → AttributeValue { S = "John" }
  :p1 → AttributeValue { N = "1" }

↓

UpdateItemRequest built with combined expression:
  "SET #name = :p0 ADD #loginCount :p1"

↓

UpdateAsync() executes the request
```

## Components and Interfaces

### 1. Source-Generated Classes

The source generator creates three types of classes for each entity:

#### {Entity}UpdateExpressions

```csharp
/// <summary>
/// Expression parameter class for User update operations.
/// Properties are wrapped in UpdateExpressionProperty<T> to enable type-safe extension methods.
/// </summary>
public partial class UserUpdateExpressions
{
    /// <summary>
    /// Gets the Name property for use in update expressions.
    /// </summary>
    public UpdateExpressionProperty<string> Name { get; } = new();
    
    /// <summary>
    /// Gets the LoginCount property for use in update expressions.
    /// Supports Add() for atomic increment/decrement.
    /// </summary>
    public UpdateExpressionProperty<int> LoginCount { get; } = new();
    
    /// <summary>
    /// Gets the Tags property for use in update expressions.
    /// Supports Delete() for removing elements from the set.
    /// </summary>
    public UpdateExpressionProperty<HashSet<string>> Tags { get; } = new();
    
    // ... all entity properties
}
```

#### {Entity}UpdateModel

```csharp
/// <summary>
/// Return type for User update expressions.
/// Properties are nullable to indicate which attributes to update.
/// </summary>
public partial class UserUpdateModel
{
    /// <summary>
    /// Gets or sets the Name value to update.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Gets or sets the LoginCount value to update.
    /// Can be set to a constant or the result of an operation like Add().
    /// </summary>
    public int? LoginCount { get; set; }
    
    /// <summary>
    /// Gets or sets the Tags value to update.
    /// Can be set to a new set or the result of Delete().
    /// </summary>
    public HashSet<string>? Tags { get; set; }
    
    // ... all entity properties as nullable
}
```

### 2. UpdateExpressionProperty<T>

```csharp
namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Wrapper type for properties in update expressions.
/// Enables extension methods scoped by generic type constraints.
/// This type is only used in expression trees and should not be instantiated directly.
/// </summary>
/// <typeparam name="T">The underlying property type.</typeparam>
public sealed class UpdateExpressionProperty<T>
{
    // Empty class - only used as a marker type for extension methods
    internal UpdateExpressionProperty() { }
}
```

### 3. Extension Methods

Extension methods are defined on `UpdateExpressionProperty<T>` with generic constraints to ensure type safety:

```csharp
namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Extension methods for update expression operations.
/// These methods are markers for expression translation and should not be called directly.
/// </summary>
public static class UpdateExpressionPropertyExtensions
{
    /// <summary>
    /// Performs an atomic ADD operation for numeric properties.
    /// Translates to DynamoDB ADD action: ADD #attr :val
    /// </summary>
    /// <param name="property">The property to increment/decrement.</param>
    /// <param name="value">The value to add (use negative for decrement).</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <example>
    /// <code>
    /// .Set(x => new UserUpdateModel { LoginCount = x.LoginCount.Add(1) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static int Add(this UpdateExpressionProperty<int> property, int value)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Performs an atomic ADD operation for long properties.
    /// </summary>
    [ExpressionOnly]
    public static long Add(this UpdateExpressionProperty<long> property, long value)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Performs an atomic ADD operation for decimal properties.
    /// </summary>
    [ExpressionOnly]
    public static decimal Add(this UpdateExpressionProperty<decimal> property, decimal value)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Performs an atomic ADD operation for double properties.
    /// </summary>
    [ExpressionOnly]
    public static double Add(this UpdateExpressionProperty<double> property, double value)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Adds elements to a set using DynamoDB's ADD action.
    /// Translates to: ADD #attr :val
    /// </summary>
    /// <typeparam name="T">The element type of the set.</typeparam>
    /// <param name="property">The set property.</param>
    /// <param name="elements">Elements to add to the set.</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <example>
    /// <code>
    /// .Set(x => new UserUpdateModel { Tags = x.Tags.Add("premium", "verified") })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static HashSet<T> Add<T>(this UpdateExpressionProperty<HashSet<T>> property, params T[] elements)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Removes an attribute using DynamoDB's REMOVE action.
    /// Translates to: REMOVE #attr
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="property">The property to remove.</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <example>
    /// <code>
    /// .Set(x => new UserUpdateModel { TempData = x.TempData.Remove() })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static T Remove<T>(this UpdateExpressionProperty<T> property)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Removes elements from a set using DynamoDB's DELETE action.
    /// Translates to: DELETE #attr :val
    /// </summary>
    /// <typeparam name="T">The element type of the set.</typeparam>
    /// <param name="property">The set property.</param>
    /// <param name="elements">Elements to remove from the set.</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <example>
    /// <code>
    /// .Set(x => new UserUpdateModel { Tags = x.Tags.Delete("old-tag") })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static HashSet<T> Delete<T>(this UpdateExpressionProperty<HashSet<T>> property, params T[] elements)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Uses DynamoDB's if_not_exists function to set a value only if the attribute doesn't exist.
    /// Translates to: SET #attr = if_not_exists(#attr, :val)
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="property">The property to check.</param>
    /// <param name="defaultValue">The value to set if the attribute doesn't exist.</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <example>
    /// <code>
    /// .Set(x => new UserUpdateModel { ViewCount = x.ViewCount.IfNotExists(0) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static T IfNotExists<T>(this UpdateExpressionProperty<T> property, T defaultValue)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Appends elements to the end of a list using DynamoDB's list_append function.
    /// Translates to: SET #attr = list_append(#attr, :val)
    /// </summary>
    /// <typeparam name="T">The element type of the list.</typeparam>
    /// <param name="property">The list property.</param>
    /// <param name="elements">Elements to append to the list.</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <example>
    /// <code>
    /// .Set(x => new UserUpdateModel { History = x.History.ListAppend("new-event") })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> ListAppend<T>(this UpdateExpressionProperty<List<T>> property, params T[] elements)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
    
    /// <summary>
    /// Prepends elements to the beginning of a list using DynamoDB's list_append function.
    /// Translates to: SET #attr = list_append(:val, #attr)
    /// </summary>
    /// <typeparam name="T">The element type of the list.</typeparam>
    /// <param name="property">The list property.</param>
    /// <param name="elements">Elements to prepend to the list.</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <example>
    /// <code>
    /// .Set(x => new UserUpdateModel { History = x.History.ListPrepend("new-event") })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> ListPrepend<T>(this UpdateExpressionProperty<List<T>> property, params T[] elements)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");
}
```

### 4. UpdateExpressionExtensions

```csharp
namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for expression-based update operations on UpdateItemRequestBuilder.
/// </summary>
public static class UpdateExpressionExtensions
{
    /// <summary>
    /// Specifies update operations using a type-safe C# lambda expression.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <typeparam name="TUpdateExpressions">The generated UpdateExpressions type.</typeparam>
    /// <typeparam name="TUpdateModel">The generated UpdateModel type.</typeparam>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
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
    ///     .Set(x => new UserUpdateModel 
    ///     {
    ///         Name = "John",
    ///         Status = "Active"
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // Atomic ADD operation
    /// table.Users.Update(userId)
    ///     .Set(x => new UserUpdateModel 
    ///     {
    ///         LoginCount = x.LoginCount.Add(1)
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // Arithmetic in SET
    /// table.Users.Update(userId)
    ///     .Set(x => new UserUpdateModel 
    ///     {
    ///         Score = x.Score + 10
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // REMOVE operation
    /// table.Users.Update(userId)
    ///     .Set(x => new UserUpdateModel 
    ///     {
    ///         TempData = x.TempData.Remove()
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // DELETE from set
    /// table.Users.Update(userId)
    ///     .Set(x => new UserUpdateModel 
    ///     {
    ///         Tags = x.Tags.Delete("old-tag")
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // if_not_exists function
    /// table.Users.Update(userId)
    ///     .Set(x => new UserUpdateModel 
    ///     {
    ///         ViewCount = x.ViewCount.IfNotExists(0)
    ///     })
    ///     .UpdateAsync();
    /// 
    /// // Combined operations
    /// table.Users.Update(userId)
    ///     .Set(x => new UserUpdateModel 
    ///     {
    ///         Name = "John",
    ///         LoginCount = x.LoginCount.Add(1),
    ///         TempData = x.TempData.Remove()
    ///     })
    ///     .UpdateAsync();
    /// </code>
    /// </example>
    public static UpdateItemRequestBuilder<TEntity> Set<TEntity, TUpdateExpressions, TUpdateModel>(
        this UpdateItemRequestBuilder<TEntity> builder,
        Expression<Func<TUpdateExpressions, TUpdateModel>> expression,
        EntityMetadata? metadata = null)
        where TEntity : class
        where TUpdateExpressions : new()
        where TUpdateModel : new()
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));

        // Resolve metadata if not provided
        if (metadata == null)
        {
            metadata = MetadataResolver.GetEntityMetadata<TEntity>();
        }

        // Create expression context
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            metadata,
            ExpressionValidationMode.None);

        // Create translator with encryption support
        var fieldEncryptor = builder.GetFieldEncryptor();
        var translator = new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: fieldEncryptor,
            encryptionContextId: null);

        // Translate the expression
        var updateExpression = translator.TranslateUpdateExpression(expression, context);

        // Apply to builder
        return builder.SetUpdateExpression(updateExpression);
    }
}
```

### 5. UpdateExpressionTranslator

```csharp
namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Translates C# lambda expressions to DynamoDB update expression syntax.
/// Supports SET, ADD, REMOVE, and DELETE actions with automatic parameter generation.
/// </summary>
public class UpdateExpressionTranslator
{
    private readonly IDynamoDbLogger? _logger;
    private readonly Func<string, bool>? _isSensitiveField;
    private readonly IFieldEncryptor? _fieldEncryptor;
    private readonly string? _encryptionContextId;

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
    /// <typeparam name="TUpdateExpressions">The UpdateExpressions parameter type.</typeparam>
    /// <typeparam name="TUpdateModel">The UpdateModel return type.</typeparam>
    /// <param name="expression">The lambda expression to translate.</param>
    /// <param name="context">Expression context with metadata and parameter tracking.</param>
    /// <returns>The DynamoDB update expression string.</returns>
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
        // Check for method calls (Add, Remove, Delete, IfNotExists, etc.)
        if (valueExpression is MethodCallExpression methodCall)
        {
            return TranslateMethodCall(methodCall, parameter, propertyName, context);
        }

        // Check for binary operations (arithmetic)
        if (valueExpression is BinaryExpression binary)
        {
            return TranslateBinaryOperation(binary, parameter, propertyName, context);
        }

        // Simple value assignment - SET operation
        return TranslateSimpleSet(valueExpression, propertyName, context);
    }

    // Additional methods for translating specific operation types...
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
```

## Data Models

### UpdateExpressionProperty<T>

- **Purpose**: Wrapper type that enables extension methods scoped by generic type
- **Properties**: None (empty marker class)
- **Usage**: Only in expression trees, never instantiated directly

### Generated Classes

For each entity `User`, the source generator creates:

1. **UserUpdateExpressions**: Parameter type with `UpdateExpressionProperty<T>` properties
2. **UserUpdateModel**: Return type with nullable properties

## Error Handling

### Exception Types

1. **UnsupportedExpressionException**: Thrown when expression pattern is not supported
2. **InvalidUpdateOperationException**: Thrown when attempting to update key properties
3. **UnmappedPropertyException**: Thrown when property doesn't map to DynamoDB attribute
4. **EncryptionRequiredException**: Thrown when encrypted property updated without encryptor
5. **FieldEncryptionException**: Thrown when encryption operation fails
6. **FormatException**: Thrown when format string is invalid for value type

### Validation

- Key properties (partition key, sort key) cannot be updated
- Property must exist in entity metadata
- Extension methods only available for compatible types
- Format strings validated for property types
- Encryption required for properties marked with [Encrypted]

## Testing Strategy

### Unit Tests

1. **UpdateExpressionTranslator Tests**
   - Test SET operation translation
   - Test ADD operation translation
   - Test REMOVE operation translation
   - Test DELETE operation translation
   - Test arithmetic operations
   - Test function calls (if_not_exists, list_append)
   - Test format string application
   - Test encryption integration
   - Test error cases

2. **Extension Method Tests**
   - Test Set() method with various expressions
   - Test method chaining
   - Test metadata resolution
   - Test integration with string-based methods

3. **Source Generator Tests**
   - Test UpdateExpressions class generation
   - Test UpdateModel class generation
   - Test extension method generation
   - Test handling of different property types

### Integration Tests

1. **End-to-End Tests with DynamoDB Local**
   - Test simple SET operations
   - Test atomic ADD operations
   - Test REMOVE operations
   - Test DELETE operations
   - Test arithmetic in SET
   - Test DynamoDB functions
   - Test format string application
   - Test encryption
   - Test combined operations
   - Test conditional updates

## Performance Considerations

- Expression translation happens once per request
- No runtime code generation (AOT compatible)
- Minimal allocations during translation
- Efficient string building for update expressions
- Extension methods have zero runtime cost (inlined)

## Backward Compatibility

- Existing string-based Set() methods unchanged
- Can mix string-based and expression-based methods
- No breaking changes to public APIs
- Existing tests continue to pass

## Known Issues and Fixes

### Expression Evaluation Issue

**Problem**: When the C# compiler generates expression trees for method calls like `x.Count.Add(1)`, the arguments (like `1`) are correctly represented as `ConstantExpression` nodes. However, the `EvaluateExpression` method in `UpdateExpressionTranslator` attempts to compile sub-expressions that may still contain references to the parameter `x` in their parent context, causing a "variable 'x' referenced from scope '', but it is not defined" error.

**Root Cause**: The `EvaluateExpression` method tries to compile expressions by wrapping them in a lambda and calling `Compile()`. When processing method call arguments, even though the argument itself is a constant, the compilation process fails because the expression tree context still references the parameter.

**Example Failing Code**:
```csharp
// User writes this (correct usage):
builder.Set<User, UserUpdateExpressions, UserUpdateModel>(
    x => new UserUpdateModel { Count = x.Count.Add(1) }
)

// C# compiler generates:
// MemberInitExpression {
//   Bindings = [
//     MemberAssignment {
//       Member = "Count",
//       Expression = MethodCallExpression {
//         Method = "Add",
//         Object = MemberExpression { x.Count },
//         Arguments = [ ConstantExpression { Value = 1 } ]  // ← This IS a constant!
//       }
//     }
//   ]
// }

// But EvaluateExpression fails when trying to compile the argument
```

**Solution**: Enhance the `EvaluateExpression` method to:
1. Directly extract values from `ConstantExpression` nodes without compilation
2. Handle `UnaryExpression` (Convert) nodes that wrap constants
3. For method call arguments, use a visitor pattern to extract constant values without compiling the entire sub-expression
4. Only attempt compilation for truly dynamic expressions that don't reference the parameter

**Implementation Approach**:
```csharp
private object? EvaluateExpression(Expression expression)
{
    // Direct constant extraction
    if (expression is ConstantExpression constant)
        return constant.Value;

    // Handle type conversions
    if (expression is UnaryExpression unary && 
        (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
    {
        return EvaluateExpression(unary.Operand);
    }

    // For member access on constants (captured variables)
    if (expression is MemberExpression member && member.Expression is ConstantExpression)
    {
        // Use reflection to get the value from the closure
        var container = ((ConstantExpression)member.Expression).Value;
        var field = member.Member as FieldInfo;
        return field?.GetValue(container);
    }

    // Only compile if expression doesn't reference parameters
    if (!ContainsParameterReference(expression))
    {
        var lambda = Expression.Lambda<Func<object?>>(
            Expression.Convert(expression, typeof(object)));
        var compiled = lambda.Compile();
        return compiled();
    }

    throw new ExpressionTranslationException(
        "Cannot evaluate expression that references update expression parameters",
        expression);
}

private bool ContainsParameterReference(Expression expression)
{
    // Visitor to check if expression tree contains parameter references
    // ...
}
```

**Impact**: This fix is critical for the feature to work with natural C# syntax. Without it, users cannot use method calls like `Add()`, `Delete()`, `Remove()`, or functions like `IfNotExists()` with any arguments.

**Testing**: The failing tests in `WithUpdateExpressionExtensionsTests.cs` will pass once this fix is implemented.

## Future Enhancements

1. Support for nested property updates (e.g., `x.Address.City`)
2. Support for list indexing (e.g., `x.Items[0]`)
3. Support for map attribute access (e.g., `x.Metadata["key"]`)
4. Additional DynamoDB functions as needed
5. Performance optimizations for complex expressions


## Limitations Discovered During Integration Testing

### 1. Nullable Type Support for Extension Methods

**Problem**: The extension methods in `UpdateExpressionPropertyExtensions` are defined for non-nullable types like `UpdateExpressionProperty<HashSet<T>>`, but the source generator creates nullable properties for nullable entity properties (e.g., `UpdateExpressionProperty<HashSet<int>?>`). This causes compilation errors when trying to use methods like `Add()`, `Delete()`, `Remove()`, `IfNotExists()`, `ListAppend()`, and `ListPrepend()` on nullable properties.

**Example**:
```csharp
// Entity definition
public class ComplexEntity
{
    public HashSet<int>? CategoryIds { get; set; }  // Nullable
}

// Generated UpdateExpressions class
public class ComplexEntityUpdateExpressions
{
    public UpdateExpressionProperty<HashSet<int>?> CategoryIds { get; } = new();  // Nullable wrapper
}

// User code - FAILS TO COMPILE
builder.Set<ComplexEntity, ComplexEntityUpdateExpressions, ComplexEntityUpdateModel>(
    x => new ComplexEntityUpdateModel
    {
        CategoryIds = x.CategoryIds.Add(4, 5)  // ❌ Add() not available on nullable type
    }
)
```

**Root Cause**: C# extension method resolution requires exact type matches. The extension method `Add<T>(this UpdateExpressionProperty<HashSet<T>> property, params T[] elements)` does not match `UpdateExpressionProperty<HashSet<int>?>` because `HashSet<int>?` is not the same as `HashSet<int>`.

**Impact**: 
- ADD operations on sets cannot be used
- DELETE operations on sets cannot be used
- REMOVE operations cannot be used
- DynamoDB functions (IfNotExists, ListAppend, ListPrepend) cannot be used
- Only simple SET operations with direct value assignments work

**Solution**: Add nullable overloads for all extension methods that need to support nullable types.

**Implementation Approach**:
```csharp
// Add nullable overloads for numeric types
public static int? Add(this UpdateExpressionProperty<int?> property, int value)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

public static long? Add(this UpdateExpressionProperty<long?> property, long value)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

public static decimal? Add(this UpdateExpressionProperty<decimal?> property, decimal value)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

public static double? Add(this UpdateExpressionProperty<double?> property, double value)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

// Add nullable overloads for set operations
public static HashSet<T>? Add<T>(this UpdateExpressionProperty<HashSet<T>?> property, params T[] elements)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

public static HashSet<T>? Delete<T>(this UpdateExpressionProperty<HashSet<T>?> property, params T[] elements)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

// Add nullable overloads for list operations
public static List<T>? ListAppend<T>(this UpdateExpressionProperty<List<T>?> property, params T[] elements)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

public static List<T>? ListPrepend<T>(this UpdateExpressionProperty<List<T>?> property, params T[] elements)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

// Add nullable overload for IfNotExists
public static T? IfNotExists<T>(this UpdateExpressionProperty<T?> property, T? defaultValue)
    => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

// Note: Remove() already works with nullable types because T can be nullable
```

**Testing**: Once implemented, the commented-out integration tests in `ExpressionBasedUpdateTests.cs` should be uncommented and should pass.

### 2. Format String Application Not Implemented

**Problem**: The `UpdateExpressionTranslator` does not currently apply format strings defined in entity metadata when translating expressions. This means that properties with format specifications (e.g., `[DynamoDbAttribute("created_date", Format = "yyyy-MM-dd")]`) are not formatted correctly when used in update expressions.

**Example**:
```csharp
// Entity definition
public class FormattedEntity
{
    [DynamoDbAttribute("created_date", Format = "yyyy-MM-dd")]
    public DateTime? CreatedDate { get; set; }
    
    [DynamoDbAttribute("amount", Format = "F2")]
    public decimal? Amount { get; set; }
}

// User code
builder.Set<FormattedEntity, FormattedEntityUpdateExpressions, FormattedEntityUpdateModel>(
    x => new FormattedEntityUpdateModel
    {
        CreatedDate = new DateTime(2024, 3, 15),  // Should format as "2024-03-15"
        Amount = 123.456m  // Should format as "123.46"
    }
)

// Current behavior: Values are not formatted
// Expected behavior: Values should be formatted according to metadata
```

**Root Cause**: The `UpdateExpressionTranslator.TranslateSimpleSet()` method converts values to `AttributeValue` objects but does not check for or apply format strings from the property metadata.

**Impact**: 
- DateTime values are not formatted correctly
- Numeric values are not formatted with specified precision
- Integer values are not zero-padded as specified
- Format strings work in PutItem/GetItem but not in UpdateItem expressions

**Solution**: Enhance `UpdateExpressionTranslator` to apply format strings when converting values to `AttributeValue` objects.

**Implementation Approach**:
```csharp
private Operation TranslateSimpleSet(
    Expression valueExpression,
    string propertyName,
    ExpressionContext context)
{
    // Get property metadata
    var propertyMetadata = context.Metadata.Properties
        .FirstOrDefault(p => p.PropertyName == propertyName);
    
    if (propertyMetadata == null)
    {
        throw new UnmappedPropertyException(
            $"Property '{propertyName}' is not mapped to a DynamoDB attribute");
    }
    
    // Validate not updating key properties
    if (propertyMetadata.IsPartitionKey || propertyMetadata.IsSortKey)
    {
        throw new InvalidUpdateOperationException(
            $"Cannot update key property '{propertyName}'");
    }
    
    // Evaluate the value
    var value = EvaluateExpression(valueExpression);
    
    // Apply format string if specified
    if (!string.IsNullOrEmpty(propertyMetadata.Format) && value != null)
    {
        value = ApplyFormatString(value, propertyMetadata.Format, propertyMetadata.PropertyType);
    }
    
    // Convert to AttributeValue
    var attributeValue = context.AttributeValueHelper.ToAttributeValue(value);
    
    // Generate parameter name
    var paramName = context.AttributeValueHelper.AddValue(attributeValue);
    
    // Generate attribute name
    var attrName = context.AttributeNameHelper.GetOrAdd(propertyMetadata.AttributeName);
    
    return new Operation
    {
        Type = OperationType.Set,
        Expression = $"{attrName} = {paramName}"
    };
}

private object ApplyFormatString(object value, string format, Type propertyType)
{
    // Handle DateTime formatting
    if (value is DateTime dateTime)
    {
        return dateTime.ToString(format, CultureInfo.InvariantCulture);
    }
    
    // Handle numeric formatting
    if (value is IFormattable formattable)
    {
        return formattable.ToString(format, CultureInfo.InvariantCulture);
    }
    
    return value;
}
```

**Testing**: Integration tests for format string application should be added once this is implemented.

### 3. Field-Level Encryption Not Implemented

**Problem**: The `UpdateExpressionTranslator` does not currently support field-level encryption for properties marked with `[Encrypted]` attribute. While the translator accepts an `IFieldEncryptor` parameter, it does not use it to encrypt values before creating `AttributeValue` objects.

**Example**:
```csharp
// Entity definition
public class SecureEntity
{
    [Encrypted]
    [DynamoDbAttribute("ssn")]
    public string? SocialSecurityNumber { get; set; }
}

// User code
builder.Set<SecureEntity, SecureEntityUpdateExpressions, SecureEntityUpdateModel>(
    x => new SecureEntityUpdateModel
    {
        SocialSecurityNumber = "123-45-6789"  // Should be encrypted
    }
)

// Current behavior: Value is stored in plaintext
// Expected behavior: Value should be encrypted before storage
```

**Root Cause**: The `UpdateExpressionTranslator` does not check for encryption attributes or call the field encryptor when processing values.

**Impact**: 
- Sensitive data is not encrypted in update operations
- Security vulnerability for applications using field-level encryption
- Inconsistent behavior between PutItem (which encrypts) and UpdateItem (which doesn't)

**Solution**: Enhance `UpdateExpressionTranslator` to detect encrypted properties and encrypt values before creating `AttributeValue` objects.

**Implementation Approach**:
```csharp
private Operation TranslateSimpleSet(
    Expression valueExpression,
    string propertyName,
    ExpressionContext context)
{
    // Get property metadata
    var propertyMetadata = context.Metadata.Properties
        .FirstOrDefault(p => p.PropertyName == propertyName);
    
    if (propertyMetadata == null)
    {
        throw new UnmappedPropertyException(
            $"Property '{propertyName}' is not mapped to a DynamoDB attribute");
    }
    
    // Validate not updating key properties
    if (propertyMetadata.IsPartitionKey || propertyMetadata.IsSortKey)
    {
        throw new InvalidUpdateOperationException(
            $"Cannot update key property '{propertyName}'");
    }
    
    // Evaluate the value
    var value = EvaluateExpression(valueExpression);
    
    // Apply format string if specified
    if (!string.IsNullOrEmpty(propertyMetadata.Format) && value != null)
    {
        value = ApplyFormatString(value, propertyMetadata.Format, propertyMetadata.PropertyType);
    }
    
    // Encrypt if needed
    if (propertyMetadata.IsEncrypted && value != null)
    {
        if (_fieldEncryptor == null)
        {
            throw new EncryptionRequiredException(
                $"Property '{propertyName}' is marked as encrypted but no field encryptor is configured");
        }
        
        // Encrypt the value
        var stringValue = value.ToString() ?? string.Empty;
        var encryptedValue = await _fieldEncryptor.EncryptAsync(
            stringValue,
            propertyMetadata.AttributeName,
            _encryptionContextId);
        
        value = encryptedValue;
    }
    
    // Convert to AttributeValue
    var attributeValue = context.AttributeValueHelper.ToAttributeValue(value);
    
    // Generate parameter name
    var paramName = context.AttributeValueHelper.AddValue(attributeValue);
    
    // Generate attribute name
    var attrName = context.AttributeNameHelper.GetOrAdd(propertyMetadata.AttributeName);
    
    return new Operation
    {
        Type = OperationType.Set,
        Expression = $"{attrName} = {paramName}"
    };
}
```

**Challenge**: The `IFieldEncryptor.EncryptAsync()` method is asynchronous, but expression translation happens synchronously. This requires either:
1. Making the translator async (breaking change)
2. Using synchronous encryption (performance impact)
3. Deferring encryption to the request builder (architectural change)

**Recommended Approach**: Defer encryption to the request builder level, similar to how it's done for PutItem operations. The translator should mark which values need encryption, and the builder should encrypt them before sending the request.

**Testing**: Integration tests for encryption should be added once this is implemented.

### 4. Mixing String-Based and Expression-Based Methods

**Problem**: When using both string-based `Set()` and expression-based `Set()` methods on the same builder, there are conflicts in attribute name generation that cause DynamoDB to reject the request with "Value provided in ExpressionAttributeNames unused in expressions" errors.

**Example**:
```csharp
// User code - FAILS
builder
    .Set<User, UserUpdateExpressions, UserUpdateModel>(
        x => new UserUpdateModel { Name = "John" }
    )
    .Set("SET description = :desc")
    .WithValue(":desc", "New Description")
    .UpdateAsync();

// Error: Value provided in ExpressionAttributeNames unused in expressions: keys: {#attr0}
```

**Root Cause**: The expression-based `Set()` method generates attribute names (e.g., `#attr0`, `#attr1`) and adds them to the builder's attribute name collection. When the string-based `Set()` is called afterward, it may generate its own attribute names that conflict or are unused, causing DynamoDB to reject the request.

**Impact**: 
- Cannot mix string-based and expression-based methods in the same builder
- Limits flexibility for complex update scenarios
- Forces users to choose one approach or the other

**Solution**: Improve attribute name generation coordination between string-based and expression-based methods.

**Implementation Approaches**:

**Option 1: Merge Update Expressions**
- Store update expressions separately for string-based and expression-based methods
- Merge them intelligently when building the final request
- Deduplicate attribute names and values

**Option 2: Use Consistent Naming**
- Ensure both methods use the same attribute name helper
- Coordinate parameter naming to avoid conflicts
- Parse string-based expressions to extract attribute names

**Option 3: Document as Limitation**
- Document that mixing approaches is not supported
- Provide clear error messages when conflicts are detected
- Recommend using one approach consistently

**Recommended Approach**: Option 1 (Merge Update Expressions) provides the best user experience but requires significant refactoring. Option 3 (Document as Limitation) is the pragmatic short-term solution.

**Testing**: Integration tests should verify that either mixing works correctly or clear error messages are provided.

### 5. Arithmetic Operations Not Implemented

**Problem**: The `UpdateExpressionTranslator` does not currently support arithmetic operations (addition and subtraction) in SET clauses. While the design document mentions this feature, it has not been implemented yet.

**Example**:
```csharp
// User code - NOT YET SUPPORTED
builder.Set<User, UserUpdateExpressions, UserUpdateModel>(
    x => new UserUpdateModel
    {
        Score = x.Score + 10,  // Arithmetic in SET
        Balance = x.Balance - 5.50m
    }
)

// Should translate to: SET #score = #score + :p0, #balance = #balance - :p1
```

**Root Cause**: The `ClassifyOperation()` method in `UpdateExpressionTranslator` has a case for `BinaryExpression` but the `TranslateBinaryOperation()` method is not implemented.

**Impact**: 
- Cannot use arithmetic operations in SET clauses
- Must use ADD operation for increments (which has different semantics)
- Less intuitive API for numeric updates

**Solution**: Implement `TranslateBinaryOperation()` method to handle arithmetic expressions.

**Implementation Approach**:
```csharp
private Operation TranslateBinaryOperation(
    BinaryExpression binary,
    ParameterExpression parameter,
    string propertyName,
    ExpressionContext context)
{
    // Only support addition and subtraction
    if (binary.NodeType != ExpressionType.Add && binary.NodeType != ExpressionType.Subtract)
    {
        throw new UnsupportedExpressionException(
            $"Binary operation {binary.NodeType} is not supported in update expressions. " +
            "Only addition (+) and subtraction (-) are supported.",
            binary);
    }
    
    // Get property metadata
    var propertyMetadata = context.Metadata.Properties
        .FirstOrDefault(p => p.PropertyName == propertyName);
    
    if (propertyMetadata == null)
    {
        throw new UnmappedPropertyException(
            $"Property '{propertyName}' is not mapped to a DynamoDB attribute");
    }
    
    // Left side should be property access (x.Score)
    // Right side should be constant or variable
    
    string leftOperand;
    if (binary.Left is MemberExpression leftMember && 
        leftMember.Expression == parameter)
    {
        // Reference to the property itself (x.Score)
        var attrName = context.AttributeNameHelper.GetOrAdd(propertyMetadata.AttributeName);
        leftOperand = attrName;
    }
    else
    {
        // Evaluate as constant
        var leftValue = EvaluateExpression(binary.Left);
        var leftAttrValue = context.AttributeValueHelper.ToAttributeValue(leftValue);
        leftOperand = context.AttributeValueHelper.AddValue(leftAttrValue);
    }
    
    // Right side - evaluate as constant
    var rightValue = EvaluateExpression(binary.Right);
    var rightAttrValue = context.AttributeValueHelper.ToAttributeValue(rightValue);
    var rightOperand = context.AttributeValueHelper.AddValue(rightAttrValue);
    
    // Build expression
    var attrName = context.AttributeNameHelper.GetOrAdd(propertyMetadata.AttributeName);
    var op = binary.NodeType == ExpressionType.Add ? "+" : "-";
    
    return new Operation
    {
        Type = OperationType.Set,
        Expression = $"{attrName} = {leftOperand} {op} {rightOperand}"
    };
}
```

**Testing**: Integration tests for arithmetic operations should be added once this is implemented.

## Summary of Limitations and Priority

| Limitation | Impact | Priority | Effort |
|-----------|--------|----------|--------|
| Nullable type support | High - Blocks most advanced operations | **Critical** | Medium |
| Format string application | Medium - Affects data consistency | High | Low |
| Field-level encryption | High - Security vulnerability | High | Medium |
| Mixing string/expression methods | Low - Workaround available | Low | High |
| Arithmetic operations | Medium - Less intuitive API | Medium | Low |

**Recommended Implementation Order**:
1. Nullable type support (unblocks most features)
2. Format string application (quick win)
3. Arithmetic operations (quick win)
4. Field-level encryption (requires architectural decision)
5. Mixing methods (nice-to-have, complex implementation)
