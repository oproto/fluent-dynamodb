using Amazon.DynamoDBv2;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

public class DynamoDbIndexGenericTests
{
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client) : base(client, "TestTable") { }
    }

    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    [Fact]
    public void QueryMethodReturnsCorrectBuilderWithIndexConfiguration()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex");
        
        var query = index.Query<TestEntity>();
        
        query.Should().NotBeNull();
        query.Should().BeOfType<QueryRequestBuilder<TestEntity>>();
        
        var request = query.ToQueryRequest();
        request.TableName.Should().Be("TestTable");
        request.IndexName.Should().Be("TestIndex");
    }

    [Fact]
    public void QueryMethodReturnsNewInstanceEachTime()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex");
        
        var query1 = index.Query<TestEntity>();
        var query2 = index.Query<TestEntity>();
        
        query1.Should().NotBeSameAs(query2);
    }

    [Fact]
    public void QueryWithExpressionConfiguresPartitionKeyCondition()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex");
        
        var query = index.Query<TestEntity>("gsi1pk = {0}", "STATUS#ACTIVE");
        
        query.Should().NotBeNull();
        var request = query.ToQueryRequest();
        request.TableName.Should().Be("TestTable");
        request.IndexName.Should().Be("TestIndex");
        request.KeyConditionExpression.Should().Be("gsi1pk = :p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("STATUS#ACTIVE");
    }

    [Fact]
    public void QueryWithCompositeKeyExpressionConfiguresCorrectly()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex");
        
        var query = index.Query<TestEntity>("gsi1pk = {0} AND gsi1sk > {1}", "STATUS#ACTIVE", "2024-01-01");
        
        var request = query.ToQueryRequest();
        request.KeyConditionExpression.Should().Be("gsi1pk = :p0 AND gsi1sk > :p1");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("STATUS#ACTIVE");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-01-01");
    }

    [Fact]
    public void GenericIndexWithProjectionExpressionAppliesAutomatically()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex", "id, name, status");
        
        var query = index.Query<TestEntity>();
        
        var request = query.ToQueryRequest();
        request.ProjectionExpression.Should().Be("id, name, status");
    }

    [Fact]
    public void GenericIndexWithProjectionExpressionAppliesWithQueryExpression()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex", "id, name, status");
        
        var query = index.Query<TestEntity>("gsi1pk = {0}", "STATUS#ACTIVE");
        
        var request = query.ToQueryRequest();
        request.ProjectionExpression.Should().Be("id, name, status");
        request.KeyConditionExpression.Should().Be("gsi1pk = :p0");
    }

    [Fact]
    public void GenericIndexNamePropertyReturnsCorrectValue()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex");
        
        index.Name.Should().Be("TestIndex");
    }

    [Fact]
    public void GenericIndexWithoutProjectionDoesNotSetProjectionExpression()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex");
        
        var query = index.Query<TestEntity>();
        
        var request = query.ToQueryRequest();
        request.ProjectionExpression.Should().BeNull();
    }

    [Fact]
    public void GenericIndexWithNullProjectionDoesNotSetProjectionExpression()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var index = new DynamoDbIndex<TestEntity>(table, "TestIndex", null);
        
        var query = index.Query<TestEntity>();
        
        var request = query.ToQueryRequest();
        request.ProjectionExpression.Should().BeNull();
    }
}
