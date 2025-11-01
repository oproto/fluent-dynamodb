using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using System.Diagnostics;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Performance;

/// <summary>
/// Performance tests to ensure the source generator performs well with various scenarios.
/// These tests verify that code generation completes within reasonable time limits.
/// </summary>
public class SourceGeneratorPerformanceTests
{
    [Fact]
    public void SourceGenerator_WithSingleEntity_CompletesQuickly()
    {
        // Arrange
        var source = CreateBasicEntitySource("TestEntity", "test-table");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GenerateCode(source);
        stopwatch.Stop();

        // Assert
        // Should generate warnings for reserved words only
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warnings for "name", "count"
        result.GeneratedSources.Should().HaveCount(4); // Fields, Keys, Entity, Table
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Single entity generation should complete within 1 second");
    }

    [Fact]
    public void SourceGenerator_WithMultipleEntities_ScalesWell()
    {
        // Arrange
        var sources = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            sources.Add(CreateBasicEntitySource($"TestEntity{i}", $"test-table-{i}"));
        }
        var combinedSource = string.Join("\n\n", sources);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GenerateCode(combinedSource);
        stopwatch.Stop();

        // Assert
        // Should generate warnings for reserved words and scalability for each entity
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warnings
        result.GeneratedSources.Should().HaveCount(40); // 4 files per entity * 10 entities (Fields, Keys, Entity, Table)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Multiple entity generation should complete within 5 seconds");
    }

    [Fact]
    public void SourceGenerator_WithComplexEntity_CompletesReasonably()
    {
        // Arrange
        var source = CreateComplexEntitySource();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GenerateCode(source);
        stopwatch.Stop();

        // Assert
        // Complex entities should generate legitimate warnings but still produce code
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warnings for "name", "status"
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB023"); // Performance warnings for collections
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB009"); // Unsupported type warnings for complex objects
        result.GeneratedSources.Should().HaveCount(5); // Fields, Keys, Entity, Table, Table.Indexes (has GSI)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Complex entity generation should complete within 2 seconds");
    }

    [Fact]
    public void SourceGenerator_WithManyProperties_HandlesEfficiently()
    {
        // Arrange
        var source = CreateEntityWithManyProperties(50); // Entity with 50 properties
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GenerateCode(source);
        stopwatch.Stop();

        // Assert
        // Should generate warnings for too many attributes and scalability
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB029"); // Too many attributes warning
        result.GeneratedSources.Should().HaveCount(4); // Fields, Keys, Entity, Table
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "Entity with many properties should complete within 3 seconds");

        // Verify all properties are included
        var fieldsCode = GetGeneratedSource(result, "ManyPropertiesEntity.g.cs");
        fieldsCode.Should().Contain("Property0");
        fieldsCode.Should().Contain("Property49");
    }

    [Fact]
    public void SourceGenerator_WithManyGSIs_HandlesEfficiently()
    {
        // Arrange
        var source = CreateEntityWithManyGSIs(10); // Entity with 10 GSIs
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GenerateCode(source);
        stopwatch.Stop();

        // Assert
        // Note: DYNDB027 scalability warnings were removed in Task 39 as they cannot be
        // accurately detected at compile time. This test now focuses on performance only.
        result.GeneratedSources.Should().HaveCount(5); // Fields, Keys, Entity, Table, Table.Indexes (has GSI)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "Entity with many GSIs should complete within 3 seconds");

        // Verify all GSIs are included in generated code
        var fieldsCode = GetGeneratedSource(result, "ManyGSIsEntityFields.g.cs");
        fieldsCode.Should().Contain("GSI0Fields");
        fieldsCode.Should().Contain("GSI9Fields");

        // No scalability warnings expected - removed in Task 39
        var scalabilityWarnings = result.Diagnostics.Where(d => d.Id == "DYNDB027").ToList();
        scalabilityWarnings.Should().BeEmpty("DYNDB027 scalability warnings were removed in Task 39");
    }

    [Fact]
    public void SourceGenerator_WithManyRelatedEntities_HandlesEfficiently()
    {
        // Arrange
        var source = CreateEntityWithManyRelatedEntities(20); // Entity with 20 related entities
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GenerateCode(source);
        stopwatch.Stop();

        // Assert
        // Should generate performance warnings for collection properties but still produce code
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB023"); // Performance warnings for collections
        result.GeneratedSources.Should().HaveCount(4); // Fields, Keys, Entity, Table
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(4000, "Entity with many related entities should complete within 4 seconds");

        // Verify related entities are included in generated code structure
        var entityCode = GetGeneratedSource(result, "ManyRelatedEntitiesEntity.g.cs");
        entityCode.Should().Contain("public partial class ManyRelatedEntitiesEntity : IDynamoDbEntity");
        entityCode.Should().Contain("public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, IDynamoDbLogger? logger = null)");
        entityCode.Should().Contain("public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, IDynamoDbLogger? logger = null)");
    }

    [Fact]
    public void SourceGenerator_RepeatedGeneration_ShowsConsistentPerformance()
    {
        // Arrange
        var source = CreateBasicEntitySource("TestEntity", "test-table");
        var times = new List<long>();

        // Warmup - Run once to allow JIT compilation
        var warmupResult = GenerateCode(source);
        warmupResult.Diagnostics.Should().NotBeEmpty();

        // Act - Run generation multiple times after warmup
        for (int i = 0; i < 5; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = GenerateCode(source);
            stopwatch.Stop();

            // Should generate warnings for reserved words
            result.Diagnostics.Should().NotBeEmpty();
            result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warnings
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert
        var averageTime = times.Average();
        var maxTime = times.Max();
        var minTime = times.Min();

        averageTime.Should().BeLessThan(1000, "Average generation time should be under 1 second");
        maxTime.Should().BeLessThan(2000, "Maximum generation time should be under 2 seconds");

        // Performance should be relatively consistent after warmup (max shouldn't be more than 10x min)
        // Increased tolerance to account for system variations and GC pauses
        (maxTime / Math.Max(minTime, 1)).Should().BeLessThan(10, "Performance should be relatively consistent after warmup");
    }

    [Fact]
    public void SourceGenerator_LargeSourceFile_HandlesEfficiently()
    {
        // Arrange - Create a large source file with comments and whitespace
        var source = CreateLargeSourceFile();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GenerateCode(source);
        stopwatch.Stop();

        // Assert
        // Should generate warnings for reserved word "name" and scalability
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warning for "name"
        result.GeneratedSources.Should().HaveCount(4); // Fields, Keys, Entity, Table
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "Large source file should be processed within 3 seconds");
    }

    private static string CreateBasicEntitySource(string className, string tableName)
    {
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""{tableName}"")]
    public partial class {className}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""count"")]
        public int Count {{ get; set; }}
    }}
}}";
    }

    private static string CreateComplexEntitySource()
    {
        return @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""complex-table"")]
    public partial class ComplexEntity
    {
        [PartitionKey(Prefix = ""tenant"", Separator = ""#"")]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey(Prefix = ""item"", Separator = ""#"")]
        [DynamoDbAttribute(""sk"")]
        public string ItemId { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""status"")]
        [GlobalSecondaryIndex(""StatusIndex"", IsPartitionKey = true)]
        public string Status { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""created_date"")]
        [GlobalSecondaryIndex(""StatusIndex"", IsSortKey = true)]
        [GlobalSecondaryIndex(""DateIndex"", IsPartitionKey = true)]
        public DateTime CreatedDate { get; set; }
        
        [DynamoDbAttribute(""category"")]
        [GlobalSecondaryIndex(""CategoryIndex"", IsPartitionKey = true)]
        public string Category { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""tags"")]
        public List<string>? Tags { get; set; }
        
        [DynamoDbAttribute(""metadata"")]
        public Dictionary<string, string>? Metadata { get; set; }
        
        [RelatedEntity(""audit#*"")]
        public List<AuditEntry>? AuditEntries { get; set; }
        
        [RelatedEntity(""summary"")]
        public SummaryEntity? Summary { get; set; }
        
        [RelatedEntity(""child#*"")]
        public List<ChildEntity>? Children { get; set; }
    }
    
    public class AuditEntry
    {
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; } = string.Empty;
    }
    
    public class SummaryEntity
    {
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    public class ChildEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsActive { get; set; }
    }
}";
    }

    private static string CreateEntityWithManyProperties(int propertyCount)
    {
        var properties = new List<string>();
        for (int i = 0; i < propertyCount; i++)
        {
            properties.Add($@"
        [DynamoDbAttribute(""property{i}"")]
        public string Property{i} {{ get; set; }} = string.Empty;");
        }

        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""many-properties-table"")]
    public partial class ManyPropertiesEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        {string.Join("", properties)}
    }}
}}";
    }

    private static string CreateEntityWithManyGSIs(int gsiCount)
    {
        var properties = new List<string>();
        for (int i = 0; i < gsiCount; i++)
        {
            properties.Add($@"
        [DynamoDbAttribute(""gsi{i}_pk"")]
        [GlobalSecondaryIndex(""GSI{i}"", IsPartitionKey = true)]
        public string Gsi{i}PartitionKey {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""gsi{i}_sk"")]
        [GlobalSecondaryIndex(""GSI{i}"", IsSortKey = true)]
        public string Gsi{i}SortKey {{ get; set; }} = string.Empty;");
        }

        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""many-gsis-table"")]
    public partial class ManyGSIsEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        {string.Join("", properties)}
    }}
}}";
    }

    private static string CreateEntityWithManyRelatedEntities(int relatedEntityCount)
    {
        var properties = new List<string>();
        var relatedClasses = new List<string>();

        for (int i = 0; i < relatedEntityCount; i++)
        {
            properties.Add($@"
        [RelatedEntity(""related{i}#*"")]
        public List<RelatedEntity{i}>? RelatedEntity{i} {{ get; set; }}");

            relatedClasses.Add($@"
    public class RelatedEntity{i}
    {{
        public string Name {{ get; set; }} = string.Empty;
        public int Value {{ get; set; }}
    }}");
        }

        return $@"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""many-related-entities-table"")]
    public partial class ManyRelatedEntitiesEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey {{ get; set; }} = string.Empty;
        {string.Join("", properties)}
    }}
    {string.Join("", relatedClasses)}
}}";
    }

    private static string CreateLargeSourceFile()
    {
        var largeComments = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"    // This is comment line {i} to make the file larger"));

        return $@"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    /// <summary>
    /// This is a large entity class with lots of comments and whitespace
    /// to test how the source generator handles large files.
    /// </summary>
{largeComments}
    [DynamoDbTable(""large-file-table"")]
    public partial class LargeFileEntity
    {{
        /// <summary>
        /// The partition key for this entity.
        /// </summary>
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        /// <summary>
        /// The name of the entity.
        /// </summary>
        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
        
        /// <summary>
        /// A description field with lots of documentation.
        /// This field can contain long text values.
        /// It's used for storing detailed information about the entity.
        /// </summary>
        [DynamoDbAttribute(""description"")]
        public string? Description {{ get; set; }}
    }}
}}";
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        // Include attribute definitions in the compilation
        var attributeSource = @"
