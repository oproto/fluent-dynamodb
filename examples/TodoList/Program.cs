// TodoList example application - demonstrates basic CRUD operations with FluentDynamoDb
// This example shows how to use the Scannable table pattern for small datasets

using Examples.Shared;
using Oproto.FluentDynamoDb.Requests.Extensions;
using TodoList.Entities;

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║           TodoList - FluentDynamoDb Example                ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: CRUD operations, Scannable tables           ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure table exists (idempotent)
ConsoleHelpers.ShowInfo("Ensuring table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    client,
    TodoItemsTable.TableName,
    "pk");

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{TodoItemsTable.TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{TodoItemsTable.TableName}' already exists");
}

// Create table instance
var table = new TodoItemsTable(client);

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        "Todo List Menu",
        "Add Todo",
        "List All Todos",
        "Mark Complete",
        "Edit Description",
        "Delete Todo",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                await AddTodoAsync(table);
                break;
            case 2:
                await ListTodosAsync(table);
                break;
            case 3:
                await MarkCompleteAsync(table);
                break;
            case 4:
                await EditDescriptionAsync(table);
                break;
            case 5:
                await DeleteTodoAsync(table);
                break;
            case 6:
                ConsoleHelpers.ShowInfo("Goodbye!");
                return;
            case 0:
                // Invalid selection - menu already showed error
                break;
        }
    }
    catch (Exception ex)
    {
        ConsoleHelpers.ShowError(ex, "Operation failed");
    }
}

/// <summary>
/// Adds a new todo item using the generated entity accessor.
/// </summary>
static async Task AddTodoAsync(TodoItemsTable table)
{
    ConsoleHelpers.ShowSection("Add New Todo");
    
    var description = ConsoleHelpers.GetInput("Enter description");
    if (string.IsNullOrWhiteSpace(description))
        return;

    var item = new TodoItem
    {
        Id = Guid.NewGuid().ToString(),
        Description = description,
        IsComplete = false,
        CreatedAt = DateTime.UtcNow,
        CompletedAt = null
    };

    // PREFERRED: Using the generated entity accessor PutAsync method
    await table.TodoItems.PutAsync(item);
    
    ConsoleHelpers.ShowSuccess($"Created todo with ID: {item.Id}");
}

