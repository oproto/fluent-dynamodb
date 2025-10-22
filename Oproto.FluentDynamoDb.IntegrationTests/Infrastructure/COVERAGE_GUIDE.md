# Code Coverage Guide

This guide explains how to collect and analyze code coverage for the Oproto.FluentDynamoDb project.

## Quick Start

### Generate Coverage Report Locally

```bash
# Generate HTML coverage report (opens in browser on macOS)
./scripts/generate-coverage-report.sh html

# Generate all report formats
./scripts/generate-coverage-report.sh all

# Generate JSON summary only
./scripts/generate-coverage-report.sh json
```

### View Coverage in CI/CD

Coverage reports are automatically generated in GitHub Actions and available as artifacts:

1. Go to the Actions tab in GitHub
2. Select a workflow run
3. Download the `coverage-report-*` artifacts
4. Open `index.html` in a browser

## Coverage Configuration

### Settings File

Coverage collection is configured in `coverlet.runsettings`:

```xml
<Configuration>
  <Format>json,cobertura,lcov,opencover</Format>
  <Exclude>[*.Tests]*,[*.UnitTests]*,[*.IntegrationTests]*</Exclude>
  <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute</ExcludeByAttribute>
  <IncludeTestAssembly>false</IncludeTestAssembly>
</Configuration>
```

### What's Covered

- ✅ Main library code (`Oproto.FluentDynamoDb`)
- ✅ Source generator code (`Oproto.FluentDynamoDb.SourceGenerator`)
- ✅ Generated code (when executed in tests)
- ❌ Test projects (excluded)
- ❌ Auto-generated properties (excluded)

## Understanding Coverage Metrics

### Line Coverage

Percentage of executable code lines that were executed during tests.

**Target:** ≥ 70%

### Branch Coverage

Percentage of decision branches (if/else, switch, etc.) that were executed.

**Target:** ≥ 60%

### Coverage by Category

| Category | Description | Target |
|----------|-------------|--------|
| Core Library | Main DynamoDB operations | ≥ 80% |
| Source Generator | Code generation logic | ≥ 70% |
| Generated Code | Runtime execution of generated code | ≥ 60% |
| Utilities | Helper classes | ≥ 75% |

## Improving Coverage

### Identify Uncovered Code

1. Generate HTML report:
   ```bash
   ./scripts/generate-coverage-report.sh html
   ```

2. Open `TestResults/CoverageReport/index.html`

3. Navigate to specific files to see line-by-line coverage

4. Red lines = not covered, need tests

### Add Tests for Uncovered Code

**Priority Order:**

1. **Critical paths** - Core functionality, error handling
2. **Complex logic** - Branching, loops, calculations
3. **Edge cases** - Null handling, empty collections
4. **Generated code** - Verify source generator output works

### Example: Improving Coverage

```csharp
// Before: Uncovered error handling
public void ProcessItem(Item item)
{
    // This null check is not covered
    if (item == null)
        throw new ArgumentNullException(nameof(item));
    
    // Main logic is covered
    DoSomething(item);
}

// Add test to cover null check
[Fact]
public void ProcessItem_WithNull_ThrowsArgumentNullException()
{
    // Arrange
    Item? item = null;
    
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() => processor.ProcessItem(item!));
}
```

## Coverage in CI/CD

### GitHub Actions

Coverage is automatically collected and reported:

1. **Per-platform reports** - Separate coverage for Linux, Windows, macOS
2. **Aggregated summary** - Combined metrics in workflow summary
3. **Codecov integration** - Historical tracking and PR comments
4. **Artifacts** - Downloadable HTML reports

### Viewing in GitHub

The workflow summary shows:

```
📊 Code Coverage

| Metric | Coverage |
|--------|----------|
| Line Coverage | 75.3% |
| Branch Coverage | 68.2% |

✅ Coverage meets threshold (≥ 70%)
```

### Coverage Badges

Badges are generated in `TestResults/CoverageReport/badges/`:

