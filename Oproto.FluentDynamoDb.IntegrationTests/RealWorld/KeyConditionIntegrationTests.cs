using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for KeyCondition shortcuts (IfExists, IfNotExists, KeyCondition enum).
/// Tests verify that key conditions correctly generate attribute_exists/attribute_not_exists
/// conditions and that DynamoDB properly enforces them.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "KeyCondition")]
public class KeyConditionIntegrationTests : IntegrationTestBase
{
    private KeyConditionTestTableWrapper _table = null!;

    public KeyConditionIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateTableAsync<KeyConditionTestEntity>();
        _table = new KeyConditionTestTableWrapper(DynamoDb, TableName);
    }

    #region Task 8.1: Put with MustNotExist on existing item (should fail)

    [Fact]
    public async Task Put_WithMustNotExist_OnExistingItem_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Create an existing item
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#001",
            Sk = "PROFILE",
            Name = "Existing User",
            Status = "active"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // Create a new entity with the same key
        var newEntity = new KeyConditionTestEntity
        {
            Pk = "USER#001",
            Sk = "PROFILE",
            Name = "New User",
            Status = "pending"
        };

        // Act & Assert - Put with MustNotExist should fail
        // The library wraps AWS exceptions in DynamoDbMappingException
        var action = async () => await _table.Entities.Put(newEntity).IfNotExists().PutAsync();
        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

        // Verify original item is unchanged
        var loaded = await _table.Entities.Get("USER#001", "PROFILE").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Existing User");
    }

    [Fact]
    public async Task Put_WithKeyConditionMustNotExist_OnExistingItem_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Create an existing item
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#002",
            Sk = "PROFILE",
            Name = "Existing User 2",
            Status = "active"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // Create a new entity with the same key
        var newEntity = new KeyConditionTestEntity
        {
            Pk = "USER#002",
            Sk = "PROFILE",
            Name = "New User 2",
            Status = "pending"
        };

        // Act & Assert - Put with KeyCondition.MustNotExist should fail
        // The library wraps AWS exceptions in DynamoDbMappingException
        var action = async () => await _table.Entities.Put(newEntity).WithKeyCondition(KeyCondition.MustNotExist).PutAsync();
        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

        // Verify original item is unchanged
        var loaded = await _table.Entities.Get("USER#002", "PROFILE").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Existing User 2");
    }

    [Fact]
    public async Task Put_WithMustNotExist_OnNonExistingItem_Succeeds()
    {
        // Arrange - Create a new entity (no existing item)
        var newEntity = new KeyConditionTestEntity
        {
            Pk = "USER#003",
            Sk = "PROFILE",
            Name = "Brand New User",
            Status = "active"
        };

        // Act - Put with MustNotExist should succeed
        await _table.Entities.Put(newEntity).IfNotExists().PutAsync();

        // Assert - Item should be created
        var loaded = await _table.Entities.Get("USER#003", "PROFILE").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Brand New User");
    }

    #endregion

    #region Task 8.2: Put with MustExist on non-existing item (should fail)

    [Fact]
    public async Task Put_WithMustExist_OnNonExistingItem_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Entity that doesn't exist
        var newEntity = new KeyConditionTestEntity
        {
            Pk = "USER#NONEXISTENT",
            Sk = "PROFILE",
            Name = "Non-existent User",
            Status = "active"
        };

        // Act & Assert - Put with MustExist should fail
        // The library wraps AWS exceptions in DynamoDbMappingException
        var action = async () => await _table.Entities.Put(newEntity).IfExists().PutAsync();
        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

        // Verify item was not created
        var loaded = await _table.Entities.Get("USER#NONEXISTENT", "PROFILE").GetItemAsync();
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Put_WithKeyConditionMustExist_OnNonExistingItem_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Entity that doesn't exist
        var newEntity = new KeyConditionTestEntity
        {
            Pk = "USER#NONEXISTENT2",
            Sk = "PROFILE",
            Name = "Non-existent User 2",
            Status = "active"
        };

        // Act & Assert - Put with KeyCondition.MustExist should fail
        // The library wraps AWS exceptions in DynamoDbMappingException
        var action = async () => await _table.Entities.Put(newEntity).WithKeyCondition(KeyCondition.MustExist).PutAsync();
        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

        // Verify item was not created
        var loaded = await _table.Entities.Get("USER#NONEXISTENT2", "PROFILE").GetItemAsync();
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Put_WithMustExist_OnExistingItem_Succeeds()
    {
        // Arrange - Create an existing item first
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#004",
            Sk = "PROFILE",
            Name = "Original User",
            Status = "active"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // Create updated entity with same key
        var updatedEntity = new KeyConditionTestEntity
        {
            Pk = "USER#004",
            Sk = "PROFILE",
            Name = "Updated User",
            Status = "verified"
        };

        // Act - Put with MustExist should succeed (replace existing)
        await _table.Entities.Put(updatedEntity).IfExists().PutAsync();

        // Assert - Item should be updated
        var loaded = await _table.Entities.Get("USER#004", "PROFILE").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Updated User");
        loaded.Status.Should().Be("verified");
    }

    #endregion

    #region Task 8.3: Update with MustExist on non-existing item (should fail, prevents upsert)

    [Fact]
    public async Task Update_WithMustExist_OnNonExistingItem_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Non-existent item key
        var pk = "USER#NOUPDATE";
        var sk = "PROFILE";

        // Act & Assert - Update with MustExist should fail (prevents upsert)
        // The library wraps AWS exceptions in DynamoDbMappingException
        var action = async () => await _table.Entities.Update(pk, sk)
            .IfExists()
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Should Not Exist")
            .UpdateAsync();
        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

        // Verify item was not created (upsert prevented)
        var loaded = await _table.Entities.Get(pk, sk).GetItemAsync();
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Update_WithKeyConditionMustExist_OnNonExistingItem_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Non-existent item key
        var pk = "USER#NOUPDATE2";
        var sk = "PROFILE";

        // Act & Assert - Update with KeyCondition.MustExist should fail
        // The library wraps AWS exceptions in DynamoDbMappingException
        var action = async () => await _table.Entities.Update(pk, sk)
            .WithKeyCondition(KeyCondition.MustExist)
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Should Not Exist")
            .UpdateAsync();
        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

        // Verify item was not created
        var loaded = await _table.Entities.Get(pk, sk).GetItemAsync();
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Update_WithMustExist_OnExistingItem_Succeeds()
    {
        // Arrange - Create an existing item first
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#005",
            Sk = "PROFILE",
            Name = "Original Name",
            Status = "active"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // Act - Update with MustExist should succeed
        await _table.Entities.Update("USER#005", "PROFILE")
            .IfExists()
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Updated Name")
            .UpdateAsync();

        // Assert - Item should be updated
        var loaded = await _table.Entities.Get("USER#005", "PROFILE").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Update_WithoutKeyCondition_OnNonExistingItem_CreatesItem_Upsert()
    {
        // Arrange - Non-existent item key
        var pk = "USER#UPSERT";
        var sk = "PROFILE";

        // Act - Update without key condition should create item (upsert behavior)
        await _table.Entities.Update(pk, sk)
            .Set("SET #name = :name, #status = :status")
            .WithAttribute("#name", "name")
            .WithAttribute("#status", "status")
            .WithValue(":name", "Upserted User")
            .WithValue(":status", "new")
            .UpdateAsync();

        // Assert - Item should be created
        var loaded = await _table.Entities.Get(pk, sk).GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Upserted User");
    }

    #endregion

    #region Task 8.4: Delete with MustExist on non-existing item (should fail)

    [Fact]
    public async Task Delete_WithMustExist_OnNonExistingItem_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Non-existent item key
        var pk = "USER#NODELETE";
        var sk = "PROFILE";

        // Act & Assert - Delete with MustExist should fail
        // The library wraps AWS exceptions in DynamoDbMappingException
        var action = async () => await _table.Entities.Delete(pk, sk).IfExists().DeleteAsync();
        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();
    }

    [Fact]
    public async Task Delete_WithKeyConditionMustExist_OnNonExistingItem_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Non-existent item key
        var pk = "USER#NODELETE2";
        var sk = "PROFILE";

        // Act & Assert - Delete with KeyCondition.MustExist should fail
        // The library wraps AWS exceptions in DynamoDbMappingException
        var action = async () => await _table.Entities.Delete(pk, sk).WithKeyCondition(KeyCondition.MustExist).DeleteAsync();
        var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();
    }

    [Fact]
    public async Task Delete_WithMustExist_OnExistingItem_Succeeds()
    {
        // Arrange - Create an existing item first
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#006",
            Sk = "PROFILE",
            Name = "To Be Deleted",
            Status = "active"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // Act - Delete with MustExist should succeed
        await _table.Entities.Delete("USER#006", "PROFILE").IfExists().DeleteAsync();

        // Assert - Item should be deleted
        var loaded = await _table.Entities.Get("USER#006", "PROFILE").GetItemAsync();
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithoutKeyCondition_OnNonExistingItem_Succeeds_Idempotent()
    {
        // Arrange - Non-existent item key
        var pk = "USER#IDEMPOTENT";
        var sk = "PROFILE";

        // Act - Delete without key condition should succeed (idempotent)
        await _table.Entities.Delete(pk, sk).DeleteAsync();

        // Assert - No exception thrown, operation is idempotent
        var loaded = await _table.Entities.Get(pk, sk).GetItemAsync();
        loaded.Should().BeNull();
    }

    #endregion

    #region Task 8.5: Transaction with key condition

    [Fact]
    public async Task Transaction_WithKeyCondition_OnPut_EnforcesCondition()
    {
        // Arrange - Create an existing item
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#TXN1",
            Sk = "PROFILE",
            Name = "Existing Transaction User",
            Status = "active"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // Create a new entity with the same key
        var newEntity = new KeyConditionTestEntity
        {
            Pk = "USER#TXN1",
            Sk = "PROFILE",
            Name = "New Transaction User",
            Status = "pending"
        };

        // Act & Assert - Transaction with MustNotExist should fail
        var exception = await Assert.ThrowsAsync<TransactionCanceledException>(async () =>
        {
            await DynamoDbTransactions.Write
                .Add(_table.Entities.Put(newEntity).IfNotExists())
                .ExecuteAsync();
        });

        exception.Should().NotBeNull();

        // Verify original item is unchanged
        var loaded = await _table.Entities.Get("USER#TXN1", "PROFILE").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Existing Transaction User");
    }

    [Fact]
    public async Task Transaction_WithKeyCondition_OnUpdate_EnforcesCondition()
    {
        // Arrange - Non-existent item key
        var pk = "USER#TXN2";
        var sk = "PROFILE";

        // Act & Assert - Transaction with MustExist on update should fail
        var exception = await Assert.ThrowsAsync<TransactionCanceledException>(async () =>
        {
            await DynamoDbTransactions.Write
                .Add(_table.Entities.Update(pk, sk)
                    .IfExists()
                    .Set("SET #name = :name")
                    .WithAttribute("#name", "name")
                    .WithValue(":name", "Should Not Exist"))
                .ExecuteAsync();
        });

        exception.Should().NotBeNull();

        // Verify item was not created
        var loaded = await _table.Entities.Get(pk, sk).GetItemAsync();
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Transaction_WithKeyCondition_OnDelete_EnforcesCondition()
    {
        // Arrange - Non-existent item key
        var pk = "USER#TXN3";
        var sk = "PROFILE";

        // Act & Assert - Transaction with MustExist on delete should fail
        var exception = await Assert.ThrowsAsync<TransactionCanceledException>(async () =>
        {
            await DynamoDbTransactions.Write
                .Add(_table.Entities.Delete(pk, sk).IfExists())
                .ExecuteAsync();
        });

        exception.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_WithKeyCondition_MultipleOperations_AllConditionsEnforced()
    {
        // Arrange - Create one existing item
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#TXN4",
            Sk = "PROFILE",
            Name = "Existing User",
            Status = "active"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // New entity that should be created
        var newEntity = new KeyConditionTestEntity
        {
            Pk = "USER#TXN5",
            Sk = "PROFILE",
            Name = "New User",
            Status = "pending"
        };

        // Act - Transaction with valid conditions should succeed
        await DynamoDbTransactions.Write
            .Add(_table.Entities.Put(newEntity).IfNotExists())  // Should succeed (new item)
            .Add(_table.Entities.Update("USER#TXN4", "PROFILE")
                .IfExists()  // Should succeed (existing item)
                .Set("SET #status = :status")
                .WithAttribute("#status", "status")
                .WithValue(":status", "verified"))
            .ExecuteAsync();

        // Assert - Both operations should succeed
        var loadedNew = await _table.Entities.Get("USER#TXN5", "PROFILE").GetItemAsync();
        loadedNew.Should().NotBeNull();
        loadedNew!.Name.Should().Be("New User");

        var loadedExisting = await _table.Entities.Get("USER#TXN4", "PROFILE").GetItemAsync();
        loadedExisting.Should().NotBeNull();
        loadedExisting!.Status.Should().Be("verified");
    }

    [Fact]
    public async Task Transaction_WithKeyCondition_CombinedWithWhereClause_BothEnforced()
    {
        // Arrange - Create an existing item with specific status
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#TXN6",
            Sk = "PROFILE",
            Name = "Conditional User",
            Status = "pending"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // Act - Update with both key condition and where clause
        await DynamoDbTransactions.Write
            .Add(_table.Entities.Update("USER#TXN6", "PROFILE")
                .IfExists()  // Key condition
                .Set("SET #status = :newStatus")
                .Where("#status = :oldStatus")  // Additional condition
                .WithAttribute("#status", "status")
                .WithValue(":newStatus", "active")
                .WithValue(":oldStatus", "pending"))
            .ExecuteAsync();

        // Assert - Update should succeed
        var loaded = await _table.Entities.Get("USER#TXN6", "PROFILE").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("active");
    }

    [Fact]
    public async Task Transaction_WithKeyCondition_CombinedWithWhereClause_FailsWhenWhereNotMet()
    {
        // Arrange - Create an existing item with specific status
        var existingEntity = new KeyConditionTestEntity
        {
            Pk = "USER#TXN7",
            Sk = "PROFILE",
            Name = "Conditional User",
            Status = "active"  // Not "pending"
        };
        await _table.Entities.Put(existingEntity).PutAsync();

        // Act & Assert - Update should fail because where clause not met
        var exception = await Assert.ThrowsAsync<TransactionCanceledException>(async () =>
        {
            await DynamoDbTransactions.Write
                .Add(_table.Entities.Update("USER#TXN7", "PROFILE")
                    .IfExists()  // Key condition - would pass
                    .Set("SET #status = :newStatus")
                    .Where("#status = :oldStatus")  // This will fail
                    .WithAttribute("#status", "status")
                    .WithValue(":newStatus", "verified")
                    .WithValue(":oldStatus", "pending"))  // Doesn't match "active"
                .ExecuteAsync();
        });

        exception.Should().NotBeNull();

        // Verify item is unchanged
        var loaded = await _table.Entities.Get("USER#TXN7", "PROFILE").GetItemAsync();
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("active");
    }

    #endregion

    #region Simple Key Entity Tests

    [Fact]
    public async Task Put_WithMustNotExist_SimpleKeyEntity_OnExistingItem_Fails()
    {
        // Arrange - Create table for simple key entity
        var simpleTableName = $"test_simple_{Guid.NewGuid():N}";
        await CreateSimpleKeyTableAsync(simpleTableName);
        var simpleTable = new SimpleKeyTestTableWrapper(DynamoDb, simpleTableName);

        try
        {
            // Create an existing item
            var existingEntity = new SimpleKeyTestEntity
            {
                Id = "SIMPLE#001",
                Name = "Existing Simple Entity"
            };
            await simpleTable.Entities.Put(existingEntity).PutAsync();

            // Create a new entity with the same key
            var newEntity = new SimpleKeyTestEntity
            {
                Id = "SIMPLE#001",
                Name = "New Simple Entity"
            };

            // Act & Assert - Put with MustNotExist should fail
            // The library wraps AWS exceptions in DynamoDbMappingException
            var action = async () => await simpleTable.Entities.Put(newEntity).IfNotExists().PutAsync();
            var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
            exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

            // Verify original item is unchanged
            var loaded = await simpleTable.Entities.Get("SIMPLE#001").GetItemAsync();
            loaded.Should().NotBeNull();
            loaded!.Name.Should().Be("Existing Simple Entity");
        }
        finally
        {
            await DynamoDb.DeleteTableAsync(simpleTableName);
        }
    }

    [Fact]
    public async Task Update_WithMustExist_SimpleKeyEntity_OnNonExistingItem_Fails()
    {
        // Arrange - Create table for simple key entity
        var simpleTableName = $"test_simple_{Guid.NewGuid():N}";
        await CreateSimpleKeyTableAsync(simpleTableName);
        var simpleTable = new SimpleKeyTestTableWrapper(DynamoDb, simpleTableName);

        try
        {
            // Act & Assert - Update with MustExist should fail
            // The library wraps AWS exceptions in DynamoDbMappingException
            var action = async () => await simpleTable.Entities.Update("SIMPLE#NONEXISTENT")
                .IfExists()
                .Set("SET #name = :name")
                .WithAttribute("#name", "name")
                .WithValue(":name", "Should Not Exist")
                .UpdateAsync();
            var exception = await action.Should().ThrowAsync<DynamoDbMappingException>();
            exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();

            // Verify item was not created
            var loaded = await simpleTable.Entities.Get("SIMPLE#NONEXISTENT").GetItemAsync();
            loaded.Should().BeNull();
        }
        finally
        {
            await DynamoDb.DeleteTableAsync(simpleTableName);
        }
    }

    private async Task CreateSimpleKeyTableAsync(string tableName)
    {
        var request = new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        await DynamoDb.CreateTableAsync(request);
        await WaitForTableActiveAsync(tableName);
    }

    #endregion
}

#region Test Entities

/// <summary>
/// Test entity with composite key (PK + SK) for key condition integration tests.
/// </summary>
[DynamoDbTable("key-condition-test")]
public partial class KeyConditionTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string? Name { get; set; }

    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
}

/// <summary>
/// Test entity with simple key (PK only) for key condition integration tests.
/// </summary>
[DynamoDbTable("simple-key-test")]
public partial class SimpleKeyTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
}

