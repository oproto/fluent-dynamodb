using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Amazon.Lambda.DynamoDBEvents;
using Oproto.FluentDynamoDb.Streams.Exceptions;
using LambdaAttributeValue = Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue;

namespace Oproto.FluentDynamoDb.Streams.Processing;

/// <summary>
/// Processes DynamoDB stream records for a single entity type with type-safe deserialization,
/// filtering, and event-specific handlers.
/// </summary>
/// <typeparam name="TEntity">The entity type to deserialize from stream records.</typeparam>
/// <remarks>
/// <para>
/// This processor provides a fluent API for handling DynamoDB stream events with strongly-typed entities.
/// It supports filtering at both the key level (before deserialization) and entity level (after deserialization),
/// as well as event-specific handlers for INSERT, MODIFY, and REMOVE operations.
/// </para>
/// <para>
/// The processor is immutable - all configuration methods return new instances, allowing safe reuse
/// and extension of processor configurations.
/// </para>
/// </remarks>
public sealed class TypedStreamProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TEntity> where TEntity : class
{
    private readonly DynamoDBEvent.DynamodbStreamRecord _record;
    private readonly List<Func<TEntity, bool>> _entityFilters;
    private readonly List<Func<Dictionary<string, LambdaAttributeValue>, bool>> _keyFilters;
    private readonly List<Func<TEntity?, TEntity, Task>> _insertHandlers;
    private readonly List<Func<TEntity, TEntity, Task>> _updateHandlers;
    private readonly List<Func<TEntity, TEntity?, Task>> _deleteHandlers;
    private readonly List<Func<TEntity, TEntity?, Task>> _ttlDeleteHandlers;
    private readonly List<Func<TEntity, TEntity?, Task>> _nonTtlDeleteHandlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedStreamProcessor{TEntity}"/> class.
    /// </summary>
    /// <param name="record">The DynamoDB stream record to process.</param>
    public TypedStreamProcessor(DynamoDBEvent.DynamodbStreamRecord record)
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _entityFilters = new List<Func<TEntity, bool>>();
        _keyFilters = new List<Func<Dictionary<string, LambdaAttributeValue>, bool>>();
        _insertHandlers = new List<Func<TEntity?, TEntity, Task>>();
        _updateHandlers = new List<Func<TEntity, TEntity, Task>>();
        _deleteHandlers = new List<Func<TEntity, TEntity?, Task>>();
        _ttlDeleteHandlers = new List<Func<TEntity, TEntity?, Task>>();
        _nonTtlDeleteHandlers = new List<Func<TEntity, TEntity?, Task>>();
    }

    /// <summary>
    /// Private constructor for creating new instances with updated filter/handler lists (immutability).
    /// </summary>
    private TypedStreamProcessor(
        DynamoDBEvent.DynamodbStreamRecord record,
        List<Func<TEntity, bool>> entityFilters,
        List<Func<Dictionary<string, LambdaAttributeValue>, bool>> keyFilters,
        List<Func<TEntity?, TEntity, Task>> insertHandlers,
        List<Func<TEntity, TEntity, Task>> updateHandlers,
        List<Func<TEntity, TEntity?, Task>> deleteHandlers,
        List<Func<TEntity, TEntity?, Task>> ttlDeleteHandlers,
        List<Func<TEntity, TEntity?, Task>> nonTtlDeleteHandlers)
    {
        _record = record;
        _entityFilters = entityFilters;
        _keyFilters = keyFilters;
        _insertHandlers = insertHandlers;
        _updateHandlers = updateHandlers;
        _deleteHandlers = deleteHandlers;
        _ttlDeleteHandlers = ttlDeleteHandlers;
        _nonTtlDeleteHandlers = nonTtlDeleteHandlers;
    }

    /// <summary>
    /// Adds an entity-level filter that is evaluated after deserialization.
    /// </summary>
    /// <param name="predicate">A LINQ expression that filters entities based on their properties.</param>
    /// <returns>A new <see cref="TypedStreamProcessor{TEntity}"/> instance with the filter added.</returns>
    /// <remarks>
    /// <para>
    /// Entity filters are evaluated after the stream record is deserialized to a strongly-typed entity.
    /// If the predicate returns false, all registered event handlers are skipped for this record.
    /// </para>
    /// <para>
    /// Multiple Where clauses can be chained and are evaluated with AND logic - all predicates
    /// must return true for handlers to execute.
    /// </para>
    /// <para>
    /// For performance optimization, consider using <see cref="WhereKey"/> to filter based on
    /// key values before deserialization occurs.
    /// </para>
    /// <para>
    /// This method returns a new processor instance, preserving immutability.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Filter by entity property
    /// await record.Process&lt;User&gt;()
    ///     .Where(u => u.Status == "active")
    ///     .Where(u => u.Email != null)  // Multiple filters with AND logic
    ///     .OnInsert(async (_, user) => await ProcessUser(user))
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypedStreamProcessor<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        // Compile the expression to a delegate for execution
        var compiledPredicate = predicate.Compile();

        var newEntityFilters = new List<Func<TEntity, bool>>(_entityFilters) { compiledPredicate };

        return new TypedStreamProcessor<TEntity>(
            _record,
            newEntityFilters,
            _keyFilters,
            _insertHandlers,
            _updateHandlers,
            _deleteHandlers,
            _ttlDeleteHandlers,
            _nonTtlDeleteHandlers);
    }

    /// <summary>
    /// Adds a key-level filter that is evaluated before deserialization for performance optimization.
    /// </summary>
    /// <param name="predicate">A function that filters based on the raw key AttributeValue dictionary.</param>
    /// <returns>A new <see cref="TypedStreamProcessor{TEntity}"/> instance with the filter added.</returns>
    /// <remarks>
    /// <para>
    /// Key filters are evaluated before entity deserialization, providing a performance optimization
    /// by avoiding expensive deserialization for records that will be filtered out.
    /// </para>
    /// <para>
    /// The predicate receives the stream record's Keys dictionary containing Lambda AttributeValue objects.
    /// If the predicate returns false, deserialization is skipped and all handlers are bypassed.
    /// </para>
    /// <para>
    /// WhereKey filters are evaluated before Where filters in the processing pipeline.
    /// </para>
    /// <para>
    /// This method returns a new processor instance, preserving immutability.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Filter by partition key prefix before deserialization
    /// await record.Process&lt;Order&gt;()
    ///     .WhereKey(keys => keys["pk"].S.StartsWith("ORDER#"))
    ///     .WhereKey(keys => keys["sk"].S.StartsWith("2024"))  // Multiple key filters
    ///     .OnInsert(async (_, order) => await ProcessOrder(order))
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    /// <example>
    /// <code>
    /// // Combine key and entity filters
    /// await record.Process&lt;User&gt;()
    ///     .WhereKey(keys => keys["pk"].S.StartsWith("USER#"))  // Pre-filter by key
    ///     .Where(u => u.Status == "active")  // Then filter by entity property
    ///     .OnInsert(async (_, user) => await ProcessUser(user))
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypedStreamProcessor<TEntity> WhereKey(
        Func<Dictionary<string, LambdaAttributeValue>, bool> predicate)
    {
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        var newKeyFilters = new List<Func<Dictionary<string, LambdaAttributeValue>, bool>>(_keyFilters) { predicate };

        return new TypedStreamProcessor<TEntity>(
            _record,
            _entityFilters,
            newKeyFilters,
            _insertHandlers,
            _updateHandlers,
            _deleteHandlers,
            _ttlDeleteHandlers,
            _nonTtlDeleteHandlers);
    }

    /// <summary>
    /// Registers a handler for INSERT events (new items added to the table).
    /// </summary>
    /// <param name="handler">
    /// An async function that receives the old value (always null for INSERT) and the new entity.
    /// </param>
    /// <returns>A new <see cref="TypedStreamProcessor{TEntity}"/> instance with the handler registered.</returns>
    /// <remarks>
    /// <para>
    /// INSERT handlers are executed when a new item is added to the DynamoDB table (EventName = "INSERT").
    /// The old value parameter is always null for INSERT events.
    /// </para>
    /// <para>
    /// Multiple handlers can be registered for the same event type and will be executed sequentially
    /// in the order they were registered.
    /// </para>
    /// <para>
    /// This method returns a new processor instance, preserving immutability.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await record.Process&lt;User&gt;()
    ///     .OnInsert(async (_, newUser) => 
    ///     {
    ///         await _emailService.SendWelcomeEmail(newUser.Email);
    ///         await _searchIndex.AddUser(newUser);
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypedStreamProcessor<TEntity> OnInsert(Func<TEntity?, TEntity, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var newInsertHandlers = new List<Func<TEntity?, TEntity, Task>>(_insertHandlers) { handler };

        return new TypedStreamProcessor<TEntity>(
            _record,
            _entityFilters,
            _keyFilters,
            newInsertHandlers,
            _updateHandlers,
            _deleteHandlers,
            _ttlDeleteHandlers,
            _nonTtlDeleteHandlers);
    }

    /// <summary>
    /// Registers a handler for MODIFY events (existing items updated in the table).
    /// </summary>
    /// <param name="handler">
    /// An async function that receives both the old entity state and the new entity state.
    /// </param>
    /// <returns>A new <see cref="TypedStreamProcessor{TEntity}"/> instance with the handler registered.</returns>
    /// <remarks>
    /// <para>
    /// UPDATE handlers are executed when an existing item is modified in the DynamoDB table (EventName = "MODIFY").
    /// Both old and new entity states are provided, allowing comparison of changes.
    /// </para>
    /// <para>
    /// Multiple handlers can be registered for the same event type and will be executed sequentially
    /// in the order they were registered.
    /// </para>
    /// <para>
    /// This method returns a new processor instance, preserving immutability.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await record.Process&lt;User&gt;()
    ///     .OnUpdate(async (oldUser, newUser) => 
    ///     {
    ///         if (oldUser.Email != newUser.Email)
    ///         {
    ///             await _emailService.SendEmailChangeNotification(newUser);
    ///         }
    ///         await _searchIndex.UpdateUser(newUser);
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypedStreamProcessor<TEntity> OnUpdate(Func<TEntity, TEntity, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var newUpdateHandlers = new List<Func<TEntity, TEntity, Task>>(_updateHandlers) { handler };

        return new TypedStreamProcessor<TEntity>(
            _record,
            _entityFilters,
            _keyFilters,
            _insertHandlers,
            newUpdateHandlers,
            _deleteHandlers,
            _ttlDeleteHandlers,
            _nonTtlDeleteHandlers);
    }

    /// <summary>
    /// Registers a handler for REMOVE events (items deleted from the table).
    /// </summary>
    /// <param name="handler">
    /// An async function that receives the old entity state and the new value (always null for REMOVE).
    /// </param>
    /// <returns>A new <see cref="TypedStreamProcessor{TEntity}"/> instance with the handler registered.</returns>
    /// <remarks>
    /// <para>
    /// DELETE handlers are executed for all REMOVE events (EventName = "REMOVE"), regardless of whether
    /// the deletion was manual or triggered by TTL expiration.
    /// </para>
    /// <para>
    /// For TTL-specific handling, use <see cref="OnTtlDelete"/> or <see cref="OnNonTtlDelete"/> instead.
    /// These handlers can be used alongside OnDelete - both will execute for applicable events.
    /// </para>
    /// <para>
    /// Multiple handlers can be registered for the same event type and will be executed sequentially
    /// in the order they were registered.
    /// </para>
    /// <para>
    /// This method returns a new processor instance, preserving immutability.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await record.Process&lt;User&gt;()
    ///     .OnDelete(async (user, _) => 
    ///     {
    ///         await _searchIndex.RemoveUser(user.UserId);
    ///         await _auditLog.LogDeletion(user);
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypedStreamProcessor<TEntity> OnDelete(Func<TEntity, TEntity?, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var newDeleteHandlers = new List<Func<TEntity, TEntity?, Task>>(_deleteHandlers) { handler };

        return new TypedStreamProcessor<TEntity>(
            _record,
            _entityFilters,
            _keyFilters,
            _insertHandlers,
            _updateHandlers,
            newDeleteHandlers,
            _ttlDeleteHandlers,
            _nonTtlDeleteHandlers);
    }

    /// <summary>
    /// Registers a handler specifically for TTL-triggered REMOVE events.
    /// </summary>
    /// <param name="handler">
    /// An async function that receives the old entity state and the new value (always null for REMOVE).
    /// </param>
    /// <returns>A new <see cref="TypedStreamProcessor{TEntity}"/> instance with the handler registered.</returns>
    /// <remarks>
    /// <para>
    /// TTL DELETE handlers are executed only for REMOVE events triggered by DynamoDB's Time To Live (TTL) feature.
    /// TTL deletes are identified by checking if UserIdentity.Type equals "Service" and
    /// UserIdentity.PrincipalId equals "dynamodb.amazonaws.com".
    /// </para>
    /// <para>
    /// This allows different business logic for automatic expiration vs. manual deletion.
    /// For example, expired sessions might just be logged, while manual deletions might trigger cleanup.
    /// </para>
    /// <para>
    /// Multiple handlers can be registered and will be executed sequentially in registration order.
    /// </para>
    /// <para>
    /// This method returns a new processor instance, preserving immutability.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await record.Process&lt;Session&gt;()
    ///     .OnTtlDelete(async (session, _) => 
    ///     {
    ///         // Session expired automatically
    ///         await _auditLog.LogSessionExpiry(session.UserId);
    ///     })
    ///     .OnNonTtlDelete(async (session, _) => 
    ///     {
    ///         // User explicitly logged out
    ///         await _auditLog.LogLogout(session.UserId);
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypedStreamProcessor<TEntity> OnTtlDelete(Func<TEntity, TEntity?, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var newTtlDeleteHandlers = new List<Func<TEntity, TEntity?, Task>>(_ttlDeleteHandlers) { handler };

        return new TypedStreamProcessor<TEntity>(
            _record,
            _entityFilters,
            _keyFilters,
            _insertHandlers,
            _updateHandlers,
            _deleteHandlers,
            newTtlDeleteHandlers,
            _nonTtlDeleteHandlers);
    }

    /// <summary>
    /// Registers a handler specifically for non-TTL REMOVE events (manual deletions).
    /// </summary>
    /// <param name="handler">
    /// An async function that receives the old entity state and the new value (always null for REMOVE).
    /// </param>
    /// <returns>A new <see cref="TypedStreamProcessor{TEntity}"/> instance with the handler registered.</returns>
    /// <remarks>
    /// <para>
    /// NON-TTL DELETE handlers are executed only for REMOVE events that were NOT triggered by TTL expiration.
    /// These represent manual deletions performed by application code or users.
    /// </para>
    /// <para>
    /// This allows different business logic for manual deletions vs. automatic expiration.
    /// For example, manual deletions might trigger cleanup of related resources, while TTL expirations
    /// might just be logged.
    /// </para>
    /// <para>
    /// Multiple handlers can be registered and will be executed sequentially in registration order.
    /// </para>
    /// <para>
    /// This method returns a new processor instance, preserving immutability.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await record.Process&lt;Session&gt;()
    ///     .OnNonTtlDelete(async (session, _) => 
    ///     {
    ///         // User explicitly logged out
    ///         await _auditLog.LogLogout(session.UserId);
    ///         await _notificationService.SendLogoutConfirmation(session.UserId);
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypedStreamProcessor<TEntity> OnNonTtlDelete(Func<TEntity, TEntity?, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var newNonTtlDeleteHandlers = new List<Func<TEntity, TEntity?, Task>>(_nonTtlDeleteHandlers) { handler };

        return new TypedStreamProcessor<TEntity>(
            _record,
            _entityFilters,
            _keyFilters,
            _insertHandlers,
            _updateHandlers,
            _deleteHandlers,
            _ttlDeleteHandlers,
            newNonTtlDeleteHandlers);
    }

    /// <summary>
    /// Executes the configured filters and event handlers for the stream record.
    /// </summary>
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
    /// <para>
    /// The entity type must have a static FromDynamoDbStream method (generated by [GenerateStreamConversion])
    /// or a FromStreamImage method for deserialization.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity type does not have the required FromDynamoDbStream or FromStreamImage method.
    /// </exception>
    public async Task ProcessAsync()
    {
        // Step 1: Evaluate WhereKey filters first (before deserialization)
        if (_keyFilters.Count > 0 && _record.Dynamodb?.Keys != null)
        {
            foreach (var keyFilter in _keyFilters)
            {
                try
                {
                    if (!keyFilter(_record.Dynamodb.Keys))
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

        var eventName = _record.EventName;

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
                if (_record.Dynamodb?.NewImage != null)
                {
                    newEntity = DeserializeEntity(_record.Dynamodb.NewImage, fromDynamoDbStreamMethod, fromStreamImageMethod, true);
                }
            }
            else if (eventName == "MODIFY")
            {
                // MODIFY: both OldImage and NewImage are available
                if (_record.Dynamodb?.OldImage != null)
                {
                    oldEntity = DeserializeEntity(_record.Dynamodb.OldImage, fromDynamoDbStreamMethod, fromStreamImageMethod, false);
                }
                if (_record.Dynamodb?.NewImage != null)
                {
                    newEntity = DeserializeEntity(_record.Dynamodb.NewImage, fromDynamoDbStreamMethod, fromStreamImageMethod, true);
                }
            }
            else if (eventName == "REMOVE")
            {
                // REMOVE: only OldImage is available
                if (_record.Dynamodb?.OldImage != null)
                {
                    oldEntity = DeserializeEntity(_record.Dynamodb.OldImage, fromDynamoDbStreamMethod, fromStreamImageMethod, false);
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
        // For INSERT/MODIFY, use newEntity; for REMOVE, use oldEntity
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

        // Step 4 & 5: Execute appropriate handlers based on event type
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

            // Step 6: Check UserIdentity for TTL detection
            var isTtlDelete = IsTtlDelete(_record);

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
        Dictionary<string, LambdaAttributeValue> image,
        System.Reflection.MethodInfo? fromDynamoDbStreamMethod,
        System.Reflection.MethodInfo? fromStreamImageMethod,
        bool useNewImage)
    {
        if (fromStreamImageMethod != null)
        {
            // Use FromStreamImage if available
            return (TEntity?)fromStreamImageMethod.Invoke(null, new object[] { _record.Dynamodb, useNewImage });
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

