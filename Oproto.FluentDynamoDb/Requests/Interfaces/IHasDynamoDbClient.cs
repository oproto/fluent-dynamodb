using Amazon.DynamoDBv2;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Interface for request builders that have access to a DynamoDB client and configuration options.
/// Used by batch and transaction builders to extract the client without reflection,
/// and by expression translators to access geospatial and other optional features.
/// </summary>
public interface IHasDynamoDbClient
{
    /// <summary>
    /// Gets the DynamoDB client used by this builder.
    /// </summary>
    /// <returns>The IAmazonDynamoDB client instance.</returns>
    IAmazonDynamoDB GetDynamoDbClient();
    
    /// <summary>
    /// Gets the configuration options for this builder.
    /// </summary>
    /// <returns>The FluentDynamoDbOptions instance.</returns>
    FluentDynamoDbOptions GetOptions();
}
