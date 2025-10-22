using System.Diagnostics;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Utility class for measuring and reporting test performance metrics.
/// </summary>
public static class PerformanceMetrics
{
    private static readonly object _lock = new();
    private static readonly List<TestExecutionMetric> _metrics = new();
    
    /// <summary>
    /// Records a test execution metric.
    /// </summary>
    public static void RecordTestExecution(string testName, long durationMs, string category = "Integration")
    {
        lock (_lock)
        {
            _metrics.Add(new TestExecutionMetric
            {
                TestName = testName,
                DurationMs = durationMs,
                Category = category,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    /// <summary>
    /// Gets all recorded metrics.
    /// </summary>
    public static IReadOnlyList<TestExecutionMetric> GetMetrics()
    {
        lock (_lock)
        {
            return _metrics.ToList().AsReadOnly();
        }
    }
    
    /// <summary>
    /// Generates a performance report.
    /// </summary>
    public static string GenerateReport()
    {
        lock (_lock)
        {
            if (_metrics.Count == 0)
            {
                return "No performance metrics recorded.";
            }
            
            var totalDuration = _metrics.Sum(m => m.DurationMs);
            var avgDuration = _metrics.Average(m => m.DurationMs);
            var maxDuration = _metrics.Max(m => m.DurationMs);
            var minDuration = _metrics.Min(m => m.DurationMs);
            
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Performance Metrics Report ===");
            report.AppendLine($"Total Tests: {_metrics.Count}");
            report.AppendLine($"Total Duration: {totalDuration}ms ({totalDuration / 1000.0:F2}s)");
            report.AppendLine($"Average Duration: {avgDuration:F2}ms");
            report.AppendLine($"Min Duration: {minDuration}ms");
            report.AppendLine($"Max Duration: {maxDuration}ms");
            report.AppendLine();
            
            // Group by category
            var byCategory = _metrics.GroupBy(m => m.Category);
            foreach (var group in byCategory)
            {
                var categoryTotal = group.Sum(m => m.DurationMs);
                var categoryAvg = group.Average(m => m.DurationMs);
                report.AppendLine($"Category: {group.Key}");
                report.AppendLine($"  Tests: {group.Count()}");
                report.AppendLine($"  Total: {categoryTotal}ms ({categoryTotal / 1000.0:F2}s)");
                report.AppendLine($"  Average: {categoryAvg:F2}ms");
                report.AppendLine();
            }
            
            // Show slowest tests
            report.AppendLine("Slowest Tests:");
            var slowest = _metrics.OrderByDescending(m => m.DurationMs).Take(10);
            foreach (var metric in slowest)
            {
                report.AppendLine($"  {metric.TestName}: {metric.DurationMs}ms");
            }
            
            return report.ToString();
        }
    }
    
    /// <summary>
    /// Clears all recorded metrics.
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _metrics.Clear();
        }
    }
    
    /// <summary>
    /// Checks if the test suite meets the performance target.
    /// </summary>
    /// <param name="targetMs">Target duration in milliseconds (default: 30000ms = 30s)</param>
    /// <returns>True if the total duration is within the target, false otherwise.</returns>
    public static bool MeetsPerformanceTarget(long targetMs = 30000)
    {
        lock (_lock)
        {
            var totalDuration = _metrics.Sum(m => m.DurationMs);
            return totalDuration <= targetMs;
        }
    }
}

/// <summary>
/// Represents a single test execution metric.
/// </summary>
public class TestExecutionMetric
{
    public string TestName { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Helper class for measuring test execution time.
/// </summary>
public class TestTimer : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly string _testName;
    private readonly string _category;
    
    public TestTimer(string testName, string category = "Integration")
    {
        _testName = testName;
        _category = category;
        _stopwatch = Stopwatch.StartNew();
    }
    
    public void Dispose()
    {
        _stopwatch.Stop();
        PerformanceMetrics.RecordTestExecution(_testName, _stopwatch.ElapsedMilliseconds, _category);
        
        // Log if test is slow
        if (_stopwatch.ElapsedMilliseconds > 5000)
        {
            Console.WriteLine($"[Performance Warning] {_testName} took {_stopwatch.ElapsedMilliseconds}ms (> 5s)");
        }
    }
}
