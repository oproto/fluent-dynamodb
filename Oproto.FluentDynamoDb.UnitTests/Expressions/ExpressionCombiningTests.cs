using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Attributes;
using NSubstitute;
using Amazon.DynamoDBv2;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for expression combining through SetConditionExpression and SetFilterExpression.
/// </summary>
public class ExpressionCombiningTests
{
    private class TestEntity : IEntityMetadataProvider
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string SortKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Status { get; set; } = string.Empty;

        public static EntityMetadata GetEntityMetadata()
        {
            return new EntityMetadata
            {
                TableName = "TestTable",
                Properties = new[]
                {
                    new PropertyMetadata
                    {
                        PropertyName = "PartitionKey",
                        AttributeName = "PK",
                        PropertyType = typeof(string),
                        IsPartitionKey = true,
                        IsSortKey = false,
                        SupportedOperations = new[] { DynamoDbOperation.Equals }
                    },
                    new PropertyMetadata
                    {
                        PropertyName = "SortKey",
                        AttributeName = "SK",
                        PropertyType = typeof(string),
                        IsPartitionKey = false,
                        IsSortKey = true,
                        SupportedOperations = new[] { DynamoDbOperation.Equals, DynamoDbOperation.BeginsWith }
                    },
                    new PropertyMetadata
                    {
                        PropertyName = "Name",
                        AttributeName = "Name",
                        PropertyType = typeof(string),
                        IsPartitionKey = false,
                        IsSortKey = false,
                        SupportedOperations = new[] { DynamoDbOperation.Equals }
                    },
                    new PropertyMetadata
                    {
                        PropertyName = "Age",
                        AttributeName = "Age",
                        PropertyType = typeof(int),
                        IsPartitionKey = false,
                        IsSortKey = false,
                        SupportedOperations = new[] { DynamoDbOperation.GreaterThan }
                    },
                    new PropertyMetadata
                    {
                        PropertyName = "Status",
                        AttributeName = "Status",
                        PropertyType = typeof(string),
                        IsPartitionKey = false,
                        IsSortKey = false,
                        SupportedOperations = new[] { DynamoDbOperation.Equals }
                    }
                }
            };
        }
    }

    private EntityMetadata CreateTestEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "PartitionKey",
                    AttributeName = "PK",
                    PropertyType = typeof(string),
                    IsPartitionKey = true,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                },
                new PropertyMetadata
                {
                    PropertyName = "SortKey",
                    AttributeName = "SK",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = true,
                    SupportedOperations = new[] { DynamoDbOperation.Equals, DynamoDbOperation.BeginsWith }
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "Name",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                },
                new PropertyMetadata
                {
                    PropertyName = "Age",
                    AttributeName = "Age",
                    PropertyType = typeof(int),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.GreaterThan }
                },
                new PropertyMetadata
                {
                    PropertyName = "Status",
                    AttributeName = "Status",
                    PropertyType = typeof(string),
                    IsPartitionKey = false,
                    IsSortKey = false,
                    SupportedOperations = new[] { DynamoDbOperation.Equals }
                }
            }
        };
    }

    [Fact]
    public void SetConditionExpression_CalledTwice_ShouldCombineWithAnd()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);

        // Act
        builder
            .ForTable("TestTable")
            .SetConditionExpression("PK = :pk")
            .SetConditionExpression("SK > :sk");

        var request = builder.ToQueryRequest();

        // Assert
        request.KeyConditionExpression.Should().Be("(PK = :pk) AND (SK > :sk)");
    }

    [Fact]
    public void SetFilterExpression_CalledTwice_ShouldCombineWithAnd()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);

        // Act
        builder
            .ForTable("TestTable")
            .SetConditionExpression("PK = :pk")
            .SetFilterExpression("Name = :name")
            .SetFilterExpression("Age > :age");

        var request = builder.ToQueryRequest();

        // Assert
        request.FilterExpression.Should().Be("(Name = :name) AND (Age > :age)");
    }

    [Fact]
    public void MixedExpressionAndStringWhere_ShouldCombineCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.PartitionKey == "USER#123", metadata)
            .Where("SK > :minSk")
            .WithValue(":minSk", "ORDER#2024");

        var request = builder.ToQueryRequest();

        // Assert
        request.KeyConditionExpression.Should().Contain("AND");
        request.KeyConditionExpression.Should().Contain("SK > :minSk");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues.Should().ContainKey(":minSk");
        request.ExpressionAttributeNames.Should().ContainKey("#attr0");
        request.ExpressionAttributeNames["#attr0"].Should().Be("PK");
    }

    [Fact]
    public void MixedStringAndExpressionWhere_ShouldCombineCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where("PK = :pk")
            .WithValue(":pk", "USER#123")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.SortKey.StartsWith("ORDER#"), metadata);

        var request = builder.ToQueryRequest();

        // Assert
        request.KeyConditionExpression.Should().Contain("AND");
        request.KeyConditionExpression.Should().Contain("PK = :pk");
        request.KeyConditionExpression.Should().Contain("begins_with");
    }

    [Fact]
    public void MixedExpressionAndFormatStringWhere_ShouldCombineCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.PartitionKey == "USER#123", metadata)
            .Where("SK > {0}", "ORDER#2024");

        var request = builder.ToQueryRequest();

        // Assert
        request.KeyConditionExpression.Should().Contain("AND");
        request.ExpressionAttributeNames.Should().ContainKey("#attr0");
        request.ExpressionAttributeNames["#attr0"].Should().Be("PK");
        request.KeyConditionExpression.Should().Contain("SK");
    }

    [Fact]
    public void MixedExpressionAndStringFilter_ShouldCombineCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.PartitionKey == "USER#123", metadata)
            .WithFilter<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.Name == "John", metadata)
            .WithFilter("#status = :status")
            .WithAttribute("#status", "Status")
            .WithValue(":status", "ACTIVE");

        var request = builder.ToQueryRequest();

        // Assert
        request.FilterExpression.Should().Contain("AND");
        request.FilterExpression.Should().Contain("#status = :status");
        request.ExpressionAttributeNames.Should().ContainKey("#attr1");
        request.ExpressionAttributeNames["#attr1"].Should().Be("Name");
    }

    [Fact]
    public void ParameterNameUniqueness_AcrossMultipleCalls_ShouldBeUnique()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.PartitionKey == "USER#123", metadata)
            .Where("SK = :sk")
            .WithValue(":sk", "ORDER#456")
            .WithFilter<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.Name == "John", metadata)
            .WithFilter<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.Age > 18, metadata);

        var request = builder.ToQueryRequest();

        // Assert
        // Each expression call should generate unique parameter names
        var parameterKeys = request.ExpressionAttributeValues.Keys.ToList();
        parameterKeys.Should().OnlyHaveUniqueItems();
        parameterKeys.Should().HaveCountGreaterThan(3); // At least 4 parameters
    }

    [Fact]
    public void AttributeNameUniqueness_AcrossMultipleCalls_ShouldBeUnique()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.PartitionKey == "USER#123", metadata)
            .Where("SK > :sk")
            .WithValue(":sk", "A")
            .WithFilter<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.Name == "John", metadata)
            .WithFilter<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.Age > 18, metadata);

        var request = builder.ToQueryRequest();

        // Assert
        // Each property access should generate unique attribute name placeholders
        var attributeNameKeys = request.ExpressionAttributeNames.Keys.ToList();
        attributeNameKeys.Should().OnlyHaveUniqueItems();
        attributeNameKeys.Should().HaveCountGreaterThan(2); // At least 3 attribute names
    }

    [Fact]
    public void ThreeWhereCalls_ShouldCombineAllWithAnd()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.PartitionKey == "USER#123", metadata)
            .Where("SK > :minSk")
            .WithValue(":minSk", "A")
            .Where("SK < :maxSk")
            .WithValue(":maxSk", "Z");

        var request = builder.ToQueryRequest();

        // Assert
        request.KeyConditionExpression.Should().Contain("AND");
        // Should have two AND operators for three conditions
        var andCount = request.KeyConditionExpression.Split("AND").Length - 1;
        andCount.Should().Be(2);
    }

    [Fact]
    public void ScanWithMultipleFilters_ShouldCombineCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .WithFilter<ScanRequestBuilder<TestEntity>, TestEntity>(x => x.Name == "John", metadata)
            .WithFilter<ScanRequestBuilder<TestEntity>, TestEntity>(x => x.Age > 18, metadata)
            .WithFilter<ScanRequestBuilder<TestEntity>, TestEntity>(x => x.Status == "ACTIVE", metadata);

        var request = builder.ToScanRequest();

        // Assert
        request.FilterExpression.Should().Contain("AND");
        var andCount = request.FilterExpression.Split("AND").Length - 1;
        andCount.Should().Be(2); // Two AND operators for three conditions
    }

    [Fact]
    public void ComplexMixedCalls_ShouldMaintainParameterIntegrity()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.PartitionKey == "USER#123", metadata)
            .Where("SK BETWEEN :low AND :high")
            .WithValue(":low", "A")
            .WithValue(":high", "Z")
            .WithFilter("Name = {0}", "John")
            .WithFilter("#status = {0}", "ACTIVE")
            .WithAttribute("#status", "Status");

        var request = builder.ToQueryRequest();

        // Assert
        // Verify all parameters are present
        request.ExpressionAttributeValues.Should().ContainKey(":p0"); // From first Where
        request.ExpressionAttributeValues.Should().ContainKey(":low"); // From second Where
        request.ExpressionAttributeValues.Should().ContainKey(":high"); // From second Where
        
        // Verify expressions are combined
        request.KeyConditionExpression.Should().Contain("AND");
        request.FilterExpression.Should().Contain("AND");
    }

    [Fact]
    public void ExpressionCombining_WithDifferentValueTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(client);
        var metadata = CreateTestEntityMetadata();

        // Act
        builder
            .ForTable("TestTable")
            .Where<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.PartitionKey == "USER#123", metadata)
            .WithFilter<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.Age > 18, metadata)
            .WithFilter<QueryRequestBuilder<TestEntity>, TestEntity>(x => x.Name == "John", metadata);

        var request = builder.ToQueryRequest();

        // Assert
        // Verify different value types are captured correctly
        var stringValue = request.ExpressionAttributeValues.Values.FirstOrDefault(v => v.S != null);
        var numberValue = request.ExpressionAttributeValues.Values.FirstOrDefault(v => v.N != null);
        
        stringValue.Should().NotBeNull();
        numberValue.Should().NotBeNull();
    }
}
