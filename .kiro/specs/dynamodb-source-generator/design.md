# DynamoDB Source Generator Design

## Overview

The DynamoDB Source Generator enhances the Oproto.FluentDynamoDb library by automatically generating entity mapping code, field constants, key builders, and enhanced ExecuteAsync methods. This design maintains AOT compatibility while providing an EF/LINQ-like developer experience through compile-time code generation.

## Architecture

### Project Structure

```
Oproto.FluentDynamoDb.sln
├── Oproto.FluentDynamoDb/                    # Main library (.NET 8)
│   ├── Storage/
│   │   ├── IDynamoDbEntity.cs               # New interface for generated entities
│   │   └── Enhanced ExecuteAsync extensions  # Generic ExecuteAsync<T> methods
│   └── Attributes/                          # New attribute definitions
├── Oproto.FluentDynamoDb.SourceGenerator/   # Source generator (.NET Standard 2.0)
│   ├── Generators/
│   │   ├── EntityGenerator.cs               # Generates IDynamoDbEntity implementations
│   │   ├── FieldsGenerator.cs               # Generates field name constants
│   │   ├── KeysGenerator.cs                 # Generates key builder methods
│   │   └── MapperGenerator.cs               # Generates mapping logic
│   └── Analyzers/                           # Syntax analyzers and receivers
├── Oproto.FluentDynamoDb.FluentResults/     # FluentResults extensions (.NET 8)
└── Tests/                                   # Unit and integration tests
```

### Core Components

#### 1. Attribute System

**Entity Definition Attributes:**
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class DynamoDbTableAttribute : Attribute
{
    public string TableName { get; }
    public string? EntityDiscriminator { get; set; }
    public DynamoDbTableAttribute(string tableName) => TableName = tableName;
}

[AttributeUsage(AttributeTargets.Property)]
public class DynamoDbAttributeAttribute : Attribute
{
    public string AttributeName { get; }
    public DynamoDbAttributeAttribute(string attributeName) => AttributeName = attributeName;
}

[AttributeUsage(AttributeTargets.Property)]
public class PartitionKeyAttribute : Attribute
{
    public string? Prefix { get; set; }
    public string? Separator { get; set; } = "#";
}

[AttributeUsage(AttributeTargets.Property)]
public class SortKeyAttribute : Attribute
{
    public string? Prefix { get; set; }
    public string? Separator { get; set; } = "#";
}
```

**Index and Relationship Attributes:**
```csharp
[AttributeUsage(AttributeTargets.Property)]
public class GlobalSecondaryIndexAttribute : Attribute
{
    public string IndexName { get; }
    public bool IsPartitionKey { get; set; }
    public bool IsSortKey { get; set; }
    public string? KeyFormat { get; set; }
    public GlobalSecondaryIndexAttribute(string indexName) => IndexName = indexName;
}

[AttributeUsage(AttributeTargets.Property)]
public class RelatedEntityAttribute : Attribute
{
    public string SortKeyPattern { get; }
    public Type? EntityType { get; set; }
    public RelatedEntityAttribute(string sortKeyPattern) => SortKeyPattern = sortKeyPattern;
}

[AttributeUsage(AttributeTargets.Property)]
public class QueryableAttribute : Attribute
{
    public DynamoDbOperation[] SupportedOperations { get; set; } = Array.Empty<DynamoDbOperation>();
    public string[]? AvailableInIndexes { get; set; }
}

public enum DynamoDbOperation
{
    Equals, BeginsWith, Between, GreaterThan, LessThan, Contains, In
}
```

**Computed and Composite Key Attributes:**
```csharp
[AttributeUsage(AttributeTargets.Property)]
public class ComputedAttribute : Attribute
{
    public string[] SourceProperties { get; }
    public string? Format { get; set; }
    public string? Separator { get; set; } = "#";
    
    public ComputedAttribute(params string[] sourceProperties) 
        => SourceProperties = sourceProperties;
}

[AttributeUsage(AttributeTargets.Property)]
public class ExtractedAttribute : Attribute
{
    public string SourceProperty { get; }
    public int Index { get; set; }
    public string? Separator { get; set; } = "#";
    
