using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;
using TransactionDemo.Entities;

namespace TransactionDemo;

/// <summary>
/// Compares FluentDynamoDb transaction API with raw AWS SDK approach.
/// </summary>
public class TransactionComparison
{
    private readonly IAmazonDynamoDB _client;
    private readonly TransactionDemoTable _table;

    public TransactionComparison(IAmazonDynamoDB client, TransactionDemoTable table)
    {
        _client = client;
        _table = table;
    }

    public record TransactionResult(
        bool Success,
        TimeSpan ExecutionTime,
        int LineCount,
        string? ErrorMessage = null);

    public async Task<TransactionResult> ExecuteFluentTransactionAsync()
    {
        const int lineCount = 35;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var transaction = DynamoDbTransactions.Write;

            for (int i = 1; i <= 25; i++)
            {
                var accountId = $"ACCT-{i:D3}";
                var timestamp = DateTime.UtcNow;
                var txnId = Guid.NewGuid().ToString("N")[..8];

                if (i <= 10)
                {
                    var account = new Account
                    {
                        Pk = Account.Keys.Pk(accountId),
                        Sk = Account.ProfileSk,
                        AccountId = accountId,
                        Name = $"Account {i}",
                        Balance = 1000m * i
                    };
                    transaction = transaction.Add(_table.Accounts.Put(account));
                }
                else
                {
                    var targetAccountId = $"ACCT-{((i - 11) % 5) + 1:D3}";
                    var txnRecord = new TransactionRecord
                    {
                        Pk = TransactionRecord.Keys.Pk(targetAccountId),
                        Sk = $"TXN#{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}#{txnId}",
                        TxnId = txnId,
                        AccountId = targetAccountId,
                        Amount = 100m * (i - 10),
                        Type = i % 2 == 0 ? "CREDIT" : "DEBIT",
                        Timestamp = timestamp,
                        Description = $"Transaction {i - 10}"
                    };
                    transaction = transaction.Add(_table.Transactions.Put(txnRecord));
                }
            }

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


    public async Task<TransactionResult> ExecuteRawSdkTransactionAsync()
    {
        const int lineCount = 95;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var request = new TransactWriteItemsRequest
            {
                TransactItems = new List<TransactWriteItem>()
            };

            for (int i = 1; i <= 25; i++)
            {
                var accountId = $"ACCT-{i:D3}";
                var timestamp = DateTime.UtcNow;
                var txnId = Guid.NewGuid().ToString("N")[..8];

                if (i <= 10)
                {
                    var item = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
                        ["sk"] = new AttributeValue { S = Account.ProfileSk },
                        ["accountId"] = new AttributeValue { S = accountId },
                        ["accountName"] = new AttributeValue { S = $"Account {i}" },
                        ["balance"] = new AttributeValue { N = (1000m * i).ToString() }
                    };

                    request.TransactItems.Add(new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = TransactionDemoTable.TableName,
                            Item = item
                        }
                    });
                }
                else
                {
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
                            TableName = TransactionDemoTable.TableName,
                            Item = item
                        }
                    });
                }
            }

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

    public async Task<(bool RollbackDemonstrated, int ItemCountBefore, int ItemCountAfter)> DemonstrateRollbackAsync()
    {
        var responseBefore = await _table.Accounts.Scan().ToDynamoDbResponseAsync();
        var itemCountBefore = responseBefore.Items?.Count ?? 0;

        try
        {
            var transaction = DynamoDbTransactions.Write;

            for (int i = 1; i <= 5; i++)
            {
                var account = new Account
                {
                    Pk = Account.Keys.Pk($"ROLLBACK-{i}"),
                    Sk = Account.ProfileSk,
                    AccountId = $"ROLLBACK-{i}",
                    Name = $"Rollback Test {i}",
                    Balance = 500m
                };
                transaction = transaction.Add(_table.Accounts.Put(account));
            }

            transaction = transaction.Add(
                _table.Accounts.ConditionCheck(
                    Account.Keys.Pk("NON-EXISTENT-ACCOUNT"),
                    Account.ProfileSk)
                    .Where("attribute_exists(pk)"));

            await transaction.ExecuteAsync();
        }
        catch (TransactionCanceledException)
        {
        }
        catch (Exception)
        {
        }

        var responseAfter = await _table.Accounts.Scan().ToDynamoDbResponseAsync();
        var itemCountAfter = responseAfter.Items?.Count ?? 0;

        return (itemCountBefore == itemCountAfter, itemCountBefore, itemCountAfter);
    }
}
