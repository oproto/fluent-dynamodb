# Design Document

## Overview

This design reimagines the source generator's table class generation to support single-table design patterns. Instead of generating one table class per entity, the generator will consolidate entities sharing the same table name into a single table class with entity-specific accessors.

**Key Design Goals:**
1. Generate one table class per unique table name (not per entity)
2. Support default entity selection for table-level operations
3. Provide entity-specific accessor properties (e.g., `table.Orders`, `table.OrderLines`)
4. Allow fine-grained control over accessor generation and visibility
5. Maintain transaction/batch operations at table level only

## Architecture

### High-Level Component Changes

```
┌─────────────────────────────────────────────────────────────┐
│           DynamoDbSourceGenerator (Enhanced)                │
│  - Group entities by TableName                              │
│  - Validate default entity selection                        │
│  - Generate consolidated table classes                      │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              EntityAnalyzer (Enhanced)                      │
│  - Extract IsDefault from [DynamoDbTable]                   │
│  - Extract [GenerateEntityProperty] configuration           │
│  - Extract [GenerateAccessors] configuration                │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│            TableGenerator (New Architecture)                │
│  - Generate table class with multiple entity support        │
│  - Generate entity accessor properties                      │
│  - Generate entity-specific operation methods               │
│  - Generate table-level operations for default entity       │
└─────────────────────────────────────────────────────────────┘
```

### Generated Code Structure

```
// Before (one entity per table)
public partial class OrderTable : DynamoDbTableBase
{
    public GetItemRequestBuilder<Order> Get() { }
    public QueryRequestBuilder<Order> Query() { }
}

public partial class OrderLineTable : DynamoDbTableBase
{
    public GetItemRequestBuilder<OrderLine> Get() { }
    public QueryRequestBuilder<OrderLine> Query() { }
}

// After (multi-entity table)
public partial class MyAppTable : DynamoDbTableBase
{
    // Table-level operations use default entity (Order)
    public GetItemRequestBuilder<Order> Get() { }
    public QueryRequestBuilder<Order> Query() { }
    
    // Entity-specific accessors
    public OrderAccessor Orders { get; }
    public OrderLineAccessor OrderLines { get; }
    
    // Transaction/batch operations at table level
    public TransactWriteItemsRequestBuilder TransactWrite() { }
    public BatchWriteItemBuilder BatchWrite() { }
    
    // Nested accessor classes
    public class OrderAccessor
    {
        public GetItemRequestBuilder<Order> Get() { }
        public QueryRequestBuilder<Order> Query() { }
        public PutItemRequestBuilder<Order> Put(Order item) { }
        // ... other operations
    }
    
    public class OrderLineAccessor
    {
        public GetItemRequestBuilder<OrderLine> Get() { }
        public QueryRequestBuilder<OrderLine> Query() { }
        // ... other operations
    }
}
```

## Components and Interfaces

### 1. Enhanced Attributes

#### DynamoDbTableAttribute Enhancement

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class DynamoDbTableAttribute : Attribute
{
    public string TableName { get; set; }
    
    // NEW: Mark entity as default for table-level operations
    public bool IsDefault { get; set; } = false;
    
    // Existing properties...
}

// Usage
[DynamoDbTable(TableName = "MyApp", IsDefault = true)]
public class Order { }

[DynamoDbTable(TableName = "MyApp")]
public class OrderLine { }
```

#### GenerateEntityPropertyAttribute (New)

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class GenerateEntityPropertyAttribute : Attribute
{
    // Custom name for the entity accessor property
    public string? Name { get; set; }
    
    // Whether to generate the accessor property
    public bool Generate { get; set; } = true;
    
    // Visibility modifier for the accessor property
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}

public enum AccessModifier
{
    Public,
    Internal,
    Protected,
    Private
}

// Usage
[DynamoDbTable(TableName = "MyApp")]
[GenerateEntityProperty(Name = "CustomOrders", Modifier = AccessModifier.Internal)]
public class Order { }

[DynamoDbTable(TableName = "MyApp")]
[GenerateEntityProperty(Generate = false)] // Don't generate accessor
public class InternalEntity { }
```

