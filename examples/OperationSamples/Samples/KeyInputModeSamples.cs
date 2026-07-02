using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates explicit KeyInputMode parameter usage on Get operations.
/// 
/// KeyInputMode controls how key values are interpreted before being sent to DynamoDB.
/// By default, operations use <see cref="KeyInputMode.Default"/> which defers to
/// <see cref="FluentDynamoDbOptions.DefaultKeyInputMode"/> (resolves to Auto when not
/// explicitly configured). These samples show how to override the mode per-operation.
/// </summary>
public static class KeyInputModeSamples
{
    /// <summary>
    /// Demonstrates <see cref="KeyInputMode.Raw"/> — the value is passed to DynamoDB unchanged.
    /// 
    /// Use Raw mode when you already have a fully-prefixed key value (e.g., from a previous
    /// query result, a stream event, or an external reference) and you want to ensure no
    /// prefix logic is applied. The caller is fully responsible for providing the correct
    /// prefixed value.
    /// </summary>
    public static async Task<Order?> GetWithRawModeAsync(OrdersTable table)
    {
        // KeyInputMode.Raw — the value "ORDER#12345" is used as-is without modification.
        // Use this when you already have the fully-prefixed key (e.g., from a previous read
        // or a DynamoDB stream event). No prefix detection or prepending occurs.
        // KeyInputMode.Default defers to FluentDynamoDbOptions.DefaultKeyInputMode which
        // resolves to Auto when not explicitly configured.
        var order = await table.Orders.Get("ORDER#12345", "META", KeyInputMode.Raw).GetItemAsync();

        return order;
    }

    /// <summary>
    /// Demonstrates <see cref="KeyInputMode.Auto"/> — detects whether the prefix is already
    /// present and prepends it only if absent.
    /// 
    /// Use Auto mode when you have a raw component value (e.g., just the order ID from user
    /// input) and want the system to automatically apply the configured "ORDER#" prefix.
    /// Auto performs an ordinal case-sensitive StartsWith check: if the value already starts
    /// with the prefix + separator, it passes through unchanged; otherwise, it prepends.
    /// </summary>
    public static async Task<Order?> GetWithAutoModeAsync(OrdersTable table)
    {
        // KeyInputMode.Auto — the raw value "12345" is passed; the system detects the
        // "ORDER#" prefix is absent and automatically prepends it, resulting in "ORDER#12345".
        // Use this when you have just the component value (e.g., from user input or a
        // business identifier) and want prefix application handled for you.
        // KeyInputMode.Default defers to FluentDynamoDbOptions.DefaultKeyInputMode which
        // resolves to Auto when not explicitly configured.
        var order = await table.Orders.Get("12345", "META", KeyInputMode.Auto).GetItemAsync();

        return order;
    }

    /// <summary>
    /// Shows both modes side-by-side retrieving the same logical item, making the behavioral
    /// difference directly observable. Both calls target order "12345" but express the key
    /// value differently.
    /// </summary>
    public static async Task<(Order? raw, Order? auto)> GetSameItemBothModesAsync(OrdersTable table)
    {
        // Both operations retrieve the same item: the order with ID "12345" and sort key "META".
        // The difference is how the partition key value is expressed:

        // Raw: caller provides the complete prefixed key — no transformation applied
        var orderViaRaw = await table.Orders.Get("ORDER#12345", "META", KeyInputMode.Raw).GetItemAsync();

        // Auto: caller provides just the raw component — prefix "ORDER#" is prepended automatically
        var orderViaAuto = await table.Orders.Get("12345", "META", KeyInputMode.Auto).GetItemAsync();

        // Both retrieve the same DynamoDB item (pk = "ORDER#12345", sk = "META")
        return (orderViaRaw, orderViaAuto);
    }
}
