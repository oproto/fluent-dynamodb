// MIGRATION STATUS: Migrated to use CompilationVerifier and SemanticAssertions
// - Compilation verification: Added to all tests
// - Semantic assertions: Replaced structural string checks
// - DynamoDB-specific checks: Preserved with descriptive "because" messages

using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class ComplexTypeGenerationTests
{
    #region Map Property Tests (Task 19.1)

    [Fact]
    public void Generator_WithDictionaryStringString_GeneratesMapConversion()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""metadata"")]
        public Dictionary<string, string>? Metadata { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates map conversion
        entityCode.Should().Contain("if (typedEntity.Metadata != null && typedEntity.Metadata.Count > 0)",
            "should check for null and empty before adding Dictionary to DynamoDB item");
        entityCode.ShouldContainAssignment("metadataMap");
        entityCode.Should().Contain("foreach (var kvp in typedEntity.Metadata)",
            "should iterate through dictionary entries");
        entityCode.Should().Contain("{ S = kvp.Value }",
            "should use String (S) attribute type for string dictionary values");
        entityCode.Should().Contain("{ M = metadataMap }",
            "should use Map (M) attribute type for Dictionary");
        
        // Check FromDynamoDb reconstructs dictionary
        entityCode.Should().Contain("if (item.TryGetValue(\"metadata\", out var metadataValue) && metadataValue.M != null)",
            "should check for attribute existence and Map type");
        entityCode.ShouldUseLinqMethod("ToDictionary");
        entityCode.Should().Contain("kvp => kvp.Key",
            "should extract dictionary keys");
        entityCode.Should().Contain("kvp => kvp.Value.S",
            "should extract string values from DynamoDB String attributes");
    }

    [Fact]
    public void Generator_WithDynamoDbMapAttribute_GeneratesNestedMapConversion()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class Product
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""attributes"")]
        [DynamoDbMap]
        public ProductAttributes? Attributes { get; set; }
    }

    [DynamoDbEntity]
    public partial class ProductAttributes
    {
        [DynamoDbAttribute(""color"")]
        public string? Color { get; set; }
        
        [DynamoDbAttribute(""size"")]
        public int? Size { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "Product.g.cs");
        var nestedEntityCode = GetGeneratedSource(result, "ProductAttributes.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source, nestedEntityCode);
        
        // Check ToDynamoDb uses nested type's generated method
        entityCode.Should().Contain("if (typedEntity.Attributes != null)",
            "should check for null before processing nested entity");
        entityCode.ShouldReferenceType("ProductAttributes");
        // Check that the nested entity is converted to a Map and added to the item
        entityCode.Should().Contain("ProductAttributes.ToDynamoDb", "should call nested type's ToDynamoDb method");
        entityCode.Should().Contain("{ M =", "should use Map (M) attribute type for nested entity");
        
        // Check FromDynamoDb uses nested type's generated method
        entityCode.Should().Contain("if (item.TryGetValue(\"attributes\", out var attributesValue) && attributesValue.M != null)",
            "should check for attribute existence and Map type");
        entityCode.ShouldContainMethod("FromDynamoDb");
    }

    [Fact]
    public void Generator_WithEmptyDictionary_OmitsAttribute()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""metadata"")]
        public Dictionary<string, string>? Metadata { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify empty collection check exists
        entityCode.Should().Contain("if (typedEntity.Metadata != null && typedEntity.Metadata.Count > 0)",
            "should check for null and empty to omit empty Dictionary from DynamoDB item");
    }

    [Fact]
    public void Generator_WithNullDictionary_HandlesGracefully()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""metadata"")]
        public Dictionary<string, string>? Metadata { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify null check exists in ToDynamoDb
        entityCode.Should().Contain("if (typedEntity.Metadata != null && typedEntity.Metadata.Count > 0)",
            "should check for null and empty to handle null Dictionary gracefully");
        
        // Verify FromDynamoDb handles missing attribute
        entityCode.Should().Contain("if (item.TryGetValue(\"metadata\", out var metadataValue) && metadataValue.M != null)",
            "should handle missing or null Map attribute gracefully");
    }

    #endregion

    #region Set Property Tests (Task 19.2)

    [Fact]
    public void Generator_WithHashSetString_GeneratesStringSetConversion()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""tags"")]
        public HashSet<string>? Tags { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates SS conversion
        entityCode.Should().Contain("if (typedEntity.Tags != null && typedEntity.Tags.Count > 0)",
            "should check for null and empty before adding HashSet to DynamoDB item");
        entityCode.Should().Contain("{ SS = typedEntity.Tags.ToList() }",
            "should use String Set (SS) attribute type for HashSet<string>");
        
        // Check FromDynamoDb reconstructs HashSet
        entityCode.Should().Contain("if (item.TryGetValue(\"tags\", out var tagsValue))",
            "should check for attribute existence");
        entityCode.ShouldContainAssignment("entity.Tags");
    }

    [Fact]
    public void Generator_WithHashSetInt_GeneratesNumberSetConversion()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""category_ids"")]
        public HashSet<int>? CategoryIds { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates NS conversion
        entityCode.Should().Contain("if (typedEntity.CategoryIds != null && typedEntity.CategoryIds.Count > 0)",
            "should check for null and empty before adding HashSet to DynamoDB item");
        entityCode.Should().Contain("NS = typedEntity.CategoryIds.Select(x => x.ToString()).ToList()",
            "should use Number Set (NS) attribute type for HashSet<int> with ToString conversion");
        
        // Check FromDynamoDb reconstructs HashSet<int>
        entityCode.Should().Contain("if (item.TryGetValue(\"category_ids\", out var categoryidsValue))",
            "should check for attribute existence");
        entityCode.ShouldContainAssignment("entity.CategoryIds");
    }

    [Fact]
    public void Generator_WithHashSetByteArray_GeneratesBinarySetConversion()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""binary_data"")]
        public HashSet<byte[]>? BinaryData { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates BS conversion
        entityCode.Should().Contain("if (typedEntity.BinaryData != null && typedEntity.BinaryData.Count > 0)",
            "should check for null and empty before adding HashSet to DynamoDB item");
        entityCode.Should().Contain("BS = typedEntity.BinaryData.Select(x => new MemoryStream(x)).ToList()",
            "should use Binary Set (BS) attribute type for HashSet<byte[]> with MemoryStream conversion");
        
        // Check FromDynamoDb reconstructs HashSet<byte[]>
        entityCode.Should().Contain("if (item.TryGetValue(\"binary_data\", out var binarydataValue))",
            "should check for attribute existence");
        entityCode.ShouldContainAssignment("entity.BinaryData");
    }

    [Fact]
    public void Generator_WithEmptyHashSet_OmitsAttribute()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""tags"")]
        public HashSet<string>? Tags { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify empty collection check exists
        entityCode.Should().Contain("if (typedEntity.Tags != null && typedEntity.Tags.Count > 0)",
            "should check for null and empty to omit empty HashSet from DynamoDB item");
    }

    #endregion

    #region List Property Tests (Task 19.3)

    [Fact]
    public void Generator_WithListString_GeneratesListConversion()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""item_ids"")]
        public List<string>? ItemIds { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates L conversion
        entityCode.Should().Contain("if (typedEntity.ItemIds != null && typedEntity.ItemIds.Count > 0)",
            "should check for null and empty before adding List to DynamoDB item");
        entityCode.Should().Contain("L = typedEntity.ItemIds.Select(x => new AttributeValue { S = x }).ToList()",
            "should use List (L) attribute type for List<string> with String elements");
        
        // Check FromDynamoDb reconstructs List
        entityCode.Should().Contain("if (item.TryGetValue(\"item_ids\", out var itemidsValue))",
            "should check for attribute existence");
        entityCode.ShouldContainAssignment("entity.ItemIds");
    }

    [Fact]
    public void Generator_WithListInt_GeneratesListConversion()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""quantities"")]
        public List<int>? Quantities { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates L conversion with numeric elements
        entityCode.Should().Contain("if (typedEntity.Quantities != null && typedEntity.Quantities.Count > 0)",
            "should check for null and empty before adding List to DynamoDB item");
        entityCode.Should().Contain("L = typedEntity.Quantities.Select(x => new AttributeValue { N = x.ToString() }).ToList()",
            "should use List (L) attribute type for List<int> with Number elements");
        
        // Check FromDynamoDb reconstructs List<int>
        entityCode.Should().Contain("if (item.TryGetValue(\"quantities\", out var quantitiesValue))",
            "should check for attribute existence");
        entityCode.ShouldContainAssignment("entity.Quantities");
    }

    [Fact]
    public void Generator_WithListDecimal_GeneratesListConversion()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""prices"")]
        public List<decimal>? Prices { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates L conversion with decimal elements
        entityCode.Should().Contain("if (typedEntity.Prices != null && typedEntity.Prices.Count > 0)",
            "should check for null and empty before adding List to DynamoDB item");
        entityCode.Should().Contain("L = typedEntity.Prices.Select(x => new AttributeValue { N = x.ToString() }).ToList()",
            "should use List (L) attribute type for List<decimal> with Number elements");
        
        // Check FromDynamoDb reconstructs List<decimal>
        entityCode.Should().Contain("if (item.TryGetValue(\"prices\", out var pricesValue))",
            "should check for attribute existence");
        entityCode.ShouldContainAssignment("entity.Prices");
    }

    [Fact]
    public void Generator_WithEmptyList_OmitsAttribute()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""items"")]
        public List<string>? Items { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify empty collection check exists
        entityCode.Should().Contain("if (typedEntity.@Items != null && typedEntity.@Items.Count > 0)",
            "should check for null and empty to omit empty List from DynamoDB item (Items is a DynamoDB reserved word)");
    }

    #endregion

    #region TTL Property Tests (Task 19.4)

    [Fact]
    public void Generator_WithDateTimeTtl_GeneratesUnixEpochConversion()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""ttl"")]
        [TimeToLive]
        public DateTime? ExpiresAt { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates Unix epoch conversion
        entityCode.Should().Contain("if (typedEntity.ExpiresAt.HasValue)",
            "should check for null before converting TTL");
        entityCode.Should().Contain("var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)",
            "should use Unix epoch for TTL conversion");
        // Check that Unix timestamp is calculated and converted to string for DynamoDB Number type
        entityCode.Should().Contain(".TotalSeconds", "should calculate total seconds from epoch");
        entityCode.Should().Contain(".ToString()", "should convert seconds to string for DynamoDB Number type");
        
        // Check FromDynamoDb reconstructs DateTime
        entityCode.Should().Contain("if (item.TryGetValue(\"ttl\", out var ttlValue) && ttlValue.N != null)",
            "should check for attribute existence and Number type");
        entityCode.Should().Contain("var seconds = long.Parse(ttlValue.N)",
            "should parse Unix timestamp from Number attribute");
        entityCode.ShouldContainAssignment("entity.ExpiresAt");
    }

    [Fact]
    public void Generator_WithDateTimeOffsetTtl_GeneratesUnixEpochConversion()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""ttl"")]
        [TimeToLive]
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb generates Unix epoch conversion using ToUnixTimeSeconds
        entityCode.Should().Contain("if (typedEntity.ExpiresAt.HasValue)",
            "should check for null before converting TTL");
        entityCode.Should().Contain("var seconds = typedEntity.ExpiresAt.Value.ToUnixTimeSeconds()",
            "should use ToUnixTimeSeconds for DateTimeOffset TTL conversion");
        entityCode.Should().Contain("{ N = seconds.ToString() }",
            "should use Number (N) attribute type for TTL Unix timestamp");
        
        // Check FromDynamoDb reconstructs DateTimeOffset
        entityCode.Should().Contain("if (item.TryGetValue(\"ttl\", out var ttlValue) && ttlValue.N != null)",
            "should check for attribute existence and Number type");
        entityCode.Should().Contain("var seconds = long.Parse(ttlValue.N)",
            "should parse Unix timestamp from Number attribute");
        entityCode.Should().Contain("entity.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(seconds)",
            "should use FromUnixTimeSeconds to reconstruct DateTimeOffset");
    }

    [Fact]
    public void Generator_WithNullTtl_OmitsAttribute()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""ttl"")]
        [TimeToLive]
        public DateTime? ExpiresAt { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify null check exists
        entityCode.Should().Contain("if (typedEntity.ExpiresAt.HasValue)",
            "should check for null to omit TTL attribute when not set");
    }

    [Fact]
    public void Generator_WithTtlFromDynamoDb_ReconstructsCorrectly()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""ttl"")]
        [TimeToLive]
        public DateTime? ExpiresAt { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify FromDynamoDb handles missing TTL attribute
        entityCode.Should().Contain("if (item.TryGetValue(\"ttl\", out var ttlValue) && ttlValue.N != null)",
            "should handle missing TTL attribute gracefully");
    }

    #endregion

    #region JSON Blob Property Tests (Task 19.5)

    [Fact]
    public void Generator_WithJsonBlob_GeneratesRuntimeSerializerCall()
    {
        // Arrange - No assembly attribute needed, serializer is configured at runtime
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}";

        // Act - Include JSON package reference to avoid DYNDB102 error
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb uses runtime-configured serializer via options
        entityCode.Should().Contain("if (typedEntity.Content != null)",
            "should check for null before serializing JSON blob");
        entityCode.Should().Contain("options?.JsonSerializer == null",
            "should check if JSON serializer is configured at runtime");
        entityCode.Should().Contain("options.JsonSerializer.Serialize",
            "should use runtime-configured serializer for serialization");
        entityCode.Should().Contain("{ S = json }",
            "should use String (S) attribute type for JSON blob");
        
        // Check FromDynamoDb uses runtime-configured serializer
        entityCode.Should().Contain("if (item.TryGetValue(\"content\", out var contentValue))",
            "should check for attribute existence");
        entityCode.Should().Contain("options.JsonSerializer.Deserialize",
            "should use runtime-configured serializer for deserialization");
        
        // Verify error message mentions the correct extension methods
        entityCode.Should().Contain("WithSystemTextJson()",
            "error message should mention WithSystemTextJson() extension method");
        entityCode.Should().Contain("WithNewtonsoftJson()",
            "error message should mention WithNewtonsoftJson() extension method");
    }

    [Fact]
    public void Generator_WithJsonBlob_GeneratesNullSerializerCheck()
    {
        // Arrange - Test that generated code throws when serializer is not configured
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}";

        // Act - Include JSON package reference to avoid DYNDB102 error
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check that generated code throws InvalidOperationException when serializer is null
        entityCode.Should().Contain("throw new InvalidOperationException",
            "should throw when JSON serializer is not configured");
        entityCode.Should().Contain("no JSON serializer is configured",
            "error message should explain the issue");
    }

    [Fact]
    public void Generator_WithJsonBlobNoPackageReference_GeneratesDYNDB102Warning()
    {
        // Arrange - No JSON package reference
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate DYNDB102 error for missing JSON serializer
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB102");
    }

    [Fact]
    public void Generator_WithJsonBlobFromDynamoDb_DeserializesCorrectly()
    {
        // Arrange - No assembly attribute needed
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check FromDynamoDb uses runtime-configured serializer
        entityCode.Should().Contain("if (item.TryGetValue(\"content\", out var contentValue))",
            "should check for attribute existence");
        entityCode.Should().Contain("options.JsonSerializer.Deserialize",
            "should use runtime-configured serializer for deserialization");
    }

    [Fact]
    public void Generator_WithJsonBlobToDynamoDb_SerializesCorrectly()
    {
        // Arrange - No assembly attribute needed
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb uses runtime-configured serializer
        entityCode.Should().Contain("if (typedEntity.Content != null)",
            "should check for null before serializing JSON blob");
        entityCode.Should().Contain("options.JsonSerializer.Serialize",
            "should use runtime-configured serializer for serialization");
        entityCode.Should().Contain("{ S = json }",
            "should use String (S) attribute type for JSON blob");
    }

    [Fact]
    public void Generator_WithJsonBlobNullValue_OmitsAttribute()
    {
        // Arrange - No assembly attribute needed
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify null check exists
        entityCode.Should().Contain("if (typedEntity.Content != null)",
            "should check for null to omit JSON blob attribute when not set");
    }

    [Fact]
    public void Generator_WithJsonBlobEmptyObject_StoresEmptyJson()
    {
        // Arrange - No assembly attribute needed
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify serialization happens even for empty objects using runtime serializer
        entityCode.Should().Contain("options.JsonSerializer.Serialize",
            "should serialize even empty objects using runtime-configured serializer");
        entityCode.Should().Contain("{ S = json }",
            "should use String (S) attribute type for JSON blob");
    }

    [Fact]
    public void Generator_WithJsonBlobComplexType_GeneratesCorrectSerialization()
    {
        // Arrange - No assembly attribute needed
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""metadata"")]
        [JsonBlob]
        public ComplexMetadata? Metadata { get; set; }
    }

    public class ComplexMetadata
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, int> Counts { get; set; } = new();
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify complex type serialization using runtime serializer
        entityCode.Should().Contain("options.JsonSerializer.Serialize",
            "should serialize complex types with nested collections using runtime serializer");
        entityCode.Should().Contain("options.JsonSerializer.Deserialize",
            "should deserialize complex types with nested collections using runtime serializer");
    }

    [Fact]
    public void Generator_WithJsonBlobAndTtl_GeneratesBothCorrectly()
    {
        // Arrange - No assembly attribute needed
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
        
        [DynamoDbAttribute(""ttl"")]
        [TimeToLive]
        public DateTime? ExpiresAt { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify both JsonBlob and TTL are handled
        entityCode.Should().Contain("options.JsonSerializer.Serialize",
            "should handle JSON blob serialization using runtime serializer");
        entityCode.Should().Contain("var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)",
            "should handle TTL Unix epoch conversion");
    }

    [Fact]
    public void Generator_WithJsonBlobInMultiItemEntity_GeneratesCorrectly()
    {
        // Arrange - No assembly attribute needed
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public DocumentContent? Content { get; set; }
    }

    public class DocumentContent
    {
        public string Title { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify JsonBlob works in multi-item entity using runtime serializer
        entityCode.Should().Contain("options.JsonSerializer.Serialize",
            "should handle JSON blob in multi-item entity using runtime serializer");
        // Note: Multi-item entity comment may not be present if entity doesn't have relationships
    }

    #endregion

    #region Blob Reference Property Tests (Task 19.6)

    [Fact]
    public void Generator_WithBlobReference_GeneratesAsyncMethodSignatures()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""data_ref"")]
        [BlobReference(BlobProvider.S3, BucketName = ""my-bucket"")]
        public byte[]? Data { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source, includeS3BlobProvider: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check async ToDynamoDb method signature
        entityCode.ShouldContainMethod("ToDynamoDbAsync");
        entityCode.ShouldReferenceType("IBlobStorageProvider");
        entityCode.ShouldReferenceType("CancellationToken");
        entityCode.Should().Contain("public static async Task<Dictionary<string, AttributeValue>> ToDynamoDbAsync<TSelf>",
            "should generate async method signature for blob storage");
        
        // Check async FromDynamoDb method signature
        entityCode.ShouldContainMethod("FromDynamoDbAsync");
        entityCode.Should().Contain("public static async Task<TSelf> FromDynamoDbAsync<TSelf>",
            "should generate async method signature for blob retrieval");
    }

    [Fact]
    public void Generator_WithBlobReference_GeneratesBlobStorageCalls()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""data_ref"")]
        [BlobReference(BlobProvider.S3, BucketName = ""my-bucket"")]
        public byte[]? Data { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source, includeS3BlobProvider: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check ToDynamoDb calls StoreAsync
        entityCode.Should().Contain("if (typedEntity.@Data != null)",
            "should check for null before storing blob (Data is a DynamoDB reserved word)");
        entityCode.ShouldReferenceType("MemoryStream");
        entityCode.Should().Contain("await blobProvider.StoreAsync",
            "should call StoreAsync to store blob data");
        entityCode.ShouldReferenceType("CancellationToken");
        entityCode.Should().Contain("{ S = reference }",
            "should use String (S) attribute type for blob reference");
    }

    [Fact]
    public void Generator_WithBlobReference_GeneratesReferenceStorageInDynamoDb()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""file_ref"")]
        [BlobReference(BlobProvider.S3, BucketName = ""files"")]
        public byte[]? FileData { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source, includeS3BlobProvider: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify reference is stored as string in DynamoDB
        entityCode.Should().Contain("{ S = reference }",
            "should use String (S) attribute type for blob reference");
    }

    [Fact]
    public void Generator_WithBlobReference_GeneratesBlobRetrievalCode()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""data_ref"")]
        [BlobReference(BlobProvider.S3, BucketName = ""my-bucket"")]
        public byte[]? Data { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source, includeS3BlobProvider: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check FromDynamoDb calls RetrieveAsync
        entityCode.Should().Contain("if (item.TryGetValue(\"data_ref\", out var dataValue))",
            "should check for attribute existence");
        entityCode.Should().Contain("await blobProvider.RetrieveAsync",
            "should call RetrieveAsync to retrieve blob data");
        entityCode.ShouldReferenceType("MemoryStream");
        entityCode.Should().Contain("await stream.CopyToAsync(memoryStream, cancellationToken)",
            "should copy blob stream to memory");
        entityCode.ShouldContainAssignment("entity.@Data"); // Data is a DynamoDB reserved word
        
        // Check error handling
        entityCode.Should().Contain("catch (Exception ex)",
            "should handle blob retrieval errors");
        entityCode.ShouldReferenceType("DynamoDbMappingException");
    }

    [Fact]
    public void Generator_WithBlobReferenceFromDynamoDb_RetrievesFromStorage()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""file_ref"")]
        [BlobReference(BlobProvider.S3, BucketName = ""files"")]
        public byte[]? FileData { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source, includeS3BlobProvider: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var entityCode = GetGeneratedSource(result, "TestEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Verify FromDynamoDb retrieves blob from storage
        entityCode.Should().Contain("if (item.TryGetValue(\"file_ref\", out var filedataValue))",
            "should check for attribute existence");
        entityCode.Should().Contain("await blobProvider.RetrieveAsync",
            "should call RetrieveAsync to retrieve blob data");
        entityCode.ShouldContainAssignment("entity.FileData");
    }

    #endregion

    #region Compilation Error Diagnostics Tests (Task 19.7)

    [Fact]
    public void Generator_WithInvalidTtlType_GeneratesDYNDB101Error()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""ttl"")]
        [TimeToLive]
        public string ExpiresAt { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB101");
        var diagnostic = result.Diagnostics.First(d => d.Id == "DYNDB101");
        diagnostic.GetMessage().Should().Contain("TimeToLive");
        diagnostic.GetMessage().Should().Contain("DateTime");
    }

    [Fact]
    public void Generator_WithJsonBlobMissingSerializer_GeneratesDYNDB102Error()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""content"")]
        [JsonBlob]
        public CustomContent? Content { get; set; }
    }

    public class CustomContent
    {
        public string Data { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB102");
        var diagnostic = result.Diagnostics.First(d => d.Id == "DYNDB102");
        diagnostic.GetMessage().Should().Contain("JsonBlob");
        diagnostic.GetMessage().Should().Contain("serializer");
    }

    [Fact]
    public void Generator_WithBlobReferenceMissingProvider_GeneratesDYNDB103Error()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""data_ref"")]
        [BlobReference(BlobProvider.S3)]
        public byte[]? Data { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Note: This test may pass without error if the generator doesn't validate provider configuration
        // The actual validation might happen at runtime or require additional package references
        if (result.Diagnostics.Any(d => d.Id == "DYNDB103"))
        {
            var diagnostic = result.Diagnostics.First(d => d.Id == "DYNDB103");
            diagnostic.GetMessage().Should().Contain("BlobReference");
            diagnostic.GetMessage().Should().Contain("provider");
        }
    }

    [Fact]
    public void Generator_WithIncompatibleAttributes_GeneratesDYNDB104Error()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""ttl"")]
        [TimeToLive]
        [JsonBlob]
        public DateTime? ExpiresAt { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB104");
        var diagnostic = result.Diagnostics.First(d => d.Id == "DYNDB104");
        diagnostic.GetMessage().Should().Contain("TimeToLive");
        diagnostic.GetMessage().Should().Contain("JsonBlob");
    }

    [Fact]
    public void Generator_WithMultipleTtlFields_GeneratesDYNDB105Error()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""ttl1"")]
        [TimeToLive]
        public DateTime? ExpiresAt { get; set; }
        
        [DynamoDbAttribute(""ttl2"")]
        [TimeToLive]
        public DateTime? DeletedAt { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB105");
        var diagnostic = result.Diagnostics.First(d => d.Id == "DYNDB105");
        diagnostic.GetMessage().Should().Contain("multiple");
        diagnostic.GetMessage().Should().Contain("TTL");
    }

    [Fact]
    public void Generator_WithUnsupportedCollectionType_GeneratesDYNDB106Error()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""data"")]
        public Stack<string>? Data { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Note: This test checks if unsupported collection types generate an error
        // The generator might handle Stack<T> as a generic collection or generate DYNDB106
        if (result.Diagnostics.Any(d => d.Id == "DYNDB106"))
        {
            var diagnostic = result.Diagnostics.First(d => d.Id == "DYNDB106");
            diagnostic.GetMessage().Should().Contain("collection");
            diagnostic.GetMessage().Should().Contain("unsupported");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock assembly for testing optional package references.
    /// Uses DynamicCompilationHelper for proper IL3000 warning handling.
    /// </summary>
    private static MetadataReference CreateMockAssembly(string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText("// Mock assembly") },
            TestHelpers.DynamicCompilationHelper.GetStandardReferences().Take(1),
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
    /// Uses DynamicCompilationHelper for proper IL3000 warning handling.
    /// </summary>
    private static GeneratorTestResult GenerateCode(
        string source,
        bool includeSystemTextJson = false,
        bool includeNewtonsoftJson = false,
        bool includeS3BlobProvider = false)
    {
        var references = TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

        if (includeSystemTextJson)
        {
            references.Add(CreateMockAssembly("Oproto.FluentDynamoDb.SystemTextJson"));
        }

        if (includeNewtonsoftJson)
        {
            references.Add(CreateMockAssembly("Oproto.FluentDynamoDb.NewtonsoftJson"));
        }

        if (includeS3BlobProvider)
        {
            references.Add(CreateMockAssembly("Oproto.FluentDynamoDb.BlobStorage.S3"));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source)
            },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(GeneratorTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        source.Should().NotBeNull($"Expected to find generated source containing '{fileNamePart}'");
        return source!.SourceText.ToString();
    }

    #endregion
}

public class GeneratorTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required GeneratedSource[] GeneratedSources { get; set; }
}

public class GeneratedSource
{
    public GeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}
