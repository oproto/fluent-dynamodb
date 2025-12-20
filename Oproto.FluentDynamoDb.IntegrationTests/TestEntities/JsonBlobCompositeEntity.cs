using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Generated table class for JsonBlob composite entity integration tests.
/// </summary>
public partial class JsonBlobCompositeTable;

/// <summary>
/// Parent entity with a [RelatedEntity] collection pointing to child entities with [JsonBlob] properties.
/// This entity is used to test the fix for JsonBlob deserialization in composite entities.
/// </summary>
/// <remarks>
/// Key Design:
/// - Partition Key (pk): "LOCATION#{locationId}"
/// - Sort Key (sk): "LOCATION#{locationId}" (same as pk for parent)
/// 
/// The [RelatedEntity] attribute tells ToCompositeEntityAsync to populate the Contacts collection
/// from ContactEntity items matching the sort key pattern "LOCATION#*#CONTACT#*".
/// </remarks>
[DynamoDbTable(typeof(JsonBlobCompositeTable), IsDefault = true)]
[GenerateEntityProperty(Name = "Locations")]
public partial class LocationWithJsonBlobEntity
{
    /// <summary>
    /// Partition key in format "LOCATION#{locationId}".
    /// </summary>
    [PartitionKey(Prefix = "LOCATION")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Sort key in format "LOCATION#{locationId}".
    /// </summary>
    [SortKey(Prefix = "LOCATION")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Location name.
    /// </summary>
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// JsonBlob property on the PARENT entity.
    /// This tests that the multi-item FromDynamoDb method correctly uses JSON deserialization.
    /// </summary>
    [JsonBlob]
    [DynamoDbAttribute("address")]
    public LocationAddress? Address { get; set; }

    /// <summary>
    /// Related entity collection - automatically populated by ToCompositeEntityAsync.
    /// The child entity (ContactWithJsonBlobEntity) contains [JsonBlob] properties.
    /// </summary>
    [RelatedEntity("LOCATION#*#CONTACT#*", EntityType = typeof(ContactWithJsonBlobEntity))]
    public List<ContactWithJsonBlobEntity> Contacts { get; set; } = new();
}

/// <summary>
/// Child entity with [JsonBlob] properties, related to LocationWithJsonBlobEntity.
/// This entity tests that JsonBlob properties in related entities are correctly deserialized.
/// </summary>
/// <remarks>
/// Key Design:
/// - Partition Key (pk): "LOCATION#{locationId}" (same as parent)
/// - Sort Key (sk): "LOCATION#{locationId}#CONTACT#{contactId}" (extends parent key)
/// 
/// The hierarchical sort key enables single-query retrieval of parent + all children.
/// </remarks>
[DynamoDbTable(typeof(JsonBlobCompositeTable))]
[GenerateEntityProperty(Name = "Contacts")]
public partial class ContactWithJsonBlobEntity
{
    /// <summary>
    /// Partition key in format "LOCATION#{locationId}".
    /// </summary>
    [PartitionKey(Prefix = "LOCATION")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Sort key in format "LOCATION#{locationId}#CONTACT#{contactId}".
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Contact name.
    /// </summary>
    [DynamoDbAttribute("contactName")]
    public string ContactName { get; set; } = string.Empty;

    /// <summary>
    /// Contact email.
    /// </summary>
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Nullable JsonBlob property - tests null handling in related entities.
    /// </summary>
    [JsonBlob]
    [DynamoDbAttribute("preferences")]
    public ContactPreferences? Preferences { get; set; }

    /// <summary>
    /// List JsonBlob property - tests collection deserialization in related entities.
    /// </summary>
    [JsonBlob]
    [DynamoDbAttribute("phoneNumbers")]
    public List<PhoneNumber>? PhoneNumbers { get; set; }
}

/// <summary>
/// Complex type for parent entity's JsonBlob property.
/// </summary>
public class LocationAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>
/// Complex type for child entity's nullable JsonBlob property.
/// </summary>
public class ContactPreferences
{
    public string PreferredLanguage { get; set; } = string.Empty;
    public bool ReceiveNewsletter { get; set; }
    public string TimeZone { get; set; } = string.Empty;
}

/// <summary>
/// Complex type for child entity's List JsonBlob property.
/// </summary>
public class PhoneNumber
{
    public string Type { get; set; } = string.Empty; // "mobile", "work", "home"
    public string Number { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
