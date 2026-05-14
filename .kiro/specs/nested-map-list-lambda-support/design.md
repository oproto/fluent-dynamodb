# Design Document

## Overview

This design extends FluentDynamoDb's lambda expression support to handle nested map properties, list indexing, and collection operations. The implementation builds on the existing `ExpressionTranslator` and `UpdateExpressionTranslator` infrastructure, adding document path building for nested access patterns.

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    Query/Filter Expressions                      │
│  .Where(x => x.Address.City == "Seattle")                       │
│  .WithFilter(x => x.Tags[0] == "featured")                      │
└────────────────────┬────────────────────────────────────────────┘
                     │
          ┌──────────▼──────────┐
          │ ExpressionTranslator │
          │ (Enhanced)           │
          │ - VisitMember        │◄── Chained property access
          │ - VisitIndex         │◄── NEW: List index access
          │ - BuildDocumentPath  │◄── NEW: Path builder
          └──────────┬──────────┘
                     │
┌────────────────────┼────────────────────────────────────────────┐
│                    │    Update Expressions                       │
│  .Set(x => new Update { Address = new { City = "Portland" } })  │
│  .Set(x => x.Tags.Append("new"))                                │
└────────────────────┬────────────────────────────────────────────┘
                     │
          ┌──────────▼──────────┐
          │ UpdateExpression    │
          │ Translator          │
          │ (Enhanced)          │
          │ - Nested MemberInit │◄── Nested update models
          │ - List operations   │◄── Append/Prepend/Remove
          │ - Set operations    │◄── Add/Delete
          └──────────┬──────────┘
                     │
          ┌──────────▼──────────┐
          │ Source Generator    │
          │ (Enhanced)          │
          │ - Nested UpdateModel│◄── Generate for [DynamoDbEntity]
          │ - Property metadata │◄── Include nested type info
          └─────────────────────┘
```

### Data Flow - Nested Filter Expression

```
Developer writes:
  .WithFilter(x => x.Address.City == "Seattle")

Note: Nested property access is only valid in filter expressions
and condition expressions, NOT in key condition expressions.
Key conditions only support partition key and sort key attributes.

↓

ExpressionTranslator.VisitMember detects chained access:
  MemberExpression {
    Member = "City",
    Expression = MemberExpression {
      Member = "Address",
      Expression = ParameterExpression { x }
    }
  }

↓

BuildDocumentPath traverses chain:
  ["Address", "City"] → "#address.#city"

↓

Register attribute names:
  #address → "address"
  #city → "city"

↓

Generate expression:
  "#address.#city = :v0"
```

### Data Flow - List Index Access (Filter/Condition Only)

```
Developer writes:
  .WithFilter(x => x.Tags[0] == "featured")

Note: List index access is only valid in filter expressions
and condition expressions, NOT in key condition expressions.

↓

ExpressionTranslator.VisitIndex detects indexer:
  IndexExpression {
    Object = MemberExpression { x.Tags },
    Arguments = [ ConstantExpression { 0 } ]
  }

↓

BuildDocumentPath with index:
  ["Tags", "[0]"] → "#tags[0]"

↓

Register attribute names:
  #tags → "tags"

↓

Generate expression:
  "#tags[0] = :v0"
```

### Data Flow - Nested Update

```
Developer writes:
  .Set(x => new CustomerUpdateModel 
  { 
      ShippingAddress = new AddressUpdateModel { City = "Portland" } 
  })

↓

UpdateExpressionTranslator detects nested MemberInitExpression:
  MemberAssignment {
    Member = "ShippingAddress",
    Expression = MemberInitExpression {
      Bindings = [
        MemberAssignment { Member = "City", Expression = "Portland" }
      ]
    }
  }

↓

Recursively process nested initializer with path prefix:
  Path: ["ShippingAddress"]
  → Process City assignment with path ["ShippingAddress", "City"]

↓

Generate SET expression:
  "SET #shippingAddress.#city = :v0"
```

## Components and Interfaces

### 1. DocumentPathBuilder (New)

```csharp
namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Builds DynamoDB document paths from expression member chains.
/// Handles nested properties and list indices.
/// </summary>
internal class DocumentPathBuilder
{
    private readonly IAttributeNameHelper _attributeNames;
    private readonly List<string> _pathSegments = new();

    public DocumentPathBuilder(IAttributeNameHelper attributeNames)
    {
        _attributeNames = attributeNames;
    }

