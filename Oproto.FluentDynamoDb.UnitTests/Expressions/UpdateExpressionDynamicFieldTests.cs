using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for dynamic field support in UpdateExpressionTranslator.
/// Validates: Requirements 5.1, 5.2, 5.3, 8.1, 8.2, 8.3
/// </summary>
public class UpdateExpressionDynamicFieldTests
{
    // Test entity with dynamic fields enabled
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public DynamicFieldAccessor DynamicFields { get; } = new();
    }

    private class TestUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> Name { get; } = new();
        public DynamicFieldAccessor DynamicFields { get; } = new();
    }

    private class TestUpdateModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public object? DynamicFieldResult { get; set; }
        public object? DynamicFieldResult2 { get; set; }
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
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string)
                }
            }
        };
    }

    #region SetDynamicField Tests

    [Fact]
    public void TranslateUpdateExpression_SetDynamicField_ShouldGenerateSetClause()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new TestUpdateModel { DynamicFieldResult = x.DynamicFields.SetDynamicField("customField", "value") }
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var dynamicFieldsProperty = Expression.Property(parameter, nameof(TestUpdateExpressions.DynamicFields));
        var setDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.SetDynamicField))!;
        var methodCall = Expression.Call(dynamicFieldsProperty, setDynamicFieldMethod, 
            Expression.Constant("customField"), Expression.Constant("value", typeof(object)));
        var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult))!, methodCall);
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #dynField0 = :p0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("customField");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("value");
    }

    [Fact]
    public void TranslateUpdateExpression_SetDynamicFieldWithNumber_ShouldGenerateCorrectValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var dynamicFieldsProperty = Expression.Property(parameter, nameof(TestUpdateExpressions.DynamicFields));
        var setDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.SetDynamicField))!;
        var methodCall = Expression.Call(dynamicFieldsProperty, setDynamicFieldMethod, 
            Expression.Constant("customScore"), Expression.Constant(42, typeof(object)));
        var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult))!, methodCall);
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #dynField0 = :p0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("customScore");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("42");
    }

    [Fact]
    public void TranslateUpdateExpression_SetDynamicFieldWithReservedWord_ShouldEscapeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // "status" is a DynamoDB reserved word
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var dynamicFieldsProperty = Expression.Property(parameter, nameof(TestUpdateExpressions.DynamicFields));
        var setDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.SetDynamicField))!;
        var methodCall = Expression.Call(dynamicFieldsProperty, setDynamicFieldMethod, 
            Expression.Constant("status"), Expression.Constant("active", typeof(object)));
        var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult))!, methodCall);
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #dynField0 = :p0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("status");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("active");
    }

    [Fact]
    public void TranslateUpdateExpression_SetDynamicFieldWithVariable_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        var customValue = "captured-value";
        
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var dynamicFieldsProperty = Expression.Property(parameter, nameof(TestUpdateExpressions.DynamicFields));
        var setDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.SetDynamicField))!;
        var methodCall = Expression.Call(dynamicFieldsProperty, setDynamicFieldMethod, 
            Expression.Constant("customField"), Expression.Constant(customValue, typeof(object)));
        var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult))!, methodCall);
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #dynField0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("captured-value");
    }

    #endregion

    #region RemoveDynamicField Tests

    [Fact]
    public void TranslateUpdateExpression_RemoveDynamicField_ShouldGenerateRemoveClause()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var dynamicFieldsProperty = Expression.Property(parameter, nameof(TestUpdateExpressions.DynamicFields));
        var removeDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.RemoveDynamicField))!;
        var methodCall = Expression.Call(dynamicFieldsProperty, removeDynamicFieldMethod, 
            Expression.Constant("tempData"));
        var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult))!, methodCall);
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #dynField0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("tempData");
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveDynamicFieldWithReservedWord_ShouldEscapeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // "data" is a DynamoDB reserved word
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var dynamicFieldsProperty = Expression.Property(parameter, nameof(TestUpdateExpressions.DynamicFields));
        var removeDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.RemoveDynamicField))!;
        var methodCall = Expression.Call(dynamicFieldsProperty, removeDynamicFieldMethod, 
            Expression.Constant("data"));
        var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult))!, methodCall);
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #dynField0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("data");
    }

    #endregion

    #region Combined Operations Tests

    [Fact]
    public void TranslateUpdateExpression_SetAndRemoveDynamicFields_ShouldCombineCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var dynamicFieldsProperty = Expression.Property(parameter, nameof(TestUpdateExpressions.DynamicFields));
        
        // SetDynamicField
        var setDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.SetDynamicField))!;
        var setMethodCall = Expression.Call(dynamicFieldsProperty, setDynamicFieldMethod, 
            Expression.Constant("newField"), Expression.Constant("value", typeof(object)));
        var setBinding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult))!, setMethodCall);
        
        // RemoveDynamicField
        var removeDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.RemoveDynamicField))!;
        var removeMethodCall = Expression.Call(dynamicFieldsProperty, removeDynamicFieldMethod, 
            Expression.Constant("oldField"));
        var removeBinding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult2))!, removeMethodCall);
        
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), setBinding, removeBinding);
        var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #dynField0 = :p0 REMOVE #dynField1");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("newField");
        context.AttributeNames.AttributeNames["#dynField1"].Should().Be("oldField");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("value");
    }

    [Fact]
    public void TranslateUpdateExpression_SetDynamicFieldWithMappedProperty_ShouldCombineCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var dynamicFieldsProperty = Expression.Property(parameter, nameof(TestUpdateExpressions.DynamicFields));
        
        // Regular property assignment
        var nameBinding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.Name))!, 
            Expression.Constant("John"));
        
        // SetDynamicField
        var setDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod(nameof(DynamicFieldAccessor.SetDynamicField))!;
        var setMethodCall = Expression.Call(dynamicFieldsProperty, setDynamicFieldMethod, 
            Expression.Constant("customField"), Expression.Constant("customValue", typeof(object)));
        var dynamicBinding = Expression.Bind(typeof(TestUpdateModel).GetProperty(nameof(TestUpdateModel.DynamicFieldResult))!, setMethodCall);
        
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), nameBinding, dynamicBinding);
        var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0, #dynField1 = :p1");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeNames.AttributeNames["#dynField1"].Should().Be("customField");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("customValue");
    }

    #endregion
}
