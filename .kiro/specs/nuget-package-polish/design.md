# Design Document

## Overview

This design covers the implementation of consistent NuGet package polish across all 10 Oproto.FluentDynamoDb packages. The work includes creating missing README files, adding package icons, ensuring consistent metadata, and reviewing existing READMEs for quality and consistency.

## Architecture

The solution uses a centralized approach for shared assets:
- Package icon stored in `docs/assets/icon.png` and referenced by all packages
- Common metadata defined in `Directory.Build.props` at the solution root
- Package-specific metadata (description, tags) defined in individual .csproj files
- README files stored in each package directory and packed into the NuGet package

```
Solution Root
├── Directory.Build.props          # Common metadata (Authors, Copyright, URLs)
├── docs/
│   └── assets/
│       ├── icon.png               # Shared 128x128 package icon
│       └── FluentDynamoDBLogo.svg # Logo for documentation
└── Oproto.FluentDynamoDb.*/
    ├── README.md                  # Package-specific README
    └── *.csproj                   # Package-specific metadata + icon/README references
```

## Components and Interfaces

### Package Icon Configuration

Each .csproj will reference the shared icon:

```xml
<PropertyGroup>
    <PackageIcon>icon.png</PackageIcon>
</PropertyGroup>

<ItemGroup>
    <None Include="..\docs\assets\icon.png" Pack="true" PackagePath="\" />
</ItemGroup>
```

### README Configuration

Each .csproj will include the README:

```xml
<PropertyGroup>
    <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>

<ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

### Standard README Template

All READMEs will follow this structure:

```markdown
# Package.Name

Brief description of what the package provides.

## Installation

\`\`\`bash
dotnet add package Package.Name
\`\`\`

## Overview

More detailed explanation of the package purpose and when to use it.

## Usage

\`\`\`csharp
// Code example showing basic usage
\`\`\`

## Features

- Feature 1
- Feature 2
- Feature 3

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Package.Name](https://www.nuget.org/packages/Package.Name)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
```

## Data Models

### Package Inventory

| Package | Has README | Needs Icon Config | Notes |
|---------|------------|-------------------|-------|
| Oproto.FluentDynamoDb | ❌ Create | ✅ Add | Core package |
| Oproto.FluentDynamoDb.BlobStorage.S3 | ✅ Review | ✅ Add | S3 blob storage |
| Oproto.FluentDynamoDb.Encryption.Kms | ✅ Review | ✅ Add | KMS encryption |
| Oproto.FluentDynamoDb.FluentResults | ✅ Review | ✅ Add | Result pattern |
| Oproto.FluentDynamoDb.Geospatial | ✅ Review | ✅ Add | Geospatial queries |
| Oproto.FluentDynamoDb.Logging.Extensions | ❌ Create | ✅ Add | MS Logging adapter |
| Oproto.FluentDynamoDb.NewtonsoftJson | ❌ Create | ✅ Add | JSON serialization (no AOT) |
| Oproto.FluentDynamoDb.SourceGenerator | ✅ Review | ✅ Add | Source generator |
| Oproto.FluentDynamoDb.Streams | ✅ Review | ✅ Add | DynamoDB Streams |
| Oproto.FluentDynamoDb.SystemTextJson | ❌ Create | ✅ Add | JSON serialization (AOT) |

### Package Descriptions

| Package | Description |
|---------|-------------|
| Oproto.FluentDynamoDb | A modern, fluent-style API wrapper for Amazon DynamoDB with source generation, expression formatting, and full AOT compatibility. |
| Oproto.FluentDynamoDb.BlobStorage.S3 | S3 blob storage integration for Oproto.FluentDynamoDb, enabling transparent storage of large fields in S3. |
| Oproto.FluentDynamoDb.Encryption.Kms | Field-level encryption for Oproto.FluentDynamoDb using AWS KMS and the AWS Encryption SDK. |
| Oproto.FluentDynamoDb.FluentResults | FluentResults extensions for Oproto.FluentDynamoDb providing Result<T> return patterns instead of exceptions. |
| Oproto.FluentDynamoDb.Geospatial | Geospatial query support for Oproto.FluentDynamoDb using S2 geometry for proximity searches. |
| Oproto.FluentDynamoDb.Logging.Extensions | Microsoft.Extensions.Logging adapter for Oproto.FluentDynamoDb diagnostic logging. |
| Oproto.FluentDynamoDb.NewtonsoftJson | Newtonsoft.Json serialization support for nested objects in Oproto.FluentDynamoDb entities. |
| Oproto.FluentDynamoDb.SourceGenerator | Source generator for Oproto.FluentDynamoDb providing compile-time code generation for entity mapping. |
| Oproto.FluentDynamoDb.Streams | DynamoDB Streams processing support for Oproto.FluentDynamoDb with fluent event handling. |
| Oproto.FluentDynamoDb.SystemTextJson | System.Text.Json serialization support for nested objects in Oproto.FluentDynamoDb entities with full AOT compatibility. |

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

This specification primarily involves documentation and configuration changes rather than runtime behavior. The correctness properties focus on structural validation:

Property 1: README file existence
*For any* package in the solution that is packable (IsPackable=true), a README.md file SHALL exist in the package directory.
**Validates: Requirements 1.1, 1.2, 1.3, 1.4**

Property 2: README structure completeness
*For any* package README.md file, the file SHALL contain all required sections: title, installation, usage, links, and license.
**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**

Property 3: Package icon configuration
*For any* package .csproj file that is packable, the file SHALL contain PackageIcon property and appropriate ItemGroup referencing the shared icon.
**Validates: Requirements 3.1, 3.2, 3.4**

Property 4: README packaging configuration
*For any* package .csproj file that has a README.md, the file SHALL contain PackageReadmeFile property and appropriate ItemGroup to include the README.
**Validates: Requirements 1.5**

## Error Handling

- If the icon file is missing, the build will fail with a clear error about the missing file
- If README references are incorrect, NuGet pack will fail with file not found errors
- Validation can be performed by running `dotnet pack` on each project

## Testing Strategy

### Manual Verification

1. Run `dotnet pack` on each package to verify icon and README are included
2. Extract the .nupkg file and verify contents include icon.png and README.md
3. Verify README renders correctly by viewing in a markdown preview

### Automated Checks

- CI/CD pipeline already runs `dotnet pack` which will catch missing file references
- Consider adding a script to verify all packable projects have README.md files

### Property-Based Testing

Given the documentation/configuration nature of this spec, traditional property-based testing is not applicable. Instead, structural validation scripts can verify:
- All packable projects have README.md
- All README.md files contain required sections
- All .csproj files have icon and README configuration
