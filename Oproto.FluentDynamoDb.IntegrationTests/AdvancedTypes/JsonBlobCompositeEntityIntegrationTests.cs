using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.SystemTextJson;

namespace Oproto.FluentDynamoDb.IntegrationTests.AdvancedTypes;

/// <summary>
/// Integration tests for JsonBlob properties in composite entities.
/// These tests verify the fix for the bug where [JsonBlob] properties were incorrectly
/// deserialized in composite entities via ToCompositeEntityAsync().
/// </summary>
/// <remarks>
/// These tests validate:
/// - JsonBlob properties on parent entities are correctly deserialized in multi-item FromDynamoDb
/// - JsonBlob properties on child entities are correctly deserialized when loaded as related entities
/// - Nullable JsonBlob properties handle null values gracefully
/// - List JsonBlob properties correctly deserialize JSON arrays
/// - Round-trip consistency for composite entities with JsonBlob properties
/// 
/// **Validates: Requirements 1.2, 4.2 from jsonblob-composite-entity-fix spec**
/// </remarks>
[Collection("DynamoDB Local")]
public class JsonBlobCompositeEntityIntegrationTests : IntegrationTestBase
{
    public JsonBlobCompositeEntityIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateTableWithSortKeyAsync();
    }

    /// <summary>
    /// Creates a table with partition key and sort key for composite entity testing.
    /// </summary>
    private async Task CreateTableWithSortKeyAsync()
    {
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        await DynamoDb.CreateTableAsync(request);
        await WaitForTableActiveAsync(TableName);
    }

    #region End-to-End Composite Entity Tests

    /// <summary>
    /// End-to-end test: Save composite entity with JsonBlob properties, load via ToCompositeEntityAsync.
    /// 
    /// This test verifies the complete round-trip:
    /// 1. Create parent entity with JsonBlob property
    /// 2. Create child entities with JsonBlob properties (nullable and collection)
    /// 3. Save all entities to DynamoDB Local
    /// 4. Load via ToCompositeEntityAsync
    /// 5. Verify all JsonBlob properties are correctly deserialized
    /// 
    /// **Validates: Requirements 1.2, 4.2**
    /// </summary>
    [Fact]
    public async Task CompositeEntity_WithJsonBlobProperties_RoundTrip_PreservesAllData()
    {
        // Arrange - Create table with JSON serializer configured
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var table = new JsonBlobCompositeTable(DynamoDb, TableName, options);

        var locationId = $"LOC-{Guid.NewGuid():N}";
        var pk = LocationWithJsonBlobEntity.Keys.Pk(locationId);
        var sk = LocationWithJsonBlobEntity.Keys.Sk(locationId);

        // Create parent entity with JsonBlob property
        var location = new LocationWithJsonBlobEntity
        {
            Pk = pk,
            Sk = sk,
            Name = "Test Location",
            Address = new LocationAddress
            {
                Street = "123 Main Street",
                City = "Seattle",
                State = "WA",
                ZipCode = "98101",
                Country = "USA"
            }
        };

        // Create child entities with JsonBlob properties
        var contact1 = new ContactWithJsonBlobEntity
        {
            Pk = pk,
            Sk = $"{sk}#CONTACT#1",
            ContactName = "John Doe",
            Email = "john.doe@example.com",
            Preferences = new ContactPreferences
            {
                PreferredLanguage = "en-US",
                ReceiveNewsletter = true,
                TimeZone = "America/Los_Angeles"
            },
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Type = "mobile", Number = "+1-555-123-4567", IsPrimary = true },
                new PhoneNumber { Type = "work", Number = "+1-555-987-6543", IsPrimary = false }
            }
        };

        var contact2 = new ContactWithJsonBlobEntity
        {
            Pk = pk,
            Sk = $"{sk}#CONTACT#2",
            ContactName = "Jane Smith",
            Email = "jane.smith@example.com",
            Preferences = null, // Test null JsonBlob
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Type = "home", Number = "+1-555-111-2222", IsPrimary = true }
            }
        };

        // Act - Save all entities to DynamoDB
        await table.Locations.Put(location).PutAsync();
        await table.Contacts.Put(contact1).PutAsync();
        await table.Contacts.Put(contact2).PutAsync();

        // Load via ToCompositeEntityAsync
        var loadedLocation = await table.Locations.Query()
            .Where(x => x.Pk == pk && x.Sk.StartsWith(sk))
            .ToCompositeEntityAsync<LocationWithJsonBlobEntity>();

        // Assert - Verify parent entity
        loadedLocation.Should().NotBeNull();
        loadedLocation!.Pk.Should().Be(pk);
        loadedLocation.Sk.Should().Be(sk);
        loadedLocation.Name.Should().Be("Test Location");

        // Verify parent JsonBlob property
        loadedLocation.Address.Should().NotBeNull();
        loadedLocation.Address!.Street.Should().Be("123 Main Street");
        loadedLocation.Address.City.Should().Be("Seattle");
        loadedLocation.Address.State.Should().Be("WA");
        loadedLocation.Address.ZipCode.Should().Be("98101");
        loadedLocation.Address.Country.Should().Be("USA");

        // Verify child entities were loaded
        loadedLocation.Contacts.Should().HaveCount(2);

        // Verify first child entity with all JsonBlob properties
        var loadedContact1 = loadedLocation.Contacts.FirstOrDefault(c => c.ContactName == "John Doe");
        loadedContact1.Should().NotBeNull();
        loadedContact1!.Email.Should().Be("john.doe@example.com");
        
        // Verify nullable JsonBlob property
        loadedContact1.Preferences.Should().NotBeNull();
        loadedContact1.Preferences!.PreferredLanguage.Should().Be("en-US");
        loadedContact1.Preferences.ReceiveNewsletter.Should().BeTrue();
        loadedContact1.Preferences.TimeZone.Should().Be("America/Los_Angeles");
        
        // Verify List JsonBlob property
        loadedContact1.PhoneNumbers.Should().NotBeNull();
        loadedContact1.PhoneNumbers.Should().HaveCount(2);
        loadedContact1.PhoneNumbers![0].Type.Should().Be("mobile");
        loadedContact1.PhoneNumbers[0].Number.Should().Be("+1-555-123-4567");
        loadedContact1.PhoneNumbers[0].IsPrimary.Should().BeTrue();
        loadedContact1.PhoneNumbers[1].Type.Should().Be("work");
        loadedContact1.PhoneNumbers[1].Number.Should().Be("+1-555-987-6543");
        loadedContact1.PhoneNumbers[1].IsPrimary.Should().BeFalse();

        // Verify second child entity with null JsonBlob
        var loadedContact2 = loadedLocation.Contacts.FirstOrDefault(c => c.ContactName == "Jane Smith");
        loadedContact2.Should().NotBeNull();
        loadedContact2!.Email.Should().Be("jane.smith@example.com");
        loadedContact2.Preferences.Should().BeNull(); // Null JsonBlob should be preserved
        loadedContact2.PhoneNumbers.Should().NotBeNull();
        loadedContact2.PhoneNumbers.Should().HaveCount(1);
    }

    /// <summary>
    /// Test that parent entity with JsonBlob but no children loads correctly.
    /// 
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public async Task CompositeEntity_ParentOnlyWithJsonBlob_LoadsCorrectly()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var table = new JsonBlobCompositeTable(DynamoDb, TableName, options);

        var locationId = $"LOC-{Guid.NewGuid():N}";
        var pk = LocationWithJsonBlobEntity.Keys.Pk(locationId);
        var sk = LocationWithJsonBlobEntity.Keys.Sk(locationId);

        var location = new LocationWithJsonBlobEntity
        {
            Pk = pk,
            Sk = sk,
            Name = "Standalone Location",
            Address = new LocationAddress
            {
                Street = "456 Oak Avenue",
                City = "Portland",
                State = "OR",
                ZipCode = "97201",
                Country = "USA"
            }
        };

        // Act - Save only parent entity
        await table.Locations.Put(location).PutAsync();

        // Load via ToCompositeEntityAsync (no children)
        var loadedLocation = await table.Locations.Query()
            .Where(x => x.Pk == pk && x.Sk.StartsWith(sk))
            .ToCompositeEntityAsync<LocationWithJsonBlobEntity>();

        // Assert
        loadedLocation.Should().NotBeNull();
        loadedLocation!.Name.Should().Be("Standalone Location");
        loadedLocation.Address.Should().NotBeNull();
        loadedLocation.Address!.City.Should().Be("Portland");
        loadedLocation.Contacts.Should().BeEmpty();
    }

    /// <summary>
    /// Test that null JsonBlob properties on both parent and children are preserved.
    /// 
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Fact]
    public async Task CompositeEntity_NullJsonBlobProperties_PreservesNulls()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var table = new JsonBlobCompositeTable(DynamoDb, TableName, options);

        var locationId = $"LOC-{Guid.NewGuid():N}";
        var pk = LocationWithJsonBlobEntity.Keys.Pk(locationId);
        var sk = LocationWithJsonBlobEntity.Keys.Sk(locationId);

        // Parent with null JsonBlob
        var location = new LocationWithJsonBlobEntity
        {
            Pk = pk,
            Sk = sk,
            Name = "Location Without Address",
            Address = null // Explicitly null
        };

        // Child with null JsonBlob properties
        var contact = new ContactWithJsonBlobEntity
        {
            Pk = pk,
            Sk = $"{sk}#CONTACT#1",
            ContactName = "Minimal Contact",
            Email = "minimal@example.com",
            Preferences = null,
            PhoneNumbers = null
        };

        // Act
        await table.Locations.Put(location).PutAsync();
        await table.Contacts.Put(contact).PutAsync();

        var loadedLocation = await table.Locations.Query()
            .Where(x => x.Pk == pk && x.Sk.StartsWith(sk))
            .ToCompositeEntityAsync<LocationWithJsonBlobEntity>();

        // Assert
        loadedLocation.Should().NotBeNull();
        loadedLocation!.Address.Should().BeNull();
        loadedLocation.Contacts.Should().HaveCount(1);
        loadedLocation.Contacts[0].Preferences.Should().BeNull();
        loadedLocation.Contacts[0].PhoneNumbers.Should().BeNull();
    }

    /// <summary>
    /// Test that loading via GetItemAsync (single item) produces same result as direct load.
    /// This verifies consistency between single-item and multi-item FromDynamoDb methods.
    /// 
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Fact]
    public async Task CompositeEntity_SingleItemLoad_ConsistentWithDirectLoad()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var table = new JsonBlobCompositeTable(DynamoDb, TableName, options);

        var locationId = $"LOC-{Guid.NewGuid():N}";
        var pk = LocationWithJsonBlobEntity.Keys.Pk(locationId);
        var sk = LocationWithJsonBlobEntity.Keys.Sk(locationId);

        var location = new LocationWithJsonBlobEntity
        {
            Pk = pk,
            Sk = sk,
            Name = "Consistency Test Location",
            Address = new LocationAddress
            {
                Street = "789 Pine Street",
                City = "San Francisco",
                State = "CA",
                ZipCode = "94102",
                Country = "USA"
            }
        };

        // Act - Save entity
        await table.Locations.Put(location).PutAsync();

        // Load via GetItemAsync (single-item FromDynamoDb)
        var singleItemLoad = await table.Locations.Get(pk, sk).GetItemAsync();

        // Load via ToCompositeEntityAsync (multi-item FromDynamoDb)
        var compositeLoad = await table.Locations.Query()
            .Where(x => x.Pk == pk && x.Sk == sk)
            .ToCompositeEntityAsync<LocationWithJsonBlobEntity>();

        // Assert - Both should have identical JsonBlob property values
        singleItemLoad.Should().NotBeNull();
        compositeLoad.Should().NotBeNull();

        singleItemLoad!.Address.Should().NotBeNull();
        compositeLoad!.Address.Should().NotBeNull();

        singleItemLoad.Address!.Street.Should().Be(compositeLoad.Address!.Street);
        singleItemLoad.Address.City.Should().Be(compositeLoad.Address.City);
        singleItemLoad.Address.State.Should().Be(compositeLoad.Address.State);
        singleItemLoad.Address.ZipCode.Should().Be(compositeLoad.Address.ZipCode);
        singleItemLoad.Address.Country.Should().Be(compositeLoad.Address.Country);
    }

    /// <summary>
    /// Test with multiple children, each having different JsonBlob configurations.
    /// 
    /// **Validates: Requirements 1.2, 1.4**
    /// </summary>
    [Fact]
    public async Task CompositeEntity_MultipleChildrenWithVariedJsonBlob_LoadsAllCorrectly()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var table = new JsonBlobCompositeTable(DynamoDb, TableName, options);

        var locationId = $"LOC-{Guid.NewGuid():N}";
        var pk = LocationWithJsonBlobEntity.Keys.Pk(locationId);
        var sk = LocationWithJsonBlobEntity.Keys.Sk(locationId);

        var location = new LocationWithJsonBlobEntity
        {
            Pk = pk,
            Sk = sk,
            Name = "Multi-Contact Location",
            Address = new LocationAddress { Street = "100 Test Blvd", City = "Austin", State = "TX", ZipCode = "78701", Country = "USA" }
        };

        // Create 5 contacts with varied JsonBlob configurations
        var contacts = new List<ContactWithJsonBlobEntity>
        {
            new ContactWithJsonBlobEntity
            {
                Pk = pk, Sk = $"{sk}#CONTACT#1", ContactName = "Contact 1", Email = "c1@test.com",
                Preferences = new ContactPreferences { PreferredLanguage = "en", ReceiveNewsletter = true, TimeZone = "UTC" },
                PhoneNumbers = new List<PhoneNumber> { new PhoneNumber { Type = "mobile", Number = "111", IsPrimary = true } }
            },
            new ContactWithJsonBlobEntity
            {
                Pk = pk, Sk = $"{sk}#CONTACT#2", ContactName = "Contact 2", Email = "c2@test.com",
                Preferences = null, // Null preferences
                PhoneNumbers = new List<PhoneNumber> { new PhoneNumber { Type = "work", Number = "222", IsPrimary = false } }
            },
            new ContactWithJsonBlobEntity
            {
                Pk = pk, Sk = $"{sk}#CONTACT#3", ContactName = "Contact 3", Email = "c3@test.com",
                Preferences = new ContactPreferences { PreferredLanguage = "es", ReceiveNewsletter = false, TimeZone = "America/Mexico_City" },
                PhoneNumbers = null // Null phone numbers
            },
            new ContactWithJsonBlobEntity
            {
                Pk = pk, Sk = $"{sk}#CONTACT#4", ContactName = "Contact 4", Email = "c4@test.com",
                Preferences = null,
                PhoneNumbers = null // Both null
            },
            new ContactWithJsonBlobEntity
            {
                Pk = pk, Sk = $"{sk}#CONTACT#5", ContactName = "Contact 5", Email = "c5@test.com",
                Preferences = new ContactPreferences { PreferredLanguage = "fr", ReceiveNewsletter = true, TimeZone = "Europe/Paris" },
                PhoneNumbers = new List<PhoneNumber>
                {
                    new PhoneNumber { Type = "home", Number = "555-1", IsPrimary = true },
                    new PhoneNumber { Type = "mobile", Number = "555-2", IsPrimary = false },
                    new PhoneNumber { Type = "work", Number = "555-3", IsPrimary = false }
                }
            }
        };

        // Act - Save all entities
        await table.Locations.Put(location).PutAsync();
        foreach (var contact in contacts)
        {
            await table.Contacts.Put(contact).PutAsync();
        }

        // Load via ToCompositeEntityAsync
        var loadedLocation = await table.Locations.Query()
            .Where(x => x.Pk == pk && x.Sk.StartsWith(sk))
            .ToCompositeEntityAsync<LocationWithJsonBlobEntity>();

        // Assert
        loadedLocation.Should().NotBeNull();
        loadedLocation!.Contacts.Should().HaveCount(5);

        // Verify each contact's JsonBlob properties
        var c1 = loadedLocation.Contacts.First(c => c.ContactName == "Contact 1");
        c1.Preferences.Should().NotBeNull();
        c1.Preferences!.PreferredLanguage.Should().Be("en");
        c1.PhoneNumbers.Should().HaveCount(1);

        var c2 = loadedLocation.Contacts.First(c => c.ContactName == "Contact 2");
        c2.Preferences.Should().BeNull();
        c2.PhoneNumbers.Should().HaveCount(1);

        var c3 = loadedLocation.Contacts.First(c => c.ContactName == "Contact 3");
        c3.Preferences.Should().NotBeNull();
        c3.Preferences!.PreferredLanguage.Should().Be("es");
        c3.PhoneNumbers.Should().BeNull();

        var c4 = loadedLocation.Contacts.First(c => c.ContactName == "Contact 4");
        c4.Preferences.Should().BeNull();
        c4.PhoneNumbers.Should().BeNull();

        var c5 = loadedLocation.Contacts.First(c => c.ContactName == "Contact 5");
        c5.Preferences.Should().NotBeNull();
        c5.Preferences!.PreferredLanguage.Should().Be("fr");
        c5.PhoneNumbers.Should().HaveCount(3);
    }

    #endregion
}
