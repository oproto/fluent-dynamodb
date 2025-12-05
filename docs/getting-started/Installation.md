---
title: "Installation Guide"
category: "getting-started"
order: 2
keywords: ["installation", "setup", "nuget", "package", "prerequisites", "AWS SDK"]
related: ["QuickStart.md", "FirstEntity.md"]
---

[Documentation](../README.md) > [Getting Started](README.md) > Installation

# Installation Guide

[Previous: Quick Start](QuickStart.md) | [Next: First Entity](FirstEntity.md)

---

This guide walks you through installing and configuring Oproto.FluentDynamoDb in your .NET project.

## Prerequisites

### .NET Requirements

- **.NET 8.0 SDK or later** (required)
- **C# 12** language features (required for source generation)
- **Nullable reference types** enabled (recommended)

Check your .NET version:

```bash
dotnet --version
```

### AWS Requirements

- **AWS Account** with DynamoDB access
- **AWS Credentials** configured (one of the following):
  - AWS CLI configured (`aws configure`)
  - Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
  - IAM role (for EC2, Lambda, ECS, etc.)
  - AWS credentials file (`~/.aws/credentials`)

### Development Environment

Supported IDEs:
- **Visual Studio 2022** (version 17.8 or later)
- **JetBrains Rider** (version 2023.3 or later)
- **Visual Studio Code** with C# Dev Kit extension

## NuGet Package Installation

### Core Package (Required)

Install the main package which includes the source generator and attributes:

```bash
# Main library with fluent API, source generator, and attributes
dotnet add package Oproto.FluentDynamoDb

# AWS SDK for DynamoDB
dotnet add package AWSSDK.DynamoDBv2
```

> **Note:** The source generator and attributes are bundled in the main package. No additional packages are required for basic usage.

### Optional Packages

```bash
# FluentResults integration for error handling
dotnet add package Oproto.FluentDynamoDb.FluentResults

# DynamoDB Streams support for Lambda functions
dotnet add package Amazon.Lambda.DynamoDBEvents

# Geospatial support (GeoHash, S2, H3)
dotnet add package Oproto.FluentDynamoDb.Geospatial

# Blob storage support (S3)
dotnet add package Oproto.FluentDynamoDb.BlobStorage.S3

# Field-level encryption (KMS)
dotnet add package Oproto.FluentDynamoDb.Encryption.Kms

# Microsoft.Extensions.Logging integration
dotnet add package Oproto.FluentDynamoDb.Logging.Extensions
```

### Configuring Optional Packages

Optional packages require configuration using `FluentDynamoDbOptions`. Each optional feature must be explicitly enabled when creating your table. See the [Configuration Guide](../core-features/Configuration.md) for complete details.

#### Geospatial Support

Install the geospatial package for GeoHash, S2, and H3 spatial indexing:

```bash
dotnet add package Oproto.FluentDynamoDb.Geospatial
```

Configure geospatial support using `AddGeospatial()`:

```csharp
using Oproto.FluentDynamoDb;

var options = new FluentDynamoDbOptions()
    .AddGeospatial();

var table = new LocationsTable(client, "locations", options);
```

Without this configuration, attempting to use geospatial features will result in an error:
> "Geospatial features require configuration. Add the Oproto.FluentDynamoDb.Geospatial package and call options.AddGeospatial() when creating your table."

#### Blob Storage (S3)

Install the S3 blob storage package for storing large objects:

```bash
dotnet add package Oproto.FluentDynamoDb.BlobStorage.S3
dotnet add package AWSSDK.S3
```

Configure blob storage using `WithBlobStorage()`:

```csharp
using Amazon.S3;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.BlobStorage.S3;

var s3Client = new AmazonS3Client();
var blobProvider = new S3BlobProvider(s3Client, "my-bucket");

var options = new FluentDynamoDbOptions()
    .WithBlobStorage(blobProvider);

var table = new DocumentsTable(client, "documents", options);
```

