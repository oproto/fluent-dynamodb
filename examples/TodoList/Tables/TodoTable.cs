using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using TodoList.Entities;

namespace TodoList.Tables;

/// <summary>
/// Table class for managing todo items in DynamoDB.
/// 
/// This class demonstrates the scannable table pattern, which is appropriate for
/// small datasets like personal todo lists. The <see cref="TodoItem"/> entity is
/// marked with [Scannable], enabling full table scans for listing all items.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Access Patterns:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Add a new todo item (Put operation)</description></item>
/// <item><description>Get all todo items (Scan operation - appropriate for small datasets)</description></item>
/// <item><description>Mark a todo item as complete (Update operation)</description></item>
/// <item><description>Edit a todo item description (Update operation)</description></item>
/// <item><description>Delete a todo item (Delete operation)</description></item>
/// </list>
/// <para>
/// <strong>Why Scannable?</strong>
/// </para>
/// <para>
/// Todo lists are typically small (tens to hundreds of items), making scan operations
/// acceptable. For larger datasets, consider using Query operations with a partition key
/// design (e.g., pk="USER#{userId}") to efficiently retrieve items for a specific user.
/// </para>
/// </remarks>
public class TodoTable : DynamoDbTableBase
{
    /// <summary>
    /// The name of the DynamoDB table for todo items.
    /// </summary>
    public const string TableName = "todo-items";

    /// <summary>
    /// Initializes a new instance of the TodoTable class.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    public TodoTable(IAmazonDynamoDB client) : base(client, TableName)
    {
    }

    /// <summary>
    /// Adds a new todo item with the specified description.
    /// Creates the item with a unique ID, false completion status, and current timestamp.
    /// </summary>
    /// <param name="description">The description of the todo item.</param>
    /// <returns>The created todo item.</returns>
    /// <remarks>
    /// This method demonstrates a simple Put operation using the fluent API.
    /// The item is created with:
    /// - A unique GUID-based ID
    /// - The provided description
    /// - IsComplete = false
    /// - CreatedAt = current UTC time
    /// - CompletedAt = null
    /// </remarks>
    public async Task<TodoItem> AddAsync(string description)
    {
        var item = new TodoItem
        {
            Id = Guid.NewGuid().ToString(),
            Description = description,
            IsComplete = false,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null
        };

        // PREFERRED: Using the express-route PutAsync method
        await PutAsync(item);

        return item;
    }

    /// <summary>
    /// Retrieves all todo items from the table.
    /// </summary>
    /// <returns>A list of all todo items.</returns>
    /// <remarks>
    /// This method uses a Scan operation, which reads every item in the table.
    /// This is appropriate for small datasets like personal todo lists.
    /// 
    /// For larger datasets, consider:
    /// - Using Query with a partition key (e.g., USER#{userId})
    /// - Implementing pagination for large result sets
    /// - Using a GSI for specific access patterns
    /// </remarks>
    public async Task<List<TodoItem>> GetAllAsync()
    {
        // PREFERRED: Using ToListAsync() extension method for strongly-typed results
        var items = await Scan<TodoItem>().ToListAsync();
        
        // ALTERNATIVE: Format string approach for filtered scans
        // var items = await Scan<TodoItem>()
        //     .WithFilter("isComplete = {0}", false)
        //     .ToListAsync();

        return items;
    }

    /// <summary>
    /// Marks a todo item as complete by setting IsComplete to true and recording the completion timestamp.
    /// </summary>
    /// <param name="id">The ID of the todo item to mark as complete.</param>
    /// <returns>True if the item was updated, false if not found.</returns>
    /// <remarks>
    /// This method demonstrates an Update operation that modifies specific attributes
    /// while preserving others. The update uses SET expressions to:
    /// - Set isComplete to true
    /// - Set completedAt to the current timestamp
    /// </remarks>
    public async Task<bool> MarkCompleteAsync(string id)
    {
        try
        {
            // PREFERRED: Format string approach - concise and readable
            // The {0:o} format specifier formats DateTime as ISO 8601
            await Update<TodoItem>()
                .WithKey("pk", id)
                .Set("SET isComplete = {0}, completedAt = {1:o}", true, DateTime.UtcNow)
                .UpdateAsync();

            // ALTERNATIVE: Manual attribute approach for more control
            // await Update<TodoItem>()
            //     .WithKey("pk", id)
            //     .Set("SET #isComplete = :isComplete, #completedAt = :completedAt")
            //     .WithAttribute("#isComplete", "isComplete")
            //     .WithAttribute("#completedAt", "completedAt")
            //     .WithValue(":isComplete", true)
            //     .WithValue(":completedAt", DateTime.UtcNow.ToString("o"))
            //     .UpdateAsync();

            return true;
        }
        catch (Amazon.DynamoDBv2.Model.ConditionalCheckFailedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Edits the description of a todo item while preserving all other fields.
    /// </summary>
    /// <param name="id">The ID of the todo item to edit.</param>
    /// <param name="newDescription">The new description.</param>
    /// <returns>True if the item was updated, false if not found.</returns>
    /// <remarks>
    /// This method demonstrates a targeted Update operation that modifies only
    /// the description attribute, leaving Id, IsComplete, CreatedAt, and CompletedAt unchanged.
    /// </remarks>
    public async Task<bool> EditDescriptionAsync(string id, string newDescription)
    {
        try
        {
            // PREFERRED: Format string approach - concise and readable
            await Update<TodoItem>()
                .WithKey("pk", id)
                .Set("SET description = {0}", newDescription)
                .UpdateAsync();

            // ALTERNATIVE: Manual attribute approach for more control
            // await Update<TodoItem>()
            //     .WithKey("pk", id)
            //     .Set("SET #description = :description")
            //     .WithAttribute("#description", "description")
            //     .WithValue(":description", newDescription)
            //     .UpdateAsync();

            return true;
        }
        catch (Amazon.DynamoDBv2.Model.ConditionalCheckFailedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes a todo item from the table.
    /// </summary>
    /// <param name="id">The ID of the todo item to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method demonstrates a Delete operation. The delete is idempotent -
    /// deleting a non-existent item does not throw an error.
    /// </remarks>
    public async Task DeleteAsync(string id)
    {
        await Delete<TodoItem>()
            .WithKey("pk", id)
            .DeleteAsync();
    }

    /// <summary>
    /// Gets a single todo item by ID.
    /// </summary>
    /// <param name="id">The ID of the todo item.</param>
    /// <returns>The todo item, or null if not found.</returns>
    public async Task<TodoItem?> GetByIdAsync(string id)
    {
        return await Get<TodoItem>()
            .WithKey("pk", id)
            .GetItemAsync();
    }
}
