using System.Text;
using System.Text.Json;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Comprehensive test metrics reporter that tracks and reports test execution statistics.
/// Supports multiple output formats including console, JSON, and CI/CD dashboard formats.
/// </summary>
public class TestMetricsReporter
{
    private readonly List<TestExecutionMetric> _metrics = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Records a test execution metric.
    /// </summary>
    public void RecordTest(string testName, long durationMs, string category, bool passed = true, string? failureReason = null)
    {
        lock (_lock)
        {
            _metrics.Add(new TestExecutionMetric
            {
                TestName = testName,
                DurationMs = durationMs,
                Category = category,
                Timestamp = DateTime.UtcNow,
                Passed = passed,
                FailureReason = failureReason
            });
        }
    }
    
    /// <summary>
    /// Gets all recorded metrics.
    /// </summary>
    public IReadOnlyList<TestExecutionMetric> GetMetrics()
    {
        lock (_lock)
        {
            return _metrics.ToList().AsReadOnly();
        }
    }
    
    /// <summary>
    /// Generates a comprehensive test metrics report.
    /// </summary>
    public TestMetricsReport GenerateReport()
    {
        lock (_lock)
        {
            var report = new TestMetricsReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalTests = _metrics.Count,
                PassedTests = _metrics.Count(m => m.Passed),
                FailedTests = _metrics.Count(m => !m.Passed),
                TotalDurationMs = _metrics.Sum(m => m.DurationMs),
                AverageDurationMs = _metrics.Any() ? _metrics.Average(m => m.DurationMs) : 0,
                MinDurationMs = _metrics.Any() ? _metrics.Min(m => m.DurationMs) : 0,
                MaxDurationMs = _metrics.Any() ? _metrics.Max(m => m.DurationMs) : 0
            };
            
            // Group by category
            var categoryGroups = _metrics.GroupBy(m => m.Category);
            foreach (var group in categoryGroups)
            {
                var categoryMetrics = new CategoryMetrics
                {
                    Category = group.Key,
                    TestCount = group.Count(),
                    PassedCount = group.Count(m => m.Passed),
                    FailedCount = group.Count(m => !m.Passed),
                    TotalDurationMs = group.Sum(m => m.DurationMs),
                    AverageDurationMs = group.Average(m => m.DurationMs),
                    MinDurationMs = group.Min(m => m.DurationMs),
                    MaxDurationMs = group.Max(m => m.DurationMs)
                };
                
                report.CategoryMetrics.Add(categoryMetrics);
            }
            
            // Identify slowest tests
            report.SlowestTests = _metrics
                .OrderByDescending(m => m.DurationMs)
                .Take(10)
                .Select(m => new TestSummary
                {
                    TestName = m.TestName,
                    Category = m.Category,
                    DurationMs = m.DurationMs,
                    Passed = m.Passed
                })
                .ToList();
            
            // Identify failed tests
            report.FailedTests = _metrics
                .Where(m => !m.Passed)
                .Select(m => new FailedTestSummary
                {
                    TestName = m.TestName,
                    Category = m.Category,
                    DurationMs = m.DurationMs,
                    FailureReason = m.FailureReason ?? "Unknown"
                })
                .ToList();
            
            return report;
        }
    }
    
    /// <summary>
    /// Generates a console-friendly text report.
    /// </summary>
    public string GenerateTextReport()
    {
        var report = GenerateReport();
        var sb = new StringBuilder();
        
        sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║           TEST EXECUTION METRICS REPORT                        ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        
        // Overall Summary
        sb.AppendLine("═══ OVERALL SUMMARY ═══");
        sb.AppendLine($"Total Tests:      {report.TotalTests}");
        sb.AppendLine($"Passed:           {report.PassedTests} ({GetPercentage(report.PassedTests, report.TotalTests):F1}%)");
        sb.AppendLine($"Failed:           {report.FailedTests} ({GetPercentage(report.FailedTests, report.TotalTests):F1}%)");
        sb.AppendLine($"Total Duration:   {FormatDuration(report.TotalDurationMs)}");
        sb.AppendLine($"Average Duration: {report.AverageDurationMs:F2}ms");
        sb.AppendLine($"Min Duration:     {report.MinDurationMs}ms");
        sb.AppendLine($"Max Duration:     {report.MaxDurationMs}ms");
        sb.AppendLine();
        
        // Category Breakdown
        if (report.CategoryMetrics.Any())
        {
            sb.AppendLine("═══ BREAKDOWN BY CATEGORY ═══");
            foreach (var category in report.CategoryMetrics.OrderBy(c => c.Category))
            {
                sb.AppendLine($"┌─ {category.Category} ─────");
                sb.AppendLine($"│  Tests:    {category.TestCount}");
                sb.AppendLine($"│  Passed:   {category.PassedCount} ({GetPercentage(category.PassedCount, category.TestCount):F1}%)");
                sb.AppendLine($"│  Failed:   {category.FailedCount}");
                sb.AppendLine($"│  Duration: {FormatDuration(category.TotalDurationMs)} (avg: {category.AverageDurationMs:F2}ms)");
                sb.AppendLine($"└────────────");
            }
            sb.AppendLine();
        }
        
        // Slowest Tests
        if (report.SlowestTests.Any())
        {
            sb.AppendLine("═══ SLOWEST TESTS (TOP 10) ═══");
            var rank = 1;
            foreach (var test in report.SlowestTests)
            {
                var status = test.Passed ? "✓" : "✗";
                sb.AppendLine($"{rank,2}. {status} {test.TestName}");
                sb.AppendLine($"     {FormatDuration(test.DurationMs)} [{test.Category}]");
                rank++;
            }
            sb.AppendLine();
        }
        
        // Failed Tests
        if (report.FailedTests.Any())
        {
            sb.AppendLine("═══ FAILED TESTS ═══");
            foreach (var test in report.FailedTests)
            {
                sb.AppendLine($"✗ {test.TestName}");
                sb.AppendLine($"  Category: {test.Category}");
                sb.AppendLine($"  Duration: {test.DurationMs}ms");
                sb.AppendLine($"  Reason:   {test.FailureReason}");
                sb.AppendLine();
            }
        }
        
        // Performance Assessment
        sb.AppendLine("═══ PERFORMANCE ASSESSMENT ═══");
        var targetMs = 30000; // 30 seconds target
        var meetsTarget = report.TotalDurationMs <= targetMs;
        sb.AppendLine($"Target:  {FormatDuration(targetMs)}");
        sb.AppendLine($"Actual:  {FormatDuration(report.TotalDurationMs)}");
        sb.AppendLine($"Status:  {(meetsTarget ? "✓ MEETS TARGET" : "✗ EXCEEDS TARGET")}");
        
        if (!meetsTarget)
        {
            var excess = report.TotalDurationMs - targetMs;
            sb.AppendLine($"Excess:  {FormatDuration(excess)} over target");
        }
        
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates a JSON report suitable for CI/CD dashboards.
    /// </summary>
    public string GenerateJsonReport()
    {
        var report = GenerateReport();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        return JsonSerializer.Serialize(report, options);
    }
    
    /// <summary>
    /// Generates a GitHub Actions summary format.
    /// </summary>
    public string GenerateGitHubSummary()
    {
        var report = GenerateReport();
        var sb = new StringBuilder();
        
        sb.AppendLine("# 📊 Test Execution Metrics");
        sb.AppendLine();
        
        // Overall stats
        sb.AppendLine("## Overall Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total Tests | {report.TotalTests} |");
        sb.AppendLine($"| ✅ Passed | {report.PassedTests} ({GetPercentage(report.PassedTests, report.TotalTests):F1}%) |");
        sb.AppendLine($"| ❌ Failed | {report.FailedTests} ({GetPercentage(report.FailedTests, report.TotalTests):F1}%) |");
        sb.AppendLine($"| ⏱️ Total Duration | {FormatDuration(report.TotalDurationMs)} |");
        sb.AppendLine($"| 📈 Average Duration | {report.AverageDurationMs:F2}ms |");
        sb.AppendLine();
        
        // Category breakdown
        if (report.CategoryMetrics.Any())
        {
            sb.AppendLine("## Breakdown by Category");
            sb.AppendLine();
            sb.AppendLine("| Category | Tests | Passed | Failed | Duration | Avg |");
            sb.AppendLine("|----------|-------|--------|--------|----------|-----|");
            
            foreach (var category in report.CategoryMetrics.OrderBy(c => c.Category))
            {
                sb.AppendLine($"| {category.Category} | {category.TestCount} | {category.PassedCount} | {category.FailedCount} | {FormatDuration(category.TotalDurationMs)} | {category.AverageDurationMs:F2}ms |");
            }
            sb.AppendLine();
        }
        
        // Performance target
        var targetMs = 30000;
        var meetsTarget = report.TotalDurationMs <= targetMs;
        var targetEmoji = meetsTarget ? "✅" : "⚠️";
        
        sb.AppendLine("## Performance Target");
        sb.AppendLine();
        sb.AppendLine($"{targetEmoji} **Target:** {FormatDuration(targetMs)} | **Actual:** {FormatDuration(report.TotalDurationMs)}");
        
        if (!meetsTarget)
        {
            var excess = report.TotalDurationMs - targetMs;
            sb.AppendLine();
            sb.AppendLine($"> ⚠️ Tests exceeded target by {FormatDuration(excess)}");
        }
        sb.AppendLine();
        
        // Slowest tests
        if (report.SlowestTests.Any())
        {
            sb.AppendLine("## 🐌 Slowest Tests");
            sb.AppendLine();
            sb.AppendLine("| Rank | Test | Duration | Category | Status |");
            sb.AppendLine("|------|------|----------|----------|--------|");
            
            var rank = 1;
            foreach (var test in report.SlowestTests.Take(5))
            {
                var status = test.Passed ? "✅" : "❌";
                sb.AppendLine($"| {rank} | {test.TestName} | {FormatDuration(test.DurationMs)} | {test.Category} | {status} |");
                rank++;
            }
            sb.AppendLine();
        }
        
        // Failed tests
        if (report.FailedTests.Any())
        {
            sb.AppendLine("## ❌ Failed Tests");
            sb.AppendLine();
            
            foreach (var test in report.FailedTests)
            {
                sb.AppendLine($"### {test.TestName}");
                sb.AppendLine($"- **Category:** {test.Category}");
                sb.AppendLine($"- **Duration:** {test.DurationMs}ms");
                sb.AppendLine($"- **Reason:** {test.FailureReason}");
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Exports metrics to a file in the specified format.
    /// </summary>
    public async Task ExportToFileAsync(string filePath, ReportFormat format = ReportFormat.Text)
    {
        string content = format switch
        {
            ReportFormat.Text => GenerateTextReport(),
            ReportFormat.Json => GenerateJsonReport(),
            ReportFormat.GitHubSummary => GenerateGitHubSummary(),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
        
        await File.WriteAllTextAsync(filePath, content);
    }
    
    /// <summary>
    /// Clears all recorded metrics.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _metrics.Clear();
        }
    }
    
    private static string FormatDuration(long ms)
    {
        if (ms < 1000)
            return $"{ms}ms";
        
        var seconds = ms / 1000.0;
        if (seconds < 60)
            return $"{seconds:F2}s";
        
        var minutes = (int)(seconds / 60);
        var remainingSeconds = seconds % 60;
        return $"{minutes}m {remainingSeconds:F2}s";
    }
    
    private static double GetPercentage(int value, int total)
    {
        return total > 0 ? (value * 100.0 / total) : 0;
    }
}

/// <summary>
/// Comprehensive test metrics report.
/// </summary>
public class TestMetricsReport
{
    public DateTime GeneratedAt { get; set; }
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public long TotalDurationMs { get; set; }
    public double AverageDurationMs { get; set; }
    public long MinDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public List<CategoryMetrics> CategoryMetrics { get; set; } = new();
    public List<TestSummary> SlowestTests { get; set; } = new();
    public List<FailedTestSummary> FailedTests { get; set; } = new();
}

/// <summary>
/// Metrics for a specific test category.
/// </summary>
public class CategoryMetrics
{
    public string Category { get; set; } = string.Empty;
    public int TestCount { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public long TotalDurationMs { get; set; }
    public double AverageDurationMs { get; set; }
    public long MinDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
}

/// <summary>
/// Summary of a single test execution.
/// </summary>
public class TestSummary
{
    public string TestName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public bool Passed { get; set; }
}

/// <summary>
/// Summary of a failed test.
/// </summary>
public class FailedTestSummary
{
    public string TestName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}

/// <summary>
/// Report output format.
/// </summary>
public enum ReportFormat
{
    Text,
    Json,
    GitHubSummary
}
