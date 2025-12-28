// ============================================================================
// Nested Map of Different Record Types Tests
// ============================================================================
// These tests verify that maps containing nested maps of different record types
// are correctly handled by the source generator.
//
// Requirements: 3.1, 3.2, 3.3, 3.4 from source-generator-bug-fixes spec
// ============================================================================

using System.Collections.Immutable;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for nested maps of different record types.
/// These tests verify that the source generator correctly generates recursive
/// ToDynamoDb and FromDynamoDb calls for nested map structures.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "source-generator-bug-fixes")]
public class NestedMapDifferentTypesTests
{
    #region Test Entities

    /// <summary>
    /// Test case: OuterMap contains InnerMap of a different type.
    /// This verifies Requirements 3.1 and 3.2.
    /// </summary>
    [Fact]
    public void Generator_WithNestedMapsOfDifferentTypes_GeneratesCorrectCode()
    {
        // Arrange - Create nested map types where OuterMap contains InnerMap
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    /// <summary>
    /// Inner map type - the deepest level of nesting.
    /// </summary>
    [DynamoDbEntity]
    public partial class InnerMap
    {
        [DynamoDbAttribute(""innerField1"")]
        public string InnerField1 { get; set; } = string.Empty;

        [DynamoDbAttribute(""innerField2"")]
        public int InnerField2 { get; set; }
    }

    /// <summary>
    /// Outer map type - contains InnerMap as a nested property.
    /// </summary>
    [DynamoDbEntity]
    public partial class OuterMap
    {
        [DynamoDbAttribute(""outerField"")]
        public string OuterField { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""inner"")]
        public InnerMap? Inner { get; set; }
    }

