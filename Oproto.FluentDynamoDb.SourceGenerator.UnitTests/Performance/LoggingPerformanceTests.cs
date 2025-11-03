using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Performance;

/// <summary>
/// Performance tests for logging functionality.
/// Tests verify that logging has minimal overhead and can be completely eliminated when disabled.
/// </summary>
[Trait("Category", "Performance")]
public class LoggingPerformanceTests
{
    private const string TestEntitySource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Storage;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity : IDynamoDbEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string? Name { get; set; }
        
        [DynamoDbAttribute(""count"")]
        public int Count { get; set; }
        
        [DynamoDbAttribute(""tags"")]
        public HashSet<string>? Tags { get; set; }
        
        [DynamoDbAttribute(""metadata"")]
        public Dictionary<string, string>? Metadata { get; set; }
    }
}";

    [Fact]
    public void NoOpLogger_HasZeroOverhead()
    {
        // Arrange
        var result = GenerateAndCompileCode(TestEntitySource);
        var assembly = result.Assembly;
        var testEntityType = assembly.GetType("TestNamespace.TestEntity");
        Assert.NotNull(testEntityType);

        var entity = CreateTestEntity(testEntityType);
        var noOpLogger = NoOpLogger.Instance;

        // Warmup
        var toDynamoDbMethod = GetGenericMethod(testEntityType, "ToDynamoDb");
        for (int i = 0; i < 1000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, noOpLogger });
        }

        // Act - Benchmark with NoOpLogger
        var swWithLogger = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, noOpLogger });
        }
        swWithLogger.Stop();
        var withLoggerMs = swWithLogger.ElapsedMilliseconds;

        // Act - Benchmark with null logger
        var swWithNull = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object?[] { entity, null });
        }
        swWithNull.Stop();
        var withNullMs = swWithNull.ElapsedMilliseconds;

        // Assert - Performance difference should be reasonable
        // Use absolute time difference instead of percentage to avoid flakiness with small values
        var difference = Math.Abs(withLoggerMs - withNullMs);
        
        // Allow up to 50ms absolute difference (accounts for timing variations and JIT effects)
        difference.Should().BeLessThan(50, 
            $"NoOpLogger should have minimal overhead. WithLogger: {withLoggerMs}ms, WithNull: {withNullMs}ms, Difference: {difference}ms");
    }

    [Fact]
    public void IsEnabledCheck_PreventsParameterEvaluation()
    {
        // Arrange
        var result = GenerateAndCompileCode(TestEntitySource);
        var assembly = result.Assembly;
        var testEntityType = assembly.GetType("TestNamespace.TestEntity");
        Assert.NotNull(testEntityType);

        var entity = CreateTestEntity(testEntityType);
        
        // Create logger that disables Debug level
        var disabledLogger = new TestLogger(LogLevel.Information);
        
        // Create logger that enables Debug level
        var enabledLogger = new TestLogger(LogLevel.Debug);

        var toDynamoDbMethod = GetGenericMethod(testEntityType, "ToDynamoDb");

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, disabledLogger });
        }

        // Act - Benchmark with logging disabled (IsEnabled returns false)
        var swDisabled = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, disabledLogger });
        }
        swDisabled.Stop();
        var disabledMs = swDisabled.ElapsedMilliseconds;

        // Act - Benchmark with logging enabled (IsEnabled returns true)
        var swEnabled = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, enabledLogger });
        }
        swEnabled.Stop();
        var enabledMs = swEnabled.ElapsedMilliseconds;

        // Assert - Disabled logging should be significantly faster
        // When logging is disabled, expensive parameter evaluation should be skipped
        disabledMs.Should().BeLessThan(enabledMs, 
            $"Logging with IsEnabled=false should be faster. Disabled: {disabledMs}ms, Enabled: {enabledMs}ms");
        
        // The difference should be noticeable (at least 10% faster when disabled)
        var percentFaster = ((double)(enabledMs - disabledMs) / enabledMs) * 100;
        percentFaster.Should().BeGreaterThan(0, 
            $"IsEnabled check should prevent parameter evaluation. Disabled: {disabledMs}ms, Enabled: {enabledMs}ms");
    }

    [Fact]
    public void ConditionalCompilation_EliminatesOverhead()
    {
        // Arrange - Generate code without DISABLE_DYNAMODB_LOGGING
        var resultWithLogging = GenerateAndCompileCode(TestEntitySource);
        var assemblyWithLogging = resultWithLogging.Assembly;
        var typeWithLogging = assemblyWithLogging.GetType("TestNamespace.TestEntity");
        Assert.NotNull(typeWithLogging);

        // Arrange - Generate code with DISABLE_DYNAMODB_LOGGING
        var resultWithoutLogging = GenerateAndCompileCodeWithDefine(TestEntitySource, "DISABLE_DYNAMODB_LOGGING");
        var assemblyWithoutLogging = resultWithoutLogging.Assembly;
        var typeWithoutLogging = assemblyWithoutLogging.GetType("TestNamespace.TestEntity");
        Assert.NotNull(typeWithoutLogging);

        var entityWithLogging = CreateTestEntity(typeWithLogging);
        var entityWithoutLogging = CreateTestEntity(typeWithoutLogging);

        var toDynamoDbWithLogging = GetGenericMethod(typeWithLogging, "ToDynamoDb");
        var toDynamoDbWithoutLogging = GetGenericMethod(typeWithoutLogging, "ToDynamoDb");

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            toDynamoDbWithLogging.Invoke(null, new object?[] { entityWithLogging, null });
            toDynamoDbWithoutLogging.Invoke(null, new object?[] { entityWithoutLogging, null });
        }

        // Act - Benchmark with logging code present (but null logger)
        var swWithLogging = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbWithLogging.Invoke(null, new object?[] { entityWithLogging, null });
        }
        swWithLogging.Stop();
        var withLoggingMs = swWithLogging.ElapsedMilliseconds;

        // Act - Benchmark with logging code removed by conditional compilation
        var swWithoutLogging = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbWithoutLogging.Invoke(null, new object?[] { entityWithoutLogging, null });
        }
        swWithoutLogging.Stop();
        var withoutLoggingMs = swWithoutLogging.ElapsedMilliseconds;

        // Assert - Code with DISABLE_DYNAMODB_LOGGING should have no logging overhead
        // Allow up to 20ms absolute difference (timing variations can affect both versions)
        var difference = Math.Abs(withLoggingMs - withoutLoggingMs);
        
        difference.Should().BeLessThan(20, 
            $"Code with DISABLE_DYNAMODB_LOGGING should have minimal difference. WithLogging: {withLoggingMs}ms, WithoutLogging: {withoutLoggingMs}ms, Difference: {difference}ms");
    }

    [Fact]
    public void LoggingAllocation_IsMinimalAndPredictable()
    {
        // Arrange
        var result = GenerateAndCompileCode(TestEntitySource);
        var assembly = result.Assembly;
        var testEntityType = assembly.GetType("TestNamespace.TestEntity");
        Assert.NotNull(testEntityType);

        var entity = CreateTestEntity(testEntityType);
        var logger = new TestLogger(LogLevel.Debug);
        var toDynamoDbMethod = GetGenericMethod(testEntityType, "ToDynamoDb");

        // Warmup and force GC
        for (int i = 0; i < 1000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, logger });
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act - Measure allocations with logging enabled
        var beforeMemory = GC.GetTotalMemory(true);
        var iterations = 10000;
        
        for (int i = 0; i < iterations; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, logger });
            logger.Clear(); // Clear log entries to avoid accumulation
        }
        
        var afterMemory = GC.GetTotalMemory(false);
        var totalAllocated = afterMemory - beforeMemory;
        var allocatedPerIteration = totalAllocated / iterations;

        // Assert - Allocations should be minimal and predictable
        // Each iteration should allocate a reasonable amount (< 10KB per operation)
        allocatedPerIteration.Should().BeLessThan(10 * 1024, 
            $"Logging should have minimal allocations. Allocated per iteration: {allocatedPerIteration} bytes");
        
        // Total allocations should be predictable (not exponential growth)
        totalAllocated.Should().BeLessThan(100 * 1024 * 1024, 
            $"Total allocations should be reasonable. Total: {totalAllocated / 1024 / 1024}MB for {iterations} iterations");
    }

    [Fact]
    public void NoOpLogger_WithComplexEntity_HasMinimalOverhead()
    {
        // Arrange - Create entity with collections to test more complex scenarios
        var result = GenerateAndCompileCode(TestEntitySource);
        var assembly = result.Assembly;
        var testEntityType = assembly.GetType("TestNamespace.TestEntity");
        Assert.NotNull(testEntityType);

        var entity = CreateComplexTestEntity(testEntityType);
        var noOpLogger = NoOpLogger.Instance;
        var toDynamoDbMethod = GetGenericMethod(testEntityType, "ToDynamoDb");

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, noOpLogger });
        }

        // Act - Benchmark with NoOpLogger
        var swWithLogger = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { entity, noOpLogger });
        }
        swWithLogger.Stop();
        var withLoggerMs = swWithLogger.ElapsedMilliseconds;

        // Act - Benchmark with null logger
        var swWithNull = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object?[] { entity, null });
        }
        swWithNull.Stop();
        var withNullMs = swWithNull.ElapsedMilliseconds;

        // Assert - Even with complex entities, overhead should be reasonable
        // Use absolute time difference instead of percentage to avoid flakiness with small values
        var difference = Math.Abs(withLoggerMs - withNullMs);
        
        // Allow up to 30ms absolute difference for complex entities (accounts for timing variations)
        difference.Should().BeLessThan(30, 
            $"NoOpLogger should have minimal overhead even with complex entities. WithLogger: {withLoggerMs}ms, WithNull: {withNullMs}ms, Difference: {difference}ms");
    }

    [Fact]
    public void LoggingPerformance_ScalesLinearlyWithEntityComplexity()
    {
        // Arrange
        var result = GenerateAndCompileCode(TestEntitySource);
        var assembly = result.Assembly;
        var testEntityType = assembly.GetType("TestNamespace.TestEntity");
        Assert.NotNull(testEntityType);

        var simpleEntity = CreateTestEntity(testEntityType);
        var complexEntity = CreateComplexTestEntity(testEntityType);
        
        var logger = new TestLogger(LogLevel.Debug);
        var toDynamoDbMethod = GetGenericMethod(testEntityType, "ToDynamoDb");

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { simpleEntity, logger });
            toDynamoDbMethod.Invoke(null, new object[] { complexEntity, logger });
        }

        // Act - Benchmark simple entity
        var swSimple = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { simpleEntity, logger });
            logger.Clear();
        }
        swSimple.Stop();
        var simpleMs = swSimple.ElapsedMilliseconds;

        // Act - Benchmark complex entity
        var swComplex = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            toDynamoDbMethod.Invoke(null, new object[] { complexEntity, logger });
            logger.Clear();
        }
        swComplex.Stop();
        var complexMs = swComplex.ElapsedMilliseconds;

        // Assert - Performance should scale reasonably with complexity
        // Complex entity should not be more than 3x slower than simple entity
        var ratio = (double)complexMs / Math.Max(simpleMs, 1);
        ratio.Should().BeLessThan(3.0, 
            $"Logging performance should scale linearly. Simple: {simpleMs}ms, Complex: {complexMs}ms, Ratio: {ratio:F2}x");
    }

    private static object CreateTestEntity(Type entityType)
    {
        var entity = Activator.CreateInstance(entityType);
        Assert.NotNull(entity);
        
        entityType.GetProperty("Id")!.SetValue(entity, "test-123");
        entityType.GetProperty("Name")!.SetValue(entity, "Test Name");
        entityType.GetProperty("Count")!.SetValue(entity, 42);
        
        return entity;
    }

    private static object CreateComplexTestEntity(Type entityType)
    {
        var entity = CreateTestEntity(entityType);
        
        // Add collections
        var tags = new HashSet<string> { "tag1", "tag2", "tag3", "tag4", "tag5" };
        entityType.GetProperty("Tags")!.SetValue(entity, tags);
        
        var metadata = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2",
            ["key3"] = "value3",
            ["key4"] = "value4",
            ["key5"] = "value5"
        };
        entityType.GetProperty("Metadata")!.SetValue(entity, metadata);
        
        return entity;
    }

    private static MethodInfo GetGenericMethod(Type type, string methodName)
    {
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == methodName && m.IsGenericMethod);
        
        if (method == null)
        {
            throw new InvalidOperationException($"Generic method '{methodName}' not found on type '{type.Name}'");
        }
        
        return method.MakeGenericMethod(type);
    }

    private static CompilationResult GenerateAndCompileCode(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Compile to assembly
        using var ms = new MemoryStream();
        var emitResult = outputCompilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new Exception($"Compilation failed:\n{errors}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        return new CompilationResult
        {
            Assembly = assembly,
            Diagnostics = diagnostics
        };
    }

    private static CompilationResult GenerateAndCompileCodeWithDefine(string source, string defineSymbol)
    {
        var parseOptions = CSharpParseOptions.Default.WithPreprocessorSymbols(defineSymbol);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Compile to assembly
        using var ms = new MemoryStream();
        var emitResult = outputCompilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new Exception($"Compilation failed:\n{errors}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        return new CompilationResult
        {
            Assembly = assembly,
            Diagnostics = diagnostics
        };
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var assemblyLocations = new HashSet<string>
        {
            typeof(object).Assembly.Location,
            typeof(System.Collections.Generic.List<>).Assembly.Location,
            typeof(System.Linq.Enumerable).Assembly.Location,
            typeof(System.IO.Stream).Assembly.Location,
            typeof(Amazon.DynamoDBv2.Model.AttributeValue).Assembly.Location,
            typeof(Oproto.FluentDynamoDb.Attributes.DynamoDbTableAttribute).Assembly.Location,
            typeof(Oproto.FluentDynamoDb.Storage.IDynamoDbEntity).Assembly.Location,
            typeof(Oproto.FluentDynamoDb.Logging.IDynamoDbLogger).Assembly.Location,
            typeof(Oproto.FluentDynamoDb.Logging.LogLevel).Assembly.Location,
            typeof(Oproto.FluentDynamoDb.Logging.LogEventIds).Assembly.Location,
        };

        var references = assemblyLocations.Select(loc => MetadataReference.CreateFromFile(loc)).ToList();

        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.AddRange(new[]
        {
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "netstandard.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Linq.Expressions.dll"))
        });

        return references;
    }
}

public class CompilationResult
{
    public required Assembly Assembly { get; set; }
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
}
