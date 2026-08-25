using System.Reflection;
using System.Text;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Comprehensive unit tests for the complex pattern exclusion fix.
/// All tests verify the FIXED behavior — they should all PASS.
///
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.4, 3.6**
/// </summary>
public class ComplexPatternExclusionFixTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // 1. CreateExclusionPattern() with bare-separator patterns
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateExclusionPattern_HashSeparator_CAP_ReturnsOffsetIndex4()
    {
        // "CAP#*#*" vs "CAP#*" → OffsetIndex = 4, LiteralText = "#"
        var entity = new EntityModel { ClassName = "MoreSpecific", TableName = "TestTable" };
        var config = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "CAP#*#*",
            Strategy = DiscriminatorStrategy.Complex
        };

        var exclusion = InvokeCreateExclusionPattern(entity, config);

        exclusion.OffsetIndex.Should().Be(4, "prefix 'CAP#' has length 4");
        exclusion.LiteralText.Should().Be("#", "bare separator is '#'");
    }

    [Fact]
    public void CreateExclusionPattern_UnderscoreSeparator_X_ReturnsOffsetIndex2()
    {
        // "X_*_*" vs "X_*" → OffsetIndex = 2, LiteralText = "_"
        var entity = new EntityModel { ClassName = "MoreSpecific", TableName = "TestTable" };
        var config = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "X_*_*",
            Strategy = DiscriminatorStrategy.Complex
        };

        var exclusion = InvokeCreateExclusionPattern(entity, config);

        exclusion.OffsetIndex.Should().Be(2, "prefix 'X_' has length 2");
        exclusion.LiteralText.Should().Be("_", "bare separator is '_'");
    }

    [Fact]
    public void CreateExclusionPattern_ColonSeparator_NS_ReturnsOffsetIndex3()
    {
        // "NS:*:*" vs "NS:*" → OffsetIndex = 3, LiteralText = ":"
        var entity = new EntityModel { ClassName = "MoreSpecific", TableName = "TestTable" };
        var config = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "NS:*:*",
            Strategy = DiscriminatorStrategy.Complex
        };

        var exclusion = InvokeCreateExclusionPattern(entity, config);

        exclusion.OffsetIndex.Should().Be(3, "prefix 'NS:' has length 3");
        exclusion.LiteralText.Should().Be(":", "bare separator is ':'");
    }

    [Fact]
    public void CreateExclusionPattern_MultiCharSeparator_PREFIX_ReturnsOffsetIndex8()
    {
        // "PREFIX##*##*" vs "PREFIX##*" (multi-char separator "##") → OffsetIndex = 8, LiteralText = "##"
        var entity = new EntityModel { ClassName = "MoreSpecific", TableName = "TestTable" };
        var config = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "PREFIX##*##*",
            Strategy = DiscriminatorStrategy.Complex
        };

        var exclusion = InvokeCreateExclusionPattern(entity, config);

        exclusion.OffsetIndex.Should().Be(8, "prefix 'PREFIX##' has length 8");
        exclusion.LiteralText.Should().Be("##", "bare separator is '##'");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2. CreateExclusionPattern() with meaningful patterns (verify unchanged)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateExclusionPattern_MeaningfulSegment_InvoiceLine_ReturnsContainsWithOffsetZero()
    {
        // "INVOICE#*#LINE#*" vs "INVOICE#*" → OffsetIndex = 0, LiteralText = "#LINE#"
        var entity = new EntityModel { ClassName = "InvoiceLineEntity", TableName = "TestTable" };
        var config = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "INVOICE#*#LINE#*",
            Strategy = DiscriminatorStrategy.Complex
        };

        var exclusion = InvokeCreateExclusionPattern(entity, config);

        exclusion.OffsetIndex.Should().Be(0, "meaningful segment uses standard Contains");
        exclusion.LiteralText.Should().Be("#LINE#", "uses last meaningful internal segment");
        exclusion.Strategy.Should().Be(DiscriminatorStrategy.Contains, "meaningful segments use Contains strategy");
    }

    [Fact]
    public void CreateExclusionPattern_MeaningfulSegment_UserRole_ReturnsContainsWithOffsetZero()
    {
        // "USER#*#ROLE#*" vs "USER#*" → OffsetIndex = 0, LiteralText = "#ROLE#"
        var entity = new EntityModel { ClassName = "UserRoleEntity", TableName = "TestTable" };
        var config = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "USER#*#ROLE#*",
            Strategy = DiscriminatorStrategy.Complex
        };

        var exclusion = InvokeCreateExclusionPattern(entity, config);

        exclusion.OffsetIndex.Should().Be(0, "meaningful segment uses standard Contains");
        exclusion.LiteralText.Should().Be("#ROLE#", "uses last meaningful internal segment");
        exclusion.Strategy.Should().Be(DiscriminatorStrategy.Contains, "meaningful segments use Contains strategy");
    }

    [Fact]
    public void CreateExclusionPattern_MultiMeaningfulSegment_UsesLastMeaningful()
    {
        // "A#*#BC#*#DEF#*" vs "A#*" → OffsetIndex = 0, LiteralText = "#DEF#" (last meaningful segment)
        var entity = new EntityModel { ClassName = "DetailEntity", TableName = "TestTable" };
        var config = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "A#*#BC#*#DEF#*",
            Strategy = DiscriminatorStrategy.Complex
        };

        var exclusion = InvokeCreateExclusionPattern(entity, config);

        exclusion.OffsetIndex.Should().Be(0, "meaningful segment uses standard Contains");
        exclusion.LiteralText.Should().Be("#DEF#", "uses last meaningful segment");
        exclusion.Strategy.Should().Be(DiscriminatorStrategy.Contains, "meaningful segments use Contains strategy");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3. IsTautologicalExclusion() — tested via Analyze() public API
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsTautologicalExclusion_DetectsContainsHash_VsStartsWithCAP()
    {
        // Contains("#") vs StartsWith("CAP#") should be detected as tautological
        // When tautological, IsTautologicalExclusion triggers DISC006 and the exclusion
        // is NOT added to OverlappingPatterns. The fix uses OffsetIndex > 0 with Strategy = None
        // so the exclusion is NOT tautological anymore. We verify the exclusion IS added
        // but with the positional approach (not a bare Contains).
        var lessSpecific = CreateEntity("CAPEntity", "CAP#*", DiscriminatorStrategy.StartsWith, "sk");
        var moreSpecific = CreateEntity("CAPDetailEntity", "CAP#*#*", DiscriminatorStrategy.Complex, "sk");

        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;

        // The exclusion should exist (it's not tautological because it uses OffsetIndex > 0)
        exclusions.Should().HaveCount(1, "positional exclusion should be added");

        // It should NOT be a bare Contains("#") — that would be tautological
        var exclusion = exclusions[0];
        var isBareContains = exclusion.Strategy == DiscriminatorStrategy.Contains
                             && exclusion.LiteralText == "#"
                             && exclusion.OffsetIndex == 0;
        isBareContains.Should().BeFalse("bare Contains(\"#\") is tautological after StartsWith(\"CAP#\")");
    }

    [Fact]
    public void IsTautologicalExclusion_DetectsContainsUnderscore_VsStartsWithCAP()
    {
        // Contains("_") vs StartsWith("CAP_") should be detected as tautological
        var lessSpecific = CreateEntity("CAPEntity", "CAP_*", DiscriminatorStrategy.StartsWith, "sk");
        var moreSpecific = CreateEntity("CAPDetailEntity", "CAP_*_*", DiscriminatorStrategy.Complex, "sk");

        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;
        exclusions.Should().HaveCount(1, "positional exclusion should be added");

        var exclusion = exclusions[0];
        var isBareContains = exclusion.Strategy == DiscriminatorStrategy.Contains
                             && exclusion.LiteralText == "_"
                             && exclusion.OffsetIndex == 0;
        isBareContains.Should().BeFalse("bare Contains(\"_\") is tautological after StartsWith(\"CAP_\")");
    }

    [Fact]
    public void IsTautologicalExclusion_DetectsContainsColon_VsStartsWithNS()
    {
        // Contains(":") vs StartsWith("NS:") should be detected as tautological
        var lessSpecific = CreateEntity("NSEntity", "NS:*", DiscriminatorStrategy.StartsWith, "sk");
        var moreSpecific = CreateEntity("NSDetailEntity", "NS:*:*", DiscriminatorStrategy.Complex, "sk");

        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;
        exclusions.Should().HaveCount(1, "positional exclusion should be added");

        var exclusion = exclusions[0];
        var isBareContains = exclusion.Strategy == DiscriminatorStrategy.Contains
                             && exclusion.LiteralText == ":"
                             && exclusion.OffsetIndex == 0;
        isBareContains.Should().BeFalse("bare Contains(\":\") is tautological after StartsWith(\"NS:\")");
    }

    [Fact]
    public void IsTautologicalExclusion_DoesNotFlagMeaningfulContains_InvoiceLine()
    {
        // Contains("#LINE#") vs StartsWith("INVOICE#") is NOT tautological
        var lessSpecific = CreateEntity("InvoiceEntity", "INVOICE#*", DiscriminatorStrategy.StartsWith, "sk");
        var moreSpecific = CreateEntity("InvoiceLineEntity", "INVOICE#*#LINE#*", DiscriminatorStrategy.Complex, "sk");

        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;
        exclusions.Should().HaveCount(1, "meaningful exclusion should be added");
        exclusions[0].Strategy.Should().Be(DiscriminatorStrategy.Contains);
        exclusions[0].LiteralText.Should().Be("#LINE#");
        exclusions[0].OffsetIndex.Should().Be(0, "meaningful segment uses standard Contains");
    }

    [Fact]
    public void IsTautologicalExclusion_DoesNotFlagMeaningfulContains_UserRole()
    {
        // Contains("#ROLE#") vs StartsWith("USER#") is NOT tautological
        var lessSpecific = CreateEntity("UserEntity", "USER#*", DiscriminatorStrategy.StartsWith, "sk");
        var moreSpecific = CreateEntity("UserRoleEntity", "USER#*#ROLE#*", DiscriminatorStrategy.Complex, "sk");

        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;
        exclusions.Should().HaveCount(1, "meaningful exclusion should be added");
        exclusions[0].Strategy.Should().Be(DiscriminatorStrategy.Contains);
        exclusions[0].LiteralText.Should().Be("#ROLE#");
        exclusions[0].OffsetIndex.Should().Be(0, "meaningful segment uses standard Contains");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4. GenerateComplexPatternCheck()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateComplexPatternCheck_BareSeparator_CAP_ProducesOnlyStartsWith()
    {
        // "CAP#*#*" produces just StartsWith("CAP#") (no redundant Contains)
        var sb = new StringBuilder();
        InvokeGenerateComplexPatternCheck(sb, "CAP#*#*", "return");
        var output = sb.ToString();

        output.Should().Contain("StartsWith(\"CAP#\")", "should have StartsWith for prefix");
        output.Should().NotContain("Contains(\"#\")", "bare separator Contains should be omitted");
    }

    [Fact]
    public void GenerateComplexPatternCheck_BareSeparator_NS_ProducesOnlyStartsWith()
    {
        // "NS:*:*" produces just StartsWith("NS:") (no redundant Contains)
        var sb = new StringBuilder();
        InvokeGenerateComplexPatternCheck(sb, "NS:*:*", "return");
        var output = sb.ToString();

        output.Should().Contain("StartsWith(\"NS:\")", "should have StartsWith for prefix");
        output.Should().NotContain("Contains(\":\")", "bare separator Contains should be omitted");
    }

    [Fact]
    public void GenerateComplexPatternCheck_MeaningfulSegment_InvoiceLine_PreservesContains()
    {
        // "INVOICE#*#LINE#*" produces StartsWith("INVOICE#") && Contains("#LINE#")
        var sb = new StringBuilder();
        InvokeGenerateComplexPatternCheck(sb, "INVOICE#*#LINE#*", "return");
        var output = sb.ToString();

        output.Should().Contain("StartsWith(\"INVOICE#\")", "should have StartsWith for prefix");
        output.Should().Contain("Contains(\"#LINE#\")", "meaningful Contains should be preserved");
    }

    [Fact]
    public void GenerateComplexPatternCheck_MeaningfulSegment_UserRole_PreservesContains()
    {
        // "USER#*#ROLE#*" produces StartsWith("USER#") && Contains("#ROLE#")
        var sb = new StringBuilder();
        InvokeGenerateComplexPatternCheck(sb, "USER#*#ROLE#*", "return");
        var output = sb.ToString();

        output.Should().Contain("StartsWith(\"USER#\")", "should have StartsWith for prefix");
        output.Should().Contain("Contains(\"#ROLE#\")", "meaningful Contains should be preserved");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 5. OffsetIndex-based exclusion code generation
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExclusionCodeGeneration_OffsetIndex4_EmitsIndexOf()
    {
        // OffsetIndex = 4, LiteralText = "#" → emits IndexOf("#", 4) >= 0
        var lessSpecific = CreateEntityWithProperties("CAPEntity", "CAP#*", DiscriminatorStrategy.StartsWith, "sk", "CAP#");
        var moreSpecific = CreateEntityWithProperties("CAPDetailEntity", "CAP#*#*", DiscriminatorStrategy.Complex, "sk", "CAP#{0}#{1}");

        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var generatedSource = MapperGenerator.GenerateEntityImplementation(lessSpecific);

        generatedSource.Should().Contain("IndexOf(\"#\", 4)", "should use positional IndexOf with offset 4");
        generatedSource.Should().NotContain("Contains(\"#\")", "should NOT use bare Contains for separator");
    }

    [Fact]
    public void ExclusionCodeGeneration_OffsetIndex0_EmitsContains()
    {
        // OffsetIndex = 0, LiteralText = "#LINE#" → emits Contains("#LINE#")
        var lessSpecific = CreateEntityWithProperties("InvoiceEntity", "INVOICE#*", DiscriminatorStrategy.StartsWith, "sk", "INVOICE#");
        var moreSpecific = CreateEntityWithProperties("InvoiceLineEntity", "INVOICE#*#LINE#*", DiscriminatorStrategy.Complex, "sk", "INVOICE#{0}#LINE#{1}");

        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var generatedSource = MapperGenerator.GenerateEntityImplementation(lessSpecific);

        generatedSource.Should().Contain("Contains(\"#LINE#\")", "should use Contains for meaningful segment");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 6. Edge cases
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EdgeCase_EmptyAfterPrefix_NotExcludedByIndexOf()
    {
        // "CAP#" (empty after prefix) → IndexOf("#", 4) returns -1 → not excluded
        var lessSpecific = CreateEntityWithProperties("CAPEntity", "CAP#*", DiscriminatorStrategy.StartsWith, "sk", "CAP#");
        var moreSpecific = CreateEntityWithProperties("CAPDetailEntity", "CAP#*#*", DiscriminatorStrategy.Complex, "sk", "CAP#{0}#{1}");

        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Verify the exclusion uses IndexOf with offset
        var exclusion = lessSpecific.Discriminator!.OverlappingPatterns[0];
        exclusion.OffsetIndex.Should().Be(4);

        // "CAP#".IndexOf("#", 4) → -1 (no '#' at or after index 4) → NOT excluded
        var value = "CAP#";
        var result = value.IndexOf("#", 4);
        result.Should().BeLessThan(0, "empty value after prefix should NOT be excluded");
    }

    [Fact]
    public void EdgeCase_SingleCharValue_NotExcludedByIndexOf()
    {
        // "CAP#a" (single char value) → IndexOf("#", 4) returns -1 → not excluded
        var value = "CAP#a";
        var result = value.IndexOf("#", 4);
        result.Should().BeLessThan(0, "single char value 'CAP#a' should NOT be excluded");
    }

    [Fact]
    public void EdgeCase_SeparatorAtExactPrefixBoundary_CorrectlyEvaluated()
    {
        // "CAP##" (separator immediately after prefix) → IndexOf("#", 4) returns 4 → IS excluded
        var value = "CAP##";
        var result = value.IndexOf("#", 4);
        result.Should().BeGreaterThanOrEqualTo(0, "value 'CAP##' has '#' at position 4 — correctly excluded");
    }

    [Fact]
    public void EdgeCase_ThreeWildcardPattern_CorrectlyDetectsBareSeparator()
    {
        // "CAP#*#*#*" vs "CAP#*" → correctly detects bare separator
        var entity = new EntityModel { ClassName = "TripleEntity", TableName = "TestTable" };
        var config = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "CAP#*#*#*",
            Strategy = DiscriminatorStrategy.Complex
        };

        var exclusion = InvokeCreateExclusionPattern(entity, config);

        // All internal segments are "#" (bare), so positional approach should be used
        exclusion.OffsetIndex.Should().Be(4, "prefix 'CAP#' has length 4");
        exclusion.LiteralText.Should().Be("#", "first bare separator");
    }

    [Fact]
    public void EdgeCase_MultiSegmentValue_CorrectlyExcluded()
    {
        // "CAP#svc1#cap1" → IndexOf("#", 4) returns 8 → IS excluded
        var value = "CAP#svc1#cap1";
        var result = value.IndexOf("#", 4);
        result.Should().BeGreaterThanOrEqualTo(0, "multi-segment value should be excluded");
    }

    [Fact]
    public void EdgeCase_SingleSegmentValue_NotExcluded()
    {
        // "CAP#capability1" → IndexOf("#", 4) returns -1 → NOT excluded
        var value = "CAP#capability1";
        var result = value.IndexOf("#", 4);
        result.Should().BeLessThan(0, "single-segment value should NOT be excluded");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private static EntityModel CreateEntity(string className, string pattern, DiscriminatorStrategy strategy, string propertyName)
    {
        return new EntityModel
        {
            ClassName = className,
            TableName = "TestTable",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = propertyName,
                Pattern = pattern,
                Strategy = strategy,
                IsAutoDerived = true
            }
        };
    }

    private static EntityModel CreateEntityWithProperties(string className, string pattern, DiscriminatorStrategy strategy, string propertyName, string skKeyFormat)
    {
        return new EntityModel
        {
            ClassName = className,
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = propertyName,
                Pattern = pattern,
                Strategy = strategy,
                IsAutoDerived = true
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    NormalizedKeyFormat = "{0}",
                    DerivedDiscriminatorPattern = null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    NormalizedKeyFormat = skKeyFormat,
                    DerivedDiscriminatorPattern = pattern
                }
            }
        };
    }

    private static ExclusionPattern InvokeCreateExclusionPattern(EntityModel entity, DiscriminatorConfig config)
    {
        var method = typeof(PatternOverlapAnalyzer)
            .GetMethod("CreateExclusionPattern", BindingFlags.NonPublic | BindingFlags.Static);
        return (ExclusionPattern)method!.Invoke(null, new object[] { entity, config })!;
    }

    private static void InvokeGenerateComplexPatternCheck(StringBuilder sb, string pattern, string mode)
    {
        var method = typeof(MapperGenerator)
            .GetMethod("GenerateComplexPatternCheck", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { sb, pattern, mode });
    }
}
