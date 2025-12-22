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

    #region SetAt Tests (Requirement 1.1, 1.3)

    [Fact]
    public void TranslateUpdateExpression_SetAtConstantIndex_ShouldGenerateSetWithIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "updated") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        
        // Access x.Tags (we need to get the underlying List<string> property)
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // Find the SetAt method on ListOperationExtensions
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // For extension methods on List<T>, we need to create a member access that gets the list
        // In the actual usage, x.Tags would be a List<string> property on the entity
        // For testing, we'll simulate this by creating a proper expression tree
        
        // Create a mock list expression (simulating x.Tags as List<string>)
        var listType = typeof(List<string>);
        var mockListParam = Expression.Parameter(listType, "mockList");
        
        // Actually, for the translator to work, we need to build the expression as it would appear
        // in real usage. The translator expects Arguments[0] to be a MemberExpression chain.
        
        // Let's create a test entity type that has List<string> Tags property
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(0), Expression.Constant("updated"));
        
        // Create the update model assignment
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[0] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("updated");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtWithIntValue_ShouldGenerateSetWithIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Scores = x.Scores.SetAt(1, 99) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var scoresListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Scores));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(int));
        
        var setAtCall = Expression.Call(setAtMethod, scoresListProperty, Expression.Constant(1), Expression.Constant(99));
        
        var scoresBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Scores))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            scoresBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[1] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("scores");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("99");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtNestedList_ShouldGenerateSetWithNestedPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Metadata = new MetadataUpdateModel { Keywords = x.Metadata.Keywords.SetAt(0, "updated") } }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        
        // Access x.Metadata.Keywords
        var metadataProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Metadata));
        var keywordsProperty = Expression.Property(metadataProperty, nameof(TestMetadata.Keywords));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var setAtCall = Expression.Call(setAtMethod, keywordsProperty, Expression.Constant(0), Expression.Constant("updated"));
        
        // Create nested update model
        var keywordsBinding = Expression.Bind(
            typeof(MetadataUpdateModel).GetProperty(nameof(MetadataUpdateModel.Keywords))!,
            setAtCall);
        var metadataInit = Expression.MemberInit(
            Expression.New(typeof(MetadataUpdateModel)),
            keywordsBinding);
        
        var metadataBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Metadata))!,
            metadataInit);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            metadataBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        // The nested path should be: #metadata.#keywords[0] = :p0
        result.Should().Contain("#attr0.#attr1[0] = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("updated");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtWithDifferentIndex_ShouldGenerateCorrectIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(5, "fifth") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(5), Expression.Constant("fifth"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[5] = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("fifth");
    }

    #endregion

    #region RemoveAt Tests (Requirement 1.2, 1.3)

    [Fact]
    public void TranslateUpdateExpression_RemoveAtConstantIndex_ShouldGenerateRemoveWithIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(2) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, Expression.Constant(2));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[2]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtFirstIndex_ShouldGenerateRemoveWithIndexZero()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(0) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, Expression.Constant(0));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[0]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithIntList_ShouldGenerateRemoveWithIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Scores = x.Scores.RemoveAt(1) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var scoresListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Scores));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(int));
        
        var removeAtCall = Expression.Call(removeAtMethod, scoresListProperty, Expression.Constant(1));
        
        var scoresBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Scores))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            scoresBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[1]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("scores");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtNestedList_ShouldGenerateRemoveWithNestedPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression that accesses nested list directly: x.Metadata.Keywords.RemoveAt(1)
        // The RemoveAt extension method extracts the path from the list expression (x.Metadata.Keywords)
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        
        // Access x.Metadata.Keywords directly (nested path)
        var metadataProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Metadata));
        var keywordsProperty = Expression.Property(metadataProperty, nameof(TestMetadata.Keywords));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var removeAtCall = Expression.Call(removeAtMethod, keywordsProperty, Expression.Constant(1));
        
        // Assign to Tags property in the update model (the property name doesn't matter for the path extraction)
        // The RemoveAt translation extracts the path from the list expression (x.Metadata.Keywords)
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        // The nested path should be: REMOVE #metadata.#keywords[1]
        result.Should().Contain("REMOVE");
        result.Should().Contain("[1]");
        // Verify the path includes both metadata and keywords
        context.AttributeNames.AttributeNames.Values.Should().Contain("metadata");
        context.AttributeNames.AttributeNames.Values.Should().Contain("keywords");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithDifferentIndex_ShouldGenerateCorrectIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(10) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, Expression.Constant(10));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[10]");
    }

    #endregion

    #region Test Entity Classes for SetAt/RemoveAt

    private class TestEntityWithList
    {
        public List<string> Tags { get; set; } = new();
        public List<int> Scores { get; set; } = new();
        public TestMetadata Metadata { get; set; } = new();
    }

    private class TestMetadata
    {
        public List<string> Keywords { get; set; } = new();
        public string? Description { get; set; }
    }

    #endregion

    #region Chained SetAt Tests (Requirement 1.1, 1.3)

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtTwoIndices_ShouldGenerateCombinedSetExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").SetAt(1, "second") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // First SetAt: x.Tags.SetAt(0, "first")
        var firstSetAt = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(0), Expression.Constant("first"));
        
        // Second SetAt: .SetAt(1, "second") - chained on the result of the first
        var secondSetAt = Expression.Call(setAtMethod, firstSetAt, Expression.Constant(1), Expression.Constant("second"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            secondSetAt);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        // Should generate: SET #tags[0] = :v0, #tags[1] = :v1
        result.Should().Be("SET #attr0[0] = :p0, #attr0[1] = :p1");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("first");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("second");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtThreeIndices_ShouldGenerateCombinedSetExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "a").SetAt(1, "b").SetAt(2, "c") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.SetAt(0, "a").SetAt(1, "b").SetAt(2, "c")
        var firstSetAt = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(0), Expression.Constant("a"));
        var secondSetAt = Expression.Call(setAtMethod, firstSetAt, Expression.Constant(1), Expression.Constant("b"));
        var thirdSetAt = Expression.Call(setAtMethod, secondSetAt, Expression.Constant(2), Expression.Constant("c"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            thirdSetAt);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        // Should generate: SET #tags[0] = :v0, #tags[1] = :v1, #tags[2] = :v2
        result.Should().Be("SET #attr0[0] = :p0, #attr0[1] = :p1, #attr0[2] = :p2");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("a");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("b");
        context.AttributeValues.AttributeValues[":p2"].S.Should().Be("c");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtNonSequentialIndices_ShouldGenerateCorrectIndices()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(5, "fifth").SetAt(10, "tenth") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.SetAt(5, "fifth").SetAt(10, "tenth")
        var firstSetAt = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(5), Expression.Constant("fifth"));
        var secondSetAt = Expression.Call(setAtMethod, firstSetAt, Expression.Constant(10), Expression.Constant("tenth"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            secondSetAt);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[5] = :p0, #attr0[10] = :p1");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("fifth");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("tenth");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtWithIntValues_ShouldGenerateCombinedSetExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Scores = x.Scores.SetAt(0, 100).SetAt(1, 200) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var scoresListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Scores));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(int));
        
        // Chain: x.Scores.SetAt(0, 100).SetAt(1, 200)
        var firstSetAt = Expression.Call(setAtMethod, scoresListProperty, Expression.Constant(0), Expression.Constant(100));
        var secondSetAt = Expression.Call(setAtMethod, firstSetAt, Expression.Constant(1), Expression.Constant(200));
        
        var scoresBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Scores))!,
            secondSetAt);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            scoresBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[0] = :p0, #attr0[1] = :p1");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("scores");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("100");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("200");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtDuplicateIndex_ShouldThrowException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").SetAt(0, "duplicate") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain with duplicate index: x.Tags.SetAt(0, "first").SetAt(0, "duplicate")
        var firstSetAt = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(0), Expression.Constant("first"));
        var secondSetAt = Expression.Call(setAtMethod, firstSetAt, Expression.Constant(0), Expression.Constant("duplicate"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            secondSetAt);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*duplicate*index*0*");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtNestedList_ShouldGenerateCombinedSetWithNestedPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression for nested list: x.Metadata.Keywords.SetAt(0, "first").SetAt(1, "second")
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        
        // Access x.Metadata.Keywords
        var metadataProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Metadata));
        var keywordsProperty = Expression.Property(metadataProperty, nameof(TestMetadata.Keywords));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Metadata.Keywords.SetAt(0, "first").SetAt(1, "second")
        var firstSetAt = Expression.Call(setAtMethod, keywordsProperty, Expression.Constant(0), Expression.Constant("first"));
        var secondSetAt = Expression.Call(setAtMethod, firstSetAt, Expression.Constant(1), Expression.Constant("second"));
        
        // Create nested update model
        var keywordsBinding = Expression.Bind(
            typeof(MetadataUpdateModel).GetProperty(nameof(MetadataUpdateModel.Keywords))!,
            secondSetAt);
        var metadataInit = Expression.MemberInit(
            Expression.New(typeof(MetadataUpdateModel)),
            keywordsBinding);
        
        var metadataBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Metadata))!,
            metadataInit);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            metadataBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        // The nested path should be: SET #metadata.#keywords[0] = :v0, #metadata.#keywords[1] = :v1
        result.Should().Contain("[0] = :p0");
        result.Should().Contain("[1] = :p1");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("first");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("second");
    }

    #endregion

    #region Overlapping Path Detection Tests (DynamoDB Limitation Handling)

    [Fact]
    public void TranslateUpdateExpression_SetAtChainedWithAppend_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").Append("new") }
        // This should throw because SetAt + Append creates overlapping paths
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var appendMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.Append))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.SetAt(0, "first").Append("new")
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(0), Expression.Constant("first"));
        var appendCall = Expression.Call(appendMethod, setAtCall, Expression.Constant("new"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            appendCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*Append*SetAt*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_AppendChainedWithSetAt_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.Append("new").SetAt(0, "first") }
        // This should throw because Append + SetAt creates overlapping paths
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var appendMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.Append))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.Append("new").SetAt(0, "first")
        var appendCall = Expression.Call(appendMethod, tagsListProperty, Expression.Constant("new"));
        var setAtCall = Expression.Call(setAtMethod, appendCall, Expression.Constant(0), Expression.Constant("first"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*SetAt*Append*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtChainedWithRemoveAt_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").RemoveAt(1) }
        // This should throw because SetAt + RemoveAt creates overlapping paths (SET + REMOVE)
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.SetAt(0, "first").RemoveAt(1)
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(0), Expression.Constant("first"));
        var removeAtCall = Expression.Call(removeAtMethod, setAtCall, Expression.Constant(1));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*RemoveAt*SetAt*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtChainedWithSetAt_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(0).SetAt(1, "second") }
        // This should throw because RemoveAt + SetAt creates overlapping paths
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.RemoveAt(0).SetAt(1, "second")
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, Expression.Constant(0));
        var setAtCall = Expression.Call(setAtMethod, removeAtCall, Expression.Constant(1), Expression.Constant("second"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*SetAt*RemoveAt*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_AppendChainedWithRemoveAt_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.Append("new").RemoveAt(0) }
        // This should throw because Append + RemoveAt creates overlapping paths
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var appendMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.Append))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.Append("new").RemoveAt(0)
        var appendCall = Expression.Call(appendMethod, tagsListProperty, Expression.Constant("new"));
        var removeAtCall = Expression.Call(removeAtMethod, appendCall, Expression.Constant(0));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*RemoveAt*Append*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtChainedWithAppend_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(0).Append("new") }
        // This should throw because RemoveAt + Append creates overlapping paths
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var appendMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.Append))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.RemoveAt(0).Append("new")
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, Expression.Constant(0));
        var appendCall = Expression.Call(appendMethod, removeAtCall, Expression.Constant("new"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            appendCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*Append*RemoveAt*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_PrependChainedWithSetAt_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.Prepend("priority").SetAt(0, "first") }
        // This should throw because Prepend + SetAt creates overlapping paths
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var prependMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.Prepend))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.Prepend("priority").SetAt(0, "first")
        var prependCall = Expression.Call(prependMethod, tagsListProperty, Expression.Constant("priority"));
        var setAtCall = Expression.Call(setAtMethod, prependCall, Expression.Constant(0), Expression.Constant("first"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*SetAt*Prepend*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_AppendChainedWithPrepend_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.Append("new").Prepend("priority") }
        // This should throw because Append + Prepend creates overlapping paths (both modify entire list)
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var appendMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.Append))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var prependMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.Prepend))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.Append("new").Prepend("priority")
        var appendCall = Expression.Call(appendMethod, tagsListProperty, Expression.Constant("new"));
        var prependCall = Expression.Call(prependMethod, appendCall, Expression.Constant("priority"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            prependCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*Prepend*Append*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtChainedWithAppendRange_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").AppendRange(new[] { "a", "b" }) }
        // This should throw because SetAt + AppendRange creates overlapping paths
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var appendRangeMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.AppendRange))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.SetAt(0, "first").AppendRange(new[] { "a", "b" })
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(0), Expression.Constant("first"));
        var itemsArray = Expression.NewArrayInit(typeof(string), Expression.Constant("a"), Expression.Constant("b"));
        var appendRangeCall = Expression.Call(appendRangeMethod, setAtCall, itemsArray);
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            appendRangeCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*AppendRange*SetAt*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtChainedWithPrependRange_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").PrependRange(new[] { "a", "b" }) }
        // This should throw because SetAt + PrependRange creates overlapping paths
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var prependRangeMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.PrependRange))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Chain: x.Tags.SetAt(0, "first").PrependRange(new[] { "a", "b" })
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, Expression.Constant(0), Expression.Constant("first"));
        var itemsArray = Expression.NewArrayInit(typeof(string), Expression.Constant("a"), Expression.Constant("b"));
        var prependRangeCall = Expression.Call(prependRangeMethod, setAtCall, itemsArray);
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            prependRangeCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Cannot chain*PrependRange*SetAt*overlapping*");
    }

    #endregion

    #region Dynamic Index Tests (Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6)

    /// <summary>
    /// Helper class for testing property access indices.
    /// </summary>
    private class IndexConfig
    {
        public int Index { get; set; }
    }

    /// <summary>
    /// Helper class for testing method call indices.
    /// </summary>
    private class IndexHelper
    {
        private readonly int _index;
        public IndexHelper(int index) => _index = index;
        public int GetIndex() => _index;
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtWithVariableIndex_ShouldEvaluateAndGenerateCorrectExpression()
    {
        // Arrange
        // Validates: Requirement 3.1 - Support variable indices in SetAt extension method
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: int i = 1; x => new ItemUpdateModel { Tags = x.Tags.SetAt(i, "val") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create a captured variable expression (simulating: int i = 1; ... x.Tags.SetAt(i, "val"))
        int i = 1;
        Expression<Func<int>> indexLambda = () => i;
        var indexExpr = indexLambda.Body;
        
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, indexExpr, Expression.Constant("val"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[1] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("val");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtWithMethodCallIndex_ShouldEvaluateAndGenerateCorrectExpression()
    {
        // Arrange
        // Validates: Requirement 3.3 - Support method call indices in update expressions
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(GetIndex(), "val") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create a method call expression (simulating: helper.GetIndex())
        var helper = new IndexHelper(2);
        Expression<Func<int>> indexLambda = () => helper.GetIndex();
        var indexExpr = indexLambda.Body;
        
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, indexExpr, Expression.Constant("val"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[2] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("val");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtWithPropertyAccessIndex_ShouldEvaluateAndGenerateCorrectExpression()
    {
        // Arrange
        // Validates: Requirement 3.4 - Support property access indices in update expressions
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(config.Index, "val") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create a property access expression (simulating: config.Index)
        var config = new IndexConfig { Index = 3 };
        Expression<Func<int>> indexLambda = () => config.Index;
        var indexExpr = indexLambda.Body;
        
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, indexExpr, Expression.Constant("val"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[3] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("val");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtWithEntityParameterIndex_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        // Validates: Requirement 3.5 - Reject indices that reference the entity parameter
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(x.Index, "val") }
        // This should throw because the index references the entity parameter
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithIndex), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithIndex.Tags));
        var indexProperty = Expression.Property(testEntityParam, nameof(TestEntityWithIndex.Index));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, indexProperty, Expression.Constant("val"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithIndex, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*List index cannot reference the entity parameter*");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtWithNegativeIndex_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        // Validates: Requirement 3.6 - Validate index is non-negative at translation time
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: int i = -1; x => new ItemUpdateModel { Tags = x.Tags.SetAt(i, "val") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create a captured variable expression with negative value
        int i = -1;
        Expression<Func<int>> indexLambda = () => i;
        var indexExpr = indexLambda.Body;
        
        var setAtCall = Expression.Call(setAtMethod, tagsListProperty, indexExpr, Expression.Constant("val"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            setAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*List index must be non-negative*-1*");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithVariableIndex_ShouldEvaluateAndGenerateCorrectExpression()
    {
        // Arrange
        // Validates: Requirement 3.2 - Support variable indices in RemoveAt extension method
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: int i = 2; x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(i) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create a captured variable expression
        int i = 2;
        Expression<Func<int>> indexLambda = () => i;
        var indexExpr = indexLambda.Body;
        
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, indexExpr);
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[2]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithMethodCallIndex_ShouldEvaluateAndGenerateCorrectExpression()
    {
        // Arrange
        // Validates: Requirement 3.3 - Support method call indices in update expressions
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(GetIndex()) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create a method call expression
        var helper = new IndexHelper(4);
        Expression<Func<int>> indexLambda = () => helper.GetIndex();
        var indexExpr = indexLambda.Body;
        
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, indexExpr);
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[4]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithPropertyAccessIndex_ShouldEvaluateAndGenerateCorrectExpression()
    {
        // Arrange
        // Validates: Requirement 3.4 - Support property access indices in update expressions
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(config.Index) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create a property access expression
        var config = new IndexConfig { Index = 5 };
        Expression<Func<int>> indexLambda = () => config.Index;
        var indexExpr = indexLambda.Body;
        
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, indexExpr);
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[5]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithEntityParameterIndex_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        // Validates: Requirement 3.5 - Reject indices that reference the entity parameter
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(x.Index) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithIndex), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithIndex.Tags));
        var indexProperty = Expression.Property(testEntityParam, nameof(TestEntityWithIndex.Index));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, indexProperty);
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithIndex, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*List index cannot reference the entity parameter*");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithNegativeIndex_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        // Validates: Requirement 3.6 - Validate index is non-negative at translation time
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: int i = -2; x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(i) }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var removeAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.RemoveAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create a captured variable expression with negative value
        int i = -2;
        Expression<Func<int>> indexLambda = () => i;
        var indexExpr = indexLambda.Body;
        
        var removeAtCall = Expression.Call(removeAtMethod, tagsListProperty, indexExpr);
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            removeAtCall);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*List index must be non-negative*-2*");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtWithVariableIndices_ShouldEvaluateAndGenerateCorrectExpression()
    {
        // Arrange
        // Validates: Requirement 3.1 - Support variable indices in chained SetAt operations
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: int i = 0, j = 1; x => new ItemUpdateModel { Tags = x.Tags.SetAt(i, "a").SetAt(j, "b") }
        var testEntityParam = Expression.Parameter(typeof(TestEntityWithList), "entity");
        var tagsListProperty = Expression.Property(testEntityParam, nameof(TestEntityWithList.Tags));
        
        var setAtMethod = typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(ListOperationExtensions.SetAt))
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(string));
        
        // Create captured variable expressions
        int i = 0, j = 1;
        Expression<Func<int>> indexLambda1 = () => i;
        Expression<Func<int>> indexLambda2 = () => j;
        
        // Chain: x.Tags.SetAt(i, "a").SetAt(j, "b")
        var firstSetAt = Expression.Call(setAtMethod, tagsListProperty, indexLambda1.Body, Expression.Constant("a"));
        var secondSetAt = Expression.Call(setAtMethod, firstSetAt, indexLambda2.Body, Expression.Constant("b"));
        
        var tagsBinding = Expression.Bind(
            typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!,
            secondSetAt);
        var itemInit = Expression.MemberInit(
            Expression.New(typeof(ItemUpdateModel)),
            tagsBinding);
        
        var lambda = Expression.Lambda<Func<TestEntityWithList, ItemUpdateModel>>(itemInit, testEntityParam);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[0] = :p0, #attr0[1] = :p1");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("a");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("b");
    }

    #endregion

    #region Test Entity Classes for Dynamic Index Tests

    private class TestEntityWithIndex
    {
        public List<string> Tags { get; set; } = new();
        public int Index { get; set; }
    }

    #endregion
}