#### GenerateAccessorsAttribute (New)

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class GenerateAccessorsAttribute : Attribute
{
    // Which operations to configure
    public TableOperation Operations { get; set; } = TableOperation.All;
    
    // Whether to generate the operations
    public bool Generate { get; set; } = true;
    
    // Visibility modifier for the operations
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}

[Flags]
public enum TableOperation
{
    Get = 1,
    Query = 2,
    Scan = 4,
    Put = 8,
    Delete = 16,
    Update = 32,
    All = Get | Query | Scan | Put | Delete | Update
}

// Usage examples
[DynamoDbTable(TableName = "MyApp")]
[GenerateAccessors(Operations = TableOperation.Get | TableOperation.Query)]
public class Order { }

[DynamoDbTable(TableName = "MyApp")]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
[GenerateAccessors(Operations = TableOperation.Query, Modifier = AccessModifier.Public)]
public class OrderLine { }

[DynamoDbTable(TableName = "MyApp")]
[GenerateAccessors(Operations = TableOperation.Delete, Generate = false)]
public class ReadOnlyEntity { }
```

### 2. Source Generator Changes

#### Entity Grouping by Table Name

```csharp
public class DynamoDbSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all entities
        var entities = context.SyntaxProvider
            .CreateSyntaxProvider(/* ... */)
            .Select((ctx, ct) => AnalyzeEntity(ctx, ct));
        
        // Group entities by table name
        var tableGroups = entities
            .Collect()
            .Select((entities, ct) => GroupEntitiesByTable(entities));
        
        // Generate one table class per group
        context.RegisterSourceOutput(tableGroups, GenerateTableClasses);
    }
    
    private Dictionary<string, List<EntityModel>> GroupEntitiesByTable(
        ImmutableArray<EntityModel> entities)
    {
        return entities
            .GroupBy(e => e.TableName)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
```

#### Default Entity Validation

```csharp
private void ValidateDefaultEntity(List<EntityModel> entities, SourceProductionContext context)
{
    var defaultEntities = entities.Where(e => e.IsDefault).ToList();
    
    if (entities.Count > 1 && defaultEntities.Count == 0)
    {
        // Error: Multiple entities, no default
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.NoDefaultEntitySpecified,
            entities[0].Location,
            entities[0].TableName));
    }
    else if (defaultEntities.Count > 1)
    {
        // Error: Multiple defaults
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MultipleDefaultEntities,
            defaultEntities[1].Location,
            entities[0].TableName));
    }
}
```

### 3. Table Class Generation

#### Table Class Structure

```csharp
private string GenerateTableClass(string tableName, List<EntityModel> entities)
{
    var defaultEntity = entities.Count == 1 
        ? entities[0] 
        : entities.FirstOrDefault(e => e.IsDefault);
    
    var sb = new StringBuilder();
    
    // Class declaration
    sb.AppendLine($"public partial class {GetTableClassName(tableName)} : DynamoDbTableBase");
    sb.AppendLine("{");
    
    // Constructor
    GenerateConstructor(sb, tableName, entities);
    
    // Table-level operations (if default entity exists)
    if (defaultEntity != null)
    {
        GenerateTableLevelOperations(sb, defaultEntity);
    }
    
    // Entity accessor properties
    foreach (var entity in entities)
    {
        if (ShouldGenerateEntityProperty(entity))
        {
            GenerateEntityAccessorProperty(sb, entity);
        }
    }
    
    // Transaction/batch operations (always at table level)
    GenerateTransactionOperations(sb);
    
    // Nested accessor classes
    foreach (var entity in entities)
    {
        if (ShouldGenerateEntityProperty(entity))
        {
            GenerateEntityAccessorClass(sb, entity);
        }
    }
    
    sb.AppendLine("}");
    
    return sb.ToString();
}
```

#### Entity Accessor Property Generation

```csharp
private void GenerateEntityAccessorProperty(StringBuilder sb, EntityModel entity)
{
    var config = entity.EntityPropertyConfig;
    var modifier = GetModifierString(config.Modifier);
    var propertyName = config.Name ?? GetPluralName(entity.ClassName);
    var accessorClassName = $"{entity.ClassName}Accessor";
    
    sb.AppendLine($"    {modifier} {accessorClassName} {propertyName} {{ get; }}");
}

