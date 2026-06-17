using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests verifying backward compatibility for non-overlapping discriminator patterns.
/// When entities on the same table use patterns like "USER#*" and "ORDER#*" that cannot overlap,
/// the source generator should produce identical code to pre-enhancement behavior:
/// - No exclusion guards in generated MatchesEntity methods
/// - No DISC004 or DISC005 diagnostics emitted
/// - MatchesEntity correctly identifies its own items and rejects others
///
/// Validates: Requirements 4.1, 4.4
/// </summary>
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class NonOverlappingPatternsBackwardCompatibilityIntegrationTests
{
    private const string EntitySource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""USER#*"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""ORDER#*"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""total"")]
        public decimal Total { get; set; }
    }
}";

    /// <summary>
    /// Verifies that User.MatchesEntity returns true for "USER#123" and false for "ORDER#456".
    /// This confirms non-overlapping patterns match their own items correctly.
    /// </summary>
    [Fact]
    public void User_MatchesEntity_ReturnsTrue_ForUserItem()
    {
        // Arrange
        var (userType, orderType) = CompileAndLoadEntities();
        var item = CreateItem("TENANT#1", "USER#123");

        // Act
        var result = InvokeMatchesEntity(userType, item);

        // Assert
        result.Should().BeTrue("User should match sort key 'USER#123'");
    }

    /// <summary>
    /// Verifies that User.MatchesEntity returns false for "ORDER#456".
    /// </summary>
    [Fact]
    public void User_MatchesEntity_ReturnsFalse_ForOrderItem()
    {
        // Arrange
        var (userType, orderType) = CompileAndLoadEntities();
        var item = CreateItem("TENANT#1", "ORDER#456");

        // Act
        var result = InvokeMatchesEntity(userType, item);

        // Assert
        result.Should().BeFalse("User should NOT match sort key 'ORDER#456'");
    }

    /// <summary>
    /// Verifies that Order.MatchesEntity returns true for "ORDER#456" and false for "USER#123".
    /// </summary>
    [Fact]
    public void Order_MatchesEntity_ReturnsTrue_ForOrderItem()
    {
        // Arrange
        var (userType, orderType) = CompileAndLoadEntities();
        var item = CreateItem("TENANT#1", "ORDER#456");

        // Act
        var result = InvokeMatchesEntity(orderType, item);

        // Assert
        result.Should().BeTrue("Order should match sort key 'ORDER#456'");
    }

    /// <summary>
    /// Verifies that Order.MatchesEntity returns false for "USER#123".
    /// </summary>
    [Fact]
    public void Order_MatchesEntity_ReturnsFalse_ForUserItem()
    {
        // Arrange
        var (userType, orderType) = CompileAndLoadEntities();
        var item = CreateItem("TENANT#1", "USER#123");

        // Act
        var result = InvokeMatchesEntity(orderType, item);

        // Assert
        result.Should().BeFalse("Order should NOT match sort key 'USER#123'");
    }

    /// <summary>
    /// Verifies that the source generator does NOT emit DISC004 (ambiguous overlap error)
    /// or DISC005 (resolved overlap info) diagnostics for non-overlapping patterns.
    /// This confirms no overlap-related diagnostics are emitted for backward-compatible scenarios.
    /// </summary>
    [Fact]
    public void SourceGenerator_DoesNotEmitOverlapDiagnostics_ForNonOverlappingPatterns()
    {
        // Arrange & Act
        var result = RunSourceGenerator();

        // Assert — No DISC004 diagnostics (ambiguous overlap error)
        var disc004 = result.Diagnostics.Where(d => d.Id == "DISC004").ToList();
        disc004.Should().BeEmpty(
            "DISC004 should NOT be emitted for non-overlapping patterns (USER#* and ORDER#* cannot overlap)");

        // Assert — No DISC005 diagnostics (resolved overlap info)
        var disc005 = result.Diagnostics.Where(d => d.Id == "DISC005").ToList();
        disc005.Should().BeEmpty(
            "DISC005 should NOT be emitted for non-overlapping patterns (no overlap to resolve)");
    }

    /// <summary>
    /// Verifies that the generated code for non-overlapping patterns contains NO exclusion guards.
    /// The generated code should be identical to pre-enhancement behavior — just a simple
    /// StartsWith check with no "Exclusion:" comment lines.
    /// </summary>
    [Fact]
    public void GeneratedCode_ContainsNoExclusionGuards_ForNonOverlappingPatterns()
    {
        // Arrange & Act
        var result = GenerateCode();

        // Assert — User generated code should not have exclusion guards
        var userCode = GetGeneratedSource(result, "User.g.cs");
        userCode.Should().NotContain("Exclusion:",
            "User generated code should not contain exclusion comments (no overlapping patterns)");

        // Assert — Order generated code should not have exclusion guards
        var orderCode = GetGeneratedSource(result, "Order.g.cs");
        orderCode.Should().NotContain("Exclusion:",
            "Order generated code should not contain exclusion comments (no overlapping patterns)");
    }

    /// <summary>
    /// Verifies PatternOverlapAnalyzer.Analyze produces no diagnostics for non-overlapping patterns.
    /// This directly tests the analyzer component confirms backward compatibility.
    /// </summary>
    [Fact]
    public void PatternOverlapAnalyzer_ProducesNoDiagnostics_ForNonOverlappingPatterns()
    {
        // Arrange — create entity models with non-overlapping patterns
        var userEntity = new EntityModel
        {
            ClassName = "User",
            TableName = "shared-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "USER#*",
                Strategy = DiscriminatorStrategy.StartsWith
            }
        };

        var orderEntity = new EntityModel
        {
            ClassName = "Order",
            TableName = "shared-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*",
                Strategy = DiscriminatorStrategy.StartsWith
            }
        };

        var tableEntities = new List<EntityModel> { userEntity, orderEntity };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — no diagnostics emitted for non-overlapping patterns
        diagnostics.Should().BeEmpty(
            "PatternOverlapAnalyzer should produce no diagnostics when patterns do not overlap");

        // Assert — no exclusion patterns added to either entity
        userEntity.Discriminator.OverlappingPatterns.Should().BeEmpty(
            "User should have no overlapping patterns (USER#* does not overlap ORDER#*)");
        orderEntity.Discriminator.OverlappingPatterns.Should().BeEmpty(
            "Order should have no overlapping patterns (ORDER#* does not overlap USER#*)");
    }

    #region Helper Methods

    private static (Type userType, Type orderType) CompileAndLoadEntities()
    {
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            EntitySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var userType = compilationResult.Assembly.GetType("TestNamespace.User")
            ?? throw new InvalidOperationException("User type not found in compiled assembly");
        var orderType = compilationResult.Assembly.GetType("TestNamespace.Order")
            ?? throw new InvalidOperationException("Order type not found in compiled assembly");

        return (userType, orderType);
    }

    private static GeneratorTestResult RunSourceGenerator()
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(EntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var driverDiagnostics);

        return new GeneratorTestResult
        {
            Diagnostics = driverDiagnostics,
            GeneratedSources = Array.Empty<GeneratedSource>()
        };
    }

    private static GeneratorTestResult GenerateCode()
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(EntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var driverDiagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = driverDiagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        source.Should().NotBeNull($"Generated source file {fileName} should exist");
        return source!.SourceText.ToString();
    }

    private static Dictionary<string, AttributeValue> CreateItem(string pk, string sk)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
    }

    private static bool InvokeMatchesEntity(Type entityType, Dictionary<string, AttributeValue> item)
    {
        var method = entityType.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException(
                $"MatchesEntity method not found on type '{entityType.Name}'. " +
                "Ensure the source generator produced the expected code.");
        }

        var result = method.Invoke(null, new object[] { item });
        return (bool)result!;
    }

    #endregion
}
