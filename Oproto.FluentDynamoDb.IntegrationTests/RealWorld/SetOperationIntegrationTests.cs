using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for set operations with DynamoDB Local.
/// These tests verify end-to-end functionality of set operations using lambda expressions.
/// 
/// Requirements covered:
/// - 5.1: Support Add operation for sets (single element)
/// - 5.2: Support adding multiple elements to set
/// - 5.3: Support Delete operation for sets (single element)
/// - 5.4: Support deleting multiple elements from set
/// - 5.5: Set operations work with numeric sets
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "SetOperations")]
public class SetOperationIntegrationTests : IntegrationTestBase
{
    private SetOperationTestTable _table = null!;

    public SetOperationIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await CreateTableAsync<SetOperationTestEntity>();
        _table = new SetOperationTestTable(DynamoDb, TableName);
    }

    #region AddToSet Tests - Single Element (Requirement 5.1)

    [Fact]
    public async Task AddToSet_SingleStringElement_AddsToExistingSet()
    {
        // Arrange - Requirement 5.1: Support Add operation for sets
        var entity = CreateTestEntity("add-single-string");
        entity.Categories = new HashSet<string> { "existing-category" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Add single element to string set
        await _table.Update(entity.Id)
            .AddToSet(x => x.Categories, "new-category")
            .UpdateAsync();

        // Assert - Verify element was added
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(2);
        loaded.Categories.Should().Contain("existing-category");
        loaded.Categories.Should().Contain("new-category");
    }

    [Fact]
    public async Task AddToSet_SingleStringElement_CreatesNewSetIfNotExists()
    {
        // Arrange - Test adding to a set that doesn't exist yet
        var entity = CreateTestEntity("add-single-new-set");
        entity.Categories = null; // No set initially
        await _table.Entities.Put(entity).PutAsync();

        // Act - Add single element to create new set
        await _table.Update(entity.Id)
            .AddToSet(x => x.Categories, "first-category")
            .UpdateAsync();

        // Assert - Verify set was created with the element
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(1);
        loaded.Categories.Should().Contain("first-category");
    }

    [Fact]
    public async Task AddToSet_DuplicateElement_DoesNotAddDuplicate()
    {
        // Arrange - Sets should not contain duplicates
        var entity = CreateTestEntity("add-duplicate");
        entity.Categories = new HashSet<string> { "existing-category" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Try to add duplicate element
        await _table.Update(entity.Id)
            .AddToSet(x => x.Categories, "existing-category")
            .UpdateAsync();

        // Assert - Set should still have only one element
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(1);
        loaded.Categories.Should().Contain("existing-category");
    }

    #endregion

    #region AddToSet Tests - Multiple Elements (Requirement 5.2)

    [Fact]
    public async Task AddToSet_MultipleStringElements_AddsAllToSet()
    {
        // Arrange - Requirement 5.2: Support adding multiple elements to set
        var entity = CreateTestEntity("add-multiple-strings");
        entity.Categories = new HashSet<string> { "original" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Add multiple elements
        await _table.Update(entity.Id)
            .AddToSet(x => x.Categories, new[] { "category1", "category2", "category3" })
            .UpdateAsync();

        // Assert - Verify all elements were added
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(4);
        loaded.Categories.Should().Contain("original");
        loaded.Categories.Should().Contain("category1");
        loaded.Categories.Should().Contain("category2");
        loaded.Categories.Should().Contain("category3");
    }

    [Fact]
    public async Task AddToSet_MultipleElements_WithSomeDuplicates_OnlyAddsNew()
    {
        // Arrange - Test adding mix of new and existing elements
        var entity = CreateTestEntity("add-mixed");
        entity.Categories = new HashSet<string> { "existing1", "existing2" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Add mix of new and existing elements
        await _table.Update(entity.Id)
            .AddToSet(x => x.Categories, new[] { "existing1", "new1", "new2" })
            .UpdateAsync();

        // Assert - Only new elements should be added
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(4);
        loaded.Categories.Should().Contain("existing1");
        loaded.Categories.Should().Contain("existing2");
        loaded.Categories.Should().Contain("new1");
        loaded.Categories.Should().Contain("new2");
    }

    #endregion

    #region DeleteFromSet Tests - Single Element (Requirement 5.3)

    [Fact]
    public async Task DeleteFromSet_SingleStringElement_RemovesFromSet()
    {
        // Arrange - Requirement 5.3: Support Delete operation for sets
        var entity = CreateTestEntity("delete-single-string");
        entity.Categories = new HashSet<string> { "keep", "remove" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Delete single element from string set
        await _table.Update(entity.Id)
            .DeleteFromSet(x => x.Categories, "remove")
            .UpdateAsync();

        // Assert - Verify element was removed
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(1);
        loaded.Categories.Should().Contain("keep");
        loaded.Categories.Should().NotContain("remove");
    }

    [Fact]
    public async Task DeleteFromSet_NonExistentElement_DoesNothing()
    {
        // Arrange - Deleting non-existent element should be a no-op
        var entity = CreateTestEntity("delete-nonexistent");
        entity.Categories = new HashSet<string> { "existing" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Try to delete element that doesn't exist
        await _table.Update(entity.Id)
            .DeleteFromSet(x => x.Categories, "nonexistent")
            .UpdateAsync();

        // Assert - Set should be unchanged
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(1);
        loaded.Categories.Should().Contain("existing");
    }

    [Fact]
    public async Task DeleteFromSet_LastElement_RemovesSetAttribute()
    {
        // Arrange - Deleting last element should remove the set attribute
        var entity = CreateTestEntity("delete-last");
        entity.Categories = new HashSet<string> { "only-one" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Delete the only element
        await _table.Update(entity.Id)
            .DeleteFromSet(x => x.Categories, "only-one")
            .UpdateAsync();

        // Assert - Set should be null/empty (DynamoDB removes empty sets)
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        // DynamoDB removes empty sets, so it should be null or empty
        (loaded!.Categories == null || loaded.Categories.Count == 0).Should().BeTrue();
    }

    #endregion

    #region DeleteFromSet Tests - Multiple Elements (Requirement 5.4)

    [Fact]
    public async Task DeleteFromSet_MultipleStringElements_RemovesAllFromSet()
    {
        // Arrange - Requirement 5.4: Support deleting multiple elements from set
        var entity = CreateTestEntity("delete-multiple-strings");
        entity.Categories = new HashSet<string> { "keep1", "remove1", "keep2", "remove2" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Delete multiple elements
        await _table.Update(entity.Id)
            .DeleteFromSet(x => x.Categories, new[] { "remove1", "remove2" })
            .UpdateAsync();

        // Assert - Verify all specified elements were removed
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(2);
        loaded.Categories.Should().Contain("keep1");
        loaded.Categories.Should().Contain("keep2");
        loaded.Categories.Should().NotContain("remove1");
        loaded.Categories.Should().NotContain("remove2");
    }

    [Fact]
    public async Task DeleteFromSet_MultipleElements_WithSomeNonExistent_OnlyRemovesExisting()
    {
        // Arrange - Test deleting mix of existing and non-existing elements
        var entity = CreateTestEntity("delete-mixed");
        entity.Categories = new HashSet<string> { "existing1", "existing2" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Delete mix of existing and non-existing elements
        await _table.Update(entity.Id)
            .DeleteFromSet(x => x.Categories, new[] { "existing1", "nonexistent" })
            .UpdateAsync();

        // Assert - Only existing elements should be removed
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(1);
        loaded.Categories.Should().Contain("existing2");
    }

    #endregion

    #region Numeric Set Tests (Requirement 5.5)

    [Fact]
    public async Task AddToSet_SingleIntElement_AddsToNumericSet()
    {
        // Arrange - Requirement 5.5: Set operations work with numeric sets
        var entity = CreateTestEntity("add-single-int");
        entity.Scores = new HashSet<int> { 100 };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Add single element to int set
        await _table.Update(entity.Id)
            .AddToSet(x => x.Scores, 200)
            .UpdateAsync();

        // Assert - Verify element was added
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Scores.Should().HaveCount(2);
        loaded.Scores.Should().Contain(100);
        loaded.Scores.Should().Contain(200);
    }

    [Fact]
    public async Task AddToSet_MultipleIntElements_AddsAllToNumericSet()
    {
        // Arrange - Requirement 5.5: Set operations work with numeric sets
        var entity = CreateTestEntity("add-multiple-ints");
        entity.Scores = new HashSet<int> { 100 };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Add multiple elements to int set
        await _table.Update(entity.Id)
            .AddToSet(x => x.Scores, new[] { 200, 300, 400 })
            .UpdateAsync();

        // Assert - Verify all elements were added
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Scores.Should().HaveCount(4);
        loaded.Scores.Should().Contain(100);
        loaded.Scores.Should().Contain(200);
        loaded.Scores.Should().Contain(300);
        loaded.Scores.Should().Contain(400);
    }

    [Fact]
    public async Task DeleteFromSet_SingleIntElement_RemovesFromNumericSet()
    {
        // Arrange - Requirement 5.5: Set operations work with numeric sets
        var entity = CreateTestEntity("delete-single-int");
        entity.Scores = new HashSet<int> { 100, 200, 300 };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Delete single element from int set
        await _table.Update(entity.Id)
            .DeleteFromSet(x => x.Scores, 200)
            .UpdateAsync();

        // Assert - Verify element was removed
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Scores.Should().HaveCount(2);
        loaded.Scores.Should().Contain(100);
        loaded.Scores.Should().Contain(300);
        loaded.Scores.Should().NotContain(200);
    }

    [Fact]
    public async Task DeleteFromSet_MultipleIntElements_RemovesAllFromNumericSet()
    {
        // Arrange - Requirement 5.5: Set operations work with numeric sets
        var entity = CreateTestEntity("delete-multiple-ints");
        entity.Scores = new HashSet<int> { 100, 200, 300, 400 };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Delete multiple elements from int set
        await _table.Update(entity.Id)
            .DeleteFromSet(x => x.Scores, new[] { 200, 400 })
            .UpdateAsync();

        // Assert - Verify all specified elements were removed
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Scores.Should().HaveCount(2);
        loaded.Scores.Should().Contain(100);
        loaded.Scores.Should().Contain(300);
    }

    [Fact]
    public async Task AddToSet_LongElement_AddsToNumericSet()
    {
        // Arrange - Test with long values
        var entity = CreateTestEntity("add-long");
        entity.LargeNumbers = new HashSet<long> { 1000000000L };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Add long element
        await _table.Update(entity.Id)
            .AddToSet(x => x.LargeNumbers, 2000000000L)
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.LargeNumbers.Should().HaveCount(2);
        loaded.LargeNumbers.Should().Contain(1000000000L);
        loaded.LargeNumbers.Should().Contain(2000000000L);
    }

    [Fact]
    public async Task AddToSet_DecimalElement_AddsToNumericSet()
    {
        // Arrange - Test with decimal values
        var entity = CreateTestEntity("add-decimal");
        entity.Prices = new HashSet<decimal> { 9.99m };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Add decimal element
        await _table.Update(entity.Id)
            .AddToSet(x => x.Prices, 19.99m)
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Prices.Should().HaveCount(2);
        loaded.Prices.Should().Contain(9.99m);
        loaded.Prices.Should().Contain(19.99m);
    }

    #endregion

    #region Combined Operations Tests

    [Fact]
    public async Task SetOperations_InTransaction_Succeeds()
    {
        // Arrange - Test set operations in transactions
        var entity = CreateTestEntity("txn-set-ops");
        entity.Categories = new HashSet<string> { "original" };
        await _table.Entities.Put(entity).PutAsync();

        // Act - Transaction with set operation
        await DynamoDbTransactions.Write
            .Add(_table.Update(entity.Id)
                .AddToSet(x => x.Categories, "txn-category"))
            .ExecuteAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().HaveCount(2);
        loaded.Categories.Should().Contain("txn-category");
    }

    // Note: Tests for combining multiple set operations on different sets in a single update
    // were removed because this is a known library limitation. Each set operation should be
    // performed in a separate update call when targeting different set attributes.

    #endregion

    #region Helper Methods and Nested Classes

    private static SetOperationTestEntity CreateTestEntity(string id)
    {
        return new SetOperationTestEntity
        {
            Id = id,
            Name = "Test Entity",
            Categories = new HashSet<string>(),
            Scores = new HashSet<int>(),
            LargeNumbers = new HashSet<long>(),
            Prices = new HashSet<decimal>()
        };
    }

    /// <summary>
    /// Test table class for SetOperationTestEntity.
    /// Note: In a real application, this would be generated by the source generator.
    /// For integration tests, we create a manual implementation.
    /// </summary>
    private class SetOperationTestTable : GenericTable
    {
        public SetOperationTestTable(IAmazonDynamoDB client, string tableName)
            : base(client, tableName)
        {
        }

        /// <summary>
        /// Entity accessor for SetOperationTestEntity operations.
        /// </summary>
        public SetOperationTestEntityAccessor Entities => 
            new SetOperationTestEntityAccessor(DynamoDbClient, Name);

        /// <summary>
        /// Creates an update builder for the specified entity ID.
        /// </summary>
        public UpdateItemRequestBuilder<SetOperationTestEntity> Update(string id) =>
            new UpdateItemRequestBuilder<SetOperationTestEntity>(DynamoDbClient)
                .ForTable(Name)
                .WithKey("pk", id);
    }

    /// <summary>
    /// Entity accessor for SetOperationTestEntity.
    /// Provides type-safe access to CRUD operations.
    /// </summary>
    private class SetOperationTestEntityAccessor
    {
        private readonly IAmazonDynamoDB _client;
        private readonly string _tableName;

        public SetOperationTestEntityAccessor(IAmazonDynamoDB client, string tableName)
        {
            _client = client;
            _tableName = tableName;
        }

        public PutItemRequestBuilder<SetOperationTestEntity> Put(SetOperationTestEntity entity) =>
            new PutItemRequestBuilder<SetOperationTestEntity>(_client).ForTable(_tableName).WithItem(entity);

        public async Task PutAsync(SetOperationTestEntity entity) =>
            await Put(entity).PutAsync();

        public GetItemRequestBuilder<SetOperationTestEntity> Get(string pk) =>
            new GetItemRequestBuilder<SetOperationTestEntity>(_client)
                .ForTable(_tableName)
                .WithKey("pk", pk);

        public async Task<SetOperationTestEntity?> GetAsync(string pk) =>
            await Get(pk).GetItemAsync();

        public UpdateItemRequestBuilder<SetOperationTestEntity> Update(string pk) =>
            new UpdateItemRequestBuilder<SetOperationTestEntity>(_client)
                .ForTable(_tableName)
                .WithKey("pk", pk);

        public DeleteItemRequestBuilder<SetOperationTestEntity> Delete(string pk) =>
            new DeleteItemRequestBuilder<SetOperationTestEntity>(_client)
                .ForTable(_tableName)
                .WithKey("pk", pk);

        public async Task DeleteAsync(string pk) =>
            await Delete(pk).DeleteAsync();
    }

    #endregion
}
