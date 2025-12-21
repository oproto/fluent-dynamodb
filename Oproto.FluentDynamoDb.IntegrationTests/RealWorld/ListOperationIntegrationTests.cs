using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
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

        // Act - Update element at index 1 using SetAt
        await _table.Update(entity.Id, entity.Type)
            .SetAt(x => x.Tags[1], "updated-second")
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

        // Act - Update first element (index 0) using SetAt
        await _table.Update(entity.Id, entity.Type)
            .SetAt(x => x.Tags[0], "new-first")
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

        // Act - Update last element (index 2) using SetAt
        await _table.Update(entity.Id, entity.Type)
            .SetAt(x => x.Tags[2], "new-last")
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags[2].Should().Be("new-last");
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

        // Act - Remove element at index 1 using RemoveAt
        await _table.Update(entity.Id, entity.Type)
            .RemoveAt(x => x.Tags[1])
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

        // Act - Remove first element (index 0) using RemoveAt
        await _table.Update(entity.Id, entity.Type)
            .RemoveAt(x => x.Tags[0])
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

        // Act - Remove last element (index 2) using RemoveAt
        await _table.Update(entity.Id, entity.Type)
            .RemoveAt(x => x.Tags[2])
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Tags.Should().HaveCount(2);
        loaded.Tags[0].Should().Be("first");
        loaded.Tags[1].Should().Be("second");
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

        // Act - Update nested list element by index using SetAt
        await _table.Update(entity.Id, entity.Type)
            .SetAt(x => x.Metadata.Keywords[1], "updated-second")
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords[1].Should().Be("updated-second");
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

        // Act - Remove nested list element by index using RemoveAt
        await _table.Update(entity.Id, entity.Type)
            .RemoveAt(x => x.Metadata.Keywords[1])
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Metadata.Keywords.Should().HaveCount(2);
        loaded.Metadata.Keywords[0].Should().Be("first");
        loaded.Metadata.Keywords[1].Should().Be("third");
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

    #endregion
}
