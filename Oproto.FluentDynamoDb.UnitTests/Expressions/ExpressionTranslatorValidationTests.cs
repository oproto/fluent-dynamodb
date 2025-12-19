using AwesomeAssertions;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for validation and error handling in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorValidationTests
{
    private class TestEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string SortKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string UnmappedProperty { get; set; } = string.Empty;
    }

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateContext(
        EntityMetadata? metadata = null,
        ExpressionValidationMode validationMode = ExpressionValidationMode.None)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            validationMode);
    }

    private EntityMetadata CreateTestEntityMetadata()
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
                    SupportedOperations = new[] { DynamoDbOperation.Equals, DynamoDbOperation.LessThan, DynamoDbOperation.GreaterThan }
                },
                new PropertyMetadata
                {
                    PropertyName = "SortKey",
                    AttributeName = "SK",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = true,
                    SupportedOperations = new[] { DynamoDbOperation.Equals, DynamoDbOperation.LessThan, DynamoDbOperation.GreaterThan }
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "Name",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals, DynamoDbOperation.LessThan, DynamoDbOperation.GreaterThan }
                },
                new PropertyMetadata
                {
                    PropertyName = "Age",
                    AttributeName = "Age",
                    PropertyType = typeof(int),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals, DynamoDbOperation.LessThan, DynamoDbOperation.GreaterThan }
                }
            }
        };
    }

    [Fact]
    public void Translate_UnmappedProperty_ShouldThrowUnmappedPropertyException()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata);
        Expression<Func<TestEntity, bool>> expression = x => x.UnmappedProperty == "test";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnmappedPropertyException>()
            .WithMessage("*UnmappedProperty*")
            .WithMessage("*TestEntity*")
            .And.PropertyName.Should().Be("UnmappedProperty");
    }

    [Fact]
    public void Translate_NonKeyPropertyInKeysOnlyMode_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*Name*")
            .WithMessage("*not a key attribute*")
            .WithMessage("*WithFilter()*")
            .And.PropertyName.Should().Be("Name");
    }

    [Fact]
    public void Translate_PartitionKeyInKeysOnlyMode_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
        Expression<Func<TestEntity, bool>> expression = x => x.PartitionKey == "USER#123";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("PK");
    }

    [Fact]
    public void Translate_SortKeyInKeysOnlyMode_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
        Expression<Func<TestEntity, bool>> expression = x => x.SortKey.StartsWith("ORDER#");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("begins_with(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("SK");
    }

    [Fact]
    public void Translate_BothKeysInKeysOnlyMode_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
        Expression<Func<TestEntity, bool>> expression = x => 
            x.PartitionKey == "USER#123" && x.SortKey.StartsWith("ORDER#");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (begins_with(#attr1, :p1))");
    }

    [Fact]
    public void Translate_NonQueryableProperty_ShouldThrowUnsupportedExpressionException()
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
                    PropertyName = "Name",
                    AttributeName = "Name",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = Array.Empty<DynamoDbOperation>() // No operations supported
                }
            }
        };
        var context = CreateContext(metadata);
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Name*")
            .WithMessage("*non-queryable*");
    }

    [Fact]
    public void Translate_UnsupportedBinaryOperator_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        // Using modulo operator which is not supported
        Expression<Func<TestEntity, bool>> expression = x => x.Age % 2 == 0;

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Modulo*")
            .WithMessage("*not supported*");
    }

    [Fact]
    public void Translate_UnsupportedUnaryOperator_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        // Using unary negation on a property which is not supported
        Expression<Func<TestEntity, bool>> expression = x => -x.Age == 5;

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*not supported*");
    }

    [Fact]
    public void Translate_NullExpression_ShouldThrowArgumentNullException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();

        // Act
        var act = () => translator.Translate<TestEntity>(null!, context);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("expression");
    }

    [Fact]
    public void Translate_NullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var translator = CreateTranslator();
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John";

        // Act
        var act = () => translator.Translate(expression, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Translate_WithoutMetadata_ShouldNotValidateProperties()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(null); // No metadata
        Expression<Func<TestEntity, bool>> expression = x => x.UnmappedProperty == "test";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        // Should succeed because validation is skipped without metadata
    }

    [Fact]
    public void Translate_NonKeyPropertyInNoneMode_ShouldSucceed()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata, ExpressionValidationMode.None);
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        // Should succeed in None mode even for non-key properties
    }

    [Fact]
    public void Translate_ComplexNonKeyExpressionInKeysOnlyMode_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
        Expression<Func<TestEntity, bool>> expression = x => 
            x.PartitionKey == "USER#123" && x.Name == "John";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public void Translate_UnsupportedExpressionType_ShouldThrowException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // Create an expression with an unsupported node type (NewArrayInit)
        Expression<Func<TestEntity, bool>> expression = x => new[] { x.Name }.Contains("John");

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        // This throws ExpressionTranslationException because it tries to evaluate the array creation
        act.Should().Throw<ExpressionTranslationException>();
    }

    [Fact]
    public void UnmappedPropertyException_ShouldIncludeOriginalExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata);
        Expression<Func<TestEntity, bool>> expression = x => x.UnmappedProperty == "test";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnmappedPropertyException>()
            .Which.OriginalExpression.Should().NotBeNull();
    }

    [Fact]
    public void InvalidKeyExpressionException_ShouldIncludeOriginalExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<InvalidKeyExpressionException>()
            .Which.OriginalExpression.Should().NotBeNull();
    }

    [Fact]
    public void UnsupportedExpressionException_ShouldIncludeOriginalExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age % 2 == 0;

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .Which.OriginalExpression.Should().NotBeNull();
    }

    [Fact]
    public void Translate_PropertyWithDifferentAttributeName_ShouldUseAttributeName()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestEntityMetadata();
        var context = CreateContext(metadata);
        Expression<Func<TestEntity, bool>> expression = x => x.PartitionKey == "USER#123";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("PK"); // Uses AttributeName, not PropertyName
    }
}
