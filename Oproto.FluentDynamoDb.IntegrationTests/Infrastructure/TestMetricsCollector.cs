using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit test fixture that automatically collects test execution metrics.
/// This can be used as a collection fixture to track metrics across all tests.
/// </summary>
public class TestMetricsCollector : IAsyncLifetime
{
    private static readonly TestMetricsReporter _reporter = new();
    private static readonly TestFailureAnalyzer _failureAnalyzer = new();
    private static readonly object _lock = new();
    
    /// <summary>
    /// Gets the global test metrics reporter instance.
    /// </summary>
    public static TestMetricsReporter Reporter => _reporter;
    
    /// <summary>
    /// Gets the global test failure analyzer instance.
    /// </summary>
    public static TestFailureAnalyzer FailureAnalyzer => _failureAnalyzer;
    
    public Task InitializeAsync()
    {
        // Clear metrics at the start of a test run
        lock (_lock)
        {
            _reporter.Clear();
            _failureAnalyzer.Clear();
        }
        
        Console.WriteLine("[Metrics] Test metrics and failure analysis collection started");
        return Task.CompletedTask;
    }
    
    public async Task DisposeAsync()
    {
        // Generate and output reports at the end of test run
        lock (_lock)
        {
            // Metrics report
            var metricsReport = _reporter.GenerateTextReport();
            Console.WriteLine();
            Console.WriteLine(metricsReport);
            
            // Failure analysis report (if there are failures)
            var failures = _failureAnalyzer.GetFailures();
            if (failures.Any())
            {
                Console.WriteLine();
                var failureReport = _failureAnalyzer.GenerateTextReport();
                Console.WriteLine(failureReport);
            }
            
            // Export metrics to file
            var exportPath = Environment.GetEnvironmentVariable("TEST_METRICS_EXPORT_PATH");
            if (!string.IsNullOrEmpty(exportPath))
            {
                var format = GetExportFormat();
                _reporter.ExportToFileAsync(exportPath, format).Wait();
                Console.WriteLine($"[Metrics] Exported to: {exportPath} (format: {format})");
            }
            
            // Export failure analysis to file
            var failureExportPath = Environment.GetEnvironmentVariable("TEST_FAILURES_EXPORT_PATH");
            if (!string.IsNullOrEmpty(failureExportPath) && failures.Any())
            {
                var failureJson = _failureAnalyzer.GenerateJsonReport();
                File.WriteAllText(failureExportPath, failureJson);
                Console.WriteLine($"[Failures] Exported to: {failureExportPath}");
            }
            
            // Export GitHub summary if in CI
            var githubStepSummary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
            if (!string.IsNullOrEmpty(githubStepSummary))
            {
                var metricsSummary = _reporter.GenerateGitHubSummary();
                File.AppendAllText(githubStepSummary, metricsSummary);
                
                if (failures.Any())
                {
                    var failureSummary = _failureAnalyzer.GenerateGitHubSummary();
                    File.AppendAllText(githubStepSummary, failureSummary);
                }
                
                Console.WriteLine("[Metrics] Appended to GitHub Step Summary");
            }
        }
        
        await Task.CompletedTask;
    }
    
    private static ReportFormat GetExportFormat()
    {
        var formatStr = Environment.GetEnvironmentVariable("TEST_METRICS_FORMAT");
        return formatStr?.ToLowerInvariant() switch
        {
            "json" => ReportFormat.Json,
            "github" => ReportFormat.GitHubSummary,
            _ => ReportFormat.Text
        };
    }
}

/// <summary>
/// xUnit collection definition for test metrics collection.
/// Tests in this collection will have their metrics automatically tracked.
/// </summary>
[CollectionDefinition("Test Metrics")]
public class TestMetricsCollection : ICollectionFixture<TestMetricsCollector>
{
}

/// <summary>
/// Base class for tests that want automatic metrics collection.
/// Tracks test execution time and pass/fail status.
/// </summary>
public abstract class MetricsTrackingTestBase : IAsyncLifetime
{
    private readonly Stopwatch _testTimer = new();
    private readonly string _testClassName;
    private string? _currentTestName;
    private bool _testPassed = true;
    private string? _failureReason;
    
    protected MetricsTrackingTestBase()
    {
        _testClassName = GetType().Name;
    }
    
    /// <summary>
    /// Gets the category for this test class (default: "Integration").
    /// Override to specify a different category.
    /// </summary>
    protected virtual string TestCategory => "Integration";
    
    public virtual Task InitializeAsync()
    {
        _testTimer.Restart();
        return Task.CompletedTask;
    }
    
    public virtual Task DisposeAsync()
    {
        _testTimer.Stop();
        
        // Record the metric
        var testName = _currentTestName ?? _testClassName;
        TestMetricsCollector.Reporter.RecordTest(
            testName,
            _testTimer.ElapsedMilliseconds,
            TestCategory,
            _testPassed,
            _failureReason
        );
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Records a test failure with the failure analyzer.
    /// </summary>
    protected void RecordFailure(Exception exception)
    {
        var testName = _currentTestName ?? _testClassName;
        var failure = TestMetricsCollector.FailureAnalyzer.AnalyzeFailure(testName, exception, TestCategory);
        
        _testPassed = false;
        _failureReason = failure.Message;
        
        // Log failure details
        Console.WriteLine($"[Failure] {testName}");
        Console.WriteLine($"  Type: {failure.FailureType}");
        Console.WriteLine($"  Severity: {failure.Severity}");
        if (failure.IsFlaky)
        {
            Console.WriteLine($"  ⚠️  Potentially flaky");
        }
        Console.WriteLine($"  💡 {failure.SuggestedAction}");
    }
    
    /// <summary>
    /// Sets the current test name. Call this at the start of each test method.
    /// </summary>
    protected void SetTestName(string testName)
    {
        _currentTestName = $"{_testClassName}.{testName}";
    }
    
    /// <summary>
    /// Marks the current test as failed with a reason.
    /// </summary>
    protected void MarkTestFailed(string reason)
    {
        _testPassed = false;
        _failureReason = reason;
    }
    
    /// <summary>
    /// Executes a test action and automatically tracks pass/fail status.
    /// </summary>
    protected async Task ExecuteTestAsync(string testName, Func<Task> testAction)
    {
        SetTestName(testName);
        
        try
        {
            await testAction();
            _testPassed = true;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }
    
    /// <summary>
    /// Executes a test action and automatically tracks pass/fail status.
    /// </summary>
    protected void ExecuteTest(string testName, Action testAction)
    {
        SetTestName(testName);
        
        try
        {
            testAction();
            _testPassed = true;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }
}
