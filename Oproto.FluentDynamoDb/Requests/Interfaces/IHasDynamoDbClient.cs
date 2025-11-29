using Amazon.DynamoDBv2;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Interface for request builders that have access to a DynamoDB client.
/// Used by batch and transaction builders to extract the client without reflection.
/// </summary>
public interface IHasDynamoDbClient
{
    /// <summary>
    /// Gets the DynamoDB client used by this builder.
    /// </summary>
    /// <returns>The IAmazonDynamoDB client instance.</returns>
    IAmazonDynamoDB GetDynamoDbClient();
}
