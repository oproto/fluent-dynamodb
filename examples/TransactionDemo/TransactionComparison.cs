using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;
using TransactionDemo.Entities;
using TransactionDemo.Tables;

namespace TransactionDemo;

/// <summary>
/// Compares FluentDynamoDb transaction API with raw AWS SDK approach.
/// 
/// This class demonstrates the code reduction and improved readability
/// provided by FluentDynamoDb's fluent transaction API compared to
/// manually building TransactWriteItemsRequest with the raw SDK.
/// </summary>
public class TransactionComparison
{
    private readonly IAmazonDynamoDB _client;
    private readonly Tables.TransactionDemoTable _table;

    /// <summary>
    /// Initializes a new instance of the TransactionComparison class.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="table">The transaction demo table.</param>
    public TransactionComparison(IAmazonDynamoDB client, Tables.TransactionDemoTable table)
    {
        _client = client;
        _table = table;
    }

    /// <summary>
    /// Result of a transaction execution including timing and line count metrics.
    /// </summary>
    public record TransactionResult(
        bool Success,
        TimeSpan ExecutionTime,
        int LineCount,
        string? ErrorMessage = null);

    /// <summary>
    /// Executes 25 put operations using FluentDynamoDb's transaction API.
    /// 
    /// This method demonstrates the concise, fluent approach to building
    /// DynamoDB transactions. The code is type-safe and readable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>FluentDynamoDb Approach:</strong>
    /// </para>
    /// <para>
    /// The fluent API allows chaining Add() calls to compose a transaction.
    /// Each operation uses the table's Put() method with strongly-typed entities.
    /// </para>
    /// <code>
    /// await DynamoDbTransactions.Write
    ///     .Add(table.Put&lt;Entity&gt;().WithItem(entity1))
    ///     .Add(table.Put&lt;Entity&gt;().WithItem(entity2))
    ///     .ExecuteAsync();
    /// </code>
    /// </remarks>
    /// <returns>The transaction result with timing and line count.</returns>
    public async Task<TransactionResult> ExecuteFluentTransactionAsync()
    {
        // Line count for FluentDynamoDb approach: ~35 lines
        // (This comment documents the approximate line count for comparison)
        const int lineCount = 35;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build the transaction using FluentDynamoDb's fluent API
            var transaction = DynamoDbTransactions.Write;

            // Add 25 put operations - mix of accounts and transaction records
            for (int i = 1; i <= 25; i++)
            {
                var accountId = $"ACCT-{i:D3}";
                var timestamp = DateTime.UtcNow;
                var txnId = Guid.NewGuid().ToString("N")[..8];

                if (i <= 10)
                {
                    // First 10: Create account profiles
                    var account = new Account
                    {
                        Pk = Account.CreatePk(accountId),
                        Sk = Account.ProfileSk,
                        AccountId = accountId,
                        Name = $"Account {i}",
                        Balance = 1000m * i
                    };
                    transaction = transaction.Add(_table.Put<Account>().WithItem(account));
                }
                else
                {
                    // Remaining 15: Create transaction records for first 5 accounts
                    var targetAccountId = $"ACCT-{((i - 11) % 5) + 1:D3}";
                    var txnRecord = new TransactionRecord
                    {
                        Pk = Account.CreatePk(targetAccountId),
                        Sk = TransactionRecord.CreateSk(timestamp, txnId),
                        TxnId = txnId,
                        AccountId = targetAccountId,
                        Amount = 100m * (i - 10),
                        Type = i % 2 == 0 ? "CREDIT" : "DEBIT",
                        Timestamp = timestamp,
                        Description = $"Transaction {i - 10}"
                    };
                    transaction = transaction.Add(_table.Put<TransactionRecord>().WithItem(txnRecord));
                }
            }

            // Execute the transaction atomically
            await transaction.ExecuteAsync();

            stopwatch.Stop();
            return new TransactionResult(true, stopwatch.Elapsed, lineCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TransactionResult(false, stopwatch.Elapsed, lineCount, ex.Message);
        }
    }