using System;

namespace Oproto.FluentDynamoDb.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamoDbTableAttribute : Attribute
    {
        public string TableName { get; }
        public string? EntityDiscriminator { get; set; }
        public DynamoDbTableAttribute(string tableName) => TableName = tableName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DynamoDbAttributeAttribute : Attribute
    {
        public string AttributeName { get; }
        public DynamoDbAttributeAttribute(string attributeName) => AttributeName = attributeName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PartitionKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string? Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SortKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string? Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class GlobalSecondaryIndexAttribute : Attribute
    {
        public string IndexName { get; }
        public bool IsPartitionKey { get; set; }
        public bool IsSortKey { get; set; }
        public string? KeyFormat { get; set; }
        public GlobalSecondaryIndexAttribute(string indexName) => IndexName = indexName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class RelatedEntityAttribute : Attribute
    {
        public string SortKeyPattern { get; }
        public Type? EntityType { get; set; }
        public RelatedEntityAttribute(string sortKeyPattern) => SortKeyPattern = sortKeyPattern;
    }
}";

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText(attributeSource)
            },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                // Add AWS SDK references for generated code
                MetadataReference.CreateFromFile(typeof(Amazon.DynamoDBv2.Model.AttributeValue).Assembly.Location),
                // Add main library reference for IDynamoDbEntity and other types
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Storage.IDynamoDbEntity).Assembly.Location),
                // Add System.Linq reference
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                // Add System.IO reference  
                MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        source.Should().NotBeNull($"Generated source file {fileName} should exist");
        return source!.SourceText.ToString();
    }
}