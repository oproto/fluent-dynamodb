using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for transaction and batch operation generation.
/// Verifies that TransactWrite, TransactGet, BatchWrite, and BatchGet operations are generated
/// at the table level only and NOT on entity accessor classes.
/// Covers requirement 7 from the table-generation-redesign spec.
/// </summary>
[Trait("Category", "Unit")]
public class TransactionOperationTests
{
    [Fact]
    public void TransactWrite_GeneratedAtTableLevel()
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
        
        // TransactWrite should be generated at table level
        tableCode.Should().Contain("TransactWriteItemsRequestBuilder TransactWrite()",
            "should generate TransactWrite method at table level");
    }

    [Fact]
    public void TransactGet_GeneratedAtTableLevel()
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
        
        // TransactGet should be generated at table level
        tableCode.Should().Contain("TransactGetItemsRequestBuilder TransactGet()",
            "should generate TransactGet method at table level");
    }

    [Fact]
    public void BatchWrite_GeneratedAtTableLevel()
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
        
        // BatchWrite should be generated at table level
        tableCode.Should().Contain("BatchWriteItemRequestBuilder BatchWrite()",
            "should generate BatchWrite method at table level");
    }

    [Fact]
    public void BatchGet_GeneratedAtTableLevel()
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
        
        // BatchGet should be generated at table level
        tableCode.Should().Contain("BatchGetItemRequestBuilder BatchGet()",
            "should generate BatchGet method at table level");
    }

    [Fact]
    public void TransactionMethods_NotGeneratedOnEntityAccessors()
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
        
        // Extract OrderAccessor class
        var orderAccessorStart = tableCode.IndexOf("public class OrderAccessor");
        var orderLineAccessorStart = tableCode.IndexOf("public class OrderLineAccessor");
        
        orderAccessorStart.Should().BeGreaterThan(0, "should contain OrderAccessor class");
        orderLineAccessorStart.Should().BeGreaterThan(0, "should contain OrderLineAccessor class");
        
        var orderAccessorCode = tableCode.Substring(orderAccessorStart, orderLineAccessorStart - orderAccessorStart);
        
        // OrderAccessor should NOT have transaction methods
        orderAccessorCode.Should().NotContain("TransactWrite",
            "OrderAccessor should not have TransactWrite method");
        orderAccessorCode.Should().NotContain("TransactGet",
            "OrderAccessor should not have TransactGet method");
        orderAccessorCode.Should().NotContain("BatchWrite",
            "OrderAccessor should not have BatchWrite method");
        orderAccessorCode.Should().NotContain("BatchGet",
            "OrderAccessor should not have BatchGet method");
        
        // Extract OrderLineAccessor class (from its start to end of file or next class)
        var orderLineAccessorEnd = tableCode.Length;
        var orderLineAccessorCode = tableCode.Substring(orderLineAccessorStart, orderLineAccessorEnd - orderLineAccessorStart);
        
        // OrderLineAccessor should NOT have transaction methods
        orderLineAccessorCode.Should().NotContain("TransactWrite",
            "OrderLineAccessor should not have TransactWrite method");
        orderLineAccessorCode.Should().NotContain("TransactGet",
            "OrderLineAccessor should not have TransactGet method");
        orderLineAccessorCode.Should().NotContain("BatchWrite",
            "OrderLineAccessor should not have BatchWrite method");
        orderLineAccessorCode.Should().NotContain("BatchGet",
            "OrderLineAccessor should not have BatchGet method");
    }

    [Fact]
    public void AllTransactionMethods_GeneratedAtTableLevel_SingleEntity()
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
        
        // All transaction methods should be generated at table level
        tableCode.Should().Contain("TransactWriteItemsRequestBuilder TransactWrite()",
            "should generate TransactWrite method at table level");
        tableCode.Should().Contain("TransactGetItemsRequestBuilder TransactGet()",
            "should generate TransactGet method at table level");
        tableCode.Should().Contain("BatchWriteItemRequestBuilder BatchWrite()",
            "should generate BatchWrite method at table level");
        tableCode.Should().Contain("BatchGetItemRequestBuilder BatchGet()",
            "should generate BatchGet method at table level");
    }

    [Fact]
    public void AllTransactionMethods_GeneratedAtTableLevel_MultipleEntities()
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
        
        // All transaction methods should be generated at table level
        tableCode.Should().Contain("TransactWriteItemsRequestBuilder TransactWrite()",
            "should generate TransactWrite method at table level with multiple entities");
        tableCode.Should().Contain("TransactGetItemsRequestBuilder TransactGet()",
            "should generate TransactGet method at table level with multiple entities");
        tableCode.Should().Contain("BatchWriteItemRequestBuilder BatchWrite()",
            "should generate BatchWrite method at table level with multiple entities");
        tableCode.Should().Contain("BatchGetItemRequestBuilder BatchGet()",
            "should generate BatchGet method at table level with multiple entities");
    }

    [Fact]
    public void TransactionMethods_ArePublic()
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
        
        // Transaction methods should be public
        tableCode.Should().Contain("public TransactWriteItemsRequestBuilder TransactWrite()",
            "TransactWrite should be public");
        tableCode.Should().Contain("public TransactGetItemsRequestBuilder TransactGet()",
            "TransactGet should be public");
        tableCode.Should().Contain("public BatchWriteItemRequestBuilder BatchWrite()",
            "BatchWrite should be public");
        tableCode.Should().Contain("public BatchGetItemRequestBuilder BatchGet()",
            "BatchGet should be public");
    }

    [Fact]
    public void TransactionMethods_InTableClassBody_NotInAccessors()
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
        
        // Extract table class body (before first accessor class)
        var tableClassStart = tableCode.IndexOf("public partial class AppTableTable");
        var firstAccessorStart = tableCode.IndexOf("public class OrderAccessor");
        
        tableClassStart.Should().BeGreaterThan(0, "should contain table class");
        firstAccessorStart.Should().BeGreaterThan(0, "should contain accessor class");
        
        var tableClassBody = tableCode.Substring(tableClassStart, firstAccessorStart - tableClassStart);
        
        // Transaction methods should be in table class body
        tableClassBody.Should().Contain("TransactWriteItemsRequestBuilder TransactWrite()",
            "TransactWrite should be in table class body");
        tableClassBody.Should().Contain("TransactGetItemsRequestBuilder TransactGet()",
            "TransactGet should be in table class body");
        tableClassBody.Should().Contain("BatchWriteItemRequestBuilder BatchWrite()",
            "BatchWrite should be in table class body");
        tableClassBody.Should().Contain("BatchGetItemRequestBuilder BatchGet()",
            "BatchGet should be in table class body");
    }

    [Fact]
    public void TransactionMethods_GeneratedEvenWithoutDefaultEntity()
    {
        // Arrange - Multiple entities without default (will emit diagnostic but should still generate transaction methods)
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
        
        // Even with error, transaction methods should be generated
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        if (tableFiles.Any())
        {
            var tableCode = tableFiles[0].SourceText.ToString();
            
            // Transaction methods should still be generated
            tableCode.Should().Contain("TransactWriteItemsRequestBuilder TransactWrite()",
                "should generate TransactWrite even without default entity");
            tableCode.Should().Contain("TransactGetItemsRequestBuilder TransactGet()",
                "should generate TransactGet even without default entity");
            tableCode.Should().Contain("BatchWriteItemRequestBuilder BatchWrite()",
                "should generate BatchWrite even without default entity");
            tableCode.Should().Contain("BatchGetItemRequestBuilder BatchGet()",
                "should generate BatchGet even without default entity");
        }
    }

    [Fact]
    public void TransactionMethods_NotAffectedByAccessorConfiguration()
    {
        // Arrange - Configure accessor operations but transaction methods should remain unaffected
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    [GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
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
        
        // Transaction methods should still be public (not affected by accessor configuration)
        tableCode.Should().Contain("public TransactWriteItemsRequestBuilder TransactWrite()",
            "TransactWrite should remain public despite accessor configuration");
        tableCode.Should().Contain("public TransactGetItemsRequestBuilder TransactGet()",
            "TransactGet should remain public despite accessor configuration");
        tableCode.Should().Contain("public BatchWriteItemRequestBuilder BatchWrite()",
            "BatchWrite should remain public despite accessor configuration");
        tableCode.Should().Contain("public BatchGetItemRequestBuilder BatchGet()",
            "BatchGet should remain public despite accessor configuration");
        
        // Verify accessor operations are internal as configured
        var accessorStart = tableCode.IndexOf("public class OrderAccessor");
        var accessorCode = tableCode.Substring(accessorStart);
        
        accessorCode.Should().Contain("internal GetItemRequestBuilder<Order> Get(string pk)",
            "accessor operations should be internal as configured");
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
