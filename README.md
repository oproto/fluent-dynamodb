# oproto-fluent-dynamodb
Oproto.FluentDynamoDb library

A fluent-style API wrapper for Amazon DynamoDB.  This implementation is safe for use in AOT projects.

## Table and Index Definition
An optional feature of FluentDynamoDb it so define your tables, indexes and access patterns using the DynamoDbTableBase class.

```csharp
public class ToDoTable(IAmazonDynamoDB client, string todoTableName) : DynamoDbTableBase(client,todoTableName)
{
    Gsi1 = new DynamoDbIndex(this,"gsi1");
    
    public DynamoDbIndex Gsi1 { get; private init; }
}
```

You can then access query builders using the Get, Update, Query and Put properties.
```csharp
var table = new ToDoTable(...);
var getItemResponse = table.Get.WithKey("pk", todoId).ExecuteAsync();
```

You can further customize your table class implementation to include access patterns.
```csharp
public class ToDoTable(IAmazonDynamoDB client, string todoTableName) : DynamoDbTableBase(client,todoTableName)
{
    Gsi1 = new DynamoDbIndex(this,"gsi1");
    
    public DynamoDbIndex Gsi1 { get; private init; }
    
    public async Task<GetItemResponse> GetTodoAsync(string todoId) =>
        await Get.WithKey("pk", todoId).ExecuteAsync();
}
```

You can then access your access pattern as follows.
```csharp
var table = new ToDoTable(...);
var getItemResponse = await table.GetTodoAsync(todoId);
```

## Pagination
Pagination features in FluentDynamoDb are optional.

The Pagination extension method takes an implementation of IPaginationRequest.
If your service's request models implement this interface, you can pass the request object directly.

If you need to call your page size and request token values something different, the PaginationRequest class provides a default implementation you can pass in.

```csharp
var queryResponse = await table.Gsi1.Query
    .Where("gsi1pk = :gsi1pk")
    .WithValue(":gsi1pk", "Foo")
    .Paginate(paginationRequest)
    .ExecuteAsync();
```