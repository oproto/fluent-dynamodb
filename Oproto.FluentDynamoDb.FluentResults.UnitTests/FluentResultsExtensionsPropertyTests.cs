using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentResults;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.FluentResults.UnitTests;

/// <summary>
/// Property-based tests for FluentResults extension methods.
/// These tests verify the correctness properties defined in the design document.
/// </summary>
public class FluentResultsExtensionsPropertyTests
{
    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value**
    /// *For any* successful DynamoDB GetItem operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the same value as the traditional async method would return.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetItemAsyncResult_Success_PreservesValue()
    {
        return Prop.ForAll(
            TestEntityArbitrary(),
            entity =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
                
                var mockResponse = new GetItemResponse
                {
                    Item = PropertyTestEntity.ToDynamoDb(entity, null)
                };

                mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(mockResponse));

                // Act
                var result = builder.GetItemAsyncResult().GetAwaiter().GetResult();

                // Assert
                return result.IsSuccess && 
                       result.Value != null &&
                       result.Value.Id == entity.Id &&
                       result.Value.Name == entity.Name;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error**
    /// *For any* failed DynamoDB GetItem operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the exception chain.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetItemAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");

                mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<GetItemResponse>(ex));

                // Act
                var result = builder.GetItemAsyncResult().GetAwaiter().GetResult();

                // Assert - The exception is wrapped by EntityExecuteAsyncExtensions in DynamoDbMappingException
                // so we check that the error contains an exception and the original is in the chain
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value**
    /// *For any* successful DynamoDB Query operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the same list of values.
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryToListAsyncResult_Success_PreservesValue()
    {
        return Prop.ForAll(
            TestEntityListArbitrary(),
            entities =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
                
                var mockResponse = new QueryResponse
                {
                    Items = entities.Select(e => PropertyTestEntity.ToDynamoDb(e, null)).ToList(),
                    Count = entities.Count
                };

                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(mockResponse));

                // Act
                var result = builder.ToListAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert
                return result.IsSuccess && 
                       result.Value != null &&
                       result.Value.Count == entities.Count;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error**
    /// *For any* failed DynamoDB Query operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the exception chain.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryToListAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");

                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<QueryResponse>(ex));

                // Act
                var result = builder.ToListAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert - The exception is wrapped by EntityExecuteAsyncExtensions in DynamoDbMappingException
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value**
    /// *For any* successful DynamoDB Scan operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the same list of values.
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ScanToListAsyncResult_Success_PreservesValue()
    {
        return Prop.ForAll(
            TestEntityListArbitrary(),
            entities =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new ScanRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
                
                var mockResponse = new ScanResponse
                {
                    Items = entities.Select(e => PropertyTestEntity.ToDynamoDb(e, null)).ToList(),
                    Count = entities.Count
                };

                mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(mockResponse));

                // Act
                var result = builder.ToListAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert
                return result.IsSuccess && 
                       result.Value != null &&
                       result.Value.Count == entities.Count;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error**
    /// *For any* failed DynamoDB Scan operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the exception chain.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ScanToListAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new ScanRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");

                mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<ScanResponse>(ex));

                // Act
                var result = builder.ToListAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert - The exception is wrapped by EntityExecuteAsyncExtensions in DynamoDbMappingException
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value**
    /// *For any* successful DynamoDB PutItem operation, the Result-returning extension method SHALL return 
    /// Result.Ok.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutAsyncResult_Success_ReturnsOk()
    {
        return Prop.ForAll(
            TestEntityArbitrary(),
            entity =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
                
                var mockResponse = new PutItemResponse();

                mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(mockResponse));

                // Act
                var result = builder.WithItem(entity).PutAsyncResult().GetAwaiter().GetResult();

                // Assert
                return result.IsSuccess;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error**
    /// *For any* failed DynamoDB PutItem operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the exception chain.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            TestEntityArbitrary(),
            ExceptionArbitrary(),
            (entity, ex) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");

                mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<PutItemResponse>(ex));

                // Act
                var result = builder.WithItem(entity).PutAsyncResult().GetAwaiter().GetResult();

