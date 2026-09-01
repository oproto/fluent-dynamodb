using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Preservation property tests for the compound-discrimination-prefix-subsumption bugfix.
/// These tests verify that existing behavior is preserved for inputs that do NOT
/// involve the bug conditions (prefix subsumption, ExactMatch vs Complex, spurious FDDB102).
///
/// All tests are written against UNFIXED code using observation-first methodology
/// and should PASS on unfixed code to confirm baseline behavior to preserve.
///
/// Feature: compound-discrimination-prefix-subsumption
/// </summary>
[Trait("Category", "Preservation")]
[Trait("Feature", "compound-discrimination-prefix-subsumption")]
public class PrefixSubsumptionPreservationTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Preservation 1 — Non-Subsumptive Prefix Pairs (Req 3.1, 3.7)
    //
    // For any pair of entities where CompoundPromotionPass assigns dual
    // positive StartsWith compound constraints and neither entity's
    // LiteralText is a prefix of the other's (or they are identical),
    // no exclusion guards are added. Both entities receive positive
    // StartsWith compound constraints.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 3.1, 3.7**
    ///
    /// When two entities share the same SK discriminator pattern and have
    /// non-subsumptive PK prefixes (neither is a prefix of the other, and
    /// they are not identical), both receive positive StartsWith compound
    /// constraints with no exclusion guards (no AdditionalExclusions).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonSubsumptivePrefixPairs_BothGetPositiveConstraints_NoExclusionGuards()
    {
        return Prop.ForAll(
            GenNonSubsumptivePrefixPair().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Pair should be resolved (both have different non-null cross-key patterns)
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;

                // Both should have constraints assigned
                var bothHaveConstraints = constraintA != null && constraintB != null;
                if (!bothHaveConstraints)
                    return false.Label("Both entities should have CompoundConstraint assigned");

                // Both should be positive (not exclusion)
                var bothPositive = !constraintA!.IsExclusion && !constraintB!.IsExclusion;

                // Both should be StartsWith strategy
                var bothStartsWith = constraintA.Strategy == DiscriminatorStrategy.StartsWith
                    && constraintB.Strategy == DiscriminatorStrategy.StartsWith;

                // No exclusion guards should be present
                var noExclusionGuardsA = constraintA.AdditionalExclusions == null
                    || constraintA.AdditionalExclusions.Count == 0;
                var noExclusionGuardsB = constraintB.AdditionalExclusions == null
                    || constraintB.AdditionalExclusions.Count == 0;

                return (isResolved && bothPositive && bothStartsWith && noExclusionGuardsA && noExclusionGuardsB)
                    .Label($"Non-subsumptive prefix pair should have dual positive StartsWith constraints " +
                           $"with no exclusion guards. A.Literal='{constraintA.LiteralText}', " +
                           $"B.Literal='{constraintB.LiteralText}'");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Preservation 2 — One-Null-One-NonNull Cross-Key (Req 3.2)
    //
    // When one entity has a non-null PK pattern and the other has null,
    // the non-null entity gets a positive constraint and the null entity
    // gets an exclusion guard.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 3.2**
    ///
    /// When two entities share the same SK discriminator pattern and one has
    /// a non-null PK prefix while the other has null, the non-null entity gets
    /// a positive constraint and the null entity gets an exclusion guard.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OneNullOneNonNull_PositiveAndExclusion_Preserved()
    {
        return Prop.ForAll(
            GenOneNullOneNonNullPair().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Pair should be resolved
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                // EntityA (non-null cross-key) should have positive constraint
                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var aIsPositive = constraintA != null && !constraintA.IsExclusion;

                // EntityB (null cross-key) should have exclusion constraint
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;
                var bIsExclusion = constraintB != null && constraintB.IsExclusion;

                // EntityB's exclusion should reference EntityA's cross-key pattern
                var bSourceCorrect = constraintB != null
                    && constraintB.ExclusionSourceEntity == pair.EntityA.ClassName;

                return (isResolved && aIsPositive && bIsExclusion && bSourceCorrect)
                    .Label("Non-null entity gets positive constraint; null entity gets exclusion guard");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Preservation 3 — ExactValueMatchesPattern Non-Complex Strategies (Req 3.5)
    //
    // ExactValueMatchesPattern returns expected results for StartsWith,
    // EndsWith, Contains strategies. We test through PatternsOverlap since
    // ExactValueMatchesPattern is private.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 3.5**
    ///
    /// For ExactMatch values paired with StartsWith patterns, PatternsOverlap
    /// returns true if and only if the exact value starts with the StartsWith
    /// literal text. This structural matching logic must remain unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExactValueVsStartsWith_StructuralMatchingPreserved()
    {
        return Prop.ForAll(
            GenExactValueAndStartsWithPattern().ToArbitrary(),
            testCase =>
            {
                var result = PatternOverlapAnalyzer.PatternsOverlap(testCase.ExactConfig, testCase.PatternConfig);

                // The expected result: exact value starts with the pattern's literal text
                var literalText = DiscriminatorAnalyzer.GetPatternText(
                    testCase.PatternConfig.Pattern!, testCase.PatternConfig.Strategy);
                var expected = testCase.ExactConfig.ExactValue!.StartsWith(literalText, StringComparison.Ordinal);

                return (result == expected)
                    .Label($"PatternsOverlap(ExactMatch('{testCase.ExactConfig.ExactValue}'), " +
                           $"StartsWith('{literalText}')) = {result}, expected {expected}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.5**
    ///
    /// For ExactMatch values paired with EndsWith patterns, PatternsOverlap
    /// returns true if and only if the exact value ends with the EndsWith
    /// literal text. This structural matching logic must remain unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExactValueVsEndsWith_StructuralMatchingPreserved()
    {
        return Prop.ForAll(
            GenExactValueAndEndsWithPattern().ToArbitrary(),
            testCase =>
            {
                var result = PatternOverlapAnalyzer.PatternsOverlap(testCase.ExactConfig, testCase.PatternConfig);

                // The expected result: exact value ends with the pattern's literal text
                var literalText = DiscriminatorAnalyzer.GetPatternText(
                    testCase.PatternConfig.Pattern!, testCase.PatternConfig.Strategy);
                var expected = testCase.ExactConfig.ExactValue!.EndsWith(literalText, StringComparison.Ordinal);

                return (result == expected)
                    .Label($"PatternsOverlap(ExactMatch('{testCase.ExactConfig.ExactValue}'), " +
                           $"EndsWith('{literalText}')) = {result}, expected {expected}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.5**
    ///
    /// For ExactMatch values paired with Contains patterns, PatternsOverlap
    /// returns true if and only if the exact value contains the Contains
    /// literal text. This structural matching logic must remain unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExactValueVsContains_StructuralMatchingPreserved()
    {
        return Prop.ForAll(
            GenExactValueAndContainsPattern().ToArbitrary(),
            testCase =>
            {
                var result = PatternOverlapAnalyzer.PatternsOverlap(testCase.ExactConfig, testCase.PatternConfig);

                // The expected result: exact value contains the pattern's literal text
                var literalText = DiscriminatorAnalyzer.GetPatternText(
                    testCase.PatternConfig.Pattern!, testCase.PatternConfig.Strategy);
                var expected = testCase.ExactConfig.ExactValue!.IndexOf(literalText, StringComparison.Ordinal) >= 0;

                return (result == expected)
                    .Label($"PatternsOverlap(ExactMatch('{testCase.ExactConfig.ExactValue}'), " +
                           $"Contains('{literalText}')) = {result}, expected {expected}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Preservation 4 — Same-Score Auto-Derived FDDB102 (Req 3.4, 3.6)
    //
    // PatternOverlapAnalyzer.Analyze emits FDDB102 for same-score auto-derived
    // pairs on unfixed code.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 3.4, 3.6**
    ///
    /// When two auto-derived entities have the same specificity score and
    /// overlapping patterns, PatternOverlapAnalyzer.Analyze emits an FDDB102
    /// diagnostic. This behavior must be preserved after the fix.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameScoreAutoDerived_FDDB102Emitted()
    {
        return Prop.ForAll(
            GenSameScoreAutoDerivedOverlappingPair().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                // FDDB102 should be present for same-score auto-derived overlapping pairs
                var hasFddb102 = diagnostics.Any(d => d.Id == "FDDB102");

                return hasFddb102
                    .Label("Same-score auto-derived overlapping pair should emit FDDB102");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Preservation 5 — Internal-Segment Fallback (Req 3.8)
    //
    // CompoundPromotionPass resolves entities with Complex PK patterns
    // sharing the same reduced prefix via internal-segment positional
    // constraints.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 3.8**
    ///
    /// Two entities with PK TENANT#*#ROLE#* and TENANT#*#DEPT#* (both reduce
    /// to TENANT#*) are resolved via internal-segment positional constraints.
    /// </summary>
    [Fact]
    [Trait("Category", "Preservation")]
    [Trait("Feature", "compound-discrimination-prefix-subsumption")]
    public void InternalSegmentFallback_SameReducedPrefix_ResolvedViaPositionalConstraints()
    {
        // Arrange: Two entities with Complex PK patterns sharing the same reduced prefix
        var entityRole = CreateEntityWithCrossKeyPattern(
            "RoleEntity", "sk", "DATA#*", "pk", "TENANT#*#ROLE#*");
        var entityDept = CreateEntityWithCrossKeyPattern(
            "DeptEntity", "sk", "DATA#*", "pk", "TENANT#*#DEPT#*");

        var tableEntities = new List<EntityModel> { entityRole, entityDept };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        var orderedPair = OrderPair("RoleEntity", "DeptEntity");
        Assert.Contains(orderedPair, result.ResolvedPairs);

        // Assert: Both entities get positive positional constraints
        var constraintRole = entityRole.Discriminator!.CompoundConstraint;
        var constraintDept = entityDept.Discriminator!.CompoundConstraint;

        Assert.NotNull(constraintRole);
        Assert.NotNull(constraintDept);

        // Both should be positive (not exclusion)
        Assert.False(constraintRole!.IsExclusion);
        Assert.False(constraintDept!.IsExclusion);

        // Both should use Strategy=None with OffsetIndex > 0 (positional check)
        Assert.Equal(DiscriminatorStrategy.None, constraintRole.Strategy);
        Assert.Equal(DiscriminatorStrategy.None, constraintDept.Strategy);
        Assert.True(constraintRole.OffsetIndex > 0);
        Assert.True(constraintDept.OffsetIndex > 0);

        // The internal segments should differ (ROLE vs DEPT)
        Assert.NotEqual(constraintRole.LiteralText, constraintDept.LiteralText);

        // FDDB104 diagnostics should be emitted for both entities
        var fddb104Count = result.Diagnostics.Count(d => d.Id == "FDDB104");
        Assert.Equal(2, fddb104Count);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test Data Model
    // ──────────────────────────────────────────────────────────────────────

    private record EntityPair(EntityModel EntityA, EntityModel EntityB);

    private record PatternOverlapTestCase(DiscriminatorConfig ExactConfig, DiscriminatorConfig PatternConfig);

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenClassName = Gen.Elements(
        "PlatformCapability", "TenantCapability", "UserProfile", "AdminProfile",
        "OrderItem", "InvoiceItem", "EventLog", "AuditLog",
        "ProductConfig", "ServiceConfig", "AlertRule", "MetricRule");

    /// <summary>
    /// Prefixes chosen to be non-subsumptive (no prefix is a prefix of another)
    /// and each ends with '#' to form valid StartsWith patterns.
    /// </summary>
    private static readonly Gen<string> GenNonSubsumptivePrefix = Gen.Elements(
        "PLATFORM", "TENANT", "SERVICE", "ORDER", "INVOICE",
        "PRODUCT", "CONFIG", "ALERT", "METRIC", "EVENT");

    private static readonly Gen<string> GenSharedSkPrefix = Gen.Elements(
        "CAP", "ITEM", "LOG", "CFG", "RULE", "DATA", "REC", "META", "EVT", "REQ");

    /// <summary>
    /// Generates a pair of entities with non-subsumptive PK prefixes.
    /// The two prefixes are guaranteed to be different and neither is a prefix of the other.
    /// Both entities share the same SK discriminator pattern (same-score overlap).
    /// </summary>
    private static Gen<EntityPair> GenNonSubsumptivePrefixPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenSharedSkPrefix.SelectMany(skPrefix =>
                GenNonSubsumptivePrefix.Two()
                    .Where(prefixes =>
                    {
                        var litA = prefixes.Item1 + "#";
                        var litB = prefixes.Item2 + "#";
                        // Ensure non-subsumptive: neither is a prefix of the other, and not identical
                        return litA != litB
                            && !litA.StartsWith(litB, StringComparison.Ordinal)
                            && !litB.StartsWith(litA, StringComparison.Ordinal);
                    })
                    .Select(prefixes =>
                    {
                        var (nameA, nameB) = names;
                        if (nameA == nameB) nameB += "Alt";

                        var skPattern = $"{skPrefix}#*";
                        var pkPatternA = $"{prefixes.Item1}#*";
                        var pkPatternB = $"{prefixes.Item2}#*";

                        var entityA = CreateEntityWithCrossKeyPattern(nameA, "sk", skPattern, "pk", pkPatternA);
                        var entityB = CreateEntityWithCrossKeyPattern(nameB, "sk", skPattern, "pk", pkPatternB);

                        return new EntityPair(entityA, entityB);
                    })));
    }

    /// <summary>
    /// Generates a pair where one entity has a non-null PK prefix and the other has null.
    /// </summary>
    private static Gen<EntityPair> GenOneNullOneNonNullPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenSharedSkPrefix.SelectMany(skPrefix =>
                GenNonSubsumptivePrefix.Select(prefix =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var skPattern = $"{skPrefix}#*";
                    var pkPattern = $"{prefix}#*";

                    // EntityA has non-null cross-key, EntityB has null
                    var entityA = CreateEntityWithCrossKeyPattern(nameA, "sk", skPattern, "pk", pkPattern);
                    var entityB = CreateEntityWithCrossKeyPattern(nameB, "sk", skPattern, "pk", null);

                    return new EntityPair(entityA, entityB);
                })));
    }

    /// <summary>
    /// Generates exact values that may or may not match various pattern prefixes.
    /// Mix of matching and non-matching values to exercise both branches.
    /// </summary>
    private static Gen<string> GenExactValue()
    {
        var genMatchingStartsWith = GenNonSubsumptivePrefix.Select(p => $"{p}#somevalue");
        var genMatchingEndsWith = GenNonSubsumptivePrefix.Select(p => $"someprefix#{p}");
        var genMatchingContains = GenNonSubsumptivePrefix.Select(p => $"before#{p}#after");
        var genNonMatching = Gen.Elements(
            "SETTINGS", "PROFILE", "METADATA", "RANDOM", "OTHER",
            "X", "test", "nopattern");

        return Gen.OneOf(genMatchingStartsWith, genMatchingEndsWith, genMatchingContains, genNonMatching);
    }

    /// <summary>
    /// Generates a test case with an ExactMatch config paired with a StartsWith pattern config.
    /// Both share the same PropertyName so PatternsOverlap can evaluate them.
    /// </summary>
    private static Gen<PatternOverlapTestCase> GenExactValueAndStartsWithPattern()
    {
        return GenExactValue().SelectMany(exactValue =>
            GenNonSubsumptivePrefix.Select(prefix =>
            {
                var exactConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    ExactValue = exactValue,
                    Strategy = DiscriminatorStrategy.ExactMatch,
                    IsAutoDerived = true,
                    OverlappingPatterns = new List<ExclusionPattern>()
                };

                var startsWithPattern = $"{prefix}#*";
                var patternConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = startsWithPattern,
                    Strategy = DiscriminatorStrategy.StartsWith,
                    IsAutoDerived = true,
                    OverlappingPatterns = new List<ExclusionPattern>()
                };

                return new PatternOverlapTestCase(exactConfig, patternConfig);
            }));
    }

    /// <summary>
    /// Generates a test case with an ExactMatch config paired with an EndsWith pattern config.
    /// </summary>
    private static Gen<PatternOverlapTestCase> GenExactValueAndEndsWithPattern()
    {
        return GenExactValue().SelectMany(exactValue =>
            GenNonSubsumptivePrefix.Select(prefix =>
            {
                var exactConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    ExactValue = exactValue,
                    Strategy = DiscriminatorStrategy.ExactMatch,
                    IsAutoDerived = true,
                    OverlappingPatterns = new List<ExclusionPattern>()
                };

                var endsWithPattern = $"*#{prefix}";
                var patternConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = endsWithPattern,
                    Strategy = DiscriminatorStrategy.EndsWith,
                    IsAutoDerived = true,
                    OverlappingPatterns = new List<ExclusionPattern>()
                };

                return new PatternOverlapTestCase(exactConfig, patternConfig);
            }));
    }

    /// <summary>
    /// Generates a test case with an ExactMatch config paired with a Contains pattern config.
    /// </summary>
    private static Gen<PatternOverlapTestCase> GenExactValueAndContainsPattern()
    {
        return GenExactValue().SelectMany(exactValue =>
            GenNonSubsumptivePrefix.Select(prefix =>
            {
                var exactConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    ExactValue = exactValue,
                    Strategy = DiscriminatorStrategy.ExactMatch,
                    IsAutoDerived = true,
                    OverlappingPatterns = new List<ExclusionPattern>()
                };

                var containsPattern = $"*#{prefix}#*";
                var patternConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = containsPattern,
                    Strategy = DiscriminatorStrategy.Contains,
                    IsAutoDerived = true,
                    OverlappingPatterns = new List<ExclusionPattern>()
                };

                return new PatternOverlapTestCase(exactConfig, patternConfig);
            }));
    }

    /// <summary>
    /// Generates a same-score auto-derived overlapping pair for FDDB102 preservation.
    /// Both entities share the same SK discriminator pattern (same score) and both
    /// have null or identical PK patterns so the overlap is NOT resolved by
    /// CompoundPromotionPass.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreAutoDerivedOverlappingPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenSharedSkPrefix.Select(skPrefix =>
            {
                var (nameA, nameB) = names;
                if (nameA == nameB) nameB += "Alt";

                var skPattern = $"{skPrefix}#*";

                // Both have null PK patterns → same-score overlap, not disambiguable
                var entityA = CreateEntityWithCrossKeyPattern(nameA, "sk", skPattern, "pk", null);
                var entityB = CreateEntityWithCrossKeyPattern(nameB, "sk", skPattern, "pk", null);

                return new EntityPair(entityA, entityB);
            }));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Entity Construction Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an EntityModel with a same-score discriminator on the specified property
    /// and a cross-key DerivedDiscriminatorPattern on the opposite key property.
    /// </summary>
    private static EntityModel CreateEntityWithCrossKeyPattern(
        string className,
        string discriminatorPropertyName,
        string discriminatorPattern,
        string crossKeyAttributeName,
        string? crossKeyPattern)
    {
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            AttributeName = "pk",
            PropertyType = "string",
            IsPartitionKey = true,
            IsSortKey = false,
            DerivedDiscriminatorPattern = crossKeyAttributeName == "pk" ? crossKeyPattern : null
        };

        var skProperty = new PropertyModel
        {
            PropertyName = "Sk",
            AttributeName = "sk",
            PropertyType = "string",
            IsPartitionKey = false,
            IsSortKey = true,
            DerivedDiscriminatorPattern = crossKeyAttributeName == "sk" ? crossKeyPattern : null
        };

        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[] { pkProperty, skProperty },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = discriminatorPropertyName,
                Pattern = discriminatorPattern,
                Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(discriminatorPattern),
                IsAutoDerived = true,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };
    }

    private static void ClearState(EntityModel entityA, EntityModel entityB)
    {
        entityA.Discriminator!.CompoundConstraint = null;
        entityA.Discriminator.OverlappingPatterns.Clear();
        entityB.Discriminator!.CompoundConstraint = null;
        entityB.Discriminator.OverlappingPatterns.Clear();
    }

    private static (string, string) OrderPair(string nameA, string nameB)
    {
        return string.Compare(nameA, nameB, StringComparison.Ordinal) <= 0
            ? (nameA, nameB)
            : (nameB, nameA);
    }
}
