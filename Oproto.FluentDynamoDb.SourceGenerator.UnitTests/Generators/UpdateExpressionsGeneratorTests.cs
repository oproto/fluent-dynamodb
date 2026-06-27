using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for UpdateExpressionsGenerator to verify generation of UpdateExpressions and UpdateModel classes.
/// Tests cover property type mapping, nullable generation, namespace preservation, and XML documentation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Generator", "UpdateExpressions")]
public class UpdateExpressionsGeneratorTests
{
    [Fact]
    public void Generator_WithBasicEntity_GeneratesUpdateExpressionsClass()
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
        
        [DynamoDbAttribute(""count"")]
        public int Count { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("TestEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull("should generate UpdateExpressions class");
        
        var code = updateExpressionsFile!.SourceText.ToString();
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(code, source);
        
        // Verify class structure
        code.ShouldContainClass("TestEntityUpdateExpressions");
        code.Should().Contain("namespace TestNamespace", "should preserve entity namespace");
        
        // Key property should be excluded (Req 1.1)
        code.Should().NotContain("UpdateExpressionProperty<string> Id", "should exclude partition key property from UpdateExpressions");
        
        // Non-key properties should be included
        code.Should().Contain("UpdateExpressionProperty<string> Name", "should generate UpdateExpressionProperty<string> for Name property");
        code.Should().Contain("UpdateExpressionProperty<int> Count", "should generate UpdateExpressionProperty<int> for int property");
        
        // Verify XML documentation
        code.Should().Contain("/// <summary>", "should include XML documentation");
        code.Should().Contain("Expression parameter class for TestEntity update operations", "should document class purpose");
    }

    [Fact]
    public void Generator_WithBasicEntity_GeneratesUpdateModelClass()
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
        
        [DynamoDbAttribute(""count"")]
        public int Count { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("TestEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull("should generate UpdateModel class");
        
        var code = updateModelFile!.SourceText.ToString();
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(code, source);
        
        // Verify class structure
        code.ShouldContainClass("TestEntityUpdateModel");
        code.Should().Contain("namespace TestNamespace", "should preserve entity namespace");
        
        // Key property should be excluded (Req 1.1)
        code.Should().NotContain("string? Id", "should exclude partition key property from UpdateModel");
        
        // Non-key properties should be included as nullable
        code.Should().Contain("string? Name", "should generate nullable string for Name property");
        code.Should().Contain("int? Count", "should generate nullable int for int property");
        
        // Verify XML documentation
        code.Should().Contain("/// <summary>", "should include XML documentation");
        code.Should().Contain("Return type for TestEntity update expressions", "should document class purpose");
    }

    [Fact]
    public void Generator_WithNumericTypes_MapsToCorrectUpdateExpressionPropertyTypes()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class NumericEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""int_val"")]
        public int IntValue { get; set; }
        
        [DynamoDbAttribute(""long_val"")]
        public long LongValue { get; set; }
        
        [DynamoDbAttribute(""decimal_val"")]
        public decimal DecimalValue { get; set; }
        
        [DynamoDbAttribute(""double_val"")]
        public double DoubleValue { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("NumericEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull();
        var code = updateExpressionsFile!.SourceText.ToString();
        
        // Verify correct type mapping
        code.Should().Contain("UpdateExpressionProperty<int> IntValue", "should map int to UpdateExpressionProperty<int>");
        code.Should().Contain("UpdateExpressionProperty<long> LongValue", "should map long to UpdateExpressionProperty<long>");
        code.Should().Contain("UpdateExpressionProperty<decimal> DecimalValue", "should map decimal to UpdateExpressionProperty<decimal>");
        code.Should().Contain("UpdateExpressionProperty<double> DoubleValue", "should map double to UpdateExpressionProperty<double>");
        
        // Verify documentation mentions Add() operation for numeric types
        code.Should().Contain("Add() - Atomic increment/decrement", "should document Add() operation for numeric types");
    }

    [Fact]
    public void Generator_WithCollectionTypes_MapsToCorrectUpdateExpressionPropertyTypes()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class CollectionEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""tags"")]
        public HashSet<string> Tags { get; set; } = new();
        
        [DynamoDbAttribute(""items"")]
        public List<string> Items { get; set; } = new();
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("CollectionEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull();
        var code = updateExpressionsFile!.SourceText.ToString();
        
        // Verify correct type mapping
        code.Should().Contain("UpdateExpressionProperty<System.Collections.Generic.HashSet<string>> Tags", "should map HashSet<string> to UpdateExpressionProperty<HashSet<string>>");
        code.Should().Contain("UpdateExpressionProperty<System.Collections.Generic.List<string>> Items", "should map List<string> to UpdateExpressionProperty<List<string>>");
        
        // Verify documentation mentions appropriate operations
        code.Should().Contain("Delete() - Remove elements from set", "should document Delete() operation for HashSet");
        code.Should().Contain("ListAppend() - Append elements to list", "should document ListAppend() operation for List");
        code.Should().Contain("ListPrepend() - Prepend elements to list", "should document ListPrepend() operation for List");
    }

    [Fact]
    public void Generator_WithNullableTypes_GeneratesCorrectUpdateModelProperties()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class NullableEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""nullable_int"")]
        public int? NullableInt { get; set; }
        
        [DynamoDbAttribute(""nullable_long"")]
        public long? NullableLong { get; set; }
        
        [DynamoDbAttribute(""nullable_decimal"")]
        public decimal? NullableDecimal { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("NullableEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull();
        var code = updateModelFile!.SourceText.ToString();
        
        // Verify nullable types remain nullable in UpdateModel
        code.Should().Contain("int? NullableInt", "should keep int? as int? in UpdateModel");
        code.Should().Contain("long? NullableLong", "should keep long? as long? in UpdateModel");
        code.Should().Contain("decimal? NullableDecimal", "should keep decimal? as decimal? in UpdateModel");
    }

    [Fact]
    public void Generator_WithKeyProperties_IncludesWarningInDocumentation()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class KeyEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string PartitionKey { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""data"")]
        public string Data { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("KeyEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull();
        var expressionsCode = updateExpressionsFile!.SourceText.ToString();
        
        // Key properties should be excluded entirely from UpdateExpressions (Req 1.1, 1.2)
        expressionsCode.Should().NotContain("UpdateExpressionProperty<string> PartitionKey", 
            "should exclude partition key from UpdateExpressions");
        expressionsCode.Should().NotContain("UpdateExpressionProperty<string> SortKey", 
            "should exclude sort key from UpdateExpressions");
        
        // Non-key properties should still be present
        expressionsCode.Should().Contain("UpdateExpressionProperty<string> Data",
            "should include non-key property Data in UpdateExpressions");
        
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("KeyEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull();
        var modelCode = updateModelFile!.SourceText.ToString();
        
        // Key properties should be excluded entirely from UpdateModel (Req 1.1, 1.2)
        modelCode.Should().NotContain("string? PartitionKey", 
            "should exclude partition key from UpdateModel");
        modelCode.Should().NotContain("string? SortKey", 
            "should exclude sort key from UpdateModel");
        
        // Non-key properties should still be present
        modelCode.Should().Contain("string? Data",
            "should include non-key property Data in UpdateModel");
    }

    [Fact]
    public void Generator_WithDifferentNamespaces_PreservesNamespace()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace MyApp.Domain.Entities
{
    [DynamoDbTable(""test-table"")]
    public partial class CustomEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("CustomEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull();
        var expressionsCode = updateExpressionsFile!.SourceText.ToString();
        
        expressionsCode.Should().Contain("namespace MyApp.Domain.Entities", 
            "should preserve original namespace in UpdateExpressions");
        
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("CustomEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull();
        var modelCode = updateModelFile!.SourceText.ToString();
        
        modelCode.Should().Contain("namespace MyApp.Domain.Entities", 
            "should preserve original namespace in UpdateModel");
    }

    [Fact]
    public void Generator_WithComplexTypes_GeneratesCorrectPropertyTypes()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class ComplexEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""created"")]
        public DateTime CreatedAt { get; set; }
        
        [DynamoDbAttribute(""guid"")]
        public Guid UniqueId { get; set; }
        
        [DynamoDbAttribute(""active"")]
        public bool IsActive { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("ComplexEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull();
        var expressionsCode = updateExpressionsFile!.SourceText.ToString();
        
        // Verify correct type mapping for complex types
        expressionsCode.Should().Contain("UpdateExpressionProperty<System.DateTime> CreatedAt", 
            "should map DateTime to UpdateExpressionProperty<DateTime>");
        expressionsCode.Should().Contain("UpdateExpressionProperty<System.Guid> UniqueId", 
            "should map Guid to UpdateExpressionProperty<Guid>");
        expressionsCode.Should().Contain("UpdateExpressionProperty<bool> IsActive", 
            "should map bool to UpdateExpressionProperty<bool>");
        
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("ComplexEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull();
        var modelCode = updateModelFile!.SourceText.ToString();
        
        // Verify nullable versions in UpdateModel
        modelCode.Should().Contain("System.DateTime? CreatedAt", "should make DateTime nullable in UpdateModel");
        modelCode.Should().Contain("System.Guid? UniqueId", "should make Guid nullable in UpdateModel");
        modelCode.Should().Contain("bool? IsActive", "should make bool nullable in UpdateModel");
    }

    [Fact]
    public void Generator_WithAllOperationTypes_DocumentsAvailableOperations()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class OperationsEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""counter"")]
        public int Counter { get; set; }
        
        [DynamoDbAttribute(""tags"")]
        public HashSet<string> Tags { get; set; } = new();
        
        [DynamoDbAttribute(""history"")]
        public List<string> History { get; set; } = new();
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("OperationsEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull();
        var code = updateExpressionsFile!.SourceText.ToString();
        
        // Verify documentation for numeric type operations
        code.Should().Contain("Add() - Atomic increment/decrement", 
            "should document Add() for numeric types");
        code.Should().Contain("Arithmetic (+, -) - Arithmetic operations in SET", 
            "should document arithmetic operations for numeric types");
        
        // Verify documentation for set operations
        code.Should().Contain("Add() - Add elements to set", 
            "should document Add() for HashSet");
        code.Should().Contain("Delete() - Remove elements from set", 
            "should document Delete() for HashSet");
        
        // Verify documentation for list operations
        code.Should().Contain("ListAppend() - Append elements to list", 
            "should document ListAppend() for List");
        code.Should().Contain("ListPrepend() - Prepend elements to list", 
            "should document ListPrepend() for List");
        
        // Verify documentation for universal operations
        code.Should().Contain("Remove() - Remove attribute", 
            "should document Remove() for all types");
        code.Should().Contain("IfNotExists() - Set value if attribute doesn't exist", 
            "should document IfNotExists() for all types");
    }

    [Fact]
    public void Generator_WithPropertiesWithoutAttributeMapping_ExcludesFromGeneratedClasses()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class MixedEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""mapped"")]
        public string MappedProperty { get; set; } = string.Empty;
        
        // Property without DynamoDbAttribute - should not be included
        public string UnmappedProperty { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("MixedEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull();
        var expressionsCode = updateExpressionsFile!.SourceText.ToString();
        
        // Verify mapped property is included
        expressionsCode.Should().Contain("UpdateExpressionProperty<string> MappedProperty", 
            "should include property with DynamoDbAttribute");
        
        // Verify unmapped property is excluded
        expressionsCode.Should().NotContain("UnmappedProperty", 
            "should exclude property without DynamoDbAttribute");
        
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("MixedEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull();
        var modelCode = updateModelFile!.SourceText.ToString();
        
        // Verify same exclusion in UpdateModel
        modelCode.Should().Contain("string? MappedProperty", 
            "should include property with DynamoDbAttribute in UpdateModel");
        modelCode.Should().NotContain("UnmappedProperty", 
            "should exclude property without DynamoDbAttribute from UpdateModel");
    }

    [Fact]
    public void Generator_UpdateExpressionsClass_IncludesUsageExamples()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class ExampleEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateExpressionsFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("ExampleEntityUpdateExpressions.g.cs"));
        
        updateExpressionsFile.Should().NotBeNull();
        var code = updateExpressionsFile!.SourceText.ToString();
        
        // Verify usage examples in documentation
        code.Should().Contain("<code>", "should include code examples");
        code.Should().Contain("table.ExampleEntity.Update(key)", "should show usage example with entity name");
        code.Should().Contain(".Set(x => new ExampleEntityUpdateModel", "should show Set() method usage");
        code.Should().Contain("UpdateAsync()", "should show async execution");
    }

    [Fact]
    public void Generator_UpdateModelClass_IncludesUsageExamples()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class ExampleEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("ExampleEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull();
        var code = updateModelFile!.SourceText.ToString();
        
        // Verify usage examples in documentation
        code.Should().Contain("<code>", "should include code examples");
        code.Should().Contain("table.ExampleEntity.Update(key)", "should show usage example with entity name");
        code.Should().Contain(".Set(x => new ExampleEntityUpdateModel", "should show Set() method usage");
        code.Should().Contain("// Assign values to properties you want to update", "should include helpful comments");
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


/// <summary>
/// Tests for DynamicFields property generation in UpdateModel class.
/// </summary>
public class UpdateModelDynamicFieldsTests
{
    [Fact]
    public void Generator_WithEnableDynamicFields_GeneratesDynamicFieldsPropertyInUpdateModel()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [EnableDynamicFields]
    public partial class DynamicEntity
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
        result.Diagnostics.Should().NotContain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("DynamicEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull("should generate UpdateModel class");
        
        var code = updateModelFile!.SourceText.ToString();
        
        // Verify DynamicFields property is generated
        code.Should().Contain("public DynamicFieldCollection? DynamicFields { get; set; }",
            "should generate nullable DynamicFieldCollection property");
        
        // Verify using statement for DynamicFieldCollection
        code.Should().Contain("using Oproto.FluentDynamoDb.Entities;",
            "should include Entities namespace for DynamicFieldCollection");
        
        // Verify XML documentation
        code.Should().Contain("Gets or sets the dynamic fields to update",
            "should include XML documentation for DynamicFields property");
        code.Should().Contain("ChangesOnly()",
            "should document ChangesOnly() usage pattern");
    }

    [Fact]
    public void Generator_WithoutEnableDynamicFields_DoesNotGenerateDynamicFieldsPropertyInUpdateModel()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class RegularEntity
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
        result.Diagnostics.Should().NotContain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("RegularEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull("should generate UpdateModel class");
        
        var code = updateModelFile!.SourceText.ToString();
        
        // Verify DynamicFields property is NOT generated
        code.Should().NotContain("DynamicFieldCollection",
            "should not generate DynamicFieldCollection property when EnableDynamicFields is not present");
        code.Should().NotContain("DynamicFields",
            "should not have any DynamicFields reference");
        
        // Verify Entities namespace is NOT included
        code.Should().NotContain("using Oproto.FluentDynamoDb.Entities;",
            "should not include Entities namespace when not needed");
    }

    [Fact]
    public void Generator_WithEnableDynamicFields_DynamicFieldsPropertyHasCorrectDocumentation()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [EnableDynamicFields]
    public partial class DocumentedEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("DocumentedEntityUpdateModel.g.cs"));
        
        updateModelFile.Should().NotBeNull();
        var code = updateModelFile!.SourceText.ToString();
        
        // Verify comprehensive documentation
        code.Should().Contain("Leave null to not modify any dynamic fields",
            "should document null behavior");
        code.Should().Contain("entity.DynamicFields.ChangesOnly()",
            "should show ChangesOnly() usage example");
        code.Should().Contain("collection.Remove(fieldName)",
            "should document removal pattern");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source)
            },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(generator);

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
