using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Analyzes test failures and categorizes them by type for better diagnostics and reporting.
/// </summary>
public class TestFailureAnalyzer
{
    private readonly List<CategorizedFailure> _failures = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Analyzes and categorizes a test failure.
    /// </summary>
    public CategorizedFailure AnalyzeFailure(string testName, Exception exception, string category = "Integration")
    {
        var failure = new CategorizedFailure
        {
            TestName = testName,
            Category = category,
            Timestamp = DateTime.UtcNow,
            ExceptionType = exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace ?? string.Empty,
            FailureType = DetermineFailureType(exception),
            Severity = DetermineSeverity(exception),
            IsFlaky = IsLikelyFlaky(exception),
            SuggestedAction = GetSuggestedAction(exception)
        };
        
        lock (_lock)
        {
            _failures.Add(failure);
        }
        
        return failure;
    }
    
    /// <summary>
    /// Gets all recorded failures.
    /// </summary>
    public IReadOnlyList<CategorizedFailure> GetFailures()
    {
        lock (_lock)
        {
            return _failures.ToList().AsReadOnly();
        }
    }
    
    /// <summary>
    /// Generates a failure analysis report.
    /// </summary>
    public FailureAnalysisReport GenerateReport()
    {
        lock (_lock)
        {
            var report = new FailureAnalysisReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalFailures = _failures.Count,
                Failures = _failures.ToList()
            };
            
            // Group by failure type
            var typeGroups = _failures.GroupBy(f => f.FailureType);
            foreach (var group in typeGroups)
            {
                report.FailuresByType[group.Key] = group.Count();
            }
            
            // Group by severity
            var severityGroups = _failures.GroupBy(f => f.Severity);
            foreach (var group in severityGroups)
            {
                report.FailuresBySeverity[group.Key] = group.Count();
            }
            
            // Identify flaky tests
            report.FlakyTests = _failures.Where(f => f.IsFlaky).Select(f => f.TestName).Distinct().ToList();
            
            // Identify most common failure patterns
            report.CommonPatterns = _failures
                .GroupBy(f => f.FailureType)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new FailurePattern
                {
                    Type = g.Key,
                    Count = g.Count(),
                    Examples = g.Take(3).Select(f => f.TestName).ToList()
                })
                .ToList();
            