    /// <summary>
    /// Adds a property segment to the path.
    /// </summary>
    public void AddProperty(string propertyName, string? attributeName = null)
    {
        var attrName = attributeName ?? propertyName.ToLowerInvariant();
        var placeholder = _attributeNames.GetOrAdd(attrName);
        _pathSegments.Add(placeholder);
    }

    /// <summary>
    /// Adds a list index segment to the path.
    /// </summary>
    public void AddIndex(int index)
    {
        _pathSegments.Add($"[{index}]");
    }

    /// <summary>
    /// Builds the complete document path string.
    /// </summary>
    public string Build()
    {
        var result = new StringBuilder();
        for (int i = 0; i < _pathSegments.Count; i++)
        {
            var segment = _pathSegments[i];
            if (segment.StartsWith("["))
            {
                // Index - append directly without dot
                result.Append(segment);
            }
            else
            {
                // Property - add dot separator if not first
                if (i > 0 && !_pathSegments[i - 1].StartsWith("["))
                    result.Append('.');
                else if (i > 0)
                    result.Append('.');
                result.Append(segment);
            }
        }
        return result.ToString();
    }
}
```

### 2. ExpressionTranslator Enhancements

```csharp
// Enhanced VisitMember to handle chained property access
protected override Expression VisitMember(MemberExpression node)
{
    // Check if this is a chained property access (nested)
    if (IsNestedPropertyAccess(node))
    {
        var path = BuildDocumentPathFromMemberChain(node);
        _builder.Append(path);
        return node;
    }
    
    // Existing single-level property handling
    return base.VisitMember(node);
}

// New method to detect nested access
private bool IsNestedPropertyAccess(MemberExpression node)
{
    // Walk up the expression tree
    var current = node.Expression;
    while (current != null)
    {
        if (current is MemberExpression member)
        {
            // Check if the member's type has [DynamoDbMap] or [DynamoDbEntity]
            if (IsMapType(member.Type))
                return true;
            current = member.Expression;
        }
        else if (current is ParameterExpression)
        {
            return false; // Reached the root parameter
        }
        else
        {
            break;
        }
    }
    return false;
}

// New method to build document path from member chain
private string BuildDocumentPathFromMemberChain(MemberExpression node)
{
    var pathBuilder = new DocumentPathBuilder(_attributeNames);
    var segments = new Stack<(string PropertyName, string? AttributeName)>();
    
    // Collect all segments from leaf to root
    Expression? current = node;
    while (current is MemberExpression member)
    {
        var attrName = GetDynamoDbAttributeName(member.Member);
        segments.Push((member.Member.Name, attrName));
        current = member.Expression;
    }
    
    // Build path from root to leaf
    while (segments.Count > 0)
    {
        var (propName, attrName) = segments.Pop();
        pathBuilder.AddProperty(propName, attrName);
    }
    
    return pathBuilder.Build();
}
```

### 3. New VisitIndex Method

```csharp
// New method to handle list/array index access
protected override Expression VisitIndex(IndexExpression node)
{
    // Build path for the collection
    if (node.Object is MemberExpression memberExpr)
    {
        var pathBuilder = new DocumentPathBuilder(_attributeNames);
        BuildPathFromMember(memberExpr, pathBuilder);
        
        // Add the index
        if (node.Arguments[0] is ConstantExpression indexConst && 
            indexConst.Value is int index)
        {
            pathBuilder.AddIndex(index);
        }
        else
        {
            throw new UnsupportedExpressionException(
                "List index must be a constant integer",
                node);
        }
        
        _builder.Append(pathBuilder.Build());
    }
    
    return node;
}

// Also handle array access via BinaryExpression (ArrayIndex)
protected override Expression VisitBinary(BinaryExpression node)
{
    if (node.NodeType == ExpressionType.ArrayIndex)
    {
        return HandleArrayIndex(node);
    }
    return base.VisitBinary(node);
}
```

### 4. UpdateExpressionTranslator Enhancements

```csharp
// Enhanced to handle nested MemberInitExpression
private void ProcessMemberAssignment(
    MemberAssignment assignment,
    string[] pathPrefix,
    ExpressionContext context)
{
    var propertyName = assignment.Member.Name;
    var currentPath = pathPrefix.Append(propertyName).ToArray();
    
    // Check if value is a nested MemberInitExpression
    if (assignment.Expression is MemberInitExpression nestedInit)
    {
        // Recursively process nested initializer
        foreach (var binding in nestedInit.Bindings)
        {
            if (binding is MemberAssignment nestedAssignment)
            {
                ProcessMemberAssignment(nestedAssignment, currentPath, context);
            }
        }
    }
    else
    {
        // Simple value assignment - generate SET clause
        var path = BuildDocumentPath(currentPath, context);
        var value = EvaluateExpression(assignment.Expression);
        var placeholder = context.AddValue(value);
        
        _setOperations.Add($"{path} = {placeholder}");
    }
}