#### Field-Level Encryption (KMS)

Install the KMS encryption package for field-level encryption:

```bash
dotnet add package Oproto.FluentDynamoDb.Encryption.Kms
```

Configure encryption using `WithEncryption()`:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Encryption.Kms;

var keyResolver = new DefaultKmsKeyResolver("arn:aws:kms:us-east-1:123456789012:key/my-key");
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);

var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);

var table = new SecretsTable(client, "secrets", options);
```

#### Combining Multiple Features

You can chain configuration methods to enable multiple features:

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<MyTable>())
    .AddGeospatial()
    .WithBlobStorage(blobProvider)
    .WithEncryption(encryptor);

var table = new MyTable(client, "my-table", options);
```

### Package Manager Console (Visual Studio)

If you prefer using the Package Manager Console:

```powershell
Install-Package Oproto.FluentDynamoDb
Install-Package AWSSDK.DynamoDBv2
```

### Manual .csproj Configuration

Alternatively, add package references directly to your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Oproto.FluentDynamoDb" Version="0.8.0" />
    <PackageReference Include="AWSSDK.DynamoDBv2" Version="4.0.0" />
  </ItemGroup>
</Project>
```

> **Note:** The source generator and attributes are bundled in the main package. Check [NuGet.org](https://www.nuget.org/packages/Oproto.FluentDynamoDb) for the latest version numbers.

## Project Configuration

### Enable Source Generation

Ensure your project is configured to support source generators:

```xml
<PropertyGroup>
  <!-- Required for source generation -->
  <LangVersion>12.0</LangVersion>
  
  <!-- Recommended for better null safety -->
  <Nullable>enable</Nullable>
  
  <!-- Optional: Enable implicit usings -->
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

### AOT Compatibility (Optional)

If you're using Native AOT compilation:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

Oproto.FluentDynamoDb is fully compatible with AOT compilation.

## AWS SDK Setup

### Configure AWS Credentials

Choose one of the following methods:

#### Option 1: AWS CLI (Recommended for Development)

```bash
aws configure
```

Enter your credentials when prompted:
- AWS Access Key ID
- AWS Secret Access Key
- Default region (e.g., `us-east-1`)
- Default output format (e.g., `json`)

#### Option 2: Environment Variables

```bash
# Linux/macOS
export AWS_ACCESS_KEY_ID="your-access-key"
export AWS_SECRET_ACCESS_KEY="your-secret-key"
export AWS_DEFAULT_REGION="us-east-1"

# Windows (PowerShell)
$env:AWS_ACCESS_KEY_ID="your-access-key"
$env:AWS_SECRET_ACCESS_KEY="your-secret-key"
$env:AWS_DEFAULT_REGION="us-east-1"
```

#### Option 3: IAM Role (Recommended for Production)

When running on AWS services (EC2, Lambda, ECS, etc.), use IAM roles:

```csharp
// No explicit credentials needed - SDK uses IAM role automatically
var client = new AmazonDynamoDBClient();
```

#### Option 4: Credentials File

Create or edit `~/.aws/credentials`:

```ini
[default]
aws_access_key_id = your-access-key
aws_secret_access_key = your-secret-key
region = us-east-1
```

### DynamoDB Local (Optional)

For local development, you can use DynamoDB Local:

```bash
# Download and run DynamoDB Local
docker run -p 8000:8000 amazon/dynamodb-local
```

Configure client to use local endpoint:

```csharp
var config = new AmazonDynamoDBConfig
{
    ServiceURL = "http://localhost:8000"
};
var client = new AmazonDynamoDBClient(config);
```

## Verifying Source Generator

After installation, verify the source generator is working:

### Step 1: Create a Test Entity

