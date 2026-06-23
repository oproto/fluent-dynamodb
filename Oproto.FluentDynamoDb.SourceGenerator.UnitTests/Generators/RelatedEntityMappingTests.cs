// ============================================================================
// Related Entity Mapping Tests
// ============================================================================
// These tests verify that the source generator correctly generates related entity
// mapping code WITHOUT using MatchesEntity() checks. The fix removes the MatchesEntity()
// filter and replaces it with try/catch for graceful error handling.
//
// Requirements: 1.2, 3.1, 3.2, 3.3 from hydration-architecture-consolidation spec
// ============================================================================

using System.Collections.Immutable;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for related entity mapping code generation.
/// Verifies that MatchesEntity() is NOT used in related entity mapping,
/// and that try/catch with logging is used instead.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "hydration-architecture-consolidation")]
public class RelatedEntityMappingTests
{
    #region MatchesEntity Removal Tests

    /// <summary>
    /// Verifies that generated code for [RelatedEntity] collection mapping does NOT contain
    /// MatchesEntity() check. The fix removes this check and uses try/catch instead.
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithRelatedEntityCollection_DoesNotUseMatchesEntity()
    {
        // Arrange - Create entity with [RelatedEntity] collection
        var entity = new EntityModel
        {
            ClassName = "ParentEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            IsMultiItemEntity = true,
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
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Children",
                    SortKeyPattern = "CHILD#*",
                    PropertyType = "List<ChildEntity>",
                    IsCollection = true,
                    EntityType = "ChildEntity"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify MatchesEntity is NOT used in related entity mapping
        // The pattern "ChildEntity.MatchesEntity(item)" should NOT appear
        result.Should().NotContain("ChildEntity.MatchesEntity(item)",
            "generated code should NOT use MatchesEntity() check for related entity mapping - this was the bug");
        
        // Verify try/catch is used instead
        result.Should().Contain("try",
            "generated code should use try/catch for graceful error handling");
        result.Should().Contain("catch (Exception ex)",
            "generated code should catch exceptions during related entity deserialization");
        
        // Verify logging is present
        result.Should().Contain("LogWarning",
            "generated code should log warnings when related entity deserialization fails");
        result.Should().Contain("RelatedEntityMappingFailed",
            "generated code should use RelatedEntityMappingFailed event ID for logging");
        
        // Verify FromDynamoDb is still called
        result.Should().Contain("ChildEntity.FromDynamoDb<ChildEntity>(item, options)",
            "generated code should still call FromDynamoDb for related entity deserialization");
    }

    /// <summary>
    /// Verifies that generated code for [RelatedEntity] single mapping does NOT contain
    /// MatchesEntity() check. The fix removes this check and uses try/catch instead.
    /// 
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithRelatedEntitySingle_DoesNotUseMatchesEntity()
    {
        // Arrange - Create entity with single [RelatedEntity]
        var entity = new EntityModel
        {
            ClassName = "ParentEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            IsMultiItemEntity = true,
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
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Details",
                    SortKeyPattern = "DETAILS",
                    PropertyType = "DetailsEntity",
                    IsCollection = false,
                    EntityType = "DetailsEntity"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify MatchesEntity is NOT used in related entity mapping
        result.Should().NotContain("DetailsEntity.MatchesEntity(item)",
            "generated code should NOT use MatchesEntity() check for single related entity mapping");
        
        // Verify try/catch is used instead
        result.Should().Contain("try",
            "generated code should use try/catch for graceful error handling");
        result.Should().Contain("catch (Exception ex)",
            "generated code should catch exceptions during related entity deserialization");
        
        // Verify FromDynamoDb is still called
        result.Should().Contain("DetailsEntity.FromDynamoDb<DetailsEntity>(item, options)",
            "generated code should still call FromDynamoDb for single related entity deserialization");
    }

    /// <summary>
    /// Verifies that generated code for multiple [RelatedEntity] attributes does NOT contain
    /// any MatchesEntity() checks.
    /// 
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithMultipleRelatedEntities_DoesNotUseMatchesEntity()
    {
        // Arrange - Create entity with multiple [RelatedEntity] attributes
        var entity = new EntityModel
        {
            ClassName = "OrderEntity",
            Namespace = "TestNamespace",
            TableName = "orders",
            IsMultiItemEntity = true,
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
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Lines",
                    SortKeyPattern = "LINE#*",
                    PropertyType = "List<OrderLine>",
                    IsCollection = true,
                    EntityType = "OrderLine"
                },
                new RelationshipModel
                {
                    PropertyName = "Payments",
                    SortKeyPattern = "PAYMENT#*",
                    PropertyType = "List<Payment>",
                    IsCollection = true,
                    EntityType = "Payment"
                },
                new RelationshipModel
                {
                    PropertyName = "ShippingInfo",
                    SortKeyPattern = "SHIPPING",
                    PropertyType = "ShippingInfo",
                    IsCollection = false,
                    EntityType = "ShippingInfo"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify NO MatchesEntity calls for any related entity
        result.Should().NotContain("OrderLine.MatchesEntity(item)",
            "generated code should NOT use MatchesEntity() for OrderLine");
        result.Should().NotContain("Payment.MatchesEntity(item)",
            "generated code should NOT use MatchesEntity() for Payment");
        result.Should().NotContain("ShippingInfo.MatchesEntity(item)",
            "generated code should NOT use MatchesEntity() for ShippingInfo");
        
        // Verify all FromDynamoDb calls are present
        result.Should().Contain("OrderLine.FromDynamoDb<OrderLine>(item, options)",
            "generated code should call FromDynamoDb for OrderLine");
        result.Should().Contain("Payment.FromDynamoDb<Payment>(item, options)",
            "generated code should call FromDynamoDb for Payment");
        result.Should().Contain("ShippingInfo.FromDynamoDb<ShippingInfo>(item, options)",
            "generated code should call FromDynamoDb for ShippingInfo");
    }

    #endregion

    #region Inferred Type Mapping Tests (EntityType = null)

    /// <summary>
    /// Verifies that when EntityType is null and the relationship is a collection,
    /// the generated code infers the element type from PropertyType and uses FromDynamoDb
    /// rather than emitting a TODO stub or creating an empty instance.
    /// 
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithCollectionRelationship_EntityTypeNull_UsesFromDynamoDb()
    {
        // Arrange - Create entity with [RelatedEntity] collection where EntityType is NOT specified
        var entity = new EntityModel
        {
            ClassName = "ParentEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            IsMultiItemEntity = true,
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
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Children",
                    SortKeyPattern = "CHILD#*",
                    EntityType = null,
                    IsCollection = true,
                    PropertyType = "List<ChildEntity>",
                    ChildEntityHasRelationships = false,
                    ChildEntityRelationships = Array.Empty<RelationshipModel>()
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify FromDynamoDb is called with the inferred element type
        result.Should().Contain("ChildEntity.FromDynamoDb<ChildEntity>(item, options)",
            "generated code should call FromDynamoDb using the element type inferred from the collection PropertyType");

        // Verify no TODO stubs remain
        result.Should().NotContain("// TODO",
            "generated code should not contain TODO comments - the stub should be replaced with actual deserialization");

        // Verify no empty instance creation
        result.Should().NotContain("new ChildEntity()",
            "generated code should not create empty instances - it should deserialize using FromDynamoDb");

        // Verify try/catch with logging is present
        result.Should().Contain("try",
            "generated code should use try/catch for graceful error handling");
        result.Should().Contain("catch (Exception ex)",
            "generated code should catch exceptions during related entity deserialization");
        result.Should().Contain("LogWarning",
            "generated code should log warnings when deserialization fails");
        result.Should().Contain("RelatedEntityMappingFailed",
            "generated code should use RelatedEntityMappingFailed event ID");
    }

    /// <summary>
    /// Verifies that when EntityType is null and the relationship is a single entity (not a collection),
    /// the generated code infers the property type and uses FromDynamoDb
    /// rather than emitting a TODO stub or creating an empty instance.
    /// 
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithSingleRelationship_EntityTypeNull_UsesFromDynamoDb()
    {
        // Arrange - Create entity with [RelatedEntity] single where EntityType is NOT specified
        var entity = new EntityModel
        {
            ClassName = "ParentEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            IsMultiItemEntity = true,
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
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Summary",
                    SortKeyPattern = "SUMMARY",
                    EntityType = null,
                    IsCollection = false,
                    PropertyType = "SummaryInfo",
                    ChildEntityHasRelationships = false,
                    ChildEntityRelationships = Array.Empty<RelationshipModel>()
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify FromDynamoDb is called with the inferred property type
        result.Should().Contain("SummaryInfo.FromDynamoDb<SummaryInfo>(item, options)",
            "generated code should call FromDynamoDb using the property type inferred from the single entity PropertyType");

        // Verify no TODO stubs remain
        result.Should().NotContain("// TODO",
            "generated code should not contain TODO comments - the stub should be replaced with actual deserialization");

        // Verify no empty instance creation
        result.Should().NotContain("new SummaryInfo()",
            "generated code should not create empty instances - it should deserialize using FromDynamoDb");

        // Verify try/catch error handling is present
        result.Should().Contain("try",
            "generated code should use try/catch for graceful error handling");
        result.Should().Contain("catch (Exception ex)",
            "generated code should catch exceptions during related entity deserialization");
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Verifies that generated code includes proper error handling with logging
    /// for related entity deserialization failures.
    /// 
    /// **Validates: Requirements 3.3, 6.1, 6.4**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithRelatedEntity_IncludesErrorLogging()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "ParentEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            IsMultiItemEntity = true,
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
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Items",
                    SortKeyPattern = "ITEM#*",
                    PropertyType = "List<ItemEntity>",
                    IsCollection = true,
                    EntityType = "ItemEntity"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify error logging includes entity type and sort key
        result.Should().Contain("Failed to deserialize related entity",
            "error message should describe the failure");
        result.Should().Contain("EntityType",
            "error message should include entity type placeholder");
        result.Should().Contain("SortKey",
            "error message should include sort key placeholder");
        result.Should().Contain("Error",
            "error message should include error details placeholder");
        result.Should().Contain("// Skip this item and continue processing",
            "generated code should include comment explaining skip behavior");
    }

    #endregion

    #region Compilation Verification Tests

    /// <summary>
    /// Verifies that the generated code compiles successfully with the new
    /// try/catch pattern for related entity mapping.
    /// 
    /// **Validates: Requirements 1.2, 3.1, 3.2, 3.3**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithRelatedEntity_CompilesSuccessfully()
    {
        // Arrange - Full source code test using source generator
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true)]
    public partial class Invoice
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [RelatedEntity(""INVOICE#*#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act - Generate code
        var result = GenerateCode(source);

        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "source generator should not produce errors for valid composite entity");

        // Get the generated code
        var invoiceCode = GetGeneratedSource(result, "Invoice.g.cs");
        var lineCode = GetGeneratedSource(result, "InvoiceLine.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(invoiceCode, source, lineCode);

        // Verify MatchesEntity is NOT used
        invoiceCode.Should().NotContain("InvoiceLine.MatchesEntity(item)",
            "generated code should NOT use MatchesEntity() check");
        
        // Verify try/catch is used
        invoiceCode.Should().Contain("try",
            "generated code should use try/catch for error handling");
        invoiceCode.Should().Contain("catch (Exception",
            "generated code should catch exceptions");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static RelatedEntityTestResult GenerateCode(string source)
    {
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new RelatedEntityGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new RelatedEntityTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(RelatedEntityTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        source.Should().NotBeNull($"Expected to find generated source containing '{fileNamePart}'");
        return source!.SourceText.ToString();
    }

    #endregion
}

/// <summary>
/// Result from running the source generator for related entity tests.
/// </summary>
internal class RelatedEntityTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required RelatedEntityGeneratedSource[] GeneratedSources { get; set; }
}

/// <summary>
/// Represents a generated source file for related entity tests.
/// </summary>
internal class RelatedEntityGeneratedSource
{
    public RelatedEntityGeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}
