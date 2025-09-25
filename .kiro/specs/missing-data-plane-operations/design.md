# Design Document

## Overview

This design implements missing DynamoDB data plane operations (Scan, DeleteItem, BatchGetItem, BatchWriteItem) while maintaining the library's existing patterns and introducing a thoughtful approach to Scan operations that discourages accidental misuse. The design follows the established fluent builder pattern, interface segregation principles, and AOT compatibility requirements.

## Architecture

### Core Design Principles

1. **Fluent Interface Consistency**: All new builders follow the same patterns as existing builders
2. **Interface Segregation**: Clean separation between core table operations and scan operations
3. **Intentional Friction for Scan**: Scan operations require explicit opt-in via `AsScannable()`
4. **AOT Compatibility**: No reflection or dynamic code generation
5. **Wrapper Pattern**: Scannable functionality wraps existing table instances without modification

### Component Overview

```
DynamoDbTableBase (implements IDynamoDbTable)
├── Get: GetItemRequestBuilder
├── Put: PutItemRequestBuilder  
├── Update: UpdateItemRequestBuilder
├── Query: QueryRequestBuilder
├── Delete: DeleteItemRequestBuilder (NEW)
└── AsScannable(): IScannableDynamoDbTable (NEW)

IScannableDynamoDbTable (extends IDynamoDbTable)
├── All IDynamoDbTable operations (pass-through)
├── Scan: ScanRequestBuilder (NEW)
└── UnderlyingTable: DynamoDbTableBase (access to original)

New Request Builders:
├── ScanRequestBuilder
├── DeleteItemRequestBuilder
├── BatchGetItemRequestBuilder
└── BatchWriteItemRequestBuilder
```

## Components and Interfaces

### 1. Table Interface Hierarchy

#### IDynamoDbTable Interface
```csharp
public interface IDynamoDbTable
{
    IAmazonDynamoDB DynamoDbClient { get; }
    string Name { get; }
    
    GetItemRequestBuilder Get { get; }
    PutItemRequestBuilder Put { get; }
    UpdateItemRequestBuilder Update { get; }
    QueryRequestBuilder Query { get; }
    DeleteItemRequestBuilder Delete { get; }
}
```

#### IScannableDynamoDbTable Interface
```csharp
public interface IScannableDynamoDbTable : IDynamoDbTable
{
    ScanRequestBuilder Scan { get; }
    DynamoDbTableBase UnderlyingTable { get; }
}
```

#### ScannableDynamoDbTable Implementation
```csharp
internal class ScannableDynamoDbTable : IScannableDynamoDbTable
{
    private readonly DynamoDbTableBase _table;
    
    public ScannableDynamoDbTable(DynamoDbTableBase table)
    {
        _table = table;
    }
    
    // Pass-through properties and operations
    public IAmazonDynamoDB DynamoDbClient => _table.DynamoDbClient;
    public string Name => _table.Name;
    public DynamoDbTableBase UnderlyingTable => _table;
    
    // Pass-through operations
    public GetItemRequestBuilder Get => _table.Get;
    public PutItemRequestBuilder Put => _table.Put;
    public UpdateItemRequestBuilder Update => _table.Update;
    public QueryRequestBuilder Query => _table.Query;
    public DeleteItemRequestBuilder Delete => _table.Delete;
    
    // New scan operation
    public ScanRequestBuilder Scan => new ScanRequestBuilder(DynamoDbClient).ForTable(Name);
}
```

### 2. Request Builder Implementations

#### ScanRequestBuilder
```csharp
public class ScanRequestBuilder : 
    IWithAttributeNames<ScanRequestBuilder>, 
    IWithAttributeValues<ScanRequestBuilder>
{
    private ScanRequest _req = new ScanRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    // Core methods:
    // - ForTable(string tableName)
    // - WithFilter(string filterExpression)
    // - UsingIndex(string indexName)
    // - WithProjection(string projectionExpression)
    // - Take(int limit)
    // - StartAt(Dictionary<string,AttributeValue> exclusiveStartKey)
    // - UsingConsistentRead()
    // - WithSegment(int segment, int totalSegments) // For parallel scans (DynamoDB native feature)
    // - Count() // Select COUNT
    // - ReturnConsumedCapacity methods
    // - Standard attribute name/value methods
    // - ExecuteAsync() and ToScanRequest()
}
```

