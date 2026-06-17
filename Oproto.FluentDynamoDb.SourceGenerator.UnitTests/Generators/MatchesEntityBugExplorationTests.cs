using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Bug condition exploration tests for the MatchesEntity filtering fix.
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists.
///
/// Bug Condition: isBugCondition(entity, item) where entity has a valid DiscriminatorConfig
/// OR entity.TableEntityCount == 1, AND generated code checks all non-nullable properties
/// instead of using discriminator-only or key-only checks.
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 2.1, 2.3, 2.4**
/// </summary>
[Trait("Category", "BugExploration")]
public class MatchesEntityBugExplorationTests
{
    /// <summary>
    /// Property 1 (Tier 1): When an entity has a valid DiscriminatorConfig with ExactMatch strategy,
    /// the generated MatchesEntity code should NOT contain ContainsKey checks for non-key data attributes.
    /// Instead, it should use only the discriminator check.
    ///
    /// On unfixed code, this test FAILS because GenerateMatchesEntityMethod ignores entity.Discriminator
    /// and always checks all non-nullable properties with ContainsKey.
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Tier1_ExactMatch_ShouldNotCheckNonKeyDataAttributes()
    {
        var strategyGen = Gen.Constant(DiscriminatorStrategy.ExactMatch);

        var discriminatorPropertyGen = Gen.Elements("entity_type", "type", "sk", "discriminator");
        var discriminatorValueGen = Gen.Elements("USER", "ORDER", "EMPLOYEE", "PRODUCT");

        var entityGen = from discProp in discriminatorPropertyGen
                        from discValue in discriminatorValueGen
                        select new EntityModel
                        {
                            ClassName = "TestEntity",
                            Namespace = "TestNamespace",
                            TableName = "test-table",
                            Discriminator = new DiscriminatorConfig
                            {
                                PropertyName = discProp,
                                ExactValue = discValue,
                                Strategy = DiscriminatorStrategy.ExactMatch
                            },
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
                                },
                                new PropertyModel
                                {
                                    PropertyName = "MiddleName",
                                    AttributeName = "middleName",
                                    PropertyType = "string",
                                    IsNullable = false
                                },
                                new PropertyModel
                                {
                                    PropertyName = "Phones",
                                    AttributeName = "phones",
                                    PropertyType = "List<string>",
                                    IsNullable = false,
                                    IsCollection = true
                                }
                            }
                        };

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                // Act: Generate entity implementation (includes MatchesEntity)
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

                // Extract the MatchesEntity method body
                var matchesEntitySection = ExtractMatchesEntityMethod(generatedCode);

                // Assert: generated code should NOT contain ContainsKey for non-key data attributes
                var checksMiddleName = matchesEntitySection.Contains("ContainsKey(\"middleName\")");
                var checksPhones = matchesEntitySection.Contains("ContainsKey(\"phones\")");

                // Assert: generated code SHOULD contain discriminator-based check
                var hasDiscriminatorCheck = matchesEntitySection.Contains(entity.Discriminator!.PropertyName);

                return (!checksMiddleName && !checksPhones && hasDiscriminatorCheck)
                    .Label($"Discriminator property: '{entity.Discriminator.PropertyName}', " +
                           $"value: '{entity.Discriminator.ExactValue}'. " +
                           $"checksMiddleName={checksMiddleName}, checksPhones={checksPhones}, " +
                           $"hasDiscriminatorCheck={hasDiscriminatorCheck}. " +
                           $"MatchesEntity body:\n{matchesEntitySection}");
            });
    }

    /// <summary>
    /// Property 1 (Tier 1): When an entity has a valid DiscriminatorConfig with StartsWith strategy,
    /// the generated MatchesEntity code should use StartsWith check and NOT check non-key data attributes.
    ///
    /// On unfixed code, this test FAILS because the discriminator pattern is never used.
    ///
    /// **Validates: Requirements 1.1, 2.3**
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Tier1_StartsWith_ShouldUsePatternAndNotCheckDataAttributes()
    {
        var discriminatorPropertyGen = Gen.Elements("sk", "gsi1sk", "type_key");
        var patternPrefixGen = Gen.Elements("EMPLOYEE#", "ORDER#", "USER#", "PRODUCT#");

        var entityGen = from discProp in discriminatorPropertyGen
                        from prefix in patternPrefixGen
                        select new EntityModel
                        {
                            ClassName = "PatternEntity",
                            Namespace = "TestNamespace",
                            TableName = "pattern-table",
                            Discriminator = new DiscriminatorConfig
                            {
                                PropertyName = discProp,
                                Pattern = prefix + "*",
                                Strategy = DiscriminatorStrategy.StartsWith
                            },
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
                                },
                                new PropertyModel
                                {
                                    PropertyName = "Email",
                                    AttributeName = "email",
                                    PropertyType = "string",
                                    IsNullable = false
                                },
                                new PropertyModel
                                {
                                    PropertyName = "Tags",
                                    AttributeName = "tags",
                                    PropertyType = "List<string>",
                                    IsNullable = false,
                                    IsCollection = true
                                }
                            }
                        };

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);
                var matchesEntitySection = ExtractMatchesEntityMethod(generatedCode);

                // Should NOT check non-key data attributes
                var checksEmail = matchesEntitySection.Contains("ContainsKey(\"email\")");
                var checksTags = matchesEntitySection.Contains("ContainsKey(\"tags\")");

                // Should contain StartsWith-based discriminator check
                var hasStartsWithCheck = matchesEntitySection.Contains("StartsWith");

                return (!checksEmail && !checksTags && hasStartsWithCheck)
                    .Label($"Discriminator: '{entity.Discriminator!.PropertyName}' with pattern '{entity.Discriminator.Pattern}'. " +
                           $"checksEmail={checksEmail}, checksTags={checksTags}, " +
                           $"hasStartsWithCheck={hasStartsWithCheck}. " +
                           $"MatchesEntity body:\n{matchesEntitySection}");
            });
    }

    /// <summary>
    /// Property 1 (Tier 1): When an entity has a valid DiscriminatorConfig with EndsWith or Contains strategy,
    /// the generated MatchesEntity code should NOT check non-key data attributes.
    ///
    /// On unfixed code, this test FAILS because entity.Discriminator is never consulted.
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Tier1_EndsWithOrContains_ShouldNotCheckDataAttributes()
    {
        var strategyGen = Gen.Elements(DiscriminatorStrategy.EndsWith, DiscriminatorStrategy.Contains);
        var patternGen = Gen.Elements("*#USER", "*#ORDER", "*USER*", "*ITEM*");

        var entityGen = from strategy in strategyGen
                        from pattern in patternGen
                        select new EntityModel
                        {
                            ClassName = "AdvancedPatternEntity",
                            Namespace = "TestNamespace",
                            TableName = "advanced-table",
                            Discriminator = new DiscriminatorConfig
                            {
                                PropertyName = "sk",
                                Pattern = pattern,
                                Strategy = strategy
                            },
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
                                },
                                new PropertyModel
                                {
                                    PropertyName = "Address",
                                    AttributeName = "address",
                                    PropertyType = "string",
                                    IsNullable = false
                                },
                                new PropertyModel
                                {
                                    PropertyName = "Orders",
                                    AttributeName = "orders",
                                    PropertyType = "List<int>",
                                    IsNullable = false,
                                    IsCollection = true
                                }
                            }
                        };

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);
                var matchesEntitySection = ExtractMatchesEntityMethod(generatedCode);

                // Should NOT check non-key data attributes
                var checksAddress = matchesEntitySection.Contains("ContainsKey(\"address\")");
                var checksOrders = matchesEntitySection.Contains("ContainsKey(\"orders\")");

                // Should reference the discriminator property name
                var hasDiscriminatorPropertyRef = matchesEntitySection.Contains(entity.Discriminator!.PropertyName);

                return (!checksAddress && !checksOrders && hasDiscriminatorPropertyRef)
                    .Label($"Strategy: {entity.Discriminator.Strategy}, Pattern: '{entity.Discriminator.Pattern}'. " +
                           $"checksAddress={checksAddress}, checksOrders={checksOrders}, " +
                           $"hasDiscriminatorPropertyRef={hasDiscriminatorPropertyRef}. " +
                           $"MatchesEntity body:\n{matchesEntitySection}");
            });
    }

    /// <summary>
    /// Property 1 (Tier 2): When an entity is on a single-entity table (TableEntityCount == 1),
    /// the generated MatchesEntity code should only check key attributes (pk, sk), not non-key 
    /// non-nullable data attributes.
    ///
    /// NOTE: TableEntityCount does not yet exist on EntityModel (will be added in task 3.1).
    /// This test demonstrates the bug on the current code which checks ALL non-nullable properties
    /// regardless of table context.
    ///
    /// On unfixed code, this test FAILS because there is no concept of single-entity table and
    /// all non-nullable properties are checked.
    ///
    /// **Validates: Requirements 1.2, 1.3, 1.4, 2.4**
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Tier2_SingleEntityTable_ShouldOnlyCheckKeyAttributes()
    {
        var nonKeyAttrGen = Gen.Elements(
            ("Status", "status"),
            ("Name", "name"),
            ("Balance", "balance"),
            ("CreatedAt", "createdAt"));

        var entityGen = from attr1 in nonKeyAttrGen
                        from attr2 in nonKeyAttrGen
                        where attr1.Item1 != attr2.Item1
                        select new EntityModel
                        {
                            ClassName = "SingleTableEntity",
                            Namespace = "TestNamespace",
                            TableName = "single-entity-table",
                            // No discriminator - relying on single-entity-table behavior
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
                                },
                                new PropertyModel
                                {
                                    PropertyName = attr1.Item1,
                                    AttributeName = attr1.Item2,
                                    PropertyType = "string",
                                    IsNullable = false
                                },
                                new PropertyModel
                                {
                                    PropertyName = attr2.Item1,
                                    AttributeName = attr2.Item2,
                                    PropertyType = "string",
                                    IsNullable = false
                                }
                            }
                        };

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);
                var matchesEntitySection = ExtractMatchesEntityMethod(generatedCode);

                // Should check key attributes
                var checksPk = matchesEntitySection.Contains("ContainsKey(\"pk\")");
                var checksSk = matchesEntitySection.Contains("ContainsKey(\"sk\")");

                // Should NOT check non-key data attributes
                var nonKeyAttributes = entity.Properties
                    .Where(p => !p.IsPartitionKey && !p.IsSortKey)
                    .ToArray();

                var checksAnyNonKeyAttr = nonKeyAttributes
                    .Any(p => matchesEntitySection.Contains($"ContainsKey(\"{p.AttributeName}\")"));

                return (checksPk && checksSk && !checksAnyNonKeyAttr)
                    .Label($"Non-key attributes: [{string.Join(", ", nonKeyAttributes.Select(p => p.AttributeName))}]. " +
                           $"checksPk={checksPk}, checksSk={checksSk}, " +
                           $"checksAnyNonKeyAttr={checksAnyNonKeyAttr}. " +
                           $"MatchesEntity body:\n{matchesEntitySection}");
            });
    }

    /// <summary>
    /// Extracts the MatchesEntity method body from the full generated code.
    /// </summary>
    private static string ExtractMatchesEntityMethod(string generatedCode)
    {
        const string methodSignature = "public static bool MatchesEntity(Dictionary<string, AttributeValue> item)";
        var startIndex = generatedCode.IndexOf(methodSignature, StringComparison.Ordinal);
        if (startIndex < 0)
            return string.Empty;

        // Find the opening brace of the method
        var braceIndex = generatedCode.IndexOf('{', startIndex);
        if (braceIndex < 0)
            return string.Empty;

        // Track braces to find the matching closing brace
        var depth = 0;
        var endIndex = braceIndex;
        for (var i = braceIndex; i < generatedCode.Length; i++)
        {
            if (generatedCode[i] == '{') depth++;
            else if (generatedCode[i] == '}') depth--;

            if (depth == 0)
            {
                endIndex = i;
                break;
            }
        }

        return generatedCode.Substring(startIndex, endIndex - startIndex + 1);
    }
}