#endregion

#region Test Table Classes

/// <summary>
/// Test table class for KeyConditionTestEntity (composite key).
/// </summary>
internal class KeyConditionTestTableWrapper : GenericTable
{
    public KeyConditionTestTableWrapper(IAmazonDynamoDB client, string tableName)
        : base(client, tableName)
    {
    }

    public KeyConditionTestEntityAccessor Entities =>
        new KeyConditionTestEntityAccessor(DynamoDbClient, Name);
}

/// <summary>
/// Entity accessor for KeyConditionTestEntity.
/// </summary>
internal class KeyConditionTestEntityAccessor
{
    private readonly IAmazonDynamoDB _client;
    private readonly string _tableName;

    public KeyConditionTestEntityAccessor(IAmazonDynamoDB client, string tableName)
    {
        _client = client;
        _tableName = tableName;
    }

    public PutItemRequestBuilder<KeyConditionTestEntity> Put(KeyConditionTestEntity entity) =>
        new PutItemRequestBuilder<KeyConditionTestEntity>(_client).ForTable(_tableName).WithItem(entity);

    public async Task PutAsync(KeyConditionTestEntity entity) =>
        await Put(entity).PutAsync();

    public GetItemRequestBuilder<KeyConditionTestEntity> Get(string pk, string sk) =>
        new GetItemRequestBuilder<KeyConditionTestEntity>(_client)
            .ForTable(_tableName)
            .WithKey("pk", pk)
            .WithKey("sk", sk);

