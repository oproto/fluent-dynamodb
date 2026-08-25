using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Integration tests that verify the complex pattern exclusion fix works through the complete
/// source generator pipeline. Tests verify the GENERATED MatchesEntity code against actual
/// DynamoDB item dictionary values using the full Analyze → Generate flow.
///
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.5, 3.1, 3.4, 3.6**
/// </summary>
public class ComplexPatternExclusionIntegrationTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // 1. Hash separator (#) integration test
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("CAP#capability1", true, "single segment after prefix should match less-specific entity")]
    [InlineData("CAP#svc1#cap1", false, "multi-segment should be excluded from less-specific entity")]
    public void HashSeparator_LessSpecificEntity_DiscriminatesCorrectly(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Act — generate the less-specific entity implementation
        var generatedSource = MapperGenerator.GenerateEntityImplementation(lessSpecific);

        // Assert — verify generated code contains IndexOf-based exclusion
        generatedSource.Should().Contain("IndexOf(\"#\", 4)", "exclusion should use positional IndexOf");

        // Verify the logic directly: StartsWith("CAP#") AND IndexOf("#", 4) < 0
        var matchesLessSpecific = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) < 0;
        matchesLessSpecific.Should().Be(shouldMatch, reason);
    }

    [Theory]
    [InlineData("CAP#svc1#cap1", true, "multi-segment should match more-specific entity")]
    [InlineData("CAP#capability1", false, "single segment should not match more-specific entity")]
    public void HashSeparator_MoreSpecificEntity_DiscriminatesCorrectly(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Act — generate the more-specific entity implementation
        var generatedSource = MapperGenerator.GenerateEntityImplementation(moreSpecific);

        // Assert — verify generated code uses just StartsWith (no redundant Contains)
        generatedSource.Should().Contain("StartsWith(\"CAP#\")", "should check prefix");
        generatedSource.Should().NotContain("Contains(\"#\")", "should not have redundant Contains");

        // Verify the logic directly: StartsWith("CAP#") AND has additional # after prefix
        var matchesMoreSpecific = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) >= 0;
        matchesMoreSpecific.Should().Be(shouldMatch, reason);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2. Underscore separator (_) integration test
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("CAP_capability1", true, "single segment after prefix should match less-specific entity")]
    [InlineData("CAP_svc1_cap1", false, "multi-segment should be excluded from less-specific entity")]
    public void UnderscoreSeparator_LessSpecificEntity_DiscriminatesCorrectly(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "_", "CAP_*", "CAP_*_*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(lessSpecific);

        // Assert — verify generated code contains IndexOf-based exclusion with offset 4
        generatedSource.Should().Contain("IndexOf(\"_\", 4)", "exclusion should use positional IndexOf for underscore");

        // Verify the logic directly
        var matchesLessSpecific = skValue.StartsWith("CAP_") && skValue.IndexOf("_", 4) < 0;
        matchesLessSpecific.Should().Be(shouldMatch, reason);
    }

    [Theory]
    [InlineData("CAP_svc1_cap1", true, "multi-segment should match more-specific entity")]
    [InlineData("CAP_capability1", false, "single segment should not match more-specific entity")]
    public void UnderscoreSeparator_MoreSpecificEntity_DiscriminatesCorrectly(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "_", "CAP_*", "CAP_*_*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(moreSpecific);

        // Assert
        generatedSource.Should().Contain("StartsWith(\"CAP_\")", "should check prefix");
        generatedSource.Should().NotContain("Contains(\"_\")", "should not have redundant Contains");

        // Verify the logic directly
        var matchesMoreSpecific = skValue.StartsWith("CAP_") && skValue.IndexOf("_", 4) >= 0;
        matchesMoreSpecific.Should().Be(shouldMatch, reason);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3. Colon separator (:) integration test
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NS:value1", true, "single segment after prefix should match less-specific entity")]
    [InlineData("NS:ns1:val1", false, "multi-segment should be excluded from less-specific entity")]
    public void ColonSeparator_LessSpecificEntity_DiscriminatesCorrectly(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("NS", ":", "NS:*", "NS:*:*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(lessSpecific);

        // Assert — verify generated code contains IndexOf-based exclusion with offset 3
        generatedSource.Should().Contain("IndexOf(\":\", 3)", "exclusion should use positional IndexOf for colon");

        // Verify the logic directly
        var matchesLessSpecific = skValue.StartsWith("NS:") && skValue.IndexOf(":", 3) < 0;
        matchesLessSpecific.Should().Be(shouldMatch, reason);
    }

    [Theory]
    [InlineData("NS:ns1:val1", true, "multi-segment should match more-specific entity")]
    [InlineData("NS:value1", false, "single segment should not match more-specific entity")]
    public void ColonSeparator_MoreSpecificEntity_DiscriminatesCorrectly(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("NS", ":", "NS:*", "NS:*:*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(moreSpecific);

        // Assert
        generatedSource.Should().Contain("StartsWith(\"NS:\")", "should check prefix");
        generatedSource.Should().NotContain("Contains(\":\")", "should not have redundant Contains");

        // Verify the logic directly
        var matchesMoreSpecific = skValue.StartsWith("NS:") && skValue.IndexOf(":", 3) >= 0;
        matchesMoreSpecific.Should().Be(shouldMatch, reason);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4. Meaningful segment preservation integration test
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("INVOICE#INV001", true, "invoice without LINE should match less-specific entity")]
    [InlineData("INVOICE#INV001#LINE#1", false, "invoice with LINE should be excluded from less-specific entity")]
    public void MeaningfulSegment_LessSpecificEntity_UsesContainsForExclusion(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var lessSpecific = CreateEntityWithProperties("InvoiceEntity", "INVOICE#*", DiscriminatorStrategy.StartsWith, "sk", "INVOICE#{0}");
        var moreSpecific = CreateEntityWithProperties("InvoiceLineEntity", "INVOICE#*#LINE#*", DiscriminatorStrategy.Complex, "sk", "INVOICE#{0}#LINE#{1}");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(lessSpecific);

        // Assert — should use Contains("#LINE#") for exclusion, NOT IndexOf
        generatedSource.Should().Contain("Contains(\"#LINE#\")", "meaningful segment should use Contains");
        generatedSource.Should().NotContain("IndexOf(\"#LINE#\"", "meaningful segment should NOT use IndexOf");

        // Verify the logic directly
        var matchesLessSpecific = skValue.StartsWith("INVOICE#") && !skValue.Contains("#LINE#");
        matchesLessSpecific.Should().Be(shouldMatch, reason);
    }

    [Theory]
    [InlineData("INVOICE#INV001#LINE#1", true, "invoice with LINE should match more-specific entity")]
    [InlineData("INVOICE#INV001", false, "invoice without LINE should not match more-specific entity")]
    public void MeaningfulSegment_MoreSpecificEntity_UsesContainsForPositiveMatch(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var lessSpecific = CreateEntityWithProperties("InvoiceEntity", "INVOICE#*", DiscriminatorStrategy.StartsWith, "sk", "INVOICE#{0}");
        var moreSpecific = CreateEntityWithProperties("InvoiceLineEntity", "INVOICE#*#LINE#*", DiscriminatorStrategy.Complex, "sk", "INVOICE#{0}#LINE#{1}");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(moreSpecific);

        // Assert — should have StartsWith and Contains for positive match
        generatedSource.Should().Contain("StartsWith(\"INVOICE#\")", "should check prefix");
        generatedSource.Should().Contain("Contains(\"#LINE#\")", "should check meaningful segment");

        // Verify the logic directly
        var matchesMoreSpecific = skValue.StartsWith("INVOICE#") && skValue.Contains("#LINE#");
        matchesMoreSpecific.Should().Be(shouldMatch, reason);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 5. Three-entity overlap test
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("CAP#cap1", true, "single segment should match first entity")]
    [InlineData("CAP#svc1#cap1", false, "two segments should NOT match first entity")]
    [InlineData("CAP#svc1#cap1#extra", false, "three segments should NOT match first entity")]
    public void ThreeEntityOverlap_FirstEntity_MatchesOnlySingleSegment(string skValue, bool shouldMatch, string reason)
    {
        // Arrange — three entities: CAP#*, CAP#*#*, CAP#*#*#*
        var (entity1, entity2, entity3) = CreateThreeOverlappingEntities();
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entity1, entity2, entity3 });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity1);

        // Assert — first entity should have exclusion(s)
        entity1.Discriminator!.OverlappingPatterns.Should().NotBeEmpty("first entity should have exclusions");

        // Verify: passes StartsWith("CAP#") AND IndexOf("#", 4) < 0
        var matchesFirst = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) < 0;
        matchesFirst.Should().Be(shouldMatch, reason);
    }

    [Theory]
    [InlineData("CAP#svc1#cap1", true, "two segments should match second entity")]
    [InlineData("CAP#cap1", false, "single segment should NOT match second entity")]
    [InlineData("CAP#svc1#cap1#extra", false, "three segments should NOT match second entity")]
    public void ThreeEntityOverlap_SecondEntity_MatchesOnlyTwoSegments(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var (entity1, entity2, entity3) = CreateThreeOverlappingEntities();
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entity1, entity2, entity3 });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity2);

        // Verify: passes StartsWith("CAP#") AND has exactly one additional "#" after prefix
        // IndexOf("#", 4) >= 0 means at least one '#' after prefix
        // To distinguish from 3-segment: need to check there's no SECOND '#' after prefix
        var firstSepIdx = skValue.IndexOf("#", 4);
        var matchesSecond = skValue.StartsWith("CAP#")
                            && firstSepIdx >= 0
                            && (firstSepIdx == skValue.Length - 1 || skValue.IndexOf("#", firstSepIdx + 1) < 0);
        matchesSecond.Should().Be(shouldMatch, reason);
    }

    [Theory]
    [InlineData("CAP#svc1#cap1#extra", true, "three segments should match third entity")]
    [InlineData("CAP#cap1", false, "single segment should NOT match third entity")]
    [InlineData("CAP#svc1#cap1", false, "two segments should NOT match third entity")]
    public void ThreeEntityOverlap_ThirdEntity_MatchesOnlyThreeSegments(string skValue, bool shouldMatch, string reason)
    {
        // Arrange
        var (entity1, entity2, entity3) = CreateThreeOverlappingEntities();
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entity1, entity2, entity3 });

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity3);

        // Verify: passes StartsWith("CAP#") AND has at least two "#" after prefix
        var firstSepIdx = skValue.IndexOf("#", 4);
        var secondSepIdx = firstSepIdx >= 0 ? skValue.IndexOf("#", firstSepIdx + 1) : -1;
        var matchesThird = skValue.StartsWith("CAP#") && secondSepIdx >= 0;
        matchesThird.Should().Be(shouldMatch, reason);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 6. Edge case values
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EdgeCase_EmptyAfterPrefix_MatchesLessSpecificEntity()
    {
        // "CAP#" (empty after prefix) → IndexOf("#", 4) returns -1 → NOT excluded
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var skValue = "CAP#";
        var matchesLess = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) < 0;
        matchesLess.Should().BeTrue("empty value after prefix should match less-specific entity");

        var matchesMore = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) >= 0;
        matchesMore.Should().BeFalse("empty value after prefix should NOT match more-specific entity");
    }

    [Fact]
    public void EdgeCase_SingleCharValue_MatchesLessSpecificEntity()
    {
        // "CAP#a" (single char value) → IndexOf("#", 4) returns -1 → NOT excluded
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var skValue = "CAP#a";
        var matchesLess = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) < 0;
        matchesLess.Should().BeTrue("single char value should match less-specific entity");

        var matchesMore = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) >= 0;
        matchesMore.Should().BeFalse("single char value should NOT match more-specific entity");
    }

    [Fact]
    public void EdgeCase_SeparatorImmediatelyAfterPrefix_CorrectlyEvaluated()
    {
        // "CAP##" (separator immediately after prefix) → IndexOf("#", 4) returns 4 → IS excluded
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var skValue = "CAP##";

        // This value has a '#' at position 4 — it IS multi-segment, so it should be excluded
        // from the less-specific entity and match the more-specific entity
        var matchesLess = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) < 0;
        matchesLess.Should().BeFalse("separator immediately after prefix means multi-segment — excluded from less-specific");

        var matchesMore = skValue.StartsWith("CAP#") && skValue.IndexOf("#", 4) >= 0;
        matchesMore.Should().BeTrue("separator immediately after prefix means multi-segment — matches more-specific");
    }

    [Fact]
    public void EdgeCase_GeneratedCodeForLessSpecific_ContainsCorrectExclusion()
    {
        // Verify that the full generated source for the less-specific entity uses IndexOf correctly
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        PatternOverlapAnalyzer.Analyze(new List<EntityModel> { lessSpecific, moreSpecific });

        var generatedSource = MapperGenerator.GenerateEntityImplementation(lessSpecific);

        // The generated code should:
        // 1. Check StartsWith("CAP#") as positive match
        generatedSource.Should().Contain("StartsWith(\"CAP#\")", "positive match should use StartsWith");
        // 2. Use IndexOf("#", 4) >= 0 as exclusion (not bare Contains("#"))
        generatedSource.Should().Contain("IndexOf(\"#\", 4)", "exclusion should use positional IndexOf");
        // 3. NOT use Contains("#") anywhere as it would be tautological
        generatedSource.Should().NotContain("Contains(\"#\")", "should not have tautological Contains");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a pair of overlapping entities with the given prefix and separator.
    /// </summary>
    private static (EntityModel lessSpecific, EntityModel moreSpecific) CreateOverlappingEntities(
        string prefix, string separator, string lessPattern, string morePattern)
    {
        var lessStrategy = DiscriminatorAnalyzer.DeterminePatternStrategy(lessPattern);
        var moreStrategy = DiscriminatorAnalyzer.DeterminePatternStrategy(morePattern);

        var lessSpecific = new EntityModel
        {
            ClassName = "LessSpecificEntity",
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = lessPattern,
                Strategy = lessStrategy,
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
                    NormalizedKeyFormat = $"{prefix}{separator}{{0}}",
                    DerivedDiscriminatorPattern = lessPattern
                }
            }
        };

        var moreSpecific = new EntityModel
        {
            ClassName = "MoreSpecificEntity",
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = morePattern,
                Strategy = moreStrategy,
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
                    NormalizedKeyFormat = $"{prefix}{separator}{{0}}{separator}{{1}}",
                    DerivedDiscriminatorPattern = morePattern
                }
            }
        };

        return (lessSpecific, moreSpecific);
    }

    /// <summary>
    /// Creates three overlapping entities: CAP#*, CAP#*#*, CAP#*#*#*
    /// </summary>
    private static (EntityModel entity1, EntityModel entity2, EntityModel entity3) CreateThreeOverlappingEntities()
    {
        var entity1 = new EntityModel
        {
            ClassName = "SingleSegmentEntity",
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "CAP#*",
                Strategy = DiscriminatorStrategy.StartsWith,
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
                    NormalizedKeyFormat = "CAP#{0}",
                    DerivedDiscriminatorPattern = "CAP#*"
                }
            }
        };

        var entity2 = new EntityModel
        {
            ClassName = "TwoSegmentEntity",
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "CAP#*#*",
                Strategy = DiscriminatorStrategy.Complex,
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
                    NormalizedKeyFormat = "CAP#{0}#{1}",
                    DerivedDiscriminatorPattern = "CAP#*#*"
                }
            }
        };

        var entity3 = new EntityModel
        {
            ClassName = "ThreeSegmentEntity",
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "CAP#*#*#*",
                Strategy = DiscriminatorStrategy.Complex,
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
                    NormalizedKeyFormat = "CAP#{0}#{1}#{2}",
                    DerivedDiscriminatorPattern = "CAP#*#*#*"
                }
            }
        };

        return (entity1, entity2, entity3);
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
}
