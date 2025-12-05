using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Amazon.Lambda.DynamoDBEvents;
using Oproto.FluentDynamoDb.Streams.Exceptions;
using LambdaAttributeValue = Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue;

namespace Oproto.FluentDynamoDb.Streams.Processing;

/// <summary>
/// Abstract base class for type-specific handler registrations in discriminator-based stream processing.
/// </summary>
/// <remarks>
/// This class serves as the base for storing and executing handlers for specific entity types
/// in multi-entity stream processing scenarios. Each concrete implementation handles a specific
/// entity type with its own filters and event handlers.
/// </remarks>
public abstract class TypeHandlerRegistration
{
    /// <summary>
    /// Processes a stream record by deserializing to the appropriate entity type,
    /// applying filters, and executing registered event handlers.
    /// </summary>
    /// <param name="record">The DynamoDB stream record to process.</param>
    /// <returns>A task representing the asynchronous processing operation.</returns>
    public abstract Task ProcessAsync(DynamoDBEvent.DynamodbStreamRecord record);
}

/// <summary>
/// Generic implementation of type-specific handler registration for a particular entity type.
/// </summary>
/// <typeparam name="TEntity">The entity type this registration handles.</typeparam>
/// <remarks>
/// This class stores filters and event handlers for a specific entity type in discriminator-based
/// stream processing. It provides the same filtering and handler registration capabilities as
/// TypedStreamProcessor but is used by DiscriminatorStreamProcessorBuilder for multi-entity routing.
/// </remarks>
public sealed class TypeHandlerRegistration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TEntity> : TypeHandlerRegistration
    where TEntity : class
{
    private readonly DiscriminatorStreamProcessorBuilder? _parentBuilder;
    private readonly List<Func<TEntity, bool>> _entityFilters = new();
    private readonly List<Func<Dictionary<string, LambdaAttributeValue>, bool>> _keyFilters = new();
    private readonly List<Func<TEntity?, TEntity, Task>> _insertHandlers = new();
    private readonly List<Func<TEntity, TEntity, Task>> _updateHandlers = new();
    private readonly List<Func<TEntity, TEntity?, Task>> _deleteHandlers = new();
    private readonly List<Func<TEntity, TEntity?, Task>> _ttlDeleteHandlers = new();
    private readonly List<Func<TEntity, TEntity?, Task>> _nonTtlDeleteHandlers = new();

    /// <summary>
    /// Initializes a new instance with an optional parent builder for fluent chaining.
    /// </summary>
    internal TypeHandlerRegistration(DiscriminatorStreamProcessorBuilder? parentBuilder = null)
    {
        _parentBuilder = parentBuilder;
    }

    /// <summary>
    /// Adds an entity-level filter that is evaluated after deserialization.
    /// </summary>
    /// <param name="predicate">A LINQ expression that filters entities based on their properties.</param>
    /// <returns>This registration instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Entity filters are evaluated after the stream record is deserialized. Multiple Where clauses
    /// can be chained and are evaluated with AND logic. For performance optimization, consider using
    /// <see cref="WhereKey"/> to filter based on key values before deserialization.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// .For&lt;UserEntity&gt;("User")
    ///     .Where(u => u.Status == "active")
    ///     .Where(u => u.Email != null)
    ///     .OnInsert(async (_, user) => await ProcessUser(user))
    /// </code>
    /// </example>
    public TypeHandlerRegistration<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        var compiledPredicate = predicate.Compile();
        _entityFilters.Add(compiledPredicate);
        return this;
    }

    /// <summary>
    /// Adds a key-level filter that is evaluated before deserialization.
    /// </summary>
    /// <param name="predicate">A function that filters based on the raw key AttributeValue dictionary.</param>
    /// <returns>This registration instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Key filters are evaluated before entity deserialization, providing a performance optimization
    /// by avoiding expensive deserialization for records that will be filtered out. WhereKey filters
    /// are evaluated before Where filters in the processing pipeline.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// .For&lt;OrderEntity&gt;("Order")
    ///     .WhereKey(keys => keys["pk"].S.StartsWith("ORDER#2024"))
    ///     .OnInsert(async (_, order) => await ProcessOrder(order))
    /// </code>
    /// </example>
    public TypeHandlerRegistration<TEntity> WhereKey(
        Func<Dictionary<string, LambdaAttributeValue>, bool> predicate)
    {
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        _keyFilters.Add(predicate);
        return this;
    }

    /// <summary>
    /// Registers a handler for INSERT events.
    /// </summary>
    /// <param name="handler">An async function that receives the old value (null) and the new entity.</param>
    /// <returns>The parent builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// INSERT handlers are executed when a new item is added to the DynamoDB table (EventName = "INSERT").
    /// The old value parameter is always null for INSERT events. Multiple handlers can be registered
    /// and will be executed sequentially in registration order.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// .For&lt;UserEntity&gt;("User")
    ///     .OnInsert(async (_, user) => 
    ///     {
    ///         await _searchIndex.AddUser(user);
    ///         await _emailService.SendWelcomeEmail(user.Email);
    ///     })
    /// </code>
    /// </example>
    public DiscriminatorStreamProcessorBuilder OnInsert(Func<TEntity?, TEntity, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _insertHandlers.Add(handler);
        return _parentBuilder ?? throw new InvalidOperationException("Parent builder is required for fluent chaining.");
    }

    /// <summary>
    /// Registers a handler for MODIFY events.
    /// </summary>
    /// <param name="handler">An async function that receives both the old and new entity states.</param>
    /// <returns>The parent builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// UPDATE handlers are executed when an existing item is modified (EventName = "MODIFY").
    /// Both old and new entity states are provided, allowing comparison of changes. Multiple handlers
    /// can be registered and will be executed sequentially in registration order.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// .For&lt;UserEntity&gt;("User")
    ///     .OnUpdate(async (oldUser, newUser) => 
    ///     {
    ///         if (oldUser.Email != newUser.Email)
    ///         {
    ///             await _emailService.SendEmailChangeNotification(newUser);
    ///         }
    ///     })
    /// </code>
    /// </example>
    public DiscriminatorStreamProcessorBuilder OnUpdate(Func<TEntity, TEntity, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _updateHandlers.Add(handler);
        return _parentBuilder ?? throw new InvalidOperationException("Parent builder is required for fluent chaining.");
    }

    /// <summary>
    /// Registers a handler for REMOVE events.
    /// </summary>
    /// <param name="handler">An async function that receives the old entity and null for new value.</param>
    /// <returns>The parent builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// DELETE handlers are executed for all REMOVE events (EventName = "REMOVE"), regardless of whether
    /// the deletion was manual or triggered by TTL. For TTL-specific handling, use <see cref="OnTtlDelete"/>
    /// or <see cref="OnNonTtlDelete"/>. Multiple handlers can be registered and will be executed sequentially.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// .For&lt;UserEntity&gt;("User")
    ///     .OnDelete(async (user, _) => 
    ///     {
    ///         await _searchIndex.RemoveUser(user.UserId);
    ///     })
    /// </code>
    /// </example>
    public DiscriminatorStreamProcessorBuilder OnDelete(Func<TEntity, TEntity?, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _deleteHandlers.Add(handler);
        return _parentBuilder ?? throw new InvalidOperationException("Parent builder is required for fluent chaining.");
    }

    /// <summary>
    /// Registers a handler specifically for TTL-triggered REMOVE events.
    /// </summary>
    /// <param name="handler">An async function that receives the old entity and null for new value.</param>
    /// <returns>The parent builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// TTL DELETE handlers are executed only for REMOVE events triggered by DynamoDB's Time To Live feature.
    /// TTL deletes are identified by checking if UserIdentity.Type equals "Service" and
    /// UserIdentity.PrincipalId equals "dynamodb.amazonaws.com". This allows different business logic
    /// for automatic expiration vs. manual deletion.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// .For&lt;SessionEntity&gt;("Session")
    ///     .OnTtlDelete(async (session, _) => 
    ///     {
    ///         await _auditLog.LogSessionExpiry(session.UserId);
    ///     })
    /// </code>
    /// </example>
    public DiscriminatorStreamProcessorBuilder OnTtlDelete(Func<TEntity, TEntity?, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _ttlDeleteHandlers.Add(handler);
        return _parentBuilder ?? throw new InvalidOperationException("Parent builder is required for fluent chaining.");
    }

    /// <summary>
    /// Registers a handler specifically for non-TTL REMOVE events.
    /// </summary>
    /// <param name="handler">An async function that receives the old entity and null for new value.</param>
    /// <returns>The parent builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// NON-TTL DELETE handlers are executed only for REMOVE events that were NOT triggered by TTL expiration.
    /// These represent manual deletions performed by application code or users. This allows different business
    /// logic for manual deletions vs. automatic expiration.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// .For&lt;SessionEntity&gt;("Session")
    ///     .OnNonTtlDelete(async (session, _) => 
    ///     {
    ///         await _auditLog.LogLogout(session.UserId);
    ///         await _notificationService.SendLogoutConfirmation(session.UserId);
    ///     })
    /// </code>
    /// </example>
    public DiscriminatorStreamProcessorBuilder OnNonTtlDelete(Func<TEntity, TEntity?, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _nonTtlDeleteHandlers.Add(handler);
        return _parentBuilder ?? throw new InvalidOperationException("Parent builder is required for fluent chaining.");
    }

    /// <summary>
    /// Processes a stream record by applying filters and executing registered handlers.
    /// </summary>
    /// <param name="record">The DynamoDB stream record to process.</param>
    /// <returns>A task representing the asynchronous processing operation.</returns>
    /// <remarks>
    /// <para>
    /// The processing pipeline follows this order:
    /// 1. Evaluate WhereKey filters (before deserialization)
    /// 2. Deserialize the entity using FromDynamoDbStream or FromStreamImage
    /// 3. Evaluate Where filters (after deserialization)
    /// 4. Determine event type from record.EventName
    /// 5. Execute appropriate handlers based on event type
    /// 6. For REMOVE events, check UserIdentity to distinguish TTL vs. manual deletes
    /// </para>
    /// <para>
    /// Handlers are executed sequentially in the order they were registered.
    /// If any filter returns false, all handlers are skipped.
    /// </para>
    /// </remarks>
    public override async Task ProcessAsync(DynamoDBEvent.DynamodbStreamRecord record)
    {
        // Step 1: Evaluate WhereKey filters first (before deserialization)
        if (_keyFilters.Count > 0 && record.Dynamodb?.Keys != null)
        {
            foreach (var keyFilter in _keyFilters)
            {
                try
                {
                    if (!keyFilter(record.Dynamodb.Keys))
                    {
                        // Key filter failed, skip processing
                        return;
                    }
                }
                catch (Exception ex)
                {
                    throw new StreamFilterException(
                        "Key filter evaluation failed during stream processing.",
                        "WhereKey predicate",
                        ex);
                }
            }
        }

        // Step 2: Deserialize the entity based on event type
        TEntity? oldEntity = null;
        TEntity? newEntity = null;

        var eventName = record.EventName;

        // Use reflection to find FromDynamoDbStream or FromStreamImage method
        var entityType = typeof(TEntity);
        var fromStreamImageMethod = entityType.GetMethod(
            "FromStreamImage",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(DynamoDBEvent.StreamRecord), typeof(bool) },
            null);

        var fromDynamoDbStreamMethod = entityType.GetMethod(
            "FromDynamoDbStream",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(Dictionary<string, LambdaAttributeValue>) },
            null);

        if (fromStreamImageMethod == null && fromDynamoDbStreamMethod == null)
        {
            throw new InvalidOperationException(
                $"Entity type {entityType.Name} must have a static FromDynamoDbStream or FromStreamImage method. " +
                "Apply [GenerateStreamConversion] attribute to generate these methods.");
        }

        // Deserialize based on event type
        try
        {
            if (eventName == "INSERT")
            {
                // INSERT: only NewImage is available
                if (record.Dynamodb?.NewImage != null)
                {
                    newEntity = DeserializeEntity(record, record.Dynamodb.NewImage, fromDynamoDbStreamMethod, fromStreamImageMethod, true);
                }
            }
            else if (eventName == "MODIFY")
            {
                // MODIFY: both OldImage and NewImage are available
                if (record.Dynamodb?.OldImage != null)
                {
                    oldEntity = DeserializeEntity(record, record.Dynamodb.OldImage, fromDynamoDbStreamMethod, fromStreamImageMethod, false);
                }
                if (record.Dynamodb?.NewImage != null)
                {
                    newEntity = DeserializeEntity(record, record.Dynamodb.NewImage, fromDynamoDbStreamMethod, fromStreamImageMethod, true);
                }
            }
            else if (eventName == "REMOVE")
            {
                // REMOVE: only OldImage is available
                if (record.Dynamodb?.OldImage != null)
                {
                    oldEntity = DeserializeEntity(record, record.Dynamodb.OldImage, fromDynamoDbStreamMethod, fromStreamImageMethod, false);
                }
            }
        }
        catch (Exception ex) when (ex is not StreamDeserializationException)
        {
            throw new StreamDeserializationException(
                $"Failed to deserialize stream record to entity type {entityType.Name}.",
                entityType,
                ex);
        }

        // Step 3: Evaluate Where filters on deserialized entity
        var entityToFilter = newEntity ?? oldEntity;
        if (entityToFilter != null && _entityFilters.Count > 0)
        {
            foreach (var entityFilter in _entityFilters)
            {
                try
                {
                    if (!entityFilter(entityToFilter))
                    {
                        // Entity filter failed, skip processing
                        return;
                    }
                }
                catch (Exception ex)
                {
                    throw new StreamFilterException(
                        "Entity filter evaluation failed during stream processing.",
                        "Where predicate",
                        ex);
                }
            }
        }

        // Step 4: Execute appropriate handlers based on event type
        if (eventName == "INSERT" && newEntity != null)
        {
            foreach (var handler in _insertHandlers)
            {
                await handler(null, newEntity);
            }
        }
        else if (eventName == "MODIFY" && oldEntity != null && newEntity != null)
        {
            foreach (var handler in _updateHandlers)
            {
                await handler(oldEntity, newEntity);
            }
        }
        else if (eventName == "REMOVE" && oldEntity != null)
        {
            // Execute general delete handlers
            foreach (var handler in _deleteHandlers)
            {
                await handler(oldEntity, null);
            }

            // Check UserIdentity for TTL detection
            var isTtlDelete = IsTtlDelete(record);

            if (isTtlDelete)
            {
                // Execute TTL-specific delete handlers
                foreach (var handler in _ttlDeleteHandlers)
                {
                    await handler(oldEntity, null);
                }
            }
            else
            {
                // Execute non-TTL delete handlers
                foreach (var handler in _nonTtlDeleteHandlers)
                {
                    await handler(oldEntity, null);
                }
            }
        }
    }

    /// <summary>
    /// Deserializes an entity from a Lambda AttributeValue dictionary.
    /// </summary>
    private TEntity? DeserializeEntity(
        DynamoDBEvent.DynamodbStreamRecord record,
        Dictionary<string, LambdaAttributeValue> image,
        System.Reflection.MethodInfo? fromDynamoDbStreamMethod,
        System.Reflection.MethodInfo? fromStreamImageMethod,
        bool useNewImage)
    {
        if (fromStreamImageMethod != null)
        {
            // Use FromStreamImage if available
            return (TEntity?)fromStreamImageMethod.Invoke(null, new object[] { record.Dynamodb, useNewImage });
        }
        else if (fromDynamoDbStreamMethod != null)
        {
            // Use FromDynamoDbStream
            return (TEntity?)fromDynamoDbStreamMethod.Invoke(null, new object[] { image });
        }

        return null;
    }

    /// <summary>
    /// Determines if a REMOVE event was triggered by TTL expiration.
    /// </summary>
    private bool IsTtlDelete(DynamoDBEvent.DynamodbStreamRecord record)
    {
        return record.UserIdentity != null &&
               record.UserIdentity.Type == "Service" &&
               record.UserIdentity.PrincipalId == "dynamodb.amazonaws.com";
    }
}
