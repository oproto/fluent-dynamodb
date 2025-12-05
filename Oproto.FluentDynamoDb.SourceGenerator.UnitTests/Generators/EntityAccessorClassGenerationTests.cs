using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for entity accessor class generation.
/// Verifies that nested accessor classes are generated correctly with proper structure,
/// parent table references, constructors, and operation methods.
/// </summary>
[Trait("Category", "Unit")]
public class EntityAccessorClassGenerationTests
{
    [Fact]
    public void EntityAccessorClass_IsGeneratedForEachEntity()
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
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class Customer
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
            .Where(s => s.FileName.Contains("SharedTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should generate nested accessor class for each entity
        tableCode.ShouldContainClass("OrderAccessor",
            "should generate OrderAccessor class for Order entity");
        tableCode.ShouldContainClass("OrderLineAccessor",
            "should generate OrderLineAccessor class for OrderLine entity");
        tableCode.ShouldContainClass("CustomerAccessor",
            "should generate CustomerAccessor class for Customer entity");
    }

    [Fact]
    public void EntityAccessorClass_HasCorrectName()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    public partial class Product
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class Inventory
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
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Accessor class names should follow {EntityName}Accessor pattern
        tableCode.Should().Contain("public class ProductAccessor",
            "accessor class should be named ProductAccessor (entity name + 'Accessor')");
        tableCode.Should().Contain("public class InventoryAccessor",
            "accessor class should be named InventoryAccessor (entity name + 'Accessor')");
    }

    [Fact]
    public void EntityAccessorClass_HasParentTableField()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""my-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""my-table"")]
    public partial class OrderLine
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
            .Where(s => s.FileName.Contains("MyTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Each accessor should have a private readonly field for parent table reference
        tableCode.Should().Contain("private readonly MyTableTable _table;",
            "OrderAccessor should have private readonly field for parent table");
        
        // Note: Both accessors will have the same field name but in different class contexts
        // We verify the pattern exists
        var fieldCount = System.Text.RegularExpressions.Regex.Matches(
            tableCode, 
            @"private readonly MyTableTable _table;").Count;
        
        fieldCount.Should().BeGreaterThanOrEqualTo(2,
            "should have parent table field in each accessor class");
    }

    [Fact]
    public void EntityAccessorClass_HasInternalConstructor()
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
    }

    [DynamoDbTable(""app-table"")]
    public partial class Customer
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
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Each accessor should have internal constructor accepting parent table
        tableCode.Should().Contain("internal OrderAccessor(AppTableTable table)",
            "OrderAccessor should have internal constructor accepting parent table");
        tableCode.Should().Contain("internal CustomerAccessor(AppTableTable table)",
            "CustomerAccessor should have internal constructor accepting parent table");
        
        // Constructor should assign the table parameter to the field
        tableCode.Should().Contain("_table = table;",
            "constructor should assign table parameter to _table field");
    }

    [Fact]
    public void EntityAccessorClass_ConstructorAssignsTableField()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""data-table"", IsDefault = true)]
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
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("DataTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Verify constructor body assigns parameter to field
        tableCode.ShouldContainAssignment("_table",
            "constructor should assign table parameter to _table field");
    }

    [Fact]
    public void EntityAccessorClass_ContainsOperationMethods()
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
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // By default, accessor should contain operation methods
        // We'll verify the presence of key operations that are generated
        tableCode.Should().Contain("Get(string pk)",
            "accessor should contain Get operation method");
        tableCode.Should().Contain("Query()",
            "accessor should contain Query operation method");
        tableCode.Should().Contain("Put()",
            "accessor should contain Put operation method");
        tableCode.Should().Contain("Delete(string pk)",
            "accessor should contain Delete operation method");
        tableCode.Should().Contain("Update(string pk)",
            "accessor should contain Update operation method");
    }

    [Fact]
    public void EntityAccessorClass_SingleEntity_GeneratesAccessor()
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
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Even single entity tables should generate accessor class
        tableCode.ShouldContainClass("UserAccessor",
            "should generate accessor class even for single entity table");
        tableCode.Should().Contain("internal UserAccessor(UsersTableTable table)",
            "should have constructor accepting parent table");
        tableCode.Should().Contain("private readonly UsersTableTable _table;",
            "should have parent table field");
    }

    [Fact]
    public void EntityAccessorClass_WithGenerateFalse_NotGenerated()
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
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    [GenerateEntityProperty(Generate = false)]
    public partial class InternalEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial class Customer
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
            .Where(s => s.FileName.Contains("SharedTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should generate accessors for Order and Customer
        tableCode.ShouldContainClass("OrderAccessor",
            "should generate accessor for Order entity");
        tableCode.ShouldContainClass("CustomerAccessor",
            "should generate accessor for Customer entity");
        
        // Should NOT generate accessor for InternalEntity
        tableCode.Should().NotContain("InternalEntityAccessor",
            "should not generate accessor class when Generate = false");
        tableCode.Should().NotContain("class InternalEntityAccessor",
            "should not generate accessor class definition when Generate = false");
    }

    [Fact]
    public void EntityAccessorClass_IsNestedInTableClass()
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
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Verify the accessor class is nested within the table class
        // by checking the structure contains both class declarations
        tableCode.Should().Contain("public partial class AppTableTable",
            "should have table class declaration");
        tableCode.Should().Contain("public class OrderAccessor",
            "should have nested accessor class declaration");
        
        // Verify nesting by checking that accessor class comes after table class
        var tableClassIndex = tableCode.IndexOf("public partial class AppTableTable");
        var accessorClassIndex = tableCode.IndexOf("public class OrderAccessor");
        
        accessorClassIndex.Should().BeGreaterThan(tableClassIndex,
            "accessor class should be nested inside table class");
    }

    [Fact]
    public void EntityAccessorClass_MultipleEntities_AllHaveCorrectStructure()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""multi-table"", IsDefault = true)]
    public partial class EntityA
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""multi-table"")]
    public partial class EntityB
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""multi-table"")]
    public partial class EntityC
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
            .Where(s => s.FileName.Contains("MultiTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // All three entities should have accessor classes
        tableCode.ShouldContainClass("EntityAAccessor");
        tableCode.ShouldContainClass("EntityBAccessor");
        tableCode.ShouldContainClass("EntityCAccessor");
        
        // All should have parent table fields
        var fieldMatches = System.Text.RegularExpressions.Regex.Matches(
            tableCode, 
            @"private readonly MultiTableTable _table;");
        fieldMatches.Count.Should().BeGreaterThanOrEqualTo(3,
            "each accessor should have parent table field");
        
        // All should have constructors
        tableCode.Should().Contain("internal EntityAAccessor(MultiTableTable table)");
        tableCode.Should().Contain("internal EntityBAccessor(MultiTableTable table)");
        tableCode.Should().Contain("internal EntityCAccessor(MultiTableTable table)");
    }

    [Fact]
    public void EntityAccessorClass_HasPublicVisibility()
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
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Accessor class should be public (nested classes are accessible based on parent)
        tableCode.Should().Contain("public class OrderAccessor",
            "accessor class should have public visibility");
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
