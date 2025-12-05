using Amazon.Lambda.DynamoDBEvents;

namespace Oproto.FluentDynamoDb.Streams.Processing;

/// <summary>
/// Entry point builder for discriminator-based multi-entity stream processing.
/// </summary>
/// <remarks>
/// <para>
/// This builder is used to configure discriminator-based routing for single-table designs
/// where multiple entity types are stored in the same table and identified by a discriminator field.
/// </para>
/// <para>
/// Use this builder when you need to process different entity types from the same stream with
/// type-specific handlers. For single-entity processing, use the Process&lt;TEntity&gt;() extension
/// method instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Basic multi-entity processing
/// await record.Process()
///     .WithDiscriminator("EntityType")
///     .For&lt;UserEntity&gt;("User")
///         .OnInsert(async (_, user) => await ProcessUser(user))
///     .For&lt;OrderEntity&gt;("Order")
///         .OnInsert(async (_, order) => await ProcessOrder(order))
///     .ProcessAsync();
/// </code>
/// </example>
public sealed class StreamRecordProcessorBuilder
{
    private readonly DynamoDBEvent.DynamodbStreamRecord _record;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamRecordProcessorBuilder"/> class.
    /// </summary>
    /// <param name="record">The DynamoDB stream record to process.</param>
    public StreamRecordProcessorBuilder(DynamoDBEvent.DynamodbStreamRecord record)
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
    }

    /// <summary>
    /// Configures discriminator-based routing using the specified field name.
    /// </summary>
    /// <param name="fieldName">The name of the discriminator field in the DynamoDB item.</param>
    /// <returns>A <see cref="DiscriminatorStreamProcessorBuilder"/> for registering entity-specific handlers.</returns>
    /// <remarks>
    /// <para>
    /// The discriminator field is used to identify which entity type a stream record represents.
    /// This is commonly used in single-table designs where multiple entity types share the same table.
    /// </para>
    /// <para>
    /// The discriminator value is extracted from either the NewImage (for INSERT/MODIFY events)
    /// or OldImage (for REMOVE events) of the stream record.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await record.Process()
    ///     .WithDiscriminator("EntityType")
    ///     .For&lt;UserEntity&gt;("User")
    ///         .OnInsert(async (_, user) => await ProcessUser(user))
    ///     .For&lt;OrderEntity&gt;("Order")
    ///         .OnInsert(async (_, order) => await ProcessOrder(order))
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public DiscriminatorStreamProcessorBuilder WithDiscriminator(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Discriminator field name cannot be null or whitespace.", nameof(fieldName));
        }

        return new DiscriminatorStreamProcessorBuilder(_record, fieldName);
    }
}
