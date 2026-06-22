using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Preservation property tests for MatchesEntity code generation.
/// These tests verify behaviors that must remain correct BOTH before and after the fix.
/// They run on UNFIXED code first (expected to PASS) and then again after the fix (must still PASS).
///
/// **Validates: Requirements 3.1, 3.2, 3.4, 3.5**
/// </summary>
[Trait("Category", "Preservation")]
public class MatchesEntityPreservationTests
{
    /// <summary>
    /// Property 2: Key Attribute Absence - Partition Key
    /// For any entity configuration, the generated MatchesEntity code must contain
    /// a ContainsKey check for the partition key attribute. This ensures items missing
    /// the partition key are always rejected.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_AlwaysChecksPartitionKeyPresence()
    {
        var pkAttributeNameGen = Gen.Elements("pk", "PK", "partition_key", "id", "userId");
        var classNameGen = Gen.Elements("UserEntity", "OrderEntity", "ProductEntity", "AccountEntity");
        var tableNameGen = Gen.Elements("users-table", "orders-table", "products-table");

        var entityGen = from pkAttr in pkAttributeNameGen
                        from className in classNameGen
                        from tableName in tableNameGen
                        select new EntityModel
                        {
                            ClassName = className,
                            Namespace = "TestNamespace",
                            TableName = tableName,
                            Properties = new[]
                            {
                                new PropertyModel
                                {
                                    PropertyName = "Pk",
                                    AttributeName = pkAttr,
                                    PropertyType = "string",
                                    IsPartitionKey = true
                                },
                                new PropertyModel
                                {
                                    PropertyName = "Name",
                                    AttributeName = "name",
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

                var pkAttr = entity.Properties.First(p => p.IsPartitionKey).AttributeName;
                var checksPk = matchesEntitySection.Contains($"ContainsKey(\"{pkAttr}\")");

                return checksPk
                    .Label($"Entity '{entity.ClassName}' with pk attribute '{pkAttr}'. " +
                           $"checksPk={checksPk}. " +
                           $"MatchesEntity body:\n{matchesEntitySection}");
            });
    }

    /// <summary>
    /// Property 2: Key Attribute Absence - Sort Key
    /// For any entity with a sort key defined, the generated MatchesEntity code must contain
    /// a ContainsKey check for the sort key attribute. This ensures items missing
    /// the sort key are always rejected.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_AlwaysChecksSortKeyPresenceWhenDefined()
    {
        var skAttributeNameGen = Gen.Elements("sk", "SK", "sort_key", "range_key", "sortKey");
        var classNameGen = Gen.Elements("OrderEntity", "InvoiceEntity", "EventEntity");
        var tableNameGen = Gen.Elements("orders-table", "events-table", "invoices-table");

        var entityGen = from skAttr in skAttributeNameGen
                        from className in classNameGen
                        from tableName in tableNameGen
                        select new EntityModel
                        {
                            ClassName = className,
                            Namespace = "TestNamespace",
                            TableName = tableName,
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
                                    AttributeName = skAttr,
                                    PropertyType = "string",
                                    IsSortKey = true
                                },
                                new PropertyModel
                                {
                                    PropertyName = "Data",
                                    AttributeName = "data",
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

                var skAttr = entity.Properties.First(p => p.IsSortKey).AttributeName;
                var checksSk = matchesEntitySection.Contains($"ContainsKey(\"{skAttr}\")");

                return checksSk
                    .Label($"Entity '{entity.ClassName}' with sk attribute '{skAttr}'. " +
                           $"checksSk={checksSk}. " +
                           $"MatchesEntity body:\n{matchesEntitySection}");
            });
    }

    /// <summary>
    /// Property 2: Discriminator Mismatch - Legacy EntityDiscriminator
    /// For entities with legacy EntityDiscriminator set, the generated code must check
    /// item.TryGetValue("entity_type", ...) to ensure discriminator mismatches are rejected.
    /// This preserves backward compatibility with existing discrimination behavior.
    /// 
    /// Note: The legacy EntityDiscriminator maps to DynamoDB attribute "entity_type" (snake_case).
    /// The DiscriminatorAnalyzer performs this mapping, and the entity model must have both
    /// EntityDiscriminator and Discriminator set (as the real pipeline does via EntityAnalyzer).
    ///
    /// **Validates: Requirements 3.2, 3.4**
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_LegacyEntityDiscriminator_ChecksEntityTypeAttribute()
    {
        var discriminatorValueGen = Gen.Elements("USER", "ORDER", "PRODUCT", "INVOICE", "ACCOUNT");
        var classNameGen = Gen.Elements("UserEntity", "OrderEntity", "ProductEntity");

        var entityGen = from discValue in discriminatorValueGen
                        from className in classNameGen
                        select new EntityModel
                        {
                            ClassName = className,
                            Namespace = "TestNamespace",
                            TableName = "shared-table",
                            EntityDiscriminator = discValue,
                            // The real pipeline (EntityAnalyzer) always populates Discriminator
                            // when EntityDiscriminator is set, mapping to "entity_type" attribute
                            Discriminator = new DiscriminatorConfig
                            {
                                PropertyName = "entity_type",
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
                                    PropertyName = "Name",
                                    AttributeName = "name",
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

                // The code checks TryGetValue("entity_type", ...) — the actual DynamoDB attribute name
                var hasEntityTypeCheck = matchesEntitySection.Contains("TryGetValue(\"entity_type\"");

                // The generated code should reference the discriminator value
                var hasDiscriminatorValue = matchesEntitySection.Contains(entity.EntityDiscriminator!);

                return (hasEntityTypeCheck && hasDiscriminatorValue)
                    .Label($"Entity '{entity.ClassName}' with EntityDiscriminator='{entity.EntityDiscriminator}'. " +
                           $"hasEntityTypeCheck={hasEntityTypeCheck}, hasDiscriminatorValue={hasDiscriminatorValue}. " +
                           $"MatchesEntity body:\n{matchesEntitySection}");
            });
    }

