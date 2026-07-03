using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for non-resolvable expression edge cases in constant key detection.
/// 
/// These tests verify that the EntityAnalyzer correctly rejects expressions that cannot
/// be resolved to compile-time constant strings, and correctly resolves expressions
/// that CAN be resolved (nameof, const from different class).
/// 
/// **Validates: Requirements 1.3, 1.4, 2.2**
/// </summary>
[Trait("Category", "Unit")]
public class ConstantKeyDetectionTests
{
    #region Non-resolvable expressions (should NOT detect as constant key)

    /// <summary>
    /// A method call return value is not a compile-time constant.
    /// PropertyModel.ConstantKeyValue should remain null.
    /// 
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Fact]
    public void MethodCallReturn_ShouldNotDetectAsConstantKey()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => GetValue();

        private string GetValue() => ""PROFILE"";
    }
}";

        // Act
        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        var skProperty = result!.Properties.FirstOrDefault(p => p.IsSortKey);
        skProperty.Should().NotBeNull("Sort key property should exist");
        skProperty!.ConstantKeyValue.Should().BeNull(
            "Method call returns cannot be resolved as compile-time constants");
    }

    /// <summary>
    /// An interpolated string is not a compile-time constant.
    /// PropertyModel.ConstantKeyValue should remain null.
    /// 
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Fact]
    public void InterpolatedStringReturn_ShouldNotDetectAsConstantKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => $""PREFIX_{DateTime.Now}"";
    }
}";

        // Act
        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        var skProperty = result!.Properties.FirstOrDefault(p => p.IsSortKey);
        skProperty.Should().NotBeNull("Sort key property should exist");
        skProperty!.ConstantKeyValue.Should().BeNull(
            "Interpolated strings cannot be resolved as compile-time constants");
    }

    /// <summary>
    /// A conditional (ternary) expression with a non-constant condition is not a compile-time constant.
    /// PropertyModel.ConstantKeyValue should remain null.
    /// 
    /// Note: `true ? "A" : "B"` is actually resolvable by Roslyn since the condition is constant,
    /// so we use a runtime condition (property access) to represent a realistic non-resolvable case.
    /// 
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Fact]
    public void ConditionalExpressionReturn_ShouldNotDetectAsConstantKey()
    {
        // Arrange - use a non-constant condition so Roslyn cannot fold the expression
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => IsActive ? ""A"" : ""B"";

        public bool IsActive { get; set; }
    }
}";

        // Act
        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        var skProperty = result!.Properties.FirstOrDefault(p => p.IsSortKey);
        skProperty.Should().NotBeNull("Sort key property should exist");
        skProperty!.ConstantKeyValue.Should().BeNull(
            "Conditional expressions with non-constant conditions cannot be resolved as compile-time constants");
    }

    /// <summary>
    /// A property access (referencing another property) is not a compile-time constant.
    /// PropertyModel.ConstantKeyValue should remain null.
    /// 
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Fact]
    public void PropertyAccessReturn_ShouldNotDetectAsConstantKey()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => SomeOtherProperty;

        public string SomeOtherProperty { get; set; } = ""VALUE"";
    }
}";

        // Act
        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        var skProperty = result!.Properties.FirstOrDefault(p => p.IsSortKey);
        skProperty.Should().NotBeNull("Sort key property should exist");
        skProperty!.ConstantKeyValue.Should().BeNull(
            "Property access cannot be resolved as a compile-time constant");
    }

    #endregion

    #region Resolvable expressions (SHOULD detect as constant key)

    /// <summary>
    /// A nameof() expression is resolvable via SemanticModel.GetConstantValue().
    /// PropertyModel.ConstantKeyValue should be set to the resolved string.
    /// 
    /// **Validates: Requirements 1.3 (positive case - nameof resolves via GetConstantValue)**
    /// </summary>
    [Fact]
    public void NameofExpression_ShouldDetectAsConstantKey()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public class Customer { }

    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => nameof(Customer);
    }
}";

        // Act
        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        var skProperty = result!.Properties.FirstOrDefault(p => p.IsSortKey);
        skProperty.Should().NotBeNull("Sort key property should exist");
        skProperty!.ConstantKeyValue.Should().Be("Customer",
            "nameof(Customer) should resolve to \"Customer\" via GetConstantValue");
    }

    /// <summary>
    /// A const string field from a different class in the same compilation
    /// should be resolvable via SemanticModel.GetConstantValue().
    /// PropertyModel.ConstantKeyValue should be set to the resolved string.
    /// 
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Fact]
    public void ConstFieldFromDifferentClass_ShouldDetectAsConstantKey()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public static class Constants
    {
        public const string ProfileSk = ""PROFILE"";
    }

    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => Constants.ProfileSk;
    }
}";

        // Act
        var (classDecl, semanticModel) = ParseSource(source, "TestEntity");
        var analyzer = new EntityAnalyzer();
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        var skProperty = result!.Properties.FirstOrDefault(p => p.IsSortKey);
        skProperty.Should().NotBeNull("Sort key property should exist");
        skProperty!.ConstantKeyValue.Should().Be("PROFILE",
            "Constants.ProfileSk should resolve to \"PROFILE\" via GetConstantValue");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// Finds the first class declaration in the source.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "TestEntity");

        return (classDecl, semanticModel);
    }

    /// <summary>
    /// Parses source code and returns the specified class declaration with semantic model.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source, string className)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == className);

        return (classDecl, semanticModel);
    }

    #endregion
}
