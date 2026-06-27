using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for CreateTableAsync method generation in table classes.
/// Verifies that the source generator correctly generates table creation methods
/// for both single-entity and multi-entity tables.
/// 
/// _Requirements: 6.1, 6.2_
/// </summary>
[Trait("Category", "Unit")]
public class TableCreationGeneratorTests
{
    [Fact]
    public void TableClass_GeneratesCreateTableAsyncMethod()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users"")]
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
            .Where(s => s.FileName.Contains("UsersTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1, "should generate exactly one table class");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should contain CreateTableAsync method with correct return type
        tableCode.Should().Contain("public static async System.Threading.Tasks.Task<Oproto.FluentDynamoDb.Provisioning.TableCreationResult> CreateTableAsync(",
            "should generate CreateTableAsync method with correct return type");
    }

    [Fact]
    public void CreateTableAsync_HasRequiredTableNameParameter()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"")]
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
            .Where(s => s.FileName.Contains("OrdersTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should have required tableName parameter (not optional)
        tableCode.Should().Contain("IAmazonDynamoDB client,",
            "should have IAmazonDynamoDB client parameter");
        tableCode.Should().Contain("string tableName,",
            "should have required tableName parameter");
        tableCode.Should().Contain("Oproto.FluentDynamoDb.Provisioning.TableCreationOptions? options = null,",
            "should have optional TableCreationOptions parameter");
        tableCode.Should().Contain("System.Threading.CancellationToken cancellationToken = default)",
            "should have optional CancellationToken parameter");
    }

    [Fact]
    public void CreateTableAsync_CallsTableCreatorCorrectly()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""products"")]
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
            .Where(s => s.FileName.Contains("ProductsTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should create a TableCreator instance
        tableCode.Should().Contain("var creator = new Oproto.FluentDynamoDb.Provisioning.TableCreator();",
            "should create a TableCreator instance");
        
        // Should call CreateAsync with correct parameters
        tableCode.Should().Contain("return await creator.CreateAsync(",
            "should call CreateAsync on the creator");
        tableCode.Should().Contain("client,",
            "should pass client parameter");
        tableCode.Should().Contain("tableName,",
            "should pass tableName parameter");
        tableCode.Should().Contain("Product.GetEntityMetadata(),",
            "should call GetEntityMetadata on the entity class");
        tableCode.Should().Contain("options ?? new Oproto.FluentDynamoDb.Provisioning.TableCreationOptions(),",
            "should handle null options by creating default");
        tableCode.Should().Contain("cancellationToken);",
            "should pass cancellationToken parameter");
    }

    [Fact]
    public void CreateTableAsync_HasXmlDocumentation()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""docs-test"")]
    public partial class DocTest
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
            .Where(s => s.FileName.Contains("DocsTestTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should have XML documentation
        tableCode.Should().Contain("/// <summary>",
            "should have XML summary documentation");
        tableCode.Should().Contain("/// Creates a DynamoDB table based on the entity metadata.",
            "should have descriptive summary");
        tableCode.Should().Contain("/// <param name=\"client\">",
            "should have parameter documentation for client");
        tableCode.Should().Contain("/// <param name=\"tableName\">",
            "should have parameter documentation for tableName");
        tableCode.Should().Contain("/// <param name=\"options\">",
            "should have parameter documentation for options");
        tableCode.Should().Contain("/// <returns>",
            "should have returns documentation");
        tableCode.Should().Contain("/// <example>",
            "should have example documentation");
    }

    [Fact]
    public void MultiEntityTable_GeneratesCreateTableAsyncWithDefaultEntity()
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
        
        // Should generate CreateTableAsync using the default entity (Order)
        tableCode.Should().Contain("public static async System.Threading.Tasks.Task<Oproto.FluentDynamoDb.Provisioning.TableCreationResult> CreateTableAsync(",
            "should generate CreateTableAsync method");
        tableCode.Should().Contain("Order.GetEntityMetadata(),",
            "should use the default entity (Order) for metadata");
    }

    [Fact]
    public void CreateTableAsync_GeneratedAfterValidateSchemaAsync()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
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
            .Where(s => s.FileName.Contains("TestTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Both methods should be present
        tableCode.Should().Contain("ValidateSchemaAsync(",
            "should contain ValidateSchemaAsync method");
        tableCode.Should().Contain("CreateTableAsync(",
            "should contain CreateTableAsync method");
        
        // CreateTableAsync should appear after ValidateSchemaAsync
        var validateIndex = tableCode.IndexOf("ValidateSchemaAsync(");
        var createIndex = tableCode.IndexOf("CreateTableAsync(");
        createIndex.Should().BeGreaterThan(validateIndex,
            "CreateTableAsync should be generated after ValidateSchemaAsync");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
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
