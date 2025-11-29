# Conditional Compilation for Logging

## Overview

Oproto.FluentDynamoDb includes comprehensive logging support that can be completely disabled at compile-time using conditional compilation. This allows you to have detailed logging during development and debugging while eliminating all logging overhead in production builds.

## How It Works

All logging code in the library is wrapped in conditional compilation directives:

```csharp
#if !DISABLE_DYNAMODB_LOGGING
logger?.LogInformation(LogEventIds.ExecutingQuery,
    "Executing Query on table {TableName}",
    tableName);
#endif
```

When you define the `DISABLE_DYNAMODB_LOGGING` symbol, the C# compiler completely removes all logging code from the compiled assembly. This means:

- **Zero runtime overhead** - No logging calls, no parameter evaluation, no allocations
- **Smaller binary size** - Logging code is not included in the compiled output
- **No performance impact** - The code runs as if logging never existed

## Configuration

### Per-Configuration (Recommended)

The most common approach is to disable logging only in Release builds while keeping it enabled for Debug builds:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <!-- Disable logging in Release builds only -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Oproto.FluentDynamoDb" Version="*" />
    <PackageReference Include="Oproto.FluentDynamoDb.SourceGenerator" Version="*" />
    <PackageReference Include="Oproto.FluentDynamoDb.Attributes" Version="*" />
  </ItemGroup>
</Project>
```

### Always Disabled

To disable logging in all configurations:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Oproto.FluentDynamoDb" Version="*" />
    <PackageReference Include="Oproto.FluentDynamoDb.SourceGenerator" Version="*" />
    <PackageReference Include="Oproto.FluentDynamoDb.Attributes" Version="*" />
  </ItemGroup>
</Project>
```

### Per-Environment

You can also create custom configurations for different environments:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <!-- Development: Full logging -->
  <PropertyGroup Condition="'$(Configuration)' == 'Development'">
    <!-- Logging enabled by default -->
  </PropertyGroup>

  <!-- Staging: Full logging -->
  <PropertyGroup Condition="'$(Configuration)' == 'Staging'">
    <!-- Logging enabled by default -->
  </PropertyGroup>

  <!-- Production: No logging -->
  <PropertyGroup Condition="'$(Configuration)' == 'Production'">
    <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Oproto.FluentDynamoDb" Version="*" />
    <PackageReference Include="Oproto.FluentDynamoDb.SourceGenerator" Version="*" />
    <PackageReference Include="Oproto.FluentDynamoDb.Attributes" Version="*" />
  </ItemGroup>
</Project>
```

## Verification

### Verify Logging is Disabled

To verify that logging has been completely removed from your build:

1. **Build with the symbol defined:**
   ```bash
   dotnet build -c Release
   ```

2. **Inspect the generated code:**
   - Navigate to `obj/Debug/net8.0/generated/`
   - Open the generated entity files
   - Verify that no logging code is present

3. **Use a decompiler:**
   - Use tools like ILSpy or dotPeek to inspect the compiled assembly
   - Search for logging-related strings - they should not exist

### Verify Logging is Enabled

To verify that logging is working in Debug builds:

1. **Build without the symbol:**
   ```bash
   dotnet build -c Debug
   ```

2. **Run your application with a logger configured:**
   ```csharp
   var loggerFactory = LoggerFactory.Create(builder =>
   {
       builder.AddConsole();
       builder.SetMinimumLevel(LogLevel.Trace);
   });
   
   var options = new FluentDynamoDbOptions()
       .WithLogger(loggerFactory.CreateLogger<ProductsTable>().ToDynamoDbLogger());
   var table = new ProductsTable(dynamoDbClient, "products", options);
   
   // You should see logging output
   await table.GetProductAsync(productId);
   ```

## Performance Impact

### With Logging Enabled (Debug)

When logging is enabled but using `NoOpLogger` (the default):

- **Minimal overhead** - Null-conditional operators (`logger?.Method()`) are very fast
- **No allocations** - Parameters are not evaluated when logger is null
- **Negligible performance impact** - Typically < 1% overhead

### With Logging Disabled (Release)

When `DISABLE_DYNAMODB_LOGGING` is defined:

- **Zero overhead** - All logging code is removed by the compiler
- **No allocations** - No logging-related objects are created
- **Identical performance** - Runs as if logging code never existed

## Best Practices

### 1. Use Configuration-Based Disabling

Always tie logging to your build configuration rather than manually editing code:

```xml
<!-- ✅ Good: Configuration-based -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
</PropertyGroup>

<!-- ❌ Bad: Always disabled -->
<PropertyGroup>
  <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
</PropertyGroup>
```

### 2. Keep Logging Enabled During Development

Logging is invaluable for debugging, especially in AOT scenarios where stack traces are limited. Only disable it in production builds.

### 3. Test Both Configurations

Ensure your application works correctly with both logging enabled and disabled:

```bash
# Test with logging enabled
dotnet test -c Debug

# Test with logging disabled
dotnet test -c Release
```

### 4. Document Your Configuration

Add comments to your .csproj file explaining why logging is disabled:

```xml
<!-- Disable DynamoDB logging in production for maximum performance.
     Logging adds ~1% overhead and is not needed in production.
     For troubleshooting production issues, temporarily remove this
     define and redeploy. -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
</PropertyGroup>
```

## Troubleshooting

### Logging Still Appears in Release Build

**Problem:** You defined `DISABLE_DYNAMODB_LOGGING` but still see logging code.

**Solution:**
1. Clean your build output: `dotnet clean`
2. Rebuild: `dotnet build -c Release`
3. Verify the symbol is defined in your .csproj
4. Check that you're inspecting the Release build, not Debug

### Logging Doesn't Work in Debug Build

**Problem:** Logging is not producing output even though `DISABLE_DYNAMODB_LOGGING` is not defined.

**Solution:**
1. Verify you're passing a logger to your table constructors
2. Check that your logger's minimum level includes the log levels you expect
3. Ensure you're using the correct logger adapter (e.g., `MicrosoftExtensionsLoggingAdapter`)

### Build Errors After Defining Symbol

**Problem:** Build fails with errors after defining `DISABLE_DYNAMODB_LOGGING`.

**Solution:**
1. This should not happen - all logging code is designed to compile with or without the symbol
2. Clean and rebuild: `dotnet clean && dotnet build`
3. If the issue persists, file a bug report with the error details

## Examples

### Example 1: AWS Lambda Function

For Lambda functions, disable logging in production to minimize cold start time:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AWSProjectType>Lambda</AWSProjectType>
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>

  <!-- Disable logging for Lambda deployment -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="2.2.0" />
    <PackageReference Include="Oproto.FluentDynamoDb" Version="*" />
    <PackageReference Include="Oproto.FluentDynamoDb.SourceGenerator" Version="*" />
  </ItemGroup>
</Project>
```

### Example 2: ASP.NET Core API

For web APIs, keep logging enabled in all environments except production:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <!-- Only disable in production -->
  <PropertyGroup Condition="'$(Configuration)' == 'Production'">
    <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Oproto.FluentDynamoDb" Version="*" />
    <PackageReference Include="Oproto.FluentDynamoDb.Logging.Extensions" Version="*" />
  </ItemGroup>
</Project>
```

### Example 3: Console Application

For console apps, use command-line arguments to control logging:

```bash
# Development: Full logging
dotnet run -c Debug

# Production: No logging
dotnet run -c Release
```

## Related Topics

- [Logging Configuration](./logging-configuration.md) - How to configure logging when enabled
- [Performance Optimization](./performance-optimization.md) - Other performance tuning options
- [AOT Compilation](./aot-compilation.md) - Using the library with Native AOT
