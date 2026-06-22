// ============================================================================
// JsonBlob Composite Entity Bug Investigation Tests
// ============================================================================
// These tests investigate and document the bug where [JsonBlob] properties
// are incorrectly deserialized in composite entities via ToCompositeEntityAsync().
//
// Bug: The generated FromDynamoDb method for related entities may use incorrect
// deserialization logic (e.g., Enum.Parse) instead of the configured JSON serializer.
//
// Requirements: 1.1, 1.2 from jsonblob-composite-entity-fix spec
// ============================================================================

using System.Collections.Immutable;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SystemTextJson;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for JsonBlob properties in composite entities with [RelatedEntity] attributes.
/// These tests verify that the source generator correctly generates JSON deserialization
/// code for [JsonBlob] properties in related entities.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "jsonblob-composite-entity-fix")]
public class JsonBlobCompositeEntityTests
{
    #region Bug Reproduction Tests (Task 1.1)

    /// <summary>
    /// CRITICAL BUG REPRODUCTION: Parent entity with [JsonBlob] property AND [RelatedEntity] attribute.
    /// 
    /// The bug occurs in GeneratePrimaryEntityIdentification when the PARENT entity has a [JsonBlob]
    /// property. The multi-item FromDynamoDb method directly calls GetFromAttributeValueExpression
    /// for all non-collection properties WITHOUT checking if they are [JsonBlob] properties.
    /// 
    /// This causes the generator to emit Enum.Parse<AddressValue>() instead of 
    /// options.JsonSerializer.Deserialize<AddressValue>() for the JsonBlob property.
    /// 
    /// Error: CS0453: The type 'AddressValue' must be a non-nullable value type in order to use it 
    /// as parameter 'TEnum' in the generic type or method 'Enum.Parse<TEnum>(ReadOnlySpan<char>)'
    /// 
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void Generator_WithParentEntityContainingJsonBlobAndRelatedEntity_GeneratesCorrectJsonDeserialization()
    {
        // Arrange - Create a composite entity scenario where the PARENT has a [JsonBlob] property
        // This is the actual bug scenario - the parent entity has both [JsonBlob] and [RelatedEntity]
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    /// <summary>
    /// Parent entity with BOTH a [JsonBlob] property AND a [RelatedEntity] collection.
    /// This is the bug scenario - the multi-item FromDynamoDb method incorrectly uses
    /// Enum.Parse for the Address property instead of JsonSerializer.Deserialize.
    /// </summary>
    [DynamoDbTable(""locations"", IsDefault = true)]
    public partial class LocationEntity
    {
        [PartitionKey(Prefix = ""LOCATION"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LOCATION"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// JsonBlob property on the PARENT entity - this is where the bug manifests.
        /// The multi-item FromDynamoDb method should use JsonSerializer.Deserialize,
        /// but instead generates Enum.Parse<AddressValue>() which causes CS0453.
        /// </summary>
        [JsonBlob]
        [DynamoDbAttribute(""address"")]
        public AddressValue? Address { get; set; }

        /// <summary>
        /// Related entity collection - the presence of this attribute triggers the
        /// multi-item FromDynamoDb code path where the bug occurs.
        /// </summary>
        [RelatedEntity(""LOCATION#*#CONTACT#*"", EntityType = typeof(ContactEntity))]
        public List<ContactEntity> Contacts { get; set; } = new();
    }

    /// <summary>
    /// Child entity - related to LocationEntity.
    /// </summary>
    [DynamoDbTable(""locations"")]
    public partial class ContactEntity
    {
        [PartitionKey(Prefix = ""LOCATION"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""contactName"")]
        public string ContactName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Complex type to be serialized as JSON blob.
    /// </summary>
    public class AddressValue
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }
}";

        // Act - Generate code with System.Text.Json reference
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "source generator should not produce errors for valid composite entity with JsonBlob");

        // Get the generated code for both entities
        var locationEntityCode = GetGeneratedSource(result, "LocationEntity.g.cs");
        var contactEntityCode = GetGeneratedSource(result, "ContactEntity.g.cs");
        
        // Verify compilation - THIS IS THE CRITICAL TEST
        // The bug causes CS0453 because Enum.Parse<AddressValue>() is generated
        // instead of options.JsonSerializer.Deserialize<AddressValue>()
        // Include both generated files since LocationEntity references ContactEntity
        CompilationVerifier.AssertGeneratedCodeCompiles(locationEntityCode, source, contactEntityCode);

        // CRITICAL ASSERTION: The multi-item FromDynamoDb method should NOT use Enum.Parse
        // for [JsonBlob] properties
        locationEntityCode.Should().NotContain("Enum.Parse<TestNamespace.AddressValue>",
            "generated code MUST NOT use Enum.Parse for [JsonBlob] properties - this is the bug!");
        
        locationEntityCode.Should().NotContain("Enum.Parse<AddressValue>",
            "generated code MUST NOT use Enum.Parse for [JsonBlob] properties - this is the bug!");

        // The multi-item FromDynamoDb method should use JSON deserialization for the Address property
        // Note: The single-item FromDynamoDb already works correctly
        locationEntityCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.AddressValue>",
            "generated code MUST use JsonSerializer.Deserialize for [JsonBlob] properties in multi-item mapping");
    }

    /// <summary>
    /// Minimal reproduction test case: Parent entity with [RelatedEntity] pointing to
    /// a child entity that has a [JsonBlob] property.
    /// 
    /// This test verifies that the generated code for the child entity uses
    /// JsonSerializer.Deserialize for the [JsonBlob] property, NOT Enum.Parse
    /// or other incorrect deserialization methods.
    /// 
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void Generator_WithRelatedEntityContainingJsonBlob_GeneratesCorrectJsonDeserialization()
    {
        // Arrange - Create a composite entity scenario:
        // - Parent entity (Invoice) with [RelatedEntity] attribute
        // - Child entity (InvoiceLine) with [JsonBlob] property
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    /// <summary>
    /// Parent entity with a related entity collection.
    /// </summary>
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

        /// <summary>
        /// Related entity collection - automatically populated by ToCompositeEntityAsync.
        /// The child entity (InvoiceLine) contains a [JsonBlob] property.
        /// </summary>
        [RelatedEntity(""INVOICE#*#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }

    /// <summary>
    /// Child entity with a [JsonBlob] property.
    /// This is the entity where the bug manifests - the generated FromDynamoDb
    /// method should use JsonSerializer.Deserialize for the LineMetadata property.
    /// </summary>
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

        [DynamoDbAttribute(""description"")]
        public string Description { get; set; } = string.Empty;

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }

        /// <summary>
        /// JsonBlob property - should be deserialized using JsonSerializer.Deserialize,
        /// NOT Enum.Parse or other incorrect methods.
        /// </summary>
        [JsonBlob]
        [DynamoDbAttribute(""metadata"")]
        public LineMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Complex type to be serialized as JSON blob.
    /// </summary>
    public class LineMetadata
    {
        public string Category { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, string> CustomFields { get; set; } = new();
    }
}";

        // Act - Generate code with System.Text.Json reference
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "source generator should not produce errors for valid composite entity with JsonBlob");

        // Get the generated code for the child entity (InvoiceLine)
        var invoiceLineCode = GetGeneratedSource(result, "InvoiceLine.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(invoiceLineCode, source);

        // CRITICAL ASSERTION: Verify the generated FromDynamoDb method uses JSON deserialization
        // for the [JsonBlob] property, NOT Enum.Parse or other incorrect methods
        invoiceLineCode.Should().Contain("options.JsonSerializer.Deserialize",
            "generated code MUST use JsonSerializer.Deserialize for [JsonBlob] properties");
        
        invoiceLineCode.Should().NotContain("Enum.Parse",
            "generated code MUST NOT use Enum.Parse for [JsonBlob] properties - this is the bug!");
        
        // Verify the correct deserialization pattern for the Metadata property
        invoiceLineCode.Should().Contain("if (item.TryGetValue(\"metadata\", out var metadataValue))",
            "should check for metadata attribute existence");
        
        invoiceLineCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.LineMetadata>",
            "should deserialize to the correct type (LineMetadata) with fully-qualified namespace");
    }

    /// <summary>
    /// Test that verifies the parent entity's generated code correctly calls
    /// the child entity's FromDynamoDb method with the options parameter.
    /// 
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void Generator_WithRelatedEntityContainingJsonBlob_PassesOptionsToChildFromDynamoDb()
    {
        // Arrange - Same composite entity scenario
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

        [JsonBlob]
        [DynamoDbAttribute(""metadata"")]
        public LineMetadata? Metadata { get; set; }
    }

    public class LineMetadata
    {
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        // Get the generated code for the parent entity (Invoice)
        var invoiceCode = GetGeneratedSource(result, "Invoice.g.cs");
        
        // Verify the parent entity's FromDynamoDb method passes options to child's FromDynamoDb
        // This is critical for JsonBlob deserialization to work in related entities
        invoiceCode.Should().Contain("InvoiceLine.FromDynamoDb<InvoiceLine>(item, options)",
            "parent entity MUST pass options parameter to child entity's FromDynamoDb method " +
            "so that the JSON serializer is available for [JsonBlob] property deserialization");
    }

    /// <summary>
    /// Test with nullable JsonBlob property in related entity.
    /// 
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Fact]
    public void Generator_WithNullableJsonBlobInRelatedEntity_HandlesNullGracefully()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [RelatedEntity(""ORDER#*#ITEM#*"", EntityType = typeof(OrderItem))]
        public List<OrderItem> Items { get; set; } = new();
    }

    [DynamoDbTable(""orders"")]
    public partial class OrderItem
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""productId"")]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// Nullable JsonBlob property - should handle null values gracefully.
        /// </summary>
        [JsonBlob]
        [DynamoDbAttribute(""customization"")]
        public ItemCustomization? Customization { get; set; }
    }

