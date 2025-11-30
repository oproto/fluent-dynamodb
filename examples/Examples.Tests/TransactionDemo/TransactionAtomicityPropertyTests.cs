using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Examples.Shared;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using TransactionDemo.Entities;

namespace Examples.Tests.TransactionDemo;

/// <summary>
/// Property-based tests for transaction atomicity.
/// These tests require DynamoDB Local to be running on port 8000.
/// </summary>
public class TransactionAtomicityPropertyTests
{
    private const string TestTableName = "transaction-demo-test";

    /// <summary>
    /// **Feature: example-applications, Property 15: Transaction Atomicity Success**
    /// **Validates: Requirements 4.4**
    /// 
    /// For any successful transaction of N items, exactly N items should exist
    /// in the table after completion.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TransactionSuccess_WritesAllItems()
    {
        return Prop.ForAll(
            GenerateItemCount(),
            itemCount =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestTransactionTable(client);

                    // Clear existing items
                    ClearTable(table);

                    // Build and execute a transaction with N items
                    var transaction = DynamoDbTransactions.Write;
                    var expectedItems = new List<Account>();

                    for (int i = 1; i <= itemCount; i++)
                    {
                        var account = new Account
                        {
                            Pk = Account.CreatePk($"TEST-{Guid.NewGuid():N}"),
                            Sk = Account.ProfileSk,
                            AccountId = $"TEST-{i}",
                            Name = $"Test Account {i}",
                            Balance = 1000m * i
                        };
                        expectedItems.Add(account);
                        transaction = transaction.Add(table.Put<Account>().WithItem(account));
                    }

                    // Execute the transaction
                    transaction.ExecuteAsync().GetAwaiter().GetResult();

                    // Count items in table
                    var actualCount = table.CountAllItemsAsync().GetAwaiter().GetResult();

                    // Clean up
                    ClearTable(table);

                    var allItemsWritten = actualCount == itemCount;

                    return allItemsWritten.ToProperty()
                        .Label($"Expected: {itemCount}, Actual: {actualCount}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 16: Transaction Atomicity Failure**
    /// **Validates: Requirements 4.5**
    /// 
    /// For any transaction that fails (e.g., due to condition check failure),
    /// zero items from that transaction should exist in the table.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TransactionFailure_WritesNoItems()
    {
        return Prop.ForAll(
            GenerateItemCount(),
            itemCount =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestTransactionTable(client);

                    // Clear existing items
                    ClearTable(table);

                    // Count items before transaction
                    var countBefore = table.CountAllItemsAsync().GetAwaiter().GetResult();

                    // Build a transaction that will fail
                    var transaction = DynamoDbTransactions.Write;
                    var uniquePrefix = Guid.NewGuid().ToString("N")[..8];

                    // Add N valid put operations
                    for (int i = 1; i <= itemCount; i++)
                    {
                        var account = new Account
                        {
                            Pk = Account.CreatePk($"{uniquePrefix}-{i}"),
                            Sk = Account.ProfileSk,
                            AccountId = $"{uniquePrefix}-{i}",
                            Name = $"Test Account {i}",
                            Balance = 1000m * i
                        };
                        transaction = transaction.Add(table.Put<Account>().WithItem(account));
                    }

                    // Add a condition check that will fail (checking for an item that doesn't exist)
                    var nonExistentPk = Account.CreatePk("NON-EXISTENT-ACCOUNT-" + Guid.NewGuid());
                    transaction = transaction.Add(
                        table.ConditionCheck<Account>()
                            .WithKey("pk", nonExistentPk)
                            .WithKey("sk", Account.ProfileSk)
                            .Where("attribute_exists(pk)")); // This will fail because item doesn't exist

                    // Execute the transaction (should fail)
                    bool transactionFailed = false;
                    try
                    {
                        transaction.ExecuteAsync().GetAwaiter().GetResult();
                    }
                    catch (TransactionCanceledException)
                    {
                        transactionFailed = true;
                    }

                    // Count items after failed transaction
                    var countAfter = table.CountAllItemsAsync().GetAwaiter().GetResult();

                    // Clean up (should be nothing to clean, but just in case)
                    ClearTable(table);

                    var noItemsWritten = countAfter == countBefore;
                    var transactionDidFail = transactionFailed;

                    return (noItemsWritten && transactionDidFail).ToProperty()
                        .Label($"TransactionFailed: {transactionDidFail}, " +
                               $"CountBefore: {countBefore}, CountAfter: {countAfter}, " +
                               $"NoItemsWritten: {noItemsWritten}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    #region Helper Methods

    /// <summary>
    /// Test table that uses a separate table name to avoid conflicts with the main application.
    /// </summary>
    private class TestTransactionTable : DynamoDbTableBase
    {
        public TestTransactionTable(IAmazonDynamoDB client) : base(client, TestTableName)
        {
        }

        public async Task<int> CountAllItemsAsync()
        {
            var items = await Scan<Account>().ToListAsync();
            return items.Count;
        }

        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await Scan<Account>()
                .WithFilter(x => x.Sk == Account.ProfileSk)
                .ToListAsync();
        }

        public async Task DeleteAllItemsAsync()
        {
            var response = await Scan<Account>().ToDynamoDbResponseAsync();
            
            if (response.Items == null || response.Items.Count == 0)
                return;

            foreach (var item in response.Items)
            {
                var pk = item["pk"].S;
                var sk = item["sk"].S;
                
                await Delete<Account>()
                    .WithKey("pk", pk)
                    .WithKey("sk", sk)
                    .DeleteAsync();
            }
        }
    }

    private static void EnsureTestTableExists(IAmazonDynamoDB client)
    {
        DynamoDbSetup.EnsureTableExistsAsync(client, TestTableName, "pk", "sk").GetAwaiter().GetResult();
    }

    private static void ClearTable(TestTransactionTable table)
    {
        table.DeleteAllItemsAsync().GetAwaiter().GetResult();
    }

    private static bool IsDynamoDbConnectionError(AmazonDynamoDBException ex)
    {
        return ex.Message.Contains("Unable to connect") ||
               ex.Message.Contains("Connection refused") ||
               ex.Message.Contains("No connection could be made");
    }

    /// <summary>
    /// Generates a reasonable item count for transaction testing.
    /// DynamoDB transactions support up to 100 items, but we test with smaller counts
    /// for faster test execution.
    /// </summary>
    private static Arbitrary<int> GenerateItemCount()
    {
        return Arb.From(Gen.Choose(1, 10));
    }

    #endregion
}
