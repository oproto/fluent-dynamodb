using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;

using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Property-based tests for WithClient method on request builders.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class WithClientPropertyTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items.First(), options);
        }

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
        {
            return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
        }

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
        {
            return item.ContainsKey("pk");
        }

        public static EntityMetadata GetEntityMetadata()
        {
            return new EntityMetadata
            {
                TableName = "test-table",
                Properties = Array.Empty<PropertyMetadata>(),
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
        }
    }

    #region QueryRequestBuilder WithClient Tests

    /// <summary>
    /// 
    /// For any QueryRequestBuilder instance, calling WithClient(newClient) SHALL return 
    /// the same builder instance (reference equality).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryRequestBuilder_WithClient_ReturnsSameInstance()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            // Arrange
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new QueryRequestBuilder<TestEntity>(originalClient);
            builder.ForTable(tableName);

            // Act
            var result = builder.WithClient(newClient);

            // Assert - Same instance returned
            return ReferenceEquals(builder, result);
        });
    }

    /// <summary>
    /// 
    /// For any QueryRequestBuilder instance with configuration, calling WithClient(newClient) 
    /// SHALL preserve all configuration (table name, key condition, filter, etc.).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryRequestBuilder_WithClient_PreservesConfiguration()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            (tableName, indexName) =>
            {
                // Arrange
                var originalClient = Substitute.For<IAmazonDynamoDB>();
                var newClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new QueryRequestBuilder<TestEntity>(originalClient);
                
                // Configure the builder
                builder.ForTable(tableName)
                    .UsingIndex(indexName)
                    .Where("pk = :pk")
                    .WithValue(":pk", "test-value")
                    .WithAttribute("#name", "name")
                    .Take(10);

                // Act
                builder.WithClient(newClient);
                var request = builder.ToQueryRequest();

                // Assert - Configuration preserved
                return request.TableName == tableName &&
                       request.IndexName == indexName &&
                       request.KeyConditionExpression == "pk = :pk" &&
                       request.ExpressionAttributeValues.ContainsKey(":pk") &&
                       request.ExpressionAttributeNames.ContainsKey("#name") &&
                       request.Limit == 10;
            });
    }

    #endregion

    #region GetItemRequestBuilder WithClient Tests

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetItemRequestBuilder_WithClient_ReturnsSameInstance()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new GetItemRequestBuilder<TestEntity>(originalClient);
            builder.ForTable(tableName);

            var result = builder.WithClient(newClient);

            return ReferenceEquals(builder, result);
        });
    }

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetItemRequestBuilder_WithClient_PreservesConfiguration()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new GetItemRequestBuilder<TestEntity>(originalClient);
            
            builder.ForTable(tableName)
                .WithProjection("pk, name")
                .UsingConsistentRead();

            builder.WithClient(newClient);
            var request = builder.ToGetItemRequest();

            return request.TableName == tableName &&
                   request.ProjectionExpression == "pk, name" &&
                   request.ConsistentRead == true;
        });
    }

    #endregion

    #region PutItemRequestBuilder WithClient Tests

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutItemRequestBuilder_WithClient_ReturnsSameInstance()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new PutItemRequestBuilder<TestEntity>(originalClient);
            builder.ForTable(tableName);

            var result = builder.WithClient(newClient);

            return ReferenceEquals(builder, result);
        });
    }

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutItemRequestBuilder_WithClient_PreservesConfiguration()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new PutItemRequestBuilder<TestEntity>(originalClient);
            
            builder.ForTable(tableName)
                .Where("attribute_not_exists(pk)")
                .ReturnAllOldValues();

            builder.WithClient(newClient);
            var request = builder.ToPutItemRequest();

            return request.TableName == tableName &&
                   request.ConditionExpression == "attribute_not_exists(pk)" &&
                   request.ReturnValues == ReturnValue.ALL_OLD;
        });
    }

    #endregion

    #region UpdateItemRequestBuilder WithClient Tests

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateItemRequestBuilder_WithClient_ReturnsSameInstance()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new UpdateItemRequestBuilder<TestEntity>(originalClient);
            builder.ForTable(tableName);

            var result = builder.WithClient(newClient);

            return ReferenceEquals(builder, result);
        });
    }

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateItemRequestBuilder_WithClient_PreservesConfiguration()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new UpdateItemRequestBuilder<TestEntity>(originalClient);
            
            builder.ForTable(tableName)
                .Where("attribute_exists(pk)")
                .ReturnUpdatedNewValues();

            builder.WithClient(newClient);
            var request = builder.ToUpdateItemRequest();

            return request.TableName == tableName &&
                   request.ConditionExpression == "attribute_exists(pk)" &&
                   request.ReturnValues == ReturnValue.UPDATED_NEW;
        });
    }

    #endregion

    #region DeleteItemRequestBuilder WithClient Tests

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeleteItemRequestBuilder_WithClient_ReturnsSameInstance()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new DeleteItemRequestBuilder<TestEntity>(originalClient);
            builder.ForTable(tableName);

            var result = builder.WithClient(newClient);

            return ReferenceEquals(builder, result);
        });
    }

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeleteItemRequestBuilder_WithClient_PreservesConfiguration()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new DeleteItemRequestBuilder<TestEntity>(originalClient);
            
            builder.ForTable(tableName)
                .Where("attribute_exists(pk)")
                .ReturnAllOldValues();

            builder.WithClient(newClient);
            var request = builder.ToDeleteItemRequest();

            return request.TableName == tableName &&
                   request.ConditionExpression == "attribute_exists(pk)" &&
                   request.ReturnValues == ReturnValue.ALL_OLD;
        });
    }

    #endregion

    #region ScanRequestBuilder WithClient Tests

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ScanRequestBuilder_WithClient_ReturnsSameInstance()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new ScanRequestBuilder<TestEntity>(originalClient);
            builder.ForTable(tableName);

            var result = builder.WithClient(newClient);

            return ReferenceEquals(builder, result);
        });
    }

    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ScanRequestBuilder_WithClient_PreservesConfiguration()
    {
        return Prop.ForAll(Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)), tableName =>
        {
            var originalClient = Substitute.For<IAmazonDynamoDB>();
            var newClient = Substitute.For<IAmazonDynamoDB>();
            var builder = new ScanRequestBuilder<TestEntity>(originalClient);
            
            builder.ForTable(tableName)
                .WithFilter("#status = :status")
                .WithAttribute("#status", "status")
                .WithValue(":status", "ACTIVE")
                .Take(50);

            builder.WithClient(newClient);
            var request = builder.ToScanRequest();

            return request.TableName == tableName &&
                   request.FilterExpression == "#status = :status" &&
                   request.ExpressionAttributeNames.ContainsKey("#status") &&
                   request.ExpressionAttributeValues.ContainsKey(":status") &&
                   request.Limit == 50;
        });
    }

    #endregion
}
