# Design Document

## Overview

This design upgrades the testing infrastructure for Oproto.FluentDynamoDb source generator from brittle string-matching tests to a robust, maintainable testing approach. The solution introduces three complementary test layers:

1. **Integration Tests**: Verify generated code works with actual DynamoDB Local
2. **Compilation Tests**: Ensure generated code compiles without errors
3. **Semantic Tests**: Replace string matching with syntax tree analysis

The design follows a pragmatic migration strategy that adds new capabilities without requiring a big-bang rewrite of existing tests. It prioritizes high-value improvements (integration tests) first, adds safety nets (compilation verification) to existing tests, and provides a gradual migration path for improving test quality over time.

## Architecture

### Test Project Structure

```
Solution Root/
├── Oproto.FluentDynamoDb.SourceGenerator.UnitTests/     # Existing unit tests
│   ├── Generators/
│   │   ├── AdvancedTypeGenerationTests.cs               # Keep existing, add compilation checks
│   │   ├── MapperGeneratorTests.cs
│   │   └── ...
│   └── TestUtilities/
│       ├── CompilationVerifier.cs                       # NEW: Verify generated code compiles
│       └── SemanticAssertions.cs                        # NEW: Syntax tree helpers
│
├── Oproto.FluentDynamoDb.IntegrationTests/              # NEW: Integration test project
│   ├── Infrastructure/
│   │   ├── DynamoDbLocalFixture.cs                      # Manages DynamoDB Local lifecycle
│   │   ├── IntegrationTestBase.cs                       # Base class for integration tests
│   │   └── TestTableManager.cs                          # Creates/deletes test tables
│   ├── AdvancedTypes/
│   │   ├── HashSetIntegrationTests.cs                   # Test HashSet round-trips
│   │   ├── ListIntegrationTests.cs                      # Test List round-trips
│   │   ├── DictionaryIntegrationTests.cs                # Test Dictionary round-trips
│   │   └── MixedTypesIntegrationTests.cs                # Test complex combinations
│   ├── BasicTypes/
│   │   ├── StringPropertyTests.cs                       # Test basic string properties
│   │   ├── NumericPropertyTests.cs                      # Test numeric types
│   │   └── CrudOperationsTests.cs                       # Test basic CRUD
│   ├── RealWorld/
│   │   ├── ComplexEntityTests.cs                        # Test realistic entities
│   │   ├── QueryOperationsTests.cs                      # Test queries with advanced types
│   │   └── TransactionTests.cs                          # Test transactions
│   └── TestEntities/
│       ├── AdvancedTypesEntity.cs                       # Entity with all advanced types
│       ├── BasicEntity.cs                               # Simple entity
│       └── Builders/
│           └── EntityBuilders.cs                        # Test data builders
│
└── scripts/
    ├── setup-dynamodb-local.sh                          # Download/setup DynamoDB Local
    └── run-integration-tests.sh                         # Run integration tests locally
```


### Design Principles

1. **Incremental Migration**: Add new capabilities without breaking existing tests
2. **High Value First**: Prioritize integration tests that prove code works
3. **Safety Nets**: Add compilation verification to catch breaking changes
4. **Pragmatic Approach**: Don't require perfect tests, just better tests
5. **Developer Experience**: Make it easy to write and maintain tests

## Components and Interfaces

### 1. DynamoDB Local Fixture

Manages the lifecycle of DynamoDB Local for integration tests using xUnit's collection fixture pattern.

