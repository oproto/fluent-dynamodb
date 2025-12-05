using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Integration;

/// <summary>
/// Integration tests for advanced DynamoDB types (Maps, Sets, Lists, TTL).
/// These tests verify end-to-end functionality including serialization, storage, and deserialization.
/// Note: JSON blob and blob reference tests are not included as those features require source generator implementation.
/// </summary>
public class AdvancedTypesIntegrationTests
{
    private readonly IAmazonDynamoDB _mockClient = Substitute.For<IAmazonDynamoDB>();

    #region Map Property Tests (Task 20.1)

    [Fact]
    public void MapProperty_SaveAndLoad_PreservesData()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            ["color"] = "blue",
            ["size"] = "large",
            ["category"] = "electronics"
        };

        // Act - Convert to DynamoDB format
        var attributeValue = AttributeValueConverter.ToMap(metadata);

        // Assert - Verify conversion
        attributeValue.Should().NotBeNull();
        attributeValue!.M.Should().HaveCount(3);
        attributeValue.M["color"].S.Should().Be("blue");
        attributeValue.M["size"].S.Should().Be("large");
        attributeValue.M["category"].S.Should().Be("electronics");

        // Act - Convert back from DynamoDB format
        var reconstructed = AttributeValueConverter.FromMap(attributeValue);

        // Assert - Verify round-trip preserves data
        reconstructed.Should().NotBeNull();
        reconstructed.Should().HaveCount(3);
        reconstructed.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void MapProperty_WithEmptyDictionary_IsOmitted()
    {
        // Arrange
        var emptyDict = new Dictionary<string, string>();

        // Act
        var attributeValue = AttributeValueConverter.ToMap(emptyDict);

        // Assert - Empty collections should return null to be omitted
        attributeValue.Should().BeNull();
    }

    [Fact]
    public void MapProperty_WithNullDictionary_IsOmitted()
    {
        // Arrange
        Dictionary<string, string>? nullDict = null;

        // Act
        var attributeValue = AttributeValueConverter.ToMap(nullDict);

        // Assert - Null collections should return null to be omitted
        attributeValue.Should().BeNull();
    }

    [Fact]
    public void MapProperty_WithAttributeValueDictionary_PreservesData()
    {
        // Arrange
        var complexMap = new Dictionary<string, AttributeValue>
        {
            ["name"] = new AttributeValue { S = "Product A" },
            ["price"] = new AttributeValue { N = "99.99" },
            ["inStock"] = new AttributeValue { BOOL = true }
        };

        // Act - Convert to DynamoDB format
        var attributeValue = AttributeValueConverter.ToMap(complexMap);

        // Assert - Verify conversion
        attributeValue.Should().NotBeNull();
        attributeValue!.M.Should().HaveCount(3);
        attributeValue.M["name"].S.Should().Be("Product A");
        attributeValue.M["price"].N.Should().Be("99.99");
        attributeValue.M["inStock"].BOOL.Should().BeTrue();

        // Act - Convert back (for AttributeValue dict, it's the same)
        var reconstructed = attributeValue.M;

        // Assert - Verify round-trip preserves data
        reconstructed.Should().BeEquivalentTo(complexMap);
    }

    [Fact]
    public void MapProperty_WithMissingAttribute_ReturnsNull()
    {
        // Arrange
        var attributeValue = new AttributeValue(); // No M property set

        // Act
        var result = AttributeValueConverter.FromMap(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Set Property Tests (Task 20.2)

    [Fact]
    public void SetProperty_StringSet_SaveAndLoad_PreservesData()
    {
        // Arrange
        var tags = new HashSet<string> { "electronics", "sale", "featured" };

        // Act - Convert to DynamoDB format
        var attributeValue = AttributeValueConverter.ToStringSet(tags);

        // Assert - Verify conversion
        attributeValue.Should().NotBeNull();
        attributeValue!.SS.Should().HaveCount(3);
        attributeValue.SS.Should().Contain("electronics");
        attributeValue.SS.Should().Contain("sale");
        attributeValue.SS.Should().Contain("featured");

        // Act - Convert back from DynamoDB format
        var reconstructed = AttributeValueConverter.FromStringSet(attributeValue);

        // Assert - Verify round-trip preserves data
        reconstructed.Should().NotBeNull();
        reconstructed.Should().HaveCount(3);
        reconstructed.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public void SetProperty_NumberSet_SaveAndLoad_PreservesData()
    {
        // Arrange
        var categoryIds = new HashSet<int> { 1, 5, 10, 25 };

        // Act - Convert to DynamoDB format
        var attributeValue = AttributeValueConverter.ToNumberSet(categoryIds);

        // Assert - Verify conversion
        attributeValue.Should().NotBeNull();
        attributeValue!.NS.Should().HaveCount(4);
        attributeValue.NS.Should().Contain("1");
        attributeValue.NS.Should().Contain("5");
        attributeValue.NS.Should().Contain("10");
        attributeValue.NS.Should().Contain("25");

        // Act - Convert back from DynamoDB format
        var reconstructed = AttributeValueConverter.FromNumberSetInt(attributeValue);

        // Assert - Verify round-trip preserves data
        reconstructed.Should().NotBeNull();
        reconstructed.Should().HaveCount(4);
        reconstructed.Should().BeEquivalentTo(categoryIds);
    }

    [Fact]
    public void SetProperty_WithEmptySet_IsOmitted()
    {
        // Arrange
        var emptySet = new HashSet<string>();

        // Act
        var attributeValue = AttributeValueConverter.ToStringSet(emptySet);

        // Assert - Empty collections should return null to be omitted
        attributeValue.Should().BeNull();
    }

    [Fact]
    public void SetProperty_WithNullSet_IsOmitted()
    {
        // Arrange
        HashSet<string>? nullSet = null;

        // Act
        var attributeValue = AttributeValueConverter.ToStringSet(nullSet);

        // Assert - Null collections should return null to be omitted
        attributeValue.Should().BeNull();
    }

    #endregion

    #region List Property Tests (Task 20.3)

    [Fact]
    public void ListProperty_SaveAndLoad_PreservesData()
    {
        // Arrange
        var itemIds = new List<string> { "ITEM-001", "ITEM-002", "ITEM-003" };

        // Act - Convert to DynamoDB format
        var attributeValue = AttributeValueConverter.ToList(itemIds, s => new AttributeValue { S = s });

        // Assert - Verify conversion
        attributeValue.Should().NotBeNull();
        attributeValue!.L.Should().HaveCount(3);
        attributeValue.L[0].S.Should().Be("ITEM-001");
        attributeValue.L[1].S.Should().Be("ITEM-002");
        attributeValue.L[2].S.Should().Be("ITEM-003");

        // Act - Convert back from DynamoDB format
        var reconstructed = AttributeValueConverter.FromList(attributeValue, av => av.S);

        // Assert - Verify round-trip preserves data and order
        reconstructed.Should().NotBeNull();
        reconstructed.Should().HaveCount(3);
        reconstructed.Should().ContainInOrder("ITEM-001", "ITEM-002", "ITEM-003");
    }

    [Fact]
    public void ListProperty_WithNumbers_PreservesData()
    {
        // Arrange
        var prices = new List<decimal> { 9.99m, 19.99m, 29.99m };

        // Act - Convert to DynamoDB format
        var attributeValue = AttributeValueConverter.ToList(prices, p => new AttributeValue { N = p.ToString() });

        // Assert - Verify conversion
        attributeValue.Should().NotBeNull();
        attributeValue!.L.Should().HaveCount(3);
        attributeValue.L[0].N.Should().Be("9.99");
        attributeValue.L[1].N.Should().Be("19.99");
        attributeValue.L[2].N.Should().Be("29.99");

        // Act - Convert back from DynamoDB format
        var reconstructed = AttributeValueConverter.FromList(attributeValue, av => decimal.Parse(av.N));

        // Assert - Verify round-trip preserves data and order
        reconstructed.Should().NotBeNull();
        reconstructed.Should().HaveCount(3);
        reconstructed.Should().ContainInOrder(9.99m, 19.99m, 29.99m);
    }

    [Fact]
    public void ListProperty_WithEmptyList_IsOmitted()
    {
        // Arrange
        var emptyList = new List<string>();

        // Act
        var attributeValue = AttributeValueConverter.ToList(emptyList, s => new AttributeValue { S = s });

        // Assert - Empty collections should return null to be omitted
        attributeValue.Should().BeNull();
    }

    [Fact]
    public void ListProperty_WithNullList_IsOmitted()
    {
        // Arrange
        List<string>? nullList = null;

        // Act
        var attributeValue = AttributeValueConverter.ToList(nullList, s => new AttributeValue { S = s });

        // Assert - Null collections should return null to be omitted
        attributeValue.Should().BeNull();
    }

    #endregion

    #region TTL Property Tests (Task 20.4)

    [Fact]
    public void TtlProperty_DateTime_SaveAndLoad_PreservesValue()
    {
        // Arrange
        var expiresAt = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act - Convert to DynamoDB format (Unix epoch)
        var attributeValue = AttributeValueConverter.ToTtl(expiresAt);

        // Assert - Verify conversion to Unix epoch
        attributeValue.Should().NotBeNull();
        attributeValue!.N.Should().NotBeNullOrEmpty();
        var epochSeconds = long.Parse(attributeValue.N);
        epochSeconds.Should().BeGreaterThan(0);

        // Act - Convert back from DynamoDB format
        var reconstructed = AttributeValueConverter.FromTtl(attributeValue);

        // Assert - Verify round-trip preserves value within tolerance (1 second)
        reconstructed.Should().NotBeNull();
        reconstructed.Value.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TtlProperty_DateTimeOffset_SaveAndLoad_PreservesValue()
    {
        // Arrange
        var expiresAt = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero);

        // Act - Convert to DynamoDB format (Unix epoch)
        var attributeValue = AttributeValueConverter.ToTtl(expiresAt);

        // Assert - Verify conversion to Unix epoch
        attributeValue.Should().NotBeNull();
        attributeValue!.N.Should().NotBeNullOrEmpty();
        var epochSeconds = long.Parse(attributeValue.N);
        epochSeconds.Should().BeGreaterThan(0);

        // Verify the Unix epoch value is correct
        var expectedEpoch = expiresAt.ToUnixTimeSeconds();
        epochSeconds.Should().Be(expectedEpoch);
    }

    [Fact]
    public void TtlProperty_WithNull_IsOmitted()
    {
        // Arrange
        DateTime? nullDateTime = null;

        // Act
        var attributeValue = AttributeValueConverter.ToTtl(nullDateTime);

        // Assert - Null values should return null to be omitted
        attributeValue.Should().BeNull();
    }

    [Fact]
    public void TtlProperty_UnixEpochStoredCorrectly()
    {
        // Arrange - Known date/time
        var testDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedEpoch = 1704067200; // Unix epoch for 2024-01-01 00:00:00 UTC

        // Act
        var attributeValue = AttributeValueConverter.ToTtl(testDate);

        // Assert
        attributeValue.Should().NotBeNull();
        var actualEpoch = long.Parse(attributeValue!.N);
        actualEpoch.Should().Be(expectedEpoch);
    }

    #endregion

    #region Format String Tests (Task 20.8)
    
    // Note: Format string tests with advanced types are covered in AttributeValueConverterFormatStringTests.cs
    // These integration tests focus on the core conversion methods that are used by the format string processor.
    // The format string processor itself is internal and tested separately in the Utility tests.
    // Tasks 20.5, 20.6, and 20.7 (JSON blob and blob reference) require source generator implementation
    // and will be tested once those features are fully implemented.

    #endregion
}