    public async Task<KeyConditionTestEntity?> GetAsync(string pk, string sk) =>
        await Get(pk, sk).GetItemAsync();

    public UpdateItemRequestBuilder<KeyConditionTestEntity> Update(string pk, string sk) =>
        new UpdateItemRequestBuilder<KeyConditionTestEntity>(_client)
            .ForTable(_tableName)
            .WithKey("pk", pk)
            .WithKey("sk", sk);

    public DeleteItemRequestBuilder<KeyConditionTestEntity> Delete(string pk, string sk) =>
        new DeleteItemRequestBuilder<KeyConditionTestEntity>(_client)
            .ForTable(_tableName)
            .WithKey("pk", pk)
            .WithKey("sk", sk);

    public async Task DeleteAsync(string pk, string sk) =>
        await Delete(pk, sk).DeleteAsync();
}

/// <summary>
/// Test table class for SimpleKeyTestEntity (simple key - PK only).
/// </summary>
internal class SimpleKeyTestTableWrapper : GenericTable
{
    public SimpleKeyTestTableWrapper(IAmazonDynamoDB client, string tableName)
        : base(client, tableName)
    {
    }

    public SimpleKeyTestEntityAccessor Entities =>
        new SimpleKeyTestEntityAccessor(DynamoDbClient, Name);
}