private void GenerateEntityAccessorClass(StringBuilder sb, EntityModel entity)
{
    var config = entity.EntityPropertyConfig;
    var accessorClassName = $"{entity.ClassName}Accessor";
    
    sb.AppendLine($"    public class {accessorClassName}");
    sb.AppendLine("    {");
    sb.AppendLine($"        private readonly {GetTableClassName(entity.TableName)} _table;");
    sb.AppendLine();
    sb.AppendLine($"        internal {accessorClassName}({GetTableClassName(entity.TableName)} table)");
    sb.AppendLine("        {");
    sb.AppendLine("            _table = table;");
    sb.AppendLine("        }");
    sb.AppendLine();
    
    // Generate operations based on [GenerateAccessors] configuration
    GenerateOperationMethods(sb, entity);
    
    sb.AppendLine("    }");
}
```

#### Operation Method Generation

```csharp
private void GenerateOperationMethods(StringBuilder sb, EntityModel entity)
{
    var operations = GetOperationsToGenerate(entity);
    
    foreach (var (operation, modifier) in operations)
    {
        var modifierStr = GetModifierString(modifier);
        
        switch (operation)
        {
            case DynamoDbOperation.Get:
                sb.AppendLine($"        {modifierStr} GetItemRequestBuilder<{entity.ClassName}> Get()");
                sb.AppendLine("        {");
                sb.AppendLine($"            return new GetItemRequestBuilder<{entity.ClassName}>(_table.Client, _table.TableName, {entity.ClassName}Metadata.Instance);");
                sb.AppendLine("        }");
                sb.AppendLine();
                break;
                
            case DynamoDbOperation.Query:
                sb.AppendLine($"        {modifierStr} QueryRequestBuilder<{entity.ClassName}> Query()");
                sb.AppendLine("        {");
                sb.AppendLine($"            return new QueryRequestBuilder<{entity.ClassName}>(_table.Client, _table.TableName, {entity.ClassName}Metadata.Instance);");
                sb.AppendLine("        }");
                sb.AppendLine();
                break;
                
            case DynamoDbOperation.Put:
                sb.AppendLine($"        {modifierStr} PutItemRequestBuilder<{entity.ClassName}> Put({entity.ClassName} item)");
                sb.AppendLine("        {");
                sb.AppendLine($"            return new PutItemRequestBuilder<{entity.ClassName}>(_table.Client, _table.TableName, {entity.ClassName}Metadata.Instance, item);");
                sb.AppendLine("        }");
                sb.AppendLine();
                break;
                
            // ... other operations
        }
    }
}

