# Advanced Types Examples

This document provides practical examples for using advanced types in Oproto.FluentDynamoDb.

## Table of Contents

- [Map Examples](#map-examples)
- [Set Examples](#set-examples)
- [List Examples](#list-examples)
- [TTL Examples](#ttl-examples)
- [JSON Blob Examples](#json-blob-examples)
- [Blob Reference Examples](#blob-reference-examples)
- [Combined Examples](#combined-examples)

## Map Examples

### Simple String Dictionary

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; }
    
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; }
}

// Create and save
var product = new Product
{
    ProductId = "prod-001",
    Metadata = new Dictionary<string, string>
    {
        ["color"] = "blue",
        ["size"] = "large",
        ["material"] = "cotton",
        ["origin"] = "USA"
    }
};

await productTable.Put
    .WithItem(product)
    .PutAsync();

// Query and update
var loaded = await productTable.Get<Product>()
    .WithKey("pk", "prod-001")
    .GetItemAsync();

loaded.Item.Metadata["color"] = "red";
loaded.Item.Metadata["updated"] = DateTime.UtcNow.ToString("O");

await productTable.Put
    .WithItem(loaded.Item)
    .PutAsync();
```

### Nested Object Map

```csharp
// Define nested type - MUST have [DynamoDbEntity]
[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("street")]
    public string Street { get; set; }
    
    [DynamoDbAttribute("city")]
    public string City { get; set; }
    
    [DynamoDbAttribute("state")]
    public string State { get; set; }
    
    [DynamoDbAttribute("zip")]
    public string ZipCode { get; set; }
    
    [DynamoDbAttribute("coordinates")]
    public Dictionary<string, decimal> Coordinates { get; set; }
}

[DynamoDbTable("customers")]
public partial class Customer
{
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    [DynamoDbAttribute("address")]
    [DynamoDbMap]
    public Address ShippingAddress { get; set; }
}

// Usage
var customer = new Customer
{
    CustomerId = "cust-001",
    Name = "John Doe",
    ShippingAddress = new Address
    {
        Street = "123 Main St",
        City = "Springfield",
        State = "IL",
        ZipCode = "62701",
        Coordinates = new Dictionary<string, decimal>
        {
            ["latitude"] = 39.7817m,
            ["longitude"] = -89.6501m
        }
    }
};

await customerTable.Put
    .WithItem(customer)
    .PutAsync();
```

### Complex Nested Maps

```csharp
[DynamoDbEntity]
public partial class ProductSpecifications
{
    [DynamoDbAttribute("dimensions")]
    public Dictionary<string, decimal> Dimensions { get; set; }
    
    [DynamoDbAttribute("features")]
    public HashSet<string> Features { get; set; }
    
    [DynamoDbAttribute("ratings")]
    public Dictionary<string, int> Ratings { get; set; }
}

[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; }
    
    [DynamoDbAttribute("specs")]
    [DynamoDbMap]
    public ProductSpecifications Specifications { get; set; }
}

// Usage
var product = new Product
{
    ProductId = "prod-001",
    Specifications = new ProductSpecifications
    {
        Dimensions = new Dictionary<string, decimal>
        {
            ["length"] = 10.5m,
            ["width"] = 8.0m,
            ["height"] = 3.2m,
            ["weight"] = 2.5m
        },
        Features = new HashSet<string>
        {
            "waterproof",
            "wireless",
            "rechargeable"
        },
        Ratings = new Dictionary<string, int>
        {
            ["quality"] = 5,
            ["value"] = 4,
            ["design"] = 5
        }
    }
};
```

## Set Examples

### String Set for Tags

```csharp
[DynamoDbTable("articles")]
public partial class Article
{
    [DynamoDbAttribute("pk")]
    public string ArticleId { get; set; }
    
    [DynamoDbAttribute("title")]
    public string Title { get; set; }
    
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
    
    [DynamoDbAttribute("categories")]
    public HashSet<string> Categories { get; set; }
}

// Create with tags
var article = new Article
{
    ArticleId = "article-001",
    Title = "Getting Started with DynamoDB",
    Tags = new HashSet<string>
    {
        "dynamodb",
        "aws",
        "nosql",
        "tutorial"
    },
    Categories = new HashSet<string>
    {
        "databases",
        "cloud"
    }
};

await articleTable.Put
    .WithItem(article)
    .PutAsync();

// Add tags using ADD operation
var newTags = new HashSet<string> { "beginner", "guide" };
await articleTable.Update
    .WithKey("pk", "article-001")
    .Set("ADD tags {0}", newTags)
    .UpdateAsync();

// Remove tags using DELETE operation
var removeTags = new HashSet<string> { "tutorial" };
await articleTable.Update
    .WithKey("pk", "article-001")
    .Set("DELETE tags {0}", removeTags)
    .UpdateAsync();

// Query articles with specific tag
await articleTable.Query<Article>()
    .Where("contains(tags, {0})", "dynamodb")
    .ToListAsync();
```

### Number Set for IDs

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; }
    
    [DynamoDbAttribute("follower_ids")]
    public HashSet<int> FollowerIds { get; set; }
    
    [DynamoDbAttribute("favorite_product_ids")]
    public HashSet<int> FavoriteProductIds { get; set; }
}

// Usage
var user = new User
{
    UserId = "user-001",
    FollowerIds = new HashSet<int> { 101, 102, 103, 104 },
    FavoriteProductIds = new HashSet<int> { 501, 502, 503 }
};

// Add follower
await userTable.Update
    .WithKey("pk", "user-001")
    .Set("ADD follower_ids {0}", new HashSet<int> { 105 })
    .UpdateAsync();

// Remove follower
await userTable.Update
    .WithKey("pk", "user-001")
    .Set("DELETE follower_ids {0}", new HashSet<int> { 102 })
    .UpdateAsync();
```

### Binary Set for Checksums

```csharp
[DynamoDbTable("files")]
public partial class FileRecord
{
    [DynamoDbAttribute("pk")]
    public string FileId { get; set; }
    
    [DynamoDbAttribute("checksums")]
    public HashSet<byte[]> Checksums { get; set; }
}

// Usage
var file = new FileRecord
{
    FileId = "file-001",
    Checksums = new HashSet<byte[]>
    {
        SHA256.HashData(Encoding.UTF8.GetBytes("content1")),
        SHA256.HashData(Encoding.UTF8.GetBytes("content2")),
        MD5.HashData(Encoding.UTF8.GetBytes("content1"))
    }
};

await fileTable.Put
    .WithItem(file)
    .PutAsync();
```

## List Examples

### Ordered Item Lists

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; }
    
    [DynamoDbAttribute("item_ids")]
    public List<string> ItemIds { get; set; }
    
    [DynamoDbAttribute("quantities")]
    public List<int> Quantities { get; set; }
    
    [DynamoDbAttribute("prices")]
    public List<decimal> Prices { get; set; }
}

