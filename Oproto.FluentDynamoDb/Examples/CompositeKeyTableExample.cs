using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Examples;

/// <summary>
/// Example implementation of a manual table definition with a composite key (partition key + sort key).
/// This demonstrates how to create a table class that provides convenient key-specific
/// overloads for Get, Update, and Delete operations when working with composite keys.
/// </summary>
/// <remarks>
/// This table represents an Orders table with:
/// - Partition Key: "customer_id" (customer identifier)
/// - Sort Key: "order_id" (order identifier)
/// - GSI: StatusIndex with partition key "status" and sort key "created_at"
/// - GSI: ProductIndex with partition key "product_id" and sort key "order_id"
/// 
/// This pattern is common in single-table design where you want to group related items
/// together (e.g., all orders for a customer) while maintaining unique identifiers.
/// </remarks>
/// <example>
/// <code>
/// // Initialize the table
/// var ordersTable = new OrdersTable(dynamoDbClient);
/// 
/// // Get a specific order
/// var order = await ordersTable.Get("customer-123", "order-456")
///     .WithProjection("customer_id, order_id, amount, status")
///     .ToDynamoDbResponseAsync();
/// 
/// // Update an order
/// await ordersTable.Update("customer-123", "order-456")
///     .Set("status", "SHIPPED")
///     .Set("shipped_at", DateTime.UtcNow)
///     .ToDynamoDbResponseAsync();
/// 
/// // Delete an order
/// await ordersTable.Delete("customer-123", "order-456")
///     .WithCondition("attribute_exists(customer_id)")
///     .ToDynamoDbResponseAsync();
/// 
/// // Query all orders for a customer
/// var customerOrders = await ordersTable
///     .Query("customer_id = {0}", "customer-123")
///     .ToDynamoDbResponseAsync();
/// 
/// // Query orders for a customer within a date range
/// var recentOrders = await ordersTable
///     .Query("customer_id = {0} AND order_id > {1}", "customer-123", "2024-01-01#")
///     .ToDynamoDbResponseAsync();
/// 
/// // Query orders by status
/// var pendingOrders = await ordersTable.StatusIndex
///     .Query("status = {0}", "PENDING")
///     .ToDynamoDbResponseAsync();
/// 
/// // Query orders for a specific product
/// var productOrders = await ordersTable.ProductIndex
///     .Query("product_id = {0}", "product-789")
///     .ToDynamoDbResponseAsync();
/// </code>
/// </example>
public class OrdersTable : DynamoDbTableBase
{
    /// <summary>
    /// Initializes a new instance of the OrdersTable.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    public OrdersTable(IAmazonDynamoDB client) 
        : base(client, "Orders")
    {
    }

    /// <summary>
    /// Initializes a new instance of the OrdersTable with a logger.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="logger">Logger for DynamoDB operations.</param>
    public OrdersTable(IAmazonDynamoDB client, IDynamoDbLogger logger) 
        : base(client, "Orders", logger)
    {
    }

    /// <summary>
    /// Gets an order by customer ID (partition key) and order ID (sort key).
    /// </summary>
    /// <param name="customerId">The customer ID (partition key value).</param>
    /// <param name="orderId">The order ID (sort key value).</param>
    /// <returns>A GetItemRequestBuilder configured with the composite key.</returns>
    /// <example>
    /// <code>
    /// // Get order with all attributes
    /// var order = await ordersTable.Get("customer-123", "order-456").ToDynamoDbResponseAsync();
    /// 
    /// // Get order with specific attributes
    /// var order = await ordersTable.Get("customer-123", "order-456")
    ///     .WithProjection("customer_id, order_id, amount, status")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Get order with consistent read
    /// var order = await ordersTable.Get("customer-123", "order-456")
    ///     .WithConsistentRead()
    ///     .ToDynamoDbResponseAsync();
    /// </code>
    /// </example>
    // Note: In actual implementation, replace 'PlaceholderEntity' with your entity type
    public GetItemRequestBuilder<PlaceholderEntity> Get(string customerId, string orderId) => 
        base.Get<PlaceholderEntity>().WithKey("customer_id", customerId, "order_id", orderId);

