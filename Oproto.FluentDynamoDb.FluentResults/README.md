# Oproto.FluentDynamoDb.FluentResults

FluentResults extensions for [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb), providing `Result<T>` return patterns instead of exceptions.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.FluentResults
```

## Overview

This package integrates [FluentResults](https://github.com/altmann/FluentResults) with Oproto.FluentDynamoDb, allowing you to use the Result pattern for error handling instead of exceptions.

## Usage

```csharp
using Oproto.FluentDynamoDb.FluentResults;

// Execute operations that return Result<T>
var result = await table.Users.GetAsync("pk", "sk").ToResult();

if (result.IsSuccess)
{
    var user = result.Value;
    // Process user
}
else
{
    // Handle errors
    foreach (var error in result.Errors)
    {
        Console.WriteLine(error.Message);
    }
}
```

## Features

- **Result Pattern**: Replace try/catch with explicit success/failure handling
- **Error Aggregation**: Collect multiple errors from batch operations
- **AOT Compatible**: Full support for Native AOT compilation
- **Type Safe**: Strongly-typed results with `Result<T>`

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.FluentResults](https://www.nuget.org/packages/Oproto.FluentDynamoDb.FluentResults)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
