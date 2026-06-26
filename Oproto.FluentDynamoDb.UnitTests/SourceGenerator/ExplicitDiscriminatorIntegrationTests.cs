using System.Reflection;
using System.Runtime.Serialization;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Integration tests verifying that existing entities with explicit discriminator configurations
/// produce the same MatchesEntity behavior after the auto-derivation feature is introduced.
/// Validates Requirements 10.5, 10.7, 10.8.
/// </summary>
public class ExplicitDiscriminatorIntegrationTests
{
    private readonly object _analyzer;
    private readonly MethodInfo _applyMethod;
    private readonly MethodInfo _computeFormatsMethod;
    private readonly MethodInfo _derivePatternsMethod;
    private readonly MethodInfo _validateExplicitVsDerivedMethod;
    private readonly MethodInfo _detectRedundantMethod;

    public ExplicitDiscriminatorIntegrationTests()
    {
        // Use GetUninitializedObject to create an instance without calling the constructor,
        // avoiding the Roslyn assembly dependency that fails at runtime in this test project.
        _analyzer = FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));
        _applyMethod = typeof(EntityAnalyzer).GetMethod(
            "ApplyAutoDerivedDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        _computeFormatsMethod = typeof(EntityAnalyzer).GetMethod(
            "ComputeNormalizedKeyFormats",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        _derivePatternsMethod = typeof(EntityAnalyzer).GetMethod(
            "DeriveDiscriminatorPatterns",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        _validateExplicitVsDerivedMethod = typeof(EntityAnalyzer).GetMethod(
            "ValidateExplicitVsDerivedDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        _detectRedundantMethod = typeof(EntityAnalyzer).GetMethod(
            "DetectRedundantExplicitDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private void InvokeComputeNormalizedKeyFormats(EntityModel entity)
    {
        _computeFormatsMethod.Invoke(_analyzer, new object[] { entity });
    }

    private void InvokeDeriveDiscriminatorPatterns(EntityModel entity)
    {
        _derivePatternsMethod.Invoke(_analyzer, new object[] { entity });
    }

    private void InvokeApplyAutoDerivedDiscriminator(EntityModel entity)
    {
        _applyMethod.Invoke(_analyzer, new object[] { entity });
    }

    private void InvokeValidateExplicitVsDerivedDiscriminator(EntityModel entity)
    {
        _validateExplicitVsDerivedMethod.Invoke(_analyzer, new object[] { entity });
    }

    private void InvokeDetectRedundantExplicitDiscriminator(EntityModel entity)
    {
        _detectRedundantMethod.Invoke(_analyzer, new object[] { entity });
    }

    /// <summary>
    /// Runs the full analysis pipeline on an entity: compute key formats, derive patterns,
    /// then apply auto-derived discriminator.
    /// </summary>
    private void RunFullAnalysisPipeline(EntityModel entity)
    {
        InvokeComputeNormalizedKeyFormats(entity);
        InvokeDeriveDiscriminatorPatterns(entity);
        InvokeApplyAutoDerivedDiscriminator(entity);
    }

    /// <summary>
    /// Verifies that an entity with an explicit DiscriminatorProperty/DiscriminatorPattern
    /// and a sort key with Prefix="ORDER" (which would auto-derive the same pattern)
    /// retains its explicit discriminator after analysis.
    /// The auto-derived pattern should NOT replace the explicit discriminator.
    /// Validates: Requirements 10.5, 10.7
    /// </summary>
    [Fact]
    public void ExplicitDiscriminator_WithMatchingSortKeyPrefix_IsPreserved()
    {
        // Arrange - Entity has explicit discriminator on "sk" with pattern "ORDER#*"
        // and also has a sort key with Prefix="ORDER" (would auto-derive "ORDER#*")
        var explicitDiscriminator = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "ORDER#*",
            Strategy = DiscriminatorStrategy.StartsWith,
            IsAutoDerived = false
        };

        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            TableEntityCount = 2, // multi-entity table
            Discriminator = explicitDiscriminator,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "CUSTOMER", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" }
                }
            }
        };

        // Act - Run full analysis pipeline
        RunFullAnalysisPipeline(entity);

        // Assert - Explicit discriminator should be preserved unchanged
        entity.Discriminator.Should().BeSameAs(explicitDiscriminator);
        entity.Discriminator!.PropertyName.Should().Be("sk");
        entity.Discriminator.Pattern.Should().Be("ORDER#*");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        entity.Discriminator.IsAutoDerived.Should().BeFalse();

        // Verify the derived pattern was still computed on the property
        var skProperty = entity.Properties.First(p => p.IsSortKey);
        skProperty.NormalizedKeyFormat.Should().Be("ORDER#{0}");
        skProperty.DerivedDiscriminatorPattern.Should().Be("ORDER#*");
    }

    /// <summary>
    /// Verifies that an entity with an explicit discriminator pointing to a non-key
    /// attribute (e.g., "entityType") is preserved even when keys have auto-derivable patterns.
    /// Validates: Requirements 10.5, 10.7
    /// </summary>
    [Fact]
    public void ExplicitDiscriminator_OnNonKeyAttribute_IsPreserved()
    {
        // Arrange - Entity uses a separate "entityType" attribute for discrimination
        var explicitDiscriminator = new DiscriminatorConfig
        {
            PropertyName = "entityType",
            ExactValue = "ORDER",
            Strategy = DiscriminatorStrategy.ExactMatch,
            IsAutoDerived = false
        };

        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            TableEntityCount = 2,
            Discriminator = explicitDiscriminator,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "CUSTOMER", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" }
                }
            }
        };

        // Act
        RunFullAnalysisPipeline(entity);

        // Assert - Explicit discriminator on non-key attribute is unchanged
        entity.Discriminator.Should().BeSameAs(explicitDiscriminator);
        entity.Discriminator!.PropertyName.Should().Be("entityType");
        entity.Discriminator.ExactValue.Should().Be("ORDER");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
        entity.Discriminator.IsAutoDerived.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that MatchesEntity behavior is correct for an entity with explicit discriminator:
    /// - Items with matching pattern are accepted
    /// - Items with non-matching pattern are rejected
    /// - Items missing the discriminator attribute are rejected
    /// This test validates the discriminator config produces the correct matching logic
    /// by simulating the same checks that GenerateDiscriminatorCheck would emit.
    /// Validates: Requirements 10.5, 10.7, 10.8
    /// </summary>
    [Fact]
    public void ExplicitDiscriminator_MatchesEntityBehavior_IsCorrect()
    {
        // Arrange - Entity with explicit StartsWith discriminator on "sk" = "ORDER#*"
        var explicitDiscriminator = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "ORDER#*",
            Strategy = DiscriminatorStrategy.StartsWith,
            IsAutoDerived = false
        };

        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            TableEntityCount = 2,
            Discriminator = explicitDiscriminator,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "CUSTOMER", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" }
                }
            }
        };

        // Act - Run analysis
        RunFullAnalysisPipeline(entity);

        // Assert - Simulate MatchesEntity logic based on discriminator config
        // The generated code would check: item["sk"].S.StartsWith("ORDER#")
        var disc = entity.Discriminator!;
        disc.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        var literalPrefix = DiscriminatorAnalyzer.GetPatternText(disc.Pattern!, disc.Strategy);
        literalPrefix.Should().Be("ORDER#");

        // Matching items should pass
        SimulateMatchesEntity("ORDER#12345", disc).Should().BeTrue();
        SimulateMatchesEntity("ORDER#abc", disc).Should().BeTrue();
        SimulateMatchesEntity("ORDER#", disc).Should().BeTrue();

        // Non-matching items should fail
        SimulateMatchesEntity("LINE#12345", disc).Should().BeFalse();
        SimulateMatchesEntity("USER#abc", disc).Should().BeFalse();
        SimulateMatchesEntity("ORDERX", disc).Should().BeFalse();

        // Missing/null discriminator value should fail
        SimulateMatchesEntity(null, disc).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that an entity with explicit DiscriminatorValue (exact match) is preserved
    /// through the analysis pipeline and produces correct matching behavior.
    /// Validates: Requirements 10.5, 10.7, 10.8
    /// </summary>
    [Fact]
    public void ExplicitDiscriminatorValue_ExactMatch_ProducesCorrectBehavior()
    {
        // Arrange - Entity uses DiscriminatorValue for exact match
        var explicitDiscriminator = new DiscriminatorConfig
        {
            PropertyName = "sk",
            ExactValue = "ORDER_META",
            Strategy = DiscriminatorStrategy.ExactMatch,
            IsAutoDerived = false
        };

        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            TableEntityCount = 2,
            Discriminator = explicitDiscriminator,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "CUSTOMER", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" }
                }
            }
        };

        // Act
        RunFullAnalysisPipeline(entity);

        // Assert - Explicit ExactMatch discriminator preserved
        entity.Discriminator.Should().BeSameAs(explicitDiscriminator);
        entity.Discriminator!.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
        entity.Discriminator.ExactValue.Should().Be("ORDER_META");
        entity.Discriminator.IsAutoDerived.Should().BeFalse();

        // Verify MatchesEntity behavior: only exact match passes
        SimulateMatchesEntity("ORDER_META", entity.Discriminator).Should().BeTrue();
        SimulateMatchesEntity("ORDER_META_EXTRA", entity.Discriminator).Should().BeFalse();
        SimulateMatchesEntity("ORDER#123", entity.Discriminator).Should().BeFalse();
        SimulateMatchesEntity("order_meta", entity.Discriminator).Should().BeFalse(); // case-sensitive
        SimulateMatchesEntity(null, entity.Discriminator).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that when explicit discriminator pattern matches the auto-derived pattern exactly,
    /// the entity still uses the explicit discriminator (not replaced by auto-derived),
    /// and the generated MatchesEntity behavior is identical.
    /// This is the FDDB103 scenario — redundant but not incorrect.
    /// Validates: Requirements 10.5, 10.7, 10.8
    /// </summary>
    [Fact]
    public void ExplicitDiscriminator_MatchesDerived_SameBehaviorAsAutoDerived()
    {
        // Arrange - Two entities: one with explicit discriminator (redundant), one without
        var explicitDiscriminator = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "ORDER#*",
            Strategy = DiscriminatorStrategy.StartsWith,
            IsAutoDerived = false
        };

        var entityWithExplicit = new EntityModel
        {
            ClassName = "OrderExplicit",
            TableName = "orders",
            TableEntityCount = 2,
            Discriminator = explicitDiscriminator,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "CUSTOMER", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" }
                }
            }
        };

        var entityWithoutExplicit = new EntityModel
        {
            ClassName = "OrderAutoDerived",
            TableName = "orders",
            TableEntityCount = 2,
            Discriminator = null, // no explicit discriminator
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "CUSTOMER", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" }
                }
            }
        };

        // Act - Run full analysis on both
        RunFullAnalysisPipeline(entityWithExplicit);
        RunFullAnalysisPipeline(entityWithoutExplicit);

        // Assert - Both entities produce the same MatchesEntity behavior
        var explicitDisc = entityWithExplicit.Discriminator!;
        var autoDerivedDisc = entityWithoutExplicit.Discriminator!;

        // Same property name
        explicitDisc.PropertyName.Should().Be(autoDerivedDisc.PropertyName);
        // Same pattern
        explicitDisc.Pattern.Should().Be(autoDerivedDisc.Pattern);
        // Same strategy
        explicitDisc.Strategy.Should().Be(autoDerivedDisc.Strategy);

        // Verify both produce identical MatchesEntity matching behavior
        var testValues = new[] { "ORDER#123", "ORDER#", "LINE#456", "USER#789", "ORDERX", null };
        foreach (var value in testValues)
        {
            var explicitResult = SimulateMatchesEntity(value, explicitDisc);
            var autoDerivedResult = SimulateMatchesEntity(value, autoDerivedDisc);
            explicitResult.Should().Be(autoDerivedResult,
                because: $"both discriminators should produce same result for value '{value ?? "null"}'");
        }
    }

    /// <summary>
    /// Verifies that a complex explicit discriminator pattern (multiple wildcards)
    /// is preserved and produces correct MatchesEntity behavior.
    /// Validates: Requirements 10.5, 10.7, 10.8
    /// </summary>
    [Fact]
    public void ExplicitDiscriminator_ComplexPattern_ProducesCorrectBehavior()
    {
        // Arrange - Complex pattern with multiple segments
        var explicitDiscriminator = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "TENANT#*#USER#*",
            Strategy = DiscriminatorStrategy.Complex,
            IsAutoDerived = false
        };

        var entity = new EntityModel
        {
            ClassName = "TenantUser",
            TableName = "multi-tenant",
            TableEntityCount = 2,
            Discriminator = explicitDiscriminator,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = null, Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "TENANT", Separator = "#" }
                }
            }
        };

        // Act
        RunFullAnalysisPipeline(entity);

        // Assert - Complex explicit discriminator preserved
        entity.Discriminator.Should().BeSameAs(explicitDiscriminator);
        entity.Discriminator!.Strategy.Should().Be(DiscriminatorStrategy.Complex);
        entity.Discriminator.Pattern.Should().Be("TENANT#*#USER#*");
        entity.Discriminator.IsAutoDerived.Should().BeFalse();

        // Verify complex matching behavior:
        // For complex patterns, MatchesEntity checks StartsWith first segment + Contains each internal segment
        SimulateMatchesEntity("TENANT#abc#USER#123", entity.Discriminator).Should().BeTrue();
        SimulateMatchesEntity("TENANT#xyz#USER#456", entity.Discriminator).Should().BeTrue();
        SimulateMatchesEntity("TENANT##USER#", entity.Discriminator).Should().BeTrue();

        // Non-matching items
        SimulateMatchesEntity("ORDER#abc#USER#123", entity.Discriminator).Should().BeFalse();
        SimulateMatchesEntity("TENANT#abc#ITEM#123", entity.Discriminator).Should().BeFalse();
        SimulateMatchesEntity(null, entity.Discriminator).Should().BeFalse();
    }

    /// <summary>
    /// Simulates the MatchesEntity discriminator check logic that the code generator would emit.
    /// Returns true if the value matches the discriminator config, false otherwise.
    /// </summary>
    private static bool SimulateMatchesEntity(string? value, DiscriminatorConfig disc)
    {
        // Simulate: if value is null, return false (discriminator attribute missing or null)
        if (value == null)
            return false;

        switch (disc.Strategy)
        {
            case DiscriminatorStrategy.ExactMatch:
                return value == disc.ExactValue;

            case DiscriminatorStrategy.StartsWith:
                var startsWithText = DiscriminatorAnalyzer.GetPatternText(disc.Pattern!, disc.Strategy);
                return value.StartsWith(startsWithText);

            case DiscriminatorStrategy.EndsWith:
                var endsWithText = DiscriminatorAnalyzer.GetPatternText(disc.Pattern!, disc.Strategy);
                return value.EndsWith(endsWithText);

            case DiscriminatorStrategy.Contains:
                var containsText = DiscriminatorAnalyzer.GetPatternText(disc.Pattern!, disc.Strategy);
                return value.Contains(containsText);

            case DiscriminatorStrategy.Complex:
                // Complex pattern: split by * to get literal segments, check StartsWith for first,
                // Contains for subsequent segments
                return SimulateComplexPatternMatch(value, disc.Pattern!);

            default:
                return true;
        }
    }

    /// <summary>
    /// Simulates the complex pattern matching logic (multiple wildcards).
    /// The pattern like "TENANT#*#USER#*" is split into segments ["TENANT#", "#USER#", ""]
    /// and the value must start with the first segment and contain each subsequent non-empty segment.
    /// </summary>
    private static bool SimulateComplexPatternMatch(string value, string pattern)
    {
        var segments = pattern.Split('*');

        // First segment: value must start with it
        if (segments.Length > 0 && !string.IsNullOrEmpty(segments[0]))
        {
            if (!value.StartsWith(segments[0]))
                return false;
        }

        // Subsequent segments: value must contain each non-empty segment
        for (int i = 1; i < segments.Length; i++)
        {
            if (!string.IsNullOrEmpty(segments[i]))
            {
                if (!value.Contains(segments[i]))
                    return false;
            }
        }

        return true;
    }
}
