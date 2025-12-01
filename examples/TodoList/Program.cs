// TodoList example application - demonstrates basic CRUD operations with FluentDynamoDb
// This example shows how to use the Scannable table pattern for small datasets

using Examples.Shared;
using TodoList.Entities;
using TodoList.Tables;

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
    TodoTable.TableName,
    "pk");

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{TodoTable.TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{TodoTable.TableName}' already exists");
}

// Create table instance
var table = new TodoTable(client);

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
/// Adds a new todo item.
/// </summary>
static async Task AddTodoAsync(TodoTable table)
{
    ConsoleHelpers.ShowSection("Add New Todo");
    
    var description = ConsoleHelpers.GetInput("Enter description");
    if (string.IsNullOrWhiteSpace(description))
        return;

    var item = await table.AddAsync(description);
    ConsoleHelpers.ShowSuccess($"Created todo with ID: {item.Id}");
}

/// <summary>
/// Lists all todo items, showing incomplete items first, then completed, sorted by creation date.
/// </summary>
static async Task ListTodosAsync(TodoTable table)
{
    ConsoleHelpers.ShowSection("All Todos");
    
    var items = await table.GetAllAsync();
    
    if (items.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No todos found. Add some!");
        return;
    }

    // Sort: incomplete items first, then completed, each group sorted by creation date
    // PREFERRED: Lambda expression approach - type-safe with IntelliSense
    var sortedItems = items
        .OrderBy(x => x.IsComplete)           // false (incomplete) comes before true (complete)
        .ThenBy(x => x.CreatedAt)             // Within each group, sort by creation date
        .ToList();

    // ALTERNATIVE: Using format string approach for the query itself
    // Note: DynamoDB doesn't support ORDER BY, so sorting must be done client-side
    // For server-side sorting, you would need to design your keys appropriately
    // (e.g., using a GSI with sort key = createdAt)

    ConsoleHelpers.DisplayTable(
        sortedItems,
        ("ID (first 8 chars)", item => item.Id[..Math.Min(8, item.Id.Length)]),
        ("Description", item => TruncateString(item.Description, 30)),
        ("Status", item => item.IsComplete ? "✓ Complete" : "○ Pending"),
        ("Created", item => item.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
        ("Completed", item => item.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-"));

    ConsoleHelpers.ShowInfo($"Total: {items.Count} items ({items.Count(x => !x.IsComplete)} pending, {items.Count(x => x.IsComplete)} complete)");
}

/// <summary>
/// Marks a todo item as complete.
/// </summary>
static async Task MarkCompleteAsync(TodoTable table)
{
    ConsoleHelpers.ShowSection("Mark Todo Complete");
    
    // Show incomplete items for reference
    var items = await table.GetAllAsync();
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

    var success = await table.MarkCompleteAsync(matchingItem.Id);
    if (success)
    {
        ConsoleHelpers.ShowSuccess($"Marked '{TruncateString(matchingItem.Description, 30)}' as complete");
    }
    else
    {
        ConsoleHelpers.ShowError("Failed to mark todo as complete");
    }
}

/// <summary>
/// Edits the description of a todo item.
/// </summary>
static async Task EditDescriptionAsync(TodoTable table)
{
    ConsoleHelpers.ShowSection("Edit Todo Description");
    
    // Show all items for reference
    var items = await table.GetAllAsync();
    
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

    var success = await table.EditDescriptionAsync(matchingItem.Id, newDescription);
    if (success)
    {
        ConsoleHelpers.ShowSuccess("Description updated successfully");
    }
    else
    {
        ConsoleHelpers.ShowError("Failed to update description");
    }
}

/// <summary>
/// Deletes a todo item.
/// </summary>
static async Task DeleteTodoAsync(TodoTable table)
{
    ConsoleHelpers.ShowSection("Delete Todo");
    
    // Show all items for reference
    var items = await table.GetAllAsync();
    
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

    await table.DeleteAsync(matchingItem.Id);
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