    public ExtractedAttribute(string sourceProperty, int index) 
    {
        SourceProperty = sourceProperty;
        Index = index;
    }
}
```

#### 2. Computed and Composite Key Patterns

**Pattern 1: Computed Composite Keys**
```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    // Source properties (not directly mapped to DynamoDB)
    public string TenantId { get; set; }
    public string CustomerId { get; set; }
    
    // Computed composite partition key
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(CustomerId))]
    public string Pk { get; set; }
    
    // Computed GSI keys
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("gsi1_pk")]
    [Computed(nameof(Status), Format = "STATUS#{0}")]
    public string StatusIndexPk { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; }
}

// Generated code handles computation:
// entity.Pk = $"{entity.TenantId}#{entity.CustomerId}";
// entity.StatusIndexPk = $"STATUS#{entity.Status}";
```

**Pattern 2: Extracted Component Properties**
```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    // Composite key stored in DynamoDB
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; }
    
    // Extracted component properties (not directly mapped)
    [Extracted(nameof(Pk), 0)]
    public string TenantId { get; set; }
    
    [Extracted(nameof(Pk), 1)]
    public string CustomerId { get; set; }
}

// Generated code handles extraction:
// var parts = entity.Pk.Split('#');
// entity.TenantId = parts[0];
// entity.CustomerId = parts[1];
```

**Pattern 3: Bidirectional Mapping**
```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    // Component properties
    public string TenantId { get; set; }
    public string CustomerId { get; set; }
    
    // Computed composite key with extraction support
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(CustomerId))]
    public string Pk { get; set; }
    
    // Alternative: Use both attributes for bidirectional mapping
    [Extracted(nameof(Pk), 0)]
    public string ExtractedTenantId => TenantId; // Read-only computed property
}

// Generated ToDynamoDb: Computes Pk from TenantId + CustomerId
// Generated FromDynamoDb: Extracts TenantId + CustomerId from Pk
```

**Pattern 4: Custom Formats and Separators**
```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    public DateTime Date { get; set; }
    public string TransactionType { get; set; }
    public int SequenceNumber { get; set; }
    
    // Custom format with date formatting
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed(nameof(Date), nameof(TransactionType), nameof(SequenceNumber), 
              Format = "{0:yyyy-MM-dd}#{1}#{2:D6}", Separator = "#")]
    public string Sk { get; set; }
    
    // Result: "2024-03-15#PAYMENT#000123"
}
```

#### 3. IDynamoDbEntity Interface

```csharp
public interface IDynamoDbEntity
{
    // Static abstract methods for compile-time mapping
    static abstract Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity) 
        where TSelf : IDynamoDbEntity;
    
    static abstract TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item) 
        where TSelf : IDynamoDbEntity;
    
    static abstract TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items) 
        where TSelf : IDynamoDbEntity;
    
    static abstract string GetPartitionKey(Dictionary<string, AttributeValue> item);
    
    static abstract bool MatchesEntity(Dictionary<string, AttributeValue> item);
    
    // Metadata for future LINQ support
    static abstract EntityMetadata GetEntityMetadata();
}

public class EntityMetadata
{
    public string TableName { get; set; }
    public PropertyMetadata[] Properties { get; set; }
    public IndexMetadata[] Indexes { get; set; }
    public RelationshipMetadata[] Relationships { get; set; }
}

public class PropertyMetadata
{
    public string PropertyName { get; set; }
    public string AttributeName { get; set; }
    public Type PropertyType { get; set; }
    public bool IsPartitionKey { get; set; }
    public bool IsSortKey { get; set; }
    public DynamoDbOperation[] SupportedOperations { get; set; }
    public string[]? AvailableInIndexes { get; set; }
}
```

#### 3. Enhanced Query Methods (EF/LINQ-Style)

**API Design Philosophy:**
The Query API uses EF/LINQ-style method names to make the intent clear at the call site:

- **`ToListAsync<T>()`** - Each DynamoDB item becomes a separate `T` instance (1:1 mapping)
- **`ToCompositeEntityAsync<T>()`** - Multiple DynamoDB items combined into one `T` instance (N:1 mapping)

**Usage Examples:**
```csharp
// Scenario 1: Query returns List<T> - all items are same entity type
var transactions = await Query
    .Where("pk = :pk AND begins_with(sk, :prefix)", tenantId, "TXN#")
    .ToListAsync<Transaction>();

// Scenario 2: Query returns single composite entity - primary + related entities
var order = await Query
    .Where("pk = :pk", orderId)
    .ToCompositeEntityAsync<Order>(); // Order with populated OrderItems, Payments, etc.