```csharp
public class DynamoDbLocalFixture : IAsyncLifetime
{
    private Process? _dynamoDbProcess;
    public IAmazonDynamoDB Client { get; private set; } = null!;
    public string ServiceUrl { get; private set; } = "http://localhost:8000";
    
    public async Task InitializeAsync()
    {
        // 1. Check if DynamoDB Local is already running
        if (await IsDynamoDbLocalRunning())
        {
            Client = CreateClient();
            return;
        }
        
        // 2. Download DynamoDB Local if not present
        await EnsureDynamoDbLocalInstalled();
        
        // 3. Start DynamoDB Local process
        _dynamoDbProcess = StartDynamoDbLocal();
        
        // 4. Wait for service to be ready
        await WaitForDynamoDbLocal();
        
        // 5. Create client
        Client = CreateClient();
    }
    
    public async Task DisposeAsync()
    {
        // Stop DynamoDB Local if we started it
        if (_dynamoDbProcess != null && !_dynamoDbProcess.HasExited)
        {
            _dynamoDbProcess.Kill();
            await _dynamoDbProcess.WaitForExitAsync();
        }
        
        Client?.Dispose();
    }
    
    private IAmazonDynamoDB CreateClient()
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = "us-east-1"
        };
        
        return new AmazonDynamoDBClient(
            new BasicAWSCredentials("dummy", "dummy"),
            config);
    }
    
    private async Task<bool> IsDynamoDbLocalRunning()
    {
        try
        {
            using var client = CreateClient();
            await client.ListTablesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private Process StartDynamoDbLocal()
    {
        var javaPath = FindJavaExecutable();
        var dynamoDbJar = GetDynamoDbLocalJarPath();
        
        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"-Djava.library.path=./DynamoDBLocal_lib -jar {dynamoDbJar} -inMemory -port 8000",
            WorkingDirectory = GetDynamoDbLocalDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        var process = Process.Start(startInfo);
        
        // Capture output for debugging
        process.OutputDataReceived += (sender, e) => 
            Console.WriteLine($"[DynamoDB Local] {e.Data}");
        process.ErrorDataReceived += (sender, e) => 
            Console.WriteLine($"[DynamoDB Local ERROR] {e.Data}");
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        return process;
    }
}

// Collection definition for sharing fixture across test classes
[CollectionDefinition("DynamoDB Local")]
public class DynamoDbLocalCollection : ICollectionFixture<DynamoDbLocalFixture>
{
}
```

### 2. Integration Test Base Class

Provides common functionality for integration tests including table management and cleanup.

```csharp
[Collection("DynamoDB Local")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IAmazonDynamoDB DynamoDb { get; }
    protected string TableName { get; }
    private readonly List<string> _tablesToCleanup = new();
    
    protected IntegrationTestBase(DynamoDbLocalFixture fixture)
    {
        DynamoDb = fixture.Client;
        TableName = $"test_{GetType().Name}_{Guid.NewGuid():N}";
    }
    
    public virtual async Task InitializeAsync()
    {
        // Override in derived classes to create tables
    }
    
    public virtual async Task DisposeAsync()
    {
        // Clean up all tables created during tests
        foreach (var tableName in _tablesToCleanup)
        {
            try
            {
                await DynamoDb.DeleteTableAsync(tableName);
            }
            catch (ResourceNotFoundException)
            {
                // Table already deleted, ignore
            }
        }
    }
    
    protected async Task CreateTableAsync<TEntity>() where TEntity : IDynamoDbEntity
    {
        var metadata = TEntity.GetEntityMetadata();
        
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = metadata.Properties
                        .First(p => p.IsPartitionKey).AttributeName,
                    KeyType = KeyType.HASH
                }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName = metadata.Properties
                        .First(p => p.IsPartitionKey).AttributeName,
                    AttributeType = ScalarAttributeType.S
                }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };
        
        // Add sort key if present
        var sortKeyProp = metadata.Properties.FirstOrDefault(p => p.IsSortKey);
        if (sortKeyProp != null)
        {
            request.KeySchema.Add(new KeySchemaElement
            {
                AttributeName = sortKeyProp.AttributeName,
                KeyType = KeyType.RANGE
            });
            
            request.AttributeDefinitions.Add(new AttributeDefinition
            {
                AttributeName = sortKeyProp.AttributeName,
                AttributeType = ScalarAttributeType.S
            });
        }
        
        await DynamoDb.CreateTableAsync(request);
        _tablesToCleanup.Add(TableName);
        
        // Wait for table to be active
        await WaitForTableActive(TableName);
    }
    
    protected async Task<TEntity> SaveAndLoadAsync<TEntity>(TEntity entity) 
        where TEntity : IDynamoDbEntity, new()
    {
        // Save entity
        var item = TEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Load entity back
        var partitionKey = TEntity.GetPartitionKey(item);
        var getRequest = new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [GetPartitionKeyAttributeName<TEntity>()] = new AttributeValue { S = partitionKey }
            }
        };
        
        var response = await DynamoDb.GetItemAsync(getRequest);
        return TEntity.FromDynamoDb<TEntity>(response.Item);
    }
}
```


### 3. Compilation Verifier

Utility to verify generated code compiles without errors.