#### DeleteItemRequestBuilder
```csharp
public class DeleteItemRequestBuilder : 
    IWithKey<DeleteItemRequestBuilder>, 
    IWithConditionExpression<DeleteItemRequestBuilder>,
    IWithAttributeNames<DeleteItemRequestBuilder>, 
    IWithAttributeValues<DeleteItemRequestBuilder>
{
    private DeleteItemRequest _req = new DeleteItemRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    // Core methods:
    // - ForTable(string tableName)
    // - WithKey methods (from IWithKey)
    // - Where(string conditionExpression)
    // - ReturnAllOldValues(), ReturnNone()
    // - ReturnConsumedCapacity methods
    // - ReturnItemCollectionMetrics()
    // - ReturnOldValuesOnConditionCheckFailure()
    // - Standard attribute name/value methods
    // - ExecuteAsync() and ToDeleteItemRequest()
}
```

#### BatchGetItemRequestBuilder
```csharp
public class BatchGetItemRequestBuilder
{
    private BatchGetItemRequest _req = new BatchGetItemRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    
    // Core methods following TransactGetItemsRequestBuilder pattern:
    // - GetFromTable(string tableName, Action<BatchGetItemBuilder> builderAction)
    // - ReturnConsumedCapacity methods
    // - ExecuteAsync() and ToBatchGetItemRequest()
}
```

#### BatchGetItemBuilder
```csharp
public class BatchGetItemBuilder : IWithKey<BatchGetItemBuilder>, IWithAttributeNames<BatchGetItemBuilder>
{
    private KeysAndAttributes _keysAndAttributes = new KeysAndAttributes();
    private readonly string _tableName;
    
    // Core methods:
    // - WithKey methods (from IWithKey) - adds to Keys collection
    // - WithProjection(string projectionExpression)
    // - UsingConsistentRead()
    // - WithAttributes methods (from IWithAttributeNames)
    // - ToKeysAndAttributes() - internal method
}
```

#### BatchWriteItemRequestBuilder
```csharp
public class BatchWriteItemRequestBuilder
{
    private BatchWriteItemRequest _req = new BatchWriteItemRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    
    // Core methods following TransactWriteItemsRequestBuilder pattern:
    // - WriteToTable(string tableName, Action<BatchWriteItemBuilder> builderAction)
    // - ReturnConsumedCapacity methods
    // - ReturnItemCollectionMetrics()
    // - ExecuteAsync() and ToBatchWriteItemRequest()
}
```

#### BatchWriteItemBuilder
```csharp
public class BatchWriteItemBuilder
{
    private List<WriteRequest> _writeRequests = new List<WriteRequest>();
    private readonly string _tableName;
    
    // Core methods:
    // - PutItem(Dictionary<string, AttributeValue> item)
    // - PutItem<T>(T item, Func<T, Dictionary<string, AttributeValue>> mapper)
    // - DeleteItem(Dictionary<string, AttributeValue> key)
    // - DeleteItem(string keyName, string keyValue)
    // - DeleteItem(string pkName, string pkValue, string skName, string skValue)
    // - ToWriteRequests() - internal method
}
```

### 3. Updated DynamoDbTableBase

```csharp
public abstract class DynamoDbTableBase : IDynamoDbTable
{
    // Existing implementation remains the same
    // Add new Delete property and AsScannable method
    
    public DeleteItemRequestBuilder Delete => new DeleteItemRequestBuilder(DynamoDbClient).ForTable(Name);
    
    public IScannableDynamoDbTable AsScannable()
    {
        return new ScannableDynamoDbTable(this);
    }
}
```

