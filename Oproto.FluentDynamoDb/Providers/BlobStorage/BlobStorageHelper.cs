using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Helper class for coordinating blob storage operations with DynamoDB operations.
/// Used internally by request builders to invoke blob storage strategy lifecycle methods.
/// </summary>
internal static class BlobStorageHelper
{
    /// <summary>
    /// Extracts blob property information from an entity for write operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="options">The FluentDynamoDb options.</param>
    /// <returns>A list of blob property contexts, or empty if no blob properties with pending data.</returns>
    public static List<BlobPropertyContext> ExtractBlobProperties<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity>(
        TEntity entity,
        FluentDynamoDbOptions options)
        where TEntity : class
    {
        var blobProperties = new List<BlobPropertyContext>();
        
        // Use reflection to find BlobData<T> properties with pending data
        // This is AOT-safe because we're only reading property values, not creating types
        var entityType = typeof(TEntity);
        foreach (var property in entityType.GetProperties())
        {
            var propertyType = property.PropertyType;
            
            // Check if property is BlobData<T>
            if (!propertyType.IsGenericType || 
                propertyType.GetGenericTypeDefinition() != typeof(BlobData<>))
            {
                continue;
            }
            
            var blobDataValue = property.GetValue(entity);
            if (blobDataValue == null)
            {
                continue;
            }
            
            // Get HasPendingData property
            var hasPendingDataProp = propertyType.GetProperty("HasPendingData");
            var hasPendingData = hasPendingDataProp?.GetValue(blobDataValue) as bool? ?? false;
            
            if (!hasPendingData)
            {
                continue;
            }
            
            // Get the pending value
            var getPendingValueMethod = propertyType.GetMethod("GetPendingValue", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var pendingValue = getPendingValueMethod?.Invoke(blobDataValue, null);
            
            if (pendingValue == null)
            {
                continue;
            }
            
            // Get existing reference key if any
            var referenceKeyProp = propertyType.GetProperty("ReferenceKey");
            var existingReferenceKey = referenceKeyProp?.GetValue(blobDataValue) as string;
            
            // Get DynamoDB attribute name from [DynamoDbAttribute] if present
            var dynamoDbAttr = property.GetCustomAttributes(typeof(Attributes.DynamoDbAttributeAttribute), false)
                .FirstOrDefault() as Attributes.DynamoDbAttributeAttribute;
            var attributeName = dynamoDbAttr?.AttributeName ?? property.Name;
            
            // Serialize the value to a stream
            var dataStream = SerializeToStream(pendingValue, options);
            
            blobProperties.Add(new BlobPropertyContext
            {
                PropertyName = property.Name,
                AttributeName = attributeName,
                Data = dataStream,
                ContentType = DetermineContentType(pendingValue, property),
                ExistingReferenceKey = existingReferenceKey
            });
        }
        
        return blobProperties;
    }


    /// <summary>
    /// Extracts blob reference keys from an entity for delete operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <returns>A list of reference keys for blobs associated with the entity.</returns>
    public static List<string> ExtractBlobReferenceKeys<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity>(TEntity entity)
        where TEntity : class
    {
        var referenceKeys = new List<string>();
        
        var entityType = typeof(TEntity);
        foreach (var property in entityType.GetProperties())
        {
            var propertyType = property.PropertyType;
            
            // Check if property is BlobData<T>
            if (!propertyType.IsGenericType || 
                propertyType.GetGenericTypeDefinition() != typeof(BlobData<>))
            {
                continue;
            }
            
            var blobDataValue = property.GetValue(entity);
            if (blobDataValue == null)
            {
                continue;
            }
            
            // Get reference key
            var referenceKeyProp = propertyType.GetProperty("ReferenceKey");
            var referenceKey = referenceKeyProp?.GetValue(blobDataValue) as string;
            
            if (!string.IsNullOrEmpty(referenceKey))
            {
                referenceKeys.Add(referenceKey);
            }
        }
        
        return referenceKeys;
    }

    /// <summary>
    /// Extracts blob reference keys from a DynamoDB item for delete operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="item">The DynamoDB item attributes.</param>
    /// <returns>A list of reference keys for blobs associated with the entity.</returns>
    public static List<string> ExtractBlobReferenceKeysFromItem<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity>(
        Dictionary<string, AttributeValue> item)
        where TEntity : class
    {
        var referenceKeys = new List<string>();
        
        var entityType = typeof(TEntity);
        foreach (var property in entityType.GetProperties())
        {
            var propertyType = property.PropertyType;
            
            // Check if property is BlobData<T>
            if (!propertyType.IsGenericType || 
                propertyType.GetGenericTypeDefinition() != typeof(BlobData<>))
            {
                continue;
            }
            
            // Get DynamoDB attribute name
            var dynamoDbAttr = property.GetCustomAttributes(typeof(Attributes.DynamoDbAttributeAttribute), false)
                .FirstOrDefault() as Attributes.DynamoDbAttributeAttribute;
            var attributeName = dynamoDbAttr?.AttributeName ?? property.Name;
            
            // Get reference key from item
            if (item.TryGetValue(attributeName, out var attrValue) && 
                !string.IsNullOrEmpty(attrValue.S))
            {
                referenceKeys.Add(attrValue.S);
            }
        }
        
        return referenceKeys;
    }

    /// <summary>
    /// Updates the DynamoDB item with reference keys from the blob write result.
    /// </summary>
    /// <param name="item">The DynamoDB item to update.</param>
    /// <param name="result">The blob write result containing reference keys.</param>
    /// <param name="blobProperties">The blob property contexts.</param>
    public static void UpdateItemWithReferenceKeys(
        Dictionary<string, AttributeValue> item,
        BlobWriteResult result,
        IReadOnlyList<BlobPropertyContext> blobProperties)
    {
        foreach (var prop in blobProperties)
        {
            if (result.ReferenceKeys.TryGetValue(prop.PropertyName, out var referenceKey))
            {
                item[prop.AttributeName] = new AttributeValue { S = referenceKey };
            }
        }
    }

    /// <summary>
    /// Updates the entity's BlobData properties with reference keys from the blob write result.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to update.</param>
    /// <param name="result">The blob write result containing reference keys.</param>
    public static void UpdateEntityWithReferenceKeys<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity>(
        TEntity entity,
        BlobWriteResult result)
        where TEntity : class
    {
        var entityType = typeof(TEntity);
        foreach (var property in entityType.GetProperties())
        {
            var propertyType = property.PropertyType;
            
            // Check if property is BlobData<T>
            if (!propertyType.IsGenericType || 
                propertyType.GetGenericTypeDefinition() != typeof(BlobData<>))
            {
                continue;
            }
            
            if (!result.ReferenceKeys.TryGetValue(property.Name, out var referenceKey))
            {
                continue;
            }
            
            var blobDataValue = property.GetValue(entity);
            if (blobDataValue == null)
            {
                continue;
            }
            
            // Call SetReferenceKey internal method
            var setReferenceKeyMethod = propertyType.GetMethod("SetReferenceKey",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            setReferenceKeyMethod?.Invoke(blobDataValue, new object[] { referenceKey });
        }
    }


    /// <summary>
    /// Validates that blob storage is properly configured when entities have blob properties.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="options">The FluentDynamoDb options.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has [BlobStorage] properties but no blob storage provider is configured.
    /// </exception>
    public static void ValidateBlobStorageConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity>(
        FluentDynamoDbOptions options)
        where TEntity : class
    {
        if (options.BlobStorageProvider == null && HasBlobStorageProperties<TEntity>())
        {
            var entityType = typeof(TEntity);
            var blobPropertyNames = GetBlobPropertyNames<TEntity>();
            var propertyList = string.Join(", ", blobPropertyNames);
            
            throw new InvalidOperationException(
                $"Entity '{entityType.Name}' has [BlobStorage] properties ({propertyList}) but no blob storage provider is configured. " +
                "Call FluentDynamoDbOptions.WithBlobStorage() to configure a provider.");
        }
    }

    /// <summary>
    /// Gets the names of BlobData properties on an entity type.
    /// </summary>
    private static IEnumerable<string> GetBlobPropertyNames<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity>()
        where TEntity : class
    {
        var entityType = typeof(TEntity);
        return entityType.GetProperties()
            .Where(p => p.PropertyType.IsGenericType && 
                        p.PropertyType.GetGenericTypeDefinition() == typeof(BlobData<>))
            .Select(p => p.Name);
    }

    /// <summary>
    /// Executes a put operation with blob storage strategy lifecycle methods.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="entity">The entity being put.</param>
    /// <param name="item">The DynamoDB item dictionary.</param>
    /// <param name="options">The FluentDynamoDb options.</param>
    /// <param name="executeOperation">The function to execute the DynamoDB operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the DynamoDB operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has [BlobStorage] properties but no blob storage provider is configured.
    /// </exception>
    public static async Task<TResult> ExecuteWithBlobStrategyAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity, TResult>(
        TEntity entity,
        Dictionary<string, AttributeValue> item,
        FluentDynamoDbOptions options,
        Func<Task<TResult>> executeOperation,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        // Validate configuration before proceeding
        ValidateBlobStorageConfiguration<TEntity>(options);
        
        var strategy = options.BlobStorageStrategy;
        if (strategy == null)
        {
            // No strategy configured, just execute the operation
            return await executeOperation().ConfigureAwait(false);
        }
        
        var blobProperties = ExtractBlobProperties(entity, options);
        if (blobProperties.Count == 0)
        {
            // No blob properties with pending data, just execute the operation
            return await executeOperation().ConfigureAwait(false);
        }
        
        var context = new BlobWriteContext
        {
            EntityType = typeof(TEntity).Name,
            BlobProperties = blobProperties
        };
        
        try
        {
            // Step 1: Upload blobs before DynamoDB write
            var result = await strategy.OnBeforeDynamoDbWriteAsync(context, cancellationToken).ConfigureAwait(false);
            
            // Step 2: Update item with reference keys
            UpdateItemWithReferenceKeys(item, result, blobProperties);
            
            // Step 3: Execute DynamoDB operation
            TResult response;
            try
            {
                response = await executeOperation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Step 4a: Handle DynamoDB write failure
                await strategy.OnAfterDynamoDbWriteFailureAsync(context, ex, cancellationToken).ConfigureAwait(false);
                throw;
            }
            
            // Step 4b: Handle DynamoDB write success
            await strategy.OnAfterDynamoDbWriteSuccessAsync(context, cancellationToken).ConfigureAwait(false);
            
            // Step 5: Update entity with reference keys
            UpdateEntityWithReferenceKeys(entity, result);
            
            return response;
        }
        finally
        {
            // Dispose streams
            foreach (var prop in blobProperties)
            {
                prop.Data.Dispose();
            }
        }
    }

    /// <summary>
    /// Executes a delete operation with blob storage strategy lifecycle methods.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="referenceKeys">The blob reference keys to delete.</param>
    /// <param name="options">The FluentDynamoDb options.</param>
    /// <param name="executeOperation">The function to execute the DynamoDB operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the DynamoDB operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has [BlobStorage] properties but no blob storage provider is configured.
    /// </exception>
    public static async Task<TResult> ExecuteDeleteWithBlobStrategyAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity, TResult>(
        IReadOnlyList<string> referenceKeys,
        FluentDynamoDbOptions options,
        Func<Task<TResult>> executeOperation,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        // Validate configuration if there are blob reference keys
        if (referenceKeys.Count > 0)
        {
            ValidateBlobStorageConfiguration<TEntity>(options);
        }
        
        var strategy = options.BlobStorageStrategy;
        if (strategy == null || referenceKeys.Count == 0)
        {
            // No strategy configured or no blob references, just execute the operation
            return await executeOperation().ConfigureAwait(false);
        }
        
        var context = new BlobDeleteContext
        {
            EntityType = typeof(TEntity).Name,
            ReferenceKeys = referenceKeys
        };
        
        // Step 1: Prepare for blob cleanup
        context = await strategy.OnBeforeDynamoDbDeleteAsync(context, cancellationToken).ConfigureAwait(false);
        
        // Step 2: Execute DynamoDB delete operation
        var response = await executeOperation().ConfigureAwait(false);
        
        // Step 3: Clean up blobs after successful delete
        await strategy.OnAfterDynamoDbDeleteSuccessAsync(context, cancellationToken).ConfigureAwait(false);
        
        return response;
    }

    /// <summary>
    /// Checks if an entity type has any BlobData properties.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>True if the entity has blob storage properties.</returns>
    public static bool HasBlobStorageProperties<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity>()
        where TEntity : class
    {
        var entityType = typeof(TEntity);
        return entityType.GetProperties()
            .Any(p => p.PropertyType.IsGenericType && 
                      p.PropertyType.GetGenericTypeDefinition() == typeof(BlobData<>));
    }

    private static Stream SerializeToStream(object value, FluentDynamoDbOptions options)
    {
        // Handle byte[] directly
        if (value is byte[] bytes)
        {
            return new MemoryStream(bytes);
        }
        
        // Handle Stream directly
        if (value is Stream stream)
        {
            // Copy to a new MemoryStream to ensure we can read it
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        
        // For other types, use JSON serialization if configured
        if (options.JsonSerializer != null)
        {
            // Use reflection to call the generic Serialize<T> method
            var serializeMethod = typeof(IJsonBlobSerializer).GetMethod("Serialize")!
                .MakeGenericMethod(value.GetType());
            var json = (string)serializeMethod.Invoke(options.JsonSerializer, new[] { value })!;
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        }
        
        // Fallback: convert to string and encode as UTF-8
        var stringValue = value.ToString() ?? string.Empty;
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stringValue));
    }

    private static string? DetermineContentType(object value, System.Reflection.PropertyInfo property)
    {
        // Check for [JsonBlob] attribute
        var hasJsonBlob = property.GetCustomAttributes(typeof(Attributes.JsonBlobAttribute), false).Any();
        if (hasJsonBlob)
        {
            return "application/json";
        }
        
        // Check value type
        if (value is byte[])
        {
            return "application/octet-stream";
        }
        
        if (value is string)
        {
            return "text/plain";
        }
        
        // Default to JSON for complex types
        return "application/json";
    }
}
