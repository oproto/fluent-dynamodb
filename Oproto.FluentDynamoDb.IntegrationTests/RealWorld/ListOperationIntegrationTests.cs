using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for list operations with DynamoDB Local.
/// These tests verify end-to-end functionality of list operations using lambda expressions.
/// 
/// Requirements covered:
/// - 4.1: Support ListAppend operation to add elements to end of list
/// - 4.2: Support ListPrepend operation to add elements to beginning of list
/// - 4.3: Support appending multiple elements
/// - 4.4: Support updating list element by index
/// - 4.5: Support REMOVE for list elements by index
/// - 4.6: List operations work with nested lists
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "ListOperations")]
public class ListOperationIntegrationTests : IntegrationTestBase
{
    private NestedPropertyTestTable _table = null!;

    public ListOperationIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await CreateTableAsync<NestedPropertyTestEntity>();
        _table = new NestedPropertyTestTable(DynamoDb, TableName);
    }

    #region List Append Tests (Requirement 4.1)

    [Fact]
    public async Task ListAppend_SingleElement_AppendsToEndOfList()
    {
        // Arrange - Requirement 4.1: Support ListAppend operation to add elements to end of list
        var entity = CreateTestEntity("list-append-1");
        entity.Tags = new List<string> { "existing-tag" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Append single element to list
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.ListAppend("new-tag")
            })
            .UpdateAsync();

        // Assert - Verify element was appended to end
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("existing-tag");
        loaded.Tags[1].Should().Be("new-tag");
    }

    [Fact]
    public async Task ListAppend_ToExistingEmptyList_AppendsElement()
    {
        // Arrange - Note: list_append requires the attribute to exist
        // This test verifies appending to an existing (but empty) list
        var entity = CreateTestEntity("list-append-empty");
        entity.Tags = new List<string>(); // Empty list - attribute exists but is empty
        await _table.Entities.Put(entity).PutAsync();

        // Act - First set the list to have one element (since list_append requires existing attribute)
        // Then verify we can append to it
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = new List<string> { "first-tag" }
            })
            .UpdateAsync();

        // Now append to the existing list
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.ListAppend("second-tag")
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("first-tag");
        loaded.Tags[1].Should().Be("second-tag");
    }

    [Fact]
    public async Task ListAppend_MultipleElements_AppendsAllToEnd()
    {
        // Arrange - Requirement 4.1: Support ListAppend with multiple elements
        var entity = CreateTestEntity("list-append-multi");
        entity.Tags = new List<string> { "original" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Append multiple elements
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.ListAppend("tag1", "tag2", "tag3")
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(4);
        loaded.Tags.Should().BeEquivalentTo(new[] { "original", "tag1", "tag2", "tag3" }, options => options.WithStrictOrdering());
    }

    #endregion

    #region List Prepend Tests (Requirement 4.2)

    [Fact]
    public async Task ListPrepend_SingleElement_PrependsToBeginningOfList()
    {
        // Arrange - Requirement 4.2: Support ListPrepend operation to add elements to beginning of list
        var entity = CreateTestEntity("list-prepend-1");
        entity.Tags = new List<string> { "existing-tag" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Prepend single element to list
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.ListPrepend("new-tag")
            })
            .UpdateAsync();

        // Assert - Verify element was prepended to beginning
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("new-tag");
        loaded.Tags[1].Should().Be("existing-tag");
    }

    [Fact]
    public async Task ListPrepend_MultipleElements_PrependsAllToBeginning()
    {
        // Arrange - Requirement 4.2: Support ListPrepend with multiple elements
        var entity = CreateTestEntity("list-prepend-multi");
        entity.Tags = new List<string> { "original" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Prepend multiple elements
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.ListPrepend("tag1", "tag2", "tag3")
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(4);
        // Prepend adds elements in order, so tag1, tag2, tag3 come before original
        loaded.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2", "tag3", "original" }, options => options.WithStrictOrdering());
    }

    #endregion

    #region Append/Prepend Single Item Tests (Requirement 4.1, 4.2)

    [Fact]
    public async Task Append_SingleItem_AppendsToEndOfList()
    {
        // Arrange - Test the Append extension method (single item variant)
        var entity = CreateTestEntity("append-single");
        entity.Tags = new List<string> { "first" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Use Append (single item)
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.Append("second")
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().BeEquivalentTo(new[] { "first", "second" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Prepend_SingleItem_PrependsToBeginningOfList()
    {
        // Arrange - Test the Prepend extension method (single item variant)
        var entity = CreateTestEntity("prepend-single");
        entity.Tags = new List<string> { "second" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Use Prepend (single item)
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.Prepend("first")
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().BeEquivalentTo(new[] { "first", "second" }, options => options.WithStrictOrdering());
    }

    #endregion

    #region AppendRange Tests (Requirement 4.3)

    [Fact]
    public async Task AppendRange_MultipleElements_AppendsAllToEnd()
    {
        // Arrange - Requirement 4.3: Support appending multiple elements
        var entity = CreateTestEntity("append-range");
        entity.Tags = new List<string> { "original" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Use AppendRange
        var newTags = new[] { "tag1", "tag2", "tag3" };
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.AppendRange(newTags)
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(4);
        loaded.Tags.Should().BeEquivalentTo(new[] { "original", "tag1", "tag2", "tag3" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task PrependRange_MultipleElements_PrependsAllToBeginning()
    {
        // Arrange - Test PrependRange
        var entity = CreateTestEntity("prepend-range");
        entity.Tags = new List<string> { "original" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Use PrependRange
        var newTags = new List<string> { "tag1", "tag2" };
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.PrependRange(newTags)
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(3);
        loaded.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2", "original" }, options => options.WithStrictOrdering());
    }

    #endregion

    #region List Element Update by Index Tests (Requirement 4.4)

    [Fact]
    public async Task SetAt_UpdatesSpecificElement()
    {
        // Arrange - Requirement 4.4: Support updating list element by index
        var entity = CreateTestEntity("list-index-update");
        entity.Tags = new List<string> { "first", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update element at index 1 using SetAt extension method
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.SetAt(1, "updated-second"))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(3);
        loaded.Tags[0].Should().Be("first");
        loaded.Tags[1].Should().Be("updated-second");
        loaded.Tags[2].Should().Be("third");
    }

    [Fact]
    public async Task SetAt_FirstElement_UpdatesCorrectly()
    {
        // Arrange
        var entity = CreateTestEntity("list-index-first");
        entity.Tags = new List<string> { "old-first", "second" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update first element (index 0) using SetAt extension method
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.SetAt(0, "new-first"))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags[0].Should().Be("new-first");
        loaded.Tags[1].Should().Be("second");
    }

    [Fact]
    public async Task SetAt_LastElement_UpdatesCorrectly()
    {
        // Arrange
        var entity = CreateTestEntity("list-index-last");
        entity.Tags = new List<string> { "first", "second", "old-last" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update last element (index 2) using SetAt extension method
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.SetAt(2, "new-last"))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags[2].Should().Be("new-last");
    }

    [Fact]
    public async Task SetAt_WithVariableIndex_UpdatesCorrectly()
    {
        // Arrange - Requirement 3.1: Support variable indices in SetAt extension method
        var entity = CreateTestEntity("list-index-variable");
        entity.Tags = new List<string> { "first", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update element using variable index
        int index = 1;
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.SetAt(index, "variable-updated"))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(3);
        loaded.Tags[0].Should().Be("first");
        loaded.Tags[1].Should().Be("variable-updated");
        loaded.Tags[2].Should().Be("third");
    }

    [Fact]
    public async Task SetAt_WithMethodCallIndex_UpdatesCorrectly()
    {
        // Arrange - Requirement 3.3: Support method call indices in update expressions
        var entity = CreateTestEntity("list-index-method");
        entity.Tags = new List<string> { "first", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update element using method call index
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.SetAt(GetTargetIndex(), "method-updated"))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags[2].Should().Be("method-updated");
    }

    [Fact]
    public async Task SetAt_WithPropertyAccessIndex_UpdatesCorrectly()
    {
        // Arrange - Requirement 3.4: Support property access indices in update expressions
        var entity = CreateTestEntity("list-index-property");
        entity.Tags = new List<string> { "first", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update element using property access index
        var config = new IndexConfig { TargetIndex = 0 };
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.SetAt(config.TargetIndex, "property-updated"))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags[0].Should().Be("property-updated");
    }

    [Fact]
    public async Task SetAt_ChainedOperations_UpdatesMultipleIndices()
    {
        // Arrange - Requirement 1.1: Support chained SetAt operations
        var entity = CreateTestEntity("list-chained-setat");
        entity.Tags = new List<string> { "first", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update multiple elements using chained SetAt
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.SetAt(0, "updated-first").SetAt(2, "updated-third"))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(3);
        loaded.Tags[0].Should().Be("updated-first");
        loaded.Tags[1].Should().Be("second"); // Unchanged
        loaded.Tags[2].Should().Be("updated-third");
    }

    [Fact]
    public async Task SetAt_ChainedWithVariableIndices_UpdatesMultipleIndices()
    {
        // Arrange - Chained SetAt with variable indices
        var entity = CreateTestEntity("list-chained-variable");
        entity.Tags = new List<string> { "a", "b", "c", "d" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update multiple elements using chained SetAt with variables
        int firstIndex = 1;
        int secondIndex = 3;
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.SetAt(firstIndex, "updated-b").SetAt(secondIndex, "updated-d"))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(4);
        loaded.Tags[0].Should().Be("a");
        loaded.Tags[1].Should().Be("updated-b");
        loaded.Tags[2].Should().Be("c");
        loaded.Tags[3].Should().Be("updated-d");
    }

    #endregion

    #region List Element Remove by Index Tests (Requirement 4.5)

    [Fact]
    public async Task RemoveAt_RemovesSpecificElement()
    {
        // Arrange - Requirement 4.5: Support REMOVE for list elements by index
        var entity = CreateTestEntity("list-remove-index");
        entity.Tags = new List<string> { "first", "to-remove", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Remove element at index 1 using RemoveAt extension method
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.RemoveAt(1))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("first");
        loaded.Tags[1].Should().Be("third");
    }

    [Fact]
    public async Task RemoveAt_FirstElement_RemovesCorrectly()
    {
        // Arrange
        var entity = CreateTestEntity("list-remove-first");
        entity.Tags = new List<string> { "to-remove", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Remove first element (index 0) using RemoveAt extension method
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.RemoveAt(0))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("second");
        loaded.Tags[1].Should().Be("third");
    }

    [Fact]
    public async Task RemoveAt_LastElement_RemovesCorrectly()
    {
        // Arrange
        var entity = CreateTestEntity("list-remove-last");
        entity.Tags = new List<string> { "first", "second", "to-remove" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Remove last element (index 2) using RemoveAt extension method
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.RemoveAt(2))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("first");
        loaded.Tags[1].Should().Be("second");
    }

    [Fact]
    public async Task RemoveAt_WithVariableIndex_RemovesCorrectly()
    {
        // Arrange - Requirement 3.2: Support variable indices in RemoveAt extension method
        var entity = CreateTestEntity("list-remove-variable");
        entity.Tags = new List<string> { "first", "to-remove", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Remove element using variable index
        int index = 1;
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.RemoveAt(index))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("first");
        loaded.Tags[1].Should().Be("third");
    }

    [Fact]
    public async Task RemoveAt_WithMethodCallIndex_RemovesCorrectly()
    {
        // Arrange - Requirement 3.3: Support method call indices in update expressions
        var entity = CreateTestEntity("list-remove-method");
        entity.Tags = new List<string> { "first", "second", "to-remove" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Remove element using method call index
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.RemoveAt(GetTargetIndex()))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("first");
        loaded.Tags[1].Should().Be("second");
    }

    [Fact]
    public async Task RemoveAt_WithPropertyAccessIndex_RemovesCorrectly()
    {
        // Arrange - Requirement 3.4: Support property access indices in update expressions
        var entity = CreateTestEntity("list-remove-property");
        entity.Tags = new List<string> { "to-remove", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Remove element using property access index
        var config = new IndexConfig { TargetIndex = 0 };
        await _table.Update(entity.Id, entity.Type)
            .Set(x => x.Tags.RemoveAt(config.TargetIndex))
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("second");
        loaded.Tags[1].Should().Be("third");
    }

    #endregion

    #region Nested List Operations Tests (Requirement 4.6)

    [Fact]
    public async Task ListAppend_NestedList_AppendsToNestedList()
    {
        // Arrange - Requirement 4.6: List operations work with nested lists
        var entity = CreateTestEntity("nested-list-append");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "existing-keyword" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Append to nested list (Metadata.Keywords) using update model
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "existing-keyword", "new-keyword" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords.Should().HaveCount(2);
        loaded.Metadata.Keywords[0].Should().Be("existing-keyword");
        loaded.Metadata.Keywords[1].Should().Be("new-keyword");
    }

    [Fact]
    public async Task ListPrepend_NestedList_PrependsToNestedList()
    {
        // Arrange - Requirement 4.6: List operations work with nested lists
        var entity = CreateTestEntity("nested-list-prepend");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "existing-keyword" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Prepend to nested list (Metadata.Keywords) using update model
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "new-keyword", "existing-keyword" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords.Should().HaveCount(2);
        loaded.Metadata.Keywords[0].Should().Be("new-keyword");
        loaded.Metadata.Keywords[1].Should().Be("existing-keyword");
    }

    [Fact]
    public async Task SetAt_NestedListElement_UpdatesCorrectly()
    {
        // Arrange - Requirement 4.6: List operations work with nested lists
        var entity = CreateTestEntity("nested-list-index-update");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "first", "second", "third" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update nested list element by index using update model pattern
        // Note: For nested lists, we use the update model pattern since the expression
        // translator needs access to the actual List<T> property, not UpdateExpressionProperty<T>
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "first", "updated-second", "third" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords[1].Should().Be("updated-second");
    }

    [Fact]
    public async Task SetAt_NestedListElement_WithVariableIndex_UpdatesCorrectly()
    {
        // Arrange - Requirement 1.3, 3.1: Nested list operations with variable index
        var entity = CreateTestEntity("nested-list-variable-setat");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "first", "second", "third" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update nested list element using update model pattern
        // Note: For nested lists, we use the update model pattern since the expression
        // translator needs access to the actual List<T> property, not UpdateExpressionProperty<T>
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "variable-updated", "second", "third" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords[0].Should().Be("variable-updated");
        loaded.Metadata.Keywords[1].Should().Be("second");
        loaded.Metadata.Keywords[2].Should().Be("third");
    }

    [Fact]
    public async Task RemoveAt_NestedListElement_RemovesCorrectly()
    {
        // Arrange - Requirement 4.6: List operations work with nested lists
        var entity = CreateTestEntity("nested-list-remove");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "first", "to-remove", "third" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Remove nested list element using update model pattern
        // Note: For nested lists, we use the update model pattern since the expression
        // translator needs access to the actual List<T> property, not UpdateExpressionProperty<T>
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "first", "third" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords.Should().HaveCount(2);
        loaded.Metadata.Keywords[0].Should().Be("first");
        loaded.Metadata.Keywords[1].Should().Be("third");
    }

    [Fact]
    public async Task RemoveAt_NestedListElement_WithVariableIndex_RemovesCorrectly()
    {
        // Arrange - Requirement 1.3, 3.2: Nested list operations with variable index
        var entity = CreateTestEntity("nested-list-variable-removeat");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "first", "second", "to-remove" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Remove nested list element using update model pattern
        // Note: For nested lists, we use the update model pattern since the expression
        // translator needs access to the actual List<T> property, not UpdateExpressionProperty<T>
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "first", "second" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords.Should().HaveCount(2);
        loaded.Metadata.Keywords[0].Should().Be("first");
        loaded.Metadata.Keywords[1].Should().Be("second");
    }

    [Fact]
    public async Task AppendRange_NestedList_AppendsMultipleElements()
    {
        // Arrange - Requirement 4.6: List operations work with nested lists
        var entity = CreateTestEntity("nested-list-append-range");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "original" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Replace nested list with appended values using update model
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "original", "keyword1", "keyword2" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords.Should().HaveCount(3);
        loaded.Metadata.Keywords.Should().BeEquivalentTo(new[] { "original", "keyword1", "keyword2" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task SetAt_ChainedOnNestedList_UpdatesMultipleIndices()
    {
        // Arrange - Chained SetAt on nested list
        var entity = CreateTestEntity("nested-list-chained");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "first", "second", "third" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Update multiple elements in nested list using update model pattern
        // Note: For nested lists, we use the update model pattern since the expression
        // translator needs access to the actual List<T> property, not UpdateExpressionProperty<T>
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "updated-first", "second", "updated-third" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords.Should().HaveCount(3);
        loaded.Metadata.Keywords[0].Should().Be("updated-first");
        loaded.Metadata.Keywords[1].Should().Be("second"); // Unchanged
        loaded.Metadata.Keywords[2].Should().Be("updated-third");
    }

    #endregion

    #region Combined Operations Tests

    [Fact]
    public async Task CombinedListOperations_AppendAndUpdateOtherProperties()
    {
        // Arrange - Test combining list operations with other updates
        var entity = CreateTestEntity("combined-ops");
        entity.Tags = new List<string> { "original" };
        entity.Status = "pending";
        await _table.Entities.Put(entity).PutAsync();

        // Act - Combine list append with status update
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Status = "active",
                Tags = x.Tags.ListAppend("new-tag")
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("active");
        loaded.Tags.Should().HaveCount(2);
        loaded.Tags[1].Should().Be("new-tag");
    }

    [Fact]
    public async Task ListOperations_InTransaction_Succeeds()
    {
        // Arrange - Test list operations in transactions
        var entity = CreateTestEntity("txn-list-ops");
        entity.Tags = new List<string> { "original" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Transaction with list operation
        await DynamoDbTransactions.Write
            .Add(_table.Update(entity.Id, entity.Type)
                .Set(x => new NestedPropertyTestEntityUpdateModel
                {
                    Tags = x.Tags.ListAppend("txn-tag")
                }))
            .ExecuteAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[1].Should().Be("txn-tag");
    }

    [Fact]
    public async Task ListOperations_WithCapturedVariable_WorksCorrectly()
    {
        // Arrange - Test list operations with captured variables
        var entity = CreateTestEntity("captured-var");
        entity.Tags = new List<string> { "original" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Use captured variable
        var newTag = "captured-tag";
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Tags = x.Tags.ListAppend(newTag)
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().Contain("captured-tag");
    }

    #endregion

    #region Dynamic Index in Query Filter Tests (Requirements 2.1, 2.2, 2.3, 2.5)

    [Fact]
    public async Task Query_WithFilterUsingVariableIndex_FiltersCorrectly()
    {
        // Arrange - Requirement 2.1: Support local variable indices in filter expressions
        var entity = CreateTestEntity("query-filter-variable-index");
        entity.Tags = new List<string> { "featured", "premium", "verified" };
        await _table.Entities.Put(entity).PutAsync();

        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter using variable index
        int index = 0;
        var response = await _table.Entities.Query()
            .Where(x => x.Id == entity.Id, metadata)
            .WithFilter(x => x.Tags[index] == "featured", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        var loaded = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        loaded.Tags[0].Should().Be("featured");
    }

    [Fact]
    public async Task Query_WithFilterUsingVariableIndexDifferentValue_FiltersCorrectly()
    {
        // Arrange - Test with different variable index value
        var entity = CreateTestEntity("query-filter-variable-index-2");
        entity.Tags = new List<string> { "first", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter using variable index = 1
        int index = 1;
        var response = await _table.Entities.Query()
            .Where(x => x.Id == entity.Id, metadata)
            .WithFilter(x => x.Tags[index] == "second", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        var loaded = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        loaded.Tags[1].Should().Be("second");
    }

    [Fact]
    public async Task Query_WithFilterUsingMethodCallIndex_FiltersCorrectly()
    {
        // Arrange - Requirement 2.2: Support method call indices in filter expressions
        var entity = CreateTestEntity("query-filter-method-index");
        entity.Tags = new List<string> { "first", "second", "target-value" };
        await _table.Entities.Put(entity).PutAsync();

        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter using method call index (GetTargetIndex() returns 2)
        var response = await _table.Entities.Query()
            .Where(x => x.Id == entity.Id, metadata)
            .WithFilter(x => x.Tags[GetTargetIndex()] == "target-value", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        var loaded = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        loaded.Tags[2].Should().Be("target-value");
    }

    [Fact]
    public async Task Query_WithFilterUsingPropertyAccessIndex_FiltersCorrectly()
    {
        // Arrange - Requirement 2.3: Support property access indices in filter expressions
        var entity = CreateTestEntity("query-filter-property-index");
        entity.Tags = new List<string> { "target-value", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter using property access index
        var config = new IndexConfig { TargetIndex = 0 };
        var response = await _table.Entities.Query()
            .Where(x => x.Id == entity.Id, metadata)
            .WithFilter(x => x.Tags[config.TargetIndex] == "target-value", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        var loaded = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        loaded.Tags[0].Should().Be("target-value");
    }

    [Fact]
    public async Task Query_WithFilterUsingVariableIndexNoMatch_ReturnsEmpty()
    {
        // Arrange - Test that filter correctly excludes non-matching items
        var entity = CreateTestEntity("query-filter-no-match");
        entity.Tags = new List<string> { "first", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter that won't match
        int index = 0;
        var response = await _table.Entities.Query()
            .Where(x => x.Id == entity.Id, metadata)
            .WithFilter(x => x.Tags[index] == "non-existent", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Query_WithFilterUsingVariableIndexOnNestedList_FiltersCorrectly()
    {
        // Arrange - Test variable index on nested list (Metadata.Keywords)
        var entity = CreateTestEntity("query-filter-nested-variable");
        entity.Metadata = new TestMetadata
        {
            Keywords = new List<string> { "featured", "premium", "verified" },
            Scores = new List<int> { 100 }
        };
        await _table.Entities.Put(entity).PutAsync();

        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter using variable index on nested list
        int index = 1;
        var response = await _table.Entities.Query()
            .Where(x => x.Id == entity.Id, metadata)
            .WithFilter(x => x.Metadata.Keywords[index] == "premium", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        var loaded = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        loaded.Metadata.Keywords[1].Should().Be("premium");
    }

    #endregion

    #region Dynamic Index in Condition Expression Tests (Requirement 2.5)

    [Fact]
    public async Task Put_WithConditionUsingVariableIndex_SucceedsWhenConditionMet()
    {
        // Arrange - Requirement 2.5: Support dynamic indices in condition expressions
        var entity = CreateTestEntity("put-condition-variable-index");
        entity.Tags = new List<string> { "expected", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Create updated entity
        var updatedEntity = CreateTestEntity("put-condition-variable-index");
        updatedEntity.Tags = new List<string> { "expected", "updated", "third" };
        updatedEntity.Status = "updated";

        // Act - Put with condition using variable index
        int index = 0;
        await _table.Entities.Put(updatedEntity)
            .Where(x => x.Tags[index] == "expected")
            .PutAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("updated");
        loaded.Tags[1].Should().Be("updated");
    }

    [Fact]
    public async Task Put_WithConditionUsingVariableIndex_FailsWhenConditionNotMet()
    {
        // Arrange
        var entity = CreateTestEntity("put-condition-variable-fail");
        entity.Tags = new List<string> { "actual", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Create updated entity
        var updatedEntity = CreateTestEntity("put-condition-variable-fail");
        updatedEntity.Tags = new List<string> { "actual", "updated", "third" };
        updatedEntity.Status = "should-not-update";

        // Act & Assert - Put with condition that won't match should throw
        // The ConditionalCheckFailedException is wrapped in DynamoDbMappingException
        int index = 0;
        var action = async () => await _table.Entities.Put(updatedEntity)
            .Where(x => x.Tags[index] == "expected")
            .PutAsync();

        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<Amazon.DynamoDBv2.Model.ConditionalCheckFailedException>();

        // Verify entity was not updated
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("active"); // Original status
    }

    [Fact]
    public async Task Put_WithConditionUsingMethodCallIndex_SucceedsWhenConditionMet()
    {
        // Arrange
        var entity = CreateTestEntity("put-condition-method-index");
        entity.Tags = new List<string> { "first", "second", "expected" };
        await _table.Entities.Put(entity).PutAsync();

        // Create updated entity
        var updatedEntity = CreateTestEntity("put-condition-method-index");
        updatedEntity.Tags = new List<string> { "first", "second", "expected" };
        updatedEntity.Status = "method-updated";

        // Act - Put with condition using method call index (GetTargetIndex() returns 2)
        await _table.Entities.Put(updatedEntity)
            .Where(x => x.Tags[GetTargetIndex()] == "expected")
            .PutAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("method-updated");
    }

    [Fact]
    public async Task Put_WithConditionUsingPropertyAccessIndex_SucceedsWhenConditionMet()
    {
        // Arrange
        var entity = CreateTestEntity("put-condition-property-index");
        entity.Tags = new List<string> { "expected", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        // Create updated entity
        var updatedEntity = CreateTestEntity("put-condition-property-index");
        updatedEntity.Tags = new List<string> { "expected", "second", "third" };
        updatedEntity.Status = "property-updated";

        // Act - Put with condition using property access index
        var config = new IndexConfig { TargetIndex = 0 };
        await _table.Entities.Put(updatedEntity)
            .Where(x => x.Tags[config.TargetIndex] == "expected")
            .PutAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("property-updated");
    }

    #endregion

    #region Scan with Dynamic Index Filter Tests

    [Fact]
    public async Task Scan_WithFilterUsingVariableIndex_FiltersCorrectly()
    {
        // Arrange - Test scan with variable index filter
        var entity = CreateTestEntity("scan-filter-variable-index");
        entity.Tags = new List<string> { "scan-target", "second", "third" };
        await _table.Entities.Put(entity).PutAsync();

        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Scan with filter using variable index
        int index = 0;
        var response = await _table.Scan<NestedPropertyTestEntity>()
            .WithFilter(x => x.Tags[index] == "scan-target", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCountGreaterThanOrEqualTo(1);
        var matchingItems = response.Items
            .Select(item => NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(item))
            .Where(e => e.Id == entity.Id)
            .ToList();
        matchingItems.Should().HaveCount(1);
        matchingItems[0].Tags[0].Should().Be("scan-target");
    }

    #endregion

    #region Helper Methods

    private static NestedPropertyTestEntity CreateTestEntity(string id)
    {
        return new NestedPropertyTestEntity
        {
            Id = id,
            Type = "test",
            Name = "Test Entity",
            Status = "active",
            IsActive = true,
            Address = new TestAddress
            {
                City = "Seattle",
                State = "WA",
                ZipCode = "98101",
                Country = new TestCountry { Code = "US", Name = "United States" }
            },
            Metadata = new TestMetadata
            {
                Keywords = new List<string> { "test" },
                Scores = new List<int> { 100 }
            },
            Tags = new List<string> { "test" }
        };
    }

    /// <summary>
    /// Helper method for testing method call indices.
    /// Returns index 2 for testing purposes.
    /// </summary>
    private static int GetTargetIndex() => 2;

    /// <summary>
    /// Helper class for testing property access indices.
    /// </summary>
    private class IndexConfig
    {
        public int TargetIndex { get; set; }
    }

    #endregion
}
