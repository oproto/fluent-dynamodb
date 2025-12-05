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

## Project Configuration
- **ImplicitUsings**: Enabled for cleaner code
- **Nullable**: Enabled for null safety
- **GeneratePackageOnBuild**: Automatic NuGet package generation
- **IsTrimmable**: Supports .NET trimming
- **EnableTrimAnalyzer**: Trim analysis enabled