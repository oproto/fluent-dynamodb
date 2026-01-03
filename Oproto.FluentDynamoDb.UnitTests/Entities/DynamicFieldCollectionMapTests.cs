using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Tests for typed Map operations in DynamicFieldCollection.
/// Validates Requirements 4.1-4.5, 5.1-5.5.
/// </summary>
public class DynamicFieldCollectionMapTests
{
    #region GetMap<T> Tests (Requirements 4.1-4.4)

    [Fact]
    public void GetMap_WithValidMapField_ReturnsDeserializedEntity()
    {
        // Arrange - Requirement 4.1: Deserialize Map attribute using T.FromDynamoDb<T>()
        var mapValue = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "1000.50" },
            ["name"] = new AttributeValue { S = "TestChild" },
            ["count"] = new AttributeValue { N = "42" },
            ["isActive"] = new AttributeValue { BOOL = true }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { M = mapValue }
        });

        // Act
        var result = collection.GetMap<TestNestedEntity>("child");

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(1000.50m);
        result.Name.Should().Be("TestChild");
        result.Count.Should().Be(42);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public void GetMap_WithMissingField_ReturnsNull()
    {
        // Arrange - Requirement 4.2: Return null for missing field
        var collection = new DynamicFieldCollection();

        // Act
        var result = collection.GetMap<TestNestedEntity>("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetMap_WithNullField_ReturnsNull()
    {
        // Arrange - Field exists but contains NULL
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { NULL = true }
        });

        // Act
        var result = collection.GetMap<TestNestedEntity>("child");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetMap_WithNonMapField_ThrowsDynamicFieldTypeException()
    {
        // Arrange - Requirement 4.3: Throw DynamicFieldTypeException for non-Map type
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { S = "not a map" }
        });

        // Act
        var act = () => collection.GetMap<TestNestedEntity>("child");

        // Assert
        act.Should().Throw<DynamicFieldTypeException>()
            .Where(e => e.FieldName == "child" && e.RequestedType == typeof(TestNestedEntity));
    }

    [Fact]
    public void GetMap_WithNumberField_ThrowsDynamicFieldTypeException()
    {
        // Arrange - Another non-Map type test
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { N = "123" }
        });

        // Act
        var act = () => collection.GetMap<TestNestedEntity>("child");

        // Assert
        act.Should().Throw<DynamicFieldTypeException>()
            .Where(e => e.FieldName == "child");
    }

    [Fact]
    public void GetMap_WithListField_ThrowsDynamicFieldTypeException()
    {
        // Arrange - List is not a Map
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { L = new List<AttributeValue> { new() { S = "item" } } }
        });

        // Act
        var act = () => collection.GetMap<TestNestedEntity>("child");

        // Assert
        act.Should().Throw<DynamicFieldTypeException>()
            .Where(e => e.FieldName == "child");
    }

    [Fact]
    public void GetMap_WithPartialMapData_DeserializesAvailableFields()
    {
        // Arrange - Map with only some fields populated
        var mapValue = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "500" },
            ["name"] = new AttributeValue { S = "PartialChild" }
            // count and isActive not provided
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { M = mapValue }
        });

        // Act
        var result = collection.GetMap<TestNestedEntity>("child");

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(500m);
        result.Name.Should().Be("PartialChild");
        result.Count.Should().Be(0); // Default value
        result.IsActive.Should().BeFalse(); // Default value
    }

    #endregion

    #region TryGetMap<T> Tests (Requirement 4.5)

    [Fact]
    public void TryGetMap_WithValidMapField_ReturnsTrueAndPopulatesValue()
    {
        // Arrange - Requirement 4.5: Return true and populate value for valid Map
        var mapValue = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "2000" },
            ["name"] = new AttributeValue { S = "TryGetChild" },
            ["count"] = new AttributeValue { N = "10" },
            ["isActive"] = new AttributeValue { BOOL = false }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { M = mapValue }
        });

        // Act
        var result = collection.TryGetMap<TestNestedEntity>("child", out var entity);

        // Assert
        result.Should().BeTrue();
        entity.Should().NotBeNull();
        entity!.Amount.Should().Be(2000m);
        entity.Name.Should().Be("TryGetChild");
        entity.Count.Should().Be(10);
        entity.IsActive.Should().BeFalse();
    }

    [Fact]
    public void TryGetMap_WithMissingField_ReturnsFalse()
    {
        // Arrange - Requirement 4.5: Return false for missing field
        var collection = new DynamicFieldCollection();

        // Act
        var result = collection.TryGetMap<TestNestedEntity>("nonexistent", out var entity);

        // Assert
        result.Should().BeFalse();
        entity.Should().BeNull();
    }

    [Fact]
    public void TryGetMap_WithNullField_ReturnsTrueWithNullValue()
    {
        // Arrange - Field exists but contains NULL
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { NULL = true }
        });

        // Act
        var result = collection.TryGetMap<TestNestedEntity>("child", out var entity);

        // Assert
        result.Should().BeTrue();
        entity.Should().BeNull();
    }

    [Fact]
    public void TryGetMap_WithNonMapField_ReturnsFalse()
    {
        // Arrange - Non-Map type should return false (not throw)
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { S = "not a map" }
        });

        // Act
        var result = collection.TryGetMap<TestNestedEntity>("child", out var entity);

        // Assert
        result.Should().BeFalse();
        entity.Should().BeNull();
    }

    [Fact]
    public void TryGetMap_WithNumberField_ReturnsFalse()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { N = "123" }
        });

        // Act
        var result = collection.TryGetMap<TestNestedEntity>("child", out var entity);

        // Assert
        result.Should().BeFalse();
        entity.Should().BeNull();
    }

    #endregion

    #region SetMap<T> Tests (Requirements 5.1-5.5)

    [Fact]
    public void SetMap_WithEntity_SerializesCorrectly()
    {
        // Arrange - Requirement 5.1: Serialize entity using T.ToDynamoDb<T>()
        var collection = new DynamicFieldCollection();
        var entity = new TestNestedEntity
        {
            Amount = 1500.75m,
            Name = "SetMapChild",
            Count = 25,
            IsActive = true
        };

        // Act
        collection.SetMap("child", entity);

        // Assert - Requirement 5.2: Store as Map AttributeValue
        var raw = collection.GetRaw("child");
        raw.Should().NotBeNull();
        raw!.IsMSet.Should().BeTrue();
        raw.M.Should().ContainKey("amount");
        raw.M.Should().ContainKey("name");
        raw.M.Should().ContainKey("count");
        raw.M.Should().ContainKey("isActive");

        // Verify round-trip
        var retrieved = collection.GetMap<TestNestedEntity>("child");
        retrieved.Should().NotBeNull();
        retrieved!.Amount.Should().Be(1500.75m);
        retrieved.Name.Should().Be("SetMapChild");
        retrieved.Count.Should().Be(25);
        retrieved.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetMap_WithNull_RemovesField()
    {
        // Arrange - Requirement 5.3: Setting null removes the field
        var mapValue = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "100" },
            ["name"] = new AttributeValue { S = "ToBeRemoved" },
            ["count"] = new AttributeValue { N = "1" },
            ["isActive"] = new AttributeValue { BOOL = true }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { M = mapValue }
        });
        collection.ContainsKey("child").Should().BeTrue();

        // Act
        collection.SetMap<TestNestedEntity>("child", null);

        // Assert
        collection.ContainsKey("child").Should().BeFalse();
    }

    [Fact]
    public void SetMap_WithChangeTrackingEnabled_TracksModification()
    {
        // Arrange - Requirement 5.4: Track as added or modified
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        var entity = new TestNestedEntity
        {
            Amount = 500m,
            Name = "TrackedChild",
            Count = 5,
            IsActive = false
        };

        // Act
        collection.SetMap("child", entity);

        // Assert
        collection.HasChanges.Should().BeTrue();
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("child").Should().BeTrue();
    }

    [Fact]
    public void SetMap_OverwritesExistingField()
    {
        // Arrange - Existing field should be overwritten
        var originalMap = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "100" },
            ["name"] = new AttributeValue { S = "Original" },
            ["count"] = new AttributeValue { N = "1" },
            ["isActive"] = new AttributeValue { BOOL = false }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { M = originalMap }
        });
        var newEntity = new TestNestedEntity
        {
            Amount = 999m,
            Name = "Updated",
            Count = 99,
            IsActive = true
        };

        // Act
        collection.SetMap("child", newEntity);

        // Assert
        var retrieved = collection.GetMap<TestNestedEntity>("child");
        retrieved.Should().NotBeNull();
        retrieved!.Amount.Should().Be(999m);
        retrieved.Name.Should().Be("Updated");
        retrieved.Count.Should().Be(99);
        retrieved.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetMap_WithNullAndChangeTracking_TracksRemoval()
    {
        // Arrange - Setting null with change tracking should track removal
        var mapValue = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "100" },
            ["name"] = new AttributeValue { S = "ToRemove" },
            ["count"] = new AttributeValue { N = "1" },
            ["isActive"] = new AttributeValue { BOOL = true }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child"] = new AttributeValue { M = mapValue }
        });
        collection.StartTrackingChanges();

        // Act
        collection.SetMap<TestNestedEntity>("child", null);

        // Assert
        collection.HasChanges.Should().BeTrue();
        collection.RemovedFields.Should().Contain("child");
    }

    [Fact]
    public void SetMap_WithDefaultValues_SerializesAllFields()
    {
        // Arrange - Entity with default values
        var collection = new DynamicFieldCollection();
        var entity = new TestNestedEntity(); // All default values

        // Act
        collection.SetMap("child", entity);

        // Assert
        var raw = collection.GetRaw("child");
        raw.Should().NotBeNull();
        raw!.IsMSet.Should().BeTrue();
        // Default values should still be serialized
        raw.M.Should().ContainKey("amount");
        raw.M.Should().ContainKey("name");
        raw.M.Should().ContainKey("count");
        raw.M.Should().ContainKey("isActive");
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void SetMapThenGetMap_RoundTripsCorrectly()
    {
        // Arrange
        var collection = new DynamicFieldCollection();
        var original = new TestNestedEntity
        {
            Amount = 12345.67m,
            Name = "RoundTripTest",
            Count = 100,
            IsActive = true
        };

        // Act
        collection.SetMap("entity", original);
        var retrieved = collection.GetMap<TestNestedEntity>("entity");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Amount.Should().Be(original.Amount);
        retrieved.Name.Should().Be(original.Name);
        retrieved.Count.Should().Be(original.Count);
        retrieved.IsActive.Should().Be(original.IsActive);
    }

    [Fact]
    public void MultipleSetMapOperations_WorkIndependently()
    {
        // Arrange
        var collection = new DynamicFieldCollection();
        var entity1 = new TestNestedEntity { Amount = 100m, Name = "First", Count = 1, IsActive = true };
        var entity2 = new TestNestedEntity { Amount = 200m, Name = "Second", Count = 2, IsActive = false };
        var entity3 = new TestNestedEntity { Amount = 300m, Name = "Third", Count = 3, IsActive = true };

        // Act
        collection.SetMap("c_001", entity1);
        collection.SetMap("c_002", entity2);
        collection.SetMap("c_003", entity3);

        // Assert
        collection.Count.Should().Be(3);
        
        var r1 = collection.GetMap<TestNestedEntity>("c_001");
        r1.Should().NotBeNull();
        r1!.Name.Should().Be("First");
        
        var r2 = collection.GetMap<TestNestedEntity>("c_002");
        r2.Should().NotBeNull();
        r2!.Name.Should().Be("Second");
        
        var r3 = collection.GetMap<TestNestedEntity>("c_003");
        r3.Should().NotBeNull();
        r3!.Name.Should().Be("Third");
    }

    #endregion
}