```

**Data Storage (NO JSON):**
- All data stored as native DynamoDB attributes (S, N, SS, NS, etc.)
- Related entities identified by sort key patterns and mapped to properties using `[RelatedEntity]` attributes
- Collections are either separate DynamoDB items OR native DynamoDB lists

**Enhanced Request Builder Extensions:**
```csharp
public static class RequestBuilderExtensions
{
    // Extension to use scoped client
    public static GetItemRequestBuilder WithClient(this GetItemRequestBuilder builder, IAmazonDynamoDB client)
    {
        // Create new builder instance with different client
        return new GetItemRequestBuilder(client, builder.TableName);
    }
    
    public static QueryRequestBuilder WithClient(this QueryRequestBuilder builder, IAmazonDynamoDB client)
    {
        // Create new builder instance with different client  
        return new QueryRequestBuilder(client, builder.TableName);
    }
    
    // Similar extensions for Put, Update, Delete builders
}
```

```csharp
public static class EnhancedExecuteAsyncExtensions
{
    public static async Task<GetItemResponse<T>> ExecuteAsync<T>(
        this GetItemRequestBuilder builder, 
        CancellationToken cancellationToken = default) 
        where T : class, IDynamoDbEntity
    {
        var response = await builder.ExecuteAsync(cancellationToken);
        
        return new GetItemResponse<T>
        {
            Item = response.Item != null && T.MatchesEntity(response.Item) 
                ? T.FromDynamoDb<T>(response.Item) 
                : null,
            ConsumedCapacity = response.ConsumedCapacity,
            ResponseMetadata = response.ResponseMetadata
        };
    }
    
    // EF/LINQ-style Query methods with clear intent
    // Enhanced methods automatically use the builder's client - no need for separate overloads
    
    public static async Task<List<T>> ToListAsync<T>(
        this QueryRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        var response = await builder.ExecuteAsync(cancellationToken);
        
        // Each DynamoDB item becomes a separate T instance (1:1 mapping)
        var entityItems = response.Items
            .Where(T.MatchesEntity)
            .Select(item => T.FromDynamoDb<T>(item))
            .ToList();
        
        return entityItems;
    }
    
    public static async Task<T?> ToCompositeEntityAsync<T>(
        this QueryRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        var response = await builder.ExecuteAsync(cancellationToken);
        
        // Multiple DynamoDB items combined into one T instance (N:1 mapping)
        // Primary entity identified by sort key pattern, related entities populate properties
        var matchingItems = response.Items.Where(T.MatchesEntity).ToList();
        
        if (matchingItems.Count == 0)
            return null;
            
        // Use multi-item FromDynamoDb to combine all items into single entity
        return T.FromDynamoDb<T>(matchingItems);
    }
    
    public static PutItemRequestBuilder WithItem<T>(
        this PutItemRequestBuilder builder, 
        T item) where T : class, IDynamoDbEntity
    {
        var attributeDict = T.ToDynamoDb(item);
        return builder.WithItem(attributeDict);
    }
}
```

### Credential Management Considerations

**Service-Layer STS Token Pattern:**

The design accommodates scenarios where the service layer generates STS tokens with tenant-specific policies based on request context (OAuth claims, tenant ID, etc.):

```csharp
// Service layer generates scoped client per request
public class TransactionService
{
    private readonly TransactionsTable _table;
    private readonly IStsTokenGenerator _stsGenerator;
    
    public async Task<Result<TransactionEntry>> GetTransactionAsync(
        Ulid tenantId, Ulid transactionId, 
        ClaimsPrincipal user)
    {
        // Generate STS token with tenant-specific policy
        var scopedClient = await _stsGenerator.CreateClientForTenant(tenantId, user.Claims);
        
        // Pass scoped client to table operation
        return await _table.GetTransactionAsync(tenantId, transactionId, scopedClient);
    }
}

// Generated table methods accept optional scoped client
public partial class TransactionsTable : DynamoDbTableBase
{
    public async Task<Result<TransactionEntry>> GetTransactionAsync(
        Ulid tenantId, Ulid transactionId,
        IAmazonDynamoDB? scopedClient = null,
        CancellationToken cancellationToken = default)
    {
        // Use provided scoped client or fall back to default
        var client = scopedClient ?? DynamoDbClient;
        
        return await Query
            .WithClient(client)  // Use scoped client for this operation
            .Where("{0} = {1}", TransactionFields.Pk, TransactionKeys.Pk(tenantId, transactionId))
            .ToCompositeEntityAsync<TransactionEntry>(cancellationToken);
    }
}
```

**Design Decision**: All generated methods will accept an optional `IAmazonDynamoDB? scopedClient` parameter, allowing the service layer to provide tenant-scoped clients without the table needing to understand the credential generation logic.

## Components and Interfaces

### Source Generator Pipeline

#### 1. Syntax Receiver
```csharp
[Generator]
public class DynamoDbSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register syntax receiver for classes with DynamoDbTable attribute
        var entityClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsDynamoDbEntity(s),
                transform: static (ctx, _) => GetEntityInfo(ctx))
            .Where(static m => m is not null);
        
        // Register code generation
        context.RegisterSourceOutput(entityClasses.Collect(), Execute);
    }
    
    private static bool IsDynamoDbEntity(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl &&
               classDecl.AttributeLists.Any(al => 
                   al.Attributes.Any(a => 
                       a.Name.ToString().Contains("DynamoDbTable")));
    }
}
```

#### 2. Entity Analysis
```csharp
public class EntityAnalyzer
{
    public EntityModel AnalyzeEntity(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        var entityModel = new EntityModel
        {
            ClassName = classDecl.Identifier.ValueText,
            Namespace = GetNamespace(classDecl),
            TableName = GetTableName(classDecl, semanticModel),
            Properties = AnalyzeProperties(classDecl, semanticModel),
            Indexes = AnalyzeIndexes(classDecl, semanticModel),
            Relationships = AnalyzeRelationships(classDecl, semanticModel)
        };
        
        ValidateEntityModel(entityModel);
        return entityModel;
    }
    
    private PropertyModel[] AnalyzeProperties(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        return classDecl.Members
            .OfType<PropertyDeclarationSyntax>()
            .Select(prop => AnalyzeProperty(prop, semanticModel))
            .Where(prop => prop != null)
            .ToArray();
    }
}
```

#### 3. Code Generation

**Entity Implementation Generator:**
```csharp
public class EntityImplementationGenerator
{
    public string GenerateEntityImplementation(EntityModel entity)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"// <auto-generated />");
        sb.AppendLine($"using System;");
        sb.AppendLine($"using System.Collections.Generic;");
        sb.AppendLine($"using Amazon.DynamoDBv2.Model;");
        sb.AppendLine($"using Oproto.FluentDynamoDb.Storage;");
        sb.AppendLine();
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine($"{{");
        sb.AppendLine($"    public partial class {entity.ClassName} : IDynamoDbEntity");
        sb.AppendLine($"    {{");
        
        GenerateStaticMethods(sb, entity);
        
        sb.AppendLine($"    }}");
        sb.AppendLine($"}}");
        
        return sb.ToString();
    }
    
    private void GenerateStaticMethods(StringBuilder sb, EntityModel entity)
    {
        GenerateToDynamoDbMethod(sb, entity);
        GenerateFromDynamoDbMethods(sb, entity);
        GenerateGetPartitionKeyMethod(sb, entity);
        GenerateMatchesEntityMethod(sb, entity);
        GenerateGetEntityMetadataMethod(sb, entity);
    }
}
```

**Fields Generator:**
```csharp
public class FieldsGenerator
{
    public string GenerateFieldsClass(EntityModel entity)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"// <auto-generated />");
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine($"{{");
        sb.AppendLine($"    public static partial class {entity.ClassName}Fields");
        sb.AppendLine($"    {{");
        
        // Generate field constants
        foreach (var prop in entity.Properties)
        {
            sb.AppendLine($"        public const string {prop.PropertyName} = \"{prop.AttributeName}\";");
        }
        
        // Generate GSI field classes
        foreach (var index in entity.Indexes)
        {
            GenerateIndexFields(sb, index);
        }
        
        sb.AppendLine($"    }}");
        sb.AppendLine($"}}");
        
        return sb.ToString();
    }
}
```

### Data Models

#### Entity Model
```csharp
public class EntityModel
{
    public string ClassName { get; set; }
    public string Namespace { get; set; }
    public string TableName { get; set; }
    public string? EntityDiscriminator { get; set; }
    public PropertyModel[] Properties { get; set; }
    public IndexModel[] Indexes { get; set; }
    public RelationshipModel[] Relationships { get; set; }
    public bool IsMultiItemEntity { get; set; }
}

