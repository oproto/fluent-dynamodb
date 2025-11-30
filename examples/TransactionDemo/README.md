# TransactionDemo

This example demonstrates DynamoDB transactions using FluentDynamoDb and compares the code with raw AWS SDK usage.

## Features Demonstrated

- **DynamoDB Transactions**: Atomic write operations across multiple items
- **Code Comparison**: Side-by-side comparison of FluentDynamoDb vs raw SDK
- **Transaction Atomicity**: Demonstration of rollback on failure
- **Single-Table Design**: Accounts and transaction records in one table

## Key Concepts

### Transaction API

FluentDynamoDb provides a fluent API for building transactions:

```csharp
// FluentDynamoDb approach - concise and type-safe
await DynamoDbTransactions.Write
    .Add(table.Put(account1))
    .Add(table.Put(account2))
    .Add(table.Put(transactionRecord))
    .ExecuteAsync();
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

### Entity Design

The demo uses two entity types in a single table:

```
Account:
  pk: "ACCOUNT#{accountId}"
  sk: "PROFILE"

TransactionRecord:
  pk: "ACCOUNT#{accountId}"
  sk: "TXN#{timestamp}#{txnId}"
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
5. **View Current Items**: Display accounts and transactions in the table
6. **Clear All Items**: Remove all items from the table
7. **Exit**: Close the application

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
│   ├── Account.cs           # Account entity with balance
│   └── TransactionRecord.cs # Transaction record entity
├── Tables/
│   └── TransactionDemoTable.cs  # Table operations
├── TransactionComparison.cs     # Comparison logic
├── Program.cs                   # Interactive menu
└── README.md                    # This file
```

## Learn More

- [DynamoDB Transactions](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transactions.html)
- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