```csharp
public static class CompilationVerifier
{
    public static void AssertGeneratedCodeCompiles(
        string sourceCode,
        params string[] additionalSources)
    {
        var compilation = CreateCompilation(sourceCode, additionalSources);
        
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        
        if (diagnostics.Any())
        {
            var errorMessage = new StringBuilder();
            errorMessage.AppendLine("Generated code has compilation errors:");
            errorMessage.AppendLine();
            
            foreach (var diagnostic in diagnostics)
            {
                errorMessage.AppendLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
                errorMessage.AppendLine($"  Location: {diagnostic.Location.GetLineSpan()}");
                errorMessage.AppendLine();
            }
            
            errorMessage.AppendLine("Generated Source:");
            errorMessage.AppendLine(sourceCode);
            
            throw new XunitException(errorMessage.ToString());
        }
    }
    
    private static CSharpCompilation CreateCompilation(
        string sourceCode,
        params string[] additionalSources)
    {
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(sourceCode)
        };
        
        syntaxTrees.AddRange(additionalSources.Select(CSharpSyntaxTree.ParseText));
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(AttributeValue).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDynamoDbEntity).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
        };
        
        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
```

### 4. Semantic Assertions

Utilities for verifying code structure using syntax tree analysis.

```csharp
public static class SemanticAssertions
{
    public static void ShouldContainMethod(
        this string sourceCode,
        string methodName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        
        var hasMethod = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.Identifier.Text == methodName);
        
        if (!hasMethod)
        {
            throw new XunitException(
                $"Expected source code to contain method '{methodName}' {because}\n" +
                $"Available methods: {string.Join(", ", GetMethodNames(root))}");
        }
    }
    
    public static void ShouldContainAssignment(
        this string sourceCode,
        string targetName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        
        var hasAssignment = root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(a => a.Left.ToString().Contains(targetName));
        
        if (!hasAssignment)
        {
            throw new XunitException(
                $"Expected source code to contain assignment to '{targetName}' {because}");
        }
    }
    
    public static void ShouldUseLinqMethod(
        this string sourceCode,
        string methodName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        
        var hasLinqCall = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression.ToString().EndsWith($".{methodName}"));
        
        if (!hasLinqCall)
        {
            throw new XunitException(
                $"Expected source code to use LINQ method '{methodName}' {because}");
        }
    }
    
    public static void ShouldReferenceType(
        this string sourceCode,
        string typeName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        
        var hasTypeReference = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == typeName);
        
        if (!hasTypeReference)
        {
            throw new XunitException(
                $"Expected source code to reference type '{typeName}' {because}");
        }
    }
    
    private static IEnumerable<string> GetMethodNames(SyntaxNode root)
    {
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(m => m.Identifier.Text);
    }
}
```

### 5. Test Entity Builders

Fluent builders for creating test entities with various configurations.

```csharp
public class AdvancedTypesEntityBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private HashSet<int>? _categoryIds;
    private HashSet<string>? _tags;
    private List<string>? _itemIds;
    private List<decimal>? _prices;
    private Dictionary<string, string>? _metadata;
    
    public AdvancedTypesEntityBuilder WithId(string id)
    {
        _id = id;
        return this;
    }
    
    public AdvancedTypesEntityBuilder WithCategoryIds(params int[] ids)
    {
        _categoryIds = new HashSet<int>(ids);
        return this;
    }
    
    public AdvancedTypesEntityBuilder WithTags(params string[] tags)
    {
        _tags = new HashSet<string>(tags);
        return this;
    }
    
    public AdvancedTypesEntityBuilder WithItemIds(params string[] ids)
    {
        _itemIds = new List<string>(ids);
        return this;
    }
    
    public AdvancedTypesEntityBuilder WithPrices(params decimal[] prices)
    {
        _prices = new List<decimal>(prices);
        return this;
    }
    
    public AdvancedTypesEntityBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        _metadata = metadata;
        return this;
    }
    
    public AdvancedTypesEntity Build()
    {
        return new AdvancedTypesEntity
        {
            Id = _id,
            CategoryIds = _categoryIds,
            Tags = _tags,
            ItemIds = _itemIds,
            Prices = _prices,
            Metadata = _metadata
        };
    }
}

// Usage in tests:
var entity = new AdvancedTypesEntityBuilder()
    .WithCategoryIds(1, 2, 3)
    .WithTags("new", "featured")
    .WithPrices(9.99m, 19.99m)
    .Build();
```


## Example Test Implementations

### Integration Test Example

