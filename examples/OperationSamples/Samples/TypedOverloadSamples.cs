using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates typed convenience overloads for entities with computed keys.
/// 
/// When an entity uses <c>[Computed("Year", "Month", "Day", Separator = "#")]</c> on its
/// partition key, the source generator automatically produces Get, Delete, and Update methods
/// that accept individual typed parameters matching the source property types. This eliminates
/// the need to manually build composite key strings like "2024#12#25".
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ScheduledEvent"/> entity has a partition key computed from Year (int),
/// Month (int), and Day (int). The source generator produces overloads
/// on the entity accessor (<c>table.ScheduledEvents</c>) that accept these typed parameters
/// directly, followed by the sort key value.
/// </para>
/// <para>
/// In addition to builder-returning overloads, the generator also produces one-shot async
/// convenience methods (<c>GetAsync</c>, <c>DeleteAsync</c>) that combine key composition
/// with terminal method execution in a single call.
/// </para>
/// </remarks>
public static class TypedOverloadSamples
{
    /// <summary>
    /// Demonstrates the typed GetAsync convenience method for an entity with a computed partition key.
    /// This is the simplest way to retrieve an item — a single call with typed parameters.
    /// </summary>
    public static async Task<ScheduledEvent?> TypedGetAsyncConvenience(OrdersTable table)
    {
        // The source generator produces GetAsync which combines Get() + GetItemAsync() in one call.
        // No need to chain .GetItemAsync() — just pass the typed parameters and await.
        var scheduledEvent = await table.ScheduledEvents.GetAsync(2024, 12, 25, "christmas-party");

        return scheduledEvent;
    }

    /// <summary>
    /// Demonstrates the typed DeleteAsync convenience method for an entity with a computed partition key.
    /// This is the simplest way to delete an item — a single call with typed parameters.
    /// </summary>
    public static async Task TypedDeleteAsyncConvenience(OrdersTable table)
    {
        // The source generator produces DeleteAsync which combines Delete() + DeleteAsync() in one call.
        // Supports an optional KeyCondition parameter (defaults to KeyCondition.None).
        await table.ScheduledEvents.DeleteAsync(2024, 12, 25, "christmas-party");

        // With a key condition — fail if the item doesn't exist
        await table.ScheduledEvents.DeleteAsync(2024, 12, 25, "christmas-party", KeyCondition.MustExist);
    }

    /// <summary>
    /// Demonstrates the typed Get builder overload for an entity with a computed partition key.
    /// Use the builder pattern when you need to chain additional options like projections
    /// or consistent reads before executing.
    /// </summary>
    public static async Task<ScheduledEvent?> TypedGetWithBuilder(OrdersTable table)
    {
        // The builder-returning overload is useful when you need to add options before executing.
        // For simple gets without options, prefer GetAsync() above.
        var scheduledEvent = await table.ScheduledEvents.Get(2024, 12, 25, "christmas-party")
            .UsingConsistentRead()
            .GetItemAsync();

        return scheduledEvent;
    }

    /// <summary>
    /// Demonstrates the typed Delete builder overload for an entity with a computed partition key.
    /// Use the builder pattern when you need to add a condition expression before executing.
    /// </summary>
    public static async Task TypedDeleteWithBuilder(OrdersTable table)
    {
        // The builder-returning overload is useful when you need conditional deletes.
        // For simple deletes, prefer DeleteAsync() above.
        await table.ScheduledEvents.Delete(2024, 12, 25, "christmas-party")
            .Where(x => x.Title == "christmas-party")
            .DeleteAsync();
    }

    /// <summary>
    /// Demonstrates the typed Update overload for an entity with a computed partition key,
    /// chained with a Set() lambda expression to update a non-key property.
    /// </summary>
    public static async Task TypedUpdateAsync(OrdersTable table)
    {
        // Update always uses the builder pattern since you need to specify the Set() clause.
        // The generated overload signature is: Update(int year, int month, int day, string sk)
        await table.ScheduledEvents.Update(2024, 12, 25, "christmas-party")
            .Set(x => new ScheduledEventUpdateModel { Title = "Holiday" })
            .UpdateAsync();
    }
}
