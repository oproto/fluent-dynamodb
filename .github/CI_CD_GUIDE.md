# CI/CD Integration Guide

This guide explains the CI/CD setup for Oproto.FluentDynamoDb integration tests.

## Overview

The CI/CD pipeline runs both unit and integration tests across multiple platforms (Linux, Windows, macOS) to ensure cross-platform compatibility.

## Workflow Structure

### Main Workflow: `integration-tests.yml`

Located at `.github/workflows/integration-tests.yml`, this workflow:

1. **Runs on multiple platforms**: Ubuntu, Windows, and macOS
2. **Separates test types**: Unit tests and integration tests run separately
3. **Manages DynamoDB Local**: Automatically downloads and starts DynamoDB Local
4. **Uploads test results**: Separate artifacts for unit and integration tests
5. **Provides diagnostics**: Captures logs on failure

### Trigger Events

The workflow runs on:
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual trigger via `workflow_dispatch`

## Platform-Specific Handling

### Linux (Ubuntu)

- Uses `wget` to download DynamoDB Local
- Uses standard `tar` for extraction
- Java typically available via apt packages

### Windows

- Uses PowerShell `Invoke-WebRequest` for downloads
- Uses `tar.exe` (built into Windows 10+) for extraction
- Java paths checked in Program Files and common locations

### macOS

- Uses `wget` (available via Homebrew) for downloads
- Uses standard `tar` for extraction
- Java paths checked in Homebrew and system locations

## Test Result Reporting

### Artifact Structure

Test results are uploaded as separate artifacts:

```
unit-test-results-ubuntu-latest/
  └── unit-test-results.trx

integration-test-results-ubuntu-latest/
  └── integration-test-results.trx

unit-test-results-windows-latest/
  └── unit-test-results.trx

integration-test-results-windows-latest/
  └── integration-test-results.trx

unit-test-results-macos-latest/
  └── unit-test-results.trx

integration-test-results-macos-latest/
  └── integration-test-results.trx
```

### Test Summary Job

A separate `test-summary` job runs after all tests complete and:

1. Downloads all test result artifacts
2. Generates a summary in the GitHub Actions summary page
3. Shows platform coverage (which platforms passed/failed)
4. Provides links to detailed results

### Viewing Test Results

1. **In GitHub Actions UI**:
   - Navigate to Actions tab
   - Click on the workflow run
   - View the "Test Summary" section
   - Download artifacts for detailed TRX files

2. **Using Scripts**:
   ```bash
   # Generate local report (Linux/macOS)
   .github/scripts/generate-test-report.sh ./TestResults test-report.md
   
   # Generate local report (Windows)
   .github/scripts/generate-test-report.ps1 -ResultsDir ./TestResults -OutputFile test-report.md
   ```

## DynamoDB Local Management

### Download and Setup

DynamoDB Local is automatically downloaded during the workflow:

```yaml
- name: Download DynamoDB Local (Linux/macOS)
  if: runner.os != 'Windows'
  run: |
    mkdir -p dynamodb-local
    cd dynamodb-local
    wget -q https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz
    tar -xzf dynamodb_local_latest.tar.gz
```

### Environment Variables

- `DYNAMODB_LOCAL_PATH`: Path to DynamoDB Local installation
  - Set automatically in CI/CD: `${{ github.workspace }}/dynamodb-local`
  - Can be overridden locally for custom installations

### Lifecycle Management

The `DynamoDbLocalFixture` class handles:

1. **Detection**: Checks if DynamoDB Local is already running
2. **Download**: Downloads if not present (respects `DYNAMODB_LOCAL_PATH`)
3. **Startup**: Starts Java process with appropriate arguments
4. **Health Check**: Waits for service to be ready (max 30 seconds)
5. **Cleanup**: Stops process on test completion (if started by fixture)

## Failure Diagnostics

### On Test Failure

The workflow automatically collects diagnostic information:

1. **DynamoDB Local Logs**: Captured from stdout/stderr
2. **Process Information**: Lists running Java processes
3. **Directory Contents**: Shows DynamoDB Local installation
4. **Test Results**: All TRX files uploaded as artifacts

### Accessing Diagnostics

1. Navigate to the failed workflow run
2. Scroll to "Collect DynamoDB Local Logs" step
3. View inline logs in the step output
4. Download `dynamodb-logs-{platform}` artifact for full logs

## Local Development

