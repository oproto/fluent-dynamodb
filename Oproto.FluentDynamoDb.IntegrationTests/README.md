# Integration Tests

This project contains integration tests for Oproto.FluentDynamoDb that verify the library works correctly with actual DynamoDB operations using DynamoDB Local.

## Quick Start

```bash
# Run all tests
dotnet test

# Run only unit tests (fast, no DynamoDB Local required)
dotnet test --filter "Category=Unit"

# Run only integration tests (requires DynamoDB Local)
dotnet test --filter "Category=Integration"
```

For more filtering options, see the [Test Filtering Guide](./TEST_FILTERING_GUIDE.md).

## Prerequisites

Before running integration tests, ensure you have the following installed:

### Required Software

1. **.NET 8 SDK**
   ```bash
   dotnet --version  # Should be 8.0 or higher
   ```

2. **Java 17 or higher** (required for DynamoDB Local)
   ```bash
   java -version  # Should be 17 or higher
   ```
   
   **Installation:**
   - **macOS**: `brew install openjdk@17`
   - **Ubuntu/Debian**: `sudo apt install openjdk-17-jdk`
   - **Windows**: Download from [Adoptium](https://adoptium.net/)

### Optional Tools

- **AWS CLI** (for manual DynamoDB Local testing)
- **NoSQL Workbench** (for visualizing DynamoDB tables)

## Setup Process

### Automatic Setup (Recommended)

The integration tests automatically manage DynamoDB Local:

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd Oproto.FluentDynamoDb
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Run integration tests**
   ```bash
   dotnet test Oproto.FluentDynamoDb.IntegrationTests
   ```

The test fixture will automatically:
- Check if DynamoDB Local is running
- Download DynamoDB Local if not present
- Start DynamoDB Local before tests
- Stop DynamoDB Local after tests complete

### Manual Setup (Optional)

If you prefer to manage DynamoDB Local manually:

1. **Download DynamoDB Local**
   ```bash
   mkdir -p ./dynamodb-local
   cd ./dynamodb-local
   wget https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz
   tar -xzf dynamodb_local_latest.tar.gz
   ```

2. **Start DynamoDB Local**
   ```bash
   java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -inMemory -port 8000
   ```

3. **Run tests** (in another terminal)
   ```bash
   dotnet test Oproto.FluentDynamoDb.IntegrationTests
   ```

## Running Tests Locally

### Run All Integration Tests

```bash
dotnet test Oproto.FluentDynamoDb.IntegrationTests
```

### Run Tests by Category

All tests are categorized using xUnit traits for easy filtering:

```bash
# Run only integration tests (requires DynamoDB Local)
dotnet test --filter "Category=Integration"

# Run only unit tests (fast, no external dependencies)
dotnet test --filter "Category=Unit"

# Run all tests
dotnet test
```

### Run Specific Test Categories

```bash
# Run only advanced type tests
dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "FullyQualifiedName~AdvancedTypes"

# Run only HashSet tests
dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "FullyQualifiedName~HashSet"

# Run only List tests
dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "FullyQualifiedName~List"

# Run only Dictionary tests
dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "FullyQualifiedName~Dictionary"

# Run only real-world scenario tests
dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "FullyQualifiedName~RealWorld"
```

### Run Specific Test Class

```bash
dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "FullyQualifiedName~HashSetIntegrationTests"
```

### Run with Detailed Output

```bash
dotnet test Oproto.FluentDynamoDb.IntegrationTests --verbosity detailed
```

### Run with Logger

```bash
dotnet test Oproto.FluentDynamoDb.IntegrationTests --logger "console;verbosity=detailed"
```

## Test Filtering Guide

### Understanding Test Categories

Tests are organized using xUnit `[Trait]` attributes:

- **`Category=Unit`**: Fast unit tests with no external dependencies (mocks, in-memory)
- **`Category=Integration`**: Integration tests that require DynamoDB Local

### Filter Syntax

The `--filter` option supports various expressions:

#### Basic Filters

```bash
# Single category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"

# Specific test class
dotnet test --filter "FullyQualifiedName~HashSetIntegrationTests"

# Specific test method
dotnet test --filter "FullyQualifiedName~HashSetInt_RoundTrip_PreservesAllValues"
```

#### Combining Filters

```bash
# OR operator (|)
dotnet test --filter "Category=Unit|Category=Integration"

# AND operator (&)
dotnet test --filter "Category=Integration&FullyQualifiedName~HashSet"

# NOT operator (!=)
dotnet test --filter "Category!=Integration"  # Run only unit tests

# Complex expressions
dotnet test --filter "(Category=Integration)&(FullyQualifiedName~AdvancedTypes|FullyQualifiedName~RealWorld)"
```

#### Namespace Filters

```bash
# All tests in a namespace
dotnet test --filter "FullyQualifiedName~Oproto.FluentDynamoDb.IntegrationTests.AdvancedTypes"

# Multiple namespaces
dotnet test --filter "FullyQualifiedName~AdvancedTypes|FullyQualifiedName~RealWorld"
```

### Common Filtering Scenarios

#### Development Workflow

```bash
# Fast feedback during development (unit tests only)
dotnet test --filter "Category=Unit"

# Verify integration before commit
dotnet test --filter "Category=Integration"

# Run tests for specific feature
dotnet test --filter "FullyQualifiedName~HashSet"
```

#### CI/CD Workflow

```bash
# Run unit tests first (fast feedback)
dotnet test --filter "Category=Unit" --logger "trx;LogFileName=unit-tests.trx"

# Run integration tests separately
dotnet test --filter "Category=Integration" --logger "trx;LogFileName=integration-tests.trx"

# Run all tests
dotnet test --logger "trx;LogFileName=all-tests.trx"
```

#### Debugging Workflow

```bash
# Run single failing test
dotnet test --filter "FullyQualifiedName~MyFailingTest"

# Run all tests in failing class
dotnet test --filter "FullyQualifiedName~MyFailingTestClass"

# Run with detailed output
dotnet test --filter "FullyQualifiedName~MyFailingTest" --verbosity detailed
```

### Filter Performance Tips

1. **Use specific filters** for faster execution:
   ```bash
   # Slower (runs all integration tests)
   dotnet test Oproto.FluentDynamoDb.IntegrationTests
   
   # Faster (runs only HashSet tests)
   dotnet test --filter "FullyQualifiedName~HashSet"
   ```

2. **Run unit tests first** for quick feedback:
   ```bash
   dotnet test --filter "Category=Unit"  # Fast
   dotnet test --filter "Category=Integration"  # Slower (requires DynamoDB Local)
   ```

3. **Use watch mode** during development:
   ```bash
   dotnet watch test --filter "Category=Unit"
   ```

### Filter Examples by Use Case

#### Feature Development

```bash
# Working on HashSet support
dotnet test --filter "FullyQualifiedName~HashSet"

# Working on query operations
dotnet test --filter "FullyQualifiedName~Query"

# Working on update operations
dotnet test --filter "FullyQualifiedName~Update"
```

#### Bug Investigation

```bash
# Reproduce specific bug
dotnet test --filter "FullyQualifiedName~BugReproductionTest" --verbosity detailed

# Test all related functionality
dotnet test --filter "FullyQualifiedName~ComplexEntity" --verbosity detailed
```

#### Pre-Commit Checks

```bash
# Quick check (unit tests only)
dotnet test --filter "Category=Unit"

# Full check (all tests)
dotnet test

# Integration tests for changed area
dotnet test --filter "Category=Integration&FullyQualifiedName~AdvancedTypes"
```

### Troubleshooting Filters

#### No Tests Match Filter

**Symptom**: "No test matches the given testcase filter"

**Solutions**:

1. **Check trait spelling**:
   ```bash
   # Wrong
   dotnet test --filter "Category=integration"  # Case-sensitive!
   
   # Correct
   dotnet test --filter "Category=Integration"
   ```

2. **Verify test has trait**:
   ```csharp
   [Trait("Category", "Integration")]  // Ensure trait is present
   public class MyTests { }
   ```

3. **Use broader filter**:
   ```bash
   # Too specific
   dotnet test --filter "FullyQualifiedName=Exact.Match.Required"
   
   # Better (uses contains)
   dotnet test --filter "FullyQualifiedName~MyTest"
   ```

#### Filter Syntax Errors

**Symptom**: "Invalid filter expression"

**Solutions**:

1. **Quote complex expressions**:
   ```bash
   # Wrong
   dotnet test --filter Category=Unit&FullyQualifiedName~Test
   
   # Correct
   dotnet test --filter "Category=Unit&FullyQualifiedName~Test"
   ```

2. **Check operator usage**:
   ```bash
   # AND: &
   dotnet test --filter "Category=Unit&FullyQualifiedName~Test"
   
   # OR: |
   dotnet test --filter "Category=Unit|Category=Integration"
   
   # NOT: !=
   dotnet test --filter "Category!=Integration"
   ```

## Test Organization

The integration tests are organized by feature area:

```
Oproto.FluentDynamoDb.IntegrationTests/
├── Infrastructure/           # Test infrastructure and fixtures
│   ├── DynamoDbLocalFixture.cs
│   ├── DynamoDbLocalCollection.cs
│   ├── IntegrationTestBase.cs
│   └── README.md
├── AdvancedTypes/           # Tests for HashSet, List, Dictionary
│   ├── HashSetIntegrationTests.cs
│   ├── ListIntegrationTests.cs
│   └── DictionaryIntegrationTests.cs
├── BasicTypes/              # Tests for basic type operations
├── RealWorld/               # Complex scenario tests
│   ├── ComplexEntityTests.cs
│   ├── QueryOperationsTests.cs
│   └── UpdateOperationsTests.cs
└── TestEntities/            # Test entity definitions
    ├── BasicTestEntity.cs
    ├── ComplexEntity.cs
    └── Builders/            # Test data builders
```

## How Integration Tests Work

### Test Lifecycle

1. **Fixture Initialization** (once per test run)
   - `DynamoDbLocalFixture` starts DynamoDB Local
   - Creates shared `IAmazonDynamoDB` client
   - Shared across all test classes via xUnit collection fixture

2. **Test Class Initialization** (once per test class)
   - `IntegrationTestBase.InitializeAsync()` is called
   - Test tables are created with unique names
   - Tables are tracked for cleanup

3. **Test Execution**
   - Each test runs independently
   - Tests use `SaveAndLoadAsync()` helper for round-trip testing
   - Tests can create additional tables as needed

4. **Test Class Cleanup** (once per test class)
   - `IntegrationTestBase.DisposeAsync()` is called
   - All tables created during tests are deleted
   - Cleanup failures are logged but don't fail tests

5. **Fixture Cleanup** (once per test run)
   - `DynamoDbLocalFixture.DisposeAsync()` is called
   - DynamoDB Local process is stopped (if started by fixture)

### Test Isolation

Each test class gets a unique table name to ensure isolation:

```csharp
protected string TableName => $"test_{GetType().Name}_{Guid.NewGuid():N}";
```

This allows tests to run in parallel without conflicts.

## Writing New Integration Tests

### Basic Pattern

```csharp
[Collection("DynamoDB Local")]
public class MyFeatureIntegrationTests : IntegrationTestBase
{
    public MyFeatureIntegrationTests(DynamoDbLocalFixture fixture) 
        : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<MyEntity>();
    }
    
    [Fact]
    public async Task MyFeature_Scenario_ExpectedBehavior()
    {
        // Arrange
        var entity = new MyEntity
        {
            Id = "test-1",
            // ... set properties
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Should().BeEquivalentTo(entity);
    }
}
```

### Using Test Builders

```csharp
[Fact]
public async Task ComplexEntity_RoundTrip_PreservesAllProperties()
{
    // Arrange - Use builder for cleaner test setup
    var entity = new AdvancedTypesEntityBuilder()
        .WithId("test-1")
        .WithCategoryIds(1, 2, 3)
        .WithTags("new", "featured")
        .WithPrices(9.99m, 19.99m)
        .Build();
    
    // Act
    var loaded = await SaveAndLoadAsync(entity);
    
    // Assert
    loaded.Should().BeEquivalentTo(entity);
}
```

## Performance Optimization

### Parallel Test Execution

Integration tests are configured to run in parallel for faster execution:

**Configuration** (`xunit.runner.json`):
```json
{
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": -1
}
```

**Key Features:**
- Tests within the same collection can run in parallel
- Each test gets a unique table name to prevent conflicts
- DynamoDB Local instance is shared across all tests (collection fixture)
- No shared state between test instances

**Verification:**
```bash
# Run parallel execution tests to verify isolation
dotnet test --filter "FullyQualifiedName~ParallelExecutionTests"
```

**Performance Benefits:**
- **DynamoDB Local Reuse**: Single instance shared across all tests (saves ~5-10 seconds per test run)
- **Parallel Execution**: Tests run concurrently on multiple threads
- **Unique Table Names**: Each test uses `test_{ClassName}_{Guid}` format to avoid conflicts

**Controlling Parallelization:**

To disable parallel execution (for debugging):
```bash
# Run tests sequentially
dotnet test -- xUnit.ParallelizeTestCollections=false
```

To limit parallel threads:
```bash
# Limit to 4 threads
dotnet test -- xUnit.MaxParallelThreads=4
```

### Test Execution Time

**Target Performance:**
- Full integration test suite: < 30 seconds
- Individual test: < 1 second (after DynamoDB Local startup)
- DynamoDB Local startup: ~5-10 seconds (first time only)

**Measuring Performance:**

Use the provided scripts to measure test performance:

```bash
# Linux/macOS
./measure-performance.sh

# Windows PowerShell
.\measure-performance.ps1

# Manual measurement with dotnet test
time dotnet test --filter "Category=Integration"
```

**Performance Tests:**

Run dedicated performance tests to verify targets:

```bash
# Run all performance tests
dotnet test --filter "FullyQualifiedName~PerformanceTests"

# Run specific performance test
dotnet test --filter "FullyQualifiedName~SingleTest_CompletesInUnder1Second"
```

**Optimization Tips:**
1. Keep DynamoDB Local running between test runs during development
2. Use test filtering to run only relevant tests during development
3. Run full suite in CI/CD where parallel execution provides maximum benefit
4. Monitor slow tests using the performance metrics utilities

## Troubleshooting

### DynamoDB Local Won't Start

**Symptom**: Tests fail with "Failed to start DynamoDB Local"

**Solutions**:

1. **Check Java installation**
   ```bash
   java -version
   ```
   Ensure Java 17 or higher is installed.

2. **Check if port 8000 is in use**
   ```bash
   # macOS/Linux
   lsof -i :8000
   
   # Windows
   netstat -ano | findstr :8000
   ```
   If port is in use, stop the process or configure a different port.

3. **Manually download DynamoDB Local**
   ```bash
   mkdir -p ./dynamodb-local
   cd ./dynamodb-local
   wget https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz
   tar -xzf dynamodb_local_latest.tar.gz
   ```

4. **Check DynamoDB Local logs**
   The fixture captures stdout/stderr. Run tests with detailed logging:
   ```bash
   dotnet test --verbosity detailed
   ```

### Tests Fail with "Table Not Found"

**Symptom**: `ResourceNotFoundException: Requested resource not found: Table: test_...`

**Solutions**:

1. **Verify table creation in InitializeAsync**
   ```csharp
   public override async Task InitializeAsync()
   {
       await CreateTableAsync<MyEntity>();  // Ensure this is called
   }
   ```

2. **Check table name consistency**
   Ensure you're using `TableName` property consistently:
   ```csharp
   await DynamoDb.PutItemAsync(TableName, item);  // Use TableName, not hardcoded string
   ```

3. **Wait for table to be active**
   The `CreateTableAsync` method includes a wait, but if you create tables manually:
   ```csharp
   await WaitForTableActive(tableName);
   ```

### Tests Are Slow

**Symptom**: Integration tests take longer than expected

**Solutions**:

1. **Reuse DynamoDB Local instance**
   The fixture already does this via collection fixture. Ensure tests use:
   ```csharp
   [Collection("DynamoDB Local")]
   ```

2. **Use in-memory mode**
   DynamoDB Local is configured with `-inMemory` flag for faster performance.

3. **Run tests in parallel**
   xUnit runs tests in parallel by default. Ensure unique table names:
   ```csharp
   protected string TableName => $"test_{GetType().Name}_{Guid.NewGuid():N}";
   ```

4. **Check for unnecessary waits**
   Avoid explicit `Task.Delay()` calls in tests.

### Cleanup Failures

**Symptom**: Warning messages about failed table cleanup

**Solutions**:

1. **Check DynamoDB Local is still running**
   If DynamoDB Local crashes, cleanup will fail. This is logged but doesn't fail tests.

2. **Verify table names**
   Ensure table names are tracked correctly:
   ```csharp
   _tablesToCleanup.Add(TableName);
   ```

3. **Manual cleanup**
   If tables persist, restart DynamoDB Local or delete manually:
   ```bash
   aws dynamodb delete-table --table-name <table-name> --endpoint-url http://localhost:8000
   ```

### Permission Errors

**Symptom**: Access denied or permission errors

**Solutions**:

1. **Check AWS credentials**
   DynamoDB Local uses dummy credentials, but ensure no real AWS credentials interfere:
   ```bash
   unset AWS_ACCESS_KEY_ID
   unset AWS_SECRET_ACCESS_KEY
   ```

2. **Verify endpoint configuration**
   Ensure tests use local endpoint:
   ```csharp
   ServiceURL = "http://localhost:8000"
   ```

### Java Not Found

**Symptom**: "java: command not found" or similar

**Solutions**:

1. **Install Java**
   See Prerequisites section above.

2. **Set JAVA_HOME**
   ```bash
   # macOS/Linux
   export JAVA_HOME=$(/usr/libexec/java_home -v 17)
   
   # Windows
   set JAVA_HOME=C:\Program Files\Java\jdk-17
   ```

3. **Add Java to PATH**
   ```bash
   # macOS/Linux
   export PATH=$JAVA_HOME/bin:$PATH
   
   # Windows
   set PATH=%JAVA_HOME%\bin;%PATH%
   ```

## CI/CD Integration

Integration tests run automatically in CI/CD pipelines across multiple platforms (Linux, Windows, macOS).

For comprehensive CI/CD documentation, see the [CI/CD Integration Guide](../.github/CI_CD_GUIDE.md).

### GitHub Actions

The workflow (`.github/workflows/integration-tests.yml`):
1. Sets up .NET 8 SDK
2. Sets up Java 17
3. Downloads DynamoDB Local (platform-specific)
4. Runs unit tests separately
5. Runs integration tests
6. Uploads test results as artifacts
7. Generates test summary report

### Platform Support

Tests run on:
- **Ubuntu Latest**: Linux environment
- **Windows Latest**: Windows environment  
- **macOS Latest**: macOS environment

Platform-specific handling ensures DynamoDB Local works correctly on all platforms.

### Test Result Artifacts

After each CI run, test results are available as artifacts:
- `unit-test-results-{platform}`: Unit test TRX files
- `integration-test-results-{platform}`: Integration test TRX files
- `dynamodb-logs-{platform}`: DynamoDB Local logs (on failure)

### Running Locally Like CI

To replicate CI environment locally:

```bash
# Download DynamoDB Local
mkdir -p ./dynamodb-local
cd ./dynamodb-local
wget https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz
tar -xzf dynamodb_local_latest.tar.gz
cd ..

# Run tests like CI
dotnet test --filter "Category=Unit" --logger "trx;LogFileName=unit-test-results.trx"
dotnet test Oproto.FluentDynamoDb.IntegrationTests --logger "trx;LogFileName=integration-test-results.trx"
```

## Performance Expectations

- **First run**: 10-15 seconds (includes DynamoDB Local startup)
- **Subsequent runs**: 5-10 seconds (DynamoDB Local already running)
- **Individual test**: < 1 second
- **Full suite**: < 30 seconds

If tests are significantly slower, see "Tests Are Slow" in Troubleshooting section.

## Additional Resources

### Project Documentation
- [Test Filtering Guide](./TEST_FILTERING_GUIDE.md) - Comprehensive guide to filtering tests by category, namespace, and more
- [CI/CD Integration Guide](../.github/CI_CD_GUIDE.md) - Complete CI/CD setup and troubleshooting guide
- [Test Migration Guide](./MIGRATION_GUIDE.md) - Guide for migrating existing tests
- [Test Writing Guide](./TEST_WRITING_GUIDE.md) - Best practices for writing tests

### External Documentation
- [DynamoDB Local Documentation](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)

## Getting Help

If you encounter issues not covered in this guide:

1. Check existing GitHub issues
2. Review test output with `--verbosity detailed`
3. Check DynamoDB Local logs in test output
4. Create a new GitHub issue with:
   - Error message
   - Test output
   - Environment details (OS, .NET version, Java version)