// Create order
var order = new Order
{
    OrderId = "order-001",
    ItemIds = new List<string> { "item-1", "item-2", "item-3" },
    Quantities = new List<int> { 2, 1, 3 },
    Prices = new List<decimal> { 19.99m, 24.99m, 9.99m }
};

await orderTable.Put
    .WithItem(order)
    .PutAsync();

// Calculate total
var loaded = await orderTable.Get<Order>()
    .WithKey("pk", "order-001")
    .GetItemAsync();

decimal total = 0;
for (int i = 0; i < loaded.Item.ItemIds.Count; i++)
{
    total += loaded.Item.Quantities[i] * loaded.Item.Prices[i];
}
Console.WriteLine($"Order total: ${total}");
```

### Event History

```csharp
[DynamoDbEntity]
public partial class Event
{
    [DynamoDbAttribute("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [DynamoDbAttribute("type")]
    public string Type { get; set; }
    
    [DynamoDbAttribute("description")]
    public string Description { get; set; }
}

[DynamoDbTable("workflows")]
public partial class Workflow
{
    [DynamoDbAttribute("pk")]
    public string WorkflowId { get; set; }
    
    [DynamoDbAttribute("events")]
    public List<Dictionary<string, AttributeValue>> Events { get; set; }
}

// Add events maintaining order
var workflow = new Workflow
{
    WorkflowId = "wf-001",
    Events = new List<Dictionary<string, AttributeValue>>
    {
        new Dictionary<string, AttributeValue>
        {
            ["timestamp"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") },
            ["type"] = new AttributeValue { S = "started" },
            ["description"] = new AttributeValue { S = "Workflow initiated" }
        },
        new Dictionary<string, AttributeValue>
        {
            ["timestamp"] = new AttributeValue { S = DateTime.UtcNow.AddMinutes(5).ToString("O") },
            ["type"] = new AttributeValue { S = "processing" },
            ["description"] = new AttributeValue { S = "Processing step 1" }
        }
    }
};
```

## TTL Examples

### Session Management

```csharp
[DynamoDbTable("sessions")]
public partial class Session
{
    [DynamoDbAttribute("session_id")]
    public string SessionId { get; set; }
    
    [DynamoDbAttribute("user_id")]
    public string UserId { get; set; }
    
    [DynamoDbAttribute("data")]
    public Dictionary<string, string> Data { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

// Create session that expires in 1 hour
var session = new Session
{
    SessionId = Guid.NewGuid().ToString(),
    UserId = "user-001",
    Data = new Dictionary<string, string>
    {
        ["ip"] = "192.168.1.1",
        ["user_agent"] = "Mozilla/5.0..."
    },
    ExpiresAt = DateTime.UtcNow.AddHours(1)
};

await sessionTable.Put
    .WithItem(session)
    .PutAsync();

// Extend session
await sessionTable.Update
    .WithKey("session_id", session.SessionId)
    .Set("SET ttl = {0}", DateTime.UtcNow.AddHours(2))
    .UpdateAsync();
```

### Temporary Data Storage

```csharp
[DynamoDbTable("temp_data")]
public partial class TempData
{
    [DynamoDbAttribute("pk")]
    public string DataId { get; set; }
    
    [DynamoDbAttribute("content")]
    public string Content { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTimeOffset? ExpiresAt { get; set; }
}

// Store data that expires in 24 hours
var tempData = new TempData
{
    DataId = "temp-001",
    Content = "Temporary content",
    ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
};

await tempTable.Put
    .WithItem(tempData)
    .PutAsync();
```

### Cache with Expiration

```csharp
[DynamoDbTable("cache")]
public partial class CacheEntry
{
    [DynamoDbAttribute("cache_key")]
    public string CacheKey { get; set; }
    
    [DynamoDbAttribute("value")]
    [JsonBlob]
    public object Value { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

// Cache with 5-minute expiration
var cacheEntry = new CacheEntry
{
    CacheKey = "user:001:profile",
    Value = new { Name = "John", Email = "john@example.com" },
    ExpiresAt = DateTime.UtcNow.AddMinutes(5)
};
```

## JSON Blob Examples

### Complex Object Serialization

```csharp
// Install: dotnet add package Oproto.FluentDynamoDb.SystemTextJson
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

public class OrderDetails
{
    public string CustomerName { get; set; }
    public Address ShippingAddress { get; set; }
    public List<OrderItem> Items { get; set; }
    public PaymentInfo Payment { get; set; }
}

public class OrderItem
{
    public string ProductId { get; set; }
    public string Name { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

[DynamoDbTable("orders")]
public partial class Order
{
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; }
    
    [DynamoDbAttribute("details")]
    [JsonBlob]
    public OrderDetails Details { get; set; }
}

// Usage
var order = new Order
{
    OrderId = "order-001",
    Details = new OrderDetails
    {
        CustomerName = "John Doe",
        ShippingAddress = new Address
        {
            Street = "123 Main St",
            City = "Springfield"
        },
        Items = new List<OrderItem>
        {
            new OrderItem
            {
                ProductId = "prod-1",
                Name = "Widget",
                Quantity = 2,
                Price = 19.99m
            }
        },
        Payment = new PaymentInfo
        {
            Method = "credit_card",
            Last4 = "1234"
        }
    }
};

await orderTable.Put
    .WithItem(order)
    .PutAsync();
```

### Configuration Storage

```csharp
public class AppConfiguration
{
    public Dictionary<string, string> Settings { get; set; }
    public List<string> EnabledFeatures { get; set; }
    public Dictionary<string, int> Limits { get; set; }
}

[DynamoDbTable("configurations")]
public partial class Configuration
{
    [DynamoDbAttribute("config_id")]
    public string ConfigId { get; set; }
    
    [DynamoDbAttribute("version")]
    public int Version { get; set; }
    
    [DynamoDbAttribute("config")]
    [JsonBlob]
    public AppConfiguration Config { get; set; }
}

// Store configuration
var config = new Configuration
{
    ConfigId = "app-config",
    Version = 1,
    Config = new AppConfiguration
    {
        Settings = new Dictionary<string, string>
        {
            ["api_url"] = "https://api.example.com",
            ["timeout"] = "30"
        },
        EnabledFeatures = new List<string>
        {
            "feature_a",
            "feature_b"
        },
        Limits = new Dictionary<string, int>
        {
            ["max_requests"] = 1000,
            ["max_size"] = 10485760
        }
    }
};
```

## Blob Reference Examples

### File Storage with S3

```csharp
// Install: dotnet add package Oproto.FluentDynamoDb.BlobStorage.S3

[DynamoDbTable("documents")]
public partial class Document
{
    [DynamoDbAttribute("doc_id")]
    public string DocumentId { get; set; }
    
    [DynamoDbAttribute("title")]
    public string Title { get; set; }
    
    [DynamoDbAttribute("content_ref")]
    [BlobReference(BlobProvider.S3, BucketName = "my-documents", KeyPrefix = "docs")]
    public byte[] Content { get; set; }
    
    [DynamoDbAttribute("size")]
    public long Size { get; set; }
}

// Setup
var s3Client = new AmazonS3Client();
var blobProvider = new S3BlobProvider(s3Client, "my-documents", "docs");

// Save document
var document = new Document
{
    DocumentId = "doc-001",
    Title = "Important Document",
    Content = File.ReadAllBytes("document.pdf"),
    Size = new FileInfo("document.pdf").Length
};

var item = await Document.ToDynamoDbAsync(document, blobProvider);
await dynamoDbClient.PutItemAsync(new PutItemRequest
{
    TableName = "documents",
    Item = item
});

// Load document
var response = await dynamoDbClient.GetItemAsync(new GetItemRequest
{
    TableName = "documents",
    Key = new Dictionary<string, AttributeValue>
    {
        ["doc_id"] = new AttributeValue { S = "doc-001" }
    }
});

var loaded = await Document.FromDynamoDbAsync<Document>(response.Item, blobProvider);
File.WriteAllBytes("downloaded.pdf", loaded.Content);
```

### Image Storage

```csharp
[DynamoDbTable("images")]
public partial class Image
{
    [DynamoDbAttribute("image_id")]
    public string ImageId { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    [DynamoDbAttribute("data_ref")]
    [BlobReference(BlobProvider.S3, BucketName = "my-images", KeyPrefix = "uploads")]
    public byte[] Data { get; set; }
    
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; }
}

// Upload image
var image = new Image
{
    ImageId = Guid.NewGuid().ToString(),
    Name = "profile.jpg",
    Data = File.ReadAllBytes("profile.jpg"),
    Metadata = new Dictionary<string, string>
    {
        ["content_type"] = "image/jpeg",
        ["width"] = "800",
        ["height"] = "600"
    }
};
```

## Combined Examples

### Large JSON Object in S3

```csharp
public class DetailedReport
{
    public string Title { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<ReportSection> Sections { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

[DynamoDbTable("reports")]
public partial class Report
{
    [DynamoDbAttribute("report_id")]
    public string ReportId { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    [DynamoDbAttribute("data_ref")]
    [JsonBlob]
    [BlobReference(BlobProvider.S3, BucketName = "reports", KeyPrefix = "data")]
    public DetailedReport Data { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

// Create report that expires in 30 days
var report = new Report
{
    ReportId = "report-001",
    Name = "Monthly Sales Report",
    Data = new DetailedReport
    {
        Title = "Sales Report - January 2024",
        GeneratedAt = DateTime.UtcNow,
        Sections = GenerateReportSections(),
        Metadata = new Dictionary<string, object>
        {
            ["total_sales"] = 125000.50m,
            ["transaction_count"] = 1543
        }
    },
    ExpiresAt = DateTime.UtcNow.AddDays(30)
};

// The library will:
// 1. Serialize Data to JSON
// 2. Store JSON in S3
// 3. Store S3 reference in DynamoDB
// 4. Set TTL for automatic cleanup
```

### E-commerce Product with All Features

```csharp
[DynamoDbEntity]
public partial class ProductDetails
{
    [DynamoDbAttribute("description")]
    public string Description { get; set; }
    
    [DynamoDbAttribute("specifications")]
    public Dictionary<string, string> Specifications { get; set; }
}

[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    // Set for tags
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
    
    // Map for metadata
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; }
    
    // List for related products
    [DynamoDbAttribute("related_ids")]
    public List<string> RelatedProductIds { get; set; }
    
    // Nested map
    [DynamoDbAttribute("details")]
    [DynamoDbMap]
    public ProductDetails Details { get; set; }
    
    // JSON blob for complex data
    [DynamoDbAttribute("reviews")]
    [JsonBlob]
    public List<Review> Reviews { get; set; }
    
    // Blob reference for images
    [DynamoDbAttribute("image_ref")]
    [BlobReference(BlobProvider.S3, BucketName = "product-images")]
    public byte[] MainImage { get; set; }
    
    // TTL for temporary products
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

// Complete example
var product = new Product
{
    ProductId = "prod-001",
    Name = "Premium Widget",
    Tags = new HashSet<string> { "electronics", "featured", "sale" },
    Metadata = new Dictionary<string, string>
    {
        ["color"] = "blue",
        ["size"] = "large"
    },
    RelatedProductIds = new List<string> { "prod-002", "prod-003" },
    Details = new ProductDetails
    {
        Description = "High-quality widget",
        Specifications = new Dictionary<string, string>
        {
            ["weight"] = "2.5 lbs",
            ["dimensions"] = "10x8x3 inches"
        }
    },
    Reviews = new List<Review>
    {
        new Review { Rating = 5, Comment = "Excellent!" },
        new Review { Rating = 4, Comment = "Good value" }
    },
    MainImage = File.ReadAllBytes("product-image.jpg"),
    ExpiresAt = DateTime.UtcNow.AddDays(90) // Temporary listing
};
```

## See Also

- [Advanced Types Guide](../advanced-topics/AdvancedTypes.md)
- [Entity Definition](../core-features/EntityDefinition.md)
- [Expression Formatting](../core-features/ExpressionFormatting.md)
