using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Preservation tests for the extracted property with related entity fix.
/// These tests verify EXISTING working behavior that must NOT change after the fix:
/// - Single-item entities with [Extracted] properties correctly generate extraction logic (sync path)
/// - Multi-item entities without [Extracted] properties do not generate unnecessary extraction code
/// - Single-item entities with [Extracted] properties correctly generate extraction logic (async path)
///
/// These tests MUST PASS on both unfixed and fixed code.
///
/// **Feature: extracted-properties-related-entity-fix, Property 2: Preservation**
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
/// </summary>
[Trait("Category", "Preservation")]
public class ExtractedPropertyWithRelatedEntityPreservationTests
{
    /// <summary>
    /// Test 1: Single-item entity (no [RelatedEntity], IsMultiItemEntity = false) with
    /// [Extracted] DateTime property. Assert generated sync FromDynamoDb code contains
    /// the extraction logic (DateTime.Parse from split parts).
    ///
    /// This confirms single-item sync extraction currently works and must continue working.
    /// Validates: Requirement 3.1
    /// </summary>
    [Fact]
    public void SingleItemEntity_SyncPath_ShouldContainExtractionLogic()
    {
        // Arrange
        var entity = CreateSingleItemEntityWithExtractedProperty();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - single-item sync path should contain extraction logic
        result.Should().Contain("// Extract component properties from composite keys",
            "Single-item sync FromDynamoDb should generate the extraction comment");
        result.Should().Contain("DateTime.Parse(orderidParts[1])",
            "Single-item sync FromDynamoDb should generate DateTime.Parse extraction for the CreationDateTime property");
    }

    /// <summary>
    /// Test 2: Multi-item entity (has [RelatedEntity], IsMultiItemEntity = true) with
    /// NO [Extracted] properties. Assert generated code does NOT contain the extraction
    /// comment, confirming multi-item entities without extracted properties are unaffected.
    ///
    /// Validates: Requirement 3.3
    /// </summary>
    [Fact]
    public void MultiItemEntity_NoExtractedProperties_ShouldNotContainExtractionLogic()
    {
        // Arrange
        var entity = CreateMultiItemEntityWithoutExtractedProperties();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - no extraction code should be generated when there are no extracted properties
        result.Should().NotContain("// Extract component properties from composite keys",
            "Multi-item entities without [Extracted] properties should not generate any extraction logic");
    }

    /// <summary>
    /// Test 3: Single-item entity (no [RelatedEntity], IsMultiItemEntity = false) with
    /// [Extracted] DateTime property. Assert the async single-item path delegates to the
    /// sync path, ensuring extraction logic is reachable from the async code path.
    ///
    /// For entities without blob storage or encryption, the source generator creates an
    /// async delegating method that calls the sync FromDynamoDb (which contains the
    /// extraction logic). This test verifies:
    /// 1. The sync path has extraction logic (confirmed by Test 1)
    /// 2. The async path delegates to the sync path via Task.FromResult(FromDynamoDb(...))
    /// Together these confirm the async single-item path correctly reaches extraction.
    ///
    /// Validates: Requirement 3.2
    /// </summary>
    [Fact]
    public void SingleItemEntity_AsyncPath_ShouldDelegateToSyncWithExtraction()
    {
        // Arrange
        var entity = CreateSingleItemEntityWithExtractedProperty();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - the async delegating method should exist and delegate to sync FromDynamoDb
        result.Should().Contain("Task.FromResult(FromDynamoDb<TSelf>(item, options))",
            "Single-item async path should delegate to sync FromDynamoDb which contains extraction logic");

        // Also verify the sync path has extraction (baseline confirmation)
        result.Should().Contain("// Extract component properties from composite keys",
            "Sync path must contain extraction logic that the async path delegates to");
        result.Should().Contain("DateTime.Parse(orderidParts[1])",
            "Sync path must contain DateTime.Parse extraction that the async path will use via delegation");
    }

    #region Helper Methods

    /// <summary>
    /// Creates a single-item entity with [Extracted] properties but NO [RelatedEntity].
    /// Same property structure as the bug exploration tests but with IsMultiItemEntity = false
    /// and no relationships.
    /// </summary>
    private static EntityModel CreateSingleItemEntityWithExtractedProperty()
    {
        return new EntityModel
        {
            ClassName = "Order",
            Namespace = "TestNamespace",
            TableName = "orders",
            IsMultiItemEntity = false,
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
            Relationships = Array.Empty<RelationshipModel>(),
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

    /// <summary>
    /// Creates a multi-item entity with [RelatedEntity] but NO [Extracted] properties.
    /// This entity has IsMultiItemEntity = true and relationships, but none of its
    /// properties use ExtractedKey.
    /// </summary>
    private static EntityModel CreateMultiItemEntityWithoutExtractedProperties()
    {
        return new EntityModel
        {
            ClassName = "Invoice",
            Namespace = "TestNamespace",
            TableName = "invoices",
            IsMultiItemEntity = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "CustomerId",
                    AttributeName = "PK",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "CUSTOMER", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "InvoiceId",
                    AttributeName = "SK",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "INVOICE", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "InvoiceNumber",
                    AttributeName = "invoiceNumber",
                    PropertyType = "string"
                }
            },
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Lines",
                    SortKeyPattern = "INVOICE#*#LINE#*",
                    EntityType = "InvoiceLine",
                    IsCollection = true,
                    PropertyType = "List<InvoiceLine>"
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