/// <summary>
/// Lists all todo items using the generated Scan accessor.
/// Shows incomplete items first, then completed, sorted by creation date.
/// </summary>
static async Task ListTodosAsync(TodoItemsTable table)
{
    ConsoleHelpers.ShowSection("All Todos");
    
    // PREFERRED: Using the generated entity accessor Scan method
    var items = await table.TodoItems.Scan().ToListAsync();
    
    if (items.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No todos found. Add some!");
        return;
    }

    // Sort: incomplete items first, then completed, each group sorted by creation date
    // Note: DynamoDB doesn't support ORDER BY, so sorting must be done client-side
    var sortedItems = items
        .OrderBy(x => x.IsComplete)           // false (incomplete) comes before true (complete)
        .ThenBy(x => x.CreatedAt)             // Within each group, sort by creation date
        .ToList();

    ConsoleHelpers.DisplayTable(
        sortedItems,
        ("ID (first 8 chars)", item => item.Id[..Math.Min(8, item.Id.Length)]),
        ("Description", item => TruncateString(item.Description, 30)),
        ("Status", item => item.IsComplete ? "✓ Complete" : "○ Pending"),
        ("Created", item => item.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
        ("Completed", item => item.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-"));

    var pendingCount = items.Count(x => !x.IsComplete);
    var completeCount = items.Count(x => x.IsComplete);
    ConsoleHelpers.ShowInfo($"Total: {items.Count} items ({pendingCount} pending, {completeCount} complete)");
}

/// <summary>
/// Marks a todo item as complete using the generated entity accessor Update method.
/// </summary>
static async Task MarkCompleteAsync(TodoItemsTable table)
{
    ConsoleHelpers.ShowSection("Mark Todo Complete");
    
    // Show incomplete items for reference using generated Scan accessor
    var items = await table.TodoItems.Scan().ToListAsync();
    var incompleteItems = items.Where(x => !x.IsComplete).ToList();
    
    if (incompleteItems.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No incomplete todos to mark complete.");
        return;
    }

    Console.WriteLine("Incomplete todos:");
    ConsoleHelpers.DisplayTable(
        incompleteItems,
        ("ID (first 8 chars)", item => item.Id[..Math.Min(8, item.Id.Length)]),
        ("Description", item => TruncateString(item.Description, 40)));

    var id = ConsoleHelpers.GetInput("Enter todo ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    // Find matching item (support partial ID matching)
    var matchingItem = items.FirstOrDefault(x => x.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (matchingItem == null)
    {
        ConsoleHelpers.ShowError($"No todo found matching '{id}'");
        return;
    }

    if (matchingItem.IsComplete)
    {
        ConsoleHelpers.ShowInfo("This todo is already complete.");
        return;
    }

    try
    {
        // PREFERRED: Using the generated entity accessor Update method with lambda expression
        await table.TodoItems.Update(matchingItem.Id)
            .Set(x => new TodoItemUpdateModel { 
                IsComplete = true,
                CompletedAt = DateTime.UtcNow
            })
            .UpdateAsync();

        ConsoleHelpers.ShowSuccess($"Marked '{TruncateString(matchingItem.Description, 30)}' as complete");
    }
    catch (Amazon.DynamoDBv2.Model.ConditionalCheckFailedException)
    {
        ConsoleHelpers.ShowError("Failed to mark todo as complete");
    }
}

/// <summary>
/// Edits the description of a todo item using the generated entity accessor Update method.
/// </summary>
static async Task EditDescriptionAsync(TodoItemsTable table)
{
    ConsoleHelpers.ShowSection("Edit Todo Description");
    
    // Show all items for reference using generated Scan accessor
    var items = await table.TodoItems.Scan().ToListAsync();
    
    if (items.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No todos to edit.");
        return;
    }

    Console.WriteLine("Current todos:");
    ConsoleHelpers.DisplayTable(
        items,
        ("ID (first 8 chars)", item => item.Id[..Math.Min(8, item.Id.Length)]),
        ("Description", item => TruncateString(item.Description, 40)));

    var id = ConsoleHelpers.GetInput("Enter todo ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    // Find matching item (support partial ID matching)
    var matchingItem = items.FirstOrDefault(x => x.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (matchingItem == null)
    {
        ConsoleHelpers.ShowError($"No todo found matching '{id}'");
        return;
    }

    Console.WriteLine($"Current description: {matchingItem.Description}");
    var newDescription = ConsoleHelpers.GetInput("Enter new description");
    if (string.IsNullOrWhiteSpace(newDescription))
        return;

    try
    {
        await table.TodoItems.Update(matchingItem.Id)
            .Set(x => new TodoItemUpdateModel { 
                Description = newDescription
            })
            .UpdateAsync();

        ConsoleHelpers.ShowSuccess("Description updated successfully");
    }
    catch (Amazon.DynamoDBv2.Model.ConditionalCheckFailedException)
    {
        ConsoleHelpers.ShowError("Failed to update description");
    }
}

/// <summary>
/// Deletes a todo item using the generated entity accessor DeleteAsync method.
/// </summary>
static async Task DeleteTodoAsync(TodoItemsTable table)
{
    ConsoleHelpers.ShowSection("Delete Todo");
    
    // Show all items for reference using generated Scan accessor
    var items = await table.TodoItems.Scan().ToListAsync();
    
    if (items.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No todos to delete.");
        return;
    }

    Console.WriteLine("Current todos:");
    ConsoleHelpers.DisplayTable(
        items,
        ("ID (first 8 chars)", item => item.Id[..Math.Min(8, item.Id.Length)]),
        ("Description", item => TruncateString(item.Description, 40)),
        ("Status", item => item.IsComplete ? "✓ Complete" : "○ Pending"));

    var id = ConsoleHelpers.GetInput("Enter todo ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    // Find matching item (support partial ID matching)
    var matchingItem = items.FirstOrDefault(x => x.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (matchingItem == null)
    {
        ConsoleHelpers.ShowError($"No todo found matching '{id}'");
        return;
    }

    Console.Write($"Are you sure you want to delete '{TruncateString(matchingItem.Description, 30)}'? (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "y" && confirm != "yes")
    {
        ConsoleHelpers.ShowInfo("Delete cancelled");
        return;
    }

    // PREFERRED: Using the generated entity accessor DeleteAsync method
    await table.TodoItems.DeleteAsync(matchingItem.Id);
    ConsoleHelpers.ShowSuccess("Todo deleted successfully");
}

/// <summary>
/// Truncates a string to the specified maximum length, adding ellipsis if truncated.
/// </summary>
static string TruncateString(string value, int maxLength)
{
    if (string.IsNullOrEmpty(value))
        return string.Empty;
    
    return value.Length <= maxLength 
        ? value 
        : value[..(maxLength - 3)] + "...";
}
