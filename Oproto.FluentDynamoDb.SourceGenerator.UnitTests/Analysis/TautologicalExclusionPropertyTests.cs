using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for tautological exclusion guard detection in PatternOverlapAnalyzer.
///
/// Feature: tautological-exclusion-guard-detection
/// **Validates: Requirements 2.1, 2.2, 3.1, 3.4**
/// </summary>
[Trait("Category", "PropertyBased")]
public class TautologicalExclusionPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 1: Contains parent + Complex child with same segment → DISC006 always
    // **Validates: Requirements 2.1, 2.2**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any Contains-strategy entity with pattern *#SEGMENT#* overlapping a Complex-strategy
    /// entity with pattern PREFIX#*#SEGMENT#*, the analyzer MUST emit DISC006 (tautological
    /// exclusion detected) and MUST NOT populate OverlappingPatterns on the Contains entity.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ContainsParent_ComplexChild_SameSegment_AlwaysTautological()
    {
        var testCaseGen = Gen.Elements("USER", "ORDER", "EMPLOYEE", "CUSTOMER", "PRODUCT", "ITEM")
            .Select(p => p + "#")
            .SelectMany(prefix =>
                Gen.Elements("ROLE", "TAG", "NOTE", "DEDUCTION", "STATUS", "META", "DETAIL")
                    .Select(s => "#" + s + "#")
                    .Select(segment => (prefix, segment)));

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            tc =>
            {
                // Contains parent: *#SEGMENT#*
                var configA = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = "*" + tc.segment + "*",
                    Strategy = DiscriminatorStrategy.Contains
                };

                // Complex child: PREFIX#*#SEGMENT#*
                var configB = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = tc.prefix + "*" + tc.segment + "*",
                    Strategy = DiscriminatorStrategy.Complex
                };

                var entityA = new EntityModel
                {
                    ClassName = "ParentEntity",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "ChildEntity",
                    TableName = "test-table",
                    Discriminator = configB
                };

                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();

                var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

                var disc006 = diagnostics.Where(d => d.Id == "DISC006").ToList();
                return (disc006.Count == 1 && configA.OverlappingPatterns.Count == 0)
                    .Label($"Expected DISC006=1, got {disc006.Count}; OverlappingPatterns={configA.OverlappingPatterns.Count}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2: StartsWith parent + Complex child → DISC006 never
    // **Validates: Requirements 3.1**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any StartsWith-strategy entity with pattern PREFIX#* overlapping a Complex-strategy
    /// entity with pattern PREFIX#*#SEGMENT#*, the analyzer MUST NOT emit DISC006. Instead it
    /// should emit DISC005 (valid resolved overlap) and populate OverlappingPatterns with
    /// exactly one exclusion.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StartsWithParent_ComplexChild_NeverTautological()
    {
        var testCaseGen = Gen.Elements("USER", "ORDER", "EMPLOYEE", "CUSTOMER", "PRODUCT", "ITEM")
            .Select(p => p + "#")
            .SelectMany(prefix =>
                Gen.Elements("ROLE", "TAG", "NOTE", "DEDUCTION", "STATUS", "META", "DETAIL")
                    .Select(s => "#" + s + "#")
                    .Select(segment => (prefix, segment)));

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            tc =>
            {
                // StartsWith parent: PREFIX#*
                var configA = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = tc.prefix + "*",
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                // Complex child: PREFIX#*#SEGMENT#*
                var configB = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = tc.prefix + "*" + tc.segment + "*",
                    Strategy = DiscriminatorStrategy.Complex
                };

                var entityA = new EntityModel
                {
                    ClassName = "ParentEntity",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "ChildEntity",
                    TableName = "test-table",
                    Discriminator = configB
                };

                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();

                var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

                var disc006 = diagnostics.Where(d => d.Id == "DISC006").ToList();
                var disc005 = diagnostics.Where(d => d.Id == "DISC005").ToList();

                return (disc006.Count == 0 && disc005.Count == 1 && configA.OverlappingPatterns.Count == 1)
                    .Label($"Expected DISC006=0, DISC005=1, Overlaps=1; got DISC006={disc006.Count}, DISC005={disc005.Count}, Overlaps={configA.OverlappingPatterns.Count}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 3: Non-overlapping patterns → zero DISC006, zero diagnostics
    // **Validates: Requirements 3.4**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any two StartsWith entities with different prefixes (non-overlapping), the analyzer
    /// MUST NOT emit any diagnostics (including DISC006) and MUST NOT populate any OverlappingPatterns.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonOverlappingPatterns_NeverEmitDISC006()
    {
        var prefixPool = new[] { "USER", "ORDER", "EMPLOYEE", "CUSTOMER", "PRODUCT", "ITEM" };

        var testCaseGen = Gen.Choose(0, prefixPool.Length - 1)
            .SelectMany(idxA =>
                Gen.Choose(0, prefixPool.Length - 1)
                    .Where(idxB => idxB != idxA)
                    .Select(idxB => (prefixA: prefixPool[idxA] + "#", prefixB: prefixPool[idxB] + "#")));

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            tc =>
            {
                // Entity A: PREFIX_A#*
                var configA = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = tc.prefixA + "*",
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                // Entity B: PREFIX_B#*
                var configB = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = tc.prefixB + "*",
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var entityA = new EntityModel
                {
                    ClassName = "EntityA",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "EntityB",
                    TableName = "test-table",
                    Discriminator = configB
                };

                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();

                var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

                var disc006 = diagnostics.Where(d => d.Id == "DISC006").ToList();

                return (disc006.Count == 0 && diagnostics.Count == 0)
                    .Label($"Expected zero diagnostics; got DISC006={disc006.Count}, total={diagnostics.Count}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 4: Valid hierarchy → exclusion populated exactly once with correct properties
    // **Validates: Requirements 3.1**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid StartsWith parent + Complex child hierarchy, the exclusion pattern should be
    /// populated exactly once, its LiteralText must contain the SEGMENT text, its Strategy must be
    /// Contains, and its LiteralText must differ from the parent's positive match literal.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidHierarchy_ExclusionPopulated_ExactlyOnce()
    {
        var testCaseGen = Gen.Elements("USER", "ORDER", "EMPLOYEE", "CUSTOMER", "PRODUCT", "ITEM")
            .Select(p => p + "#")
            .SelectMany(prefix =>
                Gen.Elements("ROLE", "TAG", "NOTE", "DEDUCTION", "STATUS", "META", "DETAIL")
                    .Select(s => "#" + s + "#")
                    .Select(segment => (prefix, segment)));

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            tc =>
            {
                // StartsWith parent: PREFIX#*
                var configA = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = tc.prefix + "*",
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                // Complex child: PREFIX#*#SEGMENT#*
                var configB = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = tc.prefix + "*" + tc.segment + "*",
                    Strategy = DiscriminatorStrategy.Complex
                };

                var entityA = new EntityModel
                {
                    ClassName = "ParentEntity",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "ChildEntity",
                    TableName = "test-table",
                    Discriminator = configB
                };

                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();

                PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

                var overlaps = configA.OverlappingPatterns;
                if (overlaps.Count != 1)
                    return false.Label($"Expected OverlappingPatterns.Count=1, got {overlaps.Count}");

                var exclusion = overlaps[0];
                var positiveLiteral = tc.prefix; // e.g. "USER#"

                var containsSegment = exclusion.LiteralText.Contains(tc.segment.Trim('#'));
                var strategyIsContains = exclusion.Strategy == DiscriminatorStrategy.Contains;
                var literalDiffersFromPositive = !string.Equals(exclusion.LiteralText, positiveLiteral, StringComparison.Ordinal);

                return (containsSegment && strategyIsContains && literalDiffersFromPositive)
                    .Label($"Exclusion: Strategy={exclusion.Strategy}, LiteralText='{exclusion.LiteralText}', PositiveLiteral='{positiveLiteral}', Segment='{tc.segment}'");
            });
    }
}
