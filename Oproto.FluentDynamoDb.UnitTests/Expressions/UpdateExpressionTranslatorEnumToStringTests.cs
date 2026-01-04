using AwesomeAssertions;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for enum.ToString() support in UpdateExpressionTranslator.
/// Validates that calling ToString() on enum constants works correctly.
/// </summary>
public class UpdateExpressionTranslatorEnumToStringTests
{
    private enum TransactionStatus
    {
        Pending,
        Active,
        Completed
    }

    private class TestUpdateExpressions
    {
        public UpdateExpressionProperty<string> Status { get; } = new();
        public UpdateExpressionProperty<string> Name { get; } = new();
    }

    private class TestUpdateModel
    {
        public string? Status { get; set; }
        public string? Name { get; set; }
    }

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new PropertyMetadata[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Status",
                    AttributeName = "status",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false
                }
            }
        };
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    [Fact]
    public void TranslateUpdateExpression_EnumConstantToString_ShouldEvaluateAndCapture()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // This is the pattern that was reported as not working:
        // Status = TransactionStatus.Active.ToString()
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Status = TransactionStatus.Active.ToString() };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        result.Should().Contain("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Active");
    }

    [Fact]
    public void TranslateUpdateExpression_EnumVariableToString_ShouldEvaluateAndCapture()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var status = TransactionStatus.Completed;
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Status = status.ToString() };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        result.Should().Contain("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Completed");
    }

    [Fact]
    public void TranslateUpdateExpression_IntToString_ShouldEvaluateAndCapture()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var id = 12345;
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = id.ToString() };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        result.Should().Contain("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("12345");
    }

    [Fact]
    public void TranslateUpdateExpression_GuidToString_ShouldEvaluateAndCapture()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = guid.ToString() };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        result.Should().Contain("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("12345678-1234-1234-1234-123456789abc");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedMethodCalls_ShouldEvaluateAndCapture()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var name = "  John Doe  ";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = name.Trim().ToUpper() };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        result.Should().Contain("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("JOHN DOE");
    }
}
