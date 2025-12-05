// TransactionDemo example application - demonstrates transaction API comparison
// This example compares FluentDynamoDb's fluent transaction API with raw AWS SDK usage

using Examples.Shared;
using Oproto.FluentDynamoDb.Requests.Extensions;
using TransactionDemo;
using TransactionDemo.Entities;

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║        TransactionDemo - FluentDynamoDb Example            ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: Transactions, Code Comparison, Atomicity    ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure table exists (idempotent)
ConsoleHelpers.ShowInfo("Ensuring table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    client,
    TransactionDemoTable.TableName,
    "pk",
    "sk");

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{TransactionDemoTable.TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{TransactionDemoTable.TableName}' already exists");
}

// Create table and comparison instances
var table = new TransactionDemoTable(client);
var comparison = new TransactionComparison(client, table);

// Store results for comparison
TransactionComparison.TransactionResult? fluentResult = null;
TransactionComparison.TransactionResult? rawSdkResult = null;

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        "Transaction Demo Menu",
        "Run FluentDynamoDb Transaction",
        "Run Raw SDK Transaction",
        "Compare Results",
        "Demonstrate Failure Rollback",
        "View Current Items",
        "Clear All Items",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                fluentResult = await RunFluentTransactionAsync(table, comparison);
                break;
            case 2:
                rawSdkResult = await RunRawSdkTransactionAsync(table, comparison);
                break;
            case 3:
                CompareResults(fluentResult, rawSdkResult);
                break;
            case 4:
                await DemonstrateRollbackAsync(table, comparison);
                break;
            case 5:
                await ViewCurrentItemsAsync(table);
                break;
            case 6:
                await ClearAllItemsAsync(table);
                break;
            case 7:
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
/// Runs the FluentDynamoDb transaction and displays results.
/// </summary>
static async Task<TransactionComparison.TransactionResult> RunFluentTransactionAsync(
    TransactionDemoTable table,
    TransactionComparison comparison)
{
    ConsoleHelpers.ShowSection("FluentDynamoDb Transaction");
    
    // Clear existing items first
    ConsoleHelpers.ShowInfo("Clearing existing items...");
    await DeleteAllItemsAsync(table);
    
    ConsoleHelpers.ShowInfo("Executing transaction with 25 put operations...");
    Console.WriteLine();
    
    var result = await comparison.ExecuteFluentTransactionAsync();
    
    if (result.Success)
    {
        ConsoleHelpers.ShowSuccess("Transaction completed successfully!");
        Console.WriteLine();
        Console.WriteLine($"  Execution Time: {result.ExecutionTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"  Approximate Lines of Code: ~{result.LineCount}");
        
        // Verify items were written using generated entity accessor
        var itemCount = await CountAllItemsAsync(table);
        Console.WriteLine($"  Items Written: {itemCount}");
        
        if (itemCount == 25)
        {
            ConsoleHelpers.ShowSuccess("All 25 items verified in table");
        }
        else
        {
            ConsoleHelpers.ShowError($"Expected 25 items, found {itemCount}");
        }
    }
    else
    {
        ConsoleHelpers.ShowError($"Transaction failed: {result.ErrorMessage}");
    }
    
    ConsoleHelpers.WaitForKey();
    return result;
}

/// <summary>
/// Runs the raw SDK transaction and displays results.
/// </summary>
static async Task<TransactionComparison.TransactionResult> RunRawSdkTransactionAsync(
    TransactionDemoTable table,
    TransactionComparison comparison)
{
    ConsoleHelpers.ShowSection("Raw AWS SDK Transaction");
    
    // Clear existing items first
    ConsoleHelpers.ShowInfo("Clearing existing items...");
    await DeleteAllItemsAsync(table);
    
    ConsoleHelpers.ShowInfo("Executing transaction with 25 put operations...");
    Console.WriteLine();
    
    var result = await comparison.ExecuteRawSdkTransactionAsync();
    
    if (result.Success)
    {
        ConsoleHelpers.ShowSuccess("Transaction completed successfully!");
        Console.WriteLine();
        Console.WriteLine($"  Execution Time: {result.ExecutionTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"  Approximate Lines of Code: ~{result.LineCount}");
        
        // Verify items were written using generated entity accessor
        var itemCount = await CountAllItemsAsync(table);
        Console.WriteLine($"  Items Written: {itemCount}");
        
        if (itemCount == 25)
        {
            ConsoleHelpers.ShowSuccess("All 25 items verified in table");
        }
        else
        {
            ConsoleHelpers.ShowError($"Expected 25 items, found {itemCount}");
        }
    }
    else
    {
        ConsoleHelpers.ShowError($"Transaction failed: {result.ErrorMessage}");
    }
    
    ConsoleHelpers.WaitForKey();
    return result;
}

/// <summary>
/// Compares the results of both transaction approaches.
/// </summary>
static void CompareResults(
    TransactionComparison.TransactionResult? fluentResult,
    TransactionComparison.TransactionResult? rawSdkResult)
{
    ConsoleHelpers.ShowSection("Transaction Comparison");
    
    if (fluentResult == null && rawSdkResult == null)
    {
        ConsoleHelpers.ShowInfo("Run both transactions first to compare results.");
        ConsoleHelpers.WaitForKey();
        return;
    }
    
    Console.WriteLine();
    Console.WriteLine("  ┌─────────────────────────┬──────────────────┬──────────────────┐");
    Console.WriteLine("  │ Metric                  │ FluentDynamoDb   │ Raw AWS SDK      │");
    Console.WriteLine("  ├─────────────────────────┼──────────────────┼──────────────────┤");
    
    // Lines of Code
    var fluentLines = fluentResult?.LineCount.ToString() ?? "N/A";
    var rawLines = rawSdkResult?.LineCount.ToString() ?? "N/A";
    Console.WriteLine($"  │ Lines of Code           │ ~{fluentLines,-15} │ ~{rawLines,-15} │");
    
    // Execution Time
    var fluentTime = fluentResult != null ? $"{fluentResult.ExecutionTime.TotalMilliseconds:F2} ms" : "N/A";
    var rawTime = rawSdkResult != null ? $"{rawSdkResult.ExecutionTime.TotalMilliseconds:F2} ms" : "N/A";
    Console.WriteLine($"  │ Execution Time          │ {fluentTime,-16} │ {rawTime,-16} │");
    
    // Success
    var fluentSuccess = fluentResult?.Success.ToString() ?? "N/A";
    var rawSuccess = rawSdkResult?.Success.ToString() ?? "N/A";
    Console.WriteLine($"  │ Success                 │ {fluentSuccess,-16} │ {rawSuccess,-16} │");
    
    Console.WriteLine("  └─────────────────────────┴──────────────────┴──────────────────┘");
    Console.WriteLine();
    
    // Calculate code reduction
    if (fluentResult != null && rawSdkResult != null)
    {
        var reduction = ((double)(rawSdkResult.LineCount - fluentResult.LineCount) / rawSdkResult.LineCount) * 100;
        ConsoleHelpers.ShowSuccess($"FluentDynamoDb reduces code by approximately {reduction:F0}%!");
        
        Console.WriteLine();
        Console.WriteLine("  Key Benefits of FluentDynamoDb:");
        Console.WriteLine("  • Type-safe entity handling (no Dictionary<string, AttributeValue>)");
        Console.WriteLine("  • Automatic attribute name/value mapping");
        Console.WriteLine("  • Fluent, readable API");
        Console.WriteLine("  • Compile-time error checking");
        Console.WriteLine("  • IntelliSense support for entity properties");
    }
    
    ConsoleHelpers.WaitForKey();
}

/// <summary>
/// Demonstrates transaction rollback on failure.
/// </summary>
static async Task DemonstrateRollbackAsync(
    TransactionDemoTable table,
    TransactionComparison comparison)
{
    ConsoleHelpers.ShowSection("Transaction Rollback Demonstration");
    
    Console.WriteLine();
    Console.WriteLine("  This demonstration shows that when a transaction fails,");
    Console.WriteLine("  NO items are written - the entire transaction is rolled back.");
    Console.WriteLine();
    
    ConsoleHelpers.ShowInfo("Attempting a transaction that will fail...");
    
    var (rollbackDemonstrated, itemsBefore, itemsAfter) = await comparison.DemonstrateRollbackAsync();
    
    Console.WriteLine();
    Console.WriteLine($"  Items before failed transaction: {itemsBefore}");
    Console.WriteLine($"  Items after failed transaction:  {itemsAfter}");
    Console.WriteLine();
    
    if (rollbackDemonstrated)
    {
        ConsoleHelpers.ShowSuccess("Rollback verified! No partial writes occurred.");
        Console.WriteLine();
        Console.WriteLine("  This demonstrates DynamoDB's ACID transaction guarantees:");
        Console.WriteLine("  • Atomicity: All operations succeed or all fail");
        Console.WriteLine("  • Consistency: Database remains in valid state");
        Console.WriteLine("  • Isolation: Transaction is isolated from others");
        Console.WriteLine("  • Durability: Committed changes are permanent");
    }
    else
    {
        ConsoleHelpers.ShowError("Unexpected: Item count changed during failed transaction");
    }
    
    ConsoleHelpers.WaitForKey();
}

/// <summary>
/// Views all current items in the table.
/// </summary>
static async Task ViewCurrentItemsAsync(TransactionDemoTable table)
{
    ConsoleHelpers.ShowSection("Current Items in Table");
    
    // PREFERRED: Using the generated entity accessor Scan method
    var allAccounts = await table.Accounts.Scan().ToListAsync();
    
    // Filter to only account profiles (not transaction records)
    var accounts = allAccounts.Where(x => x.Sk == Account.ProfileSk).ToList();
    
    if (accounts.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No items in table. Run a transaction first.");
        ConsoleHelpers.WaitForKey();
        return;
    }
    
    Console.WriteLine();
    Console.WriteLine("  Accounts:");
    ConsoleHelpers.DisplayTable(
        accounts,
        ("Account ID", a => a.AccountId),
        ("Name", a => a.Name),
        ("Balance", a => a.Balance.ToString("C")));
    
    // Show transaction count for first few accounts
    Console.WriteLine("  Transaction Records (sample):");
    var sampleAccount = accounts.FirstOrDefault();
    if (sampleAccount != null)
    {
        var pk = Account.Keys.Pk(sampleAccount.AccountId);
        
        // PREFERRED: Using the generated entity accessor Query method with lambda expression
        var transactions = await table.Transactions.Query(
            x => x.Pk == pk && x.Sk.StartsWith(TransactionRecord.TxnSkPrefix))
            .ToListAsync();
        
        var sortedTransactions = transactions.OrderByDescending(t => t.Timestamp).ToList();
        
        if (sortedTransactions.Count > 0)
        {
            ConsoleHelpers.DisplayTable(
                sortedTransactions.Take(5),
                ("Account", t => t.AccountId),
                ("Type", t => t.Type),
                ("Amount", t => t.Amount.ToString("C")),
                ("Timestamp", t => t.Timestamp.ToString("HH:mm:ss.fff")));
            
            if (sortedTransactions.Count > 5)
            {
                Console.WriteLine($"  ... and {sortedTransactions.Count - 5} more transactions");
            }
        }
    }
    
    var totalItems = await CountAllItemsAsync(table);
    ConsoleHelpers.ShowInfo($"Total items in table: {totalItems}");
    
    ConsoleHelpers.WaitForKey();
}

/// <summary>
/// Clears all items from the table.
/// </summary>
static async Task ClearAllItemsAsync(TransactionDemoTable table)
{
    ConsoleHelpers.ShowSection("Clear All Items");
    
    var itemCount = await CountAllItemsAsync(table);
    
    if (itemCount == 0)
    {
        ConsoleHelpers.ShowInfo("Table is already empty.");
        ConsoleHelpers.WaitForKey();
        return;
    }
    
    Console.Write($"Are you sure you want to delete all {itemCount} items? (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "y" && confirm != "yes")
    {
        ConsoleHelpers.ShowInfo("Clear cancelled");
        ConsoleHelpers.WaitForKey();
        return;
    }
    
    ConsoleHelpers.ShowInfo("Deleting all items...");
    await DeleteAllItemsAsync(table);
    ConsoleHelpers.ShowSuccess("All items deleted");
    
    ConsoleHelpers.WaitForKey();
}

/// <summary>
/// Counts all items in the table (accounts and transactions).
/// </summary>
static async Task<int> CountAllItemsAsync(TransactionDemoTable table)
{
    // Use raw response to count ALL items regardless of entity type
    var response = await table.Accounts.Scan().ToDynamoDbResponseAsync();
    return response.Items?.Count ?? 0;
}

/// <summary>
/// Deletes all items in the table.
/// </summary>
static async Task DeleteAllItemsAsync(TransactionDemoTable table)
{
    // Get all items
    var response = await table.Accounts.Scan().ToDynamoDbResponseAsync();
    
    if (response.Items == null || response.Items.Count == 0)
        return;

    // Delete each item using the generated entity accessor
    foreach (var item in response.Items)
    {
        var pk = item["pk"].S;
        var sk = item["sk"].S;
        
        await table.Accounts.Delete(pk, sk).DeleteAsync();
    }
}