    /// <summary>
    /// Updates an order by customer ID (partition key) and order ID (sort key).
    /// </summary>
    /// <param name="customerId">The customer ID (partition key value).</param>
    /// <param name="orderId">The order ID (sort key value).</param>
    /// <returns>An UpdateItemRequestBuilder configured with the composite key.</returns>
    /// <example>
    /// <code>
    /// // Update order status
    /// await ordersTable.Update("customer-123", "order-456")
    ///     .Set("status", "SHIPPED")
    ///     .Set("shipped_at", DateTime.UtcNow)
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Update with condition
    /// await ordersTable.Update("customer-123", "order-456")
    ///     .Set("status", "CANCELLED")
    ///     .WithCondition("status = {0}", "PENDING")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Add items to a list
    /// await ordersTable.Update("customer-123", "order-456")
    ///     .Add("tracking_numbers", new List&lt;string&gt; { "TRACK123" })
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Increment a counter
    /// await ordersTable.Update("customer-123", "order-456")
    ///     .Add("view_count", 1)
    ///     .ToDynamoDbResponseAsync();
    /// </code>
    /// </example>
    // Note: In actual implementation, replace 'PlaceholderEntity' with your entity type
    public UpdateItemRequestBuilder<PlaceholderEntity> Update(string customerId, string orderId) => 
        base.Update<PlaceholderEntity>().WithKey("customer_id", customerId, "order_id", orderId);

    /// <summary>
    /// Deletes an order by customer ID (partition key) and order ID (sort key).
    /// </summary>
    /// <param name="customerId">The customer ID (partition key value).</param>
    /// <param name="orderId">The order ID (sort key value).</param>
    /// <returns>A DeleteItemRequestBuilder configured with the composite key.</returns>
    /// <example>
    /// <code>
    /// // Delete order
    /// await ordersTable.Delete("customer-123", "order-456").ToDynamoDbResponseAsync();
    /// 
    /// // Delete with condition
    /// await ordersTable.Delete("customer-123", "order-456")
    ///     .WithCondition("status = {0}", "CANCELLED")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Delete and return old values
    /// var deletedOrder = await ordersTable.Delete("customer-123", "order-456")
    ///     .WithReturnValues(ReturnValue.ALL_OLD)
    ///     .ToDynamoDbResponseAsync();
    /// </code>
    /// </example>
    // Note: In actual implementation, replace 'PlaceholderEntity' with your entity type
    public DeleteItemRequestBuilder<PlaceholderEntity> Delete(string customerId, string orderId) => 
        base.Delete<PlaceholderEntity>().WithKey("customer_id", customerId, "order_id", orderId);

    /// <summary>
    /// Global Secondary Index for querying orders by status and creation date.
    /// Index structure:
    /// - Partition Key: status
    /// - Sort Key: created_at
    /// - Projection: customer_id, order_id, status, amount, created_at
    /// </summary>
    /// <example>
    /// <code>
    /// // Query all pending orders
    /// var pendingOrders = await ordersTable.StatusIndex
    ///     .Query("status = {0}", "PENDING")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Query pending orders created after a specific date
    /// var recentPending = await ordersTable.StatusIndex
    ///     .Query("status = {0} AND created_at > {1}", "PENDING", "2024-01-01")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Query shipped orders in a date range
    /// var shippedInRange = await ordersTable.StatusIndex
    ///     .Query("status = {0} AND created_at BETWEEN {1} AND {2}", 
    ///         "SHIPPED", "2024-01-01", "2024-12-31")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Query with descending order (most recent first)
    /// var recentShipped = await ordersTable.StatusIndex.Query()
    ///     .Where("status = {0}", "SHIPPED")
    ///     .WithScanIndexForward(false)
    ///     .WithLimit(10)
    ///     .ToDynamoDbResponseAsync();
    /// </code>
    /// </example>
    public DynamoDbIndex StatusIndex => 
        new DynamoDbIndex(this, "StatusIndex", "customer_id, order_id, status, amount, created_at");

    /// <summary>
    /// Global Secondary Index for querying orders by product.
    /// Index structure:
    /// - Partition Key: product_id
    /// - Sort Key: order_id
    /// - Projection: customer_id, order_id, product_id, quantity, amount
    /// 
    /// This index is useful for finding all orders that contain a specific product.
    /// </summary>
    /// <example>
    /// <code>
    /// // Query all orders containing a specific product
    /// var productOrders = await ordersTable.ProductIndex
    ///     .Query("product_id = {0}", "product-789")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Query orders for a product within a specific order ID range
    /// var productOrdersInRange = await ordersTable.ProductIndex
    ///     .Query("product_id = {0} AND order_id > {1}", "product-789", "2024-01-01#")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Query with begins_with on sort key
    /// var productOrdersPrefix = await ordersTable.ProductIndex
    ///     .Query("product_id = {0} AND begins_with(order_id, {1})", 
    ///         "product-789", "2024-01")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Manual query configuration with additional filters
    /// var highValueOrders = await ordersTable.ProductIndex.Query()
    ///     .Where("product_id = {0}", "product-789")
    ///     .WithFilter("amount > {0}", 100)
    ///     .WithLimit(20)
    ///     .ToDynamoDbResponseAsync();
    /// </code>
    /// </example>
    public DynamoDbIndex ProductIndex => 
        new DynamoDbIndex(this, "ProductIndex", "customer_id, order_id, product_id, quantity, amount");
}