    /// <summary>
    /// Property 2: Method Signature Preservation
    /// For all entities, the generated code must contain the exact method signature
    /// `public static bool MatchesEntity(Dictionary&lt;string, AttributeValue&gt; item)`
    /// to ensure call-site compatibility is maintained.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_AlwaysContainsCorrectMethodSignature()
    {
        var classNameGen = Gen.Elements(
            "SimpleEntity", "UserEntity", "OrderEntity",
            "ProductEntity", "AccountEntity", "InvoiceEntity");
        var tableNameGen = Gen.Elements("table-a", "table-b", "table-c");
        var hasSkGen = Gen.Elements(true, false);
        var hasDiscriminatorGen = Gen.Elements(true, false);

        var entityGen = from className in classNameGen
                        from tableName in tableNameGen
                        from hasSk in hasSkGen
                        from hasDisc in hasDiscriminatorGen
                        select CreateEntityWithOptions(className, tableName, hasSk, hasDisc);

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

                const string expectedSignature =
                    "public static bool MatchesEntity(Dictionary<string, AttributeValue> item)";
                var hasCorrectSignature = generatedCode.Contains(expectedSignature);

                return hasCorrectSignature
                    .Label($"Entity '{entity.ClassName}' (hasSk={entity.SortKeyProperty != null}, " +
                           $"hasDisc={!string.IsNullOrEmpty(entity.EntityDiscriminator)}). " +
                           $"hasCorrectSignature={hasCorrectSignature}");
            });
    }

    /// <summary>
    /// Property 2: Key Attribute Absence on Multi-Entity Table Without Discriminator
    /// For entities without discriminator on multi-entity tables, items missing key
    /// attributes must still be rejected (ContainsKey checks for pk/sk must be present).
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_MultiEntityNoDiscriminator_StillChecksKeyAttributes()
    {
        var classNameGen = Gen.Elements("EntityA", "EntityB", "EntityC");
        var tableNameGen = Gen.Elements("shared-table", "multi-entity-table");

        var entityGen = from className in classNameGen
                        from tableName in tableNameGen
                        select new EntityModel
                        {
                            ClassName = className,
                            Namespace = "TestNamespace",
                            TableName = tableName,
                            // No discriminator configured — multi-entity scenario
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
                                    PropertyName = "Status",
                                    AttributeName = "status",
                                    PropertyType = "string",
                                    IsNullable = true
                                }
                            }
                        };

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);
                var matchesEntitySection = ExtractMatchesEntityMethod(generatedCode);

                var checksPk = matchesEntitySection.Contains("ContainsKey(\"pk\")");
                var checksSk = matchesEntitySection.Contains("ContainsKey(\"sk\")");

                return (checksPk && checksSk)
                    .Label($"Entity '{entity.ClassName}' on table '{entity.TableName}'. " +
                           $"checksPk={checksPk}, checksSk={checksSk}. " +
                           $"MatchesEntity body:\n{matchesEntitySection}");
            });
    }

    #region Helpers

    private static EntityModel CreateEntityWithOptions(
        string className, string tableName, bool hasSortKey, bool hasDiscriminator)
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                AttributeName = "pk",
                PropertyType = "string",
                IsPartitionKey = true
            }
        };

        if (hasSortKey)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = "Sk",
                AttributeName = "sk",
                PropertyType = "string",
                IsSortKey = true
            });
        }

        // Add a non-key non-nullable property
        properties.Add(new PropertyModel
        {
            PropertyName = "Name",
            AttributeName = "name",
            PropertyType = "string",
            IsNullable = false
        });

        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = tableName,
            EntityDiscriminator = hasDiscriminator ? "TEST_TYPE" : null,
            // Mirror the real pipeline: when EntityDiscriminator is set,
            // Discriminator is also populated by EntityAnalyzer
            Discriminator = hasDiscriminator ? new DiscriminatorConfig
            {
                PropertyName = "entity_type",
                ExactValue = "TEST_TYPE",
                Strategy = DiscriminatorStrategy.ExactMatch
            } : null,
            Properties = properties.ToArray()
        };
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

    #endregion
}
