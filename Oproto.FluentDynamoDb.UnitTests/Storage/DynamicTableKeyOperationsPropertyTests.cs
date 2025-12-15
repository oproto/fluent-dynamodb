using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

/// <summary>
/// Property-based tests for DynamicTable key operations.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class DynamicTableKeyOperationsPropertyTests
{
    /// <summary>
    /// Generator for valid table names (non-empty, non-whitespace strings).
    /// </summary>
    private static Arbitrary<string> ValidTableNameArb()
    {
        return Arb.Default.NonEmptyString()
            .Filter(s => !string.IsNullOrWhiteSpace(s.Get))
            .Convert(s => s.Get, s => NonEmptyString.NewNonEmptyString(s)!);
    }

    /// <summary>
    /// Generator for valid key values (non-empty, non-whitespace strings).
    /// </summary>
    private static Arbitrary<string> ValidKeyValueArb()
    {
        return Arb.Default.NonEmptyString()
            .Filter(s => !string.IsNullOrWhiteSpace(s.Get))
            .Convert(s => s.Get, s => NonEmptyString.NewNonEmptyString(s)!);
    }
    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 3: DynamicTable key operations consistency**
    /// 
    /// For any DynamicTable with configured key options, Get/Delete/Update operations using typed
    /// key parameters should produce the same results as operations using equivalent AttributeValue parameters.
    /// **Validates: Requirements 5.2, 5.3, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAsync_StringKey_ProducesSameRequestAsAttributeValue()
    {
        return Prop.ForAll(
            ValidKeyValueArb(),
            ValidTableNameArb(),
            (pkValue, tableName) =>
            {
                // Arrange
                GetItemRequest? typedRequest = null;
                GetItemRequest? rawRequest = null;

                var mockClient = Substitute.For<IAmazonDynamoDB>();
                mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        var request = callInfo.Arg<GetItemRequest>();
                        if (typedRequest == null)
                            typedRequest = request;
                        else
                            rawRequest = request;
                        return Task.FromResult(new GetItemResponse { Item = null });
                    });

                var keyOptions = new DynamicTableKeyOptions
                {
                    PartitionKeyName = "pk",
                    PartitionKeyType = ScalarAttributeType.S
                };

                var table = new DynamicTable(mockClient, tableName, keyOptions);

                // Act - call with typed string key
                table.GetAsync(pkValue).GetAwaiter().GetResult();

                // Act - call with raw AttributeValue
                table.GetAsync(new AttributeValue { S = pkValue }).GetAwaiter().GetResult();

                // Assert - both requests should have equivalent keys
                var typedKeyCorrect = typedRequest?.Key != null &&
                                      typedRequest.Key.ContainsKey("pk") &&
                                      typedRequest.Key["pk"].S == pkValue;

                var rawKeyCorrect = rawRequest?.Key != null &&
                                    rawRequest.Key.ContainsKey("pk") &&
                                    rawRequest.Key["pk"].S == pkValue;

                var tableNamesMatch = typedRequest?.TableName == rawRequest?.TableName &&
                                      typedRequest?.TableName == tableName;

                return (typedKeyCorrect && rawKeyCorrect && tableNamesMatch).ToProperty()
                    .Label($"Typed and raw key requests should be equivalent. " +
                           $"TypedKeyCorrect: {typedKeyCorrect}, RawKeyCorrect: {rawKeyCorrect}, " +
                           $"TableNamesMatch: {tableNamesMatch}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 3: DynamicTable key operations consistency**
    /// 
    /// For any DynamicTable with configured key options including sort key, Get operations using
    /// typed string key parameters should produce the same results as operations using equivalent
    /// AttributeValue parameters.
    /// **Validates: Requirements 5.2, 5.3, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAsync_StringKeyWithSortKey_ProducesSameRequestAsAttributeValue()
    {
        return Prop.ForAll(
            ValidKeyValueArb(),
            ValidKeyValueArb(),
            ValidTableNameArb(),
            (pkValue, skValue, tableName) =>
            {
                // Arrange
                GetItemRequest? typedRequest = null;
                GetItemRequest? rawRequest = null;

                var mockClient = Substitute.For<IAmazonDynamoDB>();
                mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        var request = callInfo.Arg<GetItemRequest>();
                        if (typedRequest == null)
                            typedRequest = request;
                        else
                            rawRequest = request;
                        return Task.FromResult(new GetItemResponse { Item = null });
                    });

                var keyOptions = new DynamicTableKeyOptions
                {
                    PartitionKeyName = "pk",
                    PartitionKeyType = ScalarAttributeType.S,
                    SortKeyName = "sk",
                    SortKeyType = ScalarAttributeType.S
                };

                var table = new DynamicTable(mockClient, tableName, keyOptions);

                // Act - call with typed string keys
                table.GetAsync(pkValue, skValue).GetAwaiter().GetResult();

                // Act - call with raw AttributeValues
                table.GetAsync(
                    new AttributeValue { S = pkValue },
                    new AttributeValue { S = skValue }
                ).GetAwaiter().GetResult();

                // Assert - both requests should have equivalent keys
                var typedPkCorrect = typedRequest?.Key != null &&
                                     typedRequest.Key.ContainsKey("pk") &&
                                     typedRequest.Key["pk"].S == pkValue;

                var typedSkCorrect = typedRequest?.Key != null &&
                                     typedRequest.Key.ContainsKey("sk") &&
                                     typedRequest.Key["sk"].S == skValue;

                var rawPkCorrect = rawRequest?.Key != null &&
                                   rawRequest.Key.ContainsKey("pk") &&
                                   rawRequest.Key["pk"].S == pkValue;

                var rawSkCorrect = rawRequest?.Key != null &&
                                   rawRequest.Key.ContainsKey("sk") &&
                                   rawRequest.Key["sk"].S == skValue;

                return (typedPkCorrect && typedSkCorrect && rawPkCorrect && rawSkCorrect).ToProperty()
                    .Label($"Typed and raw key requests with sort key should be equivalent. " +
                           $"TypedPkCorrect: {typedPkCorrect}, TypedSkCorrect: {typedSkCorrect}, " +
                           $"RawPkCorrect: {rawPkCorrect}, RawSkCorrect: {rawSkCorrect}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 3: DynamicTable key operations consistency**
    /// 
    /// For any DynamicTable with configured key options, Delete operations using typed
    /// key parameters should produce the same results as operations using equivalent AttributeValue parameters.
    /// **Validates: Requirements 5.2, 5.3, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeleteAsync_StringKey_ProducesSameRequestAsAttributeValue()
    {
        return Prop.ForAll(
            ValidKeyValueArb(),
            ValidTableNameArb(),
            (pkValue, tableName) =>
            {
                // Arrange
                DeleteItemRequest? typedRequest = null;
                DeleteItemRequest? rawRequest = null;

                var mockClient = Substitute.For<IAmazonDynamoDB>();
                mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        var request = callInfo.Arg<DeleteItemRequest>();
                        if (typedRequest == null)
                            typedRequest = request;
                        else
                            rawRequest = request;
                        return Task.FromResult(new DeleteItemResponse());
                    });

                var keyOptions = new DynamicTableKeyOptions
                {
                    PartitionKeyName = "pk",
                    PartitionKeyType = ScalarAttributeType.S
                };

                var table = new DynamicTable(mockClient, tableName, keyOptions);

                // Act - call with typed string key
                table.DeleteAsync(pkValue).GetAwaiter().GetResult();

                // Act - call with raw AttributeValue
                table.DeleteAsync(new AttributeValue { S = pkValue }).GetAwaiter().GetResult();

                // Assert - both requests should have equivalent keys
                var typedKeyCorrect = typedRequest?.Key != null &&
                                      typedRequest.Key.ContainsKey("pk") &&
                                      typedRequest.Key["pk"].S == pkValue;

                var rawKeyCorrect = rawRequest?.Key != null &&
                                    rawRequest.Key.ContainsKey("pk") &&
                                    rawRequest.Key["pk"].S == pkValue;

                var tableNamesMatch = typedRequest?.TableName == rawRequest?.TableName &&
                                      typedRequest?.TableName == tableName;

                return (typedKeyCorrect && rawKeyCorrect && tableNamesMatch).ToProperty()
                    .Label($"Typed and raw key delete requests should be equivalent. " +
                           $"TypedKeyCorrect: {typedKeyCorrect}, RawKeyCorrect: {rawKeyCorrect}, " +
                           $"TableNamesMatch: {tableNamesMatch}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 3: DynamicTable key operations consistency**
    /// 
    /// For any DynamicTable with configured numeric key options, Get operations using typed
    /// long key parameters should produce the same results as operations using equivalent AttributeValue parameters.
    /// **Validates: Requirements 5.2, 5.3, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAsync_NumericKey_ProducesSameRequestAsAttributeValue()
    {
        return Prop.ForAll(
            Arb.Default.Int64(),
            ValidTableNameArb(),
            (pkValue, tableName) =>
            {
                // Arrange
                GetItemRequest? typedRequest = null;
                GetItemRequest? rawRequest = null;

                var mockClient = Substitute.For<IAmazonDynamoDB>();
                mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        var request = callInfo.Arg<GetItemRequest>();
                        if (typedRequest == null)
                            typedRequest = request;
                        else
                            rawRequest = request;
                        return Task.FromResult(new GetItemResponse { Item = null });
                    });

                var keyOptions = new DynamicTableKeyOptions
                {
                    PartitionKeyName = "id",
                    PartitionKeyType = ScalarAttributeType.N
                };

                var table = new DynamicTable(mockClient, tableName, keyOptions);

                // Act - call with typed long key
                table.GetAsync(pkValue).GetAwaiter().GetResult();

                // Act - call with raw AttributeValue
                table.GetAsync(new AttributeValue { N = pkValue.ToString() }).GetAwaiter().GetResult();

                // Assert - both requests should have equivalent keys
                var typedKeyCorrect = typedRequest?.Key != null &&
                                      typedRequest.Key.ContainsKey("id") &&
                                      typedRequest.Key["id"].N == pkValue.ToString();

                var rawKeyCorrect = rawRequest?.Key != null &&
                                    rawRequest.Key.ContainsKey("id") &&
                                    rawRequest.Key["id"].N == pkValue.ToString();

                return (typedKeyCorrect && rawKeyCorrect).ToProperty()
                    .Label($"Typed and raw numeric key requests should be equivalent. " +
                           $"TypedKeyCorrect: {typedKeyCorrect}, RawKeyCorrect: {rawKeyCorrect}, " +
                           $"PkValue: {pkValue}");
            });
    }


    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 3: DynamicTable key operations consistency**
    /// 
    /// For any DynamicTable with configured key options, Update builder operations using typed
    /// key parameters should produce the same key configuration as operations using equivalent AttributeValue parameters.
    /// **Validates: Requirements 5.2, 5.3, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Update_StringKey_ProducesSameKeyAsAttributeValue()
    {
        return Prop.ForAll(
            ValidKeyValueArb(),
            ValidTableNameArb(),
            (pkValue, tableName) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();

                var keyOptions = new DynamicTableKeyOptions
                {
                    PartitionKeyName = "pk",
                    PartitionKeyType = ScalarAttributeType.S
                };

                var table = new DynamicTable(mockClient, tableName, keyOptions);

                // Act - get builders with typed and raw keys
                var typedBuilder = table.Update(pkValue);
                var rawBuilder = table.Update(new AttributeValue { S = pkValue });

                // Get the underlying requests to compare keys
                var typedRequest = typedBuilder.ToUpdateItemRequest();
                var rawRequest = rawBuilder.ToUpdateItemRequest();

                // Assert - both requests should have equivalent keys
                var typedKeyCorrect = typedRequest.Key != null &&
                                      typedRequest.Key.ContainsKey("pk") &&
                                      typedRequest.Key["pk"].S == pkValue;

                var rawKeyCorrect = rawRequest.Key != null &&
                                    rawRequest.Key.ContainsKey("pk") &&
                                    rawRequest.Key["pk"].S == pkValue;

                var tableNamesMatch = typedRequest.TableName == rawRequest.TableName &&
                                      typedRequest.TableName == tableName;

                return (typedKeyCorrect && rawKeyCorrect && tableNamesMatch).ToProperty()
                    .Label($"Typed and raw key update builders should have equivalent keys. " +
                           $"TypedKeyCorrect: {typedKeyCorrect}, RawKeyCorrect: {rawKeyCorrect}, " +
                           $"TableNamesMatch: {tableNamesMatch}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 3: DynamicTable key operations consistency**
    /// 
    /// For any DynamicTable without configured key options, calling typed key methods should throw
    /// InvalidOperationException.
    /// **Validates: Requirements 5.2, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedKeyMethods_WithoutKeyOptions_ThrowsInvalidOperationException()
    {
        return Prop.ForAll(
            ValidKeyValueArb(),
            ValidTableNameArb(),
            (pkValue, tableName) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var table = new DynamicTable(mockClient, tableName); // No key options

                // Act & Assert
                var getThrows = false;
                var deleteThrows = false;
                var updateThrows = false;

                try
                {
                    table.GetAsync(pkValue).GetAwaiter().GetResult();
                }
                catch (InvalidOperationException)
                {
                    getThrows = true;
                }

                try
                {
                    table.DeleteAsync(pkValue).GetAwaiter().GetResult();
                }
                catch (InvalidOperationException)
                {
                    deleteThrows = true;
                }

                try
                {
                    table.Update(pkValue);
                }
                catch (InvalidOperationException)
                {
                    updateThrows = true;
                }

                return (getThrows && deleteThrows && updateThrows).ToProperty()
                    .Label($"Typed key methods without KeyOptions should throw InvalidOperationException. " +
                           $"GetThrows: {getThrows}, DeleteThrows: {deleteThrows}, UpdateThrows: {updateThrows}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 3: DynamicTable key operations consistency**
    /// 
    /// For any DynamicTable with configured key options but no sort key, calling methods with sort key
    /// should throw InvalidOperationException.
    /// **Validates: Requirements 5.2, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortKeyMethods_WithoutSortKeyConfig_ThrowsInvalidOperationException()
    {
        return Prop.ForAll(
            ValidKeyValueArb(),
            ValidKeyValueArb(),
            ValidTableNameArb(),
            (pkValue, skValue, tableName) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var keyOptions = new DynamicTableKeyOptions
                {
                    PartitionKeyName = "pk",
                    PartitionKeyType = ScalarAttributeType.S
                    // No sort key configured
                };
                var table = new DynamicTable(mockClient, tableName, keyOptions);

                // Act & Assert
                var getThrows = false;
                var deleteThrows = false;
                var updateThrows = false;

                try
                {
                    table.GetAsync(pkValue, skValue).GetAwaiter().GetResult();
                }
                catch (InvalidOperationException)
                {
                    getThrows = true;
                }

                try
                {
                    table.DeleteAsync(pkValue, skValue).GetAwaiter().GetResult();
                }
                catch (InvalidOperationException)
                {
                    deleteThrows = true;
                }

                try
                {
                    table.Update(pkValue, skValue);
                }
                catch (InvalidOperationException)
                {
                    updateThrows = true;
                }

                return (getThrows && deleteThrows && updateThrows).ToProperty()
                    .Label($"Sort key methods without sort key config should throw InvalidOperationException. " +
                           $"GetThrows: {getThrows}, DeleteThrows: {deleteThrows}, UpdateThrows: {updateThrows}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 3: DynamicTable key operations consistency**
    /// 
    /// For any DynamicTable, raw AttributeValue key methods should always work regardless of KeyOptions.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RawKeyMethods_AlwaysWork_RegardlessOfKeyOptions()
    {
        return Prop.ForAll(
            ValidKeyValueArb(),
            ValidTableNameArb(),
            (pkValue, tableName) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(new GetItemResponse { Item = null }));
                mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(new DeleteItemResponse()));

                // Table without key options
                var table = new DynamicTable(mockClient, tableName);

                // Act & Assert - raw methods should not throw
                var getWorks = true;
                var deleteWorks = true;
                var updateWorks = true;

                try
                {
                    table.GetAsync(new AttributeValue { S = pkValue }).GetAwaiter().GetResult();
                }
                catch
                {
                    getWorks = false;
                }

                try
                {
                    table.DeleteAsync(new AttributeValue { S = pkValue }).GetAwaiter().GetResult();
                }
                catch
                {
                    deleteWorks = false;
                }

                try
                {
                    var builder = table.Update(new AttributeValue { S = pkValue });
                    updateWorks = builder != null;
                }
                catch
                {
                    updateWorks = false;
                }

                return (getWorks && deleteWorks && updateWorks).ToProperty()
                    .Label($"Raw key methods should always work. " +
                           $"GetWorks: {getWorks}, DeleteWorks: {deleteWorks}, UpdateWorks: {updateWorks}");
            });
    }
}
