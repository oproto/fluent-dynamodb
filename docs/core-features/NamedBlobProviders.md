# Named Blob Providers

This guide explains how to configure multiple blob storage providers in FluentDynamoDb, enabling different entity properties to target different blob backends (e.g., images in one S3 bucket, documents in another).

## Overview

By default, FluentDynamoDb supports a single blob storage provider configured via `WithBlobStorage(provider)`. Named Blob Providers extends this to support registering multiple `IBlobStorageProvider` instances by name, and allows each `[BlobStorage]` property to specify which provider it should use.

### Motivation

Real-world applications often store different types of blobs in different locations:

- **Images** in a CDN-optimized S3 bucket with aggressive caching
- **Documents** in a compliance-regulated bucket with versioning and retention policies
- **Temporary files** in a lifecycle-managed bucket with automatic expiration

Named Blob Providers lets you map each blob property to its appropriate backend without workaround code.

## Quick Start

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.BlobStorage.S3;

// Register multiple providers
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(new S3BlobProvider(s3Client, "default-bucket"))           // Default provider
    .WithBlobStorage("images", new S3BlobProvider(s3Client, "images-bucket"))  // Named: "images"
    .WithBlobStorage("documents", new S3BlobProvider(s3Client, "docs-bucket"));// Named: "documents"

var table = new MediaTable(client, "media", options);
```

```csharp
// Entity with multiple blob properties targeting different providers
[DynamoDbTable("Media")]
public partial class MediaItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [BlobStorage(Provider = "images")]
    [DynamoDbAttribute("thumbnail")]
    public BlobData<byte[]> Thumbnail { get; set; }

    [BlobStorage(Provider = "documents")]
    [DynamoDbAttribute("contract")]
    public BlobData<byte[]> ContractPdf { get; set; }

    [BlobStorage]  // Uses the default provider
    [DynamoDbAttribute("attachment")]
    public BlobData<byte[]> Attachment { get; set; }
}
```

## Registration

### Default Provider

The existing `WithBlobStorage(IBlobStorageProvider provider)` method registers the default (unnamed) provider. This is unchanged from previous versions.

```csharp
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(new S3BlobProvider(s3Client, "my-bucket"));
```

Properties using `[BlobStorage]` without a `Provider` value resolve to this default provider.

### Named Providers

Use the new `WithBlobStorage(string name, IBlobStorageProvider provider)` overload to register providers by name:

```csharp
var options = new FluentDynamoDbOptions()
    .WithBlobStorage("images", new S3BlobProvider(s3Client, "images-bucket"))
    .WithBlobStorage("documents", new S3BlobProvider(s3Client, "docs-bucket"));
```

**Name requirements:**
- Must not be null, empty, or whitespace-only
- If a name that already exists is registered again, the new provider replaces the previous one

### Fluent Chaining

Both registration methods support fluent chaining and can be combined freely:

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(logger)
    .WithBlobStorage(defaultProvider)                      // Default
    .WithBlobStorage("images", imageProvider)              // Named
    .WithBlobStorage("documents", documentProvider)        // Named
    .WithEncryption(encryptor);
```

Each call returns a new `FluentDynamoDbOptions` instance following the existing copy-on-write immutable pattern. Previously configured providers are preserved through the chain.

## Attribute Usage

### Default Provider (no change needed)

Existing `[BlobStorage]` attributes work exactly as before. When no `Provider` is specified, the default provider is used:

```csharp
[BlobStorage]
[DynamoDbAttribute("content")]
public BlobData<byte[]> Content { get; set; }

[BlobStorage(LazyLoad = true)]
[JsonBlob]
[DynamoDbAttribute("metadata")]
public BlobData<DocumentMetadata> Metadata { get; set; }
```

### Named Provider

Set the `Provider` property to target a specific named provider:

```csharp
[BlobStorage(Provider = "images")]
[DynamoDbAttribute("photo")]
public BlobData<byte[]> Photo { get; set; }

[BlobStorage(Provider = "documents", LazyLoad = true)]
[DynamoDbAttribute("report")]
public BlobData<byte[]> Report { get; set; }
```

The `Provider` property can be combined with `LazyLoad` and other attributes like `[JsonBlob]`, `[Encrypted]`, and `[Sensitive]`.

## Resolution Behavior

At runtime, the source-generated hydration and mapping code calls `options.GetBlobProvider(providerName)` once per blob property, passing the `Provider` value from the attribute (or `null` if not set).

### Default Resolution

When `GetBlobProvider` is called with `null` or an empty string, it returns the default provider registered via `WithBlobStorage(provider)`.

```csharp
// These properties resolve to the default provider:
[BlobStorage]                    // Provider is null → GetBlobProvider(null) → default
[BlobStorage(Provider = null)]   // Explicit null → same behavior
```

### Named Resolution

When `GetBlobProvider` is called with a non-empty name, it looks up the provider in the named registry:

```csharp
// This property resolves to the "images" named provider:
[BlobStorage(Provider = "images")]  // → GetBlobProvider("images") → named "images" provider
```

### Error Messages

Clear, actionable error messages are thrown when resolution fails:

**No default provider configured:**
```
InvalidOperationException: No default blob storage provider has been configured.
Call .WithBlobStorage(provider) on FluentDynamoDbOptions to register one.
```

**Named provider not found (other providers registered):**
```
InvalidOperationException: Named blob storage provider 'archives' is not registered.
Available providers: documents, images.
Call .WithBlobStorage("archives", provider) on FluentDynamoDbOptions to register it.
```

**Named provider not found (no named providers registered):**
```
InvalidOperationException: Named blob storage provider 'archives' is not registered
and no named providers have been configured.
Call .WithBlobStorage("archives", provider) on FluentDynamoDbOptions to register it.
```

These exceptions propagate naturally from the generated code without being caught, preserving the full error context for debugging.

## Complete Example

This end-to-end example shows a multi-tenant document management system where different blob types live in different S3 buckets.

### Entity Definition

```csharp
[DynamoDbTable("TenantDocuments")]
public partial class TenantDocument
{
    [PartitionKey(Prefix = "TENANT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "DOC")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;

    // PDF stored in compliance-regulated bucket with versioning
    [BlobStorage(Provider = "compliance")]
    [DynamoDbAttribute("pdfContent")]
    public BlobData<byte[]> PdfContent { get; set; }

    // Thumbnail stored in CDN-optimized bucket
    [BlobStorage(Provider = "cdn")]
    [DynamoDbAttribute("thumbnail")]
    public BlobData<byte[]> Thumbnail { get; set; }

    // Temporary working copy in lifecycle-managed bucket (lazy loaded)
    [BlobStorage(Provider = "temp", LazyLoad = true)]
    [DynamoDbAttribute("workingCopy")]
    public BlobData<byte[]> WorkingCopy { get; set; }

    // Audit log uses the default provider
    [BlobStorage]
    [JsonBlob]
    [DynamoDbAttribute("auditLog")]
    public BlobData<AuditEntry[]> AuditLog { get; set; }
}
```

### Configuration and DI Registration

```csharp
// In your service configuration (e.g., Startup.cs or Program.cs)
services.AddSingleton(sp =>
{
    var s3Client = sp.GetRequiredService<IAmazonS3>();
    var dynamoClient = sp.GetRequiredService<IAmazonDynamoDB>();

    var options = new FluentDynamoDbOptions()
        .WithLogger(sp.GetRequiredService<ILoggerFactory>().ToDynamoDbLogger<TenantDocumentsTable>())
        .WithBlobStorage(new S3BlobProvider(s3Client, "default-blobs"))
        .WithBlobStorage("compliance", new S3BlobProvider(s3Client, "regulated-documents", "docs/"))
        .WithBlobStorage("cdn", new S3BlobProvider(s3Client, "cdn-assets", "thumbnails/"))
        .WithBlobStorage("temp", new S3BlobProvider(s3Client, "temp-files", "working/"));

    return new TenantDocumentsTable(dynamoClient, "tenant-documents", options);
});
```

### Usage

```csharp
public class DocumentService
{
    private readonly TenantDocumentsTable _table;

    public DocumentService(TenantDocumentsTable table)
    {
        _table = table;
    }

    public async Task<TenantDocument> GetDocumentAsync(string tenantId, string docId)
    {
        var pk = TenantDocument.Keys.Pk(tenantId);
        var sk = TenantDocument.Keys.Sk(docId);

        // Hydration automatically resolves the correct provider per property:
        // - PdfContent → "compliance" provider → regulated-documents bucket
        // - Thumbnail → "cdn" provider → cdn-assets bucket
        // - WorkingCopy → "temp" provider (lazy, not loaded yet)
        // - AuditLog → default provider → default-blobs bucket
        return await _table.TenantDocuments.Get(pk, sk).GetItemAsync();
    }

    public async Task SaveDocumentAsync(TenantDocument doc)
    {
        // Serialization automatically routes each blob to its correct provider
        await _table.TenantDocuments.Put(doc).PutAsync();
    }
}
```

## Backwards Compatibility

Named Blob Providers is fully backwards compatible with existing code:

| Scenario | Behavior |
|----------|----------|
| Existing `[BlobStorage]` without `Provider` | Continues to use the default provider — no changes needed |
| Existing `WithBlobStorage(provider)` calls | Work exactly as before |
| `IBlobStorageProvider` interface | Unchanged — no new members |
| `BlobStorageAttribute.LazyLoad` | Still defaults to `false` |
| Existing compiled entities | Recompile without modification against the updated library |

No migration steps are required. Existing entities that use `[BlobStorage]` without a `Provider` property continue to work unchanged after upgrading.

## See Also

- [Configuration Guide](Configuration.md) — General `FluentDynamoDbOptions` configuration
- [Entity Definition](EntityDefinition.md) — Defining entities with attributes