            return report;
        }
    }
    
    /// <summary>
    /// Generates a text report of failures.
    /// </summary>
    public string GenerateTextReport()
    {
        var report = GenerateReport();
        var sb = new StringBuilder();
        
        sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║           TEST FAILURE ANALYSIS REPORT                         ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Total Failures: {report.TotalFailures}");
        sb.AppendLine();
        
        if (report.TotalFailures == 0)
        {
            sb.AppendLine("✅ No failures to analyze!");
            return sb.ToString();
        }
        
        // Failures by type
        sb.AppendLine("═══ FAILURES BY TYPE ═══");
        foreach (var kvp in report.FailuresByType.OrderByDescending(x => x.Value))
        {
            var percentage = (kvp.Value * 100.0 / report.TotalFailures);
            sb.AppendLine($"  {kvp.Key,-25} {kvp.Value,3} ({percentage:F1}%)");
        }
        sb.AppendLine();
        
        // Failures by severity
        sb.AppendLine("═══ FAILURES BY SEVERITY ═══");
        foreach (var kvp in report.FailuresBySeverity.OrderByDescending(x => x.Value))
        {
            var icon = kvp.Key switch
            {
                FailureSeverity.Critical => "🔴",
                FailureSeverity.High => "🟠",
                FailureSeverity.Medium => "🟡",
                FailureSeverity.Low => "🟢",
                _ => "⚪"
            };
            
            var percentage = (kvp.Value * 100.0 / report.TotalFailures);
            sb.AppendLine($"  {icon} {kvp.Key,-15} {kvp.Value,3} ({percentage:F1}%)");
        }
        sb.AppendLine();
        
        // Common patterns
        if (report.CommonPatterns.Any())
        {
            sb.AppendLine("═══ COMMON FAILURE PATTERNS ═══");
            var rank = 1;
            foreach (var pattern in report.CommonPatterns)
            {
                sb.AppendLine($"{rank}. {pattern.Type} ({pattern.Count} occurrences)");
                sb.AppendLine($"   Examples:");
                foreach (var example in pattern.Examples)
                {
                    sb.AppendLine($"   - {example}");
                }
                sb.AppendLine();
                rank++;
            }
        }
        
        // Flaky tests
        if (report.FlakyTests.Any())
        {
            sb.AppendLine("═══ POTENTIALLY FLAKY TESTS ═══");
            sb.AppendLine("These tests may have intermittent failures:");
            sb.AppendLine();
            foreach (var test in report.FlakyTests)
            {
                sb.AppendLine($"  ⚠️  {test}");
            }
            sb.AppendLine();
        }
        
        // Detailed failures
        sb.AppendLine("═══ DETAILED FAILURE LIST ═══");
        foreach (var failure in report.Failures.OrderBy(f => f.Severity).ThenBy(f => f.TestName))
        {
            var severityIcon = failure.Severity switch
            {
                FailureSeverity.Critical => "🔴",
                FailureSeverity.High => "🟠",
                FailureSeverity.Medium => "🟡",
                FailureSeverity.Low => "🟢",
                _ => "⚪"
            };
            
            sb.AppendLine($"{severityIcon} {failure.TestName}");
            sb.AppendLine($"   Type:     {failure.FailureType}");
            sb.AppendLine($"   Severity: {failure.Severity}");
            sb.AppendLine($"   Message:  {failure.Message}");
            
            if (failure.IsFlaky)
            {
                sb.AppendLine($"   ⚠️  Potentially flaky");
            }
            
            if (!string.IsNullOrEmpty(failure.SuggestedAction))
            {
                sb.AppendLine($"   💡 Action: {failure.SuggestedAction}");
            }
            
            sb.AppendLine();
        }
        
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
        
        sb.AppendLine("# 🔍 Test Failure Analysis");
        sb.AppendLine();
        
        if (report.TotalFailures == 0)
        {
            sb.AppendLine("✅ **No test failures!**");
            return sb.ToString();
        }
        
        sb.AppendLine($"**Total Failures:** {report.TotalFailures}");
        sb.AppendLine();
        
        // Failures by type
        sb.AppendLine("## Failures by Type");
        sb.AppendLine();
        sb.AppendLine("| Type | Count | Percentage |");
        sb.AppendLine("|------|-------|------------|");
        
        foreach (var kvp in report.FailuresByType.OrderByDescending(x => x.Value))
        {
            var percentage = (kvp.Value * 100.0 / report.TotalFailures);
            sb.AppendLine($"| {kvp.Key} | {kvp.Value} | {percentage:F1}% |");
        }
        sb.AppendLine();
        
        // Failures by severity
        sb.AppendLine("## Failures by Severity");
        sb.AppendLine();
        sb.AppendLine("| Severity | Count | Percentage |");
        sb.AppendLine("|----------|-------|------------|");
        
        foreach (var kvp in report.FailuresBySeverity.OrderByDescending(x => x.Value))
        {
            var icon = kvp.Key switch
            {
                FailureSeverity.Critical => "🔴",
                FailureSeverity.High => "🟠",
                FailureSeverity.Medium => "🟡",
                FailureSeverity.Low => "🟢",
                _ => "⚪"
            };
            
            var percentage = (kvp.Value * 100.0 / report.TotalFailures);
            sb.AppendLine($"| {icon} {kvp.Key} | {kvp.Value} | {percentage:F1}% |");
        }
        sb.AppendLine();
        
        // Flaky tests warning
        if (report.FlakyTests.Any())
        {
            sb.AppendLine("## ⚠️ Potentially Flaky Tests");
            sb.AppendLine();
            sb.AppendLine("The following tests may have intermittent failures:");
            sb.AppendLine();
            
            foreach (var test in report.FlakyTests)
            {
                sb.AppendLine($"- `{test}`");
            }
            sb.AppendLine();
        }
        
        // Common patterns
        if (report.CommonPatterns.Any())
        {
            sb.AppendLine("## 📊 Common Failure Patterns");
            sb.AppendLine();
            
            foreach (var pattern in report.CommonPatterns.Take(3))
            {
                sb.AppendLine($"### {pattern.Type} ({pattern.Count} occurrences)");
                sb.AppendLine();
                sb.AppendLine("Examples:");
                foreach (var example in pattern.Examples)
                {
                    sb.AppendLine($"- `{example}`");
                }
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Clears all recorded failures.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _failures.Clear();
        }
    }
    
    private static FailureType DetermineFailureType(Exception exception)
    {
        var exceptionType = exception.GetType().Name;
        var message = exception.Message.ToLowerInvariant();
        
        // Compilation errors
        if (exceptionType.Contains("Compilation") || message.Contains("compilation error"))
            return FailureType.Compilation;
        
        // Assertion failures
        if (exceptionType.Contains("Assert") || exceptionType.Contains("Xunit"))
            return FailureType.Assertion;
        
        // Timeout errors
        if (exceptionType.Contains("Timeout") || message.Contains("timeout") || message.Contains("timed out"))
            return FailureType.Timeout;
        
        // Network/connectivity errors
        if (message.Contains("connection") || message.Contains("network") || message.Contains("socket"))
            return FailureType.Network;
        
        // DynamoDB-specific errors
        if (message.Contains("dynamodb") || message.Contains("table") || message.Contains("item not found"))
            return FailureType.DynamoDB;
        
        // Null reference errors
        if (exceptionType.Contains("NullReference") || message.Contains("null"))
            return FailureType.NullReference;
        
        // Argument errors
        if (exceptionType.Contains("Argument"))
            return FailureType.InvalidArgument;
        
        // Setup/teardown errors
        if (message.Contains("initialize") || message.Contains("dispose") || message.Contains("setup"))
            return FailureType.SetupTeardown;
        
        // Default to runtime error
        return FailureType.Runtime;
    }
    
    private static FailureSeverity DetermineSeverity(Exception exception)
    {
        var failureType = DetermineFailureType(exception);
        
        return failureType switch
        {
            FailureType.Compilation => FailureSeverity.Critical,
            FailureType.NullReference => FailureSeverity.High,
            FailureType.DynamoDB => FailureSeverity.High,
            FailureType.Timeout => FailureSeverity.Medium,
            FailureType.Network => FailureSeverity.Medium,
            FailureType.SetupTeardown => FailureSeverity.Medium,
            FailureType.Assertion => FailureSeverity.High,
            FailureType.InvalidArgument => FailureSeverity.Medium,
            FailureType.Runtime => FailureSeverity.Medium,
            _ => FailureSeverity.Low
        };
    }
    
    private static bool IsLikelyFlaky(Exception exception)
    {
        var message = exception.Message.ToLowerInvariant();
        
        // Timeout-related failures are often flaky
        if (message.Contains("timeout") || message.Contains("timed out"))
            return true;
        
        // Network-related failures can be flaky
        if (message.Contains("connection") || message.Contains("network"))
            return true;
        
        // Resource availability issues
        if (message.Contains("already exists") || message.Contains("not found") || message.Contains("unavailable"))
            return true;
        
        return false;
    }
    
    private static string GetSuggestedAction(Exception exception)
    {
        var failureType = DetermineFailureType(exception);
        
        return failureType switch
        {
            FailureType.Compilation => "Fix compilation errors in generated code. Check source generator logic.",
            FailureType.NullReference => "Add null checks or ensure objects are properly initialized.",
            FailureType.DynamoDB => "Check DynamoDB Local is running. Verify table setup and data.",
            FailureType.Timeout => "Increase timeout values or optimize slow operations. May be flaky.",
            FailureType.Network => "Check network connectivity. May be environmental issue.",
            FailureType.SetupTeardown => "Review test initialization and cleanup logic.",
            FailureType.Assertion => "Review test expectations. Actual behavior may have changed.",
            FailureType.InvalidArgument => "Validate input parameters and test data.",
            FailureType.Runtime => "Review stack trace for root cause. Add error handling if needed.",
            _ => "Review error details and stack trace."
        };
    }
}

