using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for format string application in UpdateExpressionTranslator.
/// </summary>
public class UpdateExpressionTranslatorFormatStringTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Price { get; set; }
        public double? Rating { get; set; }
        public int? OrderNumber { get; set; }
    }

    private class TestUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<DateTime?> CreatedDate { get; } = new();
        public UpdateExpressionProperty<DateTime?> UpdatedAt { get; } = new();
        public UpdateExpressionProperty<decimal?> Amount { get; } = new();
        public UpdateExpressionProperty<decimal?> Price { get; } = new();
        public UpdateExpressionProperty<double?> Rating { get; } = new();
        public UpdateExpressionProperty<int?> OrderNumber { get; } = new();
    }

    private class TestUpdateModel
    {
        public string? Id { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Price { get; set; }
        public double? Rating { get; set; }
        public int? OrderNumber { get; set; }
    }

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext(EntityMetadata? metadata = null)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    private EntityMetadata CreateTestMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "id",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "CreatedDate",
                    AttributeName = "created_date",
                    PropertyType = typeof(DateTime?),
                    Format = "yyyy-MM-dd"
                },
                new PropertyMetadata
                {
                    PropertyName = "UpdatedAt",
                    AttributeName = "updated_at",
                    PropertyType = typeof(DateTime?),
                    Format = "yyyy-MM-ddTHH:mm:ss"
                },
                new PropertyMetadata
                {
                    PropertyName = "Amount",
                    AttributeName = "amount",
                    PropertyType = typeof(decimal?),
                    Format = "F2"
                },
                new PropertyMetadata
                {
                    PropertyName = "Price",
                    AttributeName = "price",
                    PropertyType = typeof(decimal?),
                    Format = "F4"
                },
                new PropertyMetadata
                {
                    PropertyName = "Rating",
                    AttributeName = "rating",
                    PropertyType = typeof(double?),
                    Format = "F2"
                },
                new PropertyMetadata
                {
                    PropertyName = "OrderNumber",
                    AttributeName = "order_number",
                    PropertyType = typeof(int?),
                    Format = "D8"
                }
            }
        };
    }

    [Fact]
    public void TranslateUpdateExpression_DateTimeWithDateOnlyFormat_AppliesFormat()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestMetadata();
        var context = CreateContext(metadata);
        
        var date = new DateTime(2024, 3, 15);
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { CreatedDate = date };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-03-15");
    }

    [Fact]
    public void TranslateUpdateExpression_DateTimeWithISO8601Format_AppliesFormat()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestMetadata();
        var context = CreateContext(metadata);
        
        var dateTime = new DateTime(2024, 3, 15, 14, 30, 45);
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { UpdatedAt = dateTime };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-03-15T14:30:45");
    }

    [Fact]
    public void TranslateUpdateExpression_DecimalWithTwoDecimalPlaces_AppliesFormat()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Amount = 123.456m };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("123.46");
    }

    [Fact]
    public void TranslateUpdateExpression_DecimalWithFourDecimalPlaces_AppliesFormat()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Price = 99.123456m };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("99.1235");
    }

    [Fact]
    public void TranslateUpdateExpression_DoubleWithTwoDecimalPlaces_AppliesFormat()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Rating = 4.567 };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("4.57");
    }

    [Fact]
    public void TranslateUpdateExpression_IntegerWithZeroPadding_AppliesFormat()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { OrderNumber = 42 };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("00000042");
    }

    [Fact]
    public void TranslateUpdateExpression_MultiplePropertiesWithFormats_AppliesAllFormats()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateTestMetadata();
        var context = CreateContext(metadata);
        
        var date = new DateTime(2024, 3, 15);
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel 
            { 
                CreatedDate = date,
                Amount = 100.5m,
                OrderNumber = 7
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0, #attr1 = :p1, #attr2 = :p2");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-03-15");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("100.50");
        context.AttributeValues.AttributeValues[":p2"].S.Should().Be("00000007");
    }

    [Fact]
    public void TranslateUpdateExpression_PropertyWithoutFormat_UsesDefaultConversion()
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
                    PropertyName = "Id",
                    AttributeName = "id",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "Amount",
                    AttributeName = "amount",
                    PropertyType = typeof(decimal?),
                    // No format specified
                }
            }
        };
        var context = CreateContext(metadata);
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Amount = 123.456m };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        // Without format, decimal should be stored as number
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("123.456");
    }
}
