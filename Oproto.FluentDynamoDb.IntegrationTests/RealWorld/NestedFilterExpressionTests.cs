using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for nested filter expressions with DynamoDB Local.
/// These tests verify end-to-end functionality of nested property access in filter and condition expressions.
/// 
/// Requirements covered:
/// - 1.1: Lambda expressions support chained property access in filter expressions
/// - 1.2: Lambda expressions support multi-level nesting in filter expressions
/// - 1.3: Nested property access generates correct DynamoDB document paths
/// - 1.4: Nested properties work with all comparison operators in filters
/// - 1.5: Nested properties work with logical operators in filters
/// - 1.6: Nested properties work in condition expressions for writes (including transactions)
/// - 2.1: Lambda expressions support list index access in filter expressions
/// - 2.2: List index access generates correct DynamoDB document paths
/// - 2.3: Nested list access within maps is supported in filters
/// - 2.4: List element access works with nested object properties in filters
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "NestedFilterExpressions")]
public class NestedFilterExpressionTests : IntegrationTestBase
{
    private NestedPropertyTestTable _table = null!;

    public NestedFilterExpressionTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await CreateTableAsync<NestedPropertyTestEntity>();
        _table = new NestedPropertyTestTable(DynamoDb, TableName);

        // Seed test data
        await SeedTestDataAsync();
    }

    private async Task SeedTestDataAsync()
    {
        var entities = new[]
        {
            new NestedPropertyTestEntity
            {
                Id = "customer-1",
                Type = "premium",
                Name = "Alice Smith",
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
                    Keywords = new List<string> { "featured", "premium", "verified" },
                    Scores = new List<int> { 95, 88, 92 }
                },
                Tags = new List<string> { "vip", "early-adopter", "beta-tester" }
            },
            new NestedPropertyTestEntity
            {
                Id = "customer-1",
                Type = "standard",
                Name = "Alice Smith",
                Status = "active",
                IsActive = true,
                Address = new TestAddress
                {
                    City = "Portland",
                    State = "OR",
                    ZipCode = "97201",
                    Country = new TestCountry { Code = "US", Name = "United States" }
                },
                Metadata = new TestMetadata
                {
                    Keywords = new List<string> { "budget", "new" },
                    Scores = new List<int> { 75, 80 }
                },
                Tags = new List<string> { "new-customer" }
            },
            new NestedPropertyTestEntity
            {
                Id = "customer-2",
                Type = "premium",
                Name = "Bob Johnson",
                Status = "inactive",
                IsActive = false,
                Address = new TestAddress
                {
                    City = "Seattle",
                    State = "WA",
                    ZipCode = "98102",
                    Country = new TestCountry { Code = "US", Name = "United States" }
                },
                Metadata = new TestMetadata
                {
                    Keywords = new List<string> { "archived", "legacy" },
                    Scores = new List<int> { 60, 55 }
                },
                Tags = new List<string> { "churned" }
            },
            new NestedPropertyTestEntity
            {
                Id = "customer-3",
                Type = "premium",
                Name = "Carol Williams",
                Status = "active",
                IsActive = true,
                Address = new TestAddress
                {
                    City = "Vancouver",
                    State = "BC",
                    ZipCode = "V6B 1A1",
                    Country = new TestCountry { Code = "CA", Name = "Canada" }
                },
                Metadata = new TestMetadata
                {
                    Keywords = new List<string> { "international", "premium" },
                    Scores = new List<int> { 90, 85, 88 }
                },
                Tags = new List<string> { "international", "premium" }
            }
        };

        foreach (var entity in entities)
        {
            var item = NestedPropertyTestEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
        }
    }

    #region Single-Level Nested Property Filter Tests (Requirement 1.1, 1.3)

    [Fact]
    public async Task Query_WithFilterOnSingleLevelNestedProperty_FiltersCorrectly()
    {
        // Arrange - Requirement 1.1: Lambda expressions support chained property access in filter expressions
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter on nested Address.City
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Address.City == "Seattle", metadata)
            .ToDynamoDbResponseAsync();

        // Assert - Requirement 1.3: Nested property access generates correct DynamoDB document paths
        response.Items.Should().HaveCount(1);

        var entity = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        entity.Address.City.Should().Be("Seattle");
        entity.Type.Should().Be("premium");
    }

    [Fact]
    public async Task Query_WithFilterOnNestedState_FiltersCorrectly()
    {
        // Arrange
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter on nested Address.State
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Address.State == "WA", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);

        var entity = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        entity.Address.State.Should().Be("WA");
    }

    #endregion

    #region Multi-Level Nested Property Filter Tests (Requirement 1.2)

    [Fact]
    public async Task Query_WithFilterOnMultiLevelNestedProperty_FiltersCorrectly()
    {
        // Arrange - Requirement 1.2: Lambda expressions support multi-level nesting in filter expressions
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter on multi-level nested Address.Country.Code
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Address.Country.Code == "US", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(2); // Both customer-1 records are in US

        var entities = response.Items.Select(item => 
            NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => e.Address.Country.Code.Should().Be("US"));
    }

    [Fact]
    public async Task Query_WithFilterOnMultiLevelNestedCountryName_FiltersCorrectly()
    {
        // Arrange
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Scan with filter on multi-level nested Address.Country.Name
        var response = await _table.Scan<NestedPropertyTestEntity>()
            .WithFilter<ScanRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Address.Country.Name == "Canada", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);

        var entity = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        entity.Address.Country.Name.Should().Be("Canada");
        entity.Name.Should().Be("Carol Williams");
    }

    #endregion

    #region Comparison Operators Tests (Requirement 1.4)

    [Fact]
    public async Task Query_WithFilterUsingStartsWithOnNestedProperty_FiltersCorrectly()
    {
        // Arrange - Requirement 1.4: Nested properties work with all comparison operators in filters
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter using StartsWith on nested property
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Address.ZipCode.StartsWith("98"), metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);

        var entity = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        entity.Address.ZipCode.Should().StartWith("98");
    }

    #endregion

    #region Logical Operators Tests (Requirement 1.5)

    [Fact]
    public async Task Query_WithFilterUsingAndOnNestedProperties_FiltersCorrectly()
    {
        // Arrange - Requirement 1.5: Nested properties work with logical operators in filters
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter using AND on nested properties
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Address.City == "Seattle" && x.Address.State == "WA", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);

        var entity = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        entity.Address.City.Should().Be("Seattle");
        entity.Address.State.Should().Be("WA");
    }

    [Fact]
    public async Task Query_WithFilterUsingOrOnNestedProperties_FiltersCorrectly()
    {
        // Arrange
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter using OR on nested properties
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Address.City == "Seattle" || x.Address.City == "Portland", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(2); // Both Seattle and Portland records

        var entities = response.Items.Select(item => 
            NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(item)).ToList();
        entities.Should().Contain(e => e.Address.City == "Seattle");
        entities.Should().Contain(e => e.Address.City == "Portland");
    }

    #endregion

    #region List Index Access Tests (Requirement 2.1, 2.2)

    [Fact]
    public async Task Query_WithFilterOnListIndex_FiltersCorrectly()
    {
        // Arrange - Requirement 2.1: Lambda expressions support list index access in filter expressions
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter on Tags[0]
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Tags[0] == "vip", metadata)
            .ToDynamoDbResponseAsync();

        // Assert - Requirement 2.2: List index access generates correct DynamoDB document paths
        response.Items.Should().HaveCount(1);

        var entity = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        entity.Tags[0].Should().Be("vip");
    }

    [Fact]
    public async Task Query_WithFilterOnDifferentListIndex_FiltersCorrectly()
    {
        // Arrange
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter on Tags[1]
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Tags[1] == "early-adopter", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);

        var entity = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        entity.Tags[1].Should().Be("early-adopter");
    }

    #endregion

    #region Nested List Access Tests (Requirement 2.3)

    [Fact]
    public async Task Query_WithFilterOnNestedListAccess_FiltersCorrectly()
    {
        // Arrange - Requirement 2.3: Nested list access within maps is supported in filters
        var customerId = "customer-1";
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Query with filter on Metadata.Keywords[0]
        var response = await _table.Query<NestedPropertyTestEntity>()
            .Where(x => x.Id == customerId, metadata)
            .WithFilter<QueryRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Metadata.Keywords[0] == "featured", metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);

        var entity = NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(response.Items[0]);
        entity.Metadata.Keywords[0].Should().Be("featured");
    }

    [Fact]
    public async Task Scan_WithFilterOnNestedListScores_FiltersCorrectly()
    {
        // Arrange
        var metadata = NestedPropertyTestEntity.GetEntityMetadata();

        // Act - Scan with filter on Metadata.Scores[0] > 90
        var response = await _table.Scan<NestedPropertyTestEntity>()
            .WithFilter<ScanRequestBuilder<NestedPropertyTestEntity>, NestedPropertyTestEntity>(
                x => x.Metadata.Scores[0] > 90, metadata)
            .ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCountGreaterThan(0);

        var entities = response.Items.Select(item => 
            NestedPropertyTestEntity.FromDynamoDb<NestedPropertyTestEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => e.Metadata.Scores[0].Should().BeGreaterThan(90));
    }

    #endregion

    #region Condition Expressions on Put/Update/Delete (Requirement 1.6)

    [Fact]
    public async Task Put_WithConditionOnNestedProperty_SucceedsWhenConditionMet()
    {
        // Arrange - Requirement 1.6: Nested properties work in condition expressions for writes
        var existingEntity = new NestedPropertyTestEntity
        {
            Id = "condition-test-1",
            Type = "test",
            Name = "Condition Test",
            Status = "pending",
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

        await _table.Entities.Put(existingEntity).PutAsync();

        // Verify the first put worked
        var verifyFirst = await _table.Entities.Get("condition-test-1", "test").GetItemAsync();
        verifyFirst.Should().NotBeNull("First put should have stored the entity");
        verifyFirst!.Address.City.Should().Be("Seattle", "First entity should have Seattle as city");

        var updatedEntity = new NestedPropertyTestEntity
        {
            Id = "condition-test-1",
            Type = "test",
            Name = "Updated Condition Test",
            Status = "active",
            IsActive = true,
            Address = new TestAddress
            {
                City = "Portland",
                State = "OR",
                ZipCode = "97201",
                Country = new TestCountry { Code = "US", Name = "United States" }
            },
            Metadata = new TestMetadata
            {
                Keywords = new List<string> { "updated" },
                Scores = new List<int> { 95 }
            },
            Tags = new List<string> { "updated" }
        };

        // Act - Put with condition on nested property
        await _table.Entities.Put(updatedEntity)
            .Where(x => x.Address.City == "Seattle")
            .PutAsync();

        // Assert
        var loaded = await _table.Entities.Get("condition-test-1", "test").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Updated Condition Test");
        loaded.Address.City.Should().Be("Portland");
    }

    [Fact]
    public async Task Put_WithConditionOnNestedProperty_FailsWhenConditionNotMet()
    {
        // Arrange
        var existingEntity = new NestedPropertyTestEntity
        {
            Id = "condition-test-2",
            Type = "test",
            Name = "Condition Test",
            Status = "pending",
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

        await _table.Entities.Put(existingEntity).PutAsync();

        var updatedEntity = new NestedPropertyTestEntity
        {
            Id = "condition-test-2",
            Type = "test",
            Name = "Should Not Update",
            Status = "active",
            IsActive = true,
            Address = new TestAddress
            {
                City = "Portland",
                State = "OR",
                ZipCode = "97201",
                Country = new TestCountry { Code = "US", Name = "United States" }
            },
            Metadata = new TestMetadata
            {
                Keywords = new List<string> { "updated" },
                Scores = new List<int> { 95 }
            },
            Tags = new List<string> { "updated" }
        };

        // Act & Assert - Put with condition that won't be met
        var act = async () => await _table.Entities.Put(updatedEntity)
            .Where(x => x.Address.City == "Portland") // Condition not met - city is Seattle
            .PutAsync();

        var exception = await act.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

        // Verify original entity unchanged
        var loaded = await _table.Entities.Get("condition-test-2", "test").GetItemAsync();
        loaded!.Name.Should().Be("Condition Test");
        loaded.Address.City.Should().Be("Seattle");
    }

    [Fact]
    public async Task Update_WithConditionOnNestedProperty_SucceedsWhenConditionMet()
    {
        // Arrange
        var existingEntity = new NestedPropertyTestEntity
        {
            Id = "condition-test-3",
            Type = "test",
            Name = "Update Condition Test",
            Status = "pending",
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

        await _table.Entities.Put(existingEntity).PutAsync();

        // Act - Update with condition on nested property
        await _table.Entities.Update("condition-test-3", "test")
            .Set("SET #status = :status")
            .WithAttribute("#status", "status")
            .WithValue(":status", "active")
            .Where(x => x.Address.State == "WA")
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get("condition-test-3", "test").GetItemAsync();
        loaded!.Status.Should().Be("active");
    }

    [Fact]
    public async Task Delete_WithConditionOnNestedProperty_SucceedsWhenConditionMet()
    {
        // Arrange
        var existingEntity = new NestedPropertyTestEntity
        {
            Id = "condition-test-4",
            Type = "test",
            Name = "Delete Condition Test",
            Status = "inactive",
            IsActive = false,
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

        await _table.Entities.Put(existingEntity).PutAsync();

        // Act - Delete with condition on nested property
        await _table.Entities.Delete("condition-test-4", "test")
            .Where(x => x.Address.Country.Code == "US")
            .DeleteAsync();

        // Assert
        var loaded = await _table.Entities.Get("condition-test-4", "test").GetItemAsync();
        loaded.Should().BeNull();
    }

    #endregion

    #region Condition Expressions in Transactions (Requirement 1.6)

    [Fact]
    public async Task TransactWrite_WithConditionOnNestedProperty_SucceedsWhenConditionMet()
    {
        // Arrange - Requirement 1.6: Nested properties work in condition expressions in transactions
        var existingEntity = new NestedPropertyTestEntity
        {
            Id = "txn-condition-test-1",
            Type = "test",
            Name = "Transaction Condition Test",
            Status = "pending",
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

        await _table.Entities.Put(existingEntity).PutAsync();

        var newEntity = new NestedPropertyTestEntity
        {
            Id = "txn-condition-test-2",
            Type = "test",
            Name = "New Transaction Entity",
            Status = "active",
            IsActive = true,
            Address = new TestAddress
            {
                City = "Portland",
                State = "OR",
                ZipCode = "97201",
                Country = new TestCountry { Code = "US", Name = "United States" }
            },
            Metadata = new TestMetadata
            {
                Keywords = new List<string> { "new" },
                Scores = new List<int> { 95 }
            },
            Tags = new List<string> { "new" }
        };

        // Act - Transaction with condition on nested property
        await DynamoDbTransactions.Write
            .Add(_table.Entities.Put(newEntity))
            .Add(_table.Entities.Update("txn-condition-test-1", "test")
                .Set("SET #status = :status")
                .WithAttribute("#status", "status")
                .WithValue(":status", "active")
                .Where(x => x.Address.City == "Seattle"))
            .ExecuteAsync();

        // Assert
        var loadedNew = await _table.Entities.Get("txn-condition-test-2", "test").GetItemAsync();
        loadedNew.Should().NotBeNull();
        loadedNew!.Name.Should().Be("New Transaction Entity");

        var loadedUpdated = await _table.Entities.Get("txn-condition-test-1", "test").GetItemAsync();
        loadedUpdated!.Status.Should().Be("active");
    }

    [Fact]
    public async Task TransactWrite_WithConditionOnMultiLevelNestedProperty_SucceedsWhenConditionMet()
    {
        // Arrange
        var existingEntity = new NestedPropertyTestEntity
        {
            Id = "txn-condition-test-3",
            Type = "test",
            Name = "Multi-Level Condition Test",
            Status = "pending",
            IsActive = true,
            Address = new TestAddress
            {
                City = "Vancouver",
                State = "BC",
                ZipCode = "V6B 1A1",
                Country = new TestCountry { Code = "CA", Name = "Canada" }
            },
            Metadata = new TestMetadata
            {
                Keywords = new List<string> { "test" },
                Scores = new List<int> { 100 }
            },
            Tags = new List<string> { "test" }
        };

        await _table.Entities.Put(existingEntity).PutAsync();

        // Act - Transaction with condition on multi-level nested property
        await DynamoDbTransactions.Write
            .Add(_table.Entities.Update("txn-condition-test-3", "test")
                .Set("SET #status = :status")
                .WithAttribute("#status", "status")
                .WithValue(":status", "active")
                .Where(x => x.Address.Country.Code == "CA"))
            .ExecuteAsync();

        // Assert
        var loaded = await _table.Entities.Get("txn-condition-test-3", "test").GetItemAsync();
        loaded!.Status.Should().Be("active");
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Test table class for NestedPropertyTestEntity.
    /// Note: In a real application, this would be generated by the source generator.
    /// For integration tests, we create a manual implementation.
    /// </summary>
    private class NestedPropertyTestTable : GenericTable
    {
        public NestedPropertyTestTable(IAmazonDynamoDB client, string tableName)
            : base(client, tableName)
        {
        }

        /// <summary>
        /// Entity accessor for NestedPropertyTestEntity operations.
        /// </summary>
        public NestedPropertyTestEntityAccessor Entities => 
            new NestedPropertyTestEntityAccessor(DynamoDbClient, Name);

        public new ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class, IReadOnlyEntity =>
            new ScanRequestBuilder<TEntity>(DynamoDbClient).ForTable(Name);
    }

    /// <summary>
    /// Entity accessor for NestedPropertyTestEntity.
    /// Provides type-safe access to CRUD operations.
    /// </summary>
    private class NestedPropertyTestEntityAccessor
    {
        private readonly IAmazonDynamoDB _client;
        private readonly string _tableName;

        public NestedPropertyTestEntityAccessor(IAmazonDynamoDB client, string tableName)
        {
            _client = client;
            _tableName = tableName;
        }

        public PutItemRequestBuilder<NestedPropertyTestEntity> Put(NestedPropertyTestEntity entity) =>
            new PutItemRequestBuilder<NestedPropertyTestEntity>(_client).ForTable(_tableName).WithItem(entity);

        public async Task PutAsync(NestedPropertyTestEntity entity) =>
            await Put(entity).PutAsync();

        public GetItemRequestBuilder<NestedPropertyTestEntity> Get(string pk, string sk) =>
            new GetItemRequestBuilder<NestedPropertyTestEntity>(_client)
                .ForTable(_tableName)
                .WithKey("pk", pk)
                .WithKey("sk", sk);

        public async Task<NestedPropertyTestEntity?> GetAsync(string pk, string sk) =>
            await Get(pk, sk).GetItemAsync();

        public UpdateItemRequestBuilder<NestedPropertyTestEntity> Update(string pk, string sk) =>
            new UpdateItemRequestBuilder<NestedPropertyTestEntity>(_client)
                .ForTable(_tableName)
                .WithKey("pk", pk)
                .WithKey("sk", sk);

        public DeleteItemRequestBuilder<NestedPropertyTestEntity> Delete(string pk, string sk) =>
            new DeleteItemRequestBuilder<NestedPropertyTestEntity>(_client)
                .ForTable(_tableName)
                .WithKey("pk", pk)
                .WithKey("sk", sk);

        public async Task DeleteAsync(string pk, string sk) =>
            await Delete(pk, sk).DeleteAsync();

        public QueryRequestBuilder<NestedPropertyTestEntity> Query() =>
            new QueryRequestBuilder<NestedPropertyTestEntity>(_client).ForTable(_tableName);

        public ConditionCheckBuilder<NestedPropertyTestEntity> ConditionCheck(string pk, string sk) =>
            new ConditionCheckBuilder<NestedPropertyTestEntity>(_client, _tableName)
                .WithKey("pk", pk)
                .WithKey("sk", sk);
    }

    #endregion
}