```csharp
[Collection("DynamoDB Local")]
public class HashSetIntegrationTests : IntegrationTestBase
{
    public HashSetIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<AdvancedTypesEntity>();
    }
    
    [Fact]
    public async Task HashSetInt_RoundTrip_PreservesAllValues()
    {
        // Arrange
        var entity = new AdvancedTypesEntityBuilder()
            .WithId("test-1")
            .WithCategoryIds(1, 2, 3, 5, 8)
            .Build();
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
    }
    
    [Fact]
    public async Task HashSetString_RoundTrip_PreservesAllValues()
    {
        // Arrange
        var entity = new AdvancedTypesEntityBuilder()
            .WithId("test-2")
            .WithTags("new", "featured", "sale")
            .Build();
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Tags.Should().BeEquivalentTo(entity.Tags);
    }
    
    [Fact]
    public async Task HashSet_WithNullValue_LoadsAsNull()
    {
        // Arrange
        var entity = new AdvancedTypesEntityBuilder()
            .WithId("test-3")
            .Build(); // CategoryIds is null
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.CategoryIds.Should().BeNull();
    }
    
    [Fact]
    public async Task HashSet_WithEmptySet_OmitsFromDynamoDB()
    {
        // Arrange
        var entity = new AdvancedTypesEntityBuilder()
            .WithId("test-4")
            .WithCategoryIds() // Empty set
            .Build();
        
        // Act
        var item = AdvancedTypesEntity.ToDynamoDb(entity);
        
        // Assert - Empty sets should not be stored
        item.Should().NotContainKey("category_ids");
    }
}
```

### Updated Generator Test with Compilation Verification

```csharp
public class AdvancedTypeGenerationTests
{
    [Fact]
    public void Generator_WithHashSetInt_GeneratesNumberSetConversion()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; }
        
        [DynamoDbAttribute(""category_ids"")]
        public HashSet<int>? CategoryIds { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode);
        
        // Assert - Semantic checks (more maintainable than string matching)
        entityCode.ShouldContainMethod("ToDynamoDb");
        entityCode.ShouldContainAssignment("category_ids");
        entityCode.ShouldUseLinqMethod("Select");
        
        // Assert - Keep critical string checks for DynamoDB-specific behavior
        entityCode.Should().Contain("NS =", "should use Number Set for HashSet<int>");
    }
}
```

### Real-world Scenario Test

```csharp
[Collection("DynamoDB Local")]
public class ComplexEntityTests : IntegrationTestBase
{
    public ComplexEntityTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<ComplexEntity>();
    }
    
    [Fact]
    public async Task ComplexEntity_WithAllAdvancedTypes_RoundTripsCorrectly()
    {
        // Arrange - Entity with multiple advanced types
        var entity = new ComplexEntity
        {
            Id = "complex-1",
            CategoryIds = new HashSet<int> { 1, 2, 3 },
            Tags = new HashSet<string> { "tag1", "tag2" },
            ItemIds = new List<string> { "item1", "item2", "item3" },
            Prices = new List<decimal> { 9.99m, 19.99m, 29.99m },
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - All properties preserved
        loaded.Should().BeEquivalentTo(entity, options => options
            .ComparingByMembers<ComplexEntity>());
    }
}
```

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Integration Tests

on: [push, pull_request]

jobs:
  integration-tests:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Setup Java (for DynamoDB Local)
        uses: actions/setup-java@v3
        with:
          distribution: 'temurin'
          java-version: '17'
      
      - name: Download DynamoDB Local
        run: |
          wget https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz
          tar -xzf dynamodb_local_latest.tar.gz -C ./dynamodb-local
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Run Unit Tests
        run: dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests --no-build --verbosity normal
      
      - name: Run Integration Tests
        run: dotnet test Oproto.FluentDynamoDb.IntegrationTests --no-build --verbosity normal
        env:
          DYNAMODB_LOCAL_PATH: ./dynamodb-local
      
      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: '**/TestResults/*.trx'
