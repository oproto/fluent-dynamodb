using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Examples;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

public class ManualTableImplementationTests
{
    [Fact]
    public void SingleKeyTable_GetWithKeyConfiguresCorrectly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var builder = table.Get("user-123");
        
        builder.Should().NotBeNull();
        builder.Should().BeOfType<GetItemRequestBuilder<PlaceholderEntity>>();
        
        var request = builder.ToGetItemRequest();
        request.TableName.Should().Be("Users");
        request.Key.Should().ContainKey("id");
        request.Key["id"].S.Should().Be("user-123");
    }

    [Fact]
    public void SingleKeyTable_UpdateWithKeyConfiguresCorrectly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var builder = table.Update("user-123");
        
        builder.Should().NotBeNull();
        builder.Should().BeOfType<UpdateItemRequestBuilder<PlaceholderEntity>>();
        
        var request = builder.ToUpdateItemRequest();
        request.TableName.Should().Be("Users");
        request.Key.Should().ContainKey("id");
        request.Key["id"].S.Should().Be("user-123");
    }

    [Fact]
    public void SingleKeyTable_DeleteWithKeyConfiguresCorrectly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var builder = table.Delete("user-123");
        
        builder.Should().NotBeNull();
        builder.Should().BeOfType<DeleteItemRequestBuilder<PlaceholderEntity>>();
        
        var request = builder.ToDeleteItemRequest();
        request.TableName.Should().Be("Users");
        request.Key.Should().ContainKey("id");
        request.Key["id"].S.Should().Be("user-123");
    }

    [Fact]
    public void SingleKeyTable_GetAllowsChainingWithOtherMethods()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var builder = table.Get("user-123")
            .WithProjection("id, name, email")
            .UsingConsistentRead();
        
        var request = builder.ToGetItemRequest();
        request.Key["id"].S.Should().Be("user-123");
        request.ProjectionExpression.Should().Be("id, name, email");
        request.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void SingleKeyTable_UpdateAllowsChainingWithOtherMethods()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var builder = table.Update("user-123")
            .Set("SET #name = {0}, #email = {1}", "John Doe", "john@example.com")
            .WithAttribute("#name", "name")
            .WithAttribute("#email", "email");
        
        var request = builder.ToUpdateItemRequest();
        request.Key["id"].S.Should().Be("user-123");
        request.UpdateExpression.Should().Contain("SET");
    }

    [Fact]
    public void SingleKeyTable_DeleteAllowsChainingWithOtherMethods()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var builder = table.Delete("user-123")
            .Where("attribute_exists(id)");
        
        var request = builder.ToDeleteItemRequest();
        request.Key["id"].S.Should().Be("user-123");
        request.ConditionExpression.Should().Be("attribute_exists(id)");
    }

    [Fact]
    public void SingleKeyTable_EmailIndexQueryWorks()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var builder = table.EmailIndex.Query<PlaceholderEntity>("email = {0}", "user@example.com");
        
        var request = builder.ToQueryRequest();
        request.TableName.Should().Be("Users");
        request.IndexName.Should().Be("EmailIndex");
        request.KeyConditionExpression.Should().Be("email = :p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("user@example.com");
        request.ProjectionExpression.Should().Be("id, name, email, status");
    }

    [Fact]
    public void SingleKeyTable_StatusIndexQueryWorks()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var builder = table.StatusIndex.Query<PlaceholderEntity>("status = {0} AND created_at > {1}", "ACTIVE", "2024-01-01");
        
        var request = builder.ToQueryRequest();
        request.TableName.Should().Be("Users");
        request.IndexName.Should().Be("StatusIndex");
        request.KeyConditionExpression.Should().Be("status = :p0 AND created_at > :p1");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-01-01");
        request.ProjectionExpression.Should().Be("id, name, email, status, created_at");
    }

    [Fact]
    public void CompositeKeyTable_GetWithKeysConfiguresCorrectly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.Get("customer-123", "order-456");
        
        builder.Should().NotBeNull();
        builder.Should().BeOfType<GetItemRequestBuilder<PlaceholderEntity>>();
        
        var request = builder.ToGetItemRequest();
        request.TableName.Should().Be("Orders");
        request.Key.Should().ContainKey("customer_id");
        request.Key.Should().ContainKey("order_id");
        request.Key["customer_id"].S.Should().Be("customer-123");
        request.Key["order_id"].S.Should().Be("order-456");
    }

    [Fact]
    public void CompositeKeyTable_UpdateWithKeysConfiguresCorrectly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.Update("customer-123", "order-456");
        
        builder.Should().NotBeNull();
        builder.Should().BeOfType<UpdateItemRequestBuilder<PlaceholderEntity>>();
        
        var request = builder.ToUpdateItemRequest();
        request.TableName.Should().Be("Orders");
        request.Key.Should().ContainKey("customer_id");
        request.Key.Should().ContainKey("order_id");
        request.Key["customer_id"].S.Should().Be("customer-123");
        request.Key["order_id"].S.Should().Be("order-456");
    }

    [Fact]
    public void CompositeKeyTable_DeleteWithKeysConfiguresCorrectly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.Delete("customer-123", "order-456");
        
        builder.Should().NotBeNull();
        builder.Should().BeOfType<DeleteItemRequestBuilder<PlaceholderEntity>>();
        
        var request = builder.ToDeleteItemRequest();
        request.TableName.Should().Be("Orders");
        request.Key.Should().ContainKey("customer_id");
        request.Key.Should().ContainKey("order_id");
        request.Key["customer_id"].S.Should().Be("customer-123");
        request.Key["order_id"].S.Should().Be("order-456");
    }

    [Fact]
    public void CompositeKeyTable_GetAllowsChainingWithOtherMethods()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.Get("customer-123", "order-456")
            .WithProjection("customer_id, order_id, amount, status")
            .UsingConsistentRead();
        
        var request = builder.ToGetItemRequest();
        request.Key["customer_id"].S.Should().Be("customer-123");
        request.Key["order_id"].S.Should().Be("order-456");
        request.ProjectionExpression.Should().Be("customer_id, order_id, amount, status");
        request.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void CompositeKeyTable_UpdateAllowsChainingWithOtherMethods()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.Update("customer-123", "order-456")
            .Set("SET #status = {0}", "SHIPPED")
            .WithAttribute("#status", "status")
            .Where("status = {0}", "PENDING");
        
        var request = builder.ToUpdateItemRequest();
        request.Key["customer_id"].S.Should().Be("customer-123");
        request.Key["order_id"].S.Should().Be("order-456");
        request.UpdateExpression.Should().Contain("SET");
        request.ConditionExpression.Should().Be("status = :p1"); // :p0 is used by Set, so Where uses :p1
    }

    [Fact]
    public void CompositeKeyTable_DeleteAllowsChainingWithOtherMethods()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.Delete("customer-123", "order-456")
            .Where("status = {0}", "CANCELLED")
            .ReturnAllOldValues();
        
        var request = builder.ToDeleteItemRequest();
        request.Key["customer_id"].S.Should().Be("customer-123");
        request.Key["order_id"].S.Should().Be("order-456");
        request.ConditionExpression.Should().Be("status = :p0");
        request.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void CompositeKeyTable_StatusIndexQueryWorks()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.StatusIndex.Query<PlaceholderEntity>("status = {0}", "PENDING");
        
        var request = builder.ToQueryRequest();
        request.TableName.Should().Be("Orders");
        request.IndexName.Should().Be("StatusIndex");
        request.KeyConditionExpression.Should().Be("status = :p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("PENDING");
        request.ProjectionExpression.Should().Be("customer_id, order_id, status, amount, created_at");
    }

    [Fact]
    public void CompositeKeyTable_StatusIndexQueryWithCompositeKeyWorks()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.StatusIndex.Query<PlaceholderEntity>("status = {0} AND created_at > {1}", "PENDING", "2024-01-01");
        
        var request = builder.ToQueryRequest();
        request.TableName.Should().Be("Orders");
        request.IndexName.Should().Be("StatusIndex");
        request.KeyConditionExpression.Should().Be("status = :p0 AND created_at > :p1");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("PENDING");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-01-01");
    }

    [Fact]
    public void CompositeKeyTable_ProductIndexQueryWorks()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.ProductIndex.Query<PlaceholderEntity>("product_id = {0}", "product-789");
        
        var request = builder.ToQueryRequest();
        request.TableName.Should().Be("Orders");
        request.IndexName.Should().Be("ProductIndex");
        request.KeyConditionExpression.Should().Be("product_id = :p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("product-789");
        request.ProjectionExpression.Should().Be("customer_id, order_id, product_id, quantity, amount");
    }

    [Fact]
    public void CompositeKeyTable_ProductIndexQueryWithCompositeKeyWorks()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var builder = table.ProductIndex.Query<PlaceholderEntity>("product_id = {0} AND order_id > {1}", "product-789", "2024-01-01#");
        
        var request = builder.ToQueryRequest();
        request.TableName.Should().Be("Orders");
        request.IndexName.Should().Be("ProductIndex");
        request.KeyConditionExpression.Should().Be("product_id = :p0 AND order_id > :p1");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("product-789");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-01-01#");
    }

    [Fact]
    public void SingleKeyTable_VerifyKeyValuesAreProperlyConfigured()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new UsersTable(client);
        
        var getRequest = table.Get("test-id").ToGetItemRequest();
        var updateRequest = table.Update("test-id").ToUpdateItemRequest();
        var deleteRequest = table.Delete("test-id").ToDeleteItemRequest();
        
        getRequest.Key["id"].S.Should().Be("test-id");
        updateRequest.Key["id"].S.Should().Be("test-id");
        deleteRequest.Key["id"].S.Should().Be("test-id");
    }

    [Fact]
    public void CompositeKeyTable_VerifyKeyValuesAreProperlyConfigured()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new OrdersTable(client);
        
        var getRequest = table.Get("cust-1", "order-1").ToGetItemRequest();
        var updateRequest = table.Update("cust-1", "order-1").ToUpdateItemRequest();
        var deleteRequest = table.Delete("cust-1", "order-1").ToDeleteItemRequest();
        
        getRequest.Key["customer_id"].S.Should().Be("cust-1");
        getRequest.Key["order_id"].S.Should().Be("order-1");
        updateRequest.Key["customer_id"].S.Should().Be("cust-1");
        updateRequest.Key["order_id"].S.Should().Be("order-1");
        deleteRequest.Key["customer_id"].S.Should().Be("cust-1");
        deleteRequest.Key["order_id"].S.Should().Be("order-1");
    }
}
