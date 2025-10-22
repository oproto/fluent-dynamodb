# Test Failure Analysis Guide

This guide explains how to use the test failure analysis system to categorize, diagnose, and resolve test failures efficiently.

## Overview

The failure analysis system automatically categorizes test failures by type, severity, and likelihood of being flaky. This helps teams:

- **Prioritize fixes** based on severity
- **Identify patterns** in failures
- **Detect flaky tests** that need stabilization
- **Get actionable suggestions** for resolution

## Quick Start

### Automatic Analysis

Failure analysis runs automatically when tests fail. No configuration needed!

```bash
# Run tests - failures are automatically analyzed
dotnet test

# View failure analysis in console output
# Analysis report appears after test execution
```

### Manual Analysis

You can also analyze failures programmatically:

```csharp
var analyzer = new TestFailureAnalyzer();

try
{
    // Your test code
}
catch (Exception ex)
{
    var failure = analyzer.AnalyzeFailure("MyTest", ex);
    
    Console.WriteLine($"Type: {failure.FailureType}");
    Console.WriteLine($"Severity: {failure.Severity}");
    Console.WriteLine($"Flaky: {failure.IsFlaky}");
    Console.WriteLine($"Action: {failure.SuggestedAction}");
}
```

## Failure Categories

### Failure Types

| Type | Description | Common Causes |
|------|-------------|---------------|
| **Compilation** | Generated code doesn't compile | Source generator bugs, missing references |
| **Assertion** | Test assertion failed | Logic errors, incorrect expectations |
| **Timeout** | Operation exceeded time limit | Slow operations, deadlocks, resource contention |
| **Network** | Network connectivity issues | Connection failures, DNS issues |
| **DynamoDB** | DynamoDB-specific errors | Table not found, item not found, throttling |
| **NullReference** | Null reference exception | Missing null checks, uninitialized objects |
| **InvalidArgument** | Invalid argument passed | Bad test data, validation failures |
| **SetupTeardown** | Test setup/cleanup failed | Resource initialization issues |
| **Runtime** | General runtime error | Various causes, check stack trace |

### Severity Levels

| Severity | Icon | Description | Priority |
|----------|------|-------------|----------|
| **Critical** | 🔴 | Blocks all tests, must fix immediately | P0 |
| **High** | 🟠 | Affects core functionality | P1 |
| **Medium** | 🟡 | Affects specific features | P2 |
| **Low** | 🟢 | Minor issues, edge cases | P3 |

### Flaky Test Detection

Tests are marked as potentially flaky if they exhibit:

- ⏱️ Timeout-related failures
- 🌐 Network connectivity issues
- 🔄 Resource availability problems
- 📊 Intermittent assertion failures

## Using Failure Analysis

### In Console Output

After test execution, you'll see:

```
╔════════════════════════════════════════════════════════════════╗
║           TEST FAILURE ANALYSIS REPORT                         ║
╚════════════════════════════════════════════════════════════════╝

Generated: 2025-01-15 10:30:00 UTC
Total Failures: 5

═══ FAILURES BY TYPE ═══
  Timeout                    3 (60.0%)
  Assertion                  2 (40.0%)

═══ FAILURES BY SEVERITY ═══
  🟠 High                    3 (60.0%)
  🟡 Medium                  2 (40.0%)

═══ POTENTIALLY FLAKY TESTS ═══
  ⚠️  HashSetIntegrationTests.SaveAndLoad_WithLargeSet
  ⚠️  QueryOperationsTests.Query_WithComplexFilter

═══ DETAILED FAILURE LIST ═══
🟠 HashSetIntegrationTests.SaveAndLoad_WithLargeSet
   Type:     Timeout
   Severity: High
   Message:  Operation timed out after 30 seconds
   ⚠️  Potentially flaky
   💡 Action: Increase timeout values or optimize slow operations
```

### In CI/CD

Failure analysis is automatically included in GitHub Actions:

1. **Workflow Summary** - High-level failure breakdown
2. **Artifacts** - Detailed JSON reports for download
3. **PR Comments** - Failure summary on pull requests (if configured)

### Exporting Reports

```bash
# Set environment variable to export failure analysis
export TEST_FAILURES_EXPORT_PATH=./test-failures.json

# Run tests
dotnet test

# View JSON report
cat test-failures.json | jq
```

## Interpreting Results

### Example: Timeout Failures

```
🟡 MyIntegrationTest.SlowOperation
   Type:     Timeout
   Severity: Medium
   Message:  The operation has timed out
   ⚠️  Potentially flaky
   💡 Action: Increase timeout values or optimize slow operations
```

**What to do:**

1. **Check if test is actually slow** - Profile the operation
2. **Increase timeout if reasonable** - Some operations are legitimately slow
3. **Optimize if possible** - Reduce data size, parallelize operations
4. **Mark as flaky** - Add `[Trait("Flaky", "true")]` if intermittent

### Example: Assertion Failures

```
🟠 MapperGeneratorTests.GenerateMapper_WithComplexType
   Type:     Assertion
   Severity: High
   Message:  Expected string to contain "HashSet<int>", but found "List<int>"
   💡 Action: Review test expectations. Actual behavior may have changed.
```

**What to do:**

1. **Verify expected behavior** - Is the test expectation correct?
2. **Check recent changes** - Did code behavior change intentionally?
3. **Update test or fix code** - Align test with correct behavior

### Example: DynamoDB Failures

```
🟠 ComplexEntityTests.SaveAndLoad_WithAllTypes
   Type:     DynamoDB
   Severity: High
   Message:  Requested resource not found: Table: test_table_123
   💡 Action: Check DynamoDB Local is running. Verify table setup.
```

