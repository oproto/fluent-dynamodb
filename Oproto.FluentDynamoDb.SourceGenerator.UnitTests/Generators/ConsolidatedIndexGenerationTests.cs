using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for consolidated index generation in multi-entity tables.
/// 
/// **Feature: multi-entity-index-consolidation**
/// **Validates: Requirements 1.1, 1.2, 1.3, 4.1, 4.2, 4.3, 6.1, 6.2**
/// </summary>
[Trait("Category", "Unit")]
public class ConsolidatedIndexGenerationTests
{
    /// <summary>
    /// Tests that indexes from multiple entities appear on the generated table class.
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// </summary>
    [Fact]
    public void MultiEntityTable_WithIndexesOnDifferentEntities_GeneratesAllIndexProperties()
    {
        // Arrange - Entity1 has gsi1, Entity2 has gsi2
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""gsi1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi1pk"")]
        public string StatusIndex { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class Customer
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""gsi2"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi2pk"")]
        public string EmailIndex { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("SharedTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1, "should generate one table class");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Both indexes should appear on the table class
        tableCode.Should().Contain("public DynamoDbIndex Gsi1",
            "should generate index property for gsi1 from Order entity");
        tableCode.Should().Contain("public DynamoDbIndex Gsi2",
            "should generate index property for gsi2 from Customer entity");
    }

    /// <summary>
    /// Tests that indexes defined on non-default entities are still generated.
    /// **Validates: Requirement 1.3**
    /// </summary>
    [Fact]
    public void MultiEntityTable_IndexOnNonDefaultEntity_GeneratesIndexProperty()
    {
        // Arrange - Default entity has no indexes, non-default entity has gsi1
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class Customer
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""email-index"", IsPartitionKey = true)]
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("SharedTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Index from non-default entity should appear
        tableCode.Should().Contain("EmailIndex",
            "should generate index property for email-index from non-default Customer entity");
    }

    /// <summary>
    /// Tests that multiple entities defining the same index with identical configuration
    /// results in a single index property.
    /// **Validates: Requirement 1.2**
    /// </summary>
    [Fact]
    public void MultiEntityTable_SameIndexSameConfig_GeneratesSingleIndexProperty()
    {
        // Arrange - Both entities define gsi1 with same configuration
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""gsi1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi1pk"")]
        public string Gsi1Pk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class Customer
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""gsi1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi1pk"")]
        public string Gsi1Pk { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("SharedTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should have exactly one gsi1 property
        var gsi1Count = System.Text.RegularExpressions.Regex.Matches(
            tableCode, 
            @"public\s+DynamoDbIndex\s+Gsi1\s*=>").Count;
        
        gsi1Count.Should().Be(1, "should generate exactly one index property for gsi1");
    }

    /// <summary>
    /// Tests that conflicting index configurations emit diagnostics and don't generate index properties.
    /// **Validates: Requirements 2.1, 2.4**
    /// </summary>
    [Fact]
    public void MultiEntityTable_ConflictingPartitionKeys_EmitsDiagnosticAndNoIndexProperty()
    {
        // Arrange - Both entities define gsi1 with different partition keys
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""gsi1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class Customer
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""gsi1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - Should have FDDB053 diagnostic for conflicting partition keys
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB053",
            "should emit FDDB053 diagnostic for conflicting partition keys");
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("SharedTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should NOT generate index property when there's a conflict
        tableCode.Should().NotContain("public DynamoDbIndex Gsi1",
            "should not generate index property when there's a configuration conflict");
    }

    /// <summary>
    /// Tests that conflicting index types (GSI vs LSI) emit diagnostics.
    /// **Validates: Requirements 2.3, 2.4**
    /// </summary>
    [Fact]
    public void MultiEntityTable_ConflictingIndexTypes_EmitsDiagnostic()
    {
        // Arrange - One entity defines GSI, another defines LSI with same name
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""index1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class Customer
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [LocalSecondaryIndex(""index1"")]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - Should have FDDB055 diagnostic for conflicting index types
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB055",
            "should emit FDDB055 diagnostic for conflicting index types (GSI vs LSI)");
    }

    /// <summary>
    /// Tests that single-entity tables still work correctly (backward compatibility).
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Fact]
    public void SingleEntityTable_GeneratesIndexPropertiesAsExpected()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""status-index"", IsPartitionKey = true)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""date-index"", IsPartitionKey = true)]
        [DynamoDbAttribute(""date"")]
        public string Date { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Both indexes should be generated
        tableCode.Should().Contain("StatusIndex",
            "should generate index property for status-index");
        tableCode.Should().Contain("DateIndex",
            "should generate index property for date-index");
    }

    /// <summary>
    /// Tests that index documentation includes referencing entities.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void MultiEntityTable_IndexDocumentation_IncludesReferencingEntities()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""gsi1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi1pk"")]
        public string Gsi1Pk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class Customer
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GlobalSecondaryIndex(""gsi1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi1pk"")]
        public string Gsi1Pk { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("SharedTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Documentation should mention both entities
        tableCode.Should().Contain("Referenced by:",
            "should include 'Referenced by:' in index documentation");
        tableCode.Should().Contain("Order",
            "should mention Order entity in index documentation");
        tableCode.Should().Contain("Customer",
            "should mention Customer entity in index documentation");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source)
            },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
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
}