private string BuildDocumentPath(string[] segments, ExpressionContext context)
{
    var parts = new List<string>();
    foreach (var segment in segments)
    {
        var attrName = GetAttributeNameForProperty(segment);
        var placeholder = context.AttributeNames.GetOrAdd(attrName);
        parts.Add(placeholder);
    }
    return string.Join(".", parts);
}
```

### 5. List Operation Extension Methods

```csharp
namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Extension methods for list operations in update expressions.
/// These are marker methods for expression translation.
/// </summary>
public static class ListOperationExtensions
{
    /// <summary>
    /// Appends an element to the end of a list.
    /// Translates to: SET #attr = list_append(#attr, :val)
    /// </summary>
    [ExpressionOnly]
    public static List<T> Append<T>(this List<T> list, T item)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions.");

    /// <summary>
    /// Prepends an element to the beginning of a list.
    /// Translates to: SET #attr = list_append(:val, #attr)
    /// </summary>
    [ExpressionOnly]
    public static List<T> Prepend<T>(this List<T> list, T item)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions.");

    /// <summary>
    /// Appends multiple elements to the end of a list.
    /// Translates to: SET #attr = list_append(#attr, :val)
    /// </summary>
    [ExpressionOnly]
    public static List<T> AppendRange<T>(this List<T> list, IEnumerable<T> items)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions.");

    /// <summary>
    /// Prepends multiple elements to the beginning of a list.
    /// Translates to: SET #attr = list_append(:val, #attr)
    /// </summary>
    [ExpressionOnly]
    public static List<T> PrependRange<T>(this List<T> list, IEnumerable<T> items)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions.");
}
```

### 6. Source Generator - Nested UpdateModel Generation

```csharp
// In EntityGenerator.cs - Generate nested update models

private void GenerateNestedUpdateModels(
    EntityInfo entity,
    SourceProductionContext context)
{
    foreach (var property in entity.Properties)
    {
        if (property.HasDynamoDbMapAttribute && 
            property.Type is INamedTypeSymbol namedType)
        {
            // Check if the nested type has [DynamoDbEntity]
            if (HasDynamoDbEntityAttribute(namedType))
            {
                GenerateUpdateModelForType(namedType, context);
            }
        }
    }
}

private void GenerateUpdateModelForType(
    INamedTypeSymbol type,
    SourceProductionContext context)
{
    var updateModelName = $"{type.Name}UpdateModel";
    var source = new StringBuilder();
    
    source.AppendLine($"namespace {type.ContainingNamespace};");
    source.AppendLine();
    source.AppendLine($"public partial class {updateModelName}");
    source.AppendLine("{");
    
    foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
    {
        if (member.DeclaredAccessibility == Accessibility.Public &&
            member.GetMethod != null && member.SetMethod != null)
        {
            var propertyType = GetNullableTypeName(member.Type);
            source.AppendLine($"    public {propertyType}? {member.Name} {{ get; set; }}");
        }
    }
    
    source.AppendLine("}");
    
    context.AddSource($"{updateModelName}.g.cs", source.ToString());
}
```

## Data Models

### PropertyPathInfo

```csharp
/// <summary>
/// Represents a path to a property, including nested paths.
/// </summary>
internal class PropertyPathInfo
{
    public string[] Segments { get; }
    public string DynamoDbPath { get; }
    public Type PropertyType { get; }
    public bool IsListIndex { get; }
    public int? ListIndex { get; }
}
```

### NestedPropertyMetadata

```csharp
/// <summary>
/// Metadata for nested properties in entity metadata.
/// </summary>
public class NestedPropertyMetadata
{
    public string PropertyName { get; }
    public string AttributeName { get; }
    public Type NestedType { get; }
    public IReadOnlyDictionary<string, PropertyMetadata> Properties { get; }
}
```

## Error Handling

### New Exception Types

1. **NestedPropertyAccessException**: Thrown when nested property access fails
2. **InvalidListIndexException**: Thrown when list index is invalid (negative, non-constant)
3. **UnsupportedNestedOperationException**: Thrown for unsupported nested operations

### Validation Rules

1. Nested property types must have `[DynamoDbEntity]` or `[DynamoDbMap]` attribute
2. List indices must be non-negative constant integers
3. Cannot update key properties at any nesting level
4. Nested update models must match entity structure

## Testing Strategy

### Unit Tests

1. **DocumentPathBuilder Tests**
   - Single property path
   - Nested property path (2 levels)
   - Deep nested path (3+ levels)
   - Path with list index
   - Mixed property and index path

2. **ExpressionTranslator Nested Tests** (Filter/Condition expressions only)
   - `x => x.Address.City == "Seattle"` (in WithFilter)
   - `x => x.Address.Country.Code == "US"` (in WithFilter)
   - `x => x.Tags[0] == "featured"` (in WithFilter)
   - `x => x.LineItems[0].ProductId == "123"` (in WithFilter)
   - `x => x.Metadata.Tags[0] == "sale"` (in WithFilter)
   - Condition expressions on Put/Update/Delete

3. **UpdateExpressionTranslator Nested Tests**
   - Single nested property update
   - Multiple nested property updates
   - Multi-level nested updates
   - Mixed top-level and nested updates
   - List append/prepend operations
   - Set add/delete operations

4. **Source Generator Tests**
   - Nested UpdateModel generation
   - UpdateModel for multi-level nesting
   - Property type handling in nested models

### Integration Tests

1. **Filter with nested property** (not key condition - nested only valid in filters)
2. **Condition expression with nested property** (Put/Update/Delete)
3. **Update nested property**
4. **List append operation**
5. **List prepend operation**
6. **List element update by index**
7. **Set add operation**
8. **Set delete operation**
9. **Combined nested and list operations**

## Performance Considerations

- Document path building is O(n) where n is nesting depth
- No additional allocations for simple (non-nested) expressions
- Nested update model generation happens at compile time
- Expression tree traversal is single-pass

## Backward Compatibility

- All existing expression patterns continue to work
- No changes to public API signatures
- New functionality is additive only
- Existing tests pass without modification

---

## Implementation Tasks

### Task 1: DocumentPathBuilder Implementation
- [ ] Create `DocumentPathBuilder` class
- [ ] Implement `AddProperty` method
- [ ] Implement `AddIndex` method
- [ ] Implement `Build` method
- [ ] Add unit tests

### Task 2: ExpressionTranslator Nested Property Support
- [ ] Add `IsNestedPropertyAccess` detection
- [ ] Implement `BuildDocumentPathFromMemberChain`
- [ ] Enhance `VisitMember` for chained access
- [ ] Add unit tests for nested queries

### Task 3: ExpressionTranslator List Index Support
- [ ] Implement `VisitIndex` method
- [ ] Handle `ArrayIndex` in `VisitBinary`
- [ ] Add unit tests for list index queries

### Task 4: UpdateExpressionTranslator Nested Updates
- [ ] Implement recursive `ProcessMemberAssignment`
- [ ] Add path prefix tracking
- [ ] Generate correct SET expressions for nested paths
- [ ] Add unit tests

### Task 5: List Operation Extension Methods
- [ ] Create `ListOperationExtensions` class
- [ ] Implement `Append`, `Prepend`, `AppendRange`, `PrependRange`
- [ ] Add `[ExpressionOnly]` attribute
- [ ] Update translator to recognize these methods

### Task 6: Set Operation Builder Methods
- [ ] Add `Add<T>` method to UpdateItemRequestBuilder
- [ ] Add `Delete<T>` method to UpdateItemRequestBuilder
- [ ] Implement ADD/DELETE expression generation
- [ ] Add unit tests

### Task 7: Source Generator - Nested UpdateModel
- [ ] Detect `[DynamoDbMap]` properties
- [ ] Generate `*UpdateModel` for nested types
- [ ] Handle multi-level nesting
- [ ] Add source generator tests

### Task 8: Integration Tests
- [ ] Nested query filter tests
- [ ] Nested update tests
- [ ] List operation tests
- [ ] Set operation tests

### Task 9: Documentation
- [ ] Update `fluentdynamodb.md` steering document
- [ ] Update `CHANGELOG.md`
- [ ] Create/update `docs/maps-and-lists.md`
- [ ] Update `docs/DOCUMENTATION_CHANGELOG.md`