    /// <summary>
    /// Entity that uses OuterMap as a property.
    /// </summary>
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""outer"")]
        public OuterMap? Outer { get; set; }
    }
}";

        // Act - Generate code
        var result = GenerateCode(source);

        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "source generator should not produce errors for valid nested map types");

        // Get the generated code for all types
        var testEntityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        var outerMapCode = GetGeneratedSource(result, "OuterMap.g.cs");
        var innerMapCode = GetGeneratedSource(result, "InnerMap.g.cs");

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(testEntityCode, source, outerMapCode, innerMapCode);

        // Verify OuterMap generates recursive calls for InnerMap
        outerMapCode.Should().Contain("InnerMap.ToDynamoDb",
            "OuterMap should call InnerMap.ToDynamoDb for serialization");
        outerMapCode.Should().Contain("InnerMap.FromDynamoDb<InnerMap>",
            "OuterMap should call InnerMap.FromDynamoDb for deserialization");

        // Verify TestEntity generates recursive calls for OuterMap
        testEntityCode.Should().Contain("OuterMap.ToDynamoDb",
            "TestEntity should call OuterMap.ToDynamoDb for serialization");
        testEntityCode.Should().Contain("OuterMap.FromDynamoDb<OuterMap>",
            "TestEntity should call OuterMap.FromDynamoDb for deserialization");
    }

    /// <summary>
    /// Test case: Three levels of nesting (Entity -> OuterMap -> MiddleMap -> InnerMap).
    /// This verifies deep nesting works correctly.
    /// </summary>
    [Fact]
    public void Generator_WithThreeLevelsOfNestedMaps_GeneratesCorrectCode()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class DeepInnerMap
    {
        [DynamoDbAttribute(""deepValue"")]
        public string DeepValue { get; set; } = string.Empty;
    }

    [DynamoDbEntity]
    public partial class MiddleMap
    {
        [DynamoDbAttribute(""middleValue"")]
        public string MiddleValue { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""deep"")]
        public DeepInnerMap? Deep { get; set; }
    }

    [DynamoDbEntity]
    public partial class TopMap
    {
        [DynamoDbAttribute(""topValue"")]
        public string TopValue { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""middle"")]
        public MiddleMap? Middle { get; set; }
    }

    [DynamoDbTable(""test-table"")]
    public partial class DeepNestedEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""top"")]
        public TopMap? Top { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var entityCode = GetGeneratedSource(result, "DeepNestedEntity.g.cs");
        var topMapCode = GetGeneratedSource(result, "TopMap.g.cs");
        var middleMapCode = GetGeneratedSource(result, "MiddleMap.g.cs");
        var deepInnerMapCode = GetGeneratedSource(result, "DeepInnerMap.g.cs");

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source, topMapCode, middleMapCode, deepInnerMapCode);

        // Verify each level generates recursive calls
        entityCode.Should().Contain("TopMap.ToDynamoDb");
        entityCode.Should().Contain("TopMap.FromDynamoDb<TopMap>");

        topMapCode.Should().Contain("MiddleMap.ToDynamoDb");
        topMapCode.Should().Contain("MiddleMap.FromDynamoDb<MiddleMap>");

        middleMapCode.Should().Contain("DeepInnerMap.ToDynamoDb");
        middleMapCode.Should().Contain("DeepInnerMap.FromDynamoDb<DeepInnerMap>");
    }

    /// <summary>
    /// Test case: Nested maps with non-nullable inner type.
    /// </summary>
    [Fact]
    public void Generator_WithNonNullableNestedMap_GeneratesCorrectCode()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class RequiredInnerMap
    {
        [DynamoDbAttribute(""value"")]
        public string Value { get; set; } = string.Empty;
    }

    [DynamoDbEntity]
    public partial class RequiredOuterMap
    {
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""required"")]
        public RequiredInnerMap Required { get; set; } = new();
    }

    [DynamoDbTable(""test-table"")]
    public partial class NonNullableNestedEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""outer"")]
        public RequiredOuterMap Outer { get; set; } = new();
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var entityCode = GetGeneratedSource(result, "NonNullableNestedEntity.g.cs");
        var outerMapCode = GetGeneratedSource(result, "RequiredOuterMap.g.cs");
        var innerMapCode = GetGeneratedSource(result, "RequiredInnerMap.g.cs");

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source, outerMapCode, innerMapCode);

        // Verify recursive calls are generated
        outerMapCode.Should().Contain("RequiredInnerMap.ToDynamoDb");
        outerMapCode.Should().Contain("RequiredInnerMap.FromDynamoDb<RequiredInnerMap>");
    }

    /// <summary>
    /// Test case: Nested maps in composite entity (with RelatedEntity).
    /// This verifies nested maps work correctly in multi-item deserialization.
    /// </summary>
    [Fact]
    public void Generator_WithNestedMapsInCompositeEntity_GeneratesCorrectCode()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class NestedDetails
    {
        [DynamoDbAttribute(""detail"")]
        public string Detail { get; set; } = string.Empty;
    }

    [DynamoDbEntity]
    public partial class CompositeMapType
    {
        [DynamoDbAttribute(""field"")]
        public string Field { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""nested"")]
        public NestedDetails? Nested { get; set; }
    }

    [DynamoDbTable(""composite-table"", IsDefault = true)]
    public partial class CompositeParent
    {
        [PartitionKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""mapData"")]
        public CompositeMapType? MapData { get; set; }

        [RelatedEntity(""PARENT#*#CHILD#*"", EntityType = typeof(CompositeChild))]
        public List<CompositeChild> Children { get; set; } = new();
    }

    [DynamoDbTable(""composite-table"")]
    public partial class CompositeChild
    {
        [PartitionKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""childField"")]
        public string ChildField { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var parentCode = GetGeneratedSource(result, "CompositeParent.g.cs");
        var childCode = GetGeneratedSource(result, "CompositeChild.g.cs");
        var mapTypeCode = GetGeneratedSource(result, "CompositeMapType.g.cs");
        var nestedDetailsCode = GetGeneratedSource(result, "NestedDetails.g.cs");

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(parentCode, source, childCode, mapTypeCode, nestedDetailsCode);

        // Verify nested map calls in parent entity (both single-item and multi-item)
        parentCode.Should().Contain("CompositeMapType.FromDynamoDb<CompositeMapType>",
            "parent entity should use nested FromDynamoDb for map property");

        // Verify nested map calls in map type
        mapTypeCode.Should().Contain("NestedDetails.ToDynamoDb");
        mapTypeCode.Should().Contain("NestedDetails.FromDynamoDb<NestedDetails>");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static NestedMapTestResult GenerateCode(string source)
    {
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new NestedMapGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new NestedMapTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(NestedMapTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        source.Should().NotBeNull($"Expected to find generated source containing '{fileNamePart}'");
        return source!.SourceText.ToString();
    }

    #endregion
}

/// <summary>
/// Result from running the source generator for nested map tests.
/// </summary>
internal class NestedMapTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required NestedMapGeneratedSource[] GeneratedSources { get; set; }
}

/// <summary>
/// Represents a generated source file for nested map tests.
/// </summary>
internal class NestedMapGeneratedSource
{
    public NestedMapGeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}


#region Task 7.2 - Verify Generated Code Handles Nested Maps Correctly

/// <summary>
/// Tests that verify the generated code structure for nested maps.
/// These tests explicitly check that ToDynamoDb and FromDynamoDb generate
/// recursive calls for nested map types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "source-generator-bug-fixes")]
public class NestedMapGeneratedCodeVerificationTests
{
    /// <summary>
    /// Verifies that ToDynamoDb generates recursive calls for nested maps.
    /// **Validates: Requirements 3.3, 3.4**
    /// </summary>
    [Fact]
    public void Generator_ToDynamoDb_GeneratesRecursiveCallsForNestedMaps()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class Level2Map
    {
        [DynamoDbAttribute(""level2Value"")]
        public string Level2Value { get; set; } = string.Empty;
    }

    [DynamoDbEntity]
    public partial class Level1Map
    {
        [DynamoDbAttribute(""level1Value"")]
        public string Level1Value { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""level2"")]
        public Level2Map? Level2 { get; set; }
    }

    [DynamoDbTable(""test-table"")]
    public partial class RootEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""level1"")]
        public Level1Map? Level1 { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - No errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        // Get generated code
        var rootEntityCode = GetGeneratedSource(result, "RootEntity.g.cs");
        var level1MapCode = GetGeneratedSource(result, "Level1Map.g.cs");
        var level2MapCode = GetGeneratedSource(result, "Level2Map.g.cs");

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(rootEntityCode, source, level1MapCode, level2MapCode);

        // VERIFY: RootEntity.ToDynamoDb calls Level1Map.ToDynamoDb
        rootEntityCode.Should().Contain("Level1Map.ToDynamoDb",
            "RootEntity.ToDynamoDb MUST call Level1Map.ToDynamoDb for nested map serialization");
        
        // VERIFY: Level1Map.ToDynamoDb calls Level2Map.ToDynamoDb
        level1MapCode.Should().Contain("Level2Map.ToDynamoDb",
            "Level1Map.ToDynamoDb MUST call Level2Map.ToDynamoDb for nested map serialization");

        // VERIFY: The nested ToDynamoDb results are wrapped in Map (M) attribute
        // The pattern is: var xxxMap = XxxMap.ToDynamoDb(...); then item["xxx"] = new AttributeValue { M = xxxMap };
        rootEntityCode.Should().Contain("{ M = level1Map }",
            "RootEntity should wrap Level1Map.ToDynamoDb result in Map (M) attribute");
        level1MapCode.Should().Contain("{ M = level2Map }",
            "Level1Map should wrap Level2Map.ToDynamoDb result in Map (M) attribute");
    }

    /// <summary>
    /// Verifies that FromDynamoDb generates recursive calls for nested maps.
    /// **Validates: Requirements 3.3, 3.4**
    /// </summary>
    [Fact]
    public void Generator_FromDynamoDb_GeneratesRecursiveCallsForNestedMaps()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class InnerData
    {
        [DynamoDbAttribute(""innerValue"")]
        public string InnerValue { get; set; } = string.Empty;
    }

    [DynamoDbEntity]
    public partial class OuterData
    {
        [DynamoDbAttribute(""outerValue"")]
        public string OuterValue { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""inner"")]
        public InnerData? Inner { get; set; }
    }

    [DynamoDbTable(""test-table"")]
    public partial class DataEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""outer"")]
        public OuterData? Outer { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - No errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        // Get generated code
        var dataEntityCode = GetGeneratedSource(result, "DataEntity.g.cs");
        var outerDataCode = GetGeneratedSource(result, "OuterData.g.cs");
        var innerDataCode = GetGeneratedSource(result, "InnerData.g.cs");

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(dataEntityCode, source, outerDataCode, innerDataCode);

        // VERIFY: DataEntity.FromDynamoDb calls OuterData.FromDynamoDb
        dataEntityCode.Should().Contain("OuterData.FromDynamoDb<OuterData>",
            "DataEntity.FromDynamoDb MUST call OuterData.FromDynamoDb for nested map deserialization");
        
        // VERIFY: OuterData.FromDynamoDb calls InnerData.FromDynamoDb
        outerDataCode.Should().Contain("InnerData.FromDynamoDb<InnerData>",
            "OuterData.FromDynamoDb MUST call InnerData.FromDynamoDb for nested map deserialization");

        // VERIFY: The nested FromDynamoDb calls access .M property
        dataEntityCode.Should().Contain(".M, options)",
            "DataEntity should access .M property for nested map deserialization");
        outerDataCode.Should().Contain(".M, options)",
            "OuterData should access .M property for nested map deserialization");
    }

    /// <summary>
    /// Verifies that both ToDynamoDb and FromDynamoDb handle null nested maps correctly.
    /// </summary>
    [Fact]
    public void Generator_NullableNestedMaps_GeneratesNullChecks()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class NullableInner
    {
        [DynamoDbAttribute(""value"")]
        public string Value { get; set; } = string.Empty;
    }

    [DynamoDbEntity]
    public partial class NullableOuter
    {
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""inner"")]
        public NullableInner? Inner { get; set; }
    }

    [DynamoDbTable(""test-table"")]
    public partial class NullableNestedEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""outer"")]
        public NullableOuter? Outer { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - No errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        // Get generated code
        var entityCode = GetGeneratedSource(result, "NullableNestedEntity.g.cs");
        var outerCode = GetGeneratedSource(result, "NullableOuter.g.cs");
        var innerCode = GetGeneratedSource(result, "NullableInner.g.cs");

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source, outerCode, innerCode);

        // VERIFY: ToDynamoDb has null checks for nullable nested maps
        // The generated code uses @PropertyName for escaped property names
        entityCode.Should().Contain("if (typedEntity.@Outer != null)",
            "ToDynamoDb should check for null before serializing nullable nested map");
        outerCode.Should().Contain("if (typedEntity.@Inner != null)",
            "ToDynamoDb should check for null before serializing nullable nested map");

        // VERIFY: FromDynamoDb has null checks for nullable nested maps
        entityCode.Should().Contain("TryGetValue(\"outer\"",
            "FromDynamoDb should use TryGetValue for nullable nested map");
        outerCode.Should().Contain("TryGetValue(\"inner\"",
            "FromDynamoDb should use TryGetValue for nullable nested map");
    }

    #region Helper Methods

    private static NestedMapTestResult GenerateCode(string source)
    {
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new NestedMapGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new NestedMapTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(NestedMapTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        source.Should().NotBeNull($"Expected to find generated source containing '{fileNamePart}'");
        return source!.SourceText.ToString();
    }

    #endregion
}

