# Oproto.FluentDynamoDb.Streams

DynamoDB Streams processing support for [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb), enabling fluent event handling in AWS Lambda functions.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.Streams
```

## Overview

This package provides a fluent API for processing DynamoDB stream events in AWS Lambda functions. It integrates seamlessly with the Oproto.FluentDynamoDb source generator for type-safe entity deserialization.

## Usage

```csharp
using Oproto.FluentDynamoDb.Streams;
using Amazon.Lambda.DynamoDBEvents;

public class StreamHandler
{
    public async Task HandleAsync(DynamoDBEvent dynamoEvent)
    {
        var processor = new StreamProcessor<Order>();
        
        await processor
            .OnInsert(async order => 
            {
                Console.WriteLine($"New order: {order.OrderId}");
            })
            .OnModify(async (oldOrder, newOrder) => 
            {
                Console.WriteLine($"Order updated: {newOrder.OrderId}");
            })
            .OnRemove(async order => 
            {
                Console.WriteLine($"Order deleted: {order.OrderId}");
            })
            .ProcessAsync(dynamoEvent);
    }
}
```

## Features

- **Type-Safe Deserialization**: Automatic entity mapping using source-generated code
- **LINQ-Style Filtering**: Filter events before processing
- **Discriminator Routing**: Route events to handlers based on entity type in single-table designs
- **AOT Compatible**: Full support for Native AOT compilation
- **Fluent API**: Chain handlers for clean, readable code

## Single-Table Design Support

```csharp
// Route events based on entity discriminator
var processor = new MultiEntityStreamProcessor()
    .ForEntity<Order>(p => p
        .OnInsert(async order => { /* handle new order */ })
        .OnModify(async (old, @new) => { /* handle order update */ }))
    .ForEntity<Customer>(p => p
        .OnInsert(async customer => { /* handle new customer */ }));

await processor.ProcessAsync(dynamoEvent);
```

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.Streams](https://www.nuget.org/packages/Oproto.FluentDynamoDb.Streams)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