```


## Migration Strategy

### Phase 1: Foundation (Week 1-2)

**Goal**: Establish integration test infrastructure and prove value

1. Create `Oproto.FluentDynamoDb.IntegrationTests` project
2. Implement `DynamoDbLocalFixture` with lifecycle management
3. Create `IntegrationTestBase` with table management
4. Write 10-15 integration tests for advanced types:
   - HashSet<int>, HashSet<string>, HashSet<byte[]>
   - List<string>, List<int>, List<decimal>
   - Dictionary<string, string>
   - Complex entities with multiple types
5. Set up CI/CD to run integration tests
6. Document how to run tests locally

**Success Criteria**:
- Integration tests run in CI/CD
- Tests catch real bugs that unit tests miss
- Developers can run tests locally with simple command

### Phase 2: Safety Net (Week 3)

**Goal**: Add compilation verification to existing tests

1. Create `CompilationVerifier` utility class
2. Add `AssertGeneratedCodeCompiles()` to all generator tests
3. Fix any compilation issues discovered
4. Document the compilation verification pattern

**Success Criteria**:
- All existing generator tests verify compilation
- No false positives from compilation checks
- Clear error messages when compilation fails

### Phase 3: Semantic Assertions (Week 4-5)

**Goal**: Provide better alternatives to string matching

1. Create `SemanticAssertions` utility class
2. Document semantic assertion patterns
3. Update 5-10 high-churn tests to use semantic assertions
4. Create examples in documentation

**Success Criteria**:
- Semantic assertion utilities are easy to use
- Examples show clear benefits over string matching
- Developers prefer semantic assertions for new tests

### Phase 4: Gradual Migration (Ongoing)

**Goal**: Improve test quality over time

1. When fixing bugs, improve related tests
2. When adding features, use new test patterns
3. Periodically review and update high-maintenance tests
4. Track metrics on test maintenance burden

**Success Criteria**:
- Test maintenance time decreases over time
- New tests use better patterns
- No big-bang rewrites required

## Testing Strategy

### Test Pyramid

```
                    /\
                   /  \
                  / E2E\ (5%)
                 /------\
                /        \
               /Integration\ (25%)
              /------------\
             /              \
            /   Unit Tests   \ (70%)
           /------------------\
```

**Unit Tests (70%)**:
- Fast, isolated tests of individual components
- Generator logic, analyzers, utilities
- Keep existing tests, add compilation verification

**Integration Tests (25%)**:
- Test generated code with real DynamoDB
- Verify round-trips, queries, updates
- Focus on advanced types and complex scenarios

**E2E Tests (5%)**:
- Real-world scenarios with multiple features
- Complex entities, transactions, queries
- Run less frequently (nightly builds)

### Test Categories

Use xUnit traits to categorize tests:

```csharp
[Trait("Category", "Unit")]
public class MapperGeneratorTests { }

[Trait("Category", "Integration")]
[Collection("DynamoDB Local")]
public class HashSetIntegrationTests { }

[Trait("Category", "E2E")]
[Collection("DynamoDB Local")]
public class ComplexScenarioTests { }
```

Run specific categories:
```bash
# Unit tests only (fast feedback)
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"

# All tests
dotnet test
```

## Error Handling and Diagnostics

### DynamoDB Local Startup Failures

```csharp
public async Task InitializeAsync()
{
    try
    {
        await StartDynamoDbLocal();
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "Failed to start DynamoDB Local. " +
            "Ensure Java is installed and DynamoDB Local is downloaded. " +
            "Run: ./scripts/setup-dynamodb-local.sh\n" +
            $"Error: {ex.Message}", ex);
    }
}
```

### Compilation Failures

```csharp
if (diagnostics.Any())
{
    var errorMessage = new StringBuilder();
    errorMessage.AppendLine("Generated code has compilation errors:");
    errorMessage.AppendLine();
    
    foreach (var diagnostic in diagnostics)
    {
        errorMessage.AppendLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
        errorMessage.AppendLine($"  Location: {diagnostic.Location.GetLineSpan()}");
    }
    
    errorMessage.AppendLine();
    errorMessage.AppendLine("Generated Source:");
    errorMessage.AppendLine("================");
    errorMessage.AppendLine(sourceCode);
    
    throw new XunitException(errorMessage.ToString());
}
```

### Test Isolation Failures

```csharp
public async Task DisposeAsync()
{
    var cleanupErrors = new List<Exception>();
    
    foreach (var tableName in _tablesToCleanup)
    {
        try
        {
            await DynamoDb.DeleteTableAsync(tableName);
        }
        catch (Exception ex)
        {
            cleanupErrors.Add(ex);
            Console.WriteLine($"Warning: Failed to cleanup table {tableName}: {ex.Message}");
        }
    }
    
    // Don't fail the test due to cleanup issues, but log them
    if (cleanupErrors.Any())
    {
        Console.WriteLine($"Cleanup completed with {cleanupErrors.Count} errors");
    }
}
```

## Performance Considerations

### DynamoDB Local Reuse

- Start DynamoDB Local once per test run (collection fixture)
- Reuse client across all tests
- Use unique table names to avoid conflicts
- Target: < 30 seconds for full integration test suite

### Parallel Test Execution

```csharp
// Each test class gets unique table name
protected string TableName => $"test_{GetType().Name}_{Guid.NewGuid():N}";