#endregion


#region Task 7.3 - Property Test for Nested Map Round-Trip

/// <summary>
/// Property-based tests for nested map round-trip consistency.
/// These tests verify Property 2 from the design document.
/// </summary>
[Trait("Category", "PropertyTest")]
[Trait("Feature", "source-generator-bug-fixes")]
public class NestedMapRoundTripPropertyTests
{
    /// <summary>
    /// **Feature: source-generator-bug-fixes, Property 2: Nested Map Round-Trip Consistency**
    /// *For any* valid nested map structure (a [DynamoDbMap] property containing another 
    /// [DynamoDbMap] property of a different type), serializing to DynamoDB format and 
    /// deserializing back SHALL produce an equivalent object.
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NestedMap_RoundTrip_ProducesEquivalentObject()
    {
        return Prop.ForAll(
            NestedMapConfigArbitrary(),
            config =>
            {
                // Generate source code for the nested map configuration
                var source = GenerateNestedMapSource(config);
                
                // Generate code using the source generator
                var result = GenerateCode(source);
                
                // Check for compilation errors
                var hasErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                if (hasErrors)
                {
                    var errors = string.Join(", ", result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.GetMessage()));
                    return false.Label($"Compilation errors: {errors}");
                }
                
                // Get the generated code for all types
                var entityCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.EntityName}.g.cs"));
                var outerMapCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.OuterMapName}.g.cs"));
                var innerMapCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.InnerMapName}.g.cs"));
                
                if (entityCode == null || outerMapCode == null || innerMapCode == null)
                {
                    return false.Label("Generated source files not found");
                }
                
                var entityCodeStr = entityCode.SourceText.ToString();
                var outerMapCodeStr = outerMapCode.SourceText.ToString();
                var innerMapCodeStr = innerMapCode.SourceText.ToString();
                
                // VERIFY: Round-trip capability exists
                // 1. Entity has ToDynamoDb that calls OuterMap.ToDynamoDb
                var entityHasToDynamoDb = entityCodeStr.Contains($"{config.OuterMapName}.ToDynamoDb");
                
                // 2. Entity has FromDynamoDb that calls OuterMap.FromDynamoDb
                var entityHasFromDynamoDb = entityCodeStr.Contains($"{config.OuterMapName}.FromDynamoDb<{config.OuterMapName}>");
                
                // 3. OuterMap has ToDynamoDb that calls InnerMap.ToDynamoDb
                var outerHasToDynamoDb = outerMapCodeStr.Contains($"{config.InnerMapName}.ToDynamoDb");
                
                // 4. OuterMap has FromDynamoDb that calls InnerMap.FromDynamoDb
                var outerHasFromDynamoDb = outerMapCodeStr.Contains($"{config.InnerMapName}.FromDynamoDb<{config.InnerMapName}>");
                
                // 5. InnerMap has both ToDynamoDb and FromDynamoDb methods
                var innerHasToDynamoDb = innerMapCodeStr.Contains("public static Dictionary<string, AttributeValue> ToDynamoDb");
                var innerHasFromDynamoDb = innerMapCodeStr.Contains("public static TSelf FromDynamoDb<TSelf>");
                
                // 6. Verify the generated code compiles (this validates the round-trip is structurally correct)
                try
                {
                    CompilationVerifier.AssertGeneratedCodeCompiles(entityCodeStr, source, outerMapCodeStr, innerMapCodeStr);
                }
                catch (CompilationFailedException ex)
                {
                    return false.Label($"Compilation failed: {ex.Message}");
                }
                
                var allConditionsMet = entityHasToDynamoDb && entityHasFromDynamoDb &&
                                       outerHasToDynamoDb && outerHasFromDynamoDb &&
                                       innerHasToDynamoDb && innerHasFromDynamoDb;
                
                return allConditionsMet.Label(
                    $"entityHasToDynamoDb={entityHasToDynamoDb}, entityHasFromDynamoDb={entityHasFromDynamoDb}, " +
                    $"outerHasToDynamoDb={outerHasToDynamoDb}, outerHasFromDynamoDb={outerHasFromDynamoDb}, " +
                    $"innerHasToDynamoDb={innerHasToDynamoDb}, innerHasFromDynamoDb={innerHasFromDynamoDb}");
            });
    }

    #region Arbitraries

    /// <summary>
    /// Generates arbitrary nested map configurations for property-based testing.
    /// </summary>
    private static Arbitrary<NestedMapConfig> NestedMapConfigArbitrary()
    {
        return Gen.Elements(
            // Basic nested map
            new NestedMapConfig
            {
                EntityName = "BasicEntity",
                OuterMapName = "BasicOuterMap",
                InnerMapName = "BasicInnerMap",
                OuterPropertyName = "OuterData",
                InnerPropertyName = "InnerData",
                IsOuterNullable = true,
                IsInnerNullable = true
            },
            // Non-nullable nested maps
            new NestedMapConfig
            {
                EntityName = "RequiredEntity",
                OuterMapName = "RequiredOuterMap",
                InnerMapName = "RequiredInnerMap",
                OuterPropertyName = "RequiredOuter",
                InnerPropertyName = "RequiredInner",
                IsOuterNullable = false,
                IsInnerNullable = false
            },
            // Mixed nullability
            new NestedMapConfig
            {
                EntityName = "MixedEntity",
                OuterMapName = "MixedOuterMap",
                InnerMapName = "MixedInnerMap",
                OuterPropertyName = "MixedOuter",
                InnerPropertyName = "MixedInner",
                IsOuterNullable = true,
                IsInnerNullable = false
            },
            // Different naming patterns
            new NestedMapConfig
            {
                EntityName = "CustomerProfile",
                OuterMapName = "AddressInfo",
                InnerMapName = "GeoCoordinates",
                OuterPropertyName = "Address",
                InnerPropertyName = "Coordinates",
                IsOuterNullable = true,
                IsInnerNullable = true
            },
            // Order-related naming
            new NestedMapConfig
            {
                EntityName = "OrderRecord",
                OuterMapName = "ShippingDetails",
                InnerMapName = "CarrierInfo",
                OuterPropertyName = "Shipping",
                InnerPropertyName = "Carrier",
                IsOuterNullable = true,
                IsInnerNullable = true
            }
        ).ToArbitrary();
    }

    /// <summary>
    /// Generates source code for a nested map configuration.
    /// </summary>
    private static string GenerateNestedMapSource(NestedMapConfig config)
    {
        var outerNullable = config.IsOuterNullable ? "?" : "";
        var innerNullable = config.IsInnerNullable ? "?" : "";
        var outerDefault = config.IsOuterNullable ? "" : " = new();";
        var innerDefault = config.IsInnerNullable ? "" : " = new();";
        
        return $@"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbEntity]
    public partial class {config.InnerMapName}
    {{
        [DynamoDbAttribute(""innerValue"")]
        public string InnerValue {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""innerNumber"")]
        public int InnerNumber {{ get; set; }}
    }}

    [DynamoDbEntity]
    public partial class {config.OuterMapName}
    {{
        [DynamoDbAttribute(""outerValue"")]
        public string OuterValue {{ get; set; }} = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""{config.InnerPropertyName.ToLowerInvariant()}"")]
        public {config.InnerMapName}{innerNullable} {config.InnerPropertyName} {{ get; set; }}{innerDefault}
    }}

    [DynamoDbTable(""test-table"")]
    public partial class {config.EntityName}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""{config.OuterPropertyName.ToLowerInvariant()}"")]
        public {config.OuterMapName}{outerNullable} {config.OuterPropertyName} {{ get; set; }}{outerDefault}
    }}
}}";
    }

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static NestedMapTestResult GenerateCode(string source)
    {
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new NestedMapGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new NestedMapTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    #endregion
}

/// <summary>
/// Configuration for generating nested map test entities.
/// </summary>
internal class NestedMapConfig
{
    public required string EntityName { get; set; }
    public required string OuterMapName { get; set; }
    public required string InnerMapName { get; set; }
    public required string OuterPropertyName { get; set; }
    public required string InnerPropertyName { get; set; }
    public bool IsOuterNullable { get; set; }
    public bool IsInnerNullable { get; set; }
}

#endregion
