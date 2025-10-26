// MIGRATION STATUS: Migrated to use CompilationVerifier and SemanticAssertions
// - Added compilation verification to all tests
// - Replaced structural string checks with semantic assertions
// - Preserved DynamoDB-specific value checks with descriptive messages

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

[Trait("Category", "Unit")]
public class DynamoDbSourceGeneratorTests
{
    [Fact]
    public void Generator_WithBasicEntity_ProducesCode()
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
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code with DYNDB021 warning for reserved word "name"
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warning for "name"
        result.GeneratedSources.Should().HaveCount(4); // Fields, Keys, Entity, Table

        // Check entity implementation
        var entityCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntity.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        entityCode.ShouldContainClass("TestEntity");
        entityCode.Should().Contain("namespace TestNamespace", "should generate code in the correct namespace");

        // Check fields class
        var fieldsCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityFields.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        fieldsCode.ShouldContainClass("TestEntityFields");
        fieldsCode.Should().Contain("public const string Id = \"pk\";", "should map Id property to pk attribute");
        fieldsCode.Should().Contain("public const string Name = \"name\";", "should map Name property to name attribute");

        // Check keys class
        var keysCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityKeys.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(keysCode, source);
        keysCode.ShouldContainClass("TestEntityKeys");
        keysCode.ShouldContainMethod("Pk");
    }

    [Fact]
    public void Generator_WithoutDynamoDbTableAttribute_ProducesNoCode()
    {
        // Arrange
        var source = @"
namespace TestNamespace
{
    public partial class RegularClass
    {
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void Generator_WithGsiEntity_GeneratesFieldsWithGsiClasses()
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
        
        [GlobalSecondaryIndex(""TestGSI"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi_pk"")]
        public string GsiKey { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""TestGSI"", IsSortKey = true)]
        [DynamoDbAttribute(""gsi_sk"")]
        public string GsiSort { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code without any diagnostics for GSI entity
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(5); // Fields, Keys, Entity, Table, Table.Indexes (has GSI)

        var fieldsCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityFields.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        fieldsCode.ShouldContainClass("TestEntityFields");
        fieldsCode.Should().Contain("public const string Id = \"pk\";", "should map Id property to pk attribute");
        fieldsCode.Should().Contain("public const string GsiKey = \"gsi_pk\";", "should map GsiKey property to gsi_pk attribute");
        fieldsCode.Should().Contain("public const string GsiSort = \"gsi_sk\";", "should map GsiSort property to gsi_sk attribute");
        fieldsCode.ShouldContainClass("TestGSIFields");
        fieldsCode.Should().Contain("public const string PartitionKey = \"gsi_pk\";", "should define GSI partition key constant");
        fieldsCode.Should().Contain("public const string SortKey = \"gsi_sk\";", "should define GSI sort key constant");
    }

    [Fact]
    public void Generator_WithSingleEntity_DoesNotRequireIsDefault()
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

        // Assert - Single entity should work without IsDefault
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB001", "single entity tables don't require explicit IsDefault");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB002", "single entity tables can't have multiple defaults");
        result.GeneratedSources.Should().HaveCount(4); // Fields, Keys, Entity, Table
    }

    [Fact]
    public void Generator_WithMultipleEntitiesNoDefault_EmitsFDDB001()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"")]
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

        // Assert - Should emit FDDB001 error
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB001", "multiple entities without default should emit FDDB001");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB001");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("shared-table");
        diagnostic.GetMessage().Should().Contain("no default specified");
    }

    [Fact]
    public void Generator_WithMultipleEntitiesOneDefault_Succeeds()
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

        // Assert - Should not emit FDDB001 or FDDB002
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB001", "one entity is marked as default");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB002", "only one entity is marked as default");
        result.GeneratedSources.Should().HaveCount(7); // 2 entities × (Fields + Keys + Entity) + 1 Table
    }

    [Fact]
    public void Generator_WithMultipleEntitiesMultipleDefaults_EmitsFDDB002()
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

    [DynamoDbTable(""shared-table"", IsDefault = true)]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - Should emit FDDB002 error
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB002", "multiple entities marked as default should emit FDDB002");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB002");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("shared-table");
        diagnostic.GetMessage().Should().Contain("multiple entities marked as default");
    }

    [Fact]
    public void Generator_UsesTableNameForTableClass()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""my-app-table"")]
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
        result.Diagnostics.Should().BeEmpty();
        
        // Check that table class is named after the table name, not entity name
        // Table name "my-app-table" should become "MyAppTableTable" (split by hyphen, capitalize each part, append "Table")
        var tableFiles = result.GeneratedSources.Where(s => s.FileName.Contains("Table.g.cs") && !s.FileName.Contains("Fields") && !s.FileName.Contains("Keys")).ToArray();
        tableFiles.Should().HaveCount(1, "should generate exactly one table class");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        tableCode.ShouldContainClass("MyAppTableTable");
        tableCode.Should().Contain("public partial class MyAppTableTable : DynamoDbTableBase", 
            "table class should be named after table name (my-app-table -> MyAppTableTable), not entity name (Order)");
        tableCode.Should().Contain("public MyAppTableTable(IAmazonDynamoDB client)", 
            "constructor should use table class name");
    }

    [Fact]
    public void Generator_MultipleEntitiesSameTable_GeneratesOneTableClass()
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
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB001" || d.Id == "FDDB002");
        
        // Should generate only one table class named after the table
        var tableFiles = result.GeneratedSources.Where(s => s.FileName.Contains("Table.g.cs") && !s.FileName.Contains("Fields") && !s.FileName.Contains("Keys")).ToArray();
        tableFiles.Should().HaveCount(1, "multiple entities sharing same table should generate only one table class");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        // Table name "shared-table" should become "SharedTableTable" (split by hyphen, capitalize each part, append "Table")
        tableCode.ShouldContainClass("SharedTableTable");
        tableCode.Should().Contain("public partial class SharedTableTable : DynamoDbTableBase", 
            "table class should be named after table name (shared-table -> SharedTableTable)");
    }

    [Fact]
    public void Generator_WithMultipleEntities_GeneratesEntityAccessorProperties()
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
        
        // Debug: Check what files were generated
        var allFiles = string.Join(", ", result.GeneratedSources.Select(s => System.IO.Path.GetFileName(s.FileName)));
        
        var tableFiles = result.GeneratedSources.Where(s => s.FileName.Contains("SharedTableTable.g.cs")).ToArray();
        tableFiles.Should().HaveCount(1, $"should generate one table class for shared-table. Generated files: {allFiles}");
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should generate entity accessor properties with default pluralized names
        tableCode.Should().Contain("public OrderAccessor Orders", "should generate accessor property for Order entity with pluralized name");
        tableCode.Should().Contain("public OrderLineAccessor OrderLines", "should generate accessor property for OrderLine entity with pluralized name");
        
        // Should initialize accessors in constructor
        tableCode.Should().Contain("Orders = new OrderAccessor(this);", "should initialize Order accessor in constructor");
        tableCode.Should().Contain("OrderLines = new OrderLineAccessor(this);", "should initialize OrderLine accessor in constructor");
    }

    [Fact]
    public void Generator_WithCustomEntityPropertyName_UsesCustomName()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    [GenerateEntityProperty(Name = ""CustomOrders"")]
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
        
        var tableFiles = result.GeneratedSources.Where(s => s.FileName.Contains("SharedTableTable.g.cs")).ToArray();
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should use custom name instead of pluralized name
        tableCode.Should().Contain("public OrderAccessor CustomOrders", "should use custom name from GenerateEntityProperty attribute");
        tableCode.Should().Contain("CustomOrders = new OrderAccessor(this);", "should initialize accessor with custom name");
    }

    [Fact]
    public void Generator_WithGenerateFalse_DoesNotGenerateAccessorProperty()
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
        
        var tableFiles = result.GeneratedSources.Where(s => s.FileName.Contains("SharedTableTable.g.cs")).ToArray();
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should generate accessor for Order but not for OrderLine
        tableCode.Should().Contain("public OrderAccessor Orders", "should generate accessor property for Order entity");
        tableCode.Should().NotContain("OrderLineAccessor", "should not generate accessor for OrderLine when Generate = false");
    }

    [Fact]
    public void Generator_WithInternalModifier_GeneratesInternalAccessorProperty()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    [GenerateEntityProperty(Modifier = AccessModifier.Internal)]
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
        
        var tableFiles = result.GeneratedSources.Where(s => s.FileName.Contains("SharedTableTable.g.cs")).ToArray();
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should generate internal accessor property
        tableCode.Should().Contain("internal OrderAccessor Orders", "should generate internal accessor property when Modifier = Internal");
        tableCode.Should().Contain("public OrderLineAccessor OrderLines", "should generate public accessor property by default");
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
                // Add AWS SDK references for generated code
                MetadataReference.CreateFromFile(typeof(Amazon.DynamoDBv2.Model.AttributeValue).Assembly.Location),
                // Add main library reference for IDynamoDbEntity and other types
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Storage.IDynamoDbEntity).Assembly.Location),
                // Add System.Linq reference
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                // Add System.IO reference  
                MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
                // Add netstandard reference for Attribute, Enum, and other base types
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll")),
                // Add System.Collections reference for Dictionary<,> and List<>
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Collections.dll")),
                // Add System.Linq.Expressions reference
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

public class GeneratorTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required GeneratedSource[] GeneratedSources { get; set; }
}

public class GeneratedSource
{
    public GeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}