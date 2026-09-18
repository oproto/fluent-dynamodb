using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Bug condition exploration tests for extracted properties with related entity fix.
/// These tests encode the EXPECTED behavior (extraction logic present in multi-item paths)
/// and are expected to FAIL on unfixed code — failure confirms the bug exists.
///
/// Bug Condition: When an entity has both [Extracted] properties and a [RelatedEntity]
/// property, the source generator sets IsMultiItemEntity = true, routing deserialization
/// through multi-item code paths. Both sync (GenerateMultiItemFromDynamoDb) and async
/// (GenerateMultiItemFromDynamoDbAsync) paths are missing the GenerateExtractedKeyLogic
/// call that the single-item paths include, causing extracted properties to silently
/// retain their default values (e.g., DateTime.MinValue).
///
/// **Feature: extracted-properties-related-entity-fix, Property 1: Bug Condition**
/// **Validates: Requirements 1.1, 1.2, 1.3**
/// </summary>
[Trait("Category", "BugExploration")]
public class ExtractedPropertyWithRelatedEntityBugExplorationTests
{
    /// <summary>
    /// Test 1 (sync path): Entity with [Extracted] + [RelatedEntity] (IsMultiItemEntity = true).
    /// Assert generated code contains DateTime.Parse extraction logic in the multi-item
    /// sync FromDynamoDb path. On unfixed code, the multi-item path is missing
    /// GenerateExtractedKeyLogic, so extraction code only appears in the single-item path.
    /// 
    /// We verify the extraction comment "// Extract component properties from composite keys"
    /// appears at least twice in the output — once for the single-item path and once for
    /// the multi-item path. On unfixed code it appears only once (single-item).
    /// </summary>
    [Fact]
    public void MultiItemEntity_SyncPath_ShouldContainExtractionLogic()
    {
        // Arrange
        var entity = CreateEntityWithExtractedAndRelatedEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - the extraction comment should appear in BOTH single-item and multi-item paths
        var extractionCommentCount = CountOccurrences(result,
            "// Extract component properties from composite keys");

        extractionCommentCount.Should().BeGreaterThanOrEqualTo(2,
            "Generated code should contain extraction logic in both single-item AND multi-item sync paths, " +
            "but the bug causes the multi-item sync path (GenerateMultiItemFromDynamoDb) to omit the " +
            "GenerateExtractedKeyLogic call, so extraction only appears in the single-item path");
    }

    /// <summary>
    /// Test 2 (async path): Entity with [Extracted] + [RelatedEntity] (IsMultiItemEntity = true).
    /// Assert generated code contains DateTime.Parse extraction logic in the multi-item
    /// async FromDynamoDbAsync path. On unfixed code, the async multi-item path is also
    /// missing GenerateExtractedKeyLogic.
    ///
    /// We verify the extraction comment appears enough times to cover the async multi-item
    /// path as well. With the fix, the comment should appear in: single-item sync,
    /// single-item async (via shared logic), multi-item sync, and multi-item async — at
    /// minimum 3 times (sync single, sync multi, async multi; async single reuses sync).
    /// </summary>
    [Fact]
    public void MultiItemEntity_AsyncPath_ShouldContainExtractionLogic()
    {
        // Arrange
        var entity = CreateEntityWithExtractedAndRelatedEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - extraction logic (DateTime.Parse with the orderid parts variable)
        // should appear in the multi-item async path. The format "ORDER#{0:o}" with
        // separator '#' maps placeholder 0 to split index 1, so the generated code
        // should reference orderidParts[1].
        //
        // Count all occurrences of the DateTime.Parse extraction in the generated code.
        // On unfixed code: appears only in single-item path(s).
        // On fixed code: also appears in multi-item sync AND async paths.
        var extractionCount = CountOccurrences(result,
            "DateTime.Parse(orderidParts[1])");

        extractionCount.Should().BeGreaterThanOrEqualTo(2,
            "Generated code should contain DateTime.Parse(orderidParts[1]) in multiple paths including " +
            "the multi-item async path, but the bug causes both multi-item paths " +
            "(GenerateMultiItemFromDynamoDb and GenerateMultiItemFromDynamoDbAsync) to omit " +
            "GenerateExtractedKeyLogic, so the extraction only appears in single-item path(s)");
    }

    #region Helper Methods

    private static EntityModel CreateEntityWithExtractedAndRelatedEntity()
    {
        return new EntityModel
        {
            ClassName = "Order",
            Namespace = "TestNamespace",
            TableName = "orders",
            IsMultiItemEntity = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "StoreId",
                    AttributeName = "PK",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "STORE", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "OrderId",
                    AttributeName = "SK",
                    PropertyType = "string",
                    IsSortKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "CreationDateTime" },
                        Format = "ORDER#{0:o}",
                        Separator = "#"
                    },
                    KeyFormat = new KeyFormatModel { Prefix = null, Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "CreationDateTime",
                    PropertyType = "DateTime",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "OrderId",
                        Index = 0,
                        Separator = "#"
                    }
                }
            },
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Lines",
                    SortKeyPattern = "*#LINE#*",
                    EntityType = "OrderLine",
                    IsCollection = true,
                    PropertyType = "List<OrderLine>"
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    private static int CountOccurrences(string source, string substring)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }

    #endregion
}
