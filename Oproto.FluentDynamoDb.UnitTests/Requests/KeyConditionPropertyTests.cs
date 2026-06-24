using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Property-based tests for KeyCondition generation logic.
/// Tests verify that key conditions are correctly generated for various entity configurations.
/// </summary>
public class KeyConditionPropertyTests
{
    #region Test Entity Factories

    /// <summary>
    /// Creates a simple key entity type with the specified partition key attribute name.
    /// </summary>
    private class SimpleKeyTestEntity : IDynamoDbEntity
    {
        public static string PkAttributeName { get; set; } = "pk";

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
            => new() { [PkAttributeName] = new AttributeValue { S = "test" } };

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
            => (TSelf)(object)new SimpleKeyTestEntity();

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
            => FromDynamoDb<TSelf>(items.First(), options);

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item) => item[PkAttributeName].S;

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => item.ContainsKey(PkAttributeName);

        public static EntityMetadata GetEntityMetadata() => new()
        {
            TableName = "test-table",
            PartitionKeyAttributeName = PkAttributeName,
            PartitionKeyAttributeType = "S",
            SortKeyAttributeName = null,
            SortKeyAttributeType = null
        };

        public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
    }

    /// <summary>
    /// Creates a composite key entity type with the specified key attribute names.
    /// </summary>
    private class CompositeKeyTestEntity : IDynamoDbEntity
    {
        public static string PkAttributeName { get; set; } = "pk";
        public static string SkAttributeName { get; set; } = "sk";

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
            => new()
            {
                [PkAttributeName] = new AttributeValue { S = "test" },
                [SkAttributeName] = new AttributeValue { S = "test" }
            };

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
            => (TSelf)(object)new CompositeKeyTestEntity();

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
            => FromDynamoDb<TSelf>(items.First(), options);

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item) => item[PkAttributeName].S;

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => 
            item.ContainsKey(PkAttributeName) && item.ContainsKey(SkAttributeName);

        public static EntityMetadata GetEntityMetadata() => new()
        {
            TableName = "test-table",
            PartitionKeyAttributeName = PkAttributeName,
            PartitionKeyAttributeType = "S",
            SortKeyAttributeName = SkAttributeName,
            SortKeyAttributeType = "S"
        };

        public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
    }

    #endregion

    #region Property 1: Simple Key Condition Generation

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 1: Simple Key Condition Generation**
    /// *For any* entity with only a partition key and KeyCondition.MustExist,
    /// the generated condition SHALL be attribute_exists({pkAttrName}).
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SimpleKey_MustExist_GeneratesAttributeExists()
    {
        return Prop.ForAll(
            ValidAttributeNameArbitrary(),
            pkAttrName =>
            {
                // Arrange
                SimpleKeyTestEntity.PkAttributeName = pkAttrName;
                var builder = new PutItemRequestBuilder<SimpleKeyTestEntity>(Substitute.For<IAmazonDynamoDB>());
                builder.ForTable("test-table");

                // Act
                builder.WithKeyCondition(KeyCondition.MustExist);
                var request = builder.ToPutItemRequest();

                // Assert
                var expected = $"attribute_exists({pkAttrName})";
                return request.ConditionExpression == expected;
            });
    }

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 1: Simple Key Condition Generation**
    /// *For any* entity with only a partition key and KeyCondition.MustNotExist,
    /// the generated condition SHALL be attribute_not_exists({pkAttrName}).
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SimpleKey_MustNotExist_GeneratesAttributeNotExists()
    {
        return Prop.ForAll(
            ValidAttributeNameArbitrary(),
            pkAttrName =>
            {
                // Arrange
                SimpleKeyTestEntity.PkAttributeName = pkAttrName;
                var builder = new PutItemRequestBuilder<SimpleKeyTestEntity>(Substitute.For<IAmazonDynamoDB>());
                builder.ForTable("test-table");

                // Act
                builder.WithKeyCondition(KeyCondition.MustNotExist);
                var request = builder.ToPutItemRequest();

                // Assert
                var expected = $"attribute_not_exists({pkAttrName})";
                return request.ConditionExpression == expected;
            });
    }

    #endregion

    #region Property 2: Composite Key Condition Generation

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 2: Composite Key Condition Generation**
    /// *For any* entity with partition key and sort key and KeyCondition.MustExist,
    /// the generated condition SHALL be attribute_exists({pkAttrName}) AND attribute_exists({skAttrName}).
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositeKey_MustExist_GeneratesAttributeExistsForBothKeys()
    {
        return Prop.ForAll(
            ValidAttributeNameArbitrary(),
            ValidAttributeNameArbitrary(),
            (pkAttrName, skAttrName) =>
            {
                // Skip if pk and sk are the same (invalid configuration)
                if (pkAttrName == skAttrName) return true;

                // Arrange
                CompositeKeyTestEntity.PkAttributeName = pkAttrName;
                CompositeKeyTestEntity.SkAttributeName = skAttrName;
                var builder = new PutItemRequestBuilder<CompositeKeyTestEntity>(Substitute.For<IAmazonDynamoDB>());
                builder.ForTable("test-table");

                // Act
                builder.WithKeyCondition(KeyCondition.MustExist);
                var request = builder.ToPutItemRequest();

                // Assert
                var expected = $"attribute_exists({pkAttrName}) AND attribute_exists({skAttrName})";
                return request.ConditionExpression == expected;
            });
    }

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 2: Composite Key Condition Generation**
    /// *For any* entity with partition key and sort key and KeyCondition.MustNotExist,
    /// the generated condition SHALL be attribute_not_exists({pkAttrName}) AND attribute_not_exists({skAttrName}).
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositeKey_MustNotExist_GeneratesAttributeNotExistsForBothKeys()
    {
        return Prop.ForAll(
            ValidAttributeNameArbitrary(),
            ValidAttributeNameArbitrary(),
            (pkAttrName, skAttrName) =>
            {
                // Skip if pk and sk are the same (invalid configuration)
                if (pkAttrName == skAttrName) return true;

                // Arrange
                CompositeKeyTestEntity.PkAttributeName = pkAttrName;
                CompositeKeyTestEntity.SkAttributeName = skAttrName;
                var builder = new PutItemRequestBuilder<CompositeKeyTestEntity>(Substitute.For<IAmazonDynamoDB>());
                builder.ForTable("test-table");

                // Act
                builder.WithKeyCondition(KeyCondition.MustNotExist);
                var request = builder.ToPutItemRequest();

                // Assert
                var expected = $"attribute_not_exists({pkAttrName}) AND attribute_not_exists({skAttrName})";
                return request.ConditionExpression == expected;
            });
    }

    #endregion

    #region Property 3: Condition Combination

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 3: Condition Combination**
    /// *For any* operation with both a key condition and a Where clause,
    /// the final condition SHALL be ({keyCondition}) AND ({whereClause}).
    /// **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeyConditionAndWhereClause_CombinedWithAnd()
    {
        return Prop.ForAll(
            ValidAttributeNameArbitrary(),
            ValidWhereClauseArbitrary(),
            KeyConditionArbitrary(),
            (pkAttrName, whereClause, keyCondition) =>
            {
                // Skip None - no condition to combine
                if (keyCondition == KeyCondition.None) return true;

                // Arrange
                SimpleKeyTestEntity.PkAttributeName = pkAttrName;
                var builder = new PutItemRequestBuilder<SimpleKeyTestEntity>(Substitute.For<IAmazonDynamoDB>());
                builder.ForTable("test-table");

                // Act - Set Where clause first, then key condition
                builder.SetConditionExpression(whereClause);
                builder.WithKeyCondition(keyCondition);
                var request = builder.ToPutItemRequest();

                // Assert - Key condition should be prepended
                var keyConditionStr = keyCondition == KeyCondition.MustExist
                    ? $"attribute_exists({pkAttrName})"
                    : $"attribute_not_exists({pkAttrName})";
                var expected = $"({keyConditionStr}) AND ({whereClause})";
                return request.ConditionExpression == expected;
            });
    }

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 3: Condition Combination**
    /// *For any* operation with only a key condition (no Where clause),
    /// the final condition SHALL be exactly the key condition.
    /// **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeyConditionOnly_NoWhereClause_UsesKeyConditionOnly()
    {
        return Prop.ForAll(
            ValidAttributeNameArbitrary(),
            KeyConditionArbitrary(),
            (pkAttrName, keyCondition) =>
            {
                // Skip None - no condition expected
                if (keyCondition == KeyCondition.None) return true;

                // Arrange
                SimpleKeyTestEntity.PkAttributeName = pkAttrName;
                var builder = new PutItemRequestBuilder<SimpleKeyTestEntity>(Substitute.For<IAmazonDynamoDB>());
                builder.ForTable("test-table");

                // Act - Only key condition, no Where clause
                builder.WithKeyCondition(keyCondition);
                var request = builder.ToPutItemRequest();

                // Assert
                var expected = keyCondition == KeyCondition.MustExist
                    ? $"attribute_exists({pkAttrName})"
                    : $"attribute_not_exists({pkAttrName})";
                return request.ConditionExpression == expected;
            });
    }

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 3: Condition Combination**
    /// *For any* operation with only a Where clause (KeyCondition.None),
    /// the final condition SHALL be exactly the Where clause (unchanged behavior).
    /// **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhereClauseOnly_NoKeyCondition_UsesWhereClauseOnly()
    {
        return Prop.ForAll(
            ValidWhereClauseArbitrary(),
            whereClause =>
            {
                // Arrange
                var builder = new PutItemRequestBuilder<SimpleKeyTestEntity>(Substitute.For<IAmazonDynamoDB>());
                builder.ForTable("test-table");

                // Act - Only Where clause, KeyCondition.None (default)
                builder.SetConditionExpression(whereClause);
                builder.WithKeyCondition(KeyCondition.None);
                var request = builder.ToPutItemRequest();

                // Assert - Where clause unchanged
                return request.ConditionExpression == whereClause;
            });
    }

    #endregion

    #region Arbitraries

    /// <summary>
    /// Generates valid DynamoDB attribute names.
    /// DynamoDB attribute names must be 1-255 characters, starting with a letter or underscore.
    /// </summary>
    private static Arbitrary<string> ValidAttributeNameArbitrary()
    {
        // Generate simple valid attribute names for testing
        var validNames = new[]
        {
            "pk", "sk", "id", "userId", "orderId", "customerId",
            "partition_key", "sort_key", "PK", "SK", "ID",
            "gsi1pk", "gsi1sk", "lsi1sk", "data", "status",
            "_pk", "_sk", "key1", "key2", "attr1", "attr2"
        };

        return Arb.From(Gen.Elements(validNames));
    }

    /// <summary>
    /// Generates valid Where clause expressions for testing.
    /// </summary>
    private static Arbitrary<string> ValidWhereClauseArbitrary()
    {
        var validClauses = new[]
        {
            "#version = :version",
            "#status = :status",
            "attribute_exists(#field)",
            "attribute_not_exists(#field)",
            "#count > :minCount",
            "#name = :name AND #status = :status",
            "begins_with(#prefix, :prefix)",
            "#timestamp BETWEEN :start AND :end"
        };

        return Arb.From(Gen.Elements(validClauses));
    }

    /// <summary>
    /// Generates KeyCondition enum values.
    /// </summary>
    private static Arbitrary<KeyCondition> KeyConditionArbitrary()
    {
        return Arb.From(Gen.Elements(KeyCondition.None, KeyCondition.MustExist, KeyCondition.MustNotExist));
    }

    /// <summary>
    /// Generates non-None KeyCondition enum values for tests that require a condition.
    /// </summary>
    private static Arbitrary<KeyCondition> NonNoneKeyConditionArbitrary()
    {
        return Arb.From(Gen.Elements(KeyCondition.MustExist, KeyCondition.MustNotExist));
    }

    /// <summary>
    /// Generates operation types for testing.
    /// </summary>
    private static Arbitrary<OperationType> OperationTypeArbitrary()
    {
        return Arb.From(Gen.Elements(OperationType.Put, OperationType.Update, OperationType.Delete));
    }

    #endregion

    #region Property 6: Transaction/Batch Compatibility

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 6: Transaction/Batch Compatibility**
    /// *For any* builder with a key condition added to a transaction,
    /// the condition SHALL be preserved in the transaction item.
    /// **Validates: Requirements 9.1, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Transaction_PreservesKeyCondition_ForAnyOperation()
    {
        return Prop.ForAll(
            ValidAttributeNameArbitrary(),
            NonNoneKeyConditionArbitrary(),
            OperationTypeArbitrary(),
            (pkAttrName, keyCondition, operationType) =>
            {
                // Arrange
                SimpleKeyTestEntity.PkAttributeName = pkAttrName;
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                TransactWriteItemsRequest? capturedRequest = null;
                
                mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        capturedRequest = callInfo.Arg<TransactWriteItemsRequest>();
                        return new TransactWriteItemsResponse();
                    });

                // Act - Create builder based on operation type and add to transaction
                var transactionBuilder = DynamoDbTransactions.Write;
                
                switch (operationType)
                {
                    case OperationType.Put:
                        var putBuilder = new PutItemRequestBuilder<SimpleKeyTestEntity>(mockClient)
                            .ForTable("test-table")
                            .WithItem(new SimpleKeyTestEntity())
                            .WithKeyCondition(keyCondition);
                        transactionBuilder.Add(putBuilder);
                        break;
                    case OperationType.Update:
                        var updateBuilder = new UpdateItemRequestBuilder<SimpleKeyTestEntity>(mockClient)
                            .ForTable("test-table")
                            .WithKey("pk", "test-id")
                            .Set("SET #name = :name")
                            .WithAttribute("#name", "name")
                            .WithValue(":name", "value")
                            .WithKeyCondition(keyCondition);
                        transactionBuilder.Add(updateBuilder);
                        break;
                    case OperationType.Delete:
                        var deleteBuilder = new DeleteItemRequestBuilder<SimpleKeyTestEntity>(mockClient)
                            .ForTable("test-table")
                            .WithKey("pk", "test-id")
                            .WithKeyCondition(keyCondition);
                        transactionBuilder.Add(deleteBuilder);
                        break;
                }

                transactionBuilder.ExecuteAsync().Wait();

                // Assert - Verify condition is preserved
                if (capturedRequest == null) return false;
                
                var expectedCondition = keyCondition == KeyCondition.MustExist
                    ? $"attribute_exists({pkAttrName})"
                    : $"attribute_not_exists({pkAttrName})";

                var actualCondition = operationType switch
                {
                    OperationType.Put => capturedRequest.TransactItems[0].Put?.ConditionExpression,
                    OperationType.Update => capturedRequest.TransactItems[0].Update?.ConditionExpression,
                    OperationType.Delete => capturedRequest.TransactItems[0].Delete?.ConditionExpression,
                    _ => null
                };

                return actualCondition == expectedCondition;
            });
    }

    /// <summary>
    /// **Feature: key-condition-shortcuts, Property 6: Transaction/Batch Compatibility**
    /// *For any* builder with a key condition added to a batch operation,
    /// the condition SHALL be ignored (DynamoDB BatchWriteItem limitation).
    /// **Validates: Requirements 9.2, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Batch_IgnoresKeyCondition_ForAnyOperation()
    {
        return Prop.ForAll(
            NonNoneKeyConditionArbitrary(),
            BatchOperationTypeArbitrary(),
            (keyCondition, operationType) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                BatchWriteItemRequest? capturedRequest = null;
                
                mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        capturedRequest = callInfo.Arg<BatchWriteItemRequest>();
                        return new BatchWriteItemResponse();
                    });

                // Act - Create builder based on operation type and add to batch
                var batchBuilder = DynamoDbBatch.Write;
                
                switch (operationType)
                {
                    case BatchOperationType.Put:
                        var putBuilder = new PutItemRequestBuilder<SimpleKeyTestEntity>(mockClient)
                            .ForTable("test-table")
                            .WithItem(new SimpleKeyTestEntity())
                            .WithKeyCondition(keyCondition);
                        batchBuilder.Add(putBuilder);
                        break;
                    case BatchOperationType.Delete:
                        var deleteBuilder = new DeleteItemRequestBuilder<SimpleKeyTestEntity>(mockClient)
                            .ForTable("test-table")
                            .WithKey("pk", "test-id")
                            .WithKeyCondition(keyCondition);
                        batchBuilder.Add(deleteBuilder);
                        break;
                }

                batchBuilder.ExecuteAsync().Wait();

                // Assert - Verify batch operation succeeded (conditions are ignored)
                // BatchWriteItem doesn't have ConditionExpression - it just ignores conditions
                if (capturedRequest == null) return false;
                
                // Verify the request was made with the correct table
                return capturedRequest.RequestItems.ContainsKey("test-table") &&
                       capturedRequest.RequestItems["test-table"].Count == 1;
            });
    }

    /// <summary>
    /// Generates batch operation types (Put and Delete only - Update not supported in batch).
    /// </summary>
    private static Arbitrary<BatchOperationType> BatchOperationTypeArbitrary()
    {
        return Arb.From(Gen.Elements(BatchOperationType.Put, BatchOperationType.Delete));
    }

    /// <summary>
    /// Operation types for transaction testing.
    /// </summary>
    public enum OperationType
    {
        Put,
        Update,
        Delete
    }

    /// <summary>
    /// Operation types for batch testing (Update not supported in BatchWriteItem).
    /// </summary>
    public enum BatchOperationType
    {
        Put,
        Delete
    }

    #endregion
}
