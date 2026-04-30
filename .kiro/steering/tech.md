# Technology Stack

## Framework & Runtime
- **.NET 8.0**: Target framework for both main library and tests
- **C# 12**: Latest language features with nullable reference types enabled
- **AOT Compatible**: Library is trimmer-safe and AOT-compatible

## Dependencies
- **AWSSDK.DynamoDBv2**: AWS SDK for DynamoDB operations (version 4.0.0+)
- **Amazon.Lambda.DynamoDBEvents**: For DynamoDB stream processing in Lambda (version 3.1.1+)

## Testing Framework
- **xUnit**: Primary testing framework
- **FluentAssertions**: For readable test assertions
- **NSubstitute**: Mocking framework for unit tests
- **Coverlet**: Code coverage collection

## Build System
- **MSBuild**: Standard .NET build system
- **NuGet**: Package management and distribution

## Common Commands

### Build
```bash
dotnet build
```

### Test
```bash
dotnet test
```

### Pack NuGet Package
```bash
dotnet pack
```

### Restore Dependencies
```bash
dotnet restore
```

### Source Generator
When modifying the source generator, Dotnet will cache the old version in memory.  You must restart it with the following:
```bash
dotnet build-server shutdown
```

By default, the source generator WILL NOT write files to disk.  This has to be enabled in the csproj if you need to inspect the output.

## Async Conventions
- **ConfigureAwait(false)**: All `await` calls in library projects (non-test) MUST use `.ConfigureAwait(false)`. This prevents deadlocks when the library is consumed from environments with a SynchronizationContext (WPF, WinForms, Blazor WASM, legacy ASP.NET).
  - ✅ `await client.GetItemAsync(request, ct).ConfigureAwait(false);`
  - ❌ `await client.GetItemAsync(request, ct);`
  - Applies to: `Oproto.FluentDynamoDb/`, `Oproto.FluentDynamoDb.Streams/`, `Oproto.FluentDynamoDb.FluentResults/`, `Oproto.FluentDynamoDb.Geospatial/`, `Oproto.FluentDynamoDb.Encryption.Kms/`, `Oproto.FluentDynamoDb.BlobStorage.S3/`, `Oproto.FluentDynamoDb.Logging.Extensions/`, `Oproto.FluentDynamoDb.SystemTextJson/`, `Oproto.FluentDynamoDb.NewtonsoftJson/`
  - Does NOT apply to: test projects, example projects, or the source generator (which runs at compile time, not async)

## Project Configuration
- **ImplicitUsings**: Enabled for cleaner code
- **Nullable**: Enabled for null safety
- **GeneratePackageOnBuild**: Automatic NuGet package generation
- **IsTrimmable**: Supports .NET trimming
- **EnableTrimAnalyzer**: Trim analysis enabled