using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.Extensions;

public class WithAttributeNamesExtensionsTests
{
    private readonly TestBuilder _builder = new();

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
        var result = _builder.WithAttributes(attributes);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().HaveCount(3);
        _builder.AttributeNameHelper.AttributeNames["#pk"].Should().Be("partitionKey");
        _builder.AttributeNameHelper.AttributeNames["#sk"].Should().Be("sortKey");
        _builder.AttributeNameHelper.AttributeNames["#name"].Should().Be("name");
    }

    [Fact]
    public void WithAttributes_EmptyDictionary_ShouldNotAddAttributes()
    {
        // Arrange
        var attributes = new Dictionary<string, string>();

        // Act
        var result = _builder.WithAttributes(attributes);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().BeEmpty();
    }

    [Fact]
    public void WithAttributes_Action_ShouldConfigureAttributes()
    {
        // Act
        var result = _builder.WithAttributes(attributes =>
        {
            attributes.Add("#pk", "partitionKey");
            attributes.Add("#status", "status");
        });

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().HaveCount(2);
        _builder.AttributeNameHelper.AttributeNames["#pk"].Should().Be("partitionKey");
        _builder.AttributeNameHelper.AttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributes_Action_EmptyAction_ShouldNotAddAttributes()
    {
        // Act
        var result = _builder.WithAttributes(attributes => { });

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().BeEmpty();
    }

    [Fact]
    public void WithAttribute_ShouldAddSingleAttribute()
    {
        // Act
        var result = _builder.WithAttribute("#pk", "partitionKey");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().HaveCount(1);
        _builder.AttributeNameHelper.AttributeNames["#pk"].Should().Be("partitionKey");
    }

    [Fact]
    public void WithAttribute_MultipleCallsChained_ShouldAddAllAttributes()
    {
        // Act
        var result = _builder
            .WithAttribute("#pk", "partitionKey")
            .WithAttribute("#sk", "sortKey")
            .WithAttribute("#name", "name");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().HaveCount(3);
        _builder.AttributeNameHelper.AttributeNames["#pk"].Should().Be("partitionKey");
        _builder.AttributeNameHelper.AttributeNames["#sk"].Should().Be("sortKey");
        _builder.AttributeNameHelper.AttributeNames["#name"].Should().Be("name");
    }

    [Fact]
    public void WithAttribute_ReservedWords_ShouldHandleCorrectly()
    {
        // Act
        var result = _builder
            .WithAttribute("#order", "order")
            .WithAttribute("#size", "size")
            .WithAttribute("#count", "count");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().HaveCount(3);
        _builder.AttributeNameHelper.AttributeNames["#order"].Should().Be("order");
        _builder.AttributeNameHelper.AttributeNames["#size"].Should().Be("size");
        _builder.AttributeNameHelper.AttributeNames["#count"].Should().Be("count");
    }

    [Fact]
    public void WithAttribute_EmptyParameterName_ShouldAddAttribute()
    {
        // Act
        var result = _builder.WithAttribute("", "actualName");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().HaveCount(1);
        _builder.AttributeNameHelper.AttributeNames[""].Should().Be("actualName");
    }

    [Fact]
    public void WithAttribute_EmptyAttributeName_ShouldAddAttribute()
    {
        // Act
        var result = _builder.WithAttribute("#param", "");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeNameHelper.AttributeNames.Should().HaveCount(1);
        _builder.AttributeNameHelper.AttributeNames["#param"].Should().Be("");
    }

    // Test builder class for testing extension methods
    private class TestBuilder : IWithAttributeNames<TestBuilder>
    {
        public AttributeNameInternal AttributeNameHelper { get; } = new();
        public TestBuilder Self => this;

        public AttributeNameInternal GetAttributeNameHelper() => AttributeNameHelper;
    }
}