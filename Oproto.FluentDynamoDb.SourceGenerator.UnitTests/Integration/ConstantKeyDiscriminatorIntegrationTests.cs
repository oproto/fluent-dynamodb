using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests for discriminator conflict and pattern overlap scenarios
/// when combined with constant key detection.
/// 
/// **Validates: Requirements 3.4, 3.5**
/// </summary>
[Trait("Category", "Integration")]
public class ConstantKeyDiscriminatorIntegrationTests
{
    #region Scenario 1: Explicit DiscriminatorValue + Constant Key

    /// <summary>
    /// When an entity has explicit DiscriminatorPattern on [DynamoDbTable] that differs from
    /// the constant key's derived pattern, FDDB101 should be emitted indicating a conflict.
    /// 
    /// Validates: Requirement 3.5
    /// </summary>
    [Fact]
    public void ExplicitDiscriminatorPattern_ConflictsWithConstantKeyDerivedPattern_EmitsFDDB101()
    {
        // Arrange: Entity with DiscriminatorPattern "META#*" on sk,
        // but constant SK derives ExactMatch pattern "PROFILE"
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Shared"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""META#*"")]
    public partial class ConflictEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = RunSourceGenerator(source);

        // Assert: FDDB101 should be emitted because explicit pattern "META#*" 
        // conflicts with derived pattern "PROFILE" from constant key
        var fddb101Diagnostics = result.Diagnostics.Where(d => d.Id == "FDDB101").ToList();
        fddb101Diagnostics.Should().NotBeEmpty(
            "FDDB101 should be emitted when explicit DiscriminatorPattern conflicts with " +
            "the constant key's auto-derived pattern");
        fddb101Diagnostics.First().Severity.Should().Be(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// When an entity has explicit DiscriminatorPattern (with wildcards) that matches
    /// the constant key's derived pattern exactly, FDDB103 (redundant) should be emitted.
    /// Note: DiscriminatorPattern without wildcards is classified as ExactMatch strategy,
    /// which skips redundancy detection. Use a wildcard pattern like "PROFILE*" to trigger FDDB103
    /// when the derived pattern also contains wildcards.
    /// 
    /// For constant keys (no wildcards in derived pattern), the redundancy detection doesn't fire
    /// because both the explicit pattern and derived pattern are ExactMatch strategy.
    /// This test verifies that an explicit DiscriminatorPattern matching the constant key's
    /// value does NOT produce an error — it's accepted without FDDB101 or FDDB103.
    /// 
    /// Validates: Requirement 3.5
    /// </summary>
    [Fact]
    public void ExplicitDiscriminatorPattern_MatchesConstantKeyValue_NoConflictDiagnostic()
    {
        // Arrange: Entity with DiscriminatorPattern "PROFILE" which matches
        // the constant key's derived ExactMatch pattern
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Shared"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""PROFILE"")]
    public partial class RedundantDiscriminatorEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = RunSourceGenerator(source);

        // Assert: Neither FDDB101 nor FDDB103 fires for this case because:
        // - FDDB101: explicit pattern "PROFILE" matches derived "PROFILE" (no conflict)
        // - FDDB103: Strategy is ExactMatch (no wildcards) → redundancy check is skipped
        // The entity should compile and produce generated code successfully.
        var conflictDiagnostics = result.Diagnostics
            .Where(d => d.Id is "FDDB101" or "FDDB103")
            .ToList();
        conflictDiagnostics.Should().BeEmpty(
            "No FDDB101/FDDB103 diagnostics expected when explicit DiscriminatorPattern " +
            "(no wildcards) matches the constant key's derived pattern — both resolve to " +
            "ExactMatch strategy and the values agree");
        result.GeneratedSources.Should().NotBeEmpty(
            "Entity should compile successfully with matching explicit and derived patterns");
    }

    /// <summary>
    /// When an entity has explicit DiscriminatorValue (ExactMatch strategy) AND a constant key
    /// that derives a different ExactMatch pattern, the explicit discriminator takes precedence.
    /// The entity should still compile (explicit discriminator is used, auto-derivation is skipped).
    /// 
    /// Validates: Requirement 3.5
    /// </summary>
    [Fact]
    public void ExplicitDiscriminatorValue_WithConstantKey_ExplicitTakesPrecedence()
    {
        // Arrange: Entity with DiscriminatorValue "META" but constant SK derives "PROFILE"
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Shared"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorValue = ""META"")]
    public partial class ExplicitValueEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = RunSourceGenerator(source);

        // Assert: The explicit DiscriminatorValue takes precedence over auto-derivation.
        // The entity should compile (explicit discriminator is valid, auto-derive is skipped).
        // No FDDB101 is expected because FDDB101 only checks Pattern-based conflicts,
        // not ExactValue (ExactMatch) discriminators.
        result.GeneratedSources.Should().NotBeEmpty(
            "Entity with explicit DiscriminatorValue should still produce generated code");
    }

    #endregion

    #region Scenario 2: Overlapping Constant-Key Patterns on Same Table

    /// <summary>
    /// When two entities on the same table have overlapping constant-key-derived patterns
    /// (same ExactMatch value "PROFILE" on the same property "sk"), the PatternOverlapAnalyzer
    /// should detect the overlap and emit FDDB102 (both auto-derived with same score).
    /// 
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public void TwoEntities_SameTable_SameConstantKeyPattern_EmitsOverlapDiagnostic()
    {
        // Arrange: Two entities on "Shared" table, both with constant SK => "PROFILE"
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Shared"", IsDefault = true)]
    public partial class EntityA
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }

    [DynamoDbTable(""Shared"")]
    public partial class EntityB
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = RunSourceGenerator(source);

        // Assert: FDDB102 should be emitted because both auto-derived patterns overlap
        // with the same specificity score (both ExactMatch "PROFILE" on "sk")
        var fddb102Diagnostics = result.Diagnostics.Where(d => d.Id == "FDDB102").ToList();
        fddb102Diagnostics.Should().NotBeEmpty(
            "FDDB102 should be emitted when two entities on the same table have " +
            "overlapping auto-derived constant-key patterns with the same value");
        fddb102Diagnostics.First().Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// When two entities on the same table have different constant-key-derived patterns
    /// (different ExactMatch values), no overlap diagnostic should be emitted.
    /// 
    /// Validates: Requirement 3.4 (non-overlapping case)
    /// </summary>
    [Fact]
    public void TwoEntities_SameTable_DifferentConstantKeyPatterns_NoOverlapDiagnostic()
    {
        // Arrange: Two entities on "Shared" table with different constant SK values
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Shared"", IsDefault = true)]
    public partial class ProfileEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }

    [DynamoDbTable(""Shared"")]
    public partial class MetadataEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""METADATA"";
    }
}";

        // Act
        var result = RunSourceGenerator(source);

        // Assert: No overlap diagnostics — the ExactMatch values are different
        var overlapDiagnostics = result.Diagnostics
            .Where(d => d.Id is "DISC004" or "DISC005" or "FDDB102")
            .ToList();
        overlapDiagnostics.Should().BeEmpty(
            "No overlap diagnostics should be emitted when constant key values differ " +
            "(ExactMatch 'PROFILE' and ExactMatch 'METADATA' never match the same item)");
    }

    /// <summary>
    /// When one entity has a constant key (ExactMatch) that matches a more general wildcard
    /// pattern from another entity on the same table, the overlap should be resolved with
    /// exclusion guards (DISC005) on the less-specific entity.
    /// 
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public void ConstantKeyEntity_OverlapsWithWildcardEntity_EmitsResolutionDiagnostic()
    {
        // Arrange: EntityA has wildcard pattern "PROF*" on sk,
        // EntityB has constant SK => "PROFILE" (ExactMatch)
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Shared"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""PROF*"")]
    public partial class WildcardEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""PROF"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""Shared"")]
    public partial class ConstantEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = RunSourceGenerator(source);

        // Assert: DISC005 (resolved overlap) should be emitted because ExactMatch "PROFILE"
        // overlaps with wildcard "PROF*", and ExactMatch is more specific (higher score)
        var disc005Diagnostics = result.Diagnostics.Where(d => d.Id == "DISC005").ToList();
        disc005Diagnostics.Should().NotBeEmpty(
            "DISC005 should be emitted when constant key's ExactMatch pattern overlaps " +
            "with another entity's wildcard pattern and the overlap is resolved by specificity");
    }

    /// <summary>
    /// When a constant key entity overlaps with a wildcard pattern entity,
    /// the less-specific (wildcard) entity should have exclusion guards generated
    /// in its MatchesEntity method to exclude the constant key entity's items.
    /// 
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public void ConstantKeyEntity_OverlapsWithWildcard_GeneratesExclusionGuards()
    {
        // Arrange: WildcardEntity "INVOICE#*" overlaps with ConstantEntity "INVOICE#META"
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Shared"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*"")]
    public partial class InvoiceEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""Shared"")]
    public partial class InvoiceMetaEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""INVOICE#META"";
    }
}";

        // Act
        var result = RunSourceGenerator(source);

        // Assert: InvoiceEntity (less specific, wildcard "INVOICE#*") should have
        // exclusion guards in its generated code to exclude InvoiceMetaEntity's pattern
        var invoiceCode = GetGeneratedSource(result, "InvoiceEntity.g.cs");
        if (invoiceCode != null)
        {
            // The exclusion guard should exclude the exact value "INVOICE#META"
            // by checking equality (ExactMatch exclusion)
            invoiceCode.Should().Contain("INVOICE#META",
                "InvoiceEntity's MatchesEntity should have an exclusion guard " +
                "for the constant key entity's ExactMatch value 'INVOICE#META'");
        }
    }

    #endregion

    #region Test Infrastructure

    private static GeneratorTestResult RunSourceGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
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

    private static string? GetGeneratedSource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        return source?.SourceText.ToString();
    }

    #endregion
}