### Running Tests Locally

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run integration tests for specific project
dotnet test Oproto.FluentDynamoDb.IntegrationTests
```

### Prerequisites

1. **.NET 8 SDK**: Required for building and running tests
2. **Java 17+**: Required for DynamoDB Local
   - Check: `java -version`
   - Install: See [Java Installation](#java-installation)

### Java Installation

#### Ubuntu/Debian
```bash
sudo apt update
sudo apt install openjdk-17-jdk
```

#### macOS
```bash
brew install openjdk@17
```

#### Windows
Download from [Adoptium](https://adoptium.net/) or [Amazon Corretto](https://aws.amazon.com/corretto/)

### Manual DynamoDB Local Setup

If you prefer to manage DynamoDB Local manually:

```bash
# Download
mkdir dynamodb-local
cd dynamodb-local
wget https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz
tar -xzf dynamodb_local_latest.tar.gz

# Start
java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -inMemory -port 8000

# In another terminal, run tests
dotnet test Oproto.FluentDynamoDb.IntegrationTests
```

## Troubleshooting

### Java Not Found

**Symptom**: Tests fail with "Java not found" or similar error

**Solutions**:
1. Ensure Java 17+ is installed: `java -version`
2. Set `JAVA_HOME` environment variable
3. Add Java to PATH

### DynamoDB Local Won't Start

**Symptom**: Tests timeout waiting for DynamoDB Local

**Solutions**:
1. Check if port 8000 is already in use
   - Linux/macOS: `lsof -i :8000`
   - Windows: `netstat -ano | findstr :8000`
2. Check Java version (must be 8+, recommended 17+)
3. Review DynamoDB Local logs in test output

### Tests Fail on Specific Platform

**Symptom**: Tests pass on some platforms but fail on others

**Solutions**:
1. Check platform-specific logs in workflow artifacts
2. Review `DynamoDbLocalFixture` platform detection logic
3. Verify Java installation on failing platform
4. Check file path handling (Windows uses backslashes)

### Cleanup Issues

**Symptom**: Tests fail due to tables from previous runs

**Solutions**:
1. Ensure `DisposeAsync` is called (xUnit should handle this)
2. Check for orphaned DynamoDB Local processes
3. Restart DynamoDB Local manually if needed

## Performance Optimization

### Fixture Reuse

The `DynamoDbLocalFixture` is shared across test classes using xUnit's collection fixture:

```csharp
[Collection("DynamoDB Local")]
public class MyTests : IntegrationTestBase
{
    // Fixture is shared, reducing startup time
}
```

### Parallel Execution

Tests run in parallel by default. Each test uses unique table names to avoid conflicts:

```csharp
protected string TableName => $"test_{GetType().Name}_{Guid.NewGuid():N}";
```

### CI/CD Optimization

- DynamoDB Local is downloaded once per platform
- Test results are uploaded in parallel
- Matrix strategy runs platforms concurrently

## Metrics and Monitoring

### Key Metrics

Track these metrics over time:

1. **Test Execution Time**: Total time for test suite
2. **Platform Success Rate**: Pass rate per platform
3. **Flaky Test Rate**: Tests that intermittently fail
4. **DynamoDB Local Startup Time**: Time to start service

### Viewing Metrics

1. GitHub Actions provides built-in metrics
2. Use workflow run history to track trends
3. Download test result artifacts for detailed analysis

## Best Practices

### Writing Tests

1. **Use IntegrationTestBase**: Inherit from base class for common functionality
2. **Unique Table Names**: Always use unique names to avoid conflicts
3. **Proper Cleanup**: Implement `DisposeAsync` for cleanup
4. **Meaningful Assertions**: Use FluentAssertions for readable tests

### CI/CD Configuration

1. **Keep Workflows Simple**: Avoid complex bash scripts in YAML
2. **Use Artifacts**: Upload important files for debugging
3. **Fail Fast**: Set `fail-fast: false` to see all platform results
4. **Clear Naming**: Use descriptive job and step names

### Maintenance

1. **Update Dependencies**: Keep .NET SDK and Java versions current
2. **Monitor Flaky Tests**: Address intermittent failures promptly
3. **Review Logs**: Check DynamoDB Local logs for warnings
4. **Update Documentation**: Keep this guide current with changes

## Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [DynamoDB Local Documentation](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET Testing Documentation](https://docs.microsoft.com/en-us/dotnet/core/testing/)