public class PropertyModel
{
    public string PropertyName { get; set; }
    public string AttributeName { get; set; }
    public string PropertyType { get; set; }
    public bool IsPartitionKey { get; set; }
    public bool IsSortKey { get; set; }
    public bool IsCollection { get; set; }
    public bool IsNullable { get; set; }
    public KeyFormatModel? KeyFormat { get; set; }
    public QueryableModel? Queryable { get; set; }
    public ComputedKeyModel? ComputedKey { get; set; }
    public ExtractedKeyModel? ExtractedKey { get; set; }
}

public class ComputedKeyModel
{
    public string[] SourceProperties { get; set; }
    public string? Format { get; set; }
    public string Separator { get; set; } = "#";
}

public class ExtractedKeyModel
{
    public string SourceProperty { get; set; }
    public int Index { get; set; }
    public string Separator { get; set; } = "#";
}

public class IndexModel
{
    public string IndexName { get; set; }
    public string PartitionKeyProperty { get; set; }
    public string? SortKeyProperty { get; set; }
    public string[] ProjectedProperties { get; set; }
}

public class RelationshipModel
{
    public string PropertyName { get; set; }
    public string SortKeyPattern { get; set; }
    public string? EntityType { get; set; }
    public bool IsCollection { get; set; }
}
```

### Computed Key Generation Logic

**Generated ToDynamoDb Method with Computed Keys:**
```csharp
public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity) 
    where TSelf : IDynamoDbEntity
{
    if (entity is not Customer typedEntity)
        throw new ArgumentException($"Expected Customer, got {entity.GetType().Name}");
    
    // Compute composite keys before mapping
    typedEntity.Pk = $"{typedEntity.TenantId}#{typedEntity.CustomerId}";
    typedEntity.StatusIndexPk = $"STATUS#{typedEntity.Status}";
    
    var item = new Dictionary<string, AttributeValue>();
    
    // Map computed keys to DynamoDB
    item["pk"] = new AttributeValue { S = typedEntity.Pk };
    item["gsi1_pk"] = new AttributeValue { S = typedEntity.StatusIndexPk };
    item["status"] = new AttributeValue { S = typedEntity.Status };
    
    return item;
}
```

**Generated FromDynamoDb Method with Extracted Keys:**
```csharp
public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item) 
    where TSelf : IDynamoDbEntity
{
    if (typeof(TSelf) != typeof(Customer))
        throw new ArgumentException($"Expected Customer, got {typeof(TSelf).Name}");
    
    var entity = new Customer();
    
    // Map from DynamoDB first
    if (item.TryGetValue("pk", out var pkValue))
        entity.Pk = pkValue.S;
    
    if (item.TryGetValue("status", out var statusValue))
        entity.Status = statusValue.S;
    
    // Extract component properties from composite keys
    if (!string.IsNullOrEmpty(entity.Pk))
    {
        var pkParts = entity.Pk.Split('#');
        if (pkParts.Length >= 2)
        {
            entity.TenantId = pkParts[0];
            entity.CustomerId = pkParts[1];
        }
    }
    
    return (TSelf)(object)entity;
}
```

**Generated Key Builder Methods:**
```csharp
public static partial class CustomerKeys
{
    public static string Pk(string tenantId, string customerId)
    {
        return $"{tenantId}#{customerId}";
    }
    
    public static string StatusIndexPk(string status)
    {
        return $"STATUS#{status}";
    }
    
