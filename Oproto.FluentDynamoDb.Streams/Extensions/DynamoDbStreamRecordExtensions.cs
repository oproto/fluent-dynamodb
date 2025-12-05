using System.Diagnostics.CodeAnalysis;
using Amazon.Lambda.DynamoDBEvents;
using Oproto.FluentDynamoDb.Streams.Processing;

namespace Oproto.FluentDynamoDb.Streams.Extensions;

/// <summary>
/// Extension methods for processing DynamoDB stream records with type-safe entity deserialization.
/// </summary>
public static class DynamoDbStreamRecordExtensions
{
    /// <summary>
    /// Processes a DynamoDB stream record for a single entity type with type-safe deserialization.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to deserialize from the stream record.</typeparam>
    /// <param name="record">The DynamoDB stream record to process.</param>
    /// <returns>A <see cref="TypedStreamProcessor{TEntity}"/> for configuring event handlers and filters.</returns>
    /// <remarks>
    /// <para>
    /// This method provides a fluent API for processing stream records with strongly-typed entities.
    /// The entity type must have the <c>[GenerateStreamConversion]</c> attribute applied to generate
    /// the required <c>FromDynamoDbStream</c> deserialization method.
    /// </para>
    /// <para>
    /// Use this method when processing a single entity type. For multi-entity processing with
    /// discriminator-based routing, use <see cref="Process(DynamoDBEvent.DynamodbStreamRecord)"/> instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Process user entity stream records
    /// await record.Process&lt;User&gt;()
    ///     .Where(u => u.Status == "active")
    ///     .OnInsert(async (_, newUser) => 
    ///     {
    ///         await _emailService.SendWelcomeEmail(newUser.Email);
    ///     })
    ///     .OnUpdate(async (oldUser, newUser) => 
    ///     {
    ///         if (oldUser.Email != newUser.Email)
    ///         {
    ///             await _emailService.SendEmailChangeNotification(newUser);
    ///         }
    ///     })
    ///     .OnDelete(async (user, _) => 
    ///     {
    ///         await _searchIndex.RemoveUser(user.UserId);
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    /// <example>
    /// <code>
    /// // Process with key-based pre-filtering for performance
    /// await record.Process&lt;Order&gt;()
    ///     .WhereKey(keys => keys["pk"].S.StartsWith("ORDER#"))
    ///     .Where(o => o.Total > 100)
    ///     .OnInsert(async (_, order) => 
    ///     {
    ///         await _orderProcessor.ProcessHighValueOrder(order);
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public static TypedStreamProcessor<TEntity> Process<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TEntity>(
        this DynamoDBEvent.DynamodbStreamRecord record)
        where TEntity : class
    {
        return new TypedStreamProcessor<TEntity>(record);
    }

    /// <summary>
    /// Processes a DynamoDB stream record with discriminator-based routing for multi-entity tables.
    /// </summary>
    /// <param name="record">The DynamoDB stream record to process.</param>
    /// <returns>A <see cref="StreamRecordProcessorBuilder"/> for configuring discriminator-based routing.</returns>
    /// <remarks>
    /// <para>
    /// This method is used for single-table designs where multiple entity types are stored in the same table
    /// and identified by a discriminator field. Chain <c>WithDiscriminator(fieldName)</c> to specify the
    /// discriminator field, then use <c>For&lt;TEntity&gt;()</c> to register handlers for each entity type.
    /// </para>
    /// <para>
    /// For single-entity processing, use <see cref="Process{TEntity}(DynamoDBEvent.DynamodbStreamRecord)"/> instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Process multi-entity table with discriminator routing
    /// await record.Process()
    ///     .WithDiscriminator("EntityType")
    ///     .For&lt;UserEntity&gt;("User")
    ///         .Where(u => u.Status == "active")
    ///         .OnInsert(async (_, user) => await IndexUser(user))
    ///     .For&lt;OrderEntity&gt;("Order")
    ///         .Where(o => o.Total > 100)
    ///         .OnInsert(async (_, order) => await ProcessOrder(order))
    ///     .OnUnknownType(async unknownRecord => 
    ///     {
    ///         _logger.LogWarning("Unknown entity type in stream");
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    /// <example>
    /// <code>
    /// // Process with pattern-based discriminators (sort key patterns)
    /// await record.Process()
    ///     .WithDiscriminator("SK")
    ///     .For&lt;UserEntity&gt;("USER#*")  // Prefix match
    ///         .OnInsert(async (_, user) => await IndexUser(user))
    ///     .For&lt;OrderEntity&gt;("ORDER#*")  // Prefix match
    ///         .OnInsert(async (_, order) => await ProcessOrder(order))
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public static StreamRecordProcessorBuilder Process(
        this DynamoDBEvent.DynamodbStreamRecord record)
    {
        return new StreamRecordProcessorBuilder(record);
    }
}