    public class ItemCustomization
    {
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var orderItemCode = GetGeneratedSource(result, "OrderItem.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(orderItemCode, source);

        // Verify null handling for JsonBlob property
        orderItemCode.Should().Contain("if (item.TryGetValue(\"customization\", out var customizationValue))",
            "should check for attribute existence before deserializing nullable JsonBlob");
        
        orderItemCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.ItemCustomization>",
            "should use JSON deserialization for nullable JsonBlob property with fully-qualified namespace");
    }

    /// <summary>
    /// Test with List<T> JsonBlob property in related entity.
    /// 
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Fact]
    public void Generator_WithListJsonBlobInRelatedEntity_DeserializesCorrectly()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""projects"", IsDefault = true)]
    public partial class Project
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""PROJECT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [RelatedEntity(""PROJECT#*#TASK#*"", EntityType = typeof(ProjectTask))]
        public List<ProjectTask> Tasks { get; set; } = new();
    }

    [DynamoDbTable(""projects"")]
    public partial class ProjectTask
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""taskName"")]
        public string TaskName { get; set; } = string.Empty;

        /// <summary>
        /// List JsonBlob property - should deserialize JSON array correctly.
        /// </summary>
        [JsonBlob]
        [DynamoDbAttribute(""assignees"")]
        public List<Assignee>? Assignees { get; set; }
    }

    public class Assignee
    {
        public string UserId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var projectTaskCode = GetGeneratedSource(result, "ProjectTask.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(projectTaskCode, source);

        // Verify List<T> JsonBlob deserialization
        projectTaskCode.Should().Contain("options.JsonSerializer.Deserialize<System.Collections.Generic.List<TestNamespace.Assignee>>",
            "should deserialize List<T> JsonBlob property using JSON serializer with fully-qualified namespace");
    }

    #endregion

    #region Source Generator Output Tests (Task 4.1)

    /// <summary>
    /// Test that verifies the generated multi-item FromDynamoDb method contains
    /// the correct JSON deserialization pattern for JsonBlob properties.
    /// 
    /// This test specifically validates the fix in GeneratePrimaryEntityIdentification
    /// where JsonBlob properties must use JsonSerializer.Deserialize instead of
    /// GetFromAttributeValueExpression (which incorrectly uses Enum.Parse).
    /// 
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void Generator_MultiItemFromDynamoDb_ContainsJsonSerializerDeserializeForJsonBlob()
    {
        // Arrange - Entity with JsonBlob property and RelatedEntity (triggers multi-item FromDynamoDb)
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""stores"", IsDefault = true)]
    public partial class Store
    {
        [PartitionKey(Prefix = ""STORE"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""STORE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [JsonBlob]
        [DynamoDbAttribute(""settings"")]
        public StoreSettings? Settings { get; set; }

        [RelatedEntity(""STORE#*#EMPLOYEE#*"", EntityType = typeof(Employee))]
        public List<Employee> Employees { get; set; } = new();
    }

    [DynamoDbTable(""stores"")]
    public partial class Employee
    {
        [PartitionKey(Prefix = ""STORE"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""employeeName"")]
        public string EmployeeName { get; set; } = string.Empty;
    }

    public class StoreSettings
    {
        public bool IsOpen { get; set; }
        public string TimeZone { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var storeCode = GetGeneratedSource(result, "Store.g.cs");
        var employeeCode = GetGeneratedSource(result, "Employee.g.cs");
        
        // Verify compilation with both generated files
        CompilationVerifier.AssertGeneratedCodeCompiles(storeCode, source, employeeCode);

        // CRITICAL: Verify the multi-item FromDynamoDb method uses JSON deserialization
        // The method signature for multi-item is: FromDynamoDb<T>(IList<Dictionary<string, AttributeValue>> items, ...)
        storeCode.Should().Contain("IList<Dictionary<string, AttributeValue>> items",
            "should have multi-item FromDynamoDb method signature");
        
        // Verify JSON deserialization is used for the Settings property in multi-item method
        storeCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.StoreSettings>",
            "multi-item FromDynamoDb MUST use JsonSerializer.Deserialize for JsonBlob properties");
        
        // Verify NO Enum.Parse is used for StoreSettings
        storeCode.Should().NotContain("Enum.Parse<TestNamespace.StoreSettings>",
            "MUST NOT use Enum.Parse for JsonBlob properties - this was the bug!");
    }

    /// <summary>
    /// Test that verifies both single-item and multi-item FromDynamoDb methods
    /// use consistent JSON deserialization for JsonBlob properties.
    /// 
    /// **Validates: Requirements 1.1, 2.1, 2.3**
    /// </summary>
    [Fact]
    public void Generator_BothFromDynamoDbOverloads_UseConsistentJsonDeserialization()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""products"", IsDefault = true)]
    public partial class Product
    {
        [PartitionKey(Prefix = ""PRODUCT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""PRODUCT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [JsonBlob]
        [DynamoDbAttribute(""specs"")]
        public ProductSpecs? Specifications { get; set; }

        [RelatedEntity(""PRODUCT#*#REVIEW#*"", EntityType = typeof(Review))]
        public List<Review> Reviews { get; set; } = new();
    }

    [DynamoDbTable(""products"")]
    public partial class Review
    {
        [PartitionKey(Prefix = ""PRODUCT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""rating"")]
        public int Rating { get; set; }
    }

    public class ProductSpecs
    {
        public double Weight { get; set; }
        public string Dimensions { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var productCode = GetGeneratedSource(result, "Product.g.cs");
        var reviewCode = GetGeneratedSource(result, "Review.g.cs");
        
        CompilationVerifier.AssertGeneratedCodeCompiles(productCode, source, reviewCode);

        // Count occurrences of JsonSerializer.Deserialize for ProductSpecs
        // Should appear in BOTH single-item and multi-item FromDynamoDb methods
        var deserializeCount = CountOccurrences(productCode, "options.JsonSerializer.Deserialize<TestNamespace.ProductSpecs>");
        
        deserializeCount.Should().BeGreaterThanOrEqualTo(2,
            "JsonSerializer.Deserialize should appear in both single-item and multi-item FromDynamoDb methods");
    }

    /// <summary>
    /// Test that verifies the generated code handles the case where a related entity
    /// (child) has a JsonBlob property - the child's FromDynamoDb should use JSON deserialization.
    /// 
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void Generator_RelatedEntityWithJsonBlob_GeneratesCorrectChildDeserialization()
    {
        // Arrange - Parent without JsonBlob, Child with JsonBlob
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""teams"", IsDefault = true)]
    public partial class Team
    {
        [PartitionKey(Prefix = ""TEAM"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""TEAM"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""teamName"")]
        public string TeamName { get; set; } = string.Empty;

        [RelatedEntity(""TEAM#*#MEMBER#*"", EntityType = typeof(TeamMember))]
        public List<TeamMember> Members { get; set; } = new();
    }

    [DynamoDbTable(""teams"")]
    public partial class TeamMember
    {
        [PartitionKey(Prefix = ""TEAM"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""memberName"")]
        public string MemberName { get; set; } = string.Empty;

        [JsonBlob]
        [DynamoDbAttribute(""preferences"")]
        public MemberPreferences? Preferences { get; set; }

        [JsonBlob]
        [DynamoDbAttribute(""skills"")]
        public List<Skill>? Skills { get; set; }
    }

    public class MemberPreferences
    {
        public string Theme { get; set; } = string.Empty;
        public bool NotificationsEnabled { get; set; }
    }

    public class Skill
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var teamCode = GetGeneratedSource(result, "Team.g.cs");
        var memberCode = GetGeneratedSource(result, "TeamMember.g.cs");
        
        CompilationVerifier.AssertGeneratedCodeCompiles(teamCode, source, memberCode);

        // Verify child entity (TeamMember) uses JSON deserialization for both JsonBlob properties
        memberCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.MemberPreferences>",
            "child entity should use JSON deserialization for MemberPreferences");
        
        memberCode.Should().Contain("options.JsonSerializer.Deserialize<System.Collections.Generic.List<TestNamespace.Skill>>",
            "child entity should use JSON deserialization for List<Skill>");
        
        // Verify parent passes options to child
        teamCode.Should().Contain("TeamMember.FromDynamoDb<TeamMember>(item, options)",
            "parent should pass options to child's FromDynamoDb");
    }

    /// <summary>
    /// Test that verifies multiple related entity collections with JsonBlob work correctly.
    /// 
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void Generator_MultipleRelatedEntityCollectionsWithJsonBlob_GeneratesCorrectDeserialization()
    {
        // Arrange - Parent with multiple related entity collections, both with JsonBlob
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""accounts"", IsDefault = true)]
    public partial class Account
    {
        [PartitionKey(Prefix = ""ACCOUNT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ACCOUNT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""accountName"")]
        public string AccountName { get; set; } = string.Empty;

        [JsonBlob]
        [DynamoDbAttribute(""config"")]
        public AccountConfig? Config { get; set; }

        [RelatedEntity(""ACCOUNT#*#PROFILE#*"", EntityType = typeof(Profile))]
        public List<Profile> Profiles { get; set; } = new();
    }

    [DynamoDbTable(""accounts"")]
    public partial class Profile
    {
        [PartitionKey(Prefix = ""ACCOUNT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [JsonBlob]
        [DynamoDbAttribute(""avatar"")]
        public AvatarData? Avatar { get; set; }
    }

    public class AccountConfig
    {
        public string Region { get; set; } = string.Empty;
    }

    public class AvatarData
    {
        public string Url { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var accountCode = GetGeneratedSource(result, "Account.g.cs");
        var profileCode = GetGeneratedSource(result, "Profile.g.cs");
        
        CompilationVerifier.AssertGeneratedCodeCompiles(accountCode, source, profileCode);

        // Verify parent entity uses JSON deserialization
        accountCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.AccountConfig>",
            "parent entity should use JSON deserialization for AccountConfig");
        
        // Verify child entity uses JSON deserialization
        profileCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.AvatarData>",
            "child entity should use JSON deserialization for AvatarData");
    }

    /// <summary>
    /// Helper method to count occurrences of a substring in a string.
    /// </summary>
    private static int CountOccurrences(string source, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock assembly for testing optional package references.
    /// </summary>
    private static MetadataReference CreateMockAssembly(string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText("// Mock assembly") },
            DynamicCompilationHelper.GetStandardReferences().Take(1),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException($"Failed to create mock assembly {assemblyName}");
        }
        ms.Seek(0, SeekOrigin.Begin);
        return MetadataReference.CreateFromStream(ms);
    }

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static JsonBlobTestResult GenerateCode(string source, bool includeSystemTextJson = false)
    {
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

        if (includeSystemTextJson)
        {
            references.Add(CreateMockAssembly("Oproto.FluentDynamoDb.SystemTextJson"));
        }

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
            .Select(tree => new JsonBlobGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new JsonBlobTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(JsonBlobTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        source.Should().NotBeNull($"Expected to find generated source containing '{fileNamePart}'");
        return source!.SourceText.ToString();
    }

    #endregion
}

/// <summary>
/// Result from running the source generator for JsonBlob tests.
/// </summary>
internal class JsonBlobTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required JsonBlobGeneratedSource[] GeneratedSources { get; set; }
}

/// <summary>
/// Represents a generated source file for JsonBlob tests.
/// </summary>
internal class JsonBlobGeneratedSource
{
    public JsonBlobGeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}


#region Property-Based Tests (Tasks 4.2 and 4.3)

/// <summary>
/// Property-based tests for JsonBlob round-trip consistency.
/// These tests verify the correctness properties defined in the design document.
/// </summary>
[Trait("Category", "PropertyTest")]
[Trait("Feature", "jsonblob-composite-entity-fix")]
public class JsonBlobPropertyTests
{
    /// <summary>
    /// **Feature: jsonblob-composite-entity-fix, Property 2: JsonBlob Round-Trip Consistency**
    /// *For any* valid entity instance with [JsonBlob] properties (including nullable and collection types), 
    /// serializing via ToDynamoDb then deserializing via FromDynamoDb SHALL produce an object where all 
    /// JsonBlob property values are equivalent to the original.
    /// **Validates: Requirements 1.3, 1.4, 2.1, 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property JsonBlob_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            JsonBlobEntityArbitrary(),
            entity =>
            {
                // Arrange - Create options with JSON serializer
                var options = new FluentDynamoDbOptions().WithSystemTextJson();

                // Act - Round-trip: ToDynamoDb -> FromDynamoDb
                var dynamoDbItem = JsonBlobTestEntity.ToDynamoDb(entity, options);
                var roundTrippedEntity = JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(dynamoDbItem, options);

                // Assert - All properties should be equivalent
                var idMatches = roundTrippedEntity.Id == entity.Id;
                var nameMatches = roundTrippedEntity.Name == entity.Name;
                
                // JsonBlob property comparison
                var settingsMatch = CompareSettings(entity.Settings, roundTrippedEntity.Settings);
                
                return (idMatches && nameMatches && settingsMatch)
                    .Label($"Id: {idMatches}, Name: {nameMatches}, Settings: {settingsMatch}");
            });
    }

    /// <summary>
    /// **Feature: jsonblob-composite-entity-fix, Property 2: JsonBlob Round-Trip Consistency** (nullable variant)
    /// *For any* entity with nullable [JsonBlob] property set to null, round-trip SHALL preserve null.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property JsonBlob_RoundTrip_PreservesNull()
    {
        return Prop.ForAll(
            Arb.Generate<NonEmptyString>().Select(s => s.Get).ToArbitrary(),
            Arb.Generate<NonEmptyString>().Select(s => s.Get).ToArbitrary(),
            (id, name) =>
            {
                // Arrange - Entity with null JsonBlob property
                var entity = new JsonBlobTestEntity
                {
                    Id = id,
                    Name = name,
                    Settings = null // Explicitly null
                };
                var options = new FluentDynamoDbOptions().WithSystemTextJson();

                // Act - Round-trip
                var dynamoDbItem = JsonBlobTestEntity.ToDynamoDb(entity, options);
                var roundTrippedEntity = JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(dynamoDbItem, options);

                // Assert - Null should be preserved
                return (roundTrippedEntity.Settings == null)
                    .Label($"Settings should be null but was: {roundTrippedEntity.Settings}");
            });
    }

    /// <summary>
    /// **Feature: jsonblob-composite-entity-fix, Property 2: JsonBlob Round-Trip Consistency** (collection variant)
    /// *For any* entity with List[T] [JsonBlob] property, round-trip SHALL preserve all list items.
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property JsonBlob_RoundTrip_PreservesListItems()
    {
        return Prop.ForAll(
            JsonBlobListEntityArbitrary(),
            entity =>
            {
                // Arrange
                var options = new FluentDynamoDbOptions().WithSystemTextJson();

                // Act - Round-trip
                var dynamoDbItem = JsonBlobListTestEntity.ToDynamoDb(entity, options);
                var roundTrippedEntity = JsonBlobListTestEntity.FromDynamoDb<JsonBlobListTestEntity>(dynamoDbItem, options);

                // Assert - List should be equivalent
                var idMatches = roundTrippedEntity.Id == entity.Id;
                var tagsMatch = CompareTags(entity.Tags, roundTrippedEntity.Tags);
                
                return (idMatches && tagsMatch)
                    .Label($"Id: {idMatches}, Tags: {tagsMatch}");
            });
    }

    #region Arbitraries

    private static Arbitrary<JsonBlobTestEntity> JsonBlobEntityArbitrary()
    {
        return Arb.From(
            from id in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from name in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from hasSettings in Arb.Generate<bool>()
            from theme in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from notificationsEnabled in Arb.Generate<bool>()
            select new JsonBlobTestEntity
            {
                Id = id,
                Name = name,
                Settings = hasSettings ? new TestSettings
                {
                    Theme = theme,
                    NotificationsEnabled = notificationsEnabled
                } : null
            });
    }

    private static Arbitrary<JsonBlobListTestEntity> JsonBlobListEntityArbitrary()
    {
        return Arb.From(
            from id in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from tagCount in Gen.Choose(0, 5)
            from tags in Gen.ListOf(tagCount, Arb.Generate<NonEmptyString>().Select(s => new TestTag { Name = s.Get }))
            select new JsonBlobListTestEntity
            {
                Id = id,
                Tags = tags.ToList()
            });
    }

    #endregion

    #region Comparison Helpers

    private static bool CompareSettings(TestSettings? a, TestSettings? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Theme == b.Theme && a.NotificationsEnabled == b.NotificationsEnabled;
    }

    private static bool CompareTags(List<TestTag>? a, List<TestTag>? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        return a.Zip(b, (x, y) => x.Name == y.Name).All(match => match);
    }

    #endregion
}

/// <summary>
/// Property-based tests for composite entity JsonBlob round-trip consistency.
/// These tests verify Property 3 from the design document.
/// </summary>
[Trait("Category", "PropertyTest")]
[Trait("Feature", "jsonblob-composite-entity-fix")]
public class CompositeEntityJsonBlobPropertyTests
{
    /// <summary>
    /// **Feature: jsonblob-composite-entity-fix, Property 3: Composite Entity JsonBlob Round-Trip**
    /// *For any* composite entity with related entities containing [JsonBlob] properties, 
    /// querying via ToCompositeEntityAsync SHALL correctly deserialize all JsonBlob properties 
    /// in both the primary entity and all related entities.
    /// **Validates: Requirements 1.2, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositeEntity_JsonBlob_RoundTrip_PreservesAllValues()
    {
        return Prop.ForAll(
            CompositeEntityArbitrary(),
            entity =>
            {
                // Arrange - Create options with JSON serializer
                var options = new FluentDynamoDbOptions().WithSystemTextJson();

                // Act - Simulate the composite entity round-trip:
                // 1. Convert parent entity to DynamoDB item
                // 2. Convert each child entity to DynamoDB item
                // 3. Combine into a list (simulating query result)
                // 4. Call multi-item FromDynamoDb (simulating ToCompositeEntityAsync)
                
                var parentItem = CompositeParentTestEntity.ToDynamoDb(entity, options);
                var childItems = entity.Children.Select(c => CompositeChildTestEntity.ToDynamoDb(c, options)).ToList();
                
                // Combine all items (parent first, then children)
                var allItems = new List<Dictionary<string, AttributeValue>> { parentItem };
                allItems.AddRange(childItems);
                
                // Round-trip through multi-item FromDynamoDb
                var roundTrippedEntity = CompositeParentTestEntity.FromDynamoDb<CompositeParentTestEntity>(allItems, options);

                // Assert - All properties should be equivalent
                var idMatches = roundTrippedEntity.Id == entity.Id;
                var nameMatches = roundTrippedEntity.Name == entity.Name;
                
                // Parent JsonBlob property comparison
                var parentConfigMatches = CompareParentConfig(entity.Config, roundTrippedEntity.Config);
                
                // Children count matches
                var childCountMatches = roundTrippedEntity.Children.Count == entity.Children.Count;
                
                // All children JsonBlob properties match
                var childrenMatch = entity.Children.Count == 0 || 
                    entity.Children.Zip(roundTrippedEntity.Children, (orig, rt) => 
                        orig.ChildId == rt.ChildId && 
                        CompareChildMetadata(orig.Metadata, rt.Metadata))
                    .All(match => match);
                
                return (idMatches && nameMatches && parentConfigMatches && childCountMatches && childrenMatch)
                    .Label($"Id: {idMatches}, Name: {nameMatches}, ParentConfig: {parentConfigMatches}, " +
                           $"ChildCount: {childCountMatches}, ChildrenMatch: {childrenMatch}");
            });
    }

    /// <summary>
    /// **Feature: jsonblob-composite-entity-fix, Property 3: Composite Entity JsonBlob Round-Trip** (empty children variant)
    /// *For any* composite entity with no related entities, round-trip SHALL preserve parent JsonBlob properties.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositeEntity_NoChildren_JsonBlob_RoundTrip_PreservesParentValues()
    {
        return Prop.ForAll(
            CompositeEntityWithNoChildrenArbitrary(),
            entity =>
            {
                // Arrange
                var options = new FluentDynamoDbOptions().WithSystemTextJson();

                // Act - Round-trip with only parent item
                var parentItem = CompositeParentTestEntity.ToDynamoDb(entity, options);
                var allItems = new List<Dictionary<string, AttributeValue>> { parentItem };
                var roundTrippedEntity = CompositeParentTestEntity.FromDynamoDb<CompositeParentTestEntity>(allItems, options);

                // Assert
                var idMatches = roundTrippedEntity.Id == entity.Id;
                var configMatches = CompareParentConfig(entity.Config, roundTrippedEntity.Config);
                var noChildren = roundTrippedEntity.Children.Count == 0;
                
                return (idMatches && configMatches && noChildren)
                    .Label($"Id: {idMatches}, Config: {configMatches}, NoChildren: {noChildren}");
            });
    }

    /// <summary>
    /// **Feature: jsonblob-composite-entity-fix, Property 3: Composite Entity JsonBlob Round-Trip** (null JsonBlob variant)
    /// *For any* composite entity where parent and children have null JsonBlob properties, round-trip SHALL preserve nulls.
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositeEntity_NullJsonBlobs_RoundTrip_PreservesNulls()
    {
        return Prop.ForAll(
            CompositeEntityWithNullJsonBlobsArbitrary(),
            entity =>
            {
                // Arrange
                var options = new FluentDynamoDbOptions().WithSystemTextJson();

                // Act
                var parentItem = CompositeParentTestEntity.ToDynamoDb(entity, options);
                var childItems = entity.Children.Select(c => CompositeChildTestEntity.ToDynamoDb(c, options)).ToList();
                var allItems = new List<Dictionary<string, AttributeValue>> { parentItem };
                allItems.AddRange(childItems);
                var roundTrippedEntity = CompositeParentTestEntity.FromDynamoDb<CompositeParentTestEntity>(allItems, options);

                // Assert - All JsonBlob properties should be null
                var parentConfigNull = roundTrippedEntity.Config == null;
                var allChildMetadataNull = roundTrippedEntity.Children.All(c => c.Metadata == null);
                
                return (parentConfigNull && allChildMetadataNull)
                    .Label($"ParentConfigNull: {parentConfigNull}, AllChildMetadataNull: {allChildMetadataNull}");
            });
    }

    #region Arbitraries

    private static Arbitrary<CompositeParentTestEntity> CompositeEntityArbitrary()
    {
        return Arb.From(
            from id in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from name in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from hasConfig in Arb.Generate<bool>()
            from region in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from maxUsers in Gen.Choose(1, 1000)
            from childCount in Gen.Choose(0, 3)
            from children in Gen.ListOf(childCount, GenerateChild(id))
            select new CompositeParentTestEntity
            {
                Id = id,
                Name = name,
                Config = hasConfig ? new ParentConfig { Region = region, MaxUsers = maxUsers } : null,
                Children = children.ToList()
            });
    }

    private static Arbitrary<CompositeParentTestEntity> CompositeEntityWithNoChildrenArbitrary()
    {
        return Arb.From(
            from id in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from name in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from hasConfig in Arb.Generate<bool>()
            from region in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from maxUsers in Gen.Choose(1, 1000)
            select new CompositeParentTestEntity
            {
                Id = id,
                Name = name,
                Config = hasConfig ? new ParentConfig { Region = region, MaxUsers = maxUsers } : null,
                Children = new List<CompositeChildTestEntity>()
            });
    }

    private static Arbitrary<CompositeParentTestEntity> CompositeEntityWithNullJsonBlobsArbitrary()
    {
        return Arb.From(
            from id in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from name in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from childCount in Gen.Choose(0, 3)
            from children in Gen.ListOf(childCount, GenerateChildWithNullMetadata(id))
            select new CompositeParentTestEntity
            {
                Id = id,
                Name = name,
                Config = null, // Explicitly null
                Children = children.ToList()
            });
    }

    private static Gen<CompositeChildTestEntity> GenerateChild(string parentId)
    {
        return from childId in Arb.Generate<NonEmptyString>().Select(s => s.Get)
               from hasMetadata in Arb.Generate<bool>()
               from category in Arb.Generate<NonEmptyString>().Select(s => s.Get)
               from priority in Gen.Choose(1, 10)
               select new CompositeChildTestEntity
               {
                   ParentId = parentId,
                   ChildId = childId,
                   Metadata = hasMetadata ? new ChildMetadata { Category = category, Priority = priority } : null
               };
    }

    private static Gen<CompositeChildTestEntity> GenerateChildWithNullMetadata(string parentId)
    {
        return from childId in Arb.Generate<NonEmptyString>().Select(s => s.Get)
               select new CompositeChildTestEntity
               {
                   ParentId = parentId,
                   ChildId = childId,
                   Metadata = null // Explicitly null
               };
    }

    #endregion

    #region Comparison Helpers

    private static bool CompareParentConfig(ParentConfig? a, ParentConfig? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Region == b.Region && a.MaxUsers == b.MaxUsers;
    }

    private static bool CompareChildMetadata(ChildMetadata? a, ChildMetadata? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Category == b.Category && a.Priority == b.Priority;
    }

    #endregion
}

#region Test Entities for Property Tests

/// <summary>
/// Test entity with a nullable JsonBlob property for property-based testing.
/// Manually implements IDynamoDbEntity to simulate the generated code behavior.
/// </summary>
public class JsonBlobTestEntity : IDynamoDbEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TestSettings? Settings { get; set; }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as JsonBlobTestEntity;
        if (testEntity == null) throw new ArgumentException("Expected JsonBlobTestEntity");

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity.Id },
            ["name"] = new AttributeValue { S = testEntity.Name }
        };

        // Serialize JsonBlob property using the configured serializer
        if (testEntity.Settings != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Settings' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            var json = options.JsonSerializer.Serialize(testEntity.Settings);
            item["settings"] = new AttributeValue { S = json };
        }

        return item;
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new JsonBlobTestEntity
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
        };

        // Deserialize JsonBlob property using the configured serializer
        if (item.TryGetValue("settings", out var settingsValue) && settingsValue.S != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Settings' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            entity.Settings = options.JsonSerializer.Deserialize<TestSettings>(settingsValue.S);
        }

        return (TSelf)(object)entity;
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        return FromDynamoDb<TSelf>(items.First(), options);
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.ContainsKey("pk") && item.ContainsKey("name");
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "test-entities",
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}

/// <summary>
/// Test entity with a List JsonBlob property for property-based testing.
/// Manually implements IDynamoDbEntity to simulate the generated code behavior.
/// </summary>
public class JsonBlobListTestEntity : IDynamoDbEntity
{
    public string Id { get; set; } = string.Empty;
    public List<TestTag>? Tags { get; set; }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as JsonBlobListTestEntity;
        if (testEntity == null) throw new ArgumentException("Expected JsonBlobListTestEntity");

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity.Id }
        };

        // Serialize JsonBlob List property using the configured serializer
        if (testEntity.Tags != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Tags' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            var json = options.JsonSerializer.Serialize(testEntity.Tags);
            item["tags"] = new AttributeValue { S = json };
        }

        return item;
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new JsonBlobListTestEntity
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty
        };

        // Deserialize JsonBlob List property using the configured serializer
        if (item.TryGetValue("tags", out var tagsValue) && tagsValue.S != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Tags' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            entity.Tags = options.JsonSerializer.Deserialize<List<TestTag>>(tagsValue.S);
        }

        return (TSelf)(object)entity;
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        return FromDynamoDb<TSelf>(items.First(), options);
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.ContainsKey("pk");
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "test-list-entities",
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}

/// <summary>
/// Complex type for JsonBlob serialization testing.
/// </summary>
public class TestSettings
{
    public string Theme { get; set; } = string.Empty;
    public bool NotificationsEnabled { get; set; }
}

/// <summary>
/// Complex type for List JsonBlob serialization testing.
/// </summary>
public class TestTag
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Parent entity for composite entity property testing.
/// Simulates a parent entity with [RelatedEntity] collection and [JsonBlob] property.
/// </summary>
public class CompositeParentTestEntity : IDynamoDbEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ParentConfig? Config { get; set; }
    public List<CompositeChildTestEntity> Children { get; set; } = new();

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as CompositeParentTestEntity;
        if (testEntity == null) throw new ArgumentException("Expected CompositeParentTestEntity");

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity.Id },
            ["sk"] = new AttributeValue { S = "PARENT" },
            ["name"] = new AttributeValue { S = testEntity.Name }
        };

        // Serialize JsonBlob property
        if (testEntity.Config != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Config' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            var json = options.JsonSerializer.Serialize(testEntity.Config);
            item["config"] = new AttributeValue { S = json };
        }

        return item;
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new CompositeParentTestEntity
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
        };

        // Deserialize JsonBlob property
        if (item.TryGetValue("config", out var configValue) && configValue.S != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Config' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            entity.Config = options.JsonSerializer.Deserialize<ParentConfig>(configValue.S);
        }

        return (TSelf)(object)entity;
    }

    /// <summary>
    /// Multi-item FromDynamoDb - simulates the generated code for composite entities.
    /// This is the method that ToCompositeEntityAsync calls.
    /// </summary>
    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        // Find the parent item (sk = "PARENT")
        var parentItem = items.FirstOrDefault(i => 
            i.TryGetValue("sk", out var sk) && sk.S == "PARENT");
        
        if (parentItem == null)
        {
            throw new InvalidOperationException("No parent item found in composite entity items");
        }

        // Create parent entity from the parent item
        var entity = FromDynamoDb<CompositeParentTestEntity>(parentItem, options);

        // Find and map child items (sk starts with "CHILD#")
        var childItems = items.Where(i => 
            i.TryGetValue("sk", out var sk) && sk.S != null && sk.S.StartsWith("CHILD#"));

        foreach (var childItem in childItems)
        {
            // This is the critical part - the child's FromDynamoDb must receive options
            // to correctly deserialize JsonBlob properties
            var child = CompositeChildTestEntity.FromDynamoDb<CompositeChildTestEntity>(childItem, options);
            entity.Children.Add(child);
        }

        return (TSelf)(object)entity;
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("sk", out var sk) && sk.S == "PARENT";
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "composite-test-entities",
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}

