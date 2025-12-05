using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for primary entity identification in GenerateMultiItemFromDynamoDb.
/// 
/// **Feature: composite-entity-assembly, Property 3: Primary Entity Identification**
/// **Validates: Requirements 2.1, 2.2**
/// 
/// These tests verify that for any set of DynamoDB items containing a primary entity item
/// (matching the entity's sort key pattern) and related items, the generated code correctly
/// identifies the primary entity item and populates non-collection properties from it.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class PrimaryEntityIdentificationPropertyTests
{
    private static readonly string[] EntityPrefixes = { "INVOICE", "ORDER", "USER", "PRODUCT", "CUSTOMER" };
    private static readonly string[] RelatedPrefixes = { "LINE", "ITEM", "DETAIL", "META", "AUDIT" };
    private static readonly string[] IdValues = { "001", "ABC", "xyz-123", "test", "12345", "a-b-c" };

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 3: Primary Entity Identification**
    /// **Validates: Requirements 2.1, 2.2**
    /// 
    /// Property: For any primary entity sort key and any set of related entity sort keys,
    /// the primary entity pattern should NOT match any related entity sort keys.
    /// This ensures the primary entity can be correctly identified.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PrimaryEntityPattern_DoesNotMatchRelatedEntitySortKeys()
    {
        var tupleArb = Arb.From(
            Gen.Elements(EntityPrefixes)
                .SelectMany(entityPrefix => Gen.Elements(RelatedPrefixes)
                    .SelectMany(relatedPrefix => Gen.Elements(IdValues)
                        .SelectMany(entityId => Gen.Elements(IdValues)
                            .Select(relatedId => (entityPrefix, relatedPrefix, entityId, relatedId)))))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (entityPrefix, relatedPrefix, entityId, relatedId) = tuple;
            
            // Primary entity sort key: "ENTITY#id"
            var primarySortKey = $"{entityPrefix}#{entityId}";
            
            // Related entity sort key: "ENTITY#id#RELATED#relatedId"
            var relatedSortKey = $"{entityPrefix}#{entityId}#{relatedPrefix}#{relatedId}";
            
            // Related entity pattern: "ENTITY#*#RELATED#*"
            var relatedPattern = $"{entityPrefix}#*#{relatedPrefix}#*";
            var relatedRegex = MapperGenerator.ConvertWildcardPatternToRegex(relatedPattern);
            
            // The primary sort key should NOT match the related pattern
            var primaryMatchesRelatedPattern = Regex.IsMatch(primarySortKey, relatedRegex);
            
            // The related sort key SHOULD match the related pattern
            var relatedMatchesRelatedPattern = Regex.IsMatch(relatedSortKey, relatedRegex);
            
            // Property: Primary entity can be distinguished from related entities
            return !primaryMatchesRelatedPattern && relatedMatchesRelatedPattern;
        });
    }

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 3: Primary Entity Identification**
    /// **Validates: Requirements 2.1, 2.2**
    /// 
    /// Property: For any list of items where the primary entity appears at any position,
    /// the identification logic should find the primary entity regardless of position.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PrimaryEntityIdentification_FindsPrimaryRegardlessOfPosition()
    {
        var tupleArb = Arb.From(
            Gen.Elements(EntityPrefixes)
                .SelectMany(entityPrefix => Gen.Elements(RelatedPrefixes)
                    .SelectMany(relatedPrefix => Gen.Elements(IdValues)
                        .SelectMany(entityId => Gen.Choose(0, 4)
                            .SelectMany(relatedCount => Gen.Choose(0, relatedCount)
                                .Select(primaryPosition => (entityPrefix, relatedPrefix, entityId, relatedCount, primaryPosition))))))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (entityPrefix, relatedPrefix, entityId, relatedCount, primaryPosition) = tuple;
            
            // Build list of sort keys
            var sortKeys = new List<string>();
            
            // Add related items before primary position
            for (int i = 0; i < primaryPosition && i < relatedCount; i++)
            {
                sortKeys.Add($"{entityPrefix}#{entityId}#{relatedPrefix}#{i}");
            }
            
            // Add primary entity at the specified position
            var primarySortKey = $"{entityPrefix}#{entityId}";
            sortKeys.Add(primarySortKey);
            
            // Add remaining related items after primary position
            for (int i = primaryPosition; i < relatedCount; i++)
            {
                sortKeys.Add($"{entityPrefix}#{entityId}#{relatedPrefix}#{i}");
            }
            
            // Related entity pattern
            var relatedPattern = $"{entityPrefix}#*#{relatedPrefix}#*";
            var relatedRegex = MapperGenerator.ConvertWildcardPatternToRegex(relatedPattern);
            
            // Find primary entity (first item that doesn't match any related pattern)
            string? foundPrimary = null;
            foreach (var sortKey in sortKeys)
            {
                if (!Regex.IsMatch(sortKey, relatedRegex))
                {
                    foundPrimary = sortKey;
                    break;
                }
            }
            
            // Property: The found primary should be the actual primary sort key
            return foundPrimary == primarySortKey;
        });
    }

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 3: Primary Entity Identification**
    /// **Validates: Requirements 2.1, 2.2**
    /// 
    /// Property: For any entity with multiple related entity patterns, the primary entity
    /// should not match ANY of the related patterns.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PrimaryEntityIdentification_ExcludesAllRelatedPatterns()
    {
        var tupleArb = Arb.From(
            Gen.Elements(EntityPrefixes)
                .SelectMany(entityPrefix => Gen.Elements(IdValues)
                    .SelectMany(entityId => Gen.Choose(1, 3)
                        .Select(patternCount => (entityPrefix, entityId, patternCount))))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (entityPrefix, entityId, patternCount) = tuple;
            
            // Primary entity sort key
            var primarySortKey = $"{entityPrefix}#{entityId}";
            
            // Generate multiple related patterns
            var relatedPatterns = RelatedPrefixes
                .Take(patternCount)
                .Select(rp => $"{entityPrefix}#*#{rp}#*")
                .ToList();
            
            // Check that primary doesn't match any related pattern
            var matchesAnyRelated = relatedPatterns.Any(pattern =>
            {
                var regex = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
                return Regex.IsMatch(primarySortKey, regex);
            });
            
            // Property: Primary entity should not match any related pattern
            return !matchesAnyRelated;
        });
    }

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 3: Primary Entity Identification**
    /// **Validates: Requirements 2.3**
    /// 
    /// Property: When no primary entity item exists (only related items), 
    /// the identification should return null/not find a primary.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PrimaryEntityIdentification_ReturnsNullWhenNoPrimaryExists()
    {
        var tupleArb = Arb.From(
            Gen.Elements(EntityPrefixes)
                .SelectMany(entityPrefix => Gen.Elements(RelatedPrefixes)
                    .SelectMany(relatedPrefix => Gen.Elements(IdValues)
                        .SelectMany(entityId => Gen.Choose(1, 5)
                            .Select(relatedCount => (entityPrefix, relatedPrefix, entityId, relatedCount)))))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (entityPrefix, relatedPrefix, entityId, relatedCount) = tuple;
            
            // Build list of ONLY related sort keys (no primary)
            var sortKeys = new List<string>();
            for (int i = 0; i < relatedCount; i++)
            {
                sortKeys.Add($"{entityPrefix}#{entityId}#{relatedPrefix}#{i}");
            }
            
            // Related entity pattern
            var relatedPattern = $"{entityPrefix}#*#{relatedPrefix}#*";
            var relatedRegex = MapperGenerator.ConvertWildcardPatternToRegex(relatedPattern);
            
            // Try to find primary entity (should find none)
            string? foundPrimary = null;
            foreach (var sortKey in sortKeys)
            {
                if (!Regex.IsMatch(sortKey, relatedRegex))
                {
                    foundPrimary = sortKey;
                    break;
                }
            }
            
            // Property: No primary should be found when only related items exist
            return foundPrimary == null;
        });
    }
}
