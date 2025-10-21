using Xunit;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection fixture that shares a single DynamoDB Local instance across all test classes.
/// This improves test performance by avoiding repeated startup/shutdown of DynamoDB Local.
/// </summary>
[CollectionDefinition("DynamoDB Local")]
public class DynamoDbLocalCollection : ICollectionFixture<DynamoDbLocalFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