Create a file `TestEntity.cs`:

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("test")]
public partial class TestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
}
```

### Step 2: Build the Project

```bash
dotnet build
```

### Step 3: Check Generated Code

The source generator should create nested classes within your entity:
- `TestEntity.Fields` class with field constants
- `TestEntity.Keys` class with key builder methods
- `TestEntityMapper` class with serialization logic

You can verify by using IntelliSense:

```csharp
// These should be available after build
var fieldName = TestEntity.Fields.Id;  // Should autocomplete
var key = TestEntity.Keys.Pk("test");  // Should autocomplete
```

### Troubleshooting Source Generator

If generated code is not available:

1. **Ensure partial keyword**: Class must be `partial`
2. **Clean and rebuild**: `dotnet clean && dotnet build`
3. **Restart IDE**: Close and reopen your IDE
4. **Check build output**: Look for source generator errors in build output
5. **Verify package installation**: Ensure `Oproto.FluentDynamoDb` is installed (source generator is bundled)

See [Troubleshooting Guide](../reference/Troubleshooting.md#source-generator-issues) for more help.

## IDE-Specific Notes

### Visual Studio 2022

- **Minimum Version**: 17.8 or later
- **View Generated Code**: Right-click project → Analyze → View Generated Files
- **IntelliSense**: Generated code appears automatically after build
- **Performance**: First build may be slower; subsequent builds are fast

### JetBrains Rider

- **Minimum Version**: 2023.3 or later
- **View Generated Code**: Navigate to generated files in Solution Explorer under Dependencies → Analyzers
- **IntelliSense**: Works seamlessly with generated code
- **Performance**: Excellent source generator support

### Visual Studio Code

- **Required Extension**: C# Dev Kit (Microsoft)
- **View Generated Code**: Check `obj/` folder for generated files
- **IntelliSense**: May require reloading window after first build
- **Command**: Use `dotnet build` from integrated terminal

## Verify Installation

Create a simple test to verify everything is working:

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Storage;

[DynamoDbTable("test-table")]
public partial class TestItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

class Program
{
    static async Task Main()
    {
        // Create client
        var client = new AmazonDynamoDBClient();
        var table = new TestItemsTable(client, "test-table");
        
        // Test generated code
        Console.WriteLine($"Field name: {TestItem.Fields.Id}");
        Console.WriteLine($"Key: {TestItem.Keys.Pk("test123")}");
        
        // Test basic operation
        var item = new TestItem { Id = "test123", Name = "Test" };
        await table.Put<TestItem>().WithItem(item).PutAsync();
        
        Console.WriteLine("Installation verified successfully!");
    }
}
```

Run the test:

```bash
dotnet run
```

If you see "Installation verified successfully!", you're all set!

## Next Steps

- **[Quick Start](QuickStart.md)** - Build your first application
- **[First Entity](FirstEntity.md)** - Learn about entity definition in depth
- **[Entity Definition](../core-features/EntityDefinition.md)** - Advanced entity patterns

## Common Issues

### Issue: "Type or namespace 'DynamoDbTable' could not be found"

**Solution**: Ensure `Oproto.FluentDynamoDb` package is installed (attributes are bundled) and you have the correct using statement:

```csharp
using Oproto.FluentDynamoDb.Attributes;
```

### Issue: "Generated code not appearing"

**Solution**: 
1. Ensure class is marked as `partial`
2. Run `dotnet clean && dotnet build`
3. Restart your IDE

### Issue: "AWS credentials not found"

**Solution**: Configure AWS credentials using one of the methods in [AWS SDK Setup](#aws-sdk-setup).

### Issue: "Unable to connect to DynamoDB"

**Solution**: 
1. Check AWS credentials are configured correctly
2. Verify IAM permissions include DynamoDB access
3. Check network connectivity to AWS
4. Verify region is correct

---

[Previous: Quick Start](QuickStart.md) | [Next: First Entity](FirstEntity.md)

**See Also:**
- [Troubleshooting Guide](../reference/Troubleshooting.md)
- [AWS SDK Documentation](https://docs.aws.amazon.com/sdk-for-net/)
- [DynamoDB Documentation](https://docs.aws.amazon.com/dynamodb/)
