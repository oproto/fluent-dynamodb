# Test Metrics and Reporting System

Comprehensive test metrics, code coverage, and failure analysis for Oproto.FluentDynamoDb.

## Overview

This system provides three integrated reporting capabilities:

1. **📊 Test Execution Metrics** - Track test counts, execution times, and performance
2. **📈 Code Coverage** - Measure line and branch coverage across the codebase
3. **🔍 Failure Analysis** - Categorize and diagnose test failures automatically

## Quick Start

### Generate All Reports Locally

```bash
# Test metrics (text format)
./scripts/generate-test-metrics.sh text

# Code coverage (HTML report)
./scripts/generate-coverage-report.sh html

# Both metrics and coverage
./scripts/generate-test-metrics.sh all
./scripts/generate-coverage-report.sh all
```

### View Reports

```bash
# Metrics report
cat ./test-metrics-report.txt

# Coverage report (opens in browser on macOS)
open ./TestResults/CoverageReport/index.html

# Failure analysis (if tests failed)
cat ./TestResults/test-failures.json | jq
```

## Features

### 1. Test Execution Metrics

**What it tracks:**
- Total test count (unit vs integration)
- Pass/fail rates
- Execution time per test and category
- Performance against 30-second target
- Slowest tests

**Output formats:**
- Text (console-friendly)
- JSON (machine-readable)
- GitHub Summary (CI/CD dashboards)

**Example output:**

```
╔════════════════════════════════════════════════════════════════╗
║           TEST EXECUTION METRICS REPORT                        ║
╚════════════════════════════════════════════════════════════════╝

═══ OVERALL SUMMARY ═══
Total Tests:      150
Passed:           145 (96.7%)
Failed:           5 (3.3%)
Total Duration:   25.3s
Average Duration: 168.67ms

═══ BREAKDOWN BY CATEGORY ═══
┌─ Unit ─────
│  Tests:    100
│  Passed:   98 (98.0%)
│  Duration: 8.5s (avg: 85ms)
└────────────

┌─ Integration ─────
│  Tests:    50
│  Passed:   47 (94.0%)
│  Duration: 16.8s (avg: 336ms)
└────────────

═══ PERFORMANCE ASSESSMENT ═══
Target:  30.0s
Actual:  25.3s
Status:  ✓ MEETS TARGET
```

### 2. Code Coverage

**What it measures:**
- Line coverage percentage
- Branch coverage percentage
- Coverage by assembly/project
- Uncovered code locations

**Output formats:**
- HTML (interactive report)
- JSON (summary data)
- Cobertura XML (CI/CD integration)
- Badges (for README)

**Coverage targets:**
- Overall: ≥ 70% line coverage
- Core library: ≥ 80% line coverage
- Source generator: ≥ 70% line coverage

**Example output:**

```
═══════════════════════════════════════════════════════════════
  Coverage Summary
═══════════════════════════════════════════════════════════════

Overall Coverage:
  Line Coverage:   75.3%
  Branch Coverage: 68.2%

Coverage by Assembly:
  Oproto.FluentDynamoDb: 82.1% lines, 75.4% branches
  Oproto.FluentDynamoDb.SourceGenerator: 71.5% lines, 64.8% branches

═══════════════════════════════════════════════════════════════
  Coverage Threshold Check
═══════════════════════════════════════════════════════════════

  Threshold: 70%
  Actual:    75.3%

✓ Coverage meets threshold
```

### 3. Failure Analysis

**What it analyzes:**
- Failure type (Compilation, Assertion, Timeout, etc.)
- Severity level (Critical, High, Medium, Low)
- Flaky test detection
- Suggested actions for resolution

**Output formats:**
- Text (detailed analysis)
- JSON (structured data)
- GitHub Summary (CI/CD integration)

**Example output:**

```
╔════════════════════════════════════════════════════════════════╗
║           TEST FAILURE ANALYSIS REPORT                         ║
╚════════════════════════════════════════════════════════════════╝

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

## CI/CD Integration

### GitHub Actions

All reports are automatically generated in CI/CD:

**Workflow Summary includes:**
- Test execution metrics
- Code coverage summary
- Failure analysis (if tests fail)
- Performance target status

**Artifacts available:**
- `test-metrics-{os}` - JSON metrics per platform
- `coverage-report-{os}` - HTML coverage reports
- `test-failures-{os}` - JSON failure analysis (if failures)

**Example workflow summary:**

```markdown
# 📊 Test Execution Metrics

## Overall Summary

| Metric | Value |
|--------|-------|
| Total Tests | 150 |
| ✅ Passed | 145 (96.7%) |
| ❌ Failed | 5 (3.3%) |
| ⏱️ Total Duration | 25.3s |

## 📊 Code Coverage

| Metric | Coverage |
|--------|----------|
| Line Coverage | 75.3% |
| Branch Coverage | 68.2% |

✅ Coverage meets threshold (≥ 70%)

## 🔍 Failure Analysis

**Total Failures:** 5

### Failures by Type

- **Timeout**: 3 occurrences
- **Assertion**: 2 occurrences
```

### Environment Variables

Configure reporting behavior:

```bash
# Test metrics
export TEST_METRICS_EXPORT_PATH=./test-metrics.json
export TEST_METRICS_FORMAT=json  # text, json, github

# Failure analysis
export TEST_FAILURES_EXPORT_PATH=./test-failures.json

# GitHub Actions (automatic)
export GITHUB_STEP_SUMMARY=/path/to/summary.md
```

## Usage Examples

### Example 1: Daily Development

```bash
# Run tests with metrics
dotnet test