/// <summary>
/// Entity accessor for SimpleKeyTestEntity.
/// </summary>
internal class SimpleKeyTestEntityAccessor
{
    private readonly IAmazonDynamoDB _client;
    private readonly string _tableName;

    public SimpleKeyTestEntityAccessor(IAmazonDynamoDB client, string tableName)
    {
        _client = client;
        _tableName = tableName;
    }

    public PutItemRequestBuilder<SimpleKeyTestEntity> Put(SimpleKeyTestEntity entity) =>
        new PutItemRequestBuilder<SimpleKeyTestEntity>(_client).ForTable(_tableName).WithItem(entity);

    public async Task PutAsync(SimpleKeyTestEntity entity) =>
        await Put(entity).PutAsync();

    public GetItemRequestBuilder<SimpleKeyTestEntity> Get(string pk) =>
        new GetItemRequestBuilder<SimpleKeyTestEntity>(_client)
            .ForTable(_tableName)
            .WithKey("pk", pk);

    public async Task<SimpleKeyTestEntity?> GetAsync(string pk) =>
        await Get(pk).GetItemAsync();

    public UpdateItemRequestBuilder<SimpleKeyTestEntity> Update(string pk) =>
        new UpdateItemRequestBuilder<SimpleKeyTestEntity>(_client)
            .ForTable(_tableName)
            .WithKey("pk", pk);

    public DeleteItemRequestBuilder<SimpleKeyTestEntity> Delete(string pk) =>
        new DeleteItemRequestBuilder<SimpleKeyTestEntity>(_client)
            .ForTable(_tableName)
            .WithKey("pk", pk);

    public async Task DeleteAsync(string pk) =>
        await Delete(pk).DeleteAsync();
}

#endregion