## Data Models

### Request/Response Flow
```
Client Code
    ↓
Table.AsScannable().Scan  →  ScanRequestBuilder
    ↓                           ↓
IScannableDynamoDbTable    →   ScanRequest
    ↓                           ↓
ScannableDynamoDbTable     →   IAmazonDynamoDB
    ↓                           ↓
DynamoDbTableBase          →   ScanResponse
```

### Key Data Structures

1. **Scan Operations**: Standard DynamoDB ScanRequest/ScanResponse
2. **Delete Operations**: Standard DynamoDB DeleteItemRequest/DeleteItemResponse  
3. **Batch Operations**: Standard DynamoDB BatchGetItemRequest/BatchWriteItemRequest with corresponding responses
4. **Interface Wrappers**: Lightweight wrappers that delegate to underlying table instances

## Error Handling

### Scan Operation Safeguards
- Scan operations are only accessible through `AsScannable()` method
- No direct scan access from base table class
- Clear documentation about performance implications

### Batch Operation Handling
- Unprocessed items in batch operations are exposed through response objects
- Builders provide access to raw AWS SDK responses for full control
- No automatic retry logic (follows existing library patterns)

### Standard Error Patterns
- All builders follow existing error handling patterns
- AWS SDK exceptions bubble up naturally
- Validation errors occur at build time where possible

## Testing Strategy

### Unit Test Coverage
1. **Request Builder Tests**: Each new builder gets comprehensive unit tests following existing patterns
2. **Interface Implementation Tests**: Verify interface contracts are properly implemented
3. **Wrapper Functionality Tests**: Ensure scannable wrapper properly delegates operations
4. **Integration Tests**: Test end-to-end flows with mock DynamoDB client

### Test Structure
```
Oproto.FluentDynamoDb.UnitTests/
├── Requests/
│   ├── ScanRequestBuilderTests.cs
│   ├── DeleteItemRequestBuilderTests.cs
│   ├── BatchGetItemRequestBuilderTests.cs
│   └── BatchWriteItemRequestBuilderTests.cs
├── Storage/
│   ├── DynamoDbTableBaseTests.cs (updated)
│   └── ScannableDynamoDbTableTests.cs
└── Integration/
    └── MissingOperationsIntegrationTests.cs
```

### Key Test Scenarios
1. **Scan Access Control**: Verify scan is only accessible through `AsScannable()`
2. **Interface Compliance**: All builders implement required interfaces correctly
3. **Pass-through Functionality**: Scannable wrapper properly delegates all operations
4. **Builder Patterns**: All new builders follow established fluent patterns
5. **AOT Compatibility**: No reflection or dynamic code generation in new components

## Implementation Phases

### Phase 1: Core Infrastructure
1. Create `IDynamoDbTable` and `IScannableDynamoDbTable` interfaces
2. Implement `ScannableDynamoDbTable` wrapper class
3. Update `DynamoDbTableBase` to implement `IDynamoDbTable`
4. Add `AsScannable()` method to `DynamoDbTableBase`

### Phase 2: Delete Operations
1. Implement `DeleteItemRequestBuilder`
2. Add `Delete` property to `DynamoDbTableBase`
3. Create comprehensive unit tests

### Phase 3: Scan Operations  
1. Implement `ScanRequestBuilder`
2. Add `Scan` property to `IScannableDynamoDbTable`
3. Create comprehensive unit tests
4. Add integration tests

### Phase 4: Batch Operations
1. Implement `BatchGetItemRequestBuilder` and `BatchGetItemBuilder` following transaction pattern
2. Implement `BatchWriteItemRequestBuilder` and `BatchWriteItemBuilder` following transaction pattern
3. Create comprehensive unit tests
4. Add integration tests

### Phase 5: Documentation and Examples
1. Update README with new operation examples
2. Add XML documentation to all new public APIs
3. Create usage examples for each new operation
4. Document the intentional friction pattern for Scan operations