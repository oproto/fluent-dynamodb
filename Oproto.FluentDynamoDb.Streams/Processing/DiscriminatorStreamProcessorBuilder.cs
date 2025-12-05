using System.Diagnostics.CodeAnalysis;
using Amazon.Lambda.DynamoDBEvents;

namespace Oproto.FluentDynamoDb.Streams.Processing;

/// <summary>
/// Builder for discriminator-based multi-entity stream processing.
/// </summary>
/// <remarks>
/// <para>
/// This builder enables routing stream records to type-specific handlers based on a discriminator field value.
/// It's designed for single-table designs where multiple entity types are stored in the same DynamoDB table
/// and identified by a discriminator field (e.g., "EntityType", "SK", etc.).
/// </para>
/// <para>
/// The builder supports both exact discriminator matching and pattern matching with wildcards
/// (prefix, suffix, and contains patterns).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Multi-entity processing with discriminator routing
/// await record.Process()
///     .WithDiscriminator("EntityType")
///     .For&lt;UserEntity&gt;("User")
///         .Where(u => u.Status == "active")
///         .OnInsert(async (_, user) => await ProcessUser(user))
///         .OnUpdate(async (old, new) => await UpdateUser(old, new))
///     .For&lt;OrderEntity&gt;("Order")
///         .Where(o => o.Total > 100)
///         .OnInsert(async (_, order) => await ProcessOrder(order))
///     .OnUnknownType(async record => 
///     {
///         _logger.LogWarning("Unknown entity type in stream");
///     })
///     .ProcessAsync();
/// </code>
/// </example>
/// <example>
/// <code>
/// // Pattern-based discriminators with sort key patterns
/// await record.Process()
///     .WithDiscriminator("SK")
///     .For&lt;UserEntity&gt;("USER#*")  // Prefix match
///         .OnInsert(async (_, user) => await ProcessUser(user))
///     .For&lt;OrderEntity&gt;("ORDER#*")  // Prefix match
///         .OnInsert(async (_, order) => await ProcessOrder(order))
///     .ProcessAsync();
/// </code>
/// </example>
public sealed class DiscriminatorStreamProcessorBuilder
{
    private readonly DynamoDBEvent.DynamodbStreamRecord _record;
    private readonly string _discriminatorField;
    private readonly Dictionary<string, TypeHandlerRegistration> _handlers = new();
    private Func<DynamoDBEvent.DynamodbStreamRecord, Task>? _unknownTypeHandler;
    private Func<Type, DiscriminatorInfo?>? _registryLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscriminatorStreamProcessorBuilder"/> class.
    /// </summary>
    /// <param name="record">The DynamoDB stream record to process.</param>
    /// <param name="discriminatorField">The name of the discriminator field in the DynamoDB item.</param>
    internal DiscriminatorStreamProcessorBuilder(
        DynamoDBEvent.DynamodbStreamRecord record,
        string discriminatorField)
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _discriminatorField = discriminatorField ?? throw new ArgumentNullException(nameof(discriminatorField));
    }

    /// <summary>
    /// Attaches a discriminator registry lookup function for automatic discriminator resolution.
    /// </summary>
    /// <param name="registryLookup">
    /// A function that looks up discriminator information for an entity type.
    /// </param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method is called internally by the generated OnStream method on table classes
    /// to attach the static discriminator registry. It enables the parameterless For&lt;TEntity&gt;()
    /// method to automatically look up discriminator values from entity configuration.
    /// </para>
    /// <para>
    /// This method is not intended for direct use by application code. Use the generated
    /// OnStream method on table classes instead.
    /// </para>
    /// </remarks>
    internal DiscriminatorStreamProcessorBuilder WithRegistry(
        Func<Type, DiscriminatorInfo?> registryLookup)
    {
        if (registryLookup == null)
        {
            throw new ArgumentNullException(nameof(registryLookup));
        }

        _registryLookup = registryLookup;
        return this;
    }

    /// <summary>
    /// Registers a handler for a specific entity type using automatic discriminator lookup from the registry.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to handle.</typeparam>
    /// <returns>
    /// A <see cref="TypeHandlerRegistration{TEntity}"/> for configuring filters and event handlers
    /// for this entity type.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method looks up the discriminator value or pattern from the generated StreamDiscriminatorRegistry
    /// based on the entity type. It's only available when using table-integrated stream processing
    /// (via the generated OnStream method on table classes).
    /// </para>
    /// <para>
    /// The discriminator information is extracted from the entity's DynamoDbTableAttribute at compile time,
    /// including the discriminator value and any pattern wildcards.
    /// </para>
    /// <para>
    /// If you need to override the configured discriminator or are not using table-integrated processing,
    /// use the For&lt;TEntity&gt;(string) overload with an explicit discriminator pattern.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registry is not available (not using table-integrated processing)
    /// or when the entity type is not registered for stream processing.
    /// </exception>
    /// <example>
    /// <code>
    /// // Using table-integrated stream processing
    /// await _myTable.OnStream(record)
    ///     .For&lt;UserEntity&gt;()  // Discriminator looked up automatically
    ///         .Where(u => u.Status == "active")
    ///         .OnInsert(async (_, user) => await ProcessUser(user))
    ///     .For&lt;OrderEntity&gt;()  // Discriminator looked up automatically
    ///         .OnInsert(async (_, order) => await ProcessOrder(order))
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypeHandlerRegistration<TEntity> For<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TEntity>()
        where TEntity : class
    {
        if (_registryLookup == null)
        {
            throw new InvalidOperationException(
                $"Cannot use For<{typeof(TEntity).Name}>() without discriminator registry. " +
                "Use table.OnStream(record) for automatic discriminator lookup, or provide an explicit discriminator value with For<TEntity>(string).");
        }

        var info = _registryLookup(typeof(TEntity));
        if (info == null)
        {
            throw new InvalidOperationException(
                $"Entity type {typeof(TEntity).Name} is not registered for stream processing. " +
                "Ensure [GenerateStreamConversion] attribute is applied to the entity class.");
        }

        // Use the pattern if available, otherwise use the value
        var discriminatorKey = info.Pattern ?? info.Value;
        return For<TEntity>(discriminatorKey);
    }

    /// <summary>
    /// Registers a handler for a specific entity type with an explicit discriminator value or pattern.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to handle.</typeparam>
    /// <param name="discriminatorPattern">
    /// The discriminator value or pattern to match. Supports wildcards:
    /// - "USER" - exact match
    /// - "USER#*" - prefix match (starts with "USER#")
    /// - "*#USER" - suffix match (ends with "#USER")
    /// - "*#USER#*" - contains match (contains "#USER#")
    /// </param>
    /// <returns>
    /// A <see cref="TypeHandlerRegistration{TEntity}"/> for configuring filters and event handlers
    /// for this entity type.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method registers a handler for a specific entity type identified by a discriminator value or pattern.
    /// The discriminator pattern supports wildcards for flexible matching in single-table designs.
    /// </para>
    /// <para>
    /// Pattern matching rules:
    /// - Trailing wildcard (*): StartsWith matching (e.g., "USER#*" matches "USER#123", "USER#456")
    /// - Leading wildcard (*): EndsWith matching (e.g., "*#USER" matches "ADMIN#USER", "GUEST#USER")
    /// - Both wildcards: Contains matching (e.g., "*#USER#*" matches "ADMIN#USER#123")
    /// - No wildcards: Exact matching (e.g., "USER" matches only "USER")
    /// </para>
    /// <para>
    /// When multiple patterns could match a discriminator value, the first registered pattern wins.
    /// </para>
    /// <para>
    /// The returned TypeHandlerRegistration allows chaining Where, WhereKey, and event handler methods
    /// to configure processing for this entity type.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await record.Process()
    ///     .WithDiscriminator("SK")
    ///     .For&lt;UserEntity&gt;("USER#*")
    ///         .Where(u => u.Status == "active")
    ///         .OnInsert(async (_, user) => await ProcessUser(user))
    ///     .For&lt;OrderEntity&gt;("ORDER#*")
    ///         .OnInsert(async (_, order) => await ProcessOrder(order))
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public TypeHandlerRegistration<TEntity> For<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TEntity>(string discriminatorPattern)
        where TEntity : class
    {
        if (string.IsNullOrWhiteSpace(discriminatorPattern))
        {
            throw new ArgumentException(
                "Discriminator pattern cannot be null or whitespace.",
                nameof(discriminatorPattern));
        }

        // Create a new handler registration for this entity type with parent builder reference
        var registration = new TypeHandlerRegistration<TEntity>(this);
        
        // Store the registration with the pattern as the key
        _handlers[discriminatorPattern] = registration;

        return registration;
    }

    /// <summary>
    /// Registers a handler for stream records with unrecognized discriminator values.
    /// </summary>
    /// <param name="handler">
    /// An async function that receives the raw stream record for processing or logging.
    /// </param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This handler is called when a stream record's discriminator value doesn't match any
    /// registered For&lt;TEntity&gt; patterns. It's useful for logging unknown entity types,
    /// handling migration scenarios, or implementing fallback logic.
    /// </para>
    /// <para>
    /// If no OnUnknownType handler is registered, records with unmatched discriminator values
    /// are silently skipped.
    /// </para>
    /// <para>
    /// The handler receives the raw DynamodbStreamRecord, allowing access to all stream record
    /// properties including Keys, NewImage, OldImage, and EventName.
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
    ///     .OnUnknownType(async record =>
    ///     {
    ///         _logger.LogWarning("Unknown entity type in stream: {Keys}", record.Dynamodb.Keys);
    ///     })
    ///     .ProcessAsync();
    /// </code>
    /// </example>
    public DiscriminatorStreamProcessorBuilder OnUnknownType(
        Func<DynamoDBEvent.DynamodbStreamRecord, Task> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _unknownTypeHandler = handler;
        return this;
    }

    /// <summary>
    /// Processes the stream record asynchronously by routing to the appropriate entity handler.
    /// </summary>
    /// <returns>A task representing the asynchronous processing operation.</returns>
    /// <remarks>
    /// <para>
    /// The processing pipeline:
    /// 1. Extract discriminator value from NewImage (INSERT/MODIFY) or OldImage (REMOVE)
    /// 2. Find matching handler registration (exact or pattern match)
    /// 3. Delegate to TypeHandlerRegistration.ProcessAsync if match found
    /// 4. Call OnUnknownType handler if no match and handler is registered
    /// 5. Skip silently if no match and no OnUnknownType handler
    /// </para>
    /// <para>
    /// Pattern matching follows first-match-wins semantics - the first registered pattern
    /// that matches the discriminator value will be used.
    /// </para>
    /// </remarks>
    public async Task ProcessAsync()
    {
        // Step 1: Extract discriminator value from NewImage or OldImage
        var discriminatorValue = ExtractDiscriminatorValue();

        // If discriminator field is missing, treat as unknown type
        if (discriminatorValue == null)
        {
            if (_unknownTypeHandler != null)
            {
                await _unknownTypeHandler(_record);
            }
            return;
        }

        // Step 2: Find matching handler registration (exact or pattern match)
        // First-match-wins: iterate through registered handlers in order
        TypeHandlerRegistration? matchedHandler = null;

        foreach (var kvp in _handlers)
        {
            var pattern = kvp.Key;
            var handler = kvp.Value;

            if (MatchesPattern(discriminatorValue, pattern))
            {
                matchedHandler = handler;
                break; // First match wins
            }
        }

        // Step 3: Delegate to TypeHandlerRegistration.ProcessAsync if match found
        if (matchedHandler != null)
        {
            await matchedHandler.ProcessAsync(_record);
            return;
        }

        // Step 4: Call OnUnknownType handler if no match and handler is registered
        if (_unknownTypeHandler != null)
        {
            await _unknownTypeHandler(_record);
        }

        // Step 5: Skip silently if no match and no OnUnknownType handler
    }

    /// <summary>
    /// Extracts the discriminator value from the stream record.
    /// </summary>
    /// <returns>The discriminator value, or null if not found.</returns>
    /// <remarks>
    /// For INSERT and MODIFY events, the discriminator is extracted from NewImage.
    /// For REMOVE events, the discriminator is extracted from OldImage.
    /// </remarks>
    private string? ExtractDiscriminatorValue()
    {
        // Determine which image to use based on event type
        var image = _record.EventName == "REMOVE"
            ? _record.Dynamodb?.OldImage
            : _record.Dynamodb?.NewImage;

        if (image == null)
        {
            return null;
        }

        // Try to get the discriminator field from the image
        if (!image.TryGetValue(_discriminatorField, out var attributeValue))
        {
            return null;
        }

        // Extract the string value from the AttributeValue
        // The discriminator is typically a string (S) attribute
        return attributeValue?.S;
    }

    /// <summary>
    /// Determines if a discriminator value matches a pattern.
    /// </summary>
    /// <param name="discriminatorValue">The actual discriminator value from the stream record.</param>
    /// <param name="pattern">The pattern to match against (may contain wildcards).</param>
    /// <returns>True if the value matches the pattern; otherwise, false.</returns>
    private bool MatchesPattern(string discriminatorValue, string pattern)
    {
        if (string.IsNullOrEmpty(discriminatorValue) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        // Check for wildcard patterns
        var hasLeadingWildcard = pattern.StartsWith("*");
        var hasTrailingWildcard = pattern.EndsWith("*");

        if (hasLeadingWildcard && hasTrailingWildcard)
        {
            // Contains match: *#USER#*
            var searchTerm = pattern.Trim('*');
            return discriminatorValue.Contains(searchTerm);
        }
        else if (hasTrailingWildcard)
        {
            // Prefix match: USER#*
            var prefix = pattern.TrimEnd('*');
            return discriminatorValue.StartsWith(prefix);
        }
        else if (hasLeadingWildcard)
        {
            // Suffix match: *#USER
            var suffix = pattern.TrimStart('*');
            return discriminatorValue.EndsWith(suffix);
        }
        else
        {
            // Exact match: USER
            return discriminatorValue == pattern;
        }
    }
}
