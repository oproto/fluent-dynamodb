# Advanced Types Migration Guide

This guide helps you migrate existing entities to use the advanced type system features in Oproto.FluentDynamoDb.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Migration Strategies](#migration-strategies)
- [Step-by-Step Migration](#step-by-step-migration)
- [Handling Existing Data](#handling-existing-data)
- [Common Migration Scenarios](#common-migration-scenarios)
- [Rollback Strategies](#rollback-strategies)
- [Testing Your Migration](#testing-your-migration)

## Prerequisites

### Package Requirements

Before migrating, ensure you have the required packages:

```xml
<ItemGroup>
  <!-- Core library with advanced type support -->
  <PackageReference Include="Oproto.FluentDynamoDb" Version="0.3.0" />
  
  <!-- Attributes package (required for advanced types) -->
  <PackageReference Include="Oproto.FluentDynamoDb.Attributes" Version="0.3.0" />
  
  <!-- Optional: JSON serialization -->
  <PackageReference Include="Oproto.FluentDynamoDb.SystemTextJson" Version="0.3.0" />
  <!-- OR -->
  <PackageReference Include="Oproto.FluentDynamoDb.NewtonsoftJson" Version="0.3.0" />
  
  <!-- Optional: S3 blob storage -->
  <PackageReference Include="Oproto.FluentDynamoDb.BlobStorage.S3" Version="0.3.0" />
</ItemGroup>
```

### Understanding Backward Compatibility

Advanced types are **backward compatible** with existing data:
- New attributes are optional - existing items without them will load with null values
- Existing code continues to work unchanged
- You can migrate incrementally, one entity at a time

## Migration Strategies

### Strategy 1: Additive Migration (Recommended)

Add new advanced type properties without removing existing ones. This allows gradual migration with zero downtime.

**Pros:**
- Zero downtime
- Easy rollback
- Can test in production safely

**Cons:**
- Temporary data duplication
- Requires cleanup phase

### Strategy 2: In-Place Migration

Replace existing properties with advanced type equivalents. Requires data migration.

**Pros:**
- Clean final state
- No duplication

**Cons:**
- Requires careful planning
- May need downtime
- More complex rollback

### Strategy 3: Dual-Write Migration

Write to both old and new formats during transition period.

**Pros:**
- Safe migration
- Easy rollback

**Cons:**
- More complex code
- Higher write costs

## Step-by-Step Migration

### Phase 1: Add Advanced Type Properties

#### Example: Adding Tags as a Set

```csharp
// Before
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    // Old: Comma-separated string
    [DynamoDbAttribute("tags")]
    public string TagsString { get; set; }
}

// After: Add new property, keep old one temporarily
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    // Old: Keep for backward compatibility
    [DynamoDbAttribute("tags")]
    public string TagsString { get; set; }
    
    // New: Advanced type
    [DynamoDbAttribute("tags_set")]
    public HashSet<string> Tags { get; set; }
}
```

### Phase 2: Implement Dual-Write Logic

```csharp
public class ProductService
{
    private readonly DynamoDbTableBase<Product> _table;
    
    public async Task SaveProductAsync(Product product)
    {
        // Write to both formats during migration
        if (product.Tags != null && product.Tags.Any())
        {
            // New format
            product.Tags = product.Tags;
            
            // Old format for backward compatibility
            product.TagsString = string.Join(",", product.Tags);
        }
        
        await _table.Put
            .WithItem(product)
            .ExecuteAsync();
    }
    
    public async Task<Product> GetProductAsync(string productId)
    {
        var result = await _table.Get
            .WithKey("pk", productId)
            .ExecuteAsync<Product>();
        
        var product = result.Item;
        
        // Migrate on read if needed
        if (product.Tags == null && !string.IsNullOrEmpty(product.TagsString))
        {
            product.Tags = new HashSet<string>(
                product.TagsString.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }
        
        return product;
    }
}
```

### Phase 3: Backfill Existing Data

```csharp
public class ProductMigrationService
{
    private readonly DynamoDbTableBase<Product> _table;
    
    public async Task MigrateAllProductsAsync()
    {
        var scanRequest = _table.Scan;
        var hasMore = true;
        Dictionary<string, AttributeValue> lastKey = null;
        int migratedCount = 0;
        
        while (hasMore)
        {
            var response = await scanRequest
                .WithExclusiveStartKey(lastKey)
                .Take(100)
                .ExecuteAsync<Product>();
            
            foreach (var product in response.Items)
            {
                if (await MigrateProductAsync(product))
                {
                    migratedCount++;
                }
            }
            
            lastKey = response.LastEvaluatedKey;
            hasMore = lastKey != null && lastKey.Count > 0;
            
            Console.WriteLine($"Migrated {migratedCount} products...");
        }
        
        Console.WriteLine($"Migration complete. Total: {migratedCount} products");
    }
    
    private async Task<bool> MigrateProductAsync(Product product)
    {
        // Skip if already migrated
        if (product.Tags != null && product.Tags.Any())
        {
            return false;
        }
        
        // Skip if no data to migrate
        if (string.IsNullOrEmpty(product.TagsString))
        {
            return false;
        }
        
        // Migrate tags
        product.Tags = new HashSet<string>(
            product.TagsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()));
        
        // Save migrated product
        await _table.Put
            .WithItem(product)
            .ExecuteAsync();
        
        return true;
    }
}
```

### Phase 4: Update Application Code

```csharp
// Before: Using old string format
var products = await _table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "PRODUCT")
    .ExecuteAsync<Product>();

foreach (var product in products.Items)
{
    var tags = product.TagsString?.Split(',') ?? Array.Empty<string>();
    if (tags.Contains("sale"))
    {
        // Process sale items
    }
}

// After: Using new set format
var products = await _table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "PRODUCT")
    .ExecuteAsync<Product>();

foreach (var product in products.Items)
{
    if (product.Tags?.Contains("sale") == true)
    {
        // Process sale items
    }
}

// Or use DynamoDB set operations
var saleProducts = await _table.Query
    .Where("pk = :pk AND contains(tags_set, :tag)")
    .WithValue(":pk", "PRODUCT")
    .WithValue(":tag", "sale")
    .ExecuteAsync<Product>();
```

### Phase 5: Remove Old Properties

After all data is migrated and application code updated:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    // Old property removed
    // [DynamoDbAttribute("tags")]
    // public string TagsString { get; set; }
    
    // Rename attribute to use original name
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
}
```

**Note**: If you want to reuse the original attribute name, you'll need to:
1. Remove the old attribute from all items
2. Update the entity definition
3. Redeploy

## Handling Existing Data

### Null Handling

Existing items without new attributes will load with null values:

```csharp
var product = await _table.Get
    .WithKey("pk", "old-product")
    .ExecuteAsync<Product>();

// For old items, Tags will be null
if (product.Item.Tags == null)
{
    product.Item.Tags = new HashSet<string>();
}

// Or use null-coalescing
var tags = product.Item.Tags ?? new HashSet<string>();
```

### Default Values

Provide defaults for missing attributes:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    private HashSet<string> _tags;
    
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags
    {
        get => _tags ??= new HashSet<string>();
        set => _tags = value;
    }
}
```

### Conditional Updates

Only update if the new attribute doesn't exist:

```csharp
await _table.Update
    .WithKey("pk", productId)
    .Set("SET tags = {0}", newTags)
    .WithConditionExpression("attribute_not_exists(tags)")
    .ExecuteAsync();
```

## Common Migration Scenarios

### Scenario 1: String to Set Migration

```csharp
// Before: Comma-separated string
[DynamoDbAttribute("categories")]
public string Categories { get; set; }

// After: String set
[DynamoDbAttribute("categories")]
public HashSet<string> Categories { get; set; }

// Migration code
public void MigrateCategories(Product product)
{
    if (product.Categories == null && !string.IsNullOrEmpty(product.CategoriesOld))
    {
        product.Categories = new HashSet<string>(
            product.CategoriesOld.Split(',')
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c)));
    }
}
```

### Scenario 2: JSON String to Map Migration

```csharp
// Before: JSON string
[DynamoDbAttribute("metadata")]
public string MetadataJson { get; set; }

// After: Dictionary
[DynamoDbAttribute("metadata")]
public Dictionary<string, string> Metadata { get; set; }

// Migration code
public void MigrateMetadata(Product product)
{
    if (product.Metadata == null && !string.IsNullOrEmpty(product.MetadataJson))
    {
        product.Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(
            product.MetadataJson);
    }
}
```

### Scenario 3: Adding TTL to Existing Table

```csharp
// Before: No expiration
[DynamoDbTable("sessions")]
public partial class Session
{
    [DynamoDbAttribute("session_id")]
    public string SessionId { get; set; }
}

// After: With TTL
[DynamoDbTable("sessions")]
public partial class Session
{
    [DynamoDbAttribute("session_id")]
    public string SessionId { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

// Migration: Add TTL to existing sessions
public async Task AddTtlToExistingSessions(TimeSpan sessionDuration)
{
    var sessions = await _table.Scan<Session>().ExecuteAsync();
    
    foreach (var session in sessions.Items)
    {
        if (session.ExpiresAt == null)
        {
            await _table.Update<Session>()
                .WithKey("session_id", session.SessionId)
                .Set("SET ttl = {0}", DateTime.UtcNow.Add(sessionDuration))
                .ExecuteAsync();
        }
    }
}

// Don't forget to enable TTL on the table
// aws dynamodb update-time-to-live \
//     --table-name sessions \
//     --time-to-live-specification "Enabled=true, AttributeName=ttl"
```

### Scenario 4: Moving Large Data to S3

```csharp
// Before: Large data in DynamoDB
[DynamoDbAttribute("content")]
public string Content { get; set; }

// After: Reference to S3
[DynamoDbAttribute("content_ref")]
[BlobReference(BlobProvider.S3, BucketName = "my-content")]
public byte[] Content { get; set; }

// Migration code
public async Task MigrateToS3(Document document, IBlobStorageProvider blobProvider)
{
    if (document.Content != null && !string.IsNullOrEmpty(document.ContentOld))
    {
        // Convert string to bytes
        var bytes = Encoding.UTF8.GetBytes(document.ContentOld);
        document.Content = bytes;
        
        // Save will automatically upload to S3
        var item = await Document.ToDynamoDbAsync(document, blobProvider);
        await _dynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = "documents",
            Item = item
        });
        
        // Optionally remove old attribute
        await _table.Update
            .WithKey("doc_id", document.DocumentId)
            .Remove("REMOVE content_old")
            .ExecuteAsync();
    }
}
```

## Rollback Strategies

### Strategy 1: Keep Old Attributes

The safest approach - keep old attributes during migration:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    // Keep both during migration period
    [DynamoDbAttribute("tags_old")]
    public string TagsString { get; set; }
    
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
}

// If rollback needed, just deploy old code version
// Old code will ignore the new 'tags' attribute
```

### Strategy 2: Feature Flags

Use feature flags to control migration:

```csharp
public class ProductService
{
    private readonly IFeatureFlags _featureFlags;
    
    public async Task<Product> GetProductAsync(string id)
    {
        var product = await _table.Get
            .WithKey("pk", id)
            .ExecuteAsync<Product>();
        
        if (_featureFlags.IsEnabled("UseAdvancedTypes"))
        {
            return product.Item; // Use new Tags property
        }
        else
        {
            // Fallback to old format
            product.Item.TagsString = string.Join(",", product.Item.Tags ?? new HashSet<string>());
            return product.Item;
        }
    }
}
```

### Strategy 3: Versioned Entities

Maintain multiple entity versions:

```csharp
// V1 entity
[DynamoDbTable("products")]
public partial class ProductV1
{
    [DynamoDbAttribute("tags")]
    public string TagsString { get; set; }
}

// V2 entity
[DynamoDbTable("products")]
public partial class ProductV2
{
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
}

// Service layer handles version
public class ProductService
{
    public async Task<IProduct> GetProductAsync(string id, int version = 2)
    {
        if (version == 1)
        {
            return await _tableV1.Get.WithKey("pk", id).ExecuteAsync<ProductV1>();
        }
        else
        {
            return await _tableV2.Get.WithKey("pk", id).ExecuteAsync<ProductV2>();
        }
    }
}
```

## Testing Your Migration

### Unit Tests

```csharp
[Fact]
public void Migration_ConvertsStringToSet_Correctly()
{
    // Arrange
    var product = new Product
    {
        ProductId = "test-1",
        TagsString = "tag1,tag2,tag3"
    };
    
    // Act
    MigrateProduct(product);
    
    // Assert
    product.Tags.Should().HaveCount(3);
    product.Tags.Should().Contain("tag1");
    product.Tags.Should().Contain("tag2");
    product.Tags.Should().Contain("tag3");
}

[Fact]
public void Migration_HandlesEmptyString_Correctly()
{
    // Arrange
    var product = new Product
    {
        ProductId = "test-1",
        TagsString = ""
    };
    
    // Act
    MigrateProduct(product);
    
    // Assert
    product.Tags.Should().BeNullOrEmpty();
}
```

### Integration Tests

```csharp
[Fact]
public async Task Migration_RoundTrip_PreservesData()
{
    // Arrange - Create old format item
    var oldProduct = new Product
    {
        ProductId = "test-1",
        TagsString = "tag1,tag2"
    };
    
    await _table.Put<Product>().WithItem(oldProduct).ExecuteAsync();
    
    // Act - Migrate
    var loaded = await _table.Get<Product>()
        .WithKey("pk", "test-1")
        .ExecuteAsync();
    
    MigrateProduct(loaded.Item);
    
    await _table.Put<Product>().WithItem(loaded.Item).ExecuteAsync();
    
    // Assert - Verify new format
    var migrated = await _table.Get<Product>()
        .WithKey("pk", "test-1")
        .ExecuteAsync();
    
    migrated.Item.Tags.Should().HaveCount(2);
    migrated.Item.Tags.Should().Contain("tag1");
}
```

### Load Testing

```csharp
public async Task LoadTestMigration()
{
    var stopwatch = Stopwatch.StartNew();
    var tasks = new List<Task>();
    
    for (int i = 0; i < 1000; i++)
    {
        tasks.Add(MigrateProductAsync($"prod-{i}"));
    }
    
    await Task.WhenAll(tasks);
    
    stopwatch.Stop();
    Console.WriteLine($"Migrated 1000 products in {stopwatch.ElapsedMilliseconds}ms");
}
```

## Best Practices

1. **Test in Non-Production First**: Always test migration in dev/staging environments

2. **Monitor During Migration**: Watch CloudWatch metrics for errors and throttling

3. **Migrate in Batches**: Don't try to migrate all data at once
   ```csharp
   // Process in batches with delays
   for (int batch = 0; batch < totalBatches; batch++)
   {
       await MigrateBatchAsync(batch, batchSize);
       await Task.Delay(TimeSpan.FromSeconds(1)); // Rate limiting
   }
   ```

4. **Keep Audit Trail**: Log all migrations
   ```csharp
   _logger.LogInformation(
       "Migrated product {ProductId} from {OldFormat} to {NewFormat}",
       product.ProductId,
       "string",
       "HashSet<string>");
   ```

5. **Plan for Rollback**: Always have a rollback plan before starting

6. **Communicate Changes**: Inform team members about migration timeline

## See Also

- [Advanced Types Guide](../advanced-topics/AdvancedTypes.md)
- [Advanced Types Examples](../examples/AdvancedTypesExamples.md)
- [Attribute Reference](./AttributeReference.md)
