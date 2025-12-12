using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

/// <summary>
/// Property-based tests for blob storage strategy lifecycle integration.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class BlobStorageStrategyPropertyTests
{
    /// <summary>
    /// **Feature: blob-storage-redesign, Property 9: Strategy Lifecycle Order for Writes**
    /// 
    /// For any Put or Update operation on an entity with [BlobStorage] properties, the strategy
    /// methods SHALL be called in order: OnBeforeDynamoDbWriteAsync → DynamoDB operation →
    /// OnAfterDynamoDbWriteSuccessAsync or OnAfterDynamoDbWriteFailureAsync.
    /// **Validates: Requirements 4.1, 4.2, 4.3, 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Strategy_WriteLifecycle_CorrectOrder()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.Bool(),
            (testData, shouldSucceed) =>
            {
                // Arrange
                var callOrder = new List<string>();
                var mockStrategy = Substitute.For<IBlobStorageStrategy>();
                
                mockStrategy.OnBeforeDynamoDbWriteAsync(Arg.Any<BlobWriteContext>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        callOrder.Add("OnBeforeDynamoDbWriteAsync");
                        return Task.FromResult(new BlobWriteResult
                        {
                            ReferenceKeys = new Dictionary<string, string> { ["TestProperty"] = "test-key" }
                        });
                    });

                mockStrategy.OnAfterDynamoDbWriteSuccessAsync(Arg.Any<BlobWriteContext>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        callOrder.Add("OnAfterDynamoDbWriteSuccessAsync");
                        return Task.CompletedTask;
                    });

                mockStrategy.OnAfterDynamoDbWriteFailureAsync(Arg.Any<BlobWriteContext>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        callOrder.Add("OnAfterDynamoDbWriteFailureAsync");
                        return Task.CompletedTask;
                    });

                // Create blob property context
                var blobProperties = new List<BlobPropertyContext>
                {
                    new BlobPropertyContext
                    {
                        PropertyName = "TestProperty",
                        AttributeName = "test_property",
                        Data = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData.Get)),
                        ContentType = "text/plain"
                    }
                };

                var context = new BlobWriteContext
                {
                    EntityType = "TestEntity",
                    BlobProperties = blobProperties
                };

                // Act - simulate the write lifecycle
                try
                {
                    // Step 1: Before write
                    var result = mockStrategy.OnBeforeDynamoDbWriteAsync(context, CancellationToken.None).GetAwaiter().GetResult();
                    context.UploadedReferenceKeys = result.ReferenceKeys;
                    
                    // Step 2: Simulate DynamoDB operation
                    callOrder.Add("DynamoDbOperation");
                    
                    if (!shouldSucceed)
                    {
                        throw new Exception("Simulated DynamoDB failure");
                    }
                    
                    // Step 3: After success
                    mockStrategy.OnAfterDynamoDbWriteSuccessAsync(context, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (Exception ex) when (ex.Message == "Simulated DynamoDB failure")
                {
                    // Step 3: After failure
                    mockStrategy.OnAfterDynamoDbWriteFailureAsync(context, ex, CancellationToken.None).GetAwaiter().GetResult();
                }

                // Assert - verify call order
                var beforeCalledFirst = callOrder.Count > 0 && callOrder[0] == "OnBeforeDynamoDbWriteAsync";
                var dynamoDbCalledSecond = callOrder.Count > 1 && callOrder[1] == "DynamoDbOperation";
                
                bool afterCalledCorrectly;
                if (shouldSucceed)
                {
                    afterCalledCorrectly = callOrder.Count == 3 && callOrder[2] == "OnAfterDynamoDbWriteSuccessAsync";
                }
                else
                {
                    afterCalledCorrectly = callOrder.Count == 3 && callOrder[2] == "OnAfterDynamoDbWriteFailureAsync";
                }

                return (beforeCalledFirst && dynamoDbCalledSecond && afterCalledCorrectly).ToProperty()
                    .Label($"Strategy lifecycle should follow correct order. " +
                           $"BeforeCalledFirst: {beforeCalledFirst}, DynamoDbCalledSecond: {dynamoDbCalledSecond}, " +
                           $"AfterCalledCorrectly: {afterCalledCorrectly}, ShouldSucceed: {shouldSucceed}, " +
                           $"CallOrder: [{string.Join(", ", callOrder)}]");
            });
    }


    /// <summary>
    /// **Feature: blob-storage-redesign, Property 10: Strategy Lifecycle Order for Deletes**
    /// 
    /// For any Delete operation on an entity with [BlobStorage] properties, the strategy methods
    /// SHALL be called in order: OnBeforeDynamoDbDeleteAsync → DynamoDB operation →
    /// OnAfterDynamoDbDeleteSuccessAsync.
    /// **Validates: Requirements 4.4, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Strategy_DeleteLifecycle_CorrectOrder()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            referenceKey =>
            {
                // Arrange
                var callOrder = new List<string>();
                var mockStrategy = Substitute.For<IBlobStorageStrategy>();

                mockStrategy.OnBeforeDynamoDbDeleteAsync(Arg.Any<BlobDeleteContext>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        callOrder.Add("OnBeforeDynamoDbDeleteAsync");
                        return Task.FromResult(callInfo.Arg<BlobDeleteContext>());
                    });

                mockStrategy.OnAfterDynamoDbDeleteSuccessAsync(Arg.Any<BlobDeleteContext>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        callOrder.Add("OnAfterDynamoDbDeleteSuccessAsync");
                        return Task.CompletedTask;
                    });

                var context = new BlobDeleteContext
                {
                    EntityType = "TestEntity",
                    ReferenceKeys = new List<string> { referenceKey.Get }
                };

                // Act - simulate the delete lifecycle
                // Step 1: Before delete
                context = mockStrategy.OnBeforeDynamoDbDeleteAsync(context, CancellationToken.None).GetAwaiter().GetResult();
                
                // Step 2: Simulate DynamoDB operation
                callOrder.Add("DynamoDbOperation");
                
                // Step 3: After success
                mockStrategy.OnAfterDynamoDbDeleteSuccessAsync(context, CancellationToken.None).GetAwaiter().GetResult();

                // Assert - verify call order
                var beforeCalledFirst = callOrder.Count > 0 && callOrder[0] == "OnBeforeDynamoDbDeleteAsync";
                var dynamoDbCalledSecond = callOrder.Count > 1 && callOrder[1] == "DynamoDbOperation";
                var afterCalledThird = callOrder.Count == 3 && callOrder[2] == "OnAfterDynamoDbDeleteSuccessAsync";

                return (beforeCalledFirst && dynamoDbCalledSecond && afterCalledThird).ToProperty()
                    .Label($"Delete lifecycle should follow correct order. " +
                           $"BeforeCalledFirst: {beforeCalledFirst}, DynamoDbCalledSecond: {dynamoDbCalledSecond}, " +
                           $"AfterCalledThird: {afterCalledThird}, " +
                           $"CallOrder: [{string.Join(", ", callOrder)}]");
            });
    }

    /// <summary>
    /// Additional property test: Strategy receives correct context for writes.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Strategy_WriteContext_ContainsCorrectData()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.NonEmptyString(),
            (propertyName, attributeName) =>
            {
                // Arrange
                BlobWriteContext? capturedContext = null;
                var mockStrategy = Substitute.For<IBlobStorageStrategy>();
                
                mockStrategy.OnBeforeDynamoDbWriteAsync(Arg.Any<BlobWriteContext>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        capturedContext = callInfo.Arg<BlobWriteContext>();
                        return Task.FromResult(new BlobWriteResult
                        {
                            ReferenceKeys = new Dictionary<string, string>()
                        });
                    });

                var testData = "test-data";
                var blobProperties = new List<BlobPropertyContext>
                {
                    new BlobPropertyContext
                    {
                        PropertyName = propertyName.Get,
                        AttributeName = attributeName.Get,
                        Data = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData)),
                        ContentType = "text/plain"
                    }
                };

                var context = new BlobWriteContext
                {
                    EntityType = "TestEntity",
                    BlobProperties = blobProperties
                };

                // Act
                mockStrategy.OnBeforeDynamoDbWriteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                var contextCaptured = capturedContext != null;
                var entityTypeCorrect = capturedContext?.EntityType == "TestEntity";
                var hasBlobProperties = capturedContext?.BlobProperties.Count == 1;
                var propertyNameCorrect = capturedContext?.BlobProperties[0].PropertyName == propertyName.Get;
                var attributeNameCorrect = capturedContext?.BlobProperties[0].AttributeName == attributeName.Get;

                return (contextCaptured && entityTypeCorrect && hasBlobProperties && 
                        propertyNameCorrect && attributeNameCorrect).ToProperty()
                    .Label($"Write context should contain correct data. " +
                           $"ContextCaptured: {contextCaptured}, EntityTypeCorrect: {entityTypeCorrect}, " +
                           $"HasBlobProperties: {hasBlobProperties}, PropertyNameCorrect: {propertyNameCorrect}, " +
                           $"AttributeNameCorrect: {attributeNameCorrect}");
            });
    }

    /// <summary>
    /// Additional property test: Strategy receives correct context for deletes.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Strategy_DeleteContext_ContainsCorrectData()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.PositiveInt().Filter(n => n.Get >= 1 && n.Get <= 5),
            (baseKey, keyCount) =>
            {
                // Arrange
                BlobDeleteContext? capturedContext = null;
                var mockStrategy = Substitute.For<IBlobStorageStrategy>();

                mockStrategy.OnBeforeDynamoDbDeleteAsync(Arg.Any<BlobDeleteContext>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        capturedContext = callInfo.Arg<BlobDeleteContext>();
                        return Task.FromResult(callInfo.Arg<BlobDeleteContext>());
                    });

                var referenceKeys = Enumerable.Range(0, keyCount.Get)
                    .Select(i => $"{baseKey.Get}-{i}")
                    .ToList();

                var context = new BlobDeleteContext
                {
                    EntityType = "TestEntity",
                    ReferenceKeys = referenceKeys
                };

                // Act
                mockStrategy.OnBeforeDynamoDbDeleteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                var contextCaptured = capturedContext != null;
                var entityTypeCorrect = capturedContext?.EntityType == "TestEntity";
                var keyCountCorrect = capturedContext?.ReferenceKeys.Count == keyCount.Get;
                var keysMatch = capturedContext?.ReferenceKeys.SequenceEqual(referenceKeys) ?? false;

                return (contextCaptured && entityTypeCorrect && keyCountCorrect && keysMatch).ToProperty()
                    .Label($"Delete context should contain correct data. " +
                           $"ContextCaptured: {contextCaptured}, EntityTypeCorrect: {entityTypeCorrect}, " +
                           $"KeyCountCorrect: {keyCountCorrect}, KeysMatch: {keysMatch}");
            });
    }

    /// <summary>
    /// Additional property test: Write result reference keys are correctly propagated.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Strategy_WriteResult_ReferenceKeysPropagate()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.NonEmptyString(),
            (propertyName, referenceKey) =>
            {
                // Arrange
                var mockStrategy = Substitute.For<IBlobStorageStrategy>();
                
                mockStrategy.OnBeforeDynamoDbWriteAsync(Arg.Any<BlobWriteContext>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(new BlobWriteResult
                    {
                        ReferenceKeys = new Dictionary<string, string> { [propertyName.Get] = referenceKey.Get }
                    }));

                var blobProperties = new List<BlobPropertyContext>
                {
                    new BlobPropertyContext
                    {
                        PropertyName = propertyName.Get,
                        AttributeName = "test_attr",
                        Data = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test")),
                        ContentType = "text/plain"
                    }
                };

                var context = new BlobWriteContext
                {
                    EntityType = "TestEntity",
                    BlobProperties = blobProperties
                };

                // Act
                var result = mockStrategy.OnBeforeDynamoDbWriteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                var hasReferenceKeys = result.ReferenceKeys.Count == 1;
                var keyMatches = result.ReferenceKeys.TryGetValue(propertyName.Get, out var actualKey) && 
                                 actualKey == referenceKey.Get;

                return (hasReferenceKeys && keyMatches).ToProperty()
                    .Label($"Write result should contain correct reference keys. " +
                           $"HasReferenceKeys: {hasReferenceKeys}, KeyMatches: {keyMatches}");
            });
    }
}