/// <summary>
/// Represents a categorized test failure.
/// </summary>
public class CategorizedFailure
{
    public string TestName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public FailureType FailureType { get; set; }
    public FailureSeverity Severity { get; set; }
    public bool IsFlaky { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
}

/// <summary>
/// Comprehensive failure analysis report.
/// </summary>
public class FailureAnalysisReport
{
    public DateTime GeneratedAt { get; set; }
    public int TotalFailures { get; set; }
    public Dictionary<FailureType, int> FailuresByType { get; set; } = new();
    public Dictionary<FailureSeverity, int> FailuresBySeverity { get; set; } = new();
    public List<string> FlakyTests { get; set; } = new();
    public List<FailurePattern> CommonPatterns { get; set; } = new();
    public List<CategorizedFailure> Failures { get; set; } = new();
}

/// <summary>
/// Represents a common failure pattern.
/// </summary>
public class FailurePattern
{
    public FailureType Type { get; set; }
    public int Count { get; set; }
    public List<string> Examples { get; set; } = new();
}

/// <summary>
/// Types of test failures.
/// </summary>
public enum FailureType
{
    Compilation,
    Assertion,
    Timeout,
    Network,
    DynamoDB,
    NullReference,
    InvalidArgument,
    SetupTeardown,
    Runtime,
    Unknown
}

/// <summary>
/// Severity levels for failures.
/// </summary>
public enum FailureSeverity
{
    Critical,
    High,
    Medium,
    Low
}
