using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Tests for record type entity serialization and deserialization.
/// Verifies that record entities can be correctly mapped to/from DynamoDB format.
/// _Requirements: 2.2, 2.3_
/// </summary>
[Trait("Category", "Unit")]
public class RecordTypeSerializationTests
{
    [Fact]
    public void TestRecordEntity_ToDynamoDb_SerializesCorrectly()
    {
        // Arrange
        var entity = new TestRecordEntity
        {
            Id = "test-123",
            SortKey = "sk-456",
            Name = "Test Name",
            Value = 42,
            CreatedAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)
        };

        // Act
        var result = TestRecordEntity.ToDynamoDb(entity);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("pk");
        result["pk"].S.Should().Be("test-123");
        result.Should().ContainKey("sk");
        result["sk"].S.Should().Be("sk-456");
        result.Should().ContainKey("name");
        result["name"].S.Should().Be("Test Name");
        result.Should().ContainKey("value");
        result["value"].N.Should().Be("42");
        result.Should().ContainKey("created_at");
        result["created_at"].S.Should().Contain("2024-01-15");
    }

    [Fact]
    public void TestRecordEntity_FromDynamoDb_DeserializesCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-123" },
            ["sk"] = new AttributeValue { S = "sk-456" },
            ["name"] = new AttributeValue { S = "Test Name" },
            ["value"] = new AttributeValue { N = "42" },
            ["created_at"] = new AttributeValue { S = "2024-01-15T10:30:00.0000000+00:00" }
        };

        // Act
        var result = TestRecordEntity.FromDynamoDb<TestRecordEntity>(item);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("test-123");
        result.SortKey.Should().Be("sk-456");
        result.Name.Should().Be("Test Name");
        result.Value.Should().Be(42);
        result.CreatedAt.Should().Be(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TestRecordEntity_RoundTrip_PreservesValues()
    {
        // Arrange
        var original = new TestRecordEntity
        {
            Id = "round-trip-test",
            SortKey = "sk-round-trip",
            Name = "Round Trip Test",
            Value = 100,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var dynamoDbItem = TestRecordEntity.ToDynamoDb(original);
        var restored = TestRecordEntity.FromDynamoDb<TestRecordEntity>(dynamoDbItem);

        // Assert
        restored.Id.Should().Be(original.Id);
        restored.SortKey.Should().Be(original.SortKey);
        restored.Name.Should().Be(original.Name);
        restored.Value.Should().Be(original.Value);
        // DateTimeOffset comparison with tolerance for serialization precision
        restored.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void TestPositionalRecordEntity_ToDynamoDb_SerializesCorrectly()
    {
        // Arrange
        var entity = new TestPositionalRecordEntity("pos-123", "sk-pos", "Positional Name", 99);

        // Act
        var result = TestPositionalRecordEntity.ToDynamoDb(entity);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("pk");
        result["pk"].S.Should().Be("pos-123");
        result.Should().ContainKey("sk");
        result["sk"].S.Should().Be("sk-pos");
        result.Should().ContainKey("name");
        result["name"].S.Should().Be("Positional Name");
        result.Should().ContainKey("count");
        result["count"].N.Should().Be("99");
    }

    [Fact]
    public void TestPositionalRecordEntity_FromDynamoDb_DeserializesCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "pos-123" },
            ["sk"] = new AttributeValue { S = "sk-pos" },
            ["name"] = new AttributeValue { S = "Positional Name" },
            ["count"] = new AttributeValue { N = "99" }
        };

        // Act
        var result = TestPositionalRecordEntity.FromDynamoDb<TestPositionalRecordEntity>(item);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("pos-123");
        result.SortKey.Should().Be("sk-pos");
        result.Name.Should().Be("Positional Name");
        result.Count.Should().Be(99);
    }

    [Fact]
    public void TestInitOnlyRecordEntity_ToDynamoDb_SerializesCorrectly()
    {
        // Arrange
        var entity = new TestInitOnlyRecordEntity
        {
            Id = "init-123",
            SortKey = "sk-init",
            Description = "Init Only Description",
            IsActive = true,
            Tags = new List<string> { "tag1", "tag2" }
        };

        // Act
        var result = TestInitOnlyRecordEntity.ToDynamoDb(entity);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("pk");
        result["pk"].S.Should().Be("init-123");
        result.Should().ContainKey("sk");
        result["sk"].S.Should().Be("sk-init");
        result.Should().ContainKey("description");
        result["description"].S.Should().Be("Init Only Description");
        result.Should().ContainKey("is_active");
        result["is_active"].BOOL.Should().BeTrue();
        result.Should().ContainKey("tags");
        result["tags"].L.Should().HaveCount(2);
    }

    [Fact]
    public void TestInitOnlyRecordEntity_FromDynamoDb_DeserializesCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "init-123" },
            ["sk"] = new AttributeValue { S = "sk-init" },
            ["description"] = new AttributeValue { S = "Init Only Description" },
            ["is_active"] = new AttributeValue { BOOL = true },
            ["tags"] = new AttributeValue { L = new List<AttributeValue>
            {
                new AttributeValue { S = "tag1" },
                new AttributeValue { S = "tag2" }
            }}
        };

        // Act
        var result = TestInitOnlyRecordEntity.FromDynamoDb<TestInitOnlyRecordEntity>(item);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("init-123");
        result.SortKey.Should().Be("sk-init");
        result.Description.Should().Be("Init Only Description");
        result.IsActive.Should().BeTrue();
        result.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2" });
    }

    [Fact]
    public void TestRecordEntity_Fields_ContainsCorrectAttributeNames()
    {
        // Assert
        TestRecordEntity.Fields.Id.Should().Be("pk");
        TestRecordEntity.Fields.SortKey.Should().Be("sk");
        TestRecordEntity.Fields.Name.Should().Be("name");
        TestRecordEntity.Fields.Value.Should().Be("value");
        TestRecordEntity.Fields.CreatedAt.Should().Be("created_at");
    }
}
