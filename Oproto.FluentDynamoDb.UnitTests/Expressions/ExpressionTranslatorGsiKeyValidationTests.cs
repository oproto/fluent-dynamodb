using AwesomeAssertions;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for GSI key validation in ExpressionTranslator.
/// Validates that lambda expressions correctly validate against GSI key schemas
/// when an index name is provided in the ExpressionContext.
/// </summary>
public class ExpressionTranslatorGsiKeyValidationTests
{
    private class TestEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string SortKey { get; set; } = string.Empty;
        public string GsiPartitionKey { get; set; } = string.Empty;
        public string GsiSortKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateContext(
        EntityMetadata? metadata = null,
        ExpressionValidationMode validationMode = ExpressionValidationMode.None,
        string? indexName = null)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            validationMode,
            indexName);
    }

    private EntityMetadata CreateTestEntityMetadataWithGsi()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "PartitionKey",
                    AttributeName = "PK",
                    PropertyType = typeof(string),
                    IsPartitionKey = true,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                },
                new PropertyMetadata
                {
                    PropertyName = "SortKey",
                    AttributeName = "SK",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = true,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                },
                new PropertyMetadata
                {
                    PropertyName = "GsiPartitionKey",
                    AttributeName = "GSI1PK",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                },
                new PropertyMetadata
                {
                    PropertyName = "GsiSortKey",
                    AttributeName = "GSI1SK",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "Name",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                },
                new PropertyMetadata
                {
                    PropertyName = "Age",
                    AttributeName = "Age",
                    PropertyType = typeof(int),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                }
            },
            Indexes = new[]
            {
                new IndexMetadata
                {
                    IndexName = "GSI1",
                    PartitionKeyProperty = "GsiPartitionKey",
                    SortKeyProperty = "GsiSortKey",
                    ProjectedProperties = Array.Empty<string>()
                }
            }
        };
    }

    [Fact]
    public void Translate_GsiPartitionKeyInKeysOnlyMode_WithIndexName_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, "GSI1");
        Expression<Func<TestEntity, bool>> expression = x => x.GsiPartitionKey == "VALUE";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("GSI1PK");
    }

    [Fact]
    public void Translate_GsiSortKeyInKeysOnlyMode_WithIndexName_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, "GSI1");
        Expression<Func<TestEntity, bool>> expression = x => x.GsiSortKey.StartsWith("PREFIX#");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("begins_with(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("GSI1SK");
    }

    [Fact]
    public void Translate_BothGsiKeysInKeysOnlyMode_WithIndexName_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, "GSI1");
        Expression<Func<TestEntity, bool>> expression = x => 
            x.GsiPartitionKey == "VALUE" && x.GsiSortKey.StartsWith("PREFIX#");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (begins_with(#attr1, :p1))");
    }

    [Fact]
    public void Translate_MainTableKeyInKeysOnlyMode_WithIndexName_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, "GSI1");
        // PartitionKey is the main table's PK, not the GSI's PK
        Expression<Func<TestEntity, bool>> expression = x => x.PartitionKey == "VALUE";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*PartitionKey*");
    }

    [Fact]
    public void Translate_NonKeyPropertyInKeysOnlyMode_WithIndexName_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, "GSI1");
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public void Translate_GsiKeyInKeysOnlyMode_WithoutIndexName_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        // No index name - should validate against main table keys
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, null);
        Expression<Func<TestEntity, bool>> expression = x => x.GsiPartitionKey == "VALUE";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*GsiPartitionKey*");
    }

    [Fact]
    public void Translate_MainTableKeyInKeysOnlyMode_WithoutIndexName_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, null);
        Expression<Func<TestEntity, bool>> expression = x => x.PartitionKey == "VALUE";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("PK");
    }

    [Fact]
    public void Translate_GsiKeyInKeysOnlyMode_WithUnknownIndexName_ShouldFallbackToMainTableValidation()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        // Unknown index name - should fall back to main table key validation
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, "UnknownIndex");
        Expression<Func<TestEntity, bool>> expression = x => x.GsiPartitionKey == "VALUE";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        // Should throw because GsiPartitionKey is not a main table key
        act.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*GsiPartitionKey*");
    }

    [Fact]
    public void Translate_GsiKeyInNoneMode_WithIndexName_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        // None mode - no key validation
        var context = CreateContext(metadata, ExpressionValidationMode.None, "GSI1");
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        // Should succeed because validation mode is None
    }

    [Fact]
    public void Translate_IndexNameCaseInsensitive_ShouldMatchIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadataWithGsi();
        // Use lowercase index name
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, "gsi1");
        Expression<Func<TestEntity, bool>> expression = x => x.GsiPartitionKey == "VALUE";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        // Should succeed because index name matching is case-insensitive
    }

    [Fact]
    public void Translate_GsiWithOnlyPartitionKey_ShouldValidateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "PartitionKey",
                    AttributeName = "PK",
                    PropertyType = typeof(string),
                    IsPartitionKey = true,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                },
                new PropertyMetadata
                {
                    PropertyName = "GsiPartitionKey",
                    AttributeName = "GSI1PK",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                }
            },
            Indexes = new[]
            {
                new IndexMetadata
                {
                    IndexName = "GSI1",
                    PartitionKeyProperty = "GsiPartitionKey",
                    SortKeyProperty = null, // No sort key
                    ProjectedProperties = Array.Empty<string>()
                }
            }
        };
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly, "GSI1");
        Expression<Func<TestEntity, bool>> expression = x => x.GsiPartitionKey == "VALUE";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
    }
}
