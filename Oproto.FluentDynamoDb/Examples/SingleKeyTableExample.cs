using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Examples;

/// <summary>
/// Example implementation of a manual table definition with a single partition key.
/// This demonstrates how to create a table class that provides convenient key-specific
/// overloads for Get, Update, and Delete operations.
/// </summary>
/// <remarks>
/// This table represents a simple Users table with:
/// - Partition Key: "id" (user identifier)
/// - GSI: EmailIndex with partition key "email"
/// - GSI: StatusIndex with partition key "status" and sort key "created_at"
/// </remarks>
/// <example>
/// <code>
/// // Initialize the table
/// var usersTable = new UsersTable(dynamoDbClient);
/// 
/// // Get a user by ID (Primary API - returns entity, populates context)
/// var user = await usersTable.Get("user-123")
///     .WithProjection("id, name, email")
///     .GetItemAsync();
/// 
/// // Access operation metadata via context
/// var context = DynamoDbOperationContext.Current;
/// Console.WriteLine($"Consumed capacity: {context?.ConsumedCapacity?.CapacityUnits}");
/// 
/// // Update a user (Primary API - returns void, populates context)
/// await usersTable.Update("user-123")
///     .Set("last_login", DateTime.UtcNow)
///     .UpdateAsync();
/// 
/// // Delete a user (Primary API - returns void, populates context)
/// await usersTable.Delete("user-123")
///     .WithCondition("attribute_exists(id)")
///     .DeleteAsync();
/// 
/// // Query by email using GSI (Primary API - returns list, populates context)
/// var usersByEmail = await usersTable.EmailIndex
///     .Query("email = {0}", "user@example.com")
///     .ToListAsync&lt;User&gt;();
/// 
/// // Query active users created after a date
/// var activeUsers = await usersTable.StatusIndex
///     .Query("status = {0} AND created_at > {1}", "ACTIVE", "2024-01-01")
///     .ToListAsync&lt;User&gt;();
/// 
/// // Advanced API - get raw AWS response without context population
/// var rawResponse = await usersTable.Get("user-123")
///     .ToDynamoDbResponseAsync();
/// var entity = rawResponse.ToEntity&lt;User&gt;();
/// </code>
/// </example>
public class UsersTable : DynamoDbTableBase
{
    /// <summary>
    /// Initializes a new instance of the UsersTable.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    public UsersTable(IAmazonDynamoDB client) 
        : base(client, "Users")
    {
    }

    /// <summary>
    /// Initializes a new instance of the UsersTable with a logger.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="logger">Logger for DynamoDB operations.</param>
    public UsersTable(IAmazonDynamoDB client, IDynamoDbLogger logger) 
        : base(client, "Users", logger)
    {
    }

    /// <summary>
    /// Gets a user by their ID (partition key).
    /// </summary>
    /// <param name="userId">The user ID (partition key value).</param>
    /// <returns>A GetItemRequestBuilder configured with the user ID.</returns>
    /// <example>
    /// <code>
    /// // Get user with all attributes (Primary API)
    /// var user = await usersTable.Get("user-123").GetItemAsync();
    /// 
    /// // Get user with specific attributes
    /// var user = await usersTable.Get("user-123")
    ///     .WithProjection("id, name, email")
    ///     .GetItemAsync();
    /// 
    /// // Get user with consistent read
    /// var user = await usersTable.Get("user-123")
    ///     .WithConsistentRead()
    ///     .GetItemAsync();
    /// 
    /// // Advanced API - get raw response
    /// var response = await usersTable.Get("user-123")
    ///     .ToDynamoDbResponseAsync();
    /// var user = response.ToEntity&lt;User&gt;();
    /// </code>
    /// </example>
    // Note: In actual implementation, replace 'PlaceholderEntity' with your entity type
    public GetItemRequestBuilder<PlaceholderEntity> Get(string userId) => 
        base.Get<PlaceholderEntity>().WithKey("id", userId);

    /// <summary>
    /// Updates a user by their ID (partition key).
    /// </summary>
    /// <param name="userId">The user ID (partition key value).</param>
    /// <returns>An UpdateItemRequestBuilder configured with the user ID.</returns>
    /// <example>
    /// <code>
    /// // Update user attributes (Primary API)
    /// await usersTable.Update("user-123")
    ///     .Set("name", "John Doe")
    ///     .Set("email", "john@example.com")
    ///     .UpdateAsync();
    /// 
    /// // Update with condition
    /// await usersTable.Update("user-123")
    ///     .Set("status", "INACTIVE")
    ///     .WithCondition("attribute_exists(id)")
    ///     .UpdateAsync();
    /// 
    /// // Increment a counter and access pre-operation values
    /// await usersTable.Update("user-123")
    ///     .Add("login_count", 1)
    ///     .Set("last_login", DateTime.UtcNow)
    ///     .WithReturnValues(ReturnValue.ALL_OLD)
    ///     .UpdateAsync();
    /// 
    /// var context = DynamoDbOperationContext.Current;
    /// var oldUser = context?.DeserializePreOperationValue&lt;User&gt;();
    /// 
    /// // Advanced API - get raw response
    /// var response = await usersTable.Update("user-123")
    ///     .Set("status", "INACTIVE")
    ///     .WithReturnValues(ReturnValue.ALL_NEW)
    ///     .ToDynamoDbResponseAsync();
    /// var updatedUser = response.ToPostOperationEntity&lt;User&gt;();
    /// </code>
    /// </example>
    // Note: In actual implementation, replace 'PlaceholderEntity' with your entity type
    public UpdateItemRequestBuilder<PlaceholderEntity> Update(string userId) => 
        base.Update<PlaceholderEntity>().WithKey("id", userId);

    /// <summary>
    /// Deletes a user by their ID (partition key).
    /// </summary>
    /// <param name="userId">The user ID (partition key value).</param>
    /// <returns>A DeleteItemRequestBuilder configured with the user ID.</returns>
    /// <example>
    /// <code>
    /// // Delete user (Primary API)
    /// await usersTable.Delete("user-123").DeleteAsync();
    /// 
    /// // Delete with condition
    /// await usersTable.Delete("user-123")
    ///     .WithCondition("attribute_exists(id)")
    ///     .DeleteAsync();
    /// 
    /// // Delete and access old values via context
    /// await usersTable.Delete("user-123")
    ///     .WithReturnValues(ReturnValue.ALL_OLD)
    ///     .DeleteAsync();
    /// 
    /// var context = DynamoDbOperationContext.Current;
    /// var deletedUser = context?.DeserializePreOperationValue&lt;User&gt;();
    /// 
    /// // Advanced API - get raw response
    /// var response = await usersTable.Delete("user-123")
    ///     .WithReturnValues(ReturnValue.ALL_OLD)
    ///     .ToDynamoDbResponseAsync();
    /// var deletedUser = response.ToPreOperationEntity&lt;User&gt;();
    /// </code>
    /// </example>
    // Note: In actual implementation, replace 'PlaceholderEntity' with your entity type
    public DeleteItemRequestBuilder<PlaceholderEntity> Delete(string userId) => 
        base.Delete<PlaceholderEntity>().WithKey("id", userId);

    /// <summary>
    /// Global Secondary Index for querying users by email.
    /// Index structure:
    /// - Partition Key: email
    /// - Projection: id, name, email, status
    /// </summary>
    /// <example>
    /// <code>
    /// // Query by email (Primary API)
    /// var users = await usersTable.EmailIndex
    ///     .Query("email = {0}", "user@example.com")
    ///     .ToListAsync&lt;User&gt;();
    /// 
    /// // Manual query configuration
    /// var users = await usersTable.EmailIndex.Query()
    ///     .Where("email = {0}", "user@example.com")
    ///     .WithLimit(10)
    ///     .ToListAsync&lt;User&gt;();
    /// 
    /// // Access pagination metadata
    /// var context = DynamoDbOperationContext.Current;
    /// var hasMorePages = context?.LastEvaluatedKey != null;
    /// 
    /// // Advanced API - get raw response
    /// var response = await usersTable.EmailIndex
    ///     .Query("email = {0}", "user@example.com")
    ///     .ToDynamoDbResponseAsync();
    /// var users = response.ToList&lt;User&gt;();
    /// </code>
    /// </example>
    public DynamoDbIndex EmailIndex => 
        new DynamoDbIndex(this, "EmailIndex", "id, name, email, status");

    /// <summary>
    /// Global Secondary Index for querying users by status and creation date.
    /// Index structure:
    /// - Partition Key: status
    /// - Sort Key: created_at
    /// - Projection: id, name, email, status, created_at
    /// </summary>
    /// <example>
    /// <code>
    /// // Query active users (Primary API)
    /// var activeUsers = await usersTable.StatusIndex
    ///     .Query("status = {0}", "ACTIVE")
    ///     .ToListAsync&lt;User&gt;();
    /// 
    /// // Query active users created after a specific date
    /// var recentActiveUsers = await usersTable.StatusIndex
    ///     .Query("status = {0} AND created_at > {1}", "ACTIVE", "2024-01-01")
    ///     .ToListAsync&lt;User&gt;();
    /// 
    /// // Query with begins_with on sort key
    /// var usersInJanuary = await usersTable.StatusIndex
    ///     .Query("status = {0} AND begins_with(created_at, {1})", "ACTIVE", "2024-01")
    ///     .ToListAsync&lt;User&gt;();
    /// 
    /// // Query with BETWEEN on sort key
    /// var usersInRange = await usersTable.StatusIndex
    ///     .Query("status = {0} AND created_at BETWEEN {1} AND {2}", 
    ///         "ACTIVE", "2024-01-01", "2024-12-31")
    ///     .ToListAsync&lt;User&gt;();
    /// 
    /// // Access query metadata
    /// var context = DynamoDbOperationContext.Current;
    /// Console.WriteLine($"Items returned: {context?.ItemCount}");
    /// Console.WriteLine($"Items scanned: {context?.ScannedCount}");
    /// 
    /// // Advanced API - get raw response
    /// var response = await usersTable.StatusIndex
    ///     .Query("status = {0}", "ACTIVE")
    ///     .ToDynamoDbResponseAsync();
    /// var users = response.ToList&lt;User&gt;();
    /// </code>
    /// </example>
    public DynamoDbIndex StatusIndex => 
        new DynamoDbIndex(this, "StatusIndex", "id, name, email, status, created_at");
}
