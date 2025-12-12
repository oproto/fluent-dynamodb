using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for ValidateSchemaAsync method generation in table classes.
/// Verifies that the source generator correctly generates schema validation methods
/// for both single-entity and multi-entity tables.
/// </summary>
[Trait("Category", "Unit")]
public class SchemaValidationGeneratorTests
{
    [Fact]
    public void TableClass_GeneratesValidateSchemaAsyncMethod()
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
        
        // Should contain ValidateSchemaAsync method
        tableCode.Should().Contain("public static async System.Threading.Tasks.Task<Oproto.FluentDynamoDb.Validation.SchemaValidationResult> ValidateSchemaAsync(",
            "should generate ValidateSchemaAsync method with correct return type");
        tableCode.Should().Contain("IAmazonDynamoDB client,",
            "should have IAmazonDynamoDB client parameter");
        tableCode.Should().Contain("Oproto.FluentDynamoDb.Validation.SchemaValidationOptions? options = null)",
            "should have optional SchemaValidationOptions parameter");
    }

    [Fact]
    public void ValidateSchemaAsync_UsesCorrectTableName()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""my-custom-table-name"")]
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
            .Where(s => s.FileName.Contains("Table.g.cs") && 
                       !s.FileName.Contains("Fields") && 
                       !s.FileName.Contains("Keys"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should use the correct table name in the validation call
        tableCode.Should().Contain("\"my-custom-table-name\",",
            "should pass the correct table name to the validator");
    }

    [Fact]
    public void ValidateSchemaAsync_UsesCorrectEntityMetadata()
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
        
        // Should call GetEntityMetadata on the correct entity
        tableCode.Should().Contain("Product.GetEntityMetadata(),",
            "should call GetEntityMetadata on the entity class");
    }

    [Fact]
    public void ValidateSchemaAsync_CreatesSchemaValidator()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""items"")]
    public partial class Item
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
            .Where(s => s.FileName.Contains("ItemsTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should create a SchemaValidator instance
        tableCode.Should().Contain("var validator = new Oproto.FluentDynamoDb.Validation.SchemaValidator();",
            "should create a SchemaValidator instance");
        tableCode.Should().Contain("return await validator.ValidateAsync(",
            "should call ValidateAsync on the validator");
    }

    [Fact]
    public void ValidateSchemaAsync_HasXmlDocumentation()
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
        tableCode.Should().Contain("/// Validates that the DynamoDB table schema matches the entity metadata.",
            "should have descriptive summary");
        tableCode.Should().Contain("/// <param name=\"client\">",
            "should have parameter documentation for client");
        tableCode.Should().Contain("/// <param name=\"options\">",
            "should have parameter documentation for options");
        tableCode.Should().Contain("/// <returns>",
            "should have returns documentation");
        tableCode.Should().Contain("/// <example>",
            "should have example documentation");
    }

    [Fact]
    public void MultiEntityTable_GeneratesValidateSchemaAsyncWithDefaultEntity()
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
        
        // Should generate ValidateSchemaAsync using the default entity (Order)
        tableCode.Should().Contain("public static async System.Threading.Tasks.Task<Oproto.FluentDynamoDb.Validation.SchemaValidationResult> ValidateSchemaAsync(",
            "should generate ValidateSchemaAsync method");
        tableCode.Should().Contain("Order.GetEntityMetadata(),",
            "should use the default entity (Order) for metadata");
        tableCode.Should().Contain("\"shared-table\",",
            "should use the correct table name");
    }

    [Fact]
    public void ValidateSchemaAsync_IncludesValidationNamespaceUsing()
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
        
        // Should include the Validation namespace using
        tableCode.Should().Contain("using Oproto.FluentDynamoDb.Validation;",
            "should include using directive for Validation namespace");
    }

    [Fact]
    public void ValidateSchemaAsync_HandlesDefaultOptions()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""options-test"")]
    public partial class OptionsTest
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
            .Where(s => s.FileName.Contains("OptionsTestTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should handle null options by creating default
        tableCode.Should().Contain("options ?? new Oproto.FluentDynamoDb.Validation.SchemaValidationOptions()",
            "should create default options when null is passed");
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
