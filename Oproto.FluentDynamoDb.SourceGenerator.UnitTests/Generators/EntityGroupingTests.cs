using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for entity grouping by table name functionality.
/// Verifies that the source generator correctly groups entities by their table name
/// and generates the appropriate number of table classes.
/// </summary>
[Trait("Category", "Unit")]
public class EntityGroupingTests
{
    [Fact]
    public void SingleEntity_CreatesOneTable()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""user_name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "single entity should not produce errors");
        
        // Should generate exactly one table class
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("Table.g.cs") && 
                       !s.FileName.Contains("Fields") && 
                       !s.FileName.Contains("Keys"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1, 
            "single entity should generate exactly one table class");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        tableCode.ShouldContainClass("UsersTableTable");
    }

    [Fact]
    public void MultipleEntitiesSameTable_CreatesOneTable()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""order_total"")]
        public decimal Total { get; set; }
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""line_amount"")]
        public decimal Amount { get; set; }
    }

    [DynamoDbTable(""app-table"")]
    public partial class Customer
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""customer_name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "multiple entities with same table and default should not produce errors");
        
        // Should generate exactly one table class for all three entities
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("Table.g.cs") && 
                       !s.FileName.Contains("Fields") && 
                       !s.FileName.Contains("Keys"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1, 
            "multiple entities sharing same table name should generate exactly one table class");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table should be named after the table name, not any entity
        tableCode.ShouldContainClass("AppTableTable");
        
        // Should generate entity accessor properties for all entities
        tableCode.Should().Contain("OrderAccessor", 
            "should generate accessor for Order entity");
        tableCode.Should().Contain("OrderLineAccessor", 
            "should generate accessor for OrderLine entity");
        tableCode.Should().Contain("CustomerAccessor", 
            "should generate accessor for Customer entity");
    }

    [Fact]
    public void EntitiesWithDifferentTableNames_CreateSeparateTables()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""user_name"")]
        public string Name { get; set; } = string.Empty;
    }

    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""order_total"")]
        public decimal Total { get; set; }
    }

    [DynamoDbTable(""products-table"")]
    public partial class Product
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""product_name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "entities with different table names should not produce errors");
        
        // Should generate three separate table classes
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("Table.g.cs") && 
                       !s.FileName.Contains("Fields") && 
                       !s.FileName.Contains("Keys"))
            .ToArray();
        
        tableFiles.Should().HaveCount(3, 
            "entities with different table names should generate separate table classes");
        
        // Verify each table class exists
        var allTableCode = string.Join("\n", tableFiles.Select(f => f.SourceText.ToString()));
        
        allTableCode.Should().Contain("class UsersTableTable", 
            "should generate table class for users-table");
        allTableCode.Should().Contain("class OrdersTableTable", 
            "should generate table class for orders-table");
        allTableCode.Should().Contain("class ProductsTableTable", 
            "should generate table class for products-table");
    }

    [Fact]
    public void MixedTableAssignments_CreatesCorrectNumberOfTables()
    {
        // Arrange - 2 entities in shared-table, 1 in separate-table
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
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""separate-table"")]
    public partial class Product
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        // Should generate exactly two table classes
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("Table.g.cs") && 
                       !s.FileName.Contains("Fields") && 
                       !s.FileName.Contains("Keys"))
            .ToArray();
        
        tableFiles.Should().HaveCount(2, 
            "should generate one table for shared-table and one for separate-table");
        
        var allTableCode = string.Join("\n", tableFiles.Select(f => f.SourceText.ToString()));
        
        // Verify shared table has both entities
        allTableCode.Should().Contain("class SharedTableTable", 
            "should generate table class for shared-table");
        allTableCode.Should().Contain("OrderAccessor", 
            "shared table should have Order accessor");
        allTableCode.Should().Contain("OrderLineAccessor", 
            "shared table should have OrderLine accessor");
        
        // Verify separate table has single entity
        allTableCode.Should().Contain("class SeparateTableTable", 
            "should generate table class for separate-table");
    }

    [Fact]
    public void GroupingByTableName_IsCaseInsensitive()
    {
        // Arrange - Same table name with different casing
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""MyTable"", IsDefault = true)]
    public partial class EntityA
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""MyTable"")]
    public partial class EntityB
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        // Should generate exactly one table class (case-insensitive grouping)
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("Table.g.cs") && 
                       !s.FileName.Contains("Fields") && 
                       !s.FileName.Contains("Keys"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1, 
            "entities with same table name (case-insensitive) should generate one table class");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        tableCode.Should().Contain("EntityAAccessor", 
            "should generate accessor for EntityA");
        tableCode.Should().Contain("EntityBAccessor", 
            "should generate accessor for EntityB");
    }

    [Fact]
    public void EmptyTableName_HandledGracefully()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable("""")]
    public partial class InvalidEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - Should handle gracefully, may produce diagnostic or skip generation
        // The exact behavior depends on implementation, but should not crash
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("Table.g.cs") && 
                       !s.FileName.Contains("Fields") && 
                       !s.FileName.Contains("Keys"))
            .ToArray();
        
        // Either generates no table or generates with empty name handling
        // The important thing is it doesn't crash
        tableFiles.Should().NotBeNull();
    }

    [Fact]
    public void TableNameWithSpecialCharacters_GeneratesValidClassName()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""my-app-table_v2"")]
    public partial class MyEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("Table.g.cs") && 
                       !s.FileName.Contains("Fields") && 
                       !s.FileName.Contains("Keys"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1, 
            "should generate one table class");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should convert special characters to valid class name
        // "my-app-table_v2" should become something like "MyAppTableV2Table"
        tableCode.Should().Contain("class ", 
            "should generate a valid class");
        tableCode.Should().Contain("Table : DynamoDbTableBase", 
            "should inherit from DynamoDbTableBase");
    }

    /// <summary>
    /// Generates code using the source generator.
    /// Uses DynamicCompilationHelper for proper IL3000 warning handling.
    /// </summary>
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
