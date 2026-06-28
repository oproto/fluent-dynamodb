using Xunit;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection fixtures for parallel integration test execution.
/// Each collection shares a DynamoDbLocalFixture instance and runs sequentially within itself,
/// but different collections run in parallel with each other.
/// Since each test class creates its own uniquely-named table via IntegrationTestBase,
/// there is no shared state between collections — parallelism is safe.
/// </summary>
[CollectionDefinition("DynamoDB Local")]
public class DynamoDbLocalCollection : ICollectionFixture<DynamoDbLocalFixture> { }

[CollectionDefinition("DynamoDB Geospatial")]
public class DynamoDbGeospatialCollection : ICollectionFixture<DynamoDbLocalFixture> { }

[CollectionDefinition("DynamoDB Expressions")]
public class DynamoDbExpressionsCollection : ICollectionFixture<DynamoDbLocalFixture> { }

[CollectionDefinition("DynamoDB Tables")]
public class DynamoDbTablesCollection : ICollectionFixture<DynamoDbLocalFixture> { }

[CollectionDefinition("DynamoDB Advanced")]
public class DynamoDbAdvancedCollection : ICollectionFixture<DynamoDbLocalFixture> { }
