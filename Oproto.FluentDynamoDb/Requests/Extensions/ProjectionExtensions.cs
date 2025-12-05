using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Mapping;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for automatic projection application and hydration.
/// These extensions detect projection models and automatically apply projection expressions.
/// </summary>
public static class ProjectionExtensions
{
    /// <summary>
    /// Executes query and returns results as specified projection type.
    /// Automatically applies projection expression based on TResult's ProjectionExpression.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The projection model type implementing IProjectionModel.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of items of type TResult.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    /// <exception cref="ProjectionValidationException">Thrown when GSI projection constraint is violated.</exception>
    public static async Task<List<TResult>> ToListAsync<TEntity, TResult>(
        this QueryRequestBuilder<TEntity> builder,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResult : class, IProjectionModel<TResult>
    {
        try
        {
            // Apply projection if no manual projection was set
            builder = ApplyProjectionIfNeeded<TEntity, TResult>(builder);

            // Execute the query
            var response = await builder.ToDynamoDbResponseAsync(cancellationToken);

            // Hydrate results using the interface method
            return HydrateResults<TResult>(response.Items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not ProjectionValidationException)
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Query operation and map to {typeof(TResult).Name}. Error: {ex.Message}", ex);
        }
    }


    /// <summary>
    /// Executes query and returns results as specified discriminated projection type.
    /// Automatically applies projection expression and filters by discriminator value.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The discriminated projection model type.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of items of type TResult that match the discriminator.</returns>
    public static async Task<List<TResult>> ToDiscriminatedListAsync<TEntity, TResult>(
        this QueryRequestBuilder<TEntity> builder,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResult : class, IDiscriminatedProjection<TResult>
    {
        try
        {
            // Apply projection if no manual projection was set
            builder = ApplyProjectionIfNeeded<TEntity, TResult>(builder);

            // Execute the query
            var response = await builder.ToDynamoDbResponseAsync(cancellationToken);

            // Hydrate results with discriminator filtering
            return HydrateDiscriminatedResults<TResult>(response.Items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not ProjectionValidationException)
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Query operation and map to {typeof(TResult).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies projection expression if no manual projection has been set.
    /// </summary>
    private static QueryRequestBuilder<TEntity> ApplyProjectionIfNeeded<TEntity, TResult>(
        QueryRequestBuilder<TEntity> builder)
        where TEntity : class
        where TResult : IProjectionModel<TResult>
    {
        // Check if a manual projection was already set
        var request = builder.ToQueryRequest();
        if (!string.IsNullOrEmpty(request.ProjectionExpression))
        {
            return builder;
        }

        // Get projection expression from the interface (no reflection!)
        var projectionExpression = TResult.ProjectionExpression;
        
        if (!string.IsNullOrEmpty(projectionExpression))
        {
            builder = builder.WithProjection(projectionExpression);
        }

        return builder;
    }

    /// <summary>
    /// Hydrates a list of projection models from DynamoDB response items.
    /// </summary>
    private static List<TResult> HydrateResults<TResult>(List<Dictionary<string, AttributeValue>> items)
        where TResult : IProjectionModel<TResult>
    {
        var results = new List<TResult>();

        foreach (var item in items)
        {
            try
            {
                // Use the interface method directly (no reflection!)
                var result = TResult.FromDynamoDb(item);
                results.Add(result);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or ArgumentNullException)
            {
                throw new DynamoDbMappingException(
                    $"Failed to hydrate projection {typeof(TResult).Name} from DynamoDB item. " +
                    $"A required attribute may be missing. Error: {ex.Message}",
                    typeof(TResult),
                    MappingOperation.FromDynamoDb,
                    item,
                    innerException: ex);
            }
        }

        return results;
    }

    /// <summary>
    /// Hydrates a list of discriminated projection models, filtering by discriminator value.
    /// </summary>
    private static List<TResult> HydrateDiscriminatedResults<TResult>(List<Dictionary<string, AttributeValue>> items)
        where TResult : IDiscriminatedProjection<TResult>
    {
        var results = new List<TResult>();
        
        var discriminatorProperty = TResult.DiscriminatorProperty;
        var expectedValue = TResult.DiscriminatorValue;

        foreach (var item in items)
        {
            // Skip items that don't match the discriminator
            if (!string.IsNullOrEmpty(discriminatorProperty) && !string.IsNullOrEmpty(expectedValue))
            {
                if (!item.TryGetValue(discriminatorProperty, out var discriminatorAttr) ||
                    discriminatorAttr.S != expectedValue)
                {
                    continue;
                }
            }

            try
            {
                var result = TResult.FromDynamoDb(item);
                results.Add(result);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or ArgumentNullException)
            {
                throw new DynamoDbMappingException(
                    $"Failed to hydrate projection {typeof(TResult).Name} from DynamoDB item. " +
                    $"A required attribute may be missing. Error: {ex.Message}",
                    typeof(TResult),
                    MappingOperation.FromDynamoDb,
                    item,
                    innerException: ex);
            }
        }

        return results;
    }
}
