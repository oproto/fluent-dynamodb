using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using TransactionDemo.Entities;

namespace TransactionDemo.Tables;

/// <summary>
/// Table class for managing accounts and transaction records in DynamoDB.
/// 
/// This class demonstrates single-table design where accounts and their
/// transaction records share the same table, using hierarchical composite
/// keys to enable efficient access patterns.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <para>
/// All entities use the same partition key format "ACCOUNT#{accountId}" to group
/// all data for an account together. The sort key distinguishes entity types:
/// </para>
/// <list type="bullet">
/// <item><description>Account: sk = "PROFILE"</description></item>
/// <item><description>Transaction: sk = "TXN#{timestamp}#{txnId}"</description></item>
/// </list>
/// <para>
/// <strong>Access Patterns:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Get account profile: Query pk = "ACCOUNT#{id}", sk = "PROFILE"</description></item>
/// <item><description>Get account transactions: Query pk = "ACCOUNT#{id}", sk begins_with "TXN#"</description></item>
/// </list>
/// </remarks>
public class TransactionDemoTable : DynamoDbTableBase
{
    /// <summary>
    /// The name of the DynamoDB table for transaction demo.
    /// </summary>
    public const string TableName = "transaction-demo";

    /// <summary>
    /// Initializes a new instance of the TransactionDemoTable class.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    public TransactionDemoTable(IAmazonDynamoDB client) : base(client, TableName)
    {
    }

    /// <summary>
    /// Gets an account by ID.
    /// </summary>
    /// <param name="accountId">The account ID.</param>
    /// <returns>The account, or null if not found.</returns>
    public async Task<Account?> GetAccountAsync(string accountId)
    {
        return await Get<Account>()
            .WithKey("pk", Account.CreatePk(accountId))
            .WithKey("sk", Account.ProfileSk)
            .GetItemAsync();
    }

    /// <summary>
    /// Gets all accounts in the table.
    /// </summary>
    /// <returns>A list of all accounts.</returns>
    public async Task<List<Account>> GetAllAccountsAsync()
    {
        // PREFERRED: Lambda expression approach
        var items = await Scan<Account>().ToListAsync();
        
        // Filter to only account profiles (not transaction records)
        return items.Where(x => x.Sk == Account.ProfileSk).ToList();
    }

    /// <summary>
    /// Gets all transaction records for an account.
    /// </summary>
    /// <param name="accountId">The account ID.</param>
    /// <returns>A list of transaction records, ordered by timestamp descending.</returns>
    public async Task<List<TransactionRecord>> GetAccountTransactionsAsync(string accountId)
    {
        var pk = Account.CreatePk(accountId);

        // PREFERRED: Format string approach for begins_with queries
        var transactions = await Query<TransactionRecord>()
            .Where("pk = {0} AND begins_with(sk, {1})", pk, TransactionRecord.TxnSkPrefix)
            .ToListAsync();

        // ALTERNATIVE: Manual attribute approach
        // var transactions = await Query<TransactionRecord>()
        //     .Where("#pk = :pk AND begins_with(#sk, :skPrefix)")
        //     .WithAttribute("#pk", "pk")
        //     .WithAttribute("#sk", "sk")
        //     .WithValue(":pk", pk)
        //     .WithValue(":skPrefix", TransactionRecord.TxnSkPrefix)
        //     .ToListAsync();

        return transactions.OrderByDescending(t => t.Timestamp).ToList();
    }

    /// <summary>
    /// Counts all items in the table (accounts and transactions).
    /// </summary>
    /// <returns>The total number of items.</returns>
    public async Task<int> CountAllItemsAsync()
    {
        var items = await Scan<Account>().ToListAsync();
        return items.Count;
    }

    /// <summary>
    /// Deletes all items in the table.
    /// </summary>
    public async Task DeleteAllItemsAsync()
    {
        // Get all items
        var response = await Scan<Account>().ToDynamoDbResponseAsync();
        
        if (response.Items == null || response.Items.Count == 0)
            return;

        // Delete each item
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