private List<(DynamoDbOperation, AccessModifier)> GetOperationsToGenerate(EntityModel entity)
{
    var result = new List<(DynamoDbOperation, AccessModifier)>();
    
    // Default: all operations are public
    var defaultOps = new Dictionary<DynamoDbOperation, AccessModifier>
    {
        [DynamoDbOperation.Get] = AccessModifier.Public,
        [DynamoDbOperation.Query] = AccessModifier.Public,
        [DynamoDbOperation.Scan] = AccessModifier.Public,
        [DynamoDbOperation.Put] = AccessModifier.Public,
        [DynamoDbOperation.Delete] = AccessModifier.Public,
        [DynamoDbOperation.Update] = AccessModifier.Public,
    };
    
    // Apply [GenerateAccessors] configurations
    foreach (var config in entity.AccessorConfigs)
    {
        var operations = ExpandOperationFlags(config.Operations);
        
        foreach (var op in operations)
        {
            if (!config.Generate)
            {
                defaultOps.Remove(op);
            }
            else
            {
                defaultOps[op] = config.Modifier;
            }
        }
    }
    
    return defaultOps.Select(kvp => (kvp.Key, kvp.Value)).ToList();
}
```

### 4. Table-Level Operations

```csharp
private void GenerateTableLevelOperations(StringBuilder sb, EntityModel defaultEntity)
{
    sb.AppendLine("    // Table-level operations using default entity");
    sb.AppendLine();
    
    sb.AppendLine($"    public GetItemRequestBuilder<{defaultEntity.ClassName}> Get()");
    sb.AppendLine("    {");
    sb.AppendLine($"        return {GetEntityPropertyName(defaultEntity)}.Get();");
    sb.AppendLine("    }");
    sb.AppendLine();
    
    sb.AppendLine($"    public QueryRequestBuilder<{defaultEntity.ClassName}> Query()");
    sb.AppendLine("    {");
    sb.AppendLine($"        return {GetEntityPropertyName(defaultEntity)}.Query();");
    sb.AppendLine("    }");
    sb.AppendLine();
    
    // ... other operations
}
```

### 5. Transaction Operations (Table Level Only)

```csharp
private void GenerateTransactionOperations(StringBuilder sb)
{
    sb.AppendLine("    // Transaction and batch operations (table level only)");
    sb.AppendLine();
    
    sb.AppendLine("    public TransactWriteItemsRequestBuilder TransactWrite()");
    sb.AppendLine("    {");
    sb.AppendLine("        return new TransactWriteItemsRequestBuilder(Client);");
    sb.AppendLine("    }");
    sb.AppendLine();
    
    sb.AppendLine("    public TransactGetItemsRequestBuilder TransactGet()");
    sb.AppendLine("    {");
    sb.AppendLine("        return new TransactGetItemsRequestBuilder(Client);");
    sb.AppendLine("    }");
    sb.AppendLine();
    
    sb.AppendLine("    public BatchWriteItemBuilder BatchWrite()");
    sb.AppendLine("    {");
    sb.AppendLine("        return new BatchWriteItemBuilder(Client);");
    sb.AppendLine("    }");
    sb.AppendLine();
    
    sb.AppendLine("    public BatchGetItemBuilder BatchGet()");
    sb.AppendLine("    {");
    sb.AppendLine("        return new BatchGetItemBuilder(Client);");
    sb.AppendLine("    }");
}
```

## Data Models

### EntityModel Enhancement

```csharp
internal class EntityModel
{
    public string ClassName { get; set; }
    public string TableName { get; set; }
    public bool IsDefault { get; set; }
    
    // NEW: Entity property configuration
    public EntityPropertyConfig EntityPropertyConfig { get; set; }
    
    // NEW: Accessor configurations
    public List<AccessorConfig> AccessorConfigs { get; set; }
    
    // Existing properties...
    public List<PropertyModel> Properties { get; set; }
    public Location Location { get; set; }
}

internal class EntityPropertyConfig
{
    public string? Name { get; set; }
    public bool Generate { get; set; } = true;
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}

internal class AccessorConfig
{
    public DynamoDbOperation Operations { get; set; }
    public bool Generate { get; set; } = true;
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}
```

## Error Handling

### Diagnostic Descriptors

```csharp
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NoDefaultEntitySpecified = new(
        id: "FDDB001",
        title: "No default entity specified",
        messageFormat: "Table '{0}' has multiple entities but no default specified. Mark one entity with IsDefault = true in [DynamoDbTable] attribute",
        category: "FluentDynamoDb.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor MultipleDefaultEntities = new(
        id: "FDDB002",
        title: "Multiple default entities",
        messageFormat: "Table '{0}' has multiple entities marked as default. Only one entity can be marked with IsDefault = true",
        category: "FluentDynamoDb.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor ConflictingAccessorConfiguration = new(
        id: "FDDB003",
        title: "Conflicting accessor configuration",
        messageFormat: "Entity '{0}' has multiple [GenerateAccessors] attributes targeting the same operation '{1}'",
        category: "FluentDynamoDb.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor EmptyEntityPropertyName = new(
        id: "FDDB004",
        title: "Empty entity property name",
        messageFormat: "Entity '{0}' has [GenerateEntityProperty] with empty Name. Provide a valid name or omit the Name property to use default",
        category: "FluentDynamoDb.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
```

## Testing Strategy

### Phase 1: Single Entity Table (Baseline)

```csharp
[Fact]
public void SingleEntityTable_GeneratesTableClass()
{
    var source = @"
        [DynamoDbTable(TableName = ""Orders"")]
        public class Order { }
    ";
    
    var result = GenerateSource(source);
    
    result.Should().ContainClass("OrdersTable");
    result.Should().ContainMethod("GetItemRequestBuilder<Order> Get()");
}
```

### Phase 2: Multi-Entity Table with Default

```csharp
[Fact]
public void MultiEntityTable_WithDefault_GeneratesConsolidatedTable()
{
    var source = @"
        [DynamoDbTable(TableName = ""MyApp"", IsDefault = true)]
        public class Order { }
        
        [DynamoDbTable(TableName = ""MyApp"")]
        public class OrderLine { }
    ";
    
    var result = GenerateSource(source);
    
    result.Should().ContainClass("MyAppTable");
    result.Should().ContainProperty("OrderAccessor Orders");
    result.Should().ContainProperty("OrderLineAccessor OrderLines");
    result.Should().ContainMethod("GetItemRequestBuilder<Order> Get()"); // Table-level uses default
}
```

### Phase 3: Multi-Entity Table without Default

```csharp
[Fact]
public void MultiEntityTable_WithoutDefault_EmitsDiagnostic()
{
    var source = @"
        [DynamoDbTable(TableName = ""MyApp"")]
        public class Order { }
        
        [DynamoDbTable(TableName = ""MyApp"")]
        public class OrderLine { }
    ";
    
    var result = GenerateSource(source);
    
    result.Diagnostics.Should().ContainError("FDDB001");
}
```

### Phase 4: Custom Entity Property Configuration

```csharp
[Fact]
public void EntityProperty_WithCustomName_UsesCustomName()
{
    var source = @"
        [DynamoDbTable(TableName = ""MyApp"", IsDefault = true)]
        [GenerateEntityProperty(Name = ""CustomOrders"")]
        public class Order { }
    ";
    
    var result = GenerateSource(source);
    
    result.Should().ContainProperty("OrderAccessor CustomOrders");
}

[Fact]
public void EntityProperty_WithGenerateFalse_DoesNotGenerateProperty()
{
    var source = @"
        [DynamoDbTable(TableName = ""MyApp"", IsDefault = true)]
        [GenerateEntityProperty(Generate = false)]
        public class Order { }
    ";
    
    var result = GenerateSource(source);
    
    result.Should().NotContainProperty("OrderAccessor Orders");
}
```

### Phase 5: Custom Accessor Configuration

```csharp
[Fact]
public void Accessor_WithInternalModifier_GeneratesInternalMethods()
{
    var source = @"
        [DynamoDbTable(TableName = ""MyApp"", IsDefault = true)]
        [GenerateAccessors(Operations = DynamoDbOperation.All, Modifier = AccessModifier.Internal)]
        public class Order { }
    ";
    
    var result = GenerateSource(source);
    
    result.Should().ContainMethod("internal GetItemRequestBuilder<Order> Get()");
}

[Fact]
public void Accessor_WithGenerateFalse_DoesNotGenerateOperation()
{
    var source = @"
        [DynamoDbTable(TableName = ""MyApp"", IsDefault = true)]
        [GenerateAccessors(Operations = DynamoDbOperation.Delete, Generate = false)]
        public class Order { }
    ";
    
    var result = GenerateSource(source);
    
    result.Should().NotContainMethod("DeleteItemRequestBuilder<Order> Delete()");
}
```

### Phase 6: Transaction Operations

```csharp
[Fact]
public void TransactionOperations_AlwaysAtTableLevel()
{
    var source = @"
        [DynamoDbTable(TableName = ""MyApp"", IsDefault = true)]
        public class Order { }
        
        [DynamoDbTable(TableName = ""MyApp"")]
        public class OrderLine { }
    ";
    
    var result = GenerateSource(source);
    
    result.Should().ContainMethod("TransactWriteItemsRequestBuilder TransactWrite()");
    result.Should().NotContainMethod("OrderAccessor", "TransactWrite"); // Not on entity accessor
}
```

## Implementation Phases

### Phase 1: Attribute Definitions
- Create GenerateEntityPropertyAttribute
- Create GenerateAccessorsAttribute
- Create AccessModifier enum
- Create DynamoDbOperation enum
- Add IsDefault property to DynamoDbTableAttribute

### Phase 2: Entity Analysis Enhancement
- Update EntityAnalyzer to extract IsDefault
- Extract [GenerateEntityProperty] configuration
- Extract [GenerateAccessors] configuration
- Validate accessor configuration conflicts

### Phase 3: Entity Grouping
- Group entities by TableName in source generator
- Validate default entity selection
- Emit diagnostics for invalid configurations

### Phase 4: Table Class Generation
- Generate consolidated table class structure
- Generate entity accessor properties
- Generate nested entity accessor classes
- Generate table-level operations for default entity

### Phase 5: Operation Method Generation
- Generate operation methods based on [GenerateAccessors]
- Apply visibility modifiers
- Handle Generate = false cases

### Phase 6: Transaction Operations
- Generate transaction/batch operations at table level only
- Ensure they accept any entity type

### Phase 7: Testing
- Update all unit tests to use new accessor pattern
- Add source generator tests for new features
- Add integration tests for multi-entity tables

### Phase 8: Documentation
- Update documentation with multi-entity examples
- Document attribute usage
- Update code examples

## Performance Considerations

### Source Generator Performance
- Entity grouping is O(n) where n is number of entities
- No significant performance impact expected
- Incremental generation still applies

### Runtime Performance
- Entity accessor properties are simple getters
- No additional overhead compared to current implementation
- Table-level operations delegate to entity accessors (one extra method call)

## Security Considerations

### Visibility Modifiers
- Internal/private modifiers allow hiding implementation details
- Developers can create custom public APIs that call internal generated methods
- Follows principle of least privilege

## Migration Strategy

### Updating Unit Tests

```csharp
// Before
var order = await orderTable.Get()
    .WithKey("pk", "ORDER#123")
    .ExecuteAsync();

// After (single entity - no change needed if using default)
var order = await myAppTable.Get()
    .WithKey("pk", "ORDER#123")
    .ExecuteAsync();

// After (multi-entity - use entity accessor)
var order = await myAppTable.Orders.Get()
    .WithKey("pk", "ORDER#123")
    .ExecuteAsync();
```

### Updating Documentation Examples

All examples showing table operations need to be updated to use either:
1. Table-level operations (for default entity)
2. Entity accessor operations (for specific entities)

## Open Questions

1. **Pluralization Strategy**: Should we use a simple "add 's'" rule or a more sophisticated pluralization library?
   - **Decision**: Simple "add 's'" for now, allow override with Name property

2. **Nested Accessor Class Naming**: Should it be `OrderAccessor` or `OrderOperations` or something else?
   - **Decision**: `{EntityName}Accessor` for consistency

3. **Private/Protected on Nested Classes**: Can nested classes be private/protected and still be accessible from the parent table class?
   - **Decision**: Yes, nested classes can be private/protected. The parent class can always access them.

4. **Table Class Naming**: Should we use `{TableName}Table` or just `{TableName}`?
   - **Decision**: Keep `{TableName}Table` for consistency with existing pattern
