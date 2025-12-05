using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

public class DynamoDbTableTests
{
    public class TestTable(IAmazonDynamoDB client) : DynamoDbTableBase(client, "TestTable")
    {
        public DynamoDbIndex Gsi1 => new DynamoDbIndex(this, "gsi1");
        
        // Override to test virtual method behavior
        public override GetItemRequestBuilder<TEntity> Get<TEntity>() where TEntity : class => base.Get<TEntity>();
        public override UpdateItemRequestBuilder<TEntity> Update<TEntity>() where TEntity : class => base.Update<TEntity>();
        public override DeleteItemRequestBuilder<TEntity> Delete<TEntity>() where TEntity : class => base.Delete<TEntity>();
    }
    
    public class TestEntity
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void TableInitializationSuccess()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        table.Name.Should().Be("TestTable");
        table.DynamoDbClient.Should().NotBeNull();
    }

    [Fact]
    public void QueryMethodReturnsCorrectBuilderType()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var query = table.Query<TestEntity>();
        
        query.Should().NotBeNull();
        query.Should().BeOfType<QueryRequestBuilder<TestEntity>>();
        query.ToQueryRequest().TableName.Should().Be("TestTable");
    }

    [Fact]
    public void QueryMethodReturnsNewInstanceEachTime()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var query1 = table.Query<TestEntity>();
        var query2 = table.Query<TestEntity>();
        
        query1.Should().NotBeSameAs(query2);
    }

    [Fact]
    public void QueryWithExpressionConfiguresKeyConditionCorrectly()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var query = table.Query<TestEntity>("pk = {0}", "USER#123");
        
        query.Should().NotBeNull();
        var request = query.ToQueryRequest();
        request.TableName.Should().Be("TestTable");
        request.KeyConditionExpression.Should().Be("pk = :p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
    }

    [Fact]
    public void QueryWithCompositeKeyExpressionConfiguresCorrectly()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var query = table.Query<TestEntity>("pk = {0} AND sk > {1}", "USER#123", "2024-01-01");
        
        var request = query.ToQueryRequest();
        request.KeyConditionExpression.Should().Be("pk = :p0 AND sk > :p1");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-01-01");
    }

    [Fact]
    public void GetMethodReturnsCorrectBuilderType()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var get = table.Get<TestEntity>();
        
        get.Should().NotBeNull();
        get.Should().BeOfType<GetItemRequestBuilder<TestEntity>>();
        get.ToGetItemRequest().TableName.Should().Be("TestTable");
    }

    [Fact]
    public void GetMethodReturnsNewInstanceEachTime()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var get1 = table.Get<TestEntity>();
        var get2 = table.Get<TestEntity>();
        
        get1.Should().NotBeSameAs(get2);
    }

    [Fact]
    public void UpdateMethodReturnsCorrectBuilderType()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var update = table.Update<TestEntity>();
        
        update.Should().NotBeNull();
        update.Should().BeOfType<UpdateItemRequestBuilder<TestEntity>>();
        update.ToUpdateItemRequest().TableName.Should().Be("TestTable");
    }

    [Fact]
    public void UpdateMethodReturnsNewInstanceEachTime()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var update1 = table.Update<TestEntity>();
        var update2 = table.Update<TestEntity>();
        
        update1.Should().NotBeSameAs(update2);
    }

    [Fact]
    public void DeleteMethodReturnsCorrectBuilderType()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var delete = table.Delete<TestEntity>();
        
        delete.Should().NotBeNull();
        delete.Should().BeOfType<DeleteItemRequestBuilder<TestEntity>>();
        delete.ToDeleteItemRequest().TableName.Should().Be("TestTable");
    }

    [Fact]
    public void DeleteMethodReturnsNewInstanceEachTime()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var delete1 = table.Delete<TestEntity>();
        var delete2 = table.Delete<TestEntity>();
        
        delete1.Should().NotBeSameAs(delete2);
    }

    [Fact]
    public void PutMethodReturnsCorrectBuilderType()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var put = table.Put<TestEntity>();
        
        put.Should().NotBeNull();
        put.Should().BeOfType<PutItemRequestBuilder<TestEntity>>();
        put.ToPutItemRequest().TableName.Should().Be("TestTable");
    }

    [Fact]
    public void PutMethodReturnsNewInstanceEachTime()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var put1 = table.Put<TestEntity>();
        var put2 = table.Put<TestEntity>();
        
        put1.Should().NotBeSameAs(put2);
    }

    [Fact]
    public void VirtualGetMethodCanBeOverridden()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var get = table.Get<TestEntity>();
        
        // Verify the method can be called and returns correct type
        get.Should().NotBeNull();
        get.Should().BeOfType<GetItemRequestBuilder<TestEntity>>();
    }

    [Fact]
    public void VirtualUpdateMethodCanBeOverridden()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var update = table.Update<TestEntity>();
        
        // Verify the method can be called and returns correct type
        update.Should().NotBeNull();
        update.Should().BeOfType<UpdateItemRequestBuilder<TestEntity>>();
    }

    [Fact]
    public void VirtualDeleteMethodCanBeOverridden()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var delete = table.Delete<TestEntity>();
        
        // Verify the method can be called and returns correct type
        delete.Should().NotBeNull();
        delete.Should().BeOfType<DeleteItemRequestBuilder<TestEntity>>();
    }

    [Fact]
    public void QueryOnIndexReturnsBuilder()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var query = table.Gsi1.Query<TestEntity>();
        
        query.Should().NotBeNull();
        query.ToQueryRequest().TableName.Should().Be("TestTable");
        query.ToQueryRequest().IndexName.Should().Be("gsi1");
    }

    [Fact]
    public void QueryOnIndexReturnsNewInstanceEachTime()
    {
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>());
        var query1 = table.Gsi1.Query<TestEntity>();
        var query2 = table.Gsi1.Query<TestEntity>();
        
        query1.Should().NotBeSameAs(query2);
    }
}