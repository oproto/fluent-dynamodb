# TransactionDemo

This example demonstrates DynamoDB transactions using FluentDynamoDb and compares the code with raw AWS SDK usage.

## Features Demonstrated

- **DynamoDB Transactions**: Atomic write operations across multiple items
- **Code Comparison**: Side-by-side comparison of FluentDynamoDb vs raw SDK
- **Transaction Atomicity**: Demonstration of rollback on failure
- **RequireWriteTransaction**: Enforcing transactional writes for critical entities
- **Single-Table Design**: Accounts and transaction records in one table

## Key Concepts

### Transaction API

FluentDynamoDb provides a fluent API for building transactions using generated entity accessors:

```csharp
// FluentDynamoDb approach - concise and type-safe using entity accessors
var transaction = DynamoDbTransactions.Write
    .Add(table.Accounts.Put(account1))
    .Add(table.Accounts.Put(account2))
    .Add(table.Transactions.Put(transactionRecord));

await transaction.ExecuteAsync();
```

Compare this to the raw SDK approach:

```csharp
// Raw SDK approach - verbose and error-prone
var request = new TransactWriteItemsRequest
{
    TransactItems = new List<TransactWriteItem>
    {
        new TransactWriteItem
        {
            Put = new Put
            {
                TableName = "transaction-demo",
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = "ACCOUNT#123" },
                    ["sk"] = new AttributeValue { S = "PROFILE" },
                    ["balance"] = new AttributeValue { N = "1000" }
                }
            }
        }
        // ... repeat for each item
    }
};
await client.TransactWriteItemsAsync(request);
```

### Transaction Atomicity

DynamoDB transactions provide ACID guarantees:
- **Atomicity**: All operations succeed or all fail
- **Consistency**: Database remains in valid state
- **Isolation**: Transaction is isolated from others
- **Durability**: Committed changes are permanent

### RequireWriteTransaction Attribute

The `[RequireWriteTransaction]` attribute enforces that certain entities can ONLY be written within a DynamoDB transaction. This is useful for:

- **Financial transactions** that must be atomic
- **Inventory updates** requiring consistency
- **Multi-entity operations** that must succeed or fail together
- **Audit-critical records** needing transactional guarantees

```csharp
// Entity marked with [RequireWriteTransaction]
[DynamoDbTable("transaction-demo")]
[GenerateEntityProperty(Name = "FinancialTransactions")]
[RequireWriteTransaction]  // <-- This attribute enforces transactional writes
public partial class FinancialTransaction
{
    [PartitionKey(Prefix = "ACCOUNT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "FIN")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
    
    // ... other properties
}
```

When this attribute is applied:

```csharp
// ❌ Direct writes throw InvalidOperationException
await table.FinancialTransactions.Put(txn).ToDynamoDbResponseAsync();
// Throws: "Entity 'FinancialTransaction' is marked with [RequireWriteTransaction] 
//          and cannot be modified outside of a transaction..."

// ✓ Transactional writes succeed
var transaction = DynamoDbTransactions.Write
    .Add(table.FinancialTransactions.Put(txn));
await transaction.ExecuteAsync();  // Works!
```

This pattern ensures that critical data modifications are always performed atomically, preventing partial updates that could leave your data in an inconsistent state.

### Entity Design

The demo uses two entity types in a single table with the `[PartitionKey(Prefix = "...")]` pattern:

```csharp
// Account entity - uses [PartitionKey(Prefix = "ACCOUNT")] for automatic key formatting
[DynamoDbTable("transaction-demo", IsDefault = true)]
[GenerateEntityProperty(Name = "Accounts")]
[Scannable]
public partial class Account
{
    [PartitionKey(Prefix = "ACCOUNT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [DynamoDbAttribute("balance")]
    public decimal Balance { get; set; }

    public const string ProfileSk = "PROFILE";
}

// TransactionRecord entity - shares partition key space with Account
[DynamoDbTable("transaction-demo")]
[GenerateEntityProperty(Name = "Transactions")]
public partial class TransactionRecord
{
    [PartitionKey(Prefix = "ACCOUNT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("txnId")]
    public string TxnId { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    public const string TxnSkPrefix = "TXN#";
}
```

Key structure in DynamoDB:
```
Account:
  pk: "ACCOUNT#{accountId}"
  sk: "PROFILE"

TransactionRecord:
  pk: "ACCOUNT#{accountId}"
  sk: "TXN#{timestamp}#{txnId}"
```

### Key Construction

The source generator creates a `Keys` class for each entity with methods to construct properly formatted keys:

```csharp
// Use the generated Keys class - NOT manual CreatePk() methods
var pk = Account.Keys.Pk(accountId);           // Returns "ACCOUNT#123"
var pk = TransactionRecord.Keys.Pk(accountId); // Returns "ACCOUNT#123"

// Creating entities with proper keys
var account = new Account
{
    Pk = Account.Keys.Pk(accountId),
    Sk = Account.ProfileSk,
    AccountId = accountId,
    Balance = 1000m
};

var txnRecord = new TransactionRecord
{
    Pk = TransactionRecord.Keys.Pk(accountId),
    Sk = $"TXN#{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}#{txnId}",
    TxnId = txnId,
    Amount = 100m
};
```

## Running the Example

### Prerequisites

1. DynamoDB Local running on port 8000:
   ```bash
   java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb
   ```

2. .NET 8.0 SDK installed

### Run the Application

```bash
cd examples/TransactionDemo
dotnet run
```

### Menu Options

1. **Run FluentDynamoDb Transaction**: Execute 25 put operations using FluentDynamoDb
2. **Run Raw SDK Transaction**: Execute identical operations using raw AWS SDK
3. **Compare Results**: View side-by-side comparison of both approaches
4. **Demonstrate Failure Rollback**: Show that failed transactions write no items
5. **Demonstrate RequireWriteTransaction**: Show how the attribute blocks direct writes
6. **View Current Items**: Display accounts and transactions in the table
7. **Clear All Items**: Remove all items from the table
8. **Exit**: Close the application

## Code Reduction

The FluentDynamoDb approach typically reduces code by approximately 60-70% compared to raw SDK usage:

| Metric | FluentDynamoDb | Raw SDK |
|--------|----------------|---------|
| Lines of Code | ~35 | ~95 |
| Type Safety | ✓ | ✗ |
| IntelliSense | ✓ | Limited |
| Compile-time Checks | ✓ | ✗ |

## Project Structure

```
TransactionDemo/
├── Entities/
│   ├── Account.cs              # Account entity with balance
│   ├── FinancialTransaction.cs # Entity with [RequireWriteTransaction]
│   └── TransactionRecord.cs    # Transaction record entity
├── TransactionComparison.cs    # Comparison logic
├── Program.cs                  # Interactive menu
└── README.md                   # This file
```

Note: The table class is generated by the source generator based on the `[DynamoDbTable]` attributes on the entity classes. No custom table class file is needed.

## Learn More

- [DynamoDB Transactions](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transactions.html)
- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