**What to do:**

1. **Verify DynamoDB Local is running** - Check process and port 8000
2. **Check table creation** - Review `InitializeAsync` in test
3. **Verify cleanup** - Ensure previous test cleaned up properly

## Common Patterns

### Pattern 1: Cascading Failures

**Symptom:** Multiple tests fail with same error

```
═══ COMMON FAILURE PATTERNS ═══
1. DynamoDB (15 occurrences)
   Examples:
   - HashSetIntegrationTests.Test1
   - ListIntegrationTests.Test2
   - DictionaryIntegrationTests.Test3
```

**Likely Cause:** Infrastructure issue (DynamoDB Local not running)

**Solution:** Fix infrastructure, all tests should pass

### Pattern 2: Flaky Test Cluster

**Symptom:** Same tests fail intermittently

```
═══ POTENTIALLY FLAKY TESTS ═══
  ⚠️  QueryOperationsTests.Query_WithTimeout
  ⚠️  QueryOperationsTests.Query_WithLargeDataset
  ⚠️  QueryOperationsTests.Query_WithComplexFilter
```

**Likely Cause:** Timing-sensitive operations in query tests

**Solution:** 
- Increase timeouts
- Add retry logic
- Stabilize test data

### Pattern 3: Compilation Failures

**Symptom:** All generator tests fail with compilation errors

```
═══ FAILURES BY TYPE ═══
  Compilation                25 (100.0%)
```

**Likely Cause:** Source generator bug affecting all generated code

**Solution:** Fix generator logic, all tests should pass

## Best Practices

### 1. Review Failure Reports Regularly

```bash
# Generate failure report after test run
./scripts/generate-test-metrics.sh

# Look for patterns in failures
# Prioritize by severity
```

### 2. Track Flaky Tests

Create a tracking issue for flaky tests:

```markdown
## Flaky Test: HashSetIntegrationTests.SaveAndLoad

**Failure Type:** Timeout
**Frequency:** 2-3 times per week
**Severity:** Medium

**Analysis:**
- Fails when DynamoDB Local is under load
- Timeout occurs during large data save

**Action Items:**
- [ ] Increase timeout from 30s to 60s
- [ ] Reduce test data size
- [ ] Add retry logic
```

### 3. Use Failure Data for Prioritization

**Priority Matrix:**

| Severity | Frequency | Priority | Action |
|----------|-----------|----------|--------|
| Critical | Any | P0 | Fix immediately |
| High | > 50% | P0 | Fix immediately |
| High | < 50% | P1 | Fix this sprint |
| Medium | > 80% | P1 | Fix this sprint |
| Medium | < 80% | P2 | Fix next sprint |
| Low | Any | P3 | Backlog |

### 4. Automate Failure Tracking

```yaml
# .github/workflows/track-failures.yml
- name: Track Failures
  if: failure()
  run: |
    # Extract failure data
    FAILURES=$(cat TestResults/test-failures-*.json)
    
    # Create GitHub issue for critical failures
    if [ $(echo "$FAILURES" | jq '.failuresBySeverity.Critical') -gt 0 ]; then
      gh issue create \
        --title "Critical Test Failures Detected" \
        --body "$(echo "$FAILURES" | jq -r '.failures[] | select(.severity == "Critical")')" \
        --label "bug,critical,tests"
    fi
```

## Troubleshooting

### No Failure Analysis Generated

**Symptoms:** Tests fail but no analysis report appears

**Solutions:**

1. Ensure `TestMetricsCollector` is used:
   ```csharp
   [Collection("Test Metrics")]
   public class MyTests { }
   ```

2. Check environment variables:
   ```bash
   export TEST_FAILURES_EXPORT_PATH=./failures.json
   ```

3. Verify test framework integration

### Incorrect Failure Categorization

**Symptoms:** Failure type doesn't match actual issue

**Solutions:**

1. **Review exception message** - Categorization is based on exception type and message
2. **Improve exception messages** - Use descriptive messages in code
3. **Customize analyzer** - Extend `TestFailureAnalyzer` for custom logic

### Missing Suggested Actions

**Symptoms:** No suggested action for failure

**Solutions:**

1. **Check failure type** - Some types have generic suggestions
2. **Add custom suggestions** - Extend `GetSuggestedAction` method
3. **Review stack trace** - Manual analysis may be needed

## Integration with Other Tools

### Codecov

Failure analysis can be correlated with coverage data:

```bash
# Generate both reports
./scripts/generate-coverage-report.sh
./scripts/generate-test-metrics.sh

# Analyze: Are failures in low-coverage areas?
```

### GitHub Issues

Auto-create issues for critical failures:

```bash
# Parse failure report
CRITICAL=$(jq '.failures[] | select(.severity == "Critical")' failures.json)

# Create issue
gh issue create --title "Critical Failure" --body "$CRITICAL"
```

### Slack/Teams Notifications

Send failure summaries to team channels:

```bash
# Extract summary
SUMMARY=$(jq -r '.failuresByType' failures.json)

# Send to Slack
curl -X POST $SLACK_WEBHOOK -d "{\"text\": \"$SUMMARY\"}"
```

## Summary

| Task | Command/Action |
|------|----------------|
| View failure analysis | Automatic in console output |
| Export to JSON | Set `TEST_FAILURES_EXPORT_PATH` env var |
| Identify flaky tests | Check "Potentially Flaky Tests" section |
| Prioritize fixes | Sort by severity (Critical → Low) |
| Track patterns | Review "Common Failure Patterns" |

**Key Metrics:**
- Failure rate by type
- Flaky test count
- Critical failure count
- Time to resolution

Use failure analysis to improve test reliability and reduce debugging time!