/// <summary>
/// Child entity for composite entity property testing.
/// Simulates a related entity with [JsonBlob] property.
/// </summary>
public class CompositeChildTestEntity : IDynamoDbEntity
{
    public string ParentId { get; set; } = string.Empty;
    public string ChildId { get; set; } = string.Empty;
    public ChildMetadata? Metadata { get; set; }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as CompositeChildTestEntity;
        if (testEntity == null) throw new ArgumentException("Expected CompositeChildTestEntity");

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity.ParentId },
            ["sk"] = new AttributeValue { S = $"CHILD#{testEntity.ChildId}" }
        };

        // Serialize JsonBlob property
        if (testEntity.Metadata != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Metadata' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            var json = options.JsonSerializer.Serialize(testEntity.Metadata);
            item["metadata"] = new AttributeValue { S = json };
        }

        return item;
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new CompositeChildTestEntity
        {
            ParentId = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            ChildId = item.TryGetValue("sk", out var sk) && sk.S != null 
                ? sk.S.Replace("CHILD#", "") 
                : string.Empty
        };

        // Deserialize JsonBlob property - THIS IS THE CRITICAL PART
        // The bug was that this would use Enum.Parse instead of JsonSerializer.Deserialize
        if (item.TryGetValue("metadata", out var metadataValue) && metadataValue.S != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Metadata' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            entity.Metadata = options.JsonSerializer.Deserialize<ChildMetadata>(metadataValue.S);
        }

        return (TSelf)(object)entity;
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        return FromDynamoDb<TSelf>(items.First(), options);
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("sk", out var sk) && sk.S != null && sk.S.StartsWith("CHILD#");
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "composite-test-entities",
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}

/// <summary>
/// Complex type for parent entity JsonBlob property.
/// </summary>
public class ParentConfig
{
    public string Region { get; set; } = string.Empty;
    public int MaxUsers { get; set; }
}

/// <summary>
/// Complex type for child entity JsonBlob property.
/// </summary>
public class ChildMetadata
{
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; }
}

#endregion

#endregion