using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Tests for prefix-based operations in DynamicFieldCollection.
/// Validates Requirements 1.1-1.4, 2.1-2.5, 3.1-3.4.
/// </summary>
public class DynamicFieldCollectionPrefixTests
{
    #region GetFieldNamesByPrefix Tests (Requirements 1.1-1.4)

    [Fact]
    public void GetFieldNamesByPrefix_WithMatchingFields_ReturnsMatchingFieldNames()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["c_DEF456"] = new AttributeValue { S = "child2" },
            ["c_GHI789"] = new AttributeValue { S = "child3" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" },
            ["other"] = new AttributeValue { S = "other" }
        });

        // Act
        var result = collection.GetFieldNamesByPrefix("c_").ToList();

        // Assert - Requirement 1.1: Returns field names matching prefix
        result.Should().HaveCount(3);
        result.Should().Contain("c_ABC123");
        result.Should().Contain("c_DEF456");
        result.Should().Contain("c_GHI789");
        // Requirement 1.3: Full attribute names including prefix
        result.Should().NotContain("ABC123");
        result.Should().NotContain("t_TXN001");
        result.Should().NotContain("other");
    }

    [Fact]
    public void GetFieldNamesByPrefix_WithNoMatchingFields_ReturnsEmptyEnumerable()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" }
        });

        // Act
        var result = collection.GetFieldNamesByPrefix("x_").ToList();

        // Assert - Requirement 1.2: Returns empty enumerable for non-matching prefix
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetFieldNamesByPrefix_WithEmptyCollection_ReturnsEmptyEnumerable()
    {
        // Arrange
        var collection = new DynamicFieldCollection();

        // Act
        var result = collection.GetFieldNamesByPrefix("c_").ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetFieldNamesByPrefix_UsesOrdinalComparison()
    {
        // Arrange - Requirement 1.4: Uses ordinal string comparison
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["C_ABC123"] = new AttributeValue { S = "uppercase" },
            ["c_DEF456"] = new AttributeValue { S = "lowercase" }
        });

        // Act
        var result = collection.GetFieldNamesByPrefix("c_").ToList();

        // Assert - Ordinal comparison is case-sensitive
        result.Should().HaveCount(1);
        result.Should().Contain("c_DEF456");
        result.Should().NotContain("C_ABC123");
    }

    [Fact]
    public void GetFieldNamesByPrefix_WithEmptyPrefix_ReturnsAllFields()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" }
        });

        // Act
        var result = collection.GetFieldNamesByPrefix("").ToList();

        // Assert - Empty prefix matches all fields
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetByPrefix Tests (Requirements 2.1-2.2, 2.5)

    [Fact]
    public void GetByPrefix_WithMatchingFields_ReturnsDictionaryWithFullKeys()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["c_DEF456"] = new AttributeValue { S = "child2" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" }
        });

        // Act
        var result = collection.GetByPrefix("c_");

        // Assert - Requirement 2.1: Returns dictionary with matching fields
        result.Should().HaveCount(2);
        // Requirement 2.2: Keys are full attribute names including prefix
        result.Should().ContainKey("c_ABC123");
        result.Should().ContainKey("c_DEF456");
        result["c_ABC123"].S.Should().Be("child1");
        result["c_DEF456"].S.Should().Be("child2");
    }

    [Fact]
    public void GetByPrefix_WithNoMatchingFields_ReturnsEmptyDictionary()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" }
        });

        // Act
        var result = collection.GetByPrefix("x_");

        // Assert - Requirement 2.5: Returns empty dictionary for non-matching prefix
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetByPrefix_WithEmptyCollection_ReturnsEmptyDictionary()
    {
        // Arrange
        var collection = new DynamicFieldCollection();

        // Act
        var result = collection.GetByPrefix("c_");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetByPrefix_ReturnsDictionaryWithOrdinalComparer()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC"] = new AttributeValue { S = "value" }
        });

        // Act
        var result = collection.GetByPrefix("c_");

        // Assert - Result dictionary uses ordinal comparer (case-sensitive)
        result.ContainsKey("c_ABC").Should().BeTrue();
        result.ContainsKey("c_abc").Should().BeFalse();
    }

    #endregion

    #region GetByPrefixWithStrippedKeys Tests (Requirements 2.3-2.5)

    [Fact]
    public void GetByPrefixWithStrippedKeys_WithMatchingFields_ReturnsDictionaryWithStrippedKeys()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["c_DEF456"] = new AttributeValue { S = "child2" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" }
        });

        // Act
        var result = collection.GetByPrefixWithStrippedKeys("c_");

        // Assert - Requirement 2.3: Returns dictionary with prefix stripped from keys
        result.Should().HaveCount(2);
        // Requirement 2.4: "c_ABC123" becomes "ABC123"
        result.Should().ContainKey("ABC123");
        result.Should().ContainKey("DEF456");
        result.Should().NotContainKey("c_ABC123");
        result["ABC123"].S.Should().Be("child1");
        result["DEF456"].S.Should().Be("child2");
    }

    [Fact]
    public void GetByPrefixWithStrippedKeys_WithNoMatchingFields_ReturnsEmptyDictionary()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" }
        });

        // Act
        var result = collection.GetByPrefixWithStrippedKeys("x_");

        // Assert - Requirement 2.5: Returns empty dictionary for non-matching prefix
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetByPrefixWithStrippedKeys_WithEmptyCollection_ReturnsEmptyDictionary()
    {
        // Arrange
        var collection = new DynamicFieldCollection();

        // Act
        var result = collection.GetByPrefixWithStrippedKeys("c_");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetByPrefixWithStrippedKeys_ReturnsDictionaryWithOrdinalComparer()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC"] = new AttributeValue { S = "value" }
        });

        // Act
        var result = collection.GetByPrefixWithStrippedKeys("c_");

        // Assert - Result dictionary uses ordinal comparer (case-sensitive)
        result.ContainsKey("ABC").Should().BeTrue();
        result.ContainsKey("abc").Should().BeFalse();
    }

    [Fact]
    public void GetByPrefixWithStrippedKeys_WithLongerPrefix_StripsCorrectly()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child_ABC123"] = new AttributeValue { S = "child1" },
            ["child_DEF456"] = new AttributeValue { S = "child2" }
        });

        // Act
        var result = collection.GetByPrefixWithStrippedKeys("child_");

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey("ABC123");
        result.Should().ContainKey("DEF456");
    }

    #endregion

    #region RemoveByPrefix Tests (Requirements 3.1-3.4)

    [Fact]
    public void RemoveByPrefix_WithMatchingFields_RemovesFieldsAndReturnsCount()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["c_DEF456"] = new AttributeValue { S = "child2" },
            ["c_GHI789"] = new AttributeValue { S = "child3" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" }
        });

        // Act
        var removedCount = collection.RemoveByPrefix("c_");

        // Assert - Requirement 3.1: Removes all fields matching prefix
        removedCount.Should().Be(3); // Requirement 3.3: Returns count of removed fields
        collection.ContainsKey("c_ABC123").Should().BeFalse();
        collection.ContainsKey("c_DEF456").Should().BeFalse();
        collection.ContainsKey("c_GHI789").Should().BeFalse();
        // Non-matching fields remain
        collection.ContainsKey("t_TXN001").Should().BeTrue();
        collection.Count.Should().Be(1);
    }

    [Fact]
    public void RemoveByPrefix_WithNoMatchingFields_ReturnsZeroAndDoesNotModify()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" }
        });
        var originalCount = collection.Count;

        // Act
        var removedCount = collection.RemoveByPrefix("x_");

        // Assert - Requirement 3.4: Returns 0 and does not modify collection
        removedCount.Should().Be(0);
        collection.Count.Should().Be(originalCount);
    }

    [Fact]
    public void RemoveByPrefix_WithChangeTrackingEnabled_TracksRemovals()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["c_DEF456"] = new AttributeValue { S = "child2" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" }
        });
        collection.StartTrackingChanges();

        // Act
        var removedCount = collection.RemoveByPrefix("c_");

        // Assert - Requirement 3.2: All removed fields are tracked as removed
        removedCount.Should().Be(2);
        collection.HasChanges.Should().BeTrue();
        collection.RemovedFields.Should().Contain("c_ABC123");
        collection.RemovedFields.Should().Contain("c_DEF456");
        collection.RemovedFields.Should().NotContain("t_TXN001");
    }

    [Fact]
    public void RemoveByPrefix_WithChangeTrackingDisabled_DoesNotTrackRemovals()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["c_DEF456"] = new AttributeValue { S = "child2" }
        });
        // Note: Not calling StartTrackingChanges()

        // Act
        var removedCount = collection.RemoveByPrefix("c_");

        // Assert
        removedCount.Should().Be(2);
        collection.HasChanges.Should().BeFalse();
        collection.RemovedFields.Should().BeEmpty();
    }

    [Fact]
    public void RemoveByPrefix_WithEmptyCollection_ReturnsZero()
    {
        // Arrange
        var collection = new DynamicFieldCollection();

        // Act
        var removedCount = collection.RemoveByPrefix("c_");

        // Assert
        removedCount.Should().Be(0);
    }

    [Fact]
    public void RemoveByPrefix_UsesOrdinalComparison()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["C_ABC123"] = new AttributeValue { S = "uppercase" },
            ["c_DEF456"] = new AttributeValue { S = "lowercase" }
        });

        // Act
        var removedCount = collection.RemoveByPrefix("c_");

        // Assert - Ordinal comparison is case-sensitive
        removedCount.Should().Be(1);
        collection.ContainsKey("C_ABC123").Should().BeTrue();
        collection.ContainsKey("c_DEF456").Should().BeFalse();
    }

    [Fact]
    public void RemoveByPrefix_WithChangesOnly_GeneratesCorrectRemovalSet()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC123"] = new AttributeValue { S = "child1" },
            ["c_DEF456"] = new AttributeValue { S = "child2" },
            ["t_TXN001"] = new AttributeValue { S = "txn1" }
        });
        collection.StartTrackingChanges();

        // Act
        collection.RemoveByPrefix("c_");
        var changes = collection.ChangesOnly(resetTracking: false);

        // Assert - ChangesOnly should reflect the removals
        changes.RemovedFields.Should().Contain("c_ABC123");
        changes.RemovedFields.Should().Contain("c_DEF456");
    }

    #endregion

    #region Mixed Prefix Operations Tests

    [Fact]
    public void PrefixOperations_WithMultiplePrefixes_WorkIndependently()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["c_ABC"] = new AttributeValue { S = "child1" },
            ["c_DEF"] = new AttributeValue { S = "child2" },
            ["t_TXN1"] = new AttributeValue { S = "txn1" },
            ["t_TXN2"] = new AttributeValue { S = "txn2" },
            ["m_META"] = new AttributeValue { S = "meta" }
        });

        // Act & Assert - GetFieldNamesByPrefix
        collection.GetFieldNamesByPrefix("c_").Should().HaveCount(2);
        collection.GetFieldNamesByPrefix("t_").Should().HaveCount(2);
        collection.GetFieldNamesByPrefix("m_").Should().HaveCount(1);

        // Act & Assert - GetByPrefix
        collection.GetByPrefix("c_").Should().HaveCount(2);
        collection.GetByPrefix("t_").Should().HaveCount(2);

        // Act & Assert - GetByPrefixWithStrippedKeys
        var children = collection.GetByPrefixWithStrippedKeys("c_");
        children.Should().ContainKey("ABC");
        children.Should().ContainKey("DEF");

        var transactions = collection.GetByPrefixWithStrippedKeys("t_");
        transactions.Should().ContainKey("TXN1");
        transactions.Should().ContainKey("TXN2");
    }

    [Fact]
    public void PrefixOperations_WithOverlappingPrefixes_MatchLongestFirst()
    {
        // Arrange
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["child_ABC"] = new AttributeValue { S = "child1" },
            ["child_ref_DEF"] = new AttributeValue { S = "childref1" },
            ["c_GHI"] = new AttributeValue { S = "short" }
        });

        // Act & Assert - Shorter prefix matches more
        collection.GetFieldNamesByPrefix("c").Should().HaveCount(3);
        collection.GetFieldNamesByPrefix("child_").Should().HaveCount(2);
        collection.GetFieldNamesByPrefix("child_ref_").Should().HaveCount(1);
    }

    #endregion
}
