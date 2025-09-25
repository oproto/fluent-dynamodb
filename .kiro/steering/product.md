# Product Overview

Oproto.FluentDynamoDb is a fluent-style API wrapper for Amazon DynamoDB designed for .NET applications. The library provides a type-safe, intuitive interface for DynamoDB operations while maintaining compatibility with AOT (Ahead-of-Time) compilation.

## Key Features

- **Fluent API**: Chain method calls for building DynamoDB requests
- **AOT Compatible**: Safe for use in Native AOT projects
- **Table Abstraction**: Optional `DynamoDbTableBase` class for defining tables and access patterns
- **Transaction Support**: Fluent interface for DynamoDB transactions
- **Stream Processing**: Helper methods for processing DynamoDB Stream events in Lambda
- **Pagination**: Built-in pagination support with `IPaginationRequest` interface

## Target Use Cases

- .NET applications requiring DynamoDB access
- AWS Lambda functions processing DynamoDB streams
- Applications requiring AOT compilation
- Projects needing type-safe DynamoDB operations
- Systems requiring transaction support across multiple tables