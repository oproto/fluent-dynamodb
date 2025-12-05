using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// Interface for projection models that provide their projection expression.
/// Implemented by source-generated projection classes.
/// Enables AOT compatibility by avoiding reflection-based property discovery.
/// </summary>
/// <typeparam name="TSelf">The implementing projection type.</typeparam>
public interface IProjectionModel<TSelf> where TSelf : IProjectionModel<TSelf>
{
    /// <summary>
    /// Gets the DynamoDB projection expression for this model.
    /// </summary>
    static abstract string ProjectionExpression { get; }
    
    /// <summary>
    /// Creates an instance from DynamoDB attributes.
    /// </summary>
    static abstract TSelf FromDynamoDb(Dictionary<string, AttributeValue> item);
}
