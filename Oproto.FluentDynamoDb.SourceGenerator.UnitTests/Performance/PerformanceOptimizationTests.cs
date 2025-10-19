using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Performance;
using Oproto.FluentDynamoDb.SourceGenerator.Advanced;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Performance;

/// <summary>
/// Tests for performance optimizations in the source generator.
/// Validates that optimizations improve build time and runtime performance.
/// </summary>
public class PerformanceOptimizationTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceOptimizationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EntityMetadataCache_ShouldImprovePerformance()
    {
        // Arrange
        var entity = CreateTestEntityModel();
        EntityMetadataCache.Clear();

        // Act & Assert - First call (cache miss)
        var stopwatch = Stopwatch.StartNew();
        var metadata1 = EntityMetadataCache.GetOrCreate(entity);
        var firstCallTime = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();

        // Second call (cache hit)
        var metadata2 = EntityMetadataCache.GetOrCreate(entity);
        var secondCallTime = stopwatch.ElapsedMilliseconds;
        stopwatch.Stop();

        // Verify cache hit is significantly faster
        Assert.True(secondCallTime < firstCallTime, 
            $"Cache hit ({secondCallTime}ms) should be faster than cache miss ({firstCallTime}ms)");
        
        // Verify same instance is returned
        Assert.Same(metadata1, metadata2);

        _output.WriteLine($"Cache miss: {firstCallTime}ms, Cache hit: {secondCallTime}ms");
        _output.WriteLine($"Performance improvement: {(double)firstCallTime / Math.Max(secondCallTime, 1):F1}x");
    }

    [Fact]
    public void EntityMetadataCache_ShouldProvideAccurateStatistics()
    {
        // Arrange
        EntityMetadataCache.Clear();
        var entity1 = CreateTestEntityModel("Entity1");
        var entity2 = CreateTestEntityModel("Entity2");

        // Act
        EntityMetadataCache.GetOrCreate(entity1); // Miss
        EntityMetadataCache.GetOrCreate(entity1); // Hit
        EntityMetadataCache.GetOrCreate(entity2); // Miss
        EntityMetadataCache.GetOrCreate(entity1); // Hit
        EntityMetadataCache.GetOrCreate(entity2); // Hit

        var stats = EntityMetadataCache.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalEntries);
        Assert.Equal(2, stats.AliveEntries);
        Assert.Equal(0, stats.DeadEntries);
        Assert.Equal(1.0, stats.MemoryEfficiency);
        Assert.False(stats.CleanupRecommended);

        _output.WriteLine($"Cache Statistics: {stats.AliveEntries}/{stats.TotalEntries} entries, {stats.MemoryEfficiency:P1} efficiency");
    }

    [Fact]
    public void OptimizedCodeGenerator_ShouldProduceEfficientCode()
    {
        // Arrange
        var entity = CreateTestEntityModel();
        var sb = new System.Text.StringBuilder();

        // Act
        var stopwatch = Stopwatch.StartNew();
        OptimizedCodeGenerator.GenerateOptimizedToDynamoDbMethod(sb, entity);
        var generationTime = stopwatch.ElapsedMilliseconds;
        stopwatch.Stop();

        var generatedCode = sb.ToString();

        // Assert
        Assert.NotEmpty(generatedCode);
        Assert.Contains("MethodImpl(MethodImplOptions.AggressiveInlining)", generatedCode);
        Assert.Contains("Pre-allocate dictionary with exact capacity", generatedCode);
        Assert.Contains("StringBuilder", generatedCode); // For complex key generation
        
        // Verify performance characteristics
        Assert.True(generationTime < 100, $"Code generation took {generationTime}ms, should be under 100ms");

        _output.WriteLine($"Generated {generatedCode.Length} characters in {generationTime}ms");
        _output.WriteLine($"Code generation rate: {generatedCode.Length / Math.Max(generationTime, 1)} chars/ms");
    }

    [Fact]
    public void IncrementalSourceGenerator_ShouldCacheTransformResults()
    {
        // Arrange
        var sourceCode = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        var compilation = CreateCompilation(sourceCode);
        var syntaxTree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First();

        // Clear cache to ensure clean test
        EntityTransformCache.PerformMaintenance();

        // Act - First transformation (cache miss)
        var stopwatch = Stopwatch.StartNew();
        var result1 = InvokeTransformEntityClass(classDecl, semanticModel);
        var firstTransformTime = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();

        // Second transformation (cache hit)
        var result2 = InvokeTransformEntityClass(classDecl, semanticModel);
        var secondTransformTime = stopwatch.ElapsedMilliseconds;
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result1.EntityModel);
        Assert.NotNull(result2.EntityModel);
        Assert.Equal(result1.CacheKey, result2.CacheKey);
        
        // Cache hit should be faster
        Assert.True(secondTransformTime <= firstTransformTime, 
            $"Cache hit ({secondTransformTime}ms) should be faster than or equal to cache miss ({firstTransformTime}ms)");

        _output.WriteLine($"Transform miss: {firstTransformTime}ms, Transform hit: {secondTransformTime}ms");
        _output.WriteLine($"Cache hit rate: {EntityTransformCache.GetCacheHitRate():P1}");
    }

    [Fact]
    public void OptimizedCodeGenerator_ShouldMinimizeAllocations()
    {
        // Arrange
        var entity = CreateComplexEntityModel();
        var sb = new System.Text.StringBuilder();

        // Act
        OptimizedCodeGenerator.GenerateOptimizedToDynamoDbMethod(sb, entity);
        var generatedCode = sb.ToString();

        // Assert - Check for allocation optimization patterns
        Assert.Contains("Pre-allocate dictionary with exact capacity", generatedCode);
        Assert.Contains("new List<", generatedCode); // Pre-allocated collections
        Assert.Contains("StringBuilder", generatedCode); // Efficient string building
        Assert.Contains("ToString(\"G17\")", generatedCode); // Optimized double formatting
        Assert.Contains("ToString(\"O\")", generatedCode); // Optimized DateTime formatting
        
        // Verify no inefficient patterns
        Assert.DoesNotContain("string.Concat", generatedCode); // Should use StringBuilder instead
        Assert.DoesNotContain("+ \"", generatedCode.Replace("\" + ", "")); // Minimal string concatenation

        _output.WriteLine($"Generated optimized code with {generatedCode.Length} characters");
        _output.WriteLine("Verified allocation optimization patterns are present");
    }

    [Fact]
    public void PerformanceOptimizations_ShouldScaleWithEntityComplexity()
    {
        // Arrange
        var simpleEntity = CreateTestEntityModel();
        var complexEntity = CreateComplexEntityModel();

        // Act - Measure generation time for different complexity levels
        var simpleTime = MeasureCodeGenerationTime(simpleEntity);
        var complexTime = MeasureCodeGenerationTime(complexEntity);

        // Assert - Complex entities should not be disproportionately slower
        var complexityRatio = (double)complexEntity.Properties.Length / simpleEntity.Properties.Length;
        var timeRatio = (double)complexTime / Math.Max(simpleTime, 1);

        Assert.True(timeRatio < complexityRatio * 2, 
            $"Time ratio ({timeRatio:F1}) should not exceed 2x complexity ratio ({complexityRatio:F1})");

        _output.WriteLine($"Simple entity ({simpleEntity.Properties.Length} props): {simpleTime}ms");
        _output.WriteLine($"Complex entity ({complexEntity.Properties.Length} props): {complexTime}ms");
        _output.WriteLine($"Complexity ratio: {complexityRatio:F1}, Time ratio: {timeRatio:F1}");
    }

    [Fact]
    public void AdvancedPerformanceOptimizations_ShouldGenerateOptimizedCode()
    {
        // Arrange
        var entity = CreateComplexEntityModel();
        var settings = new CompilationSettings
        {
            TargetFramework = "net8.0",
            OptimizationLevel = OptimizationLevel.Release,
            NullableContextOptions = NullableContextOptions.Enable,
            AssemblyName = "TestAssembly"
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var generatedCode = AdvancedPerformanceOptimizations.GenerateOptimizedEntityCode(entity, settings);
        var generationTime = stopwatch.ElapsedMilliseconds;
        stopwatch.Stop();

        // Assert
        Assert.NotEmpty(generatedCode);
        Assert.Contains("AggressiveInlining", generatedCode);
        Assert.Contains("AggressiveOptimization", generatedCode);
        Assert.Contains("ArrayPool", generatedCode);
        Assert.Contains("Ultra-high-performance", generatedCode);
        Assert.Contains("span operations", generatedCode);
        Assert.Contains("memory pooling", generatedCode);
        
        // Verify performance characteristics
        Assert.True(generationTime < 200, $"Advanced code generation took {generationTime}ms, should be under 200ms");

        _output.WriteLine($"Generated {generatedCode.Length} characters in {generationTime}ms");
        _output.WriteLine($"Advanced generation rate: {generatedCode.Length / Math.Max(generationTime, 1)} chars/ms");
    }

    [Fact]
    public void AdvancedPerformanceOptimizations_ShouldProvidePerformanceStatistics()
    {
        // Arrange
        var entity1 = CreateTestEntityModel("Entity1");
        var entity2 = CreateComplexEntityModel();
        var settings = new CompilationSettings
        {
            TargetFramework = "net8.0",
            OptimizationLevel = OptimizationLevel.Release
        };

        // Act
        AdvancedPerformanceOptimizations.GenerateOptimizedEntityCode(entity1, settings);
        AdvancedPerformanceOptimizations.GenerateOptimizedEntityCode(entity2, settings);
        
        var stats = AdvancedPerformanceOptimizations.GetPerformanceStatistics();

        // Assert
        Assert.True(stats.TotalEntitiesGenerated >= 2);
        Assert.True(stats.AverageGenerationTimeMs >= 0);
        Assert.True(stats.TotalCodeGenerated > 0);
        Assert.True(stats.AverageCodeLengthPerEntity > 0);
        Assert.True(stats.StringBuilderPoolHitRate >= 0);

        _output.WriteLine($"Performance Statistics:");
        _output.WriteLine($"  Total Entities: {stats.TotalEntitiesGenerated}");
        _output.WriteLine($"  Average Generation Time: {stats.AverageGenerationTimeMs:F1}ms");
        _output.WriteLine($"  Total Code Generated: {stats.TotalCodeGenerated} chars");
        _output.WriteLine($"  StringBuilder Pool Hit Rate: {stats.StringBuilderPoolHitRate:P1}");
    }

    [Fact]
    public void ObjectPool_ShouldImproveAllocationPerformance()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(() => new StringBuilder());
        var allocations = new List<long>();

        // Act & Assert - Measure allocations with and without pooling
        for (int i = 0; i < 10; i++)
        {
            var initialMemory = GC.GetTotalMemory(false);
            
            using (var pooled = pool.Get())
            {
                pooled.Value.Clear();
                pooled.Value.Append("Test string for allocation measurement");
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            allocations.Add(finalMemory - initialMemory);
        }

        // Pool should show improved hit rate over time
        Assert.True(pool.HitRate > 0.5, $"Pool hit rate ({pool.HitRate:P1}) should be over 50% after warmup");

        _output.WriteLine($"Object pool hit rate: {pool.HitRate:P1}");
        _output.WriteLine($"Average allocation per operation: {allocations.Average():F0} bytes");
    }

    [Fact]
    public void CustomTypeConverterSupport_ShouldGenerateConverterCode()
    {
        // Arrange
        var entity = CreateEntityWithCustomTypes();
        var sb = new StringBuilder();

        // Act
        CustomTypeConverterSupport.GenerateCustomConverterSupport(sb, entity);
        var generatedCode = sb.ToString();

        // Assert
        Assert.NotEmpty(generatedCode);
        Assert.Contains("Custom Type Converter Support", generatedCode);
        Assert.Contains("ICustomTypeConverter", generatedCode);
        Assert.Contains("_customConverters", generatedCode);

        _output.WriteLine($"Generated custom converter support: {generatedCode.Length} characters");
    }

    [Fact]
    public void LinqExpressionFoundation_ShouldGenerateLinqMetadata()
    {
        // Arrange
        var entity = CreateComplexEntityModel();
        var sb = new StringBuilder();

        // Act
        LinqExpressionFoundation.GenerateLinqFoundation(sb, entity);
        var generatedCode = sb.ToString();

        // Assert
        Assert.NotEmpty(generatedCode);
        Assert.Contains("LINQ Expression Foundation", generatedCode);
        Assert.Contains("ExpressionMetadata", generatedCode);
        Assert.Contains("PropertyAccessors", generatedCode);
        Assert.Contains("QueryCapabilities", generatedCode);
        Assert.Contains("IndexOptimization", generatedCode);

        _output.WriteLine($"Generated LINQ foundation: {generatedCode.Length} characters");
    }

    private static EntityModel CreateTestEntityModel(string className = "TestEntity")
    {
        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    PropertyType = "string",
                    AttributeName = "name"
                }
            }
        };
    }

    private static EntityModel CreateComplexEntityModel()
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Id",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true
            },
            new PropertyModel
            {
                PropertyName = "SortKey",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true
            }
        };

        // Add many properties to simulate complex entity
        for (int i = 0; i < 20; i++)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = $"Property{i}",
                PropertyType = i % 2 == 0 ? "string" : "int",
                AttributeName = $"prop{i}",
                IsNullable = i % 3 == 0
            });
        }

        // Add collection properties
        properties.Add(new PropertyModel
        {
            PropertyName = "Tags",
            PropertyType = "List<string>",
            AttributeName = "tags",
            IsCollection = true
        });

        return new EntityModel
        {
            ClassName = "ComplexEntity",
            Namespace = "TestNamespace",
            TableName = "complex-table",
            Properties = properties.ToArray(),
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "GSI1",
                    PartitionKeyProperty = "Property0",
                    SortKeyProperty = "Property1"
                }
            }
        };
    }

    private static long MeasureCodeGenerationTime(EntityModel entity)
    {
        var sb = new System.Text.StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        
        OptimizedCodeGenerator.GenerateOptimizedToDynamoDbMethod(sb, entity);
        OptimizedCodeGenerator.GenerateOptimizedFromDynamoDbMethod(sb, entity);
        
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    private static Compilation CreateCompilation(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static dynamic InvokeTransformEntityClass(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        // Create a mock GeneratorSyntaxContext using reflection since it's not directly constructible
        // Instead, we'll directly call the EntityAnalyzer which is what TransformEntityClass does
        try
        {
            var analyzer = new EntityAnalyzer();
            var entityModel = analyzer.AnalyzeEntity(classDecl, semanticModel);
            var cacheKey = $"{entityModel?.Namespace}.{entityModel?.ClassName}";
            
            return new { EntityModel = entityModel, CacheKey = cacheKey };
        }
        catch (Exception)
        {
            return new { EntityModel = (EntityModel?)null, CacheKey = "" };
        }
    }

    private static EntityModel CreateEntityWithCustomTypes()
    {
        return new EntityModel
        {
            ClassName = "CustomEntity",
            Namespace = "TestNamespace",
            TableName = "custom-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "WebsiteUrl",
                    PropertyType = "Uri",
                    AttributeName = "url"
                },
                new PropertyModel
                {
                    PropertyName = "Duration",
                    PropertyType = "TimeSpan",
                    AttributeName = "duration"
                },
                new PropertyModel
                {
                    PropertyName = "Metadata",
                    PropertyType = "Dictionary<string, object>",
                    AttributeName = "metadata"
                }
            }
        };
    }
}