using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for list operation translation in UpdateExpressionTranslator.
/// Validates: Requirements 4.1, 4.2, 4.3, 4.6
/// 
/// These tests verify the translation of UpdateExpressionPropertyExtensions methods (Append, Prepend, AppendRange, PrependRange)
/// which are called on UpdateExpressionProperty&lt;List&lt;T&gt;&gt;.
/// </summary>
public class UpdateExpressionTranslatorListOperationTests
{
    #region Test Entity Classes

    // Update expressions type with list properties
    private class ItemUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<List<string>> Tags { get; } = new();
        public UpdateExpressionProperty<List<int>> Scores { get; } = new();
        public MetadataUpdateExpressions Metadata { get; } = new();
    }

    // Nested update expressions type
    private class MetadataUpdateExpressions
    {
        public UpdateExpressionProperty<List<string>> Keywords { get; } = new();
        public UpdateExpressionProperty<string?> Description { get; } = new();
    }

    // Update model for item
    private class ItemUpdateModel
    {
        public string? Id { get; set; }
        public List<string>? Tags { get; set; }
        public List<int>? Scores { get; set; }
        public MetadataUpdateModel? Metadata { get; set; }
    }

    // Update model for nested metadata
    private class MetadataUpdateModel
    {
        public List<string>? Keywords { get; set; }
        public string? Description { get; set; }
    }

    #endregion

    #region Helper Methods

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
            TableName = "Items",
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
                    PropertyName = "Tags",
                    AttributeName = "tags",
                    PropertyType = typeof(List<string>)
                },
                new PropertyMetadata
                {
                    PropertyName = "Scores",
                    AttributeName = "scores",
                    PropertyType = typeof(List<int>)
                },
                new PropertyMetadata
                {
                    PropertyName = "Metadata",
                    AttributeName = "metadata",
                    PropertyType = typeof(object)
                }
            }
        };
    }

    #endregion

    #region Append Tests (Requirement 4.1)

    [Fact]
    public void TranslateUpdateExpression_AppendSingleItem_ShouldGenerateListAppend()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.Append("new-tag") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        
        // Access x.Tags (UpdateExpressionProperty<List<string>>)
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // Find the Append method on UpdateExpressionPropertyExtensions
        var appendMethod = typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.Append))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var appendCall = Expression.Call(appendMethod, tagsProperty, Expression.Constant("new-tag"));
        
        // Create the update model assignment
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            appendCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0 = list_append(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        // The value should be a list containing the single item
        context.AttributeValues.AttributeValues[":p0"].L.Should().HaveCount(1);
        context.AttributeValues.AttributeValues[":p0"].L[0].S.Should().Be("new-tag");
    }

    [Fact]
    public void TranslateUpdateExpression_AppendIntItem_ShouldGenerateListAppend()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Scores = x.Scores.Append(100) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        
        // Access x.Scores
        var scoresProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Scores));
        
        // Find the Append method on UpdateExpressionPropertyExtensions
        var appendMethod = typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.Append))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(int));
        
        var appendCall = Expression.Call(appendMethod, scoresProperty, Expression.Constant(100));
        
        // Create the update model assignment
        var scoresBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Scores))!,
            appendCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            scoresBinding);
        
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0 = list_append(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("scores");
        context.AttributeValues.AttributeValues[":p0"].L.Should().HaveCount(1);
        context.AttributeValues.AttributeValues[":p0"].L[0].N.Should().Be("100");
    }

    #endregion

    #region Prepend Tests (Requirement 4.2)

    [Fact]
    public void TranslateUpdateExpression_PrependSingleItem_ShouldGenerateListPrepend()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.Prepend("priority-tag") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        
        // Access x.Tags
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // Find the Prepend method on UpdateExpressionPropertyExtensions
        var prependMethod = typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.Prepend))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var prependCall = Expression.Call(prependMethod, tagsProperty, Expression.Constant("priority-tag"));
        
        // Create the update model assignment
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            prependCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        // For prepend, the order is reversed: list_append(:val, #attr)
        result.Should().Be("SET #attr0 = list_append(:p0, #attr0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].L.Should().HaveCount(1);
        context.AttributeValues.AttributeValues[":p0"].L[0].S.Should().Be("priority-tag");
    }

    #endregion

    #region AppendRange Tests (Requirement 4.3)

    [Fact]
    public void TranslateUpdateExpression_AppendRangeMultipleItems_ShouldGenerateListAppend()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.AppendRange(new[] { "tag1", "tag2" }) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        
        // Access x.Tags
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // Create array of items
        var itemsArray = Expression.NewArrayInit(typeof(string), 
            Expression.Constant("tag1"), 
            Expression.Constant("tag2"));
        
        // Find the AppendRange method on UpdateExpressionPropertyExtensions
        var appendRangeMethod = typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.AppendRange))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var appendRangeCall = Expression.Call(appendRangeMethod, tagsProperty, itemsArray);
        
        // Create the update model assignment
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            appendRangeCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0 = list_append(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].L.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].L[0].S.Should().Be("tag1");
        context.AttributeValues.AttributeValues[":p0"].L[1].S.Should().Be("tag2");
    }

    [Fact]
    public void TranslateUpdateExpression_PrependRangeMultipleItems_ShouldGenerateListPrepend()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.PrependRange(new[] { "urgent", "priority" }) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        
        // Access x.Tags
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // Create array of items
        var itemsArray = Expression.NewArrayInit(typeof(string), 
            Expression.Constant("urgent"), 
            Expression.Constant("priority"));
        
        // Find the PrependRange method on UpdateExpressionPropertyExtensions
        var prependRangeMethod = typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.PrependRange))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var prependRangeCall = Expression.Call(prependRangeMethod, tagsProperty, itemsArray);
        
        // Create the update model assignment
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            prependRangeCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        // For prepend, the order is reversed: list_append(:val, #attr)
        result.Should().Be("SET #attr0 = list_append(:p0, #attr0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].L.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].L[0].S.Should().Be("urgent");
        context.AttributeValues.AttributeValues[":p0"].L[1].S.Should().Be("priority");
    }

    #endregion

    #region Captured Variable Tests

    [Fact]
    public void TranslateUpdateExpression_AppendWithCapturedVariable_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        var tagToAdd = "captured-tag";
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.Append(tagToAdd) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        
        // Access x.Tags
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // Find the Append method on UpdateExpressionPropertyExtensions
        var appendMethod = typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.Append))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var appendCall = Expression.Call(appendMethod, tagsProperty, Expression.Constant(tagToAdd));
        
        // Create the update model assignment
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            appendCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0 = list_append(#attr0, :p0)");
        context.AttributeValues.AttributeValues[":p0"].L[0].S.Should().Be("captured-tag");
    }

    #endregion
}
