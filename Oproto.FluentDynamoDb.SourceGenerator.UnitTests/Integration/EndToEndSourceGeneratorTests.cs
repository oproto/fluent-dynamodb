using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// End-to-end integration tests that verify complete source generation scenarios.
/// These tests simulate real-world usage patterns and verify the generated code compiles and works correctly.
/// </summary>
public class EndToEndSourceGeneratorTests
{
    [Fact]
    public void SourceGenerator_WithCompleteEntity_GeneratesAllExpectedFiles()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""transactions"")]
    public partial class TransactionEntity
    {
        [PartitionKey(Prefix = ""tenant"", Separator = ""#"")]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey(Prefix = ""txn"", Separator = ""#"")]
        [DynamoDbAttribute(""sk"")]
        public string TransactionId { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
        
        [DynamoDbAttribute(""status"")]
        [GlobalSecondaryIndex(""StatusIndex"", IsPartitionKey = true)]
        public string Status { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""created_date"")]
        [GlobalSecondaryIndex(""StatusIndex"", IsSortKey = true)]
        public DateTime CreatedDate { get; set; }
        
        [DynamoDbAttribute(""tags"")]
        public List<string>? Tags { get; set; }
        
        [RelatedEntity(""audit#*"")]
        public List<AuditEntry>? AuditEntries { get; set; }
        
        [RelatedEntity(""summary"")]
        public TransactionSummary? Summary { get; set; }
    }
    
    public class AuditEntry
    {
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
    
    public class TransactionSummary
    {
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        // Verify entity implementation
        var entityCode = GetGeneratedSource(result, "TransactionEntity.g.cs");
        entityCode.Should().Contain("public partial class TransactionEntity : IDynamoDbEntity");
        entityCode.Should().Contain("public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)");
        entityCode.Should().Contain("public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item)");
        entityCode.Should().Contain("public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items)");
        entityCode.Should().Contain("public static string GetPartitionKey(Dictionary<string, AttributeValue> item)");
        entityCode.Should().Contain("public static bool MatchesEntity(Dictionary<string, AttributeValue> item)");
        entityCode.Should().Contain("public static EntityMetadata GetEntityMetadata()");
        
        // Verify fields class
        var fieldsCode = GetGeneratedSource(result, "TransactionEntityFields.g.cs");
        fieldsCode.Should().Contain("public static partial class TransactionEntityFields");
        fieldsCode.Should().Contain("public const string TenantId = \"pk\";");
        fieldsCode.Should().Contain("public const string TransactionId = \"sk\";");
        fieldsCode.Should().Contain("public const string Amount = \"amount\";");
        fieldsCode.Should().Contain("public const string Status = \"status\";");
        fieldsCode.Should().Contain("public static partial class StatusIndexFields");
        fieldsCode.Should().Contain("public const string PartitionKey = \"status\";");
        fieldsCode.Should().Contain("public const string SortKey = \"created_date\";");
        
        // Verify keys class
        var keysCode = GetGeneratedSource(result, "TransactionEntityKeys.g.cs");
        keysCode.Should().Contain("public static partial class TransactionEntityKeys");
        keysCode.Should().Contain("public static string Pk(string tenantId)");
        keysCode.Should().Contain("return \"tenant#\" + tenantId;");
        keysCode.Should().Contain("public static string Sk(string transactionId)");
        keysCode.Should().Contain("return \"txn#\" + transactionId;");
        keysCode.Should().Contain("public static (string PartitionKey, string SortKey) Key(string tenantId, string transactionId)");
        keysCode.Should().Contain("public static partial class StatusIndexKeys");
    }

    [Fact]
    public void SourceGenerator_WithMultiItemEntity_GeneratesMultiItemSupport()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""multi-item-table"")]
    public partial class MultiItemEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""items"")]
        public List<string> Items { get; set; } = new();
        
        [DynamoDbAttribute(""details"")]
        public List<DetailItem> Details { get; set; } = new();
    }
    
    public class DetailItem
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var entityCode = GetGeneratedSource(result, "MultiItemEntity.g.cs");
        
        // Should detect as multi-item entity due to collections
        entityCode.Should().Contain("public static List<Dictionary<string, AttributeValue>> ToDynamoDbMultiple<TSelf>(TSelf entity)");
        entityCode.Should().Contain("// Serialize collection Items as JSON for single-item storage");
        entityCode.Should().Contain("// Serialize collection Details as JSON for single-item storage");
        entityCode.Should().Contain("System.Text.Json.JsonSerializer.Serialize");
        entityCode.Should().Contain("System.Text.Json.JsonSerializer.Deserialize");
    }

    [Fact]
    public void SourceGenerator_WithRelatedEntities_GeneratesRelationshipMapping()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""related-entities-table"")]
    public partial class ParentEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
        
        [RelatedEntity(""child#*"")]
        public List<ChildEntity>? Children { get; set; }
        
        [RelatedEntity(""metadata"")]
        public MetadataEntity? Metadata { get; set; }
        
        [RelatedEntity(""audit#*"")]
        public List<AuditEntity>? AuditLog { get; set; }
    }
    
    public class ChildEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
    }
    
    public class MetadataEntity
    {
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }
    
    public class AuditEntity
    {
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var entityCode = GetGeneratedSource(result, "ParentEntity.g.cs");
        
        // Should generate related entity mapping logic
        entityCode.Should().Contain("Related entities: 3 relationship(s) defined.");
        entityCode.Should().Contain("// Populate related entity properties based on sort key patterns");
        entityCode.Should().Contain("// Map related entity: Children");
        entityCode.Should().Contain("// Map related entity: Metadata");
        entityCode.Should().Contain("// Map related entity: AuditLog");
        entityCode.Should().Contain("if (sortKey.StartsWith(\"child#\"))");
        entityCode.Should().Contain("if (sortKey == \"metadata\" || sortKey.StartsWith(\"metadata#\"))");
        entityCode.Should().Contain("if (sortKey.StartsWith(\"audit#\"))");
    }

    [Fact]
    public void SourceGenerator_WithComplexTypes_GeneratesCorrectConversions()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""complex-types-table"")]
    public partial class ComplexTypesEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""count"")]
        public int Count { get; set; }
        
        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
        
        [DynamoDbAttribute(""is_active"")]
        public bool IsActive { get; set; }
        
        [DynamoDbAttribute(""created_date"")]
        public DateTime CreatedDate { get; set; }
        
        [DynamoDbAttribute(""unique_id"")]
        public Guid UniqueId { get; set; }
        
        [DynamoDbAttribute(""optional_count"")]
        public int? OptionalCount { get; set; }
        
        [DynamoDbAttribute(""optional_text"")]
        public string? OptionalText { get; set; }
        
        [DynamoDbAttribute(""data"")]
        public byte[]? Data { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var entityCode = GetGeneratedSource(result, "ComplexTypesEntity.g.cs");
        
        // Verify type conversions in ToDynamoDb
        entityCode.Should().Contain("new AttributeValue { S = typedEntity.Id }");
        entityCode.Should().Contain("new AttributeValue { N = typedEntity.Count.ToString() }");
        entityCode.Should().Contain("new AttributeValue { N = typedEntity.Amount.ToString() }");
        entityCode.Should().Contain("new AttributeValue { BOOL = typedEntity.IsActive }");
        entityCode.Should().Contain("new AttributeValue { S = typedEntity.CreatedDate.ToString(\"O\") }");
        entityCode.Should().Contain("new AttributeValue { S = typedEntity.UniqueId.ToString() }");
        entityCode.Should().Contain("new AttributeValue { B = new MemoryStream(typedEntity.Data) }");
        
        // Verify nullable handling
        entityCode.Should().Contain("if (typedEntity.OptionalCount != null)");
        entityCode.Should().Contain("if (typedEntity.OptionalText != null)");
        entityCode.Should().Contain("if (typedEntity.Data != null)");
        
        // Verify type conversions in FromDynamoDb
        entityCode.Should().Contain("entity.Id = idValue.S");
        entityCode.Should().Contain("entity.Count = int.Parse(countValue.N)");
        entityCode.Should().Contain("entity.Amount = decimal.Parse(amountValue.N)");
        entityCode.Should().Contain("entity.IsActive = isActiveValue.BOOL");
        entityCode.Should().Contain("entity.CreatedDate = DateTime.Parse(createdDateValue.S)");
        entityCode.Should().Contain("entity.UniqueId = Guid.Parse(uniqueIdValue.S)");
        entityCode.Should().Contain("entity.Data = dataValue.B.ToArray()");
    }

    [Fact]
    public void SourceGenerator_WithErrorScenarios_GeneratesDiagnostics()
    {
        // Arrange - Entity without partition key
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invalid-table"")]
    public partial class InvalidEntity
    {
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].Id.Should().Be("DYNDB001");
        result.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result.GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void SourceGenerator_WithMultiplePartitionKeys_GeneratesDiagnostics()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invalid-table"")]
    public partial class InvalidEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk1"")]
        public string Id1 { get; set; } = string.Empty;
        
        [PartitionKey]
        [DynamoDbAttribute(""pk2"")]
        public string Id2 { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].Id.Should().Be("DYNDB002");
        result.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result.GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void SourceGenerator_WithNonPartialClass_GeneratesDiagnostics()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invalid-table"")]
    public class NonPartialEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].Id.Should().Be("DYNDB010");
        result.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result.GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void SourceGenerator_WithRelatedEntitiesButNoSortKey_GeneratesWarning()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""warning-table"")]
    public partial class WarningEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [RelatedEntity(""audit#*"")]
        public List<AuditEntry>? AuditEntries { get; set; }
    }
    
    public class AuditEntry
    {
        public string Action { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].Id.Should().Be("DYNDB016");
        result.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        result.GeneratedSources.Should().HaveCount(3); // Should still generate code despite warning
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

    [AttributeUsage(AttributeTargets.Property)]
    public class QueryableAttribute : Attribute
    {
        public string[] SupportedOperations { get; set; } = Array.Empty<string>();
        public string[]? AvailableInIndexes { get; set; }
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
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
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