using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Tests for enum type validation in the source generator.
/// Verifies that user-defined enum types are accepted by EntityAnalyzer
/// without emitting DYNDB009 (unsupported property type).
/// </summary>
public class EnumTypeValidationTests
{
    #region Bug Condition Tests - Enum Properties Should Be Accepted

    [Fact]
    public void AnalyzeEntity_WithSimpleEnumProperty_AcceptsWithoutError()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum Status
    {
        Pending,
        Success,
        Failure
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public Status EntityStatus { get; set; }
    }
}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == "EntityStatus");
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Fact]
    public void AnalyzeEntity_WithNullableEnumProperty_AcceptsWithoutError()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum Status
    {
        Pending,
        Success,
        Failure
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public Status? OptionalStatus { get; set; }
    }
}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == "OptionalStatus");
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Fact]
    public void AnalyzeEntity_WithEnumListProperty_AcceptsWithoutError()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum Status
    {
        Pending,
        Success,
        Failure
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""statuses"")]
        public List<Status> Statuses { get; set; } = new();
    }
}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == "Statuses");
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Fact]
    public void AnalyzeEntity_WithEnumFormatDProperty_AcceptsWithoutError()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum Status
    {
        Pending = 0,
        Success = 200,
        Failure = 500
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"", Format = ""D"")]
        public Status EntityStatus { get; set; }
    }
}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == "EntityStatus");
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Fact]
    public void AnalyzeEntity_WithEnumHashSetProperty_AcceptsWithoutError()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum Priority
    {
        Low,
        Medium,
        High
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""priorities"")]
        public HashSet<Priority> Priorities { get; set; } = new();
    }
}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == "Priorities");
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    #endregion

    #region Preservation Tests - Non-Enum Types Unchanged

    [Theory]
    [InlineData("string", "Name")]
    [InlineData("int", "Age")]
    [InlineData("bool", "IsActive")]
    [InlineData("DateTime", "CreatedAt")]
    [InlineData("Guid", "TraceId")]
    [InlineData("long", "BigNumber")]
    [InlineData("double", "Score")]
    [InlineData("float", "Weight")]
    [InlineData("decimal", "Price")]
    public void AnalyzeEntity_WithPrimitiveProperty_ContinuesToAcceptWithoutError(string typeName, string propertyName)
    {
        // Arrange
        var source = $@"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""{propertyName.ToLower()}"")]
        public {typeName} {propertyName} {{ get; set; }}
    }}
}}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == propertyName);
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Theory]
    [InlineData("ulong", "Version")]
    [InlineData("uint", "Counter")]
    [InlineData("ushort", "SmallValue")]
    [InlineData("byte", "ByteValue")]
    [InlineData("sbyte", "SignedByteValue")]
    [InlineData("short", "ShortValue")]
    public void AnalyzeEntity_WithUnsignedIntegerProperty_ContinuesToAcceptWithoutError(string typeName, string propertyName)
    {
        // Arrange
        var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""{propertyName.ToLower()}"")]
        public {typeName} {propertyName} {{ get; set; }}
    }}
}}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == propertyName);
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Fact]
    public void AnalyzeEntity_WithDynamoDbMapNestedEntity_ContinuesToAcceptWithoutError()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class Address
    {
        [DynamoDbAttribute(""street"")]
        public string Street { get; set; } = string.Empty;

        [DynamoDbAttribute(""city"")]
        public string City { get; set; } = string.Empty;
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""address"")]
        public Address ShippingAddress { get; set; } = new();
    }
}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == "ShippingAddress");
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Fact]
    public void AnalyzeEntity_WithUnsupportedArbitraryClassType_ContinuesToEmitDYNDB009()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public class SomeRandomClass
    {
        public string Value { get; set; } = string.Empty;
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""random"")]
        public SomeRandomClass Foo { get; set; } = new();
    }
}";

        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert - genuinely unsupported types should still emit DYNDB009
        analyzer.Diagnostics.Should().Contain(d => d.Id == "DYNDB009");
    }

    #endregion

    #region Helper Methods

    private static (TypeDeclarationSyntax, SemanticModel) ParseSource(string source, string className = "TestEntity")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot().DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == className);

        return (classDecl, semanticModel);
    }

    #endregion
}
