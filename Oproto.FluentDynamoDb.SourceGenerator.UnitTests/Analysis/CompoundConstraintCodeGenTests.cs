using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for compound constraint code generation in MapperGenerator.
/// Verifies that generated MatchesEntity code correctly handles positive CompoundConstraints,
/// ExclusionGuards, all strategy types, and AdditionalExclusions.
///
/// **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 7.1, 7.2, 7.3, 7.4**
/// </summary>
[Trait("Feature", "compound-key-discrimination")]
[Trait("Category", "Unit")]
public class CompoundConstraintCodeGenTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Test 1: Positive CompoundConstraint + missing cross-key attr → returns false
    // Requirements: 4.1, 4.4
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PositiveCompoundConstraint_GeneratesReturnFalseWhenCrossKeyMissing()
    {
        // Arrange: entity with positive compound constraint on "pk"
        var entity = CreateEntityWithPositiveCompoundConstraint(
            crossKeyPropertyName: "pk",
            pattern: "PLATFORM#*",
            strategy: DiscriminatorStrategy.StartsWith,
            literalText: "PLATFORM#");

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: generated code checks for cross-key attribute and returns false if missing/null
        generatedCode.Should().Contain("item.TryGetValue(\"pk\", out var compoundValue)");
        generatedCode.Should().Contain("compoundValue.S == null");
        generatedCode.Should().Contain("return false;");

        // The pattern: if (!item.TryGetValue("pk", out var compoundValue) || compoundValue.S == null)
        //                  return false;
        generatedCode.Should().Contain(
            "if (!item.TryGetValue(\"pk\", out var compoundValue) || compoundValue.S == null)");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 2: ExclusionGuard + missing cross-key attr → returns true
    // Requirements: 4.2, 4.5
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExclusionGuard_DoesNotReturnFalseWhenCrossKeyMissing()
    {
        // Arrange: entity with exclusion guard compound constraint on "pk"
        var entity = CreateEntityWithExclusionGuardConstraint(
            crossKeyPropertyName: "pk",
            pattern: "PLATFORM#*",
            strategy: DiscriminatorStrategy.StartsWith,
            literalText: "PLATFORM#",
            exclusionSourceEntity: "PlatformCapability");

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: exclusion guard uses TryGetValue in a single if-condition that returns false
        // only when the attribute IS present and MATCHES — not when it's missing
        // Pattern: if (item.TryGetValue("pk", out var compoundValue) && compoundValue.S != null
        //              && compoundValue.S.StartsWith("PLATFORM#"))
        //              return false;
        generatedCode.Should().Contain("item.TryGetValue(\"pk\", out var compoundValue)");
        generatedCode.Should().Contain("compoundValue.S != null");
        generatedCode.Should().Contain("compoundValue.S.StartsWith(\"PLATFORM#\")");

        // The exclusion guard should NOT have the pattern:
        // if (!item.TryGetValue(...) || ... == null) return false;
        // Instead it checks if the value IS present AND matches, then returns false
        generatedCode.Should().NotContain(
            "if (!item.TryGetValue(\"pk\", out var compoundValue) || compoundValue.S == null)");

        // Should still reach "return true" after passing exclusion checks
        generatedCode.Should().Contain("return true;");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 3: StartsWith, ExactMatch, EndsWith, Contains strategies
    // Requirements: 4.3, 7.1, 7.2, 7.3, 7.4
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PositiveCompoundConstraint_StartsWith_GeneratesStartsWithCheck()
    {
        var entity = CreateEntityWithPositiveCompoundConstraint(
            crossKeyPropertyName: "pk",
            pattern: "PLATFORM#*",
            strategy: DiscriminatorStrategy.StartsWith,
            literalText: "PLATFORM#");

        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        generatedCode.Should().Contain("compoundValue.S.StartsWith(\"PLATFORM#\")");
    }

    [Fact]
    public void PositiveCompoundConstraint_ExactMatch_GeneratesEqualityCheck()
    {
        var entity = CreateEntityWithPositiveCompoundConstraint(
            crossKeyPropertyName: "pk",
            pattern: "PROFILE",
            strategy: DiscriminatorStrategy.ExactMatch,
            literalText: "PROFILE");

        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        generatedCode.Should().Contain("compoundValue.S != \"PROFILE\"");
    }

    [Fact]
    public void PositiveCompoundConstraint_EndsWith_GeneratesEndsWithCheck()
    {
        var entity = CreateEntityWithPositiveCompoundConstraint(
            crossKeyPropertyName: "pk",
            pattern: "*#SUFFIX",
            strategy: DiscriminatorStrategy.EndsWith,
            literalText: "#SUFFIX");

        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        generatedCode.Should().Contain("compoundValue.S.EndsWith(\"#SUFFIX\")");
    }

    [Fact]
    public void PositiveCompoundConstraint_Contains_GeneratesContainsCheck()
    {
        var entity = CreateEntityWithPositiveCompoundConstraint(
            crossKeyPropertyName: "pk",
            pattern: "*#MIDDLE#*",
            strategy: DiscriminatorStrategy.Contains,
            literalText: "#MIDDLE#");

        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        generatedCode.Should().Contain("compoundValue.S.Contains(\"#MIDDLE#\")");
    }

    [Fact]
    public void ExclusionGuard_StartsWith_GeneratesStartsWithExclusionCheck()
    {
        var entity = CreateEntityWithExclusionGuardConstraint(
            crossKeyPropertyName: "pk",
            pattern: "ADMIN#*",
            strategy: DiscriminatorStrategy.StartsWith,
            literalText: "ADMIN#",
            exclusionSourceEntity: "AdminEntity");

        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        generatedCode.Should().Contain("compoundValue.S.StartsWith(\"ADMIN#\")");
    }

    [Fact]
    public void ExclusionGuard_ExactMatch_GeneratesEqualityExclusionCheck()
    {
        var entity = CreateEntityWithExclusionGuardConstraint(
            crossKeyPropertyName: "pk",
            pattern: "SINGLETON",
            strategy: DiscriminatorStrategy.ExactMatch,
            literalText: "SINGLETON",
            exclusionSourceEntity: "SingletonEntity");

        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        generatedCode.Should().Contain("compoundValue.S == \"SINGLETON\"");
    }

    [Fact]
    public void ExclusionGuard_EndsWith_GeneratesEndsWithExclusionCheck()
    {
        var entity = CreateEntityWithExclusionGuardConstraint(
            crossKeyPropertyName: "pk",
            pattern: "*#TAIL",
            strategy: DiscriminatorStrategy.EndsWith,
            literalText: "#TAIL",
            exclusionSourceEntity: "TailEntity");

        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        generatedCode.Should().Contain("compoundValue.S.EndsWith(\"#TAIL\")");
    }

    [Fact]
    public void ExclusionGuard_Contains_GeneratesContainsExclusionCheck()
    {
        var entity = CreateEntityWithExclusionGuardConstraint(
            crossKeyPropertyName: "pk",
            pattern: "*#CORE#*",
            strategy: DiscriminatorStrategy.Contains,
            literalText: "#CORE#",
            exclusionSourceEntity: "CoreEntity");

        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        generatedCode.Should().Contain("compoundValue.S.Contains(\"#CORE#\")");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 4: AdditionalExclusions generates multiple exclusion checks
    // Requirements: 4.2, 4.5
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void AdditionalExclusions_GeneratesMultipleExclusionChecks()
    {
        // Arrange: entity with primary exclusion + two additional exclusions
        var entity = CreateEntityWithMultipleExclusionGuards();

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: should contain the primary exclusion check variable
        generatedCode.Should().Contain("compoundValue");

        // Should contain additional exclusion variable names (compoundValue2, compoundValue3)
        generatedCode.Should().Contain("compoundValue2");
        generatedCode.Should().Contain("compoundValue3");

        // Should contain all three exclusion pattern checks
        generatedCode.Should().Contain("PLATFORM#");
        generatedCode.Should().Contain("ADMIN#");
        generatedCode.Should().Contain("SYSTEM#");

        // Each exclusion should independently return false on match
        // Count occurrences of "return false" — should have at least 3 from exclusion guards
        // plus the discriminator check return false
        var returnFalseCount = generatedCode.Split("return false;").Length - 1;
        returnFalseCount.Should().BeGreaterThanOrEqualTo(4); // discriminator + 3 exclusion guards
    }

    [Fact]
    public void AdditionalExclusions_EachHasCorrectStrategy()
    {
        // Arrange: entity with exclusions using different strategies
        var entity = CreateEntityWithMixedStrategyExclusions();

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: each exclusion uses its own strategy
        generatedCode.Should().Contain("StartsWith(\"PREFIX#\")");
        generatedCode.Should().Contain("EndsWith(\"#SUFFIX\")");
        generatedCode.Should().Contain("Contains(\"#MID#\")");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static EntityModel CreateEntityWithPositiveCompoundConstraint(
        string crossKeyPropertyName,
        string pattern,
        DiscriminatorStrategy strategy,
        string literalText)
    {
        return new EntityModel
        {
            ClassName = "PlatformCapability",
            Namespace = "TestNamespace",
            TableName = "test-table",
            TableEntityCount = 2,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "CAP#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                CompoundConstraint = new CompoundConstraint
                {
                    PropertyName = crossKeyPropertyName,
                    Pattern = pattern,
                    Strategy = strategy,
                    LiteralText = literalText,
                    IsExclusion = false
                }
            }
        };
    }

    private static EntityModel CreateEntityWithExclusionGuardConstraint(
        string crossKeyPropertyName,
        string pattern,
        DiscriminatorStrategy strategy,
        string literalText,
        string exclusionSourceEntity)
    {
        return new EntityModel
        {
            ClassName = "TenantCapability",
            Namespace = "TestNamespace",
            TableName = "test-table",
            TableEntityCount = 2,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "CAP#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                CompoundConstraint = new CompoundConstraint
                {
                    PropertyName = crossKeyPropertyName,
                    Pattern = pattern,
                    Strategy = strategy,
                    LiteralText = literalText,
                    IsExclusion = true,
                    ExclusionSourceEntity = exclusionSourceEntity
                }
            }
        };
    }

    private static EntityModel CreateEntityWithMultipleExclusionGuards()
    {
        return new EntityModel
        {
            ClassName = "GenericCapability",
            Namespace = "TestNamespace",
            TableName = "test-table",
            TableEntityCount = 4,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "CAP#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                CompoundConstraint = new CompoundConstraint
                {
                    PropertyName = "pk",
                    Pattern = "PLATFORM#*",
                    Strategy = DiscriminatorStrategy.StartsWith,
                    LiteralText = "PLATFORM#",
                    IsExclusion = true,
                    ExclusionSourceEntity = "PlatformCapability",
                    AdditionalExclusions = new List<CompoundConstraint>
                    {
                        new CompoundConstraint
                        {
                            PropertyName = "pk",
                            Pattern = "ADMIN#*",
                            Strategy = DiscriminatorStrategy.StartsWith,
                            LiteralText = "ADMIN#",
                            IsExclusion = true,
                            ExclusionSourceEntity = "AdminCapability"
                        },
                        new CompoundConstraint
                        {
                            PropertyName = "pk",
                            Pattern = "SYSTEM#*",
                            Strategy = DiscriminatorStrategy.StartsWith,
                            LiteralText = "SYSTEM#",
                            IsExclusion = true,
                            ExclusionSourceEntity = "SystemCapability"
                        }
                    }
                }
            }
        };
    }

    private static EntityModel CreateEntityWithMixedStrategyExclusions()
    {
        return new EntityModel
        {
            ClassName = "MixedEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            TableEntityCount = 4,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "CAP#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                CompoundConstraint = new CompoundConstraint
                {
                    PropertyName = "pk",
                    Pattern = "PREFIX#*",
                    Strategy = DiscriminatorStrategy.StartsWith,
                    LiteralText = "PREFIX#",
                    IsExclusion = true,
                    ExclusionSourceEntity = "PrefixEntity",
                    AdditionalExclusions = new List<CompoundConstraint>
                    {
                        new CompoundConstraint
                        {
                            PropertyName = "pk",
                            Pattern = "*#SUFFIX",
                            Strategy = DiscriminatorStrategy.EndsWith,
                            LiteralText = "#SUFFIX",
                            IsExclusion = true,
                            ExclusionSourceEntity = "SuffixEntity"
                        },
                        new CompoundConstraint
                        {
                            PropertyName = "pk",
                            Pattern = "*#MID#*",
                            Strategy = DiscriminatorStrategy.Contains,
                            LiteralText = "#MID#",
                            IsExclusion = true,
                            ExclusionSourceEntity = "MidEntity"
                        }
                    }
                }
            }
        };
    }
}
