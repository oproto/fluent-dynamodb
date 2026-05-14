using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for nested update expressions with DynamoDB Local.
/// These tests verify end-to-end functionality of nested property updates using lambda expressions.
/// 
/// Requirements covered:
/// - 3.1: Source generator creates nested update model types for entities with [DynamoDbMap] properties
/// - 3.2: Lambda update expressions support nested property assignment
/// - 3.3: Multiple nested properties can be updated in single expression
/// - 3.4: Nested updates can be combined with top-level updates
/// - 3.5: Multi-level nested updates are supported
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "NestedUpdateExpressions")]
public class NestedUpdateExpressionTests : IntegrationTestBase
{
    private NestedPropertyTestTable _table = null!;

    public NestedUpdateExpressionTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await CreateTableAsync<NestedPropertyTestEntity>();
        _table = new NestedPropertyTestTable(DynamoDb, TableName);
    }

    #region Single Nested Property Update Tests (Requirement 3.2)

    [Fact]
    public async Task Update_SingleNestedProperty_UpdatesSuccessfully()
    {
        // Arrange - Requirement 3.2: Lambda update expressions support nested property assignment
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-1",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Update single nested property using lambda expression
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Address = new TestAddressUpdateModel { City = "Portland" }
            })
            .UpdateAsync();

        // Assert - Verify only the nested property was updated
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Address.City.Should().Be("Portland");
        loaded.Address.State.Should().Be("WA"); // Unchanged
        loaded.Address.ZipCode.Should().Be("98101"); // Unchanged
        loaded.Address.Country.Code.Should().Be("US"); // Unchanged
        loaded.Name.Should().Be("Test Entity"); // Top-level unchanged
    }

    [Fact]
    public async Task Update_SingleNestedProperty_State_UpdatesSuccessfully()
    {
        // Arrange
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-2",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Update nested State property
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Address = new TestAddressUpdateModel { State = "OR" }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Address.State.Should().Be("OR");
        loaded.Address.City.Should().Be("Seattle"); // Unchanged
    }

    #endregion

    #region Multiple Nested Properties Update Tests (Requirement 3.3)

    [Fact]
    public async Task Update_MultipleNestedProperties_UpdatesAllSuccessfully()
    {
        // Arrange - Requirement 3.3: Multiple nested properties can be updated in single expression
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-3",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Update multiple nested properties in single expression
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Address = new TestAddressUpdateModel
                {
                    City = "Portland",
                    State = "OR",
                    ZipCode = "97201"
                }
            })
            .UpdateAsync();

        // Assert - Verify all nested properties were updated
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Address.City.Should().Be("Portland");
        loaded.Address.State.Should().Be("OR");
        loaded.Address.ZipCode.Should().Be("97201");
        loaded.Address.Country.Code.Should().Be("US"); // Unchanged
    }

    #endregion

    #region Multi-Level Nested Update Tests (Requirement 3.5)

    [Fact]
    public async Task Update_MultiLevelNestedProperty_UpdatesSuccessfully()
    {
        // Arrange - Requirement 3.5: Multi-level nested updates are supported
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-4",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Update multi-level nested property (Address.Country.Code)
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Address = new TestAddressUpdateModel
                {
                    Country = new TestCountryUpdateModel { Code = "CA" }
                }
            })
            .UpdateAsync();

        // Assert - Verify multi-level nested property was updated
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Address.Country.Code.Should().Be("CA");
        loaded.Address.Country.Name.Should().Be("United States"); // Unchanged
        loaded.Address.City.Should().Be("Seattle"); // Unchanged
    }

    [Fact]
    public async Task Update_MultiLevelNestedMultipleProperties_UpdatesAllSuccessfully()
    {
        // Arrange
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-5",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Update multiple multi-level nested properties
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Address = new TestAddressUpdateModel
                {
                    Country = new TestCountryUpdateModel
                    {
                        Code = "CA",
                        Name = "Canada"
                    }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Address.Country.Code.Should().Be("CA");
        loaded.Address.Country.Name.Should().Be("Canada");
    }

    #endregion

    #region Combined Top-Level and Nested Updates Tests (Requirement 3.4)

    [Fact]
    public async Task Update_CombinedTopLevelAndNestedProperties_UpdatesAllSuccessfully()
    {
        // Arrange - Requirement 3.4: Nested updates can be combined with top-level updates
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-6",
            Type = "test",
            Name = "Old Name",
            Status = "pending",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Update both top-level and nested properties
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Name = "New Name",
                Status = "active",
                IsActive = true,
                Address = new TestAddressUpdateModel { City = "Portland" }
            })
            .UpdateAsync();

        // Assert - Verify both top-level and nested properties were updated
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("New Name");
        loaded.Status.Should().Be("active");
        loaded.IsActive.Should().BeTrue();
        loaded.Address.City.Should().Be("Portland");
        loaded.Address.State.Should().Be("WA"); // Unchanged
    }

    [Fact]
    public async Task Update_CombinedTopLevelAndMultiLevelNested_UpdatesAllSuccessfully()
    {
        // Arrange
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-7",
            Type = "test",
            Name = "Old Name",
            Status = "pending",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Update top-level, single-level nested, and multi-level nested properties
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Name = "New Name",
                Address = new TestAddressUpdateModel
                {
                    City = "Vancouver",
                    State = "BC",
                    Country = new TestCountryUpdateModel { Code = "CA" }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("New Name");
        loaded.Address.City.Should().Be("Vancouver");
        loaded.Address.State.Should().Be("BC");
        loaded.Address.Country.Code.Should().Be("CA");
        loaded.Address.ZipCode.Should().Be("98101"); // Unchanged
    }

    #endregion

    #region Nested Updates in Transactions Tests (Requirement 3.5)

    [Fact]
    public async Task TransactWrite_WithNestedUpdate_Succeeds()
    {
        // Arrange - Test nested updates work in transactions
        var entity = new NestedPropertyTestEntity
        {
            Id = "txn-nested-update-1",
            Type = "test",
            Name = "Transaction Test",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Transaction with nested update
        await DynamoDbTransactions.Write
            .Add(_table.Update(entity.Id, entity.Type)
                .Set(x => new NestedPropertyTestEntityUpdateModel
                {
                    Status = "active",
                    Address = new TestAddressUpdateModel { City = "Portland" }
                }))
            .ExecuteAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("active");
        loaded.Address.City.Should().Be("Portland");
    }

    [Fact]
    public async Task TransactWrite_MultipleNestedUpdates_SucceedsForAllItems()
    {
        // Arrange - Test multiple nested updates in a single transaction
        var entity1 = new NestedPropertyTestEntity
        {
            Id = "txn-nested-update-2",
            Type = "test1",
            Name = "Entity 1",
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

        var entity2 = new NestedPropertyTestEntity
        {
            Id = "txn-nested-update-2",
            Type = "test2",
            Name = "Entity 2",
            Status = "pending",
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
                Keywords = new List<string> { "test" },
                Scores = new List<int> { 100 }
            },
            Tags = new List<string> { "test" }
        };

        await _table.Entities.Put(entity1).PutAsync();
        await _table.Entities.Put(entity2).PutAsync();

        // Act - Transaction with multiple nested updates
        await DynamoDbTransactions.Write
            .Add(_table.Update(entity1.Id, entity1.Type)
                .Set(x => new NestedPropertyTestEntityUpdateModel
                {
                    Address = new TestAddressUpdateModel { City = "San Francisco" }
                }))
            .Add(_table.Update(entity2.Id, entity2.Type)
                .Set(x => new NestedPropertyTestEntityUpdateModel
                {
                    Address = new TestAddressUpdateModel { City = "Los Angeles" }
                }))
            .ExecuteAsync();

        // Assert
        var loaded1 = await _table.Entities.Get(entity1.Id, entity1.Type).GetItemAsync();
        var loaded2 = await _table.Entities.Get(entity2.Id, entity2.Type).GetItemAsync();

        loaded1.Should().NotBeNull();
        loaded1!.Address.City.Should().Be("San Francisco");

        loaded2.Should().NotBeNull();
        loaded2!.Address.City.Should().Be("Los Angeles");
    }

    [Fact]
    public async Task TransactWrite_WithMultiLevelNestedUpdate_Succeeds()
    {
        // Arrange - Test multi-level nested updates in transactions
        var entity = new NestedPropertyTestEntity
        {
            Id = "txn-nested-update-3",
            Type = "test",
            Name = "Multi-Level Transaction Test",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Transaction with multi-level nested update
        await DynamoDbTransactions.Write
            .Add(_table.Update(entity.Id, entity.Type)
                .Set(x => new NestedPropertyTestEntityUpdateModel
                {
                    Address = new TestAddressUpdateModel
                    {
                        City = "Vancouver",
                        State = "BC",
                        Country = new TestCountryUpdateModel
                        {
                            Code = "CA",
                            Name = "Canada"
                        }
                    }
                }))
            .ExecuteAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Address.City.Should().Be("Vancouver");
        loaded.Address.State.Should().Be("BC");
        loaded.Address.Country.Code.Should().Be("CA");
        loaded.Address.Country.Name.Should().Be("Canada");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Update_NestedPropertyWithCapturedVariable_UpdatesSuccessfully()
    {
        // Arrange - Test that captured variables work with nested updates
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-captured",
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

        await _table.Entities.Put(entity).PutAsync();

        // Act - Use captured variables in nested update
        var newCity = "Portland";
        var newState = "OR";

        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Address = new TestAddressUpdateModel
                {
                    City = newCity,
                    State = newState
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Address.City.Should().Be("Portland");
        loaded.Address.State.Should().Be("OR");
    }

    [Fact]
    public async Task Update_MultipleNestedObjects_UpdatesAllSuccessfully()
    {
        // Arrange - Test updating multiple different nested objects
        var entity = new NestedPropertyTestEntity
        {
            Id = "nested-update-multiple-objects",
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
                Keywords = new List<string> { "old-keyword" },
                Scores = new List<int> { 50 }
            },
            Tags = new List<string> { "test" }
        };

        await _table.Entities.Put(entity).PutAsync();

        // Act - Update both Address and Metadata nested objects
        await _table.Update(entity.Id, entity.Type)
            .Set(x => new NestedPropertyTestEntityUpdateModel
            {
                Address = new TestAddressUpdateModel { City = "Portland" },
                Metadata = new TestMetadataUpdateModel
                {
                    Keywords = new List<string> { "new-keyword", "updated" },
                    Scores = new List<int> { 100, 95 }
                }
            })
            .UpdateAsync();

        // Assert
        var loaded = await _table.Entities.Get(entity.Id, entity.Type).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Address.City.Should().Be("Portland");
        loaded.Metadata.Keywords.Should().BeEquivalentTo(new[] { "new-keyword", "updated" });
        loaded.Metadata.Scores.Should().BeEquivalentTo(new[] { 100, 95 });
    }

    #endregion
}
