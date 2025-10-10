using FluentAssertions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class AttributeNameInternalTests
{
    private readonly AttributeNameInternal _helper = new();

    #region WithAttributes Dictionary Tests

    [Fact]
    public void WithAttributes_Dictionary_ShouldAddAllAttributes()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "#pk", "partitionKey" },
            { "#sk", "sortKey" },
            { "#name", "name" }
        };

        // Act
        _helper.WithAttributes(attributes);

        // Assert
        _helper.AttributeNames.Should().HaveCount(3);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
        _helper.AttributeNames["#sk"].Should().Be("sortKey");
        _helper.AttributeNames["#name"].Should().Be("name");
    }

    [Fact]
    public void WithAttributes_EmptyDictionary_ShouldNotAddAttributes()
    {
        // Arrange
        var attributes = new Dictionary<string, string>();

        // Act
        _helper.WithAttributes(attributes);

        // Assert
        _helper.AttributeNames.Should().BeEmpty();
    }

    [Fact]
    public void WithAttributes_DictionaryWithReservedWords_ShouldAddCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "#order", "order" },
            { "#size", "size" },
            { "#count", "count" },
            { "#status", "status" }
        };

        // Act
        _helper.WithAttributes(attributes);

        // Assert
        _helper.AttributeNames.Should().HaveCount(4);
        _helper.AttributeNames["#order"].Should().Be("order");
        _helper.AttributeNames["#size"].Should().Be("size");
        _helper.AttributeNames["#count"].Should().Be("count");
        _helper.AttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributes_DictionaryWithEmptyValues_ShouldAddEmptyValues()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "#param1", "" },
            { "#param2", "actualName" }
        };

        // Act
        _helper.WithAttributes(attributes);

        // Assert
        _helper.AttributeNames.Should().HaveCount(2);
        _helper.AttributeNames["#param1"].Should().Be("");
        _helper.AttributeNames["#param2"].Should().Be("actualName");
    }

    [Fact]
    public void WithAttributes_DictionaryWithEmptyKeys_ShouldAddEmptyKeys()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "", "actualName" },
            { "#param", "anotherName" }
        };

        // Act
        _helper.WithAttributes(attributes);

        // Assert
        _helper.AttributeNames.Should().HaveCount(2);
        _helper.AttributeNames[""].Should().Be("actualName");
        _helper.AttributeNames["#param"].Should().Be("anotherName");
    }

    #endregion

    #region WithAttributes Action Tests

    [Fact]
    public void WithAttributes_Action_ShouldConfigureAttributes()
    {
        // Act
        _helper.WithAttributes(attributes =>
        {
            attributes.Add("#pk", "partitionKey");
            attributes.Add("#status", "status");
        });

        // Assert
        _helper.AttributeNames.Should().HaveCount(2);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
        _helper.AttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributes_Action_EmptyAction_ShouldNotAddAttributes()
    {
        // Act
        _helper.WithAttributes(attributes => { });

        // Assert
        _helper.AttributeNames.Should().BeEmpty();
    }

    [Fact]
    public void WithAttributes_Action_MultipleAdditions_ShouldAddAllAttributes()
    {
        // Act
        _helper.WithAttributes(attributes =>
        {
            attributes.Add("#pk", "partitionKey");
            attributes.Add("#sk", "sortKey");
            attributes.Add("#name", "name");
            attributes.Add("#order", "order");
        });

        // Assert
        _helper.AttributeNames.Should().HaveCount(4);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
        _helper.AttributeNames["#sk"].Should().Be("sortKey");
        _helper.AttributeNames["#name"].Should().Be("name");
        _helper.AttributeNames["#order"].Should().Be("order");
    }

    [Fact]
    public void WithAttributes_Action_ConditionalAdditions_ShouldAddOnlyWhenConditionMet()
    {
        // Act
        _helper.WithAttributes(attributes =>
        {
            attributes.Add("#pk", "partitionKey");
            
            var shouldAddSortKey = true;
            if (shouldAddSortKey)
            {
                attributes.Add("#sk", "sortKey");
            }
            
            var shouldAddName = false;
            if (shouldAddName)
            {
                attributes.Add("#name", "name");
            }
        });

        // Assert
        _helper.AttributeNames.Should().HaveCount(2);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
        _helper.AttributeNames["#sk"].Should().Be("sortKey");
        _helper.AttributeNames.Should().NotContainKey("#name");
    }

    #endregion

    #region WithAttribute Single Tests

    [Fact]
    public void WithAttribute_ShouldAddSingleAttribute()
    {
        // Act
        _helper.WithAttribute("#pk", "partitionKey");

        // Assert
        _helper.AttributeNames.Should().HaveCount(1);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
    }

    [Fact]
    public void WithAttribute_MultipleCalls_ShouldAddAllAttributes()
    {
        // Act
        _helper.WithAttribute("#pk", "partitionKey");
        _helper.WithAttribute("#sk", "sortKey");
        _helper.WithAttribute("#name", "name");

        // Assert
        _helper.AttributeNames.Should().HaveCount(3);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
        _helper.AttributeNames["#sk"].Should().Be("sortKey");
        _helper.AttributeNames["#name"].Should().Be("name");
    }

    [Fact]
    public void WithAttribute_ReservedWords_ShouldHandleCorrectly()
    {
        // Act
        _helper.WithAttribute("#order", "order");
        _helper.WithAttribute("#size", "size");
        _helper.WithAttribute("#count", "count");

        // Assert
        _helper.AttributeNames.Should().HaveCount(3);
        _helper.AttributeNames["#order"].Should().Be("order");
        _helper.AttributeNames["#size"].Should().Be("size");
        _helper.AttributeNames["#count"].Should().Be("count");
    }

    [Fact]
    public void WithAttribute_EmptyParameterName_ShouldAddAttribute()
    {
        // Act
        _helper.WithAttribute("", "actualName");

        // Assert
        _helper.AttributeNames.Should().HaveCount(1);
        _helper.AttributeNames[""].Should().Be("actualName");
    }

    [Fact]
    public void WithAttribute_EmptyAttributeName_ShouldAddAttribute()
    {
        // Act
        _helper.WithAttribute("#param", "");

        // Assert
        _helper.AttributeNames.Should().HaveCount(1);
        _helper.AttributeNames["#param"].Should().Be("");
    }

    [Fact]
    public void WithAttribute_BothEmpty_ShouldAddAttribute()
    {
        // Act
        _helper.WithAttribute("", "");

        // Assert
        _helper.AttributeNames.Should().HaveCount(1);
        _helper.AttributeNames[""].Should().Be("");
    }

    #endregion

    #region Mixed Usage Tests

    [Fact]
    public void MixedUsage_WithAttributesAndWithAttribute_ShouldCombineCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "#pk", "partitionKey" },
            { "#sk", "sortKey" }
        };

        // Act
        _helper.WithAttributes(attributes);
        _helper.WithAttribute("#name", "name");
        _helper.WithAttribute("#status", "status");

        // Assert
        _helper.AttributeNames.Should().HaveCount(4);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
        _helper.AttributeNames["#sk"].Should().Be("sortKey");
        _helper.AttributeNames["#name"].Should().Be("name");
        _helper.AttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void MixedUsage_WithAttributesActionAndDictionary_ShouldCombineCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "#pk", "partitionKey" }
        };

        // Act
        _helper.WithAttributes(attributes);
        _helper.WithAttributes(attrs =>
        {
            attrs.Add("#sk", "sortKey");
            attrs.Add("#name", "name");
        });

        // Assert
        _helper.AttributeNames.Should().HaveCount(3);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
        _helper.AttributeNames["#sk"].Should().Be("sortKey");
        _helper.AttributeNames["#name"].Should().Be("name");
    }

    [Fact]
    public void MixedUsage_AllMethods_ShouldCombineCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "#pk", "partitionKey" }
        };

        // Act
        _helper.WithAttributes(attributes);
        _helper.WithAttributes(attrs => attrs.Add("#sk", "sortKey"));
        _helper.WithAttribute("#name", "name");

        // Assert
        _helper.AttributeNames.Should().HaveCount(3);
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
        _helper.AttributeNames["#sk"].Should().Be("sortKey");
        _helper.AttributeNames["#name"].Should().Be("name");
    }

    #endregion

    #region Error Conditions Tests

    [Fact]
    public void WithAttributes_DuplicateKeys_ShouldThrowArgumentException()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            { "#pk", "partitionKey" }
        };
        _helper.WithAttributes(attributes);

        var duplicateAttributes = new Dictionary<string, string>
        {
            { "#pk", "anotherKey" }
        };

        // Act & Assert
        var action = () => _helper.WithAttributes(duplicateAttributes);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithAttribute_DuplicateKey_ShouldThrowArgumentException()
    {
        // Arrange
        _helper.WithAttribute("#pk", "partitionKey");

        // Act & Assert
        var action = () => _helper.WithAttribute("#pk", "anotherKey");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithAttributes_Action_DuplicateKeysInAction_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => _helper.WithAttributes(attributes =>
        {
            attributes.Add("#pk", "partitionKey");
            attributes.Add("#pk", "anotherKey"); // This should throw
        });
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region AttributeNames Property Tests

    [Fact]
    public void AttributeNames_InitialState_ShouldBeEmpty()
    {
        // Arrange
        var helper = new AttributeNameInternal();

        // Assert
        helper.AttributeNames.Should().NotBeNull();
        helper.AttributeNames.Should().BeEmpty();
    }

    [Fact]
    public void AttributeNames_AfterAdditions_ShouldReflectChanges()
    {
        // Act
        _helper.WithAttribute("#pk", "partitionKey");

        // Assert
        _helper.AttributeNames.Should().HaveCount(1);
        _helper.AttributeNames.Should().ContainKey("#pk");
        _helper.AttributeNames["#pk"].Should().Be("partitionKey");
    }

    [Fact]
    public void AttributeNames_DirectModification_ShouldBeReflected()
    {
        // Act - Direct modification (though not recommended)
        _helper.AttributeNames.Add("#direct", "directValue");

        // Assert
        _helper.AttributeNames.Should().HaveCount(1);
        _helper.AttributeNames["#direct"].Should().Be("directValue");
    }

    #endregion
}