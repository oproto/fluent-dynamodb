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
/// // Get a user by ID
/// var user = await usersTable.Get("user-123")
///     .WithProjection("id, name, email")
///     .ToDynamoDbResponseAsync();
/// 
/// // Update a user
/// await usersTable.Update("user-123")
///     .Set("last_login", DateTime.UtcNow)
///     .ToDynamoDbResponseAsync();
/// 
/// // Delete a user
/// await usersTable.Delete("user-123")
///     .WithCondition("attribute_exists(id)")
///     .ToDynamoDbResponseAsync();
/// 
/// // Query by email using GSI
/// var usersByEmail = await usersTable.EmailIndex
///     .Query("email = {0}", "user@example.com")
///     .ToDynamoDbResponseAsync();
/// 
/// // Query active users created after a date
/// var activeUsers = await usersTable.StatusIndex
///     .Query("status = {0} AND created_at > {1}", "ACTIVE", "2024-01-01")
///     .ToDynamoDbResponseAsync();
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
    /// // Get user with all attributes
    /// var user = await usersTable.Get("user-123").ToDynamoDbResponseAsync();
    /// 
    /// // Get user with specific attributes
    /// var user = await usersTable.Get("user-123")
    ///     .WithProjection("id, name, email")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Get user with consistent read
    /// var user = await usersTable.Get("user-123")
    ///     .WithConsistentRead()
    ///     .ToDynamoDbResponseAsync();
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
    /// // Update user attributes
    /// await usersTable.Update("user-123")
    ///     .Set("name", "John Doe")
    ///     .Set("email", "john@example.com")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Update with condition
    /// await usersTable.Update("user-123")
    ///     .Set("status", "INACTIVE")
    ///     .WithCondition("attribute_exists(id)")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Increment a counter
    /// await usersTable.Update("user-123")
    ///     .Add("login_count", 1)
    ///     .Set("last_login", DateTime.UtcNow)
    ///     .ToDynamoDbResponseAsync();
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
    /// // Delete user
    /// await usersTable.Delete("user-123").ToDynamoDbResponseAsync();
    /// 
    /// // Delete with condition
    /// await usersTable.Delete("user-123")
    ///     .WithCondition("attribute_exists(id)")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Delete and return old values
    /// var deletedUser = await usersTable.Delete("user-123")
    ///     .WithReturnValues(ReturnValue.ALL_OLD)
    ///     .ToDynamoDbResponseAsync();
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
    /// // Query by email
    /// var users = await usersTable.EmailIndex
    ///     .Query("email = {0}", "user@example.com")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Manual query configuration
    /// var users = await usersTable.EmailIndex.Query()
    ///     .Where("email = {0}", "user@example.com")
    ///     .WithLimit(10)
    ///     .ToDynamoDbResponseAsync();
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
    /// // Query active users
    /// var activeUsers = await usersTable.StatusIndex
    ///     .Query("status = {0}", "ACTIVE")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Query active users created after a specific date
    /// var recentActiveUsers = await usersTable.StatusIndex
    ///     .Query("status = {0} AND created_at > {1}", "ACTIVE", "2024-01-01")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Query with begins_with on sort key
    /// var usersInJanuary = await usersTable.StatusIndex
    ///     .Query("status = {0} AND begins_with(created_at, {1})", "ACTIVE", "2024-01")
    ///     .ToDynamoDbResponseAsync();
    /// 
    /// // Query with BETWEEN on sort key
    /// var usersInRange = await usersTable.StatusIndex
    ///     .Query("status = {0} AND created_at BETWEEN {1} AND {2}", 
    ///         "ACTIVE", "2024-01-01", "2024-12-31")
    ///     .ToDynamoDbResponseAsync();
    /// </code>
    /// </example>
    public DynamoDbIndex StatusIndex => 
        new DynamoDbIndex(this, "StatusIndex", "id, name, email, status, created_at");
}