    // Extraction helpers
    public static (string TenantId, string CustomerId) ExtractPkComponents(string pk)
    {
        var parts = pk.Split('#');
        return parts.Length >= 2 ? (parts[0], parts[1]) : (string.Empty, string.Empty);
    }
}
```

## Error Handling

### Compilation Errors
```csharp
public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MissingPartitionKey = new(
        "DYNDB001",
        "Missing partition key",
        "Entity '{0}' must have exactly one property marked with [PartitionKey]",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor InvalidKeyFormat = new(
        "DYNDB002", 
        "Invalid key format",
        "Property '{0}' has invalid key format: {1}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor ConflictingEntityTypes = new(
        "DYNDB003",
        "Conflicting entity types",
        "Multiple entities in table '{0}' have conflicting sort key patterns",
        "DynamoDb", 
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor InvalidComputedKeySource = new(
        "DYNDB004",
        "Invalid computed key source",
        "Computed property '{0}' references non-existent source property '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor InvalidExtractedKeySource = new(
        "DYNDB005",
        "Invalid extracted key source",
        "Extracted property '{0}' references non-existent source property '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor CircularKeyDependency = new(
        "DYNDB006",
        "Circular key dependency",
        "Circular dependency detected between computed properties: {0}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
```

### Runtime Error Handling
```csharp
public static class MappingErrorHandler
{
    public static T HandleMappingError<T>(Exception ex, Dictionary<string, AttributeValue> item)
    {
        var itemJson = System.Text.Json.JsonSerializer.Serialize(
            item.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()));
            
        throw new DynamoDbMappingException(
            $"Failed to map DynamoDB item to {typeof(T).Name}. " +
            $"Item: {itemJson}. " +
            $"Error: {ex.Message}", ex);
    }
}
```

## Testing Strategy

### Unit Tests
- **Attribute Analysis Tests**: Verify correct parsing of entity attributes
- **Code Generation Tests**: Validate generated code syntax and correctness  
- **Mapping Logic Tests**: Test entity to/from DynamoDB conversion
- **Error Handling Tests**: Verify appropriate error messages and diagnostics

### Integration Tests
- **End-to-End Scenarios**: Test complete workflows with real DynamoDB operations
- **Multi-Item Entity Tests**: Verify complex entity mapping scenarios
- **Related Entity Tests**: Test relationship mapping and filtering
- **Performance Tests**: Ensure generated code performs well

### Source Generator Tests
```csharp
[Test]
public void GenerateEntity_WithBasicAttributes_ProducesCorrectCode()
{
    var source = @"
        [DynamoDbTable(""test-table"")]
        public partial class TestEntity
        {
            [PartitionKey]
            [DynamoDbAttribute(""pk"")]
            public string Id { get; set; }
            
            [DynamoDbAttribute(""name"")]
            public string Name { get; set; }
        }";
    
    var result = GenerateCode(source);
    
    Assert.That(result.GeneratedSources, Has.Count.EqualTo(3)); // Entity, Fields, Keys
    Assert.That(result.Diagnostics, Is.Empty);
    
    var entityCode = result.GeneratedSources[0].SourceText.ToString();
    Assert.That(entityCode, Contains.Substring("public partial class TestEntity : IDynamoDbEntity"));
    Assert.That(entityCode, Contains.Substring("public static Dictionary<string, AttributeValue> ToDynamoDb"));
}
```

### STS Integration Example

```csharp
// Service layer handles STS token generation
public class TransactionService
{
    private readonly TransactionsTable _table;
    private readonly IStsTokenService _stsService;
    
    public async Task<Result<TransactionEntry>> GetTransactionAsync(
        Ulid tenantId, Ulid transactionId, 
        HttpContext httpContext)
    {
        // Generate STS token with tenant-specific policy based on request context
        var policy = CreateTenantPolicy(tenantId, httpContext.User.Claims);
        var scopedClient = await _stsService.CreateScopedClientAsync(policy);
        
        // Generated method accepts scoped client
        return await _table.GetTransactionAsync(tenantId, transactionId, scopedClient);
    }
    
    private string CreateTenantPolicy(Ulid tenantId, IEnumerable<Claim> claims)
    {
        // Create IAM policy with tenant ID constraints
        return $@"{{
            ""Version"": ""2012-10-17"",
            ""Statement"": [{{
                ""Effect"": ""Allow"",
                ""Action"": [""dynamodb:GetItem"", ""dynamodb:Query""],
                ""Resource"": ""arn:aws:dynamodb:*:*:table/transactions"",
                ""Condition"": {{
                    ""ForAllValues:StringLike"": {{
                        ""dynamodb:LeadingKeys"": [""{tenantId}#*""]
                    }}
                }}
            }}]
        }}";
    }
}

// Generated table method signature
public async Task<Result<TransactionEntry>> GetTransactionAsync(
    Ulid tenantId, Ulid transactionId,
    IAmazonDynamoDB? scopedClient = null,
    CancellationToken cancellationToken = default)
{
    var effectiveClient = scopedClient ?? DynamoDbClient;
    
    return await Query
        .WithClient(effectiveClient)
        .Where("{0} = {1}", TransactionFields.Pk, TransactionKeys.Pk(tenantId, transactionId))
        .ExecuteAsync<TransactionEntry>(cancellationToken);
}
```

This design provides a comprehensive foundation for the DynamoDB Source Generator while maintaining flexibility for future enhancements like LINQ support and STS credential management. The modular architecture allows for incremental development and testing of individual components.