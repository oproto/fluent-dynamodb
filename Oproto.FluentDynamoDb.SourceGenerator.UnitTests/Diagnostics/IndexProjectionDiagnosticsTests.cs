using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Tests for index projection diagnostic warnings (FDDB070, FDDB072).
/// </summary>
[Trait("Category", "Unit")]
public class IndexProjectionDiagnosticsTests
{
    #region Descriptor Property Tests

    [Fact]
    public void IncludeProjectionWithoutProperties_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.IncludeProjectionWithoutProperties;

        // Assert
        descriptor.Id.Should().Be("FDDB070");
        descriptor.Title.ToString().Should().Be("Include projection without properties");
        descriptor.MessageFormat.ToString().Should().Contain("ProjectionType = Include");
        descriptor.MessageFormat.ToString().Should().Contain("ProjectedProperties");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void KeysOnlyWithUseProjection_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.KeysOnlyWithUseProjection;

        // Assert
        descriptor.Id.Should().Be("FDDB072");
        descriptor.Title.ToString().Should().Be("KeysOnly with UseProjection");
        descriptor.MessageFormat.ToString().Should().Contain("ProjectionType = KeysOnly");
        descriptor.MessageFormat.ToString().Should().Contain("[UseProjection]");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    #endregion

    #region FDDB070 - Include Projection Without Properties Tests

    [Fact]
    public void GsiWithIncludeProjection_WithoutProjectedProperties_ShouldEmitFDDB070()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"", ProjectionType = ProjectionType.Include)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB070",
            "Should emit FDDB070 warning when ProjectionType = Include but no ProjectedProperties are defined");
    }

    [Fact]
    public void LsiWithIncludeProjection_WithoutProjectedProperties_ShouldEmitFDDB070()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [LsiSortKey(""created-index"", ProjectionType = ProjectionType.Include)]
        [DynamoDbAttribute(""created_at"")]
        public DateTime CreatedAt { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB070",
            "Should emit FDDB070 warning when LSI has ProjectionType = Include but no ProjectedProperties are defined");
    }

    [Fact]
    public void GsiWithAllProjection_ShouldNotEmitFDDB070()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"", ProjectionType = ProjectionType.All)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB070",
            "Should not emit FDDB070 warning when ProjectionType = All");
    }

    [Fact]
    public void GsiWithKeysOnlyProjection_ShouldNotEmitFDDB070()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"", ProjectionType = ProjectionType.KeysOnly)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB070",
            "Should not emit FDDB070 warning when ProjectionType = KeysOnly");
    }

    #endregion

    #region FDDB072 - KeysOnly With UseProjection Tests

    [Fact]
    public void GsiWithKeysOnlyAndUseProjection_ShouldEmitFDDB072()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"", ProjectionType = ProjectionType.KeysOnly)]
        [UseProjection(typeof(TestProjection))]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }

    [DynamoDbProjection(typeof(TestEntity))]
    public partial class TestProjection
    {
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB072",
            "Should emit FDDB072 warning when both ProjectionType = KeysOnly and [UseProjection] are specified");
    }

    [Fact]
    public void GsiWithKeysOnlyWithoutUseProjection_ShouldNotEmitFDDB072()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"", ProjectionType = ProjectionType.KeysOnly)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB072",
            "Should not emit FDDB072 warning when only ProjectionType = KeysOnly is specified without [UseProjection]");
    }

    [Fact]
    public void GsiWithAllProjectionAndUseProjection_ShouldNotEmitFDDB072()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"", ProjectionType = ProjectionType.All)]
        [UseProjection(typeof(TestProjection))]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }

    [DynamoDbProjection(typeof(TestEntity))]
    public partial class TestProjection
    {
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB072",
            "Should not emit FDDB072 warning when ProjectionType = All with [UseProjection]");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static GeneratorTestResult GenerateCode(string source)
    {
        // Include attribute definitions in the compilation
        var attributeSource = @"
using System;

namespace Oproto.FluentDynamoDb.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamoDbTableAttribute : Attribute
    {
        public string TableName { get; }
        public string? EntityDiscriminator { get; set; }
        public bool IsDefault { get; set; }
        public DynamoDbTableAttribute(string tableName) => TableName = tableName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DynamoDbAttributeAttribute : Attribute
    {
        public string AttributeName { get; }
        public DynamoDbAttributeAttribute(string attributeName) => AttributeName = attributeName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PartitionKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string? Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SortKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string? Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class GsiPartitionKeyAttribute : Attribute
    {
        public string IndexName { get; }
        public string? Name { get; set; }
        public string? DiscriminatorProperty { get; set; }
        public string? DiscriminatorValue { get; set; }
        public string? DiscriminatorPattern { get; set; }
        public Oproto.FluentDynamoDb.Metadata.ProjectionType ProjectionType { get; set; } = Oproto.FluentDynamoDb.Metadata.ProjectionType.All;
        public GsiPartitionKeyAttribute(string indexName) => IndexName = indexName;
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class GsiSortKeyAttribute : Attribute
    {
        public string IndexName { get; }
        public string? Name { get; set; }
        public Oproto.FluentDynamoDb.Metadata.ProjectionType ProjectionType { get; set; } = Oproto.FluentDynamoDb.Metadata.ProjectionType.All;
        public GsiSortKeyAttribute(string indexName) => IndexName = indexName;
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class LsiSortKeyAttribute : Attribute
    {
        public string IndexName { get; }
        public string? Name { get; set; }
        public Oproto.FluentDynamoDb.Metadata.ProjectionType ProjectionType { get; set; } = Oproto.FluentDynamoDb.Metadata.ProjectionType.All;
        public LsiSortKeyAttribute(string indexName) => IndexName = indexName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class UseProjectionAttribute : Attribute
    {
        public Type ProjectionType { get; }
        public UseProjectionAttribute(Type projectionType) => ProjectionType = projectionType;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DynamoDbProjectionAttribute : Attribute
    {
        public Type SourceEntityType { get; }
        public DynamoDbProjectionAttribute(Type sourceEntityType) => SourceEntityType = sourceEntityType;
    }
}

namespace Oproto.FluentDynamoDb.Metadata
{
    public enum ProjectionType
    {
        All = 0,
        KeysOnly = 1,
        Include = 2
    }
}";

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText(attributeSource),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
            },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
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

    #endregion
}
