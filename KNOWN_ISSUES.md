# Known Issues

## Source Generator: Duplicate Index Property Generation

**Status:** Open  
**Severity:** High - Blocks compilation  
**Affected Version:** Current

### Description
The source generator creates duplicate `StatusIndex` properties for tables with Global Secondary Indexes, causing compilation errors:

```
error CS0102: The type 'TestMultiEntityTable' already contains a definition for 'StatusIndex'
```

### Root Cause
The source generator generates GSI index properties in two different ways simultaneously:

1. **Typed Index Class** (in main table file): 
   - Creates a typed index class like `TestMultiEntityTableStatusIndexIndex`
   - Adds property: `public TestMultiEntityTableStatusIndexIndex StatusIndex => ...`

2. **Simple Index Property** (in `.Indexes.g.cs` partial file):
   - Creates via `TableIndexGenerator.cs`
   - Adds property: `public DynamoDbIndex StatusIndex => ...`

Both are generated in the same namespace for the same partial class, causing a member conflict.

### Affected Files
- `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableIndexGenerator.cs` - Generates simple index properties
- Main table generator (location TBD) - Generates typed index classes

### Reproduction
1. Create an entity with a `[GlobalSecondaryIndex]` attribute
2. Mark entity with `[DynamoDbTable(IsDefault = true)]` (or have multiple entities share a table)
3. Build project
4. Observe duplicate member compilation error

### Example
```csharp
[DynamoDbTable("test-multi-entity", IsDefault = true)]
public partial class InventoryEntity
{
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    public string? Status { get; set; }
}
```

Generates both:
- `TestMultiEntityTable.g.cs`: `public TestMultiEntityTableStatusIndexIndex StatusIndex => ...`
- `testmultientityTable.Indexes.g.cs`: `public DynamoDbIndex StatusIndex => ...`

### Proposed Solution
**Option 1 (Recommended):** Generate typed index classes as nested classes within the table class
- Consolidate all index-related code into the main table file
- Remove separate `.Indexes.g.cs` file generation
- Generate typed index classes as nested classes for better organization

**Option 2:** Conditional generation based on index complexity
- Only generate typed index classes when needed (e.g., with projections)
- Use simple `DynamoDbIndex` properties for basic indexes
- Requires logic to determine which pattern to use

**Option 3:** Remove `TableIndexGenerator` entirely
- Only use typed index classes
- Simpler generation logic
- May lose some flexibility

### Workaround
Currently, there is no clean workaround. The duplicate generation must be fixed in the source generator.

### Related Files
- `Oproto.FluentDynamoDb.IntegrationTests/TestEntities/InventoryEntity.cs`
- Generated: `TestMultiEntityTable.g.cs`
- Generated: `testmultientityTable.Indexes.g.cs`

### Notes
- The issue occurs regardless of `IsDefault` setting
- Affects any table with GSI definitions
- Blocks integration test compilation
