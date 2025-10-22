# Test Filtering Guide

This guide explains how to run specific subsets of tests for faster feedback during development.

## Test Categories

Tests are organized into categories using xUnit traits:

- **Unit**: Fast, isolated tests of individual components
- **Integration**: Tests that interact with DynamoDB Local
- **E2E**: End-to-end tests of complex scenarios (future)

## Running Tests by Category

### Command Line

```bash
# Run only unit tests (fast feedback)
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run all tests
dotnet test
```

### Visual Studio

1. Open Test Explorer (Test > Test Explorer)
2. Click the filter icon
3. Select "Traits"
4. Choose the category you want to run

### Rider

1. Open Unit Tests window (View > Tool Windows > Unit Tests)
2. Right-click on a test or category
3. Select "Run" or "Debug"
4. Use the filter box to search by category

## Running Tests by Name Pattern

### Specific Test Class

```bash
# Run all tests in HashSetIntegrationTests
dotnet test --filter "FullyQualifiedName~HashSetIntegrationTests"

# Run all tests in a namespace
dotnet test --filter "FullyQualifiedName~AdvancedTypes"
```

### Specific Test Method

```bash
# Run a specific test
dotnet test --filter "FullyQualifiedName~HashSetInt_RoundTrip_PreservesAllValues"

# Run tests matching a pattern
dotnet test --filter "FullyQualifiedName~RoundTrip"
```

## Running Tests by Project

```bash
# Run only integration tests project
dotnet test Oproto.FluentDynamoDb.IntegrationTests

# Run only unit tests project
dotnet test Oproto.FluentDynamoDb.UnitTests

# Run only source generator tests
dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests
```

## Combining Filters

### AND Logic

```bash
# Run integration tests in AdvancedTypes namespace
dotnet test --filter "Category=Integration&FullyQualifiedName~AdvancedTypes"
```

### OR Logic

```bash
# Run tests in multiple namespaces
dotnet test --filter "FullyQualifiedName~HashSet|FullyQualifiedName~List"
```

## Watch Mode

Run tests automatically when code changes:

```bash
# Watch all tests
dotnet watch test

# Watch specific category
dotnet watch test --filter "Category=Unit"

# Watch specific project
dotnet watch test --project Oproto.FluentDynamoDb.IntegrationTests
```

## Performance Considerations

### Test Execution Times (Approximate)

- **Unit Tests**: < 5 seconds for full suite
- **Integration Tests**: 20-30 seconds for full suite
- **All Tests**: 30-40 seconds for full suite

### Optimization Tips

1. **Run Unit Tests First**: Get fast feedback on logic errors
2. **Run Integration Tests Before Commit**: Verify DynamoDB interactions
3. **Use Watch Mode**: Automatically run affected tests
4. **Filter by Feature**: Run only tests related to your changes

## CI/CD Integration

### GitHub Actions

The CI/CD pipeline runs tests in this order:

1. **Unit Tests**: Run first for fast feedback
2. **Integration Tests**: Run after unit tests pass
3. **Platform Matrix**: Run on Linux, Windows, macOS

### Local Pre-Commit

Recommended workflow before committing:

```bash
# 1. Run unit tests (fast)
dotnet test --filter "Category=Unit"

# 2. If unit tests pass, run integration tests
dotnet test --filter "Category=Integration"

# 3. If all tests pass, commit
git commit -m "Your message"
```

## Troubleshooting

### No Tests Found

**Symptom**: Filter returns no tests

**Solutions**:
1. Check filter syntax (case-sensitive)
2. Verify trait is applied to test class
3. Use `--list-tests` to see available tests:
   ```bash
   dotnet test --list-tests
   ```

### Tests Run Slowly

**Symptom**: Integration tests take too long

**Solutions**:
1. Ensure DynamoDB Local is already running (reuse instance)
2. Check for network issues (DynamoDB Local should be local)
3. Run fewer tests using filters
4. Check for resource contention (CPU/memory)

### Tests Fail Intermittently

**Symptom**: Tests pass sometimes, fail other times

**Solutions**:
1. Check for shared state between tests
2. Verify table names are unique
3. Ensure proper cleanup in `DisposeAsync`
4. Review DynamoDB Local logs for errors

## Examples

### Development Workflow

```bash
# Working on HashSet feature
dotnet watch test --filter "FullyQualifiedName~HashSet"

# Working on mapper generator
dotnet watch test --filter "FullyQualifiedName~MapperGenerator"

# Quick sanity check before commit
dotnet test --filter "Category=Unit"
```

### Debugging Specific Test

```bash
# Run single test with verbose output
dotnet test --filter "FullyQualifiedName~HashSetInt_RoundTrip_PreservesAllValues" --logger "console;verbosity=detailed"
```

### Performance Testing

```bash
# Measure integration test performance
time dotnet test --filter "Category=Integration"

# Measure specific test class
time dotnet test --filter "FullyQualifiedName~HashSetIntegrationTests"
```

## Best Practices

1. **Run Unit Tests Frequently**: They're fast and catch most issues
2. **Run Integration Tests Before Push**: Verify DynamoDB interactions
3. **Use Filters During Development**: Focus on relevant tests
4. **Run Full Suite Before PR**: Ensure nothing broke
5. **Monitor Test Times**: Keep tests fast for good developer experience

## Additional Resources

- [xUnit Filtering Documentation](https://xunit.net/docs/running-tests-in-vs#filtering)
- [dotnet test Documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
- [Test Explorer Documentation](https://docs.microsoft.com/en-us/visualstudio/test/run-unit-tests-with-test-explorer)
