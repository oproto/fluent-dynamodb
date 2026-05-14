using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Tests for bulk operations in DynamicFieldCollection.
/// Validates Requirements 6.1-6.6, 7.1-7.4, 8.1-8.5.
/// </summary>
public class DynamicFieldCollectionBulkTests
{
    #region SetMany Tests (Requirements 6.1-6.2, 6.6)

    [Fact]
    public void SetMany_WithFields_AddsAllFields()
    {
        // Arrange - Requirement 6.1: Add or update all fields
        var collection = new DynamicFieldCollection();
        var fields = new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { N = "123" },
            ["field3"] = new AttributeValue { BOOL = true }
        };

        // Act
        collection.SetMany(fields);

        // Assert
        collection.Count.Should().Be(3);
        collection.GetString("field1").Should().Be("value1");
        collection.GetInt("field2").Should().Be(123);
        collection.GetBool("field3").Should().BeTrue();
    }

    [Fact]
    public void SetMany_WithChangeTrackingEnabled_TracksAllModifications()
    {
        // Arrange - Requirement 6.2: Track all fields as added or modified
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        var fields = new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" },
            ["field3"] = new AttributeValue { S = "value3" }
        };

        // Act
        collection.SetMany(fields);

        // Assert
        collection.HasChanges.Should().BeTrue();
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("field1").Should().BeTrue();
        changes.ContainsKey("field2").Should().BeTrue();
        changes.ContainsKey("field3").Should().BeTrue();
    }

    [Fact]
    public void SetMany_WithNullDictionary_DoesNotModifyCollection()
    {
        // Arrange - Requirement 6.6: Handle null gracefully
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["existing"] = new AttributeValue { S = "value" }
        });
        var originalCount = collection.Count;

        // Act
        collection.SetMany(null);

        // Assert
        collection.Count.Should().Be(originalCount);
    }

    [Fact]
    public void SetMany_WithEmptyDictionary_DoesNotModifyCollection()
    {
        // Arrange - Requirement 6.6: Handle empty gracefully
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["existing"] = new AttributeValue { S = "value" }
        });
        var originalCount = collection.Count;

        // Act
        collection.SetMany(new Dictionary<string, AttributeValue>());

        // Assert
        collection.Count.Should().Be(originalCount);
    }

    [Fact]
    public void SetMany_OverwritesExistingFields()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "original" }
        });
        var fields = new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "updated" },
            ["field2"] = new AttributeValue { S = "new" }
        };

        // Act
        collection.SetMany(fields);

        // Assert
        collection.Count.Should().Be(2);
        collection.GetString("field1").Should().Be("updated");
        collection.GetString("field2").Should().Be("new");
    }

    #endregion

    #region SetManyWithPrefix Tests (Requirements 6.3-6.4, 6.6)

    [Fact]
    public void SetManyWithPrefix_PrependsPrefix()
    {
        // Arrange - Requirement 6.3: Prepend prefix to each key
        var collection = new DynamicFieldCollection();
        var fields = new Dictionary<string, AttributeValue>
        {
            ["ABC123"] = new AttributeValue { S = "value1" },
            ["DEF456"] = new AttributeValue { S = "value2" }
        };

        // Act
        collection.SetManyWithPrefix("c_", fields);

        // Assert - Requirement 6.4: "ABC123" becomes "c_ABC123"
        collection.Count.Should().Be(2);
        collection.ContainsKey("c_ABC123").Should().BeTrue();
        collection.ContainsKey("c_DEF456").Should().BeTrue();
        collection.ContainsKey("ABC123").Should().BeFalse();
        collection.GetString("c_ABC123").Should().Be("value1");
        collection.GetString("c_DEF456").Should().Be("value2");
    }

    [Fact]
    public void SetManyWithPrefix_WithChangeTrackingEnabled_TracksAllModifications()
    {
        // Arrange
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        var fields = new Dictionary<string, AttributeValue>
        {
            ["TXN001"] = new AttributeValue { S = "C1000" },
            ["TXN002"] = new AttributeValue { S = "D500" }
        };

        // Act
        collection.SetManyWithPrefix("t_", fields);

        // Assert
        collection.HasChanges.Should().BeTrue();
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("t_TXN001").Should().BeTrue();
        changes.ContainsKey("t_TXN002").Should().BeTrue();
    }

    [Fact]
    public void SetManyWithPrefix_WithNullDictionary_DoesNotModifyCollection()
    {
        // Arrange - Requirement 6.6: Handle null gracefully
        var collection = new DynamicFieldCollection();

        // Act
        collection.SetManyWithPrefix("c_", null);

        // Assert
        collection.Count.Should().Be(0);
    }

    [Fact]
    public void SetManyWithPrefix_WithEmptyDictionary_DoesNotModifyCollection()
    {
        // Arrange - Requirement 6.6: Handle empty gracefully
        var collection = new DynamicFieldCollection();

        // Act
        collection.SetManyWithPrefix("c_", new Dictionary<string, AttributeValue>());

        // Assert
        collection.Count.Should().Be(0);
    }

    [Fact]
    public void SetManyWithPrefix_WithLongerPrefix_PrependsCorrectly()
    {
        // Arrange
        var collection = new DynamicFieldCollection();
        var fields = new Dictionary<string, AttributeValue>
        {
            ["001"] = new AttributeValue { S = "value1" }
        };

        // Act
        collection.SetManyWithPrefix("child_ref_", fields);

        // Assert
        collection.ContainsKey("child_ref_001").Should().BeTrue();
        collection.GetString("child_ref_001").Should().Be("value1");
    }

    #endregion

    #region SetMapsWithPrefix<T> Tests (Requirements 6.5-6.6)

    [Fact]
    public void SetMapsWithPrefix_SerializesAllEntitiesWithPrefix()
    {
        // Arrange - Requirement 6.5: Serialize each entity and store with prefixed key
        var collection = new DynamicFieldCollection();
        var entities = new Dictionary<string, TestNestedEntity>
        {
            ["ABC123"] = new TestNestedEntity { Amount = 1000m, Name = "Child1", Count = 10, IsActive = true },
            ["DEF456"] = new TestNestedEntity { Amount = 2000m, Name = "Child2", Count = 20, IsActive = false }
        };

        // Act
        collection.SetMapsWithPrefix("c_", entities);

        // Assert
        collection.Count.Should().Be(2);
        collection.ContainsKey("c_ABC123").Should().BeTrue();
        collection.ContainsKey("c_DEF456").Should().BeTrue();

        // Verify serialization
        var child1 = collection.GetMap<TestNestedEntity>("c_ABC123");
        child1.Should().NotBeNull();
        child1!.Amount.Should().Be(1000m);
        child1.Name.Should().Be("Child1");
        child1.Count.Should().Be(10);
        child1.IsActive.Should().BeTrue();

        var child2 = collection.GetMap<TestNestedEntity>("c_DEF456");
        child2.Should().NotBeNull();
        child2!.Amount.Should().Be(2000m);
        child2.Name.Should().Be("Child2");
    }

    [Fact]
    public void SetMapsWithPrefix_WithChangeTrackingEnabled_TracksAllModifications()
    {
        // Arrange
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        var entities = new Dictionary<string, TestNestedEntity>
        {
            ["ABC123"] = new TestNestedEntity { Amount = 100m, Name = "Test", Count = 1, IsActive = true }
        };

        // Act
        collection.SetMapsWithPrefix("c_", entities);

        // Assert
        collection.HasChanges.Should().BeTrue();
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("c_ABC123").Should().BeTrue();
    }

    [Fact]
    public void SetMapsWithPrefix_WithNullDictionary_DoesNotModifyCollection()
    {
        // Arrange - Requirement 6.6: Handle null gracefully
        var collection = new DynamicFieldCollection();

        // Act
        collection.SetMapsWithPrefix<TestNestedEntity>("c_", null);

        // Assert
        collection.Count.Should().Be(0);
    }

    [Fact]
    public void SetMapsWithPrefix_WithEmptyDictionary_DoesNotModifyCollection()
    {
        // Arrange - Requirement 6.6: Handle empty gracefully
        var collection = new DynamicFieldCollection();

        // Act
        collection.SetMapsWithPrefix("c_", new Dictionary<string, TestNestedEntity>());

        // Assert
        collection.Count.Should().Be(0);
    }

    [Fact]
    public void SetMapsWithPrefix_OverwritesExistingFields()
    {
        // Arrange
        var originalMap = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "100" },
            ["name"] = new AttributeValue { S = "Original" },
            ["count"] = new AttributeValue { N = "1" },
            ["isActive"] = new AttributeValue { BOOL = false }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { M = originalMap }
        });
        var entities = new Dictionary<string, TestNestedEntity>
        {
            ["ABC123"] = new TestNestedEntity { Amount = 999m, Name = "Updated", Count = 99, IsActive = true }
        };

        // Act
        collection.SetMapsWithPrefix("c_", entities);

        // Assert
        var updated = collection.GetMap<TestNestedEntity>("c_ABC123");
        updated.Should().NotBeNull();
        updated!.Amount.Should().Be(999m);
        updated.Name.Should().Be("Updated");
        updated.Count.Should().Be(99);
        updated.IsActive.Should().BeTrue();
    }

    #endregion

    #region RemoveMany Tests (Requirements 7.1-7.4)

    [Fact]
    public void RemoveMany_RemovesSpecifiedFieldsAndReturnsCount()
    {
        // Arrange - Requirement 7.1: Remove all specified fields
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" },
            ["field3"] = new AttributeValue { S = "value3" },
            ["field4"] = new AttributeValue { S = "value4" }
        });

        // Act
        var removedCount = collection.RemoveMany(new[] { "field1", "field3" });

        // Assert - Requirement 7.3: Returns count of fields actually removed
        removedCount.Should().Be(2);
        collection.Count.Should().Be(2);
        collection.ContainsKey("field1").Should().BeFalse();
        collection.ContainsKey("field2").Should().BeTrue();
        collection.ContainsKey("field3").Should().BeFalse();
        collection.ContainsKey("field4").Should().BeTrue();
    }

    [Fact]
    public void RemoveMany_WithChangeTrackingEnabled_TracksAllRemovals()
    {
        // Arrange - Requirement 7.2: Track all removed fields
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" },
            ["field3"] = new AttributeValue { S = "value3" }
        });
        collection.StartTrackingChanges();

        // Act
        collection.RemoveMany(new[] { "field1", "field3" });

        // Assert
        collection.HasChanges.Should().BeTrue();
        collection.RemovedFields.Should().Contain("field1");
        collection.RemovedFields.Should().Contain("field3");
        collection.RemovedFields.Should().NotContain("field2");
    }

    [Fact]
    public void RemoveMany_IgnoresNonExistentFields()
    {
        // Arrange - Requirement 7.4: Non-existent names are ignored
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" }
        });

        // Act
        var removedCount = collection.RemoveMany(new[] { "field1", "nonexistent1", "nonexistent2" });

        // Assert
        removedCount.Should().Be(1); // Only field1 was actually removed
        collection.Count.Should().Be(1);
        collection.ContainsKey("field2").Should().BeTrue();
    }

    [Fact]
    public void RemoveMany_WithNullEnumerable_ReturnsZero()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" }
        });

        // Act
        var removedCount = collection.RemoveMany(null);

        // Assert
        removedCount.Should().Be(0);
        collection.Count.Should().Be(1);
    }

    [Fact]
    public void RemoveMany_WithEmptyEnumerable_ReturnsZero()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" }
        });

        // Act
        var removedCount = collection.RemoveMany(Array.Empty<string>());

        // Assert
        removedCount.Should().Be(0);
        collection.Count.Should().Be(1);
    }

    [Fact]
    public void RemoveMany_WithAllNonExistentFields_ReturnsZero()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" }
        });

        // Act
        var removedCount = collection.RemoveMany(new[] { "nonexistent1", "nonexistent2" });

        // Assert
        removedCount.Should().Be(0);
        collection.Count.Should().Be(1);
    }

    #endregion

    #region GetMapsByPrefix<T> Tests (Requirements 8.1-8.2, 8.4-8.5)

    [Fact]
    public void GetMapsByPrefix_ReturnsTypedEntitiesWithFullKeys()
    {
        // Arrange - Requirement 8.1: Return Dictionary<string, T> with Map fields
        var map1 = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "1000" },
            ["name"] = new AttributeValue { S = "Child1" },
            ["count"] = new AttributeValue { N = "10" },
            ["isActive"] = new AttributeValue { BOOL = true }
        };
        var map2 = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "2000" },
            ["name"] = new AttributeValue { S = "Child2" },
            ["count"] = new AttributeValue { N = "20" },
            ["isActive"] = new AttributeValue { BOOL = false }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { M = map1 },
            ["c_DEF456"] = new AttributeValue { M = map2 },
            ["t_TXN001"] = new AttributeValue { S = "transaction" }
        });

        // Act
        var result = collection.GetMapsByPrefix<TestNestedEntity>("c_");

        // Assert - Requirement 8.2: Keys are full attribute names
        result.Should().HaveCount(2);
        result.Should().ContainKey("c_ABC123");
        result.Should().ContainKey("c_DEF456");
        result["c_ABC123"].Amount.Should().Be(1000m);
        result["c_ABC123"].Name.Should().Be("Child1");
        result["c_DEF456"].Amount.Should().Be(2000m);
        result["c_DEF456"].Name.Should().Be("Child2");
    }

    [Fact]
    public void GetMapsByPrefix_SkipsNonMapFields()
    {
        // Arrange - Requirement 8.4: Skip non-Map fields (don't throw)
        var map1 = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "1000" },
            ["name"] = new AttributeValue { S = "Child1" },
            ["count"] = new AttributeValue { N = "10" },
            ["isActive"] = new AttributeValue { BOOL = true }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { M = map1 },
            ["c_STRING"] = new AttributeValue { S = "not a map" },
            ["c_NUMBER"] = new AttributeValue { N = "123" },
            ["c_BOOL"] = new AttributeValue { BOOL = true }
        });

        // Act
        var result = collection.GetMapsByPrefix<TestNestedEntity>("c_");

        // Assert - Only Map fields are returned
        result.Should().HaveCount(1);
        result.Should().ContainKey("c_ABC123");
        result.Should().NotContainKey("c_STRING");
        result.Should().NotContainKey("c_NUMBER");
        result.Should().NotContainKey("c_BOOL");
    }

    [Fact]
    public void GetMapsByPrefix_WithNoMatchingFields_ReturnsEmptyDictionary()
    {
        // Arrange - Requirement 8.5: Return empty dictionary for non-matching prefix
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "value" }
        });

        // Act
        var result = collection.GetMapsByPrefix<TestNestedEntity>("x_");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetMapsByPrefix_WithEmptyCollection_ReturnsEmptyDictionary()
    {
        // Arrange
        var collection = new DynamicFieldCollection();

        // Act
        var result = collection.GetMapsByPrefix<TestNestedEntity>("c_");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetMapsByPrefixWithStrippedKeys<T> Tests (Requirements 8.3-8.5)

    [Fact]
    public void GetMapsByPrefixWithStrippedKeys_StripsPrefixFromKeys()
    {
        // Arrange - Requirement 8.3: Strip prefix from keys
        var map1 = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "1000" },
            ["name"] = new AttributeValue { S = "Child1" },
            ["count"] = new AttributeValue { N = "10" },
            ["isActive"] = new AttributeValue { BOOL = true }
        };
        var map2 = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "2000" },
            ["name"] = new AttributeValue { S = "Child2" },
            ["count"] = new AttributeValue { N = "20" },
            ["isActive"] = new AttributeValue { BOOL = false }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { M = map1 },
            ["c_DEF456"] = new AttributeValue { M = map2 }
        });

        // Act
        var result = collection.GetMapsByPrefixWithStrippedKeys<TestNestedEntity>("c_");

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey("ABC123");
        result.Should().ContainKey("DEF456");
        result.Should().NotContainKey("c_ABC123");
        result.Should().NotContainKey("c_DEF456");
        result["ABC123"].Amount.Should().Be(1000m);
        result["DEF456"].Amount.Should().Be(2000m);
    }

    [Fact]
    public void GetMapsByPrefixWithStrippedKeys_SkipsNonMapFields()
    {
        // Arrange - Requirement 8.4: Skip non-Map fields
        var map1 = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "1000" },
            ["name"] = new AttributeValue { S = "Child1" },
            ["count"] = new AttributeValue { N = "10" },
            ["isActive"] = new AttributeValue { BOOL = true }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { M = map1 },
            ["c_STRING"] = new AttributeValue { S = "not a map" }
        });

        // Act
        var result = collection.GetMapsByPrefixWithStrippedKeys<TestNestedEntity>("c_");

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("ABC123");
        result.Should().NotContainKey("STRING");
    }

    [Fact]
    public void GetMapsByPrefixWithStrippedKeys_WithNoMatchingFields_ReturnsEmptyDictionary()
    {
        // Arrange - Requirement 8.5: Return empty dictionary
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "value" }
        });

        // Act
        var result = collection.GetMapsByPrefixWithStrippedKeys<TestNestedEntity>("x_");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetMapsByPrefixWithStrippedKeys_WithLongerPrefix_StripsCorrectly()
    {
        // Arrange
        var map1 = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new AttributeValue { N = "500" },
            ["name"] = new AttributeValue { S = "Test" },
            ["count"] = new AttributeValue { N = "5" },
            ["isActive"] = new AttributeValue { BOOL = true }
        };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child_ref_ABC123"] = new AttributeValue { M = map1 }
        });

        // Act
        var result = collection.GetMapsByPrefixWithStrippedKeys<TestNestedEntity>("child_ref_");

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("ABC123");
    }

    #endregion

    #region Round-Trip Tests for Bulk Operations

    [Fact]
    public void SetMapsWithPrefix_ThenGetMapsByPrefix_RoundTripsCorrectly()
    {
        // Arrange
        var collection = new DynamicFieldCollection();
        var entities = new Dictionary<string, TestNestedEntity>
        {
            ["ABC123"] = new TestNestedEntity { Amount = 1000m, Name = "Child1", Count = 10, IsActive = true },
            ["DEF456"] = new TestNestedEntity { Amount = 2000m, Name = "Child2", Count = 20, IsActive = false },
            ["GHI789"] = new TestNestedEntity { Amount = 3000m, Name = "Child3", Count = 30, IsActive = true }
        };

        // Act
        collection.SetMapsWithPrefix("c_", entities);
        var retrieved = collection.GetMapsByPrefixWithStrippedKeys<TestNestedEntity>("c_");

        // Assert
        retrieved.Should().HaveCount(3);
        retrieved["ABC123"].Amount.Should().Be(1000m);
        retrieved["ABC123"].Name.Should().Be("Child1");
        retrieved["DEF456"].Amount.Should().Be(2000m);
        retrieved["DEF456"].Name.Should().Be("Child2");
        retrieved["GHI789"].Amount.Should().Be(3000m);
        retrieved["GHI789"].Name.Should().Be("Child3");
    }

    [Fact]
    public void BulkOperations_WithMixedSetAndRemove_TracksAllChanges()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_EXISTING1"] = new AttributeValue { S = "existing1" },
            ["c_EXISTING2"] = new AttributeValue { S = "existing2" }
        });
        collection.StartTrackingChanges();

        // Act - Add new fields and remove existing ones
        var newFields = new Dictionary<string, AttributeValue>
        {
            ["NEW1"] = new AttributeValue { S = "new1" },
            ["NEW2"] = new AttributeValue { S = "new2" }
        };
        collection.SetManyWithPrefix("c_", newFields);
        collection.RemoveMany(new[] { "c_EXISTING1" });

        // Assert
        collection.HasChanges.Should().BeTrue();
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("c_NEW1").Should().BeTrue();
        changes.ContainsKey("c_NEW2").Should().BeTrue();
        collection.RemovedFields.Should().Contain("c_EXISTING1");
    }

    #endregion
}
