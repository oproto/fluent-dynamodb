using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for list operation translation in UpdateExpressionTranslator.
/// Validates: Requirements 1.1, 1.2, 1.3, 3.1-3.6, 4.1, 4.2, 4.3, 4.6
/// 
/// These tests verify the translation of UpdateExpressionPropertyExtensions methods
/// (Append, Prepend, AppendRange, PrependRange, SetAt, RemoveAt)
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
        public UpdateExpressionProperty<int> Index { get; } = new();
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
        public int? Index { get; set; }
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
                    PropertyName = "Index",
                    AttributeName = "index",
                    PropertyType = typeof(int)
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

    /// <summary>
    /// Helper to get the SetAt method from UpdateExpressionPropertyExtensions.
    /// </summary>
    private static System.Reflection.MethodInfo GetSetAtMethod<T>()
    {
        return typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == "SetAt")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the SetAt method from ListOperationExtensions (for chained calls).
    /// </summary>
    private static System.Reflection.MethodInfo GetChainedSetAtMethod<T>()
    {
        return typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == "SetAt")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the RemoveAt method from UpdateExpressionPropertyExtensions.
    /// </summary>
    private static System.Reflection.MethodInfo GetRemoveAtMethod<T>()
    {
        return typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == "RemoveAt")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the RemoveAt method from ListOperationExtensions (for chained calls).
    /// </summary>
    private static System.Reflection.MethodInfo GetChainedRemoveAtMethod<T>()
    {
        return typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == "RemoveAt")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the Append method from UpdateExpressionPropertyExtensions.
    /// </summary>
    private static System.Reflection.MethodInfo GetAppendMethod<T>()
    {
        return typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == "Append")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the Append method from ListOperationExtensions (for chained calls).
    /// </summary>
    private static System.Reflection.MethodInfo GetChainedAppendMethod<T>()
    {
        return typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == "Append")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the Prepend method from UpdateExpressionPropertyExtensions.
    /// </summary>
    private static System.Reflection.MethodInfo GetPrependMethod<T>()
    {
        return typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == "Prepend")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the AppendRange method from UpdateExpressionPropertyExtensions.
    /// </summary>
    private static System.Reflection.MethodInfo GetAppendRangeMethod<T>()
    {
        return typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == "AppendRange")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the AppendRange method from ListOperationExtensions (for chained calls).
    /// </summary>
    private static System.Reflection.MethodInfo GetChainedAppendRangeMethod<T>()
    {
        return typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == "AppendRange")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the PrependRange method from UpdateExpressionPropertyExtensions.
    /// </summary>
    private static System.Reflection.MethodInfo GetPrependRangeMethod<T>()
    {
        return typeof(UpdateExpressionPropertyExtensions)
            .GetMethods()
            .Where(m => m.Name == "PrependRange")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
    }

    /// <summary>
    /// Helper to get the PrependRange method from ListOperationExtensions (for chained calls).
    /// </summary>
    private static System.Reflection.MethodInfo GetChainedPrependRangeMethod<T>()
    {
        return typeof(ListOperationExtensions)
            .GetMethods()
            .Where(m => m.Name == "PrependRange")
            .Where(m => m.IsGenericMethod)
            .Single()
            .MakeGenericMethod(typeof(T));
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
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var appendCall = Expression.Call(GetAppendMethod<string>(), tagsProperty, Expression.Constant("new-tag"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, appendCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0 = list_append(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
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
        var scoresProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Scores));
        var appendCall = Expression.Call(GetAppendMethod<int>(), scoresProperty, Expression.Constant(100));
        
        var scoresBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Scores))!, appendCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), scoresBinding);
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
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var prependCall = Expression.Call(GetPrependMethod<string>(), tagsProperty, Expression.Constant("priority-tag"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, prependCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
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
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var itemsArray = Expression.NewArrayInit(typeof(string), Expression.Constant("tag1"), Expression.Constant("tag2"));
        var appendRangeCall = Expression.Call(GetAppendRangeMethod<string>(), tagsProperty, itemsArray);
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, appendRangeCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
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
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var itemsArray = Expression.NewArrayInit(typeof(string), Expression.Constant("urgent"), Expression.Constant("priority"));
        var prependRangeCall = Expression.Call(GetPrependRangeMethod<string>(), tagsProperty, itemsArray);
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, prependRangeCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
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
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var appendCall = Expression.Call(GetAppendMethod<string>(), tagsProperty, Expression.Constant(tagToAdd));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, appendCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
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
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var setAtCall = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(0), Expression.Constant("updated"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, setAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

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
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var scoresProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Scores));
        var setAtCall = Expression.Call(GetSetAtMethod<int>(), scoresProperty, Expression.Constant(1), Expression.Constant(99));
        
        var scoresBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Scores))!, setAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), scoresBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[1] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("scores");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("99");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtHighIndex_ShouldGenerateSetWithIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(5, "fifth") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var setAtCall = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(5), Expression.Constant("fifth"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, setAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[5] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
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
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var removeAtCall = Expression.Call(GetRemoveAtMethod<string>(), tagsProperty, Expression.Constant(2));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, removeAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[2]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtFirstIndex_ShouldGenerateRemoveWithIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(0) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var removeAtCall = Expression.Call(GetRemoveAtMethod<string>(), tagsProperty, Expression.Constant(0));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, removeAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

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
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var scoresProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Scores));
        var removeAtCall = Expression.Call(GetRemoveAtMethod<int>(), scoresProperty, Expression.Constant(1));
        
        var scoresBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Scores))!, removeAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), scoresBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[1]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("scores");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtHighIndex_ShouldGenerateRemoveWithIndex()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(10) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        var removeAtCall = Expression.Call(GetRemoveAtMethod<string>(), tagsProperty, Expression.Constant(10));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, removeAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[10]");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
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
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // First SetAt uses UpdateExpressionPropertyExtensions (on UpdateExpressionProperty<List<T>>)
        var firstSetAt = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(0), Expression.Constant("first"));
        // Second SetAt uses ListOperationExtensions (on List<T>, the return type)
        var secondSetAt = Expression.Call(GetChainedSetAtMethod<string>(), firstSetAt, Expression.Constant(1), Expression.Constant("second"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, secondSetAt);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
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
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // First SetAt uses UpdateExpressionPropertyExtensions
        var firstSetAt = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(0), Expression.Constant("a"));
        // Subsequent SetAt calls use ListOperationExtensions
        var secondSetAt = Expression.Call(GetChainedSetAtMethod<string>(), firstSetAt, Expression.Constant(1), Expression.Constant("b"));
        var thirdSetAt = Expression.Call(GetChainedSetAtMethod<string>(), secondSetAt, Expression.Constant(2), Expression.Constant("c"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, thirdSetAt);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[0] = :p0, #attr0[1] = :p1, #attr0[2] = :p2");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("a");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("b");
        context.AttributeValues.AttributeValues[":p2"].S.Should().Be("c");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtNonConsecutiveIndices_ShouldGenerateCombinedSetExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(5, "fifth").SetAt(10, "tenth") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var firstSetAt = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(5), Expression.Constant("fifth"));
        var secondSetAt = Expression.Call(GetChainedSetAtMethod<string>(), firstSetAt, Expression.Constant(10), Expression.Constant("tenth"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, secondSetAt);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[5] = :p0, #attr0[10] = :p1");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("fifth");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("tenth");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtWithIntList_ShouldGenerateCombinedSetExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Scores = x.Scores.SetAt(0, 100).SetAt(1, 200) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var scoresProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Scores));
        
        var firstSetAt = Expression.Call(GetSetAtMethod<int>(), scoresProperty, Expression.Constant(0), Expression.Constant(100));
        var secondSetAt = Expression.Call(GetChainedSetAtMethod<int>(), firstSetAt, Expression.Constant(1), Expression.Constant(200));
        
        var scoresBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Scores))!, secondSetAt);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), scoresBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[0] = :p0, #attr0[1] = :p1");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("scores");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("100");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("200");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtSameIndex_ShouldThrowDuplicateIndexException()
    {
        // Arrange - Duplicate indices in chained SetAt should throw
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").SetAt(0, "duplicate") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var firstSetAt = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(0), Expression.Constant("first"));
        var secondSetAt = Expression.Call(GetChainedSetAtMethod<string>(), firstSetAt, Expression.Constant(0), Expression.Constant("duplicate"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, secondSetAt);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert - Duplicate indices should throw
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*duplicate*");
    }

    #endregion


    #region Overlapping Path Detection Tests (DynamoDB Limitation Handling)

    [Fact]
    public void TranslateUpdateExpression_SetAtThenAppend_ShouldThrowOverlappingPathException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").Append("new") }
        // This should throw because SetAt + Append creates overlapping paths
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var setAtCall = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(0), Expression.Constant("first"));
        var appendCall = Expression.Call(GetChainedAppendMethod<string>(), setAtCall, Expression.Constant("new"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, appendCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_AppendThenSetAt_ShouldThrowOverlappingPathException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.Append("new").SetAt(0, "first") }
        // This should throw because Append + SetAt creates overlapping paths
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var appendCall = Expression.Call(GetAppendMethod<string>(), tagsProperty, Expression.Constant("new"));
        var setAtCall = Expression.Call(GetChainedSetAtMethod<string>(), appendCall, Expression.Constant(0), Expression.Constant("first"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, setAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtThenRemoveAt_ShouldThrowOverlappingPathException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").RemoveAt(1) }
        // This should throw because SetAt + RemoveAt creates overlapping paths (SET + REMOVE)
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var setAtCall = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(0), Expression.Constant("first"));
        var removeAtCall = Expression.Call(GetChainedRemoveAtMethod<string>(), setAtCall, Expression.Constant(1));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, removeAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtThenSetAt_ShouldThrowOverlappingPathException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(0).SetAt(1, "first") }
        // This should throw because RemoveAt + SetAt creates overlapping paths
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var removeAtCall = Expression.Call(GetRemoveAtMethod<string>(), tagsProperty, Expression.Constant(0));
        var setAtCall = Expression.Call(GetChainedSetAtMethod<string>(), removeAtCall, Expression.Constant(1), Expression.Constant("first"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, setAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtThenAppendRange_ShouldThrowOverlappingPathException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").AppendRange(new[] { "a", "b" }) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var setAtCall = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(0), Expression.Constant("first"));
        var itemsArray = Expression.NewArrayInit(typeof(string), Expression.Constant("a"), Expression.Constant("b"));
        var appendRangeCall = Expression.Call(GetChainedAppendRangeMethod<string>(), setAtCall, itemsArray);
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, appendRangeCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*overlapping*");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtThenPrependRange_ShouldThrowOverlappingPathException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "first").PrependRange(new[] { "a", "b" }) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var setAtCall = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(0), Expression.Constant("first"));
        var itemsArray = Expression.NewArrayInit(typeof(string), Expression.Constant("a"), Expression.Constant("b"));
        var prependRangeCall = Expression.Call(GetChainedPrependRangeMethod<string>(), setAtCall, itemsArray);
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, prependRangeCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*overlapping*");
    }

    #endregion

    #region Dynamic Index Tests (Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6)

    [Fact]
    public void TranslateUpdateExpression_SetAtWithVariableIndex_ShouldEvaluateAndGenerateSet()
    {
        // Arrange - Requirement 3.1: Support variable indices
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        int index = 1;
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(index, "val") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // Create a closure to capture the variable
        var indexExpr = Expression.Constant(index);
        var setAtCall = Expression.Call(GetSetAtMethod<string>(), tagsProperty, indexExpr, Expression.Constant("val"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, setAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[1] = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("val");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithVariableIndex_ShouldEvaluateAndGenerateRemove()
    {
        // Arrange - Requirement 3.1: Support variable indices
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        int index = 3;
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(index) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var indexExpr = Expression.Constant(index);
        var removeAtCall = Expression.Call(GetRemoveAtMethod<string>(), tagsProperty, indexExpr);
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, removeAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0[3]");
    }

    [Fact]
    public void TranslateUpdateExpression_SetAtWithNegativeIndex_ShouldThrowArgumentOutOfRange()
    {
        // Arrange - Requirement 3.5: Validate non-negative indices
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        int index = -1;
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(-1, "val") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var indexExpr = Expression.Constant(index);
        var setAtCall = Expression.Call(GetSetAtMethod<string>(), tagsProperty, indexExpr, Expression.Constant("val"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, setAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*non-negative*");
    }

    [Fact]
    public void TranslateUpdateExpression_RemoveAtWithNegativeIndex_ShouldThrowArgumentOutOfRange()
    {
        // Arrange - Requirement 3.5: Validate non-negative indices
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        int index = -5;
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.RemoveAt(-5) }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        var indexExpr = Expression.Constant(index);
        var removeAtCall = Expression.Call(GetRemoveAtMethod<string>(), tagsProperty, indexExpr);
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, removeAtCall);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*non-negative*");
    }

    [Fact]
    public void TranslateUpdateExpression_ChainedSetAtWithVariableIndices_ShouldEvaluateAndGenerateCombinedSet()
    {
        // Arrange - Chained SetAt with variable indices
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        int firstIndex = 1;
        int secondIndex = 3;
        
        // Build expression: x => new ItemUpdateModel { Tags = x.Tags.SetAt(firstIndex, "a").SetAt(secondIndex, "b") }
        var parameter = Expression.Parameter(typeof(ItemUpdateExpressions), "x");
        var tagsProperty = Expression.Property(parameter, nameof(ItemUpdateExpressions.Tags));
        
        // First SetAt uses UpdateExpressionPropertyExtensions (on UpdateExpressionProperty<List<T>>)
        var firstSetAt = Expression.Call(GetSetAtMethod<string>(), tagsProperty, Expression.Constant(firstIndex), Expression.Constant("a"));
        // Second SetAt uses ListOperationExtensions (on List<T>, the return type)
        var secondSetAt = Expression.Call(GetChainedSetAtMethod<string>(), firstSetAt, Expression.Constant(secondIndex), Expression.Constant("b"));
        
        var tagsBinding = Expression.Bind(typeof(ItemUpdateModel).GetProperty(nameof(ItemUpdateModel.Tags))!, secondSetAt);
        var itemInit = Expression.MemberInit(Expression.New(typeof(ItemUpdateModel)), tagsBinding);
        var lambda = Expression.Lambda<Func<ItemUpdateExpressions, ItemUpdateModel>>(itemInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0[1] = :p0, #attr0[3] = :p1");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("a");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("b");
    }

    #endregion
}
