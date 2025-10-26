using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for table-level operation generation.
/// Verifies that table-level operations (Get, Query, Scan, Put, Delete, Update) are generated
/// correctly when a default entity exists, use the default entity type, and delegate to the
/// entity accessor. Also verifies that no table-level operations are generated when there is
/// no default entity.
/// Covers requirement 6 from the table-generation-redesign spec.
/// </summary>
[Trait("Category", "Unit")]
public class TableLevelOperationTests
{
    [Fact]
    public void TableLevelOperations_WithDefaultEntity_AreGenerated()
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
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table-level operations should be generated
        tableCode.Should().Contain("Get(string pk)",
            "should generate table-level Get operation when default entity exists");
        tableCode.Should().Contain("Query()",
            "should generate table-level Query operation when default entity exists");
        tableCode.Should().Contain("Put()",
            "should generate table-level Put operation when default entity exists");
        tableCode.Should().Contain("Delete(string pk)",
            "should generate table-level Delete operation when default entity exists");
        tableCode.Should().Contain("Update(string pk)",
            "should generate table-level Update operation when default entity exists");
    }

    [Fact]
    public void TableLevelOperations_UseDefaultEntityType()
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
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table-level operations should use Order (default entity) type
        tableCode.Should().Contain("GetItemRequestBuilder<Order> Get(string pk)",
            "table-level Get should use default entity type Order");
        tableCode.Should().Contain("QueryRequestBuilder<Order> Query()",
            "table-level Query should use default entity type Order");
        tableCode.Should().Contain("PutItemRequestBuilder<Order> Put()",
            "table-level Put should use default entity type Order");
        tableCode.Should().Contain("DeleteItemRequestBuilder<Order> Delete(string pk)",
            "table-level Delete should use default entity type Order");
        tableCode.Should().Contain("UpdateItemRequestBuilder<Order> Update(string pk)",
            "table-level Update should use default entity type Order");
        
        // Verify table-level operations section uses Order, not OrderLine
        // Extract the table-level operations section (between constructors and accessors)
        var tableLevelStart = tableCode.IndexOf("// Table-level operations");
        var firstAccessorStart = tableCode.IndexOf("public class OrderAccessor");
        
        if (tableLevelStart >= 0 && firstAccessorStart > tableLevelStart)
        {
            var tableLevelSection = tableCode.Substring(tableLevelStart, firstAccessorStart - tableLevelStart);
            
            // Table-level section should use Order
            tableLevelSection.Should().Contain("GetItemRequestBuilder<Order>",
                "table-level operations section should use Order type");
            
            // Table-level section should NOT use OrderLine
            tableLevelSection.Should().NotContain("GetItemRequestBuilder<OrderLine>",
                "table-level operations section should not use OrderLine type");
        }
    }

    [Fact]
    public void TableLevelOperations_DelegateToEntityAccessor()
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
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table-level operations should delegate to the default entity's accessor
        tableCode.Should().Contain("Orders.Get(pk)",
            "table-level Get should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.Query()",
            "table-level Query should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.Put()",
            "table-level Put should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.Delete(pk)",
            "table-level Delete should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.Update(pk)",
            "table-level Update should delegate to Orders accessor");
    }

    [Fact]
    public void TableLevelOperations_WithoutDefaultEntity_NotGenerated()
    {
        // Arrange - Multiple entities, no default specified (will emit diagnostic)
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - Should emit diagnostic for missing default
        result.Diagnostics.Should().Contain(d => 
            d.Severity == DiagnosticSeverity.Error && 
            d.Id == "FDDB001",
            "should emit FDDB001 error when multiple entities exist without default");
        
        // Even with error, check that table-level operations are not generated
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        if (tableFiles.Any())
        {
            var tableCode = tableFiles[0].SourceText.ToString();
            
            // Extract just the table class body (not accessor classes)
            var tableClassStart = tableCode.IndexOf("public partial class AppTableTable");
            var firstAccessorStart = tableCode.IndexOf("public class OrderAccessor");
            
            if (tableClassStart >= 0 && firstAccessorStart > tableClassStart)
            {
                var tableClassBody = tableCode.Substring(tableClassStart, firstAccessorStart - tableClassStart);
                
                // Table-level operations should NOT be in the table class body
                // (they would be between the class declaration and the first accessor)
                tableClassBody.Should().NotContain("GetItemRequestBuilder<Order> Get(string pk)",
                    "should not generate table-level Get when no default entity");
                tableClassBody.Should().NotContain("QueryRequestBuilder<Order> Query()",
                    "should not generate table-level Query when no default entity");
            }
        }
    }

    [Fact]
    public void TableLevelOperations_SingleEntity_AreGenerated()
    {
        // Arrange - Single entity doesn't require explicit IsDefault
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
        public string Id { get; set; } = string.Empty;
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
        
        // Table-level operations should be generated for single entity
        tableCode.Should().Contain("GetItemRequestBuilder<Order> Get(string pk)",
            "should generate table-level Get for single entity table");
        tableCode.Should().Contain("QueryRequestBuilder<Order> Query()",
            "should generate table-level Query for single entity table");
        tableCode.Should().Contain("PutItemRequestBuilder<Order> Put()",
            "should generate table-level Put for single entity table");
        tableCode.Should().Contain("DeleteItemRequestBuilder<Order> Delete(string pk)",
            "should generate table-level Delete for single entity table");
        tableCode.Should().Contain("UpdateItemRequestBuilder<Order> Update(string pk)",
            "should generate table-level Update for single entity table");
    }

    [Fact]
    public void TableLevelOperations_SingleEntity_DelegateToAccessor()
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
        public string Id { get; set; } = string.Empty;
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
        
        // Table-level operations should delegate to accessor even for single entity
        tableCode.Should().Contain("Orders.Get(pk)",
            "table-level Get should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.Query()",
            "table-level Query should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.Put()",
            "table-level Put should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.Delete(pk)",
            "table-level Delete should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.Update(pk)",
            "table-level Update should delegate to Orders accessor");
    }

    [Fact]
    public void TableLevelOperations_WithCustomEntityPropertyName_DelegateCorrectly()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    [GenerateEntityProperty(Name = ""AllOrders"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
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
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table-level operations should delegate to custom accessor name
        tableCode.Should().Contain("AllOrders.Get(pk)",
            "table-level Get should delegate to custom accessor name AllOrders");
        tableCode.Should().Contain("AllOrders.Query()",
            "table-level Query should delegate to custom accessor name AllOrders");
        tableCode.Should().Contain("AllOrders.Put()",
            "table-level Put should delegate to custom accessor name AllOrders");
        tableCode.Should().Contain("AllOrders.Delete(pk)",
            "table-level Delete should delegate to custom accessor name AllOrders");
        tableCode.Should().Contain("AllOrders.Update(pk)",
            "table-level Update should delegate to custom accessor name AllOrders");
        
        // Verify table-level operations section doesn't use default name
        // Extract just the CRUD operations section (before transaction operations)
        var tableLevelStart = tableCode.IndexOf("// Table-level operations");
        var transactionStart = tableCode.IndexOf("// Transaction and batch operations");
        
        if (tableLevelStart >= 0 && transactionStart > tableLevelStart)
        {
            var tableLevelCrudSection = tableCode.Substring(tableLevelStart, transactionStart - tableLevelStart);
            
            // Table-level CRUD section should NOT delegate to default name (use word boundary to avoid matching "AllOrders")
            tableLevelCrudSection.Should().NotContain(" Orders.Get(pk)",
                "table-level operations should not delegate to default name when custom name is specified");
            tableLevelCrudSection.Should().NotContain("\nOrders.Get(pk)",
                "table-level operations should not delegate to default name when custom name is specified");
        }
    }

    [Fact]
    public void TableLevelOperations_MultipleEntitiesWithDefault_OnlyDefaultUsed()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"", IsDefault = true)]
    public partial class OrderLine
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
        
        // Table-level operations should use OrderLine (the default entity)
        tableCode.Should().Contain("GetItemRequestBuilder<OrderLine> Get(string pk)",
            "table-level Get should use OrderLine as default entity");
        tableCode.Should().Contain("QueryRequestBuilder<OrderLine> Query()",
            "table-level Query should use OrderLine as default entity");
        
        // Should delegate to OrderLines accessor
        tableCode.Should().Contain("OrderLines.Get(pk)",
            "table-level Get should delegate to OrderLines accessor");
        tableCode.Should().Contain("OrderLines.Query()",
            "table-level Query should delegate to OrderLines accessor");
        
        // Verify table-level operations section uses OrderLine, not Order or Customer
        var tableLevelStart = tableCode.IndexOf("// Table-level operations");
        var firstAccessorStart = tableCode.IndexOf("public class OrderAccessor");
        
        if (tableLevelStart >= 0 && firstAccessorStart > tableLevelStart)
        {
            var tableLevelSection = tableCode.Substring(tableLevelStart, firstAccessorStart - tableLevelStart);
            
            // Table-level section should use OrderLine
            tableLevelSection.Should().Contain("GetItemRequestBuilder<OrderLine>",
                "table-level operations section should use OrderLine type");
            
            // Table-level section should NOT use Order or Customer
            tableLevelSection.Should().NotContain("GetItemRequestBuilder<Order>",
                "table-level operations section should not use non-default entity Order");
            tableLevelSection.Should().NotContain("GetItemRequestBuilder<Customer>",
                "table-level operations section should not use non-default entity Customer");
        }
    }

    [Fact]
    public void TableLevelOperations_ArePublic()
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
        
        // Table-level operations should be public
        tableCode.Should().Contain("public GetItemRequestBuilder<Order> Get(string pk)",
            "table-level Get should be public");
        tableCode.Should().Contain("public QueryRequestBuilder<Order> Query()",
            "table-level Query should be public");
        tableCode.Should().Contain("public PutItemRequestBuilder<Order> Put()",
            "table-level Put should be public");
        tableCode.Should().Contain("public DeleteItemRequestBuilder<Order> Delete(string pk)",
            "table-level Delete should be public");
        tableCode.Should().Contain("public UpdateItemRequestBuilder<Order> Update(string pk)",
            "table-level Update should be public");
    }

    [Fact]
    public void TableLevelOperations_WithInternalAccessorModifier_StillPublic()
    {
        // Arrange - Even if accessor is internal, table-level operations should be public
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    [GenerateEntityProperty(Modifier = AccessModifier.Internal)]
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
        
        // Table-level operations should still be public (they delegate to internal accessor)
        tableCode.Should().Contain("public GetItemRequestBuilder<Order> Get(string pk)",
            "table-level Get should be public even when accessor is internal");
        tableCode.Should().Contain("public QueryRequestBuilder<Order> Query()",
            "table-level Query should be public even when accessor is internal");
        
        // Accessor property should be internal
        tableCode.Should().Contain("internal OrderAccessor Orders",
            "accessor property should be internal as configured");
    }

    [Fact]
    public void TableLevelOperations_PassParametersToAccessor()
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
        
        // Verify that table-level operations pass parameters to accessor methods
        // Get and Delete take pk parameter
        tableCode.Should().Contain("public GetItemRequestBuilder<Order> Get(string pk)",
            "table-level Get should accept pk parameter");
        tableCode.Should().Contain("Orders.Get(pk)",
            "table-level Get should pass pk to accessor");
        
        tableCode.Should().Contain("public DeleteItemRequestBuilder<Order> Delete(string pk)",
            "table-level Delete should accept pk parameter");
        tableCode.Should().Contain("Orders.Delete(pk)",
            "table-level Delete should pass pk to accessor");
        
        tableCode.Should().Contain("public UpdateItemRequestBuilder<Order> Update(string pk)",
            "table-level Update should accept pk parameter");
        tableCode.Should().Contain("Orders.Update(pk)",
            "table-level Update should pass pk to accessor");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source)
            },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Attributes.DynamoDbTableAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Amazon.DynamoDBv2.Model.AttributeValue).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Storage.IDynamoDbEntity).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll")),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Collections.dll")),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Linq.Expressions.dll"))
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
}