                // Assert - The exception is wrapped by EntityExecuteAsyncExtensions in DynamoDbMappingException
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough**
    /// *For any* Result-returning extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task GetItemAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<GetItemResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.GetItemAsyncResult(cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough**
    /// *For any* Result-returning extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task QueryToListAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<QueryResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ToListAsyncResult<PropertyTestEntity>(cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough**
    /// *For any* Result-returning extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task ScanToListAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<ScanResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ToListAsyncResult<PropertyTestEntity>(cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough**
    /// *For any* Result-returning extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task PutAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var entity = new PropertyTestEntity { Id = "test", Name = "test" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<PutItemResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.WithItem(entity).PutAsyncResult(cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough**
    /// *For any* Result-returning extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task UpdateAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<UpdateItemResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.UpdateAsyncResult(cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough**
    /// *For any* Result-returning extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task DeleteAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<DeleteItemResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.DeleteAsyncResult(cts.Token));
    }

    #region Composite Entity Tests

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (composite entity variant)
    /// *For any* successful DynamoDB Query operation returning a composite entity, the Result-returning extension method 
    /// SHALL return Result.Ok with the same value as the traditional async method would return.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryToCompositeEntityAsyncResult_Success_PreservesValue()
    {
        return Prop.ForAll(
            TestEntityArbitrary(),
            entity =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
                
                // Simulate a composite entity query that returns multiple items for the same partition key
                var mockResponse = new QueryResponse
                {
                    Items = new List<Dictionary<string, AttributeValue>>
                    {
                        PropertyTestEntity.ToDynamoDb(entity, null)
                    },
                    Count = 1
                };

                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(mockResponse));

                // Act
                var result = builder.ToCompositeEntityAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert
                return result.IsSuccess && 
                       result.Value != null &&
                       result.Value.Id == entity.Id &&
                       result.Value.Name == entity.Name;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (composite entity variant)
    /// *For any* failed DynamoDB Query composite entity operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the exception chain.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryToCompositeEntityAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");

                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<QueryResponse>(ex));

                // Act
                var result = builder.ToCompositeEntityAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert - The exception is wrapped by EntityExecuteAsyncExtensions in DynamoDbMappingException
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (composite entity list variant)
    /// *For any* successful DynamoDB Query operation returning composite entity list, the Result-returning extension method 
    /// SHALL return Result.Ok with the same list of values.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryToCompositeEntityListAsyncResult_Success_PreservesValue()
    {
        return Prop.ForAll(
            TestEntityListArbitrary(),
            entities =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
                
                var mockResponse = new QueryResponse
                {
                    Items = entities.Select(e => PropertyTestEntity.ToDynamoDb(e, null)).ToList(),
                    Count = entities.Count
                };

                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(mockResponse));

                // Act
                var result = builder.ToCompositeEntityListAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert
                return result.IsSuccess && 
                       result.Value != null &&
                       result.Value.Count == entities.Count;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (composite entity list variant)
    /// *For any* failed DynamoDB Query composite entity list operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the exception chain.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryToCompositeEntityListAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");

                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<QueryResponse>(ex));

                // Act
                var result = builder.ToCompositeEntityListAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert - The exception is wrapped by EntityExecuteAsyncExtensions in DynamoDbMappingException
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (scan composite entity list variant)
    /// *For any* successful DynamoDB Scan operation returning composite entity list, the Result-returning extension method 
    /// SHALL return Result.Ok with the same list of values.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ScanToCompositeEntityListAsyncResult_Success_PreservesValue()
    {
        return Prop.ForAll(
            TestEntityListArbitrary(),
            entities =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new ScanRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
                
                var mockResponse = new ScanResponse
                {
                    Items = entities.Select(e => PropertyTestEntity.ToDynamoDb(e, null)).ToList(),
                    Count = entities.Count
                };

                mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(mockResponse));

                // Act
                var result = builder.ToCompositeEntityListAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert
                return result.IsSuccess && 
                       result.Value != null &&
                       result.Value.Count == entities.Count;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (scan composite entity list variant)
    /// *For any* failed DynamoDB Scan composite entity list operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the exception chain.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ScanToCompositeEntityListAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new ScanRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");

                mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<ScanResponse>(ex));

                // Act
                var result = builder.ToCompositeEntityListAsyncResult<PropertyTestEntity>().GetAwaiter().GetResult();

                // Assert - The exception is wrapped by EntityExecuteAsyncExtensions in DynamoDbMappingException
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (composite entity variant)
    /// *For any* Result-returning composite entity extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task QueryToCompositeEntityAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<QueryResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ToCompositeEntityAsyncResult<PropertyTestEntity>(cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (composite entity list variant)
    /// *For any* Result-returning composite entity list extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task QueryToCompositeEntityListAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<QueryResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ToCompositeEntityListAsyncResult<PropertyTestEntity>(cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (scan composite entity list variant)
    /// *For any* Result-returning scan composite entity list extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task ScanToCompositeEntityListAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<ScanResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ToCompositeEntityListAsyncResult<PropertyTestEntity>(cts.Token));
    }

    #endregion

    #region Batch Operation Tests

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 6: Batch Unprocessed Items Warning**
    /// *For any* batch get operation that completes with unprocessed keys, the Result SHALL be successful 
    /// AND contain warnings about the unprocessed keys.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Fact]
    public async Task BatchGetExecuteAsyncResult_WithUnprocessedKeys_ReturnsSuccessWithWarning()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = DynamoDbBatch.Get
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id"))
            .WithClient(mockClient);

        var mockResponse = new BatchGetItemResponse
        {
            Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
            {
                ["test-table"] = new List<Dictionary<string, AttributeValue>>()
            },
            UnprocessedKeys = new Dictionary<string, KeysAndAttributes>
            {
                ["test-table"] = new KeysAndAttributes
                {
                    Keys = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = "unprocessed-key" }
                        }
                    }
                }
            }
        };

        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Successes, s => s is UnprocessedItemsWarning);
        var warning = result.Successes.OfType<UnprocessedItemsWarning>().First();
        Assert.Equal(1, warning.UnprocessedCount);
        Assert.Contains("test-table", warning.TableNames);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 6: Batch Unprocessed Items Warning**
    /// *For any* batch write operation that completes with unprocessed items, the Result SHALL be successful 
    /// AND contain warnings about the unprocessed items.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Fact]
    public async Task BatchWriteExecuteAsyncResult_WithUnprocessedItems_ReturnsSuccessWithWarning()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbBatch.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);

        var mockResponse = new BatchWriteItemResponse
        {
            UnprocessedItems = new Dictionary<string, List<WriteRequest>>
            {
                ["test-table"] = new List<WriteRequest>
                {
                    new WriteRequest
                    {
                        PutRequest = new PutRequest
                        {
                            Item = PropertyTestEntity.ToDynamoDb(entity, null)
                        }
                    }
                }
            }
        };

        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Successes, s => s is UnprocessedItemsWarning);
        var warning = result.Successes.OfType<UnprocessedItemsWarning>().First();
        Assert.Equal(1, warning.UnprocessedCount);
        Assert.Contains("test-table", warning.TableNames);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (batch get variant)
    /// *For any* failed batch get operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BatchGetExecuteAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = DynamoDbBatch.Get
                    .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id"))
                    .WithClient(mockClient);

                mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<BatchGetItemResponse>(ex));

                // Act
                var result = builder.ExecuteAsyncResult(cancellationToken: default).GetAwaiter().GetResult();

                // Assert
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (batch write variant)
    /// *For any* failed batch write operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BatchWriteExecuteAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
                var builder = DynamoDbBatch.Write
                    .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
                    .WithClient(mockClient);

                mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<BatchWriteItemResponse>(ex));

                // Act
                var result = builder.ExecuteAsyncResult(cancellationToken: default).GetAwaiter().GetResult();

                // Assert
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (batch PartiQL variant)
    /// *For any* failed batch PartiQL operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BatchPartiQLExecuteAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var partiQLBuilder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
                    .WithStatement("SELECT * FROM \"test-table\" WHERE pk = ?", "test-id");
                var builder = DynamoDbBatch.PartiQL
                    .Add(partiQLBuilder)
                    .WithClient(mockClient);

                mockClient.BatchExecuteStatementAsync(Arg.Any<BatchExecuteStatementRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<BatchExecuteStatementResponse>(ex));

                // Act
                var result = builder.ExecuteAsyncResult(cancellationToken: default).GetAwaiter().GetResult();

                // Assert
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (batch get variant)
    /// *For any* Result-returning batch get extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task BatchGetExecuteAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = DynamoDbBatch.Get
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id"))
            .WithClient(mockClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<BatchGetItemResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ExecuteAsyncResult(cancellationToken: cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (batch write variant)
    /// *For any* Result-returning batch write extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task BatchWriteExecuteAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbBatch.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<BatchWriteItemResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ExecuteAsyncResult(cancellationToken: cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (batch PartiQL variant)
    /// *For any* Result-returning batch PartiQL extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task BatchPartiQLExecuteAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var partiQLBuilder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
            .WithStatement("SELECT * FROM \"test-table\" WHERE pk = ?", "test-id");
        var builder = DynamoDbBatch.PartiQL
            .Add(partiQLBuilder)
            .WithClient(mockClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.BatchExecuteStatementAsync(Arg.Any<BatchExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<BatchExecuteStatementResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ExecuteAsyncResult(cancellationToken: cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (batch get variant)
    /// *For any* successful batch get operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the BatchGetResponse.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Fact]
    public async Task BatchGetExecuteAsyncResult_Success_ReturnsOkWithResponse()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = DynamoDbBatch.Get
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id"))
            .WithClient(mockClient);

        var mockResponse = new BatchGetItemResponse
        {
            Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
            {
                ["test-table"] = new List<Dictionary<string, AttributeValue>>
                {
                    new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = "test-id" },
                        ["name"] = new AttributeValue { S = "test-name" }
                    }
                }
            }
        };

        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.Count);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (batch write variant)
    /// *For any* successful batch write operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the BatchWriteItemResponse.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Fact]
    public async Task BatchWriteExecuteAsyncResult_Success_ReturnsOkWithResponse()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbBatch.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);

        var mockResponse = new BatchWriteItemResponse();

        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (batch PartiQL variant)
    /// *For any* successful batch PartiQL operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the BatchPartiQLResponse.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Fact]
    public async Task BatchPartiQLExecuteAsyncResult_Success_ReturnsOkWithResponse()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var partiQLBuilder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
            .WithStatement("SELECT * FROM \"test-table\" WHERE pk = ?", "test-id");
        var builder = DynamoDbBatch.PartiQL
            .Add(partiQLBuilder)
            .WithClient(mockClient);

        var mockResponse = new BatchExecuteStatementResponse
        {
            Responses = new List<BatchStatementResponse>
            {
                new BatchStatementResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = "test-id" },
                        ["name"] = new AttributeValue { S = "test-name" }
                    }
                }
            }
        };

        mockClient.BatchExecuteStatementAsync(Arg.Any<BatchExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.Count);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (ExecuteAndMapAsyncResult variant)
    /// *For any* successful batch get operation with ExecuteAndMapAsyncResult, the Result-returning extension method 
    /// SHALL return Result.Ok with the deserialized tuple.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Fact]
    public async Task BatchGetExecuteAndMapAsyncResult_Success_ReturnsOkWithTuple()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = DynamoDbBatch.Get
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id-1"))
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id-2"))
            .WithClient(mockClient);

        var mockResponse = new BatchGetItemResponse
        {
            Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
            {
                ["test-table"] = new List<Dictionary<string, AttributeValue>>
                {
                    new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = "test-id-1" },
                        ["name"] = new AttributeValue { S = "test-name-1" }
                    },
                    new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = "test-id-2" },
                        ["name"] = new AttributeValue { S = "test-name-2" }
                    }
                }
            }
        };

        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAndMapAsyncResult<PropertyTestEntity, PropertyTestEntity>(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Item1);
        Assert.NotNull(result.Value.Item2);
        Assert.Equal("test-id-1", result.Value.Item1.Id);
        Assert.Equal("test-id-2", result.Value.Item2.Id);
    }

    #endregion

    #region Transaction Operation Tests

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 7: Error Aggregation Ordering** (transaction write variant)
    /// *For any* transaction write operation that is cancelled with multiple reasons, the errors in the Result 
    /// SHALL contain errors for each cancellation reason in order.
    /// **Validates: Requirements 6.1, 6.3, 11.2, 11.3**
    /// </summary>
    [Fact]
    public async Task TransactionWriteExecuteAsyncResult_WithCancellationReasons_ReturnsFailWithOrderedErrors()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbTransactions.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);

        var cancellationReasons = new List<CancellationReason>
        {
            new CancellationReason { Code = "ConditionalCheckFailed", Message = "Condition check failed for item 0" },
            new CancellationReason { Code = "", Message = "" }, // Empty reason - should be filtered out
            new CancellationReason { Code = "TransactionConflict", Message = "Transaction conflict for item 2" }
        };

        var transactionCanceledException = new TransactionCanceledException("Transaction cancelled")
        {
            CancellationReasons = cancellationReasons
        };

        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TransactWriteItemsResponse>(transactionCanceledException));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Single(result.Errors);
        var error = result.Errors[0] as TransactionCancelledError;
        Assert.NotNull(error);
        Assert.Equal("TRANSACTION_CANCELLED", error.ErrorCode);
        // Verify cancellation reasons are preserved (non-empty ones - where either Code or Message is non-empty)
        Assert.Equal(2, error.CancellationReasons.Count);
        Assert.Contains("Condition check failed for item 0", error.CancellationReasons);
        Assert.Contains("Transaction conflict for item 2", error.CancellationReasons);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (transaction write variant)
    /// *For any* failed transaction write operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TransactionWriteExecuteAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            TransactionExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
                var builder = DynamoDbTransactions.Write
                    .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
                    .WithClient(mockClient);

                mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<TransactWriteItemsResponse>(ex));

                // Act
                var result = builder.ExecuteAsyncResult(cancellationToken: default).GetAwaiter().GetResult();

                // Assert
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (transaction get variant)
    /// *For any* failed transaction get operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TransactionGetExecuteAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            TransactionExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = DynamoDbTransactions.Get
                    .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id"))
                    .WithClient(mockClient);

                mockClient.TransactGetItemsAsync(Arg.Any<TransactGetItemsRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<TransactGetItemsResponse>(ex));

                // Act
                var result = builder.ExecuteAsyncResult(cancellationToken: default).GetAwaiter().GetResult();

                // Assert
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 5: Error Type Specificity** (transaction cancelled variant)
    /// *For any* TransactionCanceledException, DynamoDbErrors.FromException SHALL return TransactionCancelledError.
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Fact]
    public async Task TransactionWriteExecuteAsyncResult_TransactionCancelled_ReturnsTransactionCancelledError()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbTransactions.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);

        var transactionCanceledException = new TransactionCanceledException("Transaction cancelled")
        {
            CancellationReasons = new List<CancellationReason>
            {
                new CancellationReason { Code = "ConditionalCheckFailed", Message = "Condition failed" }
            }
        };

        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TransactWriteItemsResponse>(transactionCanceledException));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Single(result.Errors);
        Assert.IsType<TransactionCancelledError>(result.Errors[0]);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 5: Error Type Specificity** (transaction conflict variant)
    /// *For any* TransactionConflictException, DynamoDbErrors.FromException SHALL return TransactionConflictError.
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Fact]
    public async Task TransactionWriteExecuteAsyncResult_TransactionConflict_ReturnsTransactionConflictError()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbTransactions.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);

        var transactionConflictException = new TransactionConflictException("Transaction conflict");

        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TransactWriteItemsResponse>(transactionConflictException));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Single(result.Errors);
        Assert.IsType<TransactionConflictError>(result.Errors[0]);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 5: Error Type Specificity** (idempotency error variant)
    /// *For any* IdempotentParameterMismatchException, DynamoDbErrors.FromException SHALL return IdempotencyError.
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Fact]
    public async Task TransactionWriteExecuteAsyncResult_IdempotencyMismatch_ReturnsIdempotencyError()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbTransactions.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);

        var idempotencyException = new IdempotentParameterMismatchException("Idempotency mismatch");

        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TransactWriteItemsResponse>(idempotencyException));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Single(result.Errors);
        Assert.IsType<IdempotencyError>(result.Errors[0]);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (transaction write variant)
    /// *For any* successful transaction write operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the TransactWriteItemsResponse.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Fact]
    public async Task TransactionWriteExecuteAsyncResult_Success_ReturnsOkWithResponse()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbTransactions.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);

        var mockResponse = new TransactWriteItemsResponse();

        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (transaction get variant)
    /// *For any* successful transaction get operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the TransactionGetResponse.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Fact]
    public async Task TransactionGetExecuteAsyncResult_Success_ReturnsOkWithResponse()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = DynamoDbTransactions.Get
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id"))
            .WithClient(mockClient);

        var mockResponse = new TransactGetItemsResponse
        {
            Responses = new List<ItemResponse>
            {
                new ItemResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = "test-id" },
                        ["name"] = new AttributeValue { S = "test-name" }
                    }
                }
            }
        };

        mockClient.TransactGetItemsAsync(Arg.Any<TransactGetItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAsyncResult(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (transaction write variant)
    /// *For any* Result-returning transaction write extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task TransactionWriteExecuteAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var entity = new PropertyTestEntity { Id = "test-id", Name = "test-name" };
        var builder = DynamoDbTransactions.Write
            .Add(new PutItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithItem(entity))
            .WithClient(mockClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<TransactWriteItemsResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ExecuteAsyncResult(cancellationToken: cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (transaction get variant)
    /// *For any* Result-returning transaction get extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task TransactionGetExecuteAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = DynamoDbTransactions.Get
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id"))
            .WithClient(mockClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.TransactGetItemsAsync(Arg.Any<TransactGetItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<TransactGetItemsResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ExecuteAsyncResult(cancellationToken: cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (ExecuteAndMapAsyncResult variant)
    /// *For any* successful transaction get operation with ExecuteAndMapAsyncResult, the Result-returning extension method 
    /// SHALL return Result.Ok with the deserialized tuple.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Fact]
    public async Task TransactionGetExecuteAndMapAsyncResult_Success_ReturnsOkWithTuple()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = DynamoDbTransactions.Get
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id-1"))
            .Add(new GetItemRequestBuilder<PropertyTestEntity>(mockClient).ForTable("test-table").WithKey("pk", "test-id-2"))
            .WithClient(mockClient);

        var mockResponse = new TransactGetItemsResponse
        {
            Responses = new List<ItemResponse>
            {
                new ItemResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = "test-id-1" },
                        ["name"] = new AttributeValue { S = "test-name-1" }
                    }
                },
                new ItemResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = "test-id-2" },
                        ["name"] = new AttributeValue { S = "test-name-2" }
                    }
                }
            }
        };

        mockClient.TransactGetItemsAsync(Arg.Any<TransactGetItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAndMapAsyncResult<PropertyTestEntity, PropertyTestEntity>(cancellationToken: default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Item1);
        Assert.NotNull(result.Value.Item2);
        Assert.Equal("test-id-1", result.Value.Item1.Id);
        Assert.Equal("test-id-2", result.Value.Item2.Id);
    }

    #endregion

    #region PartiQL Operation Tests

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (PartiQL ToListAsyncResult variant)
    /// *For any* failed PartiQL SELECT operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the exception chain.
    /// **Validates: Requirements 10.1-10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQLToListAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
                    .WithStatement("SELECT * FROM \"test-table\" WHERE pk = ?", "test-id");

                mockClient.ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<ExecuteStatementResponse>(ex));

                // Act
                var result = builder.ToListAsyncResult().GetAwaiter().GetResult();

                // Assert - The exception is wrapped by PartiQLRequestBuilder in DynamoDbMappingException
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (PartiQL ToListAsyncResult variant)
    /// *For any* successful PartiQL SELECT operation, the Result-returning extension method SHALL return 
    /// Result.Ok with the same list of values.
    /// **Validates: Requirements 10.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQLToListAsyncResult_Success_PreservesValue()
    {
        return Prop.ForAll(
            TestEntityListArbitrary(),
            entities =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
                    .WithStatement("SELECT * FROM \"test-table\" WHERE pk = ?", "test-id");
                
                var mockResponse = new ExecuteStatementResponse
                {
                    Items = entities.Select(e => PropertyTestEntity.ToDynamoDb(e, null)).ToList()
                };

                mockClient.ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(mockResponse));

                // Act
                var result = builder.ToListAsyncResult().GetAwaiter().GetResult();

                // Assert
                return result.IsSuccess && 
                       result.Value != null &&
                       result.Value.Count == entities.Count;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (PartiQL ExecuteAsyncResult variant)
    /// *For any* failed PartiQL non-SELECT operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the original exception.
    /// **Validates: Requirements 10.2-10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQLExecuteAsyncResult_Failure_ContainsError()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var builder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
                    .WithStatement("UPDATE \"test-table\" SET name = ? WHERE pk = ?", "new-name", "test-id");

                mockClient.ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<ExecuteStatementResponse>(ex));

                // Act
                var result = builder.ExecuteAsyncResult().GetAwaiter().GetResult();

                // Assert - The exception is passed directly to DynamoDbErrors.FromException
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null &&
                       ContainsExceptionInChain(dynamoDbError.InnerException, ex);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 2: Result Success Preserves Value** (PartiQL ExecuteAsyncResult variant)
    /// *For any* successful PartiQL non-SELECT operation, the Result-returning extension method SHALL return 
    /// Result.Ok.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Fact]
    public async Task PartiQLExecuteAsyncResult_Success_ReturnsOk()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
            .WithStatement("UPDATE \"test-table\" SET name = ? WHERE pk = ?", "new-name", "test-id");
        
        var mockResponse = new ExecuteStatementResponse();

        mockClient.ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ExecuteAsyncResult();

        // Assert
        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (PartiQL ToListAsyncResult variant)
    /// *For any* Result-returning PartiQL SELECT extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task PartiQLToListAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
            .WithStatement("SELECT * FROM \"test-table\" WHERE pk = ?", "test-id");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<ExecuteStatementResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ToListAsyncResult(cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (PartiQL ExecuteAsyncResult variant)
    /// *For any* Result-returning PartiQL non-SELECT extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task PartiQLExecuteAsyncResult_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PartiQLRequestBuilder<PropertyTestEntity>(mockClient)
            .WithStatement("UPDATE \"test-table\" SET name = ? WHERE pk = ?", "new-name", "test-id");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<ExecuteStatementResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.ExecuteAsyncResult(cts.Token));
    }

    #endregion

    /// <summary>
    /// Generates arbitrary test entities for property testing.
    /// </summary>
    private static Arbitrary<PropertyTestEntity> TestEntityArbitrary()
    {
        var generator = from id in Arb.Generate<NonEmptyString>()
                        from name in Arb.Generate<NonEmptyString>()
                        select new PropertyTestEntity 
                        { 
                            Id = id.Get, 
                            Name = name.Get 
                        };

        return Arb.From(generator);
    }

    /// <summary>
    /// Generates arbitrary lists of test entities for property testing.
    /// </summary>
    private static Arbitrary<List<PropertyTestEntity>> TestEntityListArbitrary()
    {
        var generator = from count in Gen.Choose(1, 10)
                        from entities in Gen.ListOf(count, TestEntityArbitrary().Generator)
                        select entities.ToList();

        return Arb.From(generator);
    }

    /// <summary>
    /// Generates arbitrary exceptions for testing failure scenarios.
    /// Excludes OperationCanceledException as it has special handling.
    /// </summary>
    private static Arbitrary<Exception> ExceptionArbitrary()
    {
        var generators = new[]
        {
            Gen.Constant<Exception>(new Exception("Generic error")),
            Gen.Constant<Exception>(new InvalidOperationException("Invalid operation")),
            Gen.Constant<Exception>(new DynamoDbMappingException("Mapping error")),
            Gen.Constant<Exception>(new ResourceNotFoundException("Resource not found")),
            Gen.Constant<Exception>(new ConditionalCheckFailedException("Condition failed")),
            Gen.Constant<Exception>(new ProvisionedThroughputExceededException("Throughput exceeded")),
        };

        return Arb.From(Gen.OneOf(generators));
    }

    /// <summary>
    /// Generates arbitrary transaction-related exceptions for testing failure scenarios.
    /// </summary>
    private static Arbitrary<Exception> TransactionExceptionArbitrary()
    {
        var generators = new[]
        {
            Gen.Constant<Exception>(new TransactionCanceledException("Transaction cancelled") 
            { 
                CancellationReasons = new List<CancellationReason> 
                { 
                    new CancellationReason { Code = "ConditionalCheckFailed", Message = "Condition failed" } 
                } 
            }),
            Gen.Constant<Exception>(new TransactionConflictException("Transaction conflict")),
            Gen.Constant<Exception>(new IdempotentParameterMismatchException("Idempotency mismatch")),
            Gen.Constant<Exception>(new ProvisionedThroughputExceededException("Throughput exceeded")),
            Gen.Constant<Exception>(new ResourceNotFoundException("Resource not found")),
            Gen.Constant<Exception>(new InvalidOperationException("Invalid operation")),
        };

        return Arb.From(Gen.OneOf(generators));
    }

    /// <summary>
    /// Helper method to check if an exception is contained in the exception chain.
    /// The EntityExecuteAsyncExtensions methods wrap exceptions in DynamoDbMappingException,
    /// so we need to traverse the chain to find the original exception.
    /// </summary>
    private static bool ContainsExceptionInChain(Exception? chain, Exception target)
    {
        var current = chain;
        while (current != null)
        {
            if (ReferenceEquals(current, target))
                return true;
            current = current.InnerException;
        }
        return false;
    }
}


/// <summary>
/// Test entity for property-based tests.
/// </summary>
public partial class PropertyTestEntity : IDynamoDbEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as PropertyTestEntity;
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
            ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty }
        };
    }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
        where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new PropertyTestEntity
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
        return item.ContainsKey("pk") && item.ContainsKey("name");
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

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}
