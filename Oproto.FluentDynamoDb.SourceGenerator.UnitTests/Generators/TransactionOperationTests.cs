using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for transaction and batch operation generation.
/// Verifies that transaction and batch operations use static entry points (DynamoDbTransactions, DynamoDbBatch)
/// and are NOT generated at the table level or on entity accessor classes.
/// Covers requirement 7 from the transaction-batch-api-redesign spec.
/// </summary>
[Trait("Category", "Unit")]
public class TransactionOperationTests
{
    [Fact]
    public void TransactWrite_NotGeneratedAtTableLevel()
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
        
        // TransactWrite should NOT be generated at table level (use static DynamoDbTransactions.Write instead)
        tableCode.Should().NotContain("TransactWrite",
            "should not generate TransactWrite method at table level - use DynamoDbTransactions.Write instead");
    }

    [Fact]
    public void TransactGet_NotGeneratedAtTableLevel()
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
        
        // TransactGet should NOT be generated at table level (use static DynamoDbTransactions.Get instead)
        tableCode.Should().NotContain("TransactGet",
            "should not generate TransactGet method at table level - use DynamoDbTransactions.Get instead");
    }

    [Fact]
    public void BatchWrite_NotGeneratedAtTableLevel()
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
        
        // BatchWrite should NOT be generated at table level (use static DynamoDbBatch.Write instead)
        tableCode.Should().NotContain("BatchWrite",
            "should not generate BatchWrite method at table level - use DynamoDbBatch.Write instead");
    }

    [Fact]
    public void BatchGet_NotGeneratedAtTableLevel()
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
        
        // BatchGet should NOT be generated at table level (use static DynamoDbBatch.Get instead)
        tableCode.Should().NotContain("BatchGet",
            "should not generate BatchGet method at table level - use DynamoDbBatch.Get instead");
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
    public void AllTransactionMethods_NotGeneratedAtTableLevel_SingleEntity()
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
        
        // Transaction methods should NOT be generated at table level (use static entry points instead)
        tableCode.Should().NotContain("TransactWrite",
            "should not generate TransactWrite - use DynamoDbTransactions.Write");
        tableCode.Should().NotContain("TransactGet",
            "should not generate TransactGet - use DynamoDbTransactions.Get");
        tableCode.Should().NotContain("BatchWrite",
            "should not generate BatchWrite - use DynamoDbBatch.Write");
        tableCode.Should().NotContain("BatchGet",
            "should not generate BatchGet - use DynamoDbBatch.Get");
    }

    [Fact]
    public void AllTransactionMethods_NotGeneratedAtTableLevel_MultipleEntities()
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
        
        // Transaction methods should NOT be generated at table level (use static entry points instead)
        tableCode.Should().NotContain("TransactWrite",
            "should not generate TransactWrite - use DynamoDbTransactions.Write");
        tableCode.Should().NotContain("TransactGet",
            "should not generate TransactGet - use DynamoDbTransactions.Get");
        tableCode.Should().NotContain("BatchWrite",
            "should not generate BatchWrite - use DynamoDbBatch.Write");
        tableCode.Should().NotContain("BatchGet",
            "should not generate BatchGet - use DynamoDbBatch.Get");
    }

    [Fact]
    public void TransactionMethods_UseStaticEntryPoints()
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
        
        // Transaction methods should NOT be on table - use static entry points instead
        tableCode.Should().NotContain("TransactWrite",
            "use DynamoDbTransactions.Write static entry point");
        tableCode.Should().NotContain("TransactGet",
            "use DynamoDbTransactions.Get static entry point");
        tableCode.Should().NotContain("BatchWrite",
            "use DynamoDbBatch.Write static entry point");
        tableCode.Should().NotContain("BatchGet",
            "use DynamoDbBatch.Get static entry point");
    }

    [Fact]
    public void TransactionMethods_NotInTableOrAccessors()
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
        
        // Transaction methods should NOT be in table class or accessor classes
        tableCode.Should().NotContain("TransactWrite",
            "TransactWrite should not be generated - use DynamoDbTransactions.Write");
        tableCode.Should().NotContain("TransactGet",
            "TransactGet should not be generated - use DynamoDbTransactions.Get");
        tableCode.Should().NotContain("BatchWrite",
            "BatchWrite should not be generated - use DynamoDbBatch.Write");
        tableCode.Should().NotContain("BatchGet",
            "BatchGet should not be generated - use DynamoDbBatch.Get");
    }

    [Fact]
    public void TransactionMethods_NotGeneratedEvenWithoutDefaultEntity()
    {
        // Arrange - Multiple entities without default (will emit diagnostic)
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
        
        // Transaction methods should NOT be generated (use static entry points)
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        if (tableFiles.Any())
        {
            var tableCode = tableFiles[0].SourceText.ToString();
            
            // Transaction methods should NOT be generated
            tableCode.Should().NotContain("TransactWrite",
                "should not generate TransactWrite - use DynamoDbTransactions.Write");
            tableCode.Should().NotContain("TransactGet",
                "should not generate TransactGet - use DynamoDbTransactions.Get");
            tableCode.Should().NotContain("BatchWrite",
                "should not generate BatchWrite - use DynamoDbBatch.Write");
            tableCode.Should().NotContain("BatchGet",
                "should not generate BatchGet - use DynamoDbBatch.Get");
        }
    }

    [Fact]
    public void TransactionMethods_NotGeneratedRegardlessOfAccessorConfiguration()
    {
        // Arrange - Configure accessor operations but transaction methods should not be generated
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
        
        // Transaction methods should NOT be generated (use static entry points)
        tableCode.Should().NotContain("TransactWrite",
            "should not generate TransactWrite - use DynamoDbTransactions.Write");
        tableCode.Should().NotContain("TransactGet",
            "should not generate TransactGet - use DynamoDbTransactions.Get");
        tableCode.Should().NotContain("BatchWrite",
            "should not generate BatchWrite - use DynamoDbBatch.Write");
        tableCode.Should().NotContain("BatchGet",
            "should not generate BatchGet - use DynamoDbBatch.Get");
        
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