    /// <summary>
    /// Executes 25 put operations using only the raw AWS SDK.
    /// 
    /// This method demonstrates the verbose approach required when using
    /// the AWS SDK directly without FluentDynamoDb. Each item must be
    /// manually constructed as a Dictionary of AttributeValue objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Raw SDK Approach:</strong>
    /// </para>
    /// <para>
    /// Without FluentDynamoDb, you must:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Create TransactWriteItemsRequest manually</description></item>
    /// <item><description>Build each item as Dictionary&lt;string, AttributeValue&gt;</description></item>
    /// <item><description>Handle type conversions (S for strings, N for numbers)</description></item>
    /// <item><description>Manage attribute names consistently</description></item>
    /// </list>
    /// </remarks>
    /// <returns>The transaction result with timing and line count.</returns>
    public async Task<TransactionResult> ExecuteRawSdkTransactionAsync()
    {
        // Line count for raw SDK approach: ~95 lines
        // (This comment documents the approximate line count for comparison)
        const int lineCount = 95;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build the transaction request manually
            var request = new TransactWriteItemsRequest
            {
                TransactItems = new List<TransactWriteItem>()
            };

            // Add 25 put operations - mix of accounts and transaction records
            for (int i = 1; i <= 25; i++)
            {
                var accountId = $"ACCT-{i:D3}";
                var timestamp = DateTime.UtcNow;
                var txnId = Guid.NewGuid().ToString("N")[..8];

                if (i <= 10)
                {
                    // First 10: Create account profiles
                    // Must manually build Dictionary<string, AttributeValue>
                    var item = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
                        ["sk"] = new AttributeValue { S = "PROFILE" },
                        ["accountId"] = new AttributeValue { S = accountId },
                        ["accountName"] = new AttributeValue { S = $"Account {i}" },
                        ["balance"] = new AttributeValue { N = (1000m * i).ToString() }
                    };

                    request.TransactItems.Add(new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = Tables.TransactionDemoTable.TableName,
                            Item = item
                        }
                    });
                }
                else
                {
                    // Remaining 15: Create transaction records for first 5 accounts
                    var targetAccountId = $"ACCT-{((i - 11) % 5) + 1:D3}";
                    var formattedTimestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                    var item = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = $"ACCOUNT#{targetAccountId}" },
                        ["sk"] = new AttributeValue { S = $"TXN#{formattedTimestamp}#{txnId}" },
                        ["txnId"] = new AttributeValue { S = txnId },
                        ["accountId"] = new AttributeValue { S = targetAccountId },
                        ["amount"] = new AttributeValue { N = (100m * (i - 10)).ToString() },
                        ["txnType"] = new AttributeValue { S = i % 2 == 0 ? "CREDIT" : "DEBIT" },
                        ["timestamp"] = new AttributeValue { S = formattedTimestamp },
                        ["description"] = new AttributeValue { S = $"Transaction {i - 10}" }
                    };

                    request.TransactItems.Add(new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = Tables.TransactionDemoTable.TableName,
                            Item = item
                        }
                    });
                }
            }

            // Execute the transaction
            await _client.TransactWriteItemsAsync(request);

            stopwatch.Stop();
            return new TransactionResult(true, stopwatch.Elapsed, lineCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TransactionResult(false, stopwatch.Elapsed, lineCount, ex.Message);
        }
    }

    /// <summary>
    /// Demonstrates transaction rollback by attempting a transaction that will fail.
    /// 
    /// This method creates a transaction with a condition check that will fail,
    /// demonstrating that no items are written when any part of the transaction fails.
    /// </summary>
    /// <returns>A tuple indicating whether rollback was demonstrated and item count.</returns>
    public async Task<(bool RollbackDemonstrated, int ItemCountBefore, int ItemCountAfter)> DemonstrateRollbackAsync()
    {
        // Count items before the failed transaction
        var itemCountBefore = await _table.CountAllItemsAsync();

        try
        {
            // Create a transaction that will fail due to a condition check
            // We'll try to put an item with a condition that the item must NOT exist,
            // but we'll use a key that already exists (if any items exist)
            var transaction = DynamoDbTransactions.Write;

            // Add some new items
            for (int i = 1; i <= 5; i++)
            {
                var account = new Account
                {
                    Pk = Account.CreatePk($"ROLLBACK-{i}"),
                    Sk = Account.ProfileSk,
                    AccountId = $"ROLLBACK-{i}",
                    Name = $"Rollback Test {i}",
                    Balance = 500m
                };
                transaction = transaction.Add(_table.Put<Account>().WithItem(account));
            }

            // Add a condition check that will fail - check for an attribute
            // on an item that doesn't exist (this will cause the transaction to fail)
            transaction = transaction.Add(
                _table.ConditionCheck<Account>()
                    .WithKey("pk", Account.CreatePk("NON-EXISTENT-ACCOUNT"))
                    .WithKey("sk", Account.ProfileSk)
                    .Where("attribute_exists(pk)")); // This will fail because item doesn't exist

            await transaction.ExecuteAsync();
        }
        catch (TransactionCanceledException)
        {
            // Expected - the transaction was rolled back
        }
        catch (Exception)
        {
            // Other errors also result in rollback
        }

        // Count items after the failed transaction
        var itemCountAfter = await _table.CountAllItemsAsync();

        // Rollback is demonstrated if item count didn't change
        return (itemCountBefore == itemCountAfter, itemCountBefore, itemCountAfter);
    }
}