- `badge_linecoverage.svg` - Line coverage badge
- `badge_branchcoverage.svg` - Branch coverage badge

Add to README:

```markdown
![Line Coverage](./TestResults/CoverageReport/badges/badge_linecoverage.svg)
![Branch Coverage](./TestResults/CoverageReport/badges/badge_branchcoverage.svg)
```

## Coverage for Generated Code

### Challenge

Source generators create code at compile time. Coverage tools need to track execution of this generated code.

### Solution

1. **Integration tests** execute generated code against real DynamoDB
2. **Compilation tests** verify generated code compiles
3. **Coverage collection** tracks execution in integration tests

### Verifying Generated Code Coverage

```bash
# Run integration tests with coverage
dotnet test Oproto.FluentDynamoDb.IntegrationTests \
    --collect:"XPlat Code Coverage" \
    --settings coverlet.runsettings

# Check coverage for generated files
# Look for files in: Oproto.FluentDynamoDb/generated/
```

## Troubleshooting

### No Coverage Data Collected

**Symptoms:** Coverage report shows 0% or "No coverage data"

**Solutions:**

1. Ensure `coverlet.collector` package is installed:
   ```bash
   dotnet add package coverlet.collector
   ```

2. Verify runsettings file is used:
   ```bash
   dotnet test --settings coverlet.runsettings
   ```

3. Check that tests actually run:
   ```bash
   dotnet test --verbosity normal
   ```

### Coverage Lower Than Expected

**Symptoms:** Coverage percentage seems too low

**Possible Causes:**

1. **Test filters** - Only running subset of tests
   ```bash
   # Run ALL tests for full coverage
   dotnet test
   ```

2. **Excluded files** - Check `coverlet.runsettings` exclusions

3. **Generated code not executed** - Integration tests may not cover all scenarios

### ReportGenerator Not Found

**Symptoms:** `reportgenerator: command not found`

**Solution:**

```bash
# Install globally
dotnet tool install --global dotnet-reportgenerator-globaltool

# Or update if already installed
dotnet tool update --global dotnet-reportgenerator-globaltool
```

## Best Practices

### 1. Run Coverage Regularly

```bash
# Before committing
./scripts/generate-coverage-report.sh html

# Check if coverage decreased
git diff TestResults/CoverageReport/Summary.json
```

### 2. Set Coverage Thresholds

In CI/CD, fail builds if coverage drops below threshold:

```yaml
- name: Check Coverage Threshold
  run: |
    COVERAGE=$(jq -r '.summary.linecoverage' TestResults/CoverageReport/Summary.json)
    if (( $(echo "$COVERAGE < 70" | bc -l) )); then
      echo "Coverage $COVERAGE% is below threshold 70%"
      exit 1
    fi
```

### 3. Focus on Meaningful Coverage

- ✅ Test business logic thoroughly
- ✅ Cover error handling paths
- ✅ Test edge cases
- ❌ Don't chase 100% coverage
- ❌ Don't test trivial getters/setters

### 4. Review Coverage in PRs

1. Generate coverage report locally
2. Compare with main branch
3. Ensure new code is covered
4. Add tests for uncovered critical paths

## Additional Resources

- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator Documentation](https://github.com/danielpalme/ReportGenerator)
- [Codecov Documentation](https://docs.codecov.com/)
- [xUnit Code Coverage](https://xunit.net/docs/getting-started/netcore/cmdline#run-tests-with-code-coverage)

## Summary

| Task | Command |
|------|---------|
| Generate HTML report | `./scripts/generate-coverage-report.sh html` |
| Generate all formats | `./scripts/generate-coverage-report.sh all` |
| Run tests with coverage | `dotnet test --collect:"XPlat Code Coverage"` |
| View report | Open `TestResults/CoverageReport/index.html` |
| Check threshold | See script output or Summary.json |

**Coverage Targets:**
- Overall: ≥ 70% line coverage
- Core library: ≥ 80% line coverage
- Source generator: ≥ 70% line coverage