// Tests can run in parallel without conflicts
[Collection("DynamoDB Local")] // Shares fixture, not state
public class HashSetIntegrationTests : IntegrationTestBase
{
    // Each test creates its own table
}
```

### Compilation Caching

```csharp
private static readonly ConcurrentDictionary<string, CSharpCompilation> _compilationCache = new();

public static void AssertGeneratedCodeCompiles(string sourceCode)
{
    var cacheKey = ComputeHash(sourceCode);
    
    var compilation = _compilationCache.GetOrAdd(cacheKey, _ => 
        CreateCompilation(sourceCode));
    
    // Check diagnostics...
}
```

## Documentation

### README for Integration Tests

```markdown
# Integration Tests

## Prerequisites

- .NET 8 SDK
- Java 17+ (for DynamoDB Local)

## Setup

1. Download DynamoDB Local:
   ```bash
   ./scripts/setup-dynamodb-local.sh
   ```

2. Run tests:
   ```bash
   dotnet test Oproto.FluentDynamoDb.IntegrationTests
   ```

## Running Specific Tests

```bash
# Run only HashSet tests
dotnet test --filter "FullyQualifiedName~HashSet"

# Run only integration tests
dotnet test --filter "Category=Integration"
```

## Troubleshooting

### DynamoDB Local won't start

- Ensure Java is installed: `java -version`
- Check if port 8000 is available: `lsof -i :8000`
- View DynamoDB Local logs in test output

### Tests fail with "Table not found"

- Check table creation in test setup
- Verify table name matches between setup and test
- Ensure cleanup from previous test run completed
```

### Migration Guide

```markdown
# Test Migration Guide

## Adding Compilation Verification

Before:
```csharp
[Fact]
public void Generator_CreatesCode()
{
    var result = GenerateCode(source);
    var code = GetGeneratedSource(result, "Entity.g.cs");
    code.Should().Contain("public class");
}
```

After:
```csharp
[Fact]
public void Generator_CreatesCode()
{
    var result = GenerateCode(source);
    var code = GetGeneratedSource(result, "Entity.g.cs");
    
    // Add compilation check
    CompilationVerifier.AssertGeneratedCodeCompiles(code);
    
    code.Should().Contain("public class");
}
```

## Replacing String Checks with Semantic Assertions

Before:
```csharp
code.Should().Contain("public static string Pk(string id)");
code.Should().Contain("var keyValue = \"tenant#\" + id;");
```

After:
```csharp
code.ShouldContainMethod("Pk");
code.ShouldContainAssignment("keyValue");
code.Should().Contain("tenant#", "should use tenant prefix");
```

## Creating Integration Tests

```csharp
[Collection("DynamoDB Local")]
public class MyFeatureIntegrationTests : IntegrationTestBase
{
    public MyFeatureIntegrationTests(DynamoDbLocalFixture fixture) 
        : base(fixture) { }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<MyEntity>();
    }
    
    [Fact]
    public async Task MyFeature_WorksCorrectly()
    {
        var entity = new MyEntity { /* ... */ };
        var loaded = await SaveAndLoadAsync(entity);
        loaded.Should().BeEquivalentTo(entity);
    }
}
```
```

## Success Metrics

Track these metrics to measure improvement:

1. **Test Maintenance Time**: Time spent fixing broken tests after refactoring
2. **False Positive Rate**: Tests that fail due to formatting changes, not bugs
3. **Bug Detection Rate**: Bugs caught by integration tests vs unit tests
4. **Test Execution Time**: Time to run full test suite
5. **Developer Satisfaction**: Survey developers on test quality

**Target Improvements**:
- 50% reduction in test maintenance time within 6 months
- 80% reduction in false positives from formatting changes
- 2x increase in bugs caught by integration tests
- Integration test suite runs in < 30 seconds
- 80%+ developer satisfaction with test infrastructure
