using Amazon.DynamoDBv2;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Minimal backward compatibility verification for UpdateItemRequestBuilder.
/// The base UpdateItemRequestBuilder class was not modified - only entity-specific builders were added.
/// The existing UpdateItemRequestBuilderTests (40 tests) already verify the base builder works correctly.
/// These tests simply confirm the public API surface remains unchanged.
/// </summary>
public class UpdateItemRequestBuilderBackwardCompatibilityTests
{
    private class TestEntity { }

    [Fact]
    public void BaseBuilder_PublicAPISignature_RemainsUnchanged()
    {
        // Verify the base builder can still be instantiated directly
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);

        // Verify it's still the correct type
        builder.Should().BeOfType<UpdateItemRequestBuilder<TestEntity>>();
    }

    [Fact]
    public void BaseBuilder_Interfaces_StillImplemented()
    {
        // Verify all interfaces are still implemented
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);

        builder.Should().BeAssignableTo<IWithKey<UpdateItemRequestBuilder<TestEntity>>>();
        builder.Should().BeAssignableTo<IWithConditionExpression<UpdateItemRequestBuilder<TestEntity>>>();
        builder.Should().BeAssignableTo<IWithAttributeNames<UpdateItemRequestBuilder<TestEntity>>>();
        builder.Should().BeAssignableTo<IWithAttributeValues<UpdateItemRequestBuilder<TestEntity>>>();
        builder.Should().BeAssignableTo<IWithUpdateExpression<UpdateItemRequestBuilder<TestEntity>>>();
    }

    [Fact]
    public void BaseBuilder_CorePublicMethods_StillAccessible()
    {
        // Verify core public methods remain accessible
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);

        var methods = typeof(UpdateItemRequestBuilder<TestEntity>).GetMethods(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly);

        var corePublicMethods = new[]
        {
            "ForTable",
            "ReturnValues",
            "ReturnUpdatedNewValues",
            "ReturnUpdatedOldValues",
            "ReturnAllNewValues",
            "ReturnAllOldValues",
            "ReturnNone",
            "ToUpdateItemRequest",
            "ToDynamoDbResponseAsync",
            "SetConditionExpression",
            "SetKey",
            "SetUpdateExpression"
        };

        foreach (var methodName in corePublicMethods)
        {
            methods.Should().Contain(m => m.Name == methodName,
                $"Core public method {methodName} should remain accessible");
        }
    }
}
