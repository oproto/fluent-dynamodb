using FluentDynamoDb.OperationSamples.Models;
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
/// Month (int), and Day (int). The source generator detects this and produces overloads
/// on the entity accessor (<c>table.ScheduledEvents</c>) that accept these typed parameters
/// directly, followed by the sort key value.
/// </para>
/// </remarks>
public static class TypedOverloadSamples
{
    /// <summary>
    /// Demonstrates the typed Get overload for an entity with a computed partition key.
    /// Instead of manually building the key string "2024#12#25", individual typed parameters
    /// are passed directly to the generated method.
    /// </summary>
    public static async Task<ScheduledEvent?> TypedGetAsync(OrdersTable table)
    {
        // The source generator automatically produces this typed Get overload for entities
        // whose partition key uses [Computed] with multiple source properties. Instead of
        // manually constructing "2024#12#25", pass each component as a typed parameter.
        // The generated overload signature is: Get(int year, int month, int day, string sk)
        var scheduledEvent = await table.ScheduledEvents.Get(2024, 12, 25, "christmas-party").GetItemAsync();

        return scheduledEvent;
    }

    /// <summary>
    /// Demonstrates the typed Delete overload for an entity with a computed partition key.
    /// The source generator produces a Delete method accepting individual typed parameters
    /// matching the computed key's source property types.
    /// </summary>
    public static async Task TypedDeleteAsync(OrdersTable table)
    {
        // The source generator automatically produces this typed Delete overload for entities
        // whose partition key uses [Computed] with multiple source properties. The key
        // "2024#12#25" is composed internally from the individual typed parameters.
        // The generated overload signature is: Delete(int year, int month, int day, string sk)
        await table.ScheduledEvents.Delete(2024, 12, 25, "christmas-party").DeleteAsync();
    }

    /// <summary>
    /// Demonstrates the typed Update overload for an entity with a computed partition key,
    /// chained with a Set() lambda expression to update a non-key property.
    /// </summary>
    public static async Task TypedUpdateAsync(OrdersTable table)
    {
        // The source generator automatically produces this typed Update overload for entities
        // whose partition key uses [Computed] with multiple source properties. The composite
        // key is built internally from the individual typed parameters, and the fluent Set()
        // lambda uses the generated ScheduledEventUpdateModel to update non-key properties.
        // The generated overload signature is: Update(int year, int month, int day, string sk)
        await table.ScheduledEvents.Update(2024, 12, 25, "christmas-party")
            .Set(x => new ScheduledEventUpdateModel { Title = "Holiday" })
            .UpdateAsync();
    }
}
