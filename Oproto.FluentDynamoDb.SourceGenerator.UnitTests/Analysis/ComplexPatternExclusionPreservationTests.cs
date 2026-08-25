using System.Reflection;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Preservation property tests for Complex pattern exclusion behavior.
///
/// These tests observe and capture the CURRENT behavior of the unfixed code for
/// patterns with meaningful internal segments (NOT bare-separator segments).
/// These tests MUST PASS on unfixed code, confirming that the baseline behavior
/// is correctly preserved after the fix is applied.
///
/// For all patterns where the internal segments are meaningful (longer than separator
/// and not contained in prefix), the exclusion behavior must remain unchanged.
///
/// **Validates: Requirements 3.1, 3.4, 3.6**
/// </summary>
[Trait("Category", "Preservation")]
public class ComplexPatternExclusionPreservationTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Observation 1: INVOICE#*#LINE#* produces Contains("#LINE#")
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: CreateExclusionPattern() for "INVOICE#*#LINE#*" vs "INVOICE#*"
    /// returns {Strategy: Contains, LiteralText: "#LINE#"} on unfixed code.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property MeaningfulSegment_InvoiceLine_ProducesContainsLineLiteral()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var lessSpecific = CreateEntity("InvoiceEntity", "INVOICE#*", DiscriminatorStrategy.StartsWith, "sk");
                var moreSpecific = CreateEntity("InvoiceLineEntity", "INVOICE#*#LINE#*", DiscriminatorStrategy.Complex, "sk");

                PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

                var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;

                return (exclusions.Count == 1).Label("one exclusion added")
                    .And((exclusions[0].Strategy == DiscriminatorStrategy.Contains).Label("strategy is Contains"))
                    .And((exclusions[0].LiteralText == "#LINE#").Label("literal text is #LINE#"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 2: USER#*#ROLE#* produces Contains("#ROLE#")
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: CreateExclusionPattern() for "USER#*#ROLE#*" vs "USER#*"
    /// returns {Strategy: Contains, LiteralText: "#ROLE#"} on unfixed code.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property MeaningfulSegment_UserRole_ProducesContainsRoleLiteral()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var lessSpecific = CreateEntity("UserEntity", "USER#*", DiscriminatorStrategy.StartsWith, "sk");
                var moreSpecific = CreateEntity("UserRoleEntity", "USER#*#ROLE#*", DiscriminatorStrategy.Complex, "sk");

                PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

                var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;

                return (exclusions.Count == 1).Label("one exclusion added")
                    .And((exclusions[0].Strategy == DiscriminatorStrategy.Contains).Label("strategy is Contains"))
                    .And((exclusions[0].LiteralText == "#ROLE#").Label("literal text is #ROLE#"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 3: A#*#BC#*#DEF#* uses "#DEF#" (last meaningful segment)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: CreateExclusionPattern() for "A#*#BC#*#DEF#*" vs "A#*"
    /// uses "#DEF#" (last meaningful segment) on unfixed code.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property MultiMeaningfulSegment_UsesLastSegment()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var lessSpecific = CreateEntity("AEntity", "A#*", DiscriminatorStrategy.StartsWith, "sk");
                var moreSpecific = CreateEntity("ADetailEntity", "A#*#BC#*#DEF#*", DiscriminatorStrategy.Complex, "sk");

                PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

                var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;

                return (exclusions.Count == 1).Label("one exclusion added")
                    .And((exclusions[0].Strategy == DiscriminatorStrategy.Contains).Label("strategy is Contains"))
                    .And((exclusions[0].LiteralText == "#DEF#").Label("literal text is #DEF# (last meaningful segment)"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 4: ExactMatch strategy patterns return exact value
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: ExactMatch strategy patterns continue to return exact value on unfixed code.
    /// When a more-specific entity uses ExactMatch, the exclusion should use ExactMatch
    /// strategy with the exact value as LiteralText.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExactMatchExclusion_ReturnsExactValue()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                // ExactMatch "ORDER#SPECIAL" overlaps with StartsWith "ORDER#*" because
                // ExactValueMatchesPattern checks "ORDER#SPECIAL".StartsWith("ORDER#") → true
                // ExactMatch has score int.MaxValue > StartsWith score 1
                var lessSpecific = CreateEntity("OrderEntity", "ORDER#*", DiscriminatorStrategy.StartsWith, "sk");
                var moreSpecific = CreateExactMatchEntity("SpecialOrderEntity", "ORDER#SPECIAL", "sk");

                PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

                var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;

                return (exclusions.Count == 1).Label("one exclusion added")
                    .And((exclusions[0].Strategy == DiscriminatorStrategy.ExactMatch).Label("strategy is ExactMatch"))
                    .And((exclusions[0].LiteralText == "ORDER#SPECIAL").Label("literal text is exact value"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 5: Non-Complex strategy patterns delegate to GetPatternText()
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: Non-Complex strategy patterns (StartsWith) delegate to GetPatternText()
    /// on unfixed code. When a more-specific entity uses a non-Complex pattern strategy
    /// and has a higher specificity score, the exclusion uses the same strategy and 
    /// GetPatternText result.
    ///
    /// Since two StartsWith patterns always have the same specificity score (1),
    /// we test the delegation by using a Contains pattern (score 1) vs a Complex pattern
    /// (score 2+) where the Complex pattern creates a Contains exclusion from internal segments.
    /// 
    /// For a more direct test of the delegation path, we use reflection to invoke 
    /// CreateExclusionPattern directly with a StartsWith config.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NonComplexStrategy_StartsWith_DelegatesToGetPatternText()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                // Test via reflection: CreateExclusionPattern with StartsWith strategy
                // should return {Strategy: StartsWith, LiteralText: GetPatternText result}
                var config = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = "ORDER#DETAIL#*",
                    Strategy = DiscriminatorStrategy.StartsWith
                };
                var entity = new EntityModel { ClassName = "DetailEntity", TableName = "TestTable" };

                var exclusion = InvokeCreateExclusionPattern(entity, config);

                // GetPatternText("ORDER#DETAIL#*", StartsWith) → "ORDER#DETAIL#"
                return (exclusion.Strategy == DiscriminatorStrategy.StartsWith).Label("strategy is StartsWith")
                    .And((exclusion.LiteralText == "ORDER#DETAIL#").Label("literal is GetPatternText result"));
            });
    }

    /// <summary>
    /// Observation: EndsWith strategy patterns delegate to GetPatternText() on unfixed code.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NonComplexStrategy_EndsWith_DelegatesToGetPatternText()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                // Test via reflection: CreateExclusionPattern with EndsWith strategy
                var config = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = "*#DETAIL#AUDIT",
                    Strategy = DiscriminatorStrategy.EndsWith
                };
                var entity = new EntityModel { ClassName = "DetailAuditEntity", TableName = "TestTable" };

                var exclusion = InvokeCreateExclusionPattern(entity, config);

                // GetPatternText("*#DETAIL#AUDIT", EndsWith) → "#DETAIL#AUDIT"
                return (exclusion.Strategy == DiscriminatorStrategy.EndsWith).Label("strategy is EndsWith")
                    .And((exclusion.LiteralText == "#DETAIL#AUDIT").Label("literal is GetPatternText result"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 6: GenerateComplexPatternCheck for "INVOICE#*#LINE#*"
    //   produces StartsWith("INVOICE#") && Contains("#LINE#")
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck() for "INVOICE#*#LINE#*" produces
    /// StartsWith("INVOICE#") && Contains("#LINE#") on unfixed code.
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property GenerateComplexPatternCheck_MeaningfulSegment_ProducesStartsWithAndContains()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "INVOICE#*#LINE#*", "return");
                var output = sb.ToString();

                return output.Contains("StartsWith(\"INVOICE#\")").Label("contains StartsWith INVOICE#")
                    .And(output.Contains("Contains(\"#LINE#\")").Label("contains Contains #LINE#"));
            });
    }

    /// <summary>
    /// Observation: GenerateComplexPatternCheck() for "USER#*#ROLE#*" produces
    /// StartsWith("USER#") && Contains("#ROLE#") on unfixed code.
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property GenerateComplexPatternCheck_UserRole_ProducesStartsWithAndContains()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "USER#*#ROLE#*", "return");
                var output = sb.ToString();

                return output.Contains("StartsWith(\"USER#\")").Label("contains StartsWith USER#")
                    .And(output.Contains("Contains(\"#ROLE#\")").Label("contains Contains #ROLE#"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property-based test: Meaningful segments always produce Contains
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For all patterns with meaningful internal segments (segments longer than
    /// separator and not contained in prefix), exclusion uses Contains(segment) unchanged.
    /// 
    /// We generate patterns of the form "PREFIX#*#SEGMENT#*" where SEGMENT has length > 1
    /// and is NOT just "#".
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MeaningfulSegments_AlwaysProduceContainsExclusion()
    {
        // Generate meaningful segment names (2-8 chars, uppercase letters)
        var meaningfulSegmentGen = Gen.ArrayOf(
            Gen.Choose(2, 8).SelectMany(len =>
                Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P'))
                    .Select(chars => new string(chars))));

        // Generate prefix names (2-6 chars, uppercase letters)
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P'))
                .Select(chars => new string(chars)));

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                Gen.Choose(1, 3).SelectMany(segCount =>
                    Gen.ArrayOf(segCount, Gen.Choose(2, 8).SelectMany(len =>
                        Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P'))
                            .Select(chars => new string(chars))))
                    .Select(segments => (prefix, segments)))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, segments) = tuple;

            // Build pattern like "PREFIX#*#SEG1#*" or "PREFIX#*#SEG1#*#SEG2#*"
            var moreSpecificPattern = prefix + "#*" + string.Join("", segments.Select(s => "#" + s + "#*"));
            var lessSpecificPattern = prefix + "#*";

            var lessSpecific = CreateEntity("LessEntity", lessSpecificPattern, DiscriminatorStrategy.StartsWith, "sk");
            var moreSpecific = CreateEntity("MoreEntity", moreSpecificPattern, DiscriminatorStrategy.Complex, "sk");

            PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

            var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;

            // The last meaningful segment should be used as Contains literal
            var expectedLiteral = "#" + segments[segments.Length - 1] + "#";

            return (exclusions.Count == 1).Label("one exclusion added")
                .And((exclusions[0].Strategy == DiscriminatorStrategy.Contains).Label("strategy is Contains"))
                .And((exclusions[0].LiteralText == expectedLiteral).Label($"literal is '{expectedLiteral}' (got '{exclusions[0].LiteralText}')"));
        });
    }

    /// <summary>
    /// Property: For all non-Complex strategy patterns (StartsWith), the exclusion
    /// uses the same strategy and delegates to GetPatternText() for literal extraction.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property NonComplexStartsWith_DelegatesToGetPatternText()
    {
        // Generate prefix strings for StartsWith patterns
        var prefixGen = Gen.Choose(2, 8).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        var arb = Arb.From(
            prefixGen.SelectMany(basePrefix =>
                prefixGen.Select(extension => (basePrefix, extension))));

        return Prop.ForAll(arb, tuple =>
        {
            var (basePrefix, extension) = tuple;

            // Pattern: "BASE#EXT#*" with StartsWith strategy
            var pattern = basePrefix + "#" + extension + "#*";

            var config = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = pattern,
                Strategy = DiscriminatorStrategy.StartsWith
            };
            var entity = new EntityModel { ClassName = "TestEntity", TableName = "TestTable" };

            var exclusion = InvokeCreateExclusionPattern(entity, config);
            var expectedLiteral = DiscriminatorAnalyzer.GetPatternText(pattern, DiscriminatorStrategy.StartsWith);

            return (exclusion.Strategy == DiscriminatorStrategy.StartsWith).Label("strategy is StartsWith")
                .And((exclusion.LiteralText == expectedLiteral).Label($"literal is '{expectedLiteral}' (got '{exclusion.LiteralText}')"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

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
                Strategy = strategy
            }
        };
    }

    private static EntityModel CreateExactMatchEntity(string className, string exactValue, string propertyName)
    {
        return new EntityModel
        {
            ClassName = className,
            TableName = "TestTable",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = propertyName,
                ExactValue = exactValue,
                Strategy = DiscriminatorStrategy.ExactMatch
            }
        };
    }

    private static void InvokeGenerateComplexPatternCheck(StringBuilder sb, string pattern, string mode)
    {
        var method = typeof(MapperGenerator)
            .GetMethod("GenerateComplexPatternCheck", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { sb, pattern, mode });
    }

    private static ExclusionPattern InvokeCreateExclusionPattern(EntityModel entity, DiscriminatorConfig config)
    {
        var method = typeof(PatternOverlapAnalyzer)
            .GetMethod("CreateExclusionPattern", BindingFlags.NonPublic | BindingFlags.Static);
        return (ExclusionPattern)method!.Invoke(null, new object[] { entity, config })!;
    }
}