# View console output for quick feedback
# Metrics and failures appear automatically
```

### Example 2: Pre-Commit Check

```bash
# Generate coverage report
./scripts/generate-coverage-report.sh html

# Check if coverage decreased
git diff TestResults/CoverageReport/Summary.json

# Review uncovered code
open TestResults/CoverageReport/index.html
```

### Example 3: CI/CD Pipeline

```yaml
# .github/workflows/test.yml
- name: Run Tests with Metrics
  run: dotnet test
  env:
    TEST_METRICS_EXPORT_PATH: ./metrics.json
    TEST_FAILURES_EXPORT_PATH: ./failures.json

- name: Upload Reports
  uses: actions/upload-artifact@v4
  with:
    name: test-reports
    path: |
      ./metrics.json
      ./failures.json
      ./TestResults/CoverageReport/
```

### Example 4: Failure Investigation

```bash
# Run tests and capture failures
dotnet test

# View failure analysis
cat ./TestResults/test-failures.json | jq

# Filter by severity
jq '.failures[] | select(.severity == "Critical")' failures.json

# Find flaky tests
jq '.flakyTests[]' failures.json
```

## Report Locations

After running tests, reports are available at:

```
TestResults/
├── Coverage/                          # Raw coverage data
│   ├── Unit/
│   └── Integration/
├── CoverageReport/                    # Generated coverage reports
│   ├── index.html                     # Main coverage report
│   ├── Summary.json                   # Coverage summary
│   ├── Cobertura.xml                  # Cobertura format
│   └── badges/                        # Coverage badges
│       ├── badge_linecoverage.svg
│       └── badge_branchcoverage.svg
├── test-metrics.json                  # Test execution metrics
└── test-failures.json                 # Failure analysis (if failures)
```

## Interpreting Results

### Metrics Interpretation

**Pass Rate:**
- ≥ 95%: Excellent
- 90-95%: Good
- 85-90%: Needs attention
- < 85%: Critical

**Execution Time:**
- < 30s: Meets target ✅
- 30-60s: Acceptable ⚠️
- > 60s: Too slow ❌

### Coverage Interpretation

**Line Coverage:**
- ≥ 80%: Excellent
- 70-80%: Good (meets target)
- 60-70%: Needs improvement
- < 60%: Critical

**Branch Coverage:**
- ≥ 70%: Excellent
- 60-70%: Good
- 50-60%: Needs improvement
- < 50%: Critical

### Failure Interpretation

**By Severity:**
- Critical: Fix immediately (P0)
- High: Fix this sprint (P1)
- Medium: Fix next sprint (P2)
- Low: Backlog (P3)

**By Type:**
- Compilation: Source generator issue
- Assertion: Logic error or incorrect expectation
- Timeout: Performance issue or flaky test
- DynamoDB: Infrastructure or data issue

## Best Practices

### 1. Review Metrics Regularly

```bash
# Weekly: Check trends
./scripts/generate-test-metrics.sh

# Look for:
# - Increasing execution time
# - Decreasing pass rate
# - New flaky tests
```

### 2. Maintain Coverage

```bash
# Before committing
./scripts/generate-coverage-report.sh html

# Ensure:
# - Coverage doesn't decrease
# - New code is covered
# - Critical paths have high coverage
```

### 3. Address Failures Promptly

```bash
# After test failures
cat test-failures.json | jq

# Prioritize by:
# 1. Severity (Critical first)
# 2. Frequency (common patterns)
# 3. Impact (blocking tests)
```

### 4. Track Performance

```bash
# Monitor execution time
jq '.totalDurationMs' test-metrics.json

# If increasing:
# - Profile slow tests
# - Optimize or parallelize
# - Consider test data size
```

## Troubleshooting

### No Metrics Generated

**Problem:** Tests run but no metrics appear

**Solutions:**
1. Check test collection:
   ```csharp
   [Collection("Test Metrics")]
   ```

2. Verify environment variables:
   ```bash
   echo $TEST_METRICS_EXPORT_PATH
   ```

3. Check console output for errors

### Coverage Shows 0%

**Problem:** Coverage report shows no coverage

**Solutions:**
1. Ensure coverlet.collector is installed
2. Use `--collect:"XPlat Code Coverage"`
3. Check runsettings file path
4. Verify tests actually execute

### Failure Analysis Missing

**Problem:** Tests fail but no analysis generated

**Solutions:**
1. Check if `TestMetricsCollector` is used
2. Verify `TEST_FAILURES_EXPORT_PATH` is set
3. Ensure exceptions are properly caught

## Additional Resources

- [Test Metrics Guide](./METRICS_GUIDE.md) - Detailed metrics documentation
- [Coverage Guide](./COVERAGE_GUIDE.md) - Coverage collection and analysis
- [Failure Analysis Guide](./FAILURE_ANALYSIS_GUIDE.md) - Failure categorization

## Summary

| Feature | Script | Output |
|---------|--------|--------|
| Test Metrics | `generate-test-metrics.sh` | Console, JSON, GitHub |
| Code Coverage | `generate-coverage-report.sh` | HTML, JSON, Cobertura |
| Failure Analysis | Automatic on failure | Console, JSON, GitHub |

**Key Benefits:**
- 📊 Data-driven test quality insights
- 📈 Track coverage trends over time
- 🔍 Faster failure diagnosis
- 🎯 Prioritized fix recommendations
- 🚀 Improved CI/CD visibility

Use these tools to maintain high test quality and quickly resolve issues!
