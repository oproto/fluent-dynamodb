# DateTime Kind Guide

## Overview

The `DateTimeKind` parameter in `DynamoDbAttribute` controls how DateTime values are handled during serialization and deserialization. This ensures consistent timezone handling across all DynamoDB operations and prevents common timezone-related bugs.

## Understanding DateTimeKind

.NET's `DateTime` type includes a `Kind` property that indicates whether the time is:

- **Unspecified**: No timezone information (default)
- **Utc**: Coordinated Universal Time
- **Local**: Local time based on the system's timezone

Without explicit handling, DateTime values can lose their timezone information when stored in DynamoDB, leading to incorrect time calculations and display issues.

## When to Use Each DateTimeKind

### DateTimeKind.Utc (Recommended)

**Use for:**
- Timestamps and audit trails
- Event times that need to be consistent across timezones
- API responses and requests
- Any time-sensitive data shared across regions
- Scheduled tasks and cron jobs

**Benefits:**
- Consistent across all servers and regions
- No ambiguity during daylight saving time transitions
- Standard practice for distributed systems
- Easy to convert to any local timezone for display

**Example:**
```csharp
public class AuditLog
{
    [DynamoDbAttribute("created_at", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("modified_at", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime ModifiedAt { get; set; }
}

// Usage
var log = new AuditLog
{
    CreatedAt = DateTime.UtcNow,  // Always use UtcNow for UTC timestamps
    ModifiedAt = DateTime.UtcNow
};
```

### DateTimeKind.Local

**Use for:**
- User-facing display times that should reflect local timezone
- Scheduled events tied to a specific timezone
- Business hours or operating schedules

**Cautions:**
- Local time depends on the server's timezone configuration
- Can cause issues in distributed systems with servers in different timezones
- Ambiguous during daylight saving time transitions
- Not recommended for most scenarios

**Example:**
```csharp
public class BusinessHours
{
    [DynamoDbAttribute("opening_time", DateTimeKind = DateTimeKind.Local)]
    public DateTime OpeningTime { get; set; }
    
    [DynamoDbAttribute("closing_time", DateTimeKind = DateTimeKind.Local)]
    public DateTime ClosingTime { get; set; }
}

// Usage - be aware of server timezone
var hours = new BusinessHours
{
    OpeningTime = new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Local),
    ClosingTime = new DateTime(2024, 3, 15, 17, 0, 0, DateTimeKind.Local)
};
```

### DateTimeKind.Unspecified (Default)

**Use for:**
- Date-only values where time is not relevant
- Times where timezone is managed separately
- Legacy code migration where timezone handling is done manually
- Relative times (e.g., "2 hours from now")

**Cautions:**
- No automatic timezone conversion
- Requires manual timezone management
- Can lead to confusion about what timezone the value represents

**Example:**
```csharp
public class Event
{
    // Date only - timezone not relevant
    [DynamoDbAttribute("event_date", DateTimeKind = DateTimeKind.Unspecified, Format = "yyyy-MM-dd")]
    public DateTime EventDate { get; set; }
    
    // Duration - relative time
    [DynamoDbAttribute("duration")]
    public TimeSpan Duration { get; set; }
}

// Usage
var evt = new Event
{
    EventDate = new DateTime(2024, 3, 15),  // Date only
    Duration = TimeSpan.FromHours(2)
};
```

## How DateTime Kind Works

### Serialization (ToDynamoDb)

When storing a DateTime with a specified Kind:

```csharp
[DynamoDbAttribute("timestamp", DateTimeKind = DateTimeKind.Utc, Format = "o")]
public DateTime Timestamp { get; set; }
```

**Generated code:**
```csharp
// If DateTimeKind is Utc
var timestampValue = entity.Timestamp.ToUniversalTime();
var timestampFormatted = timestampValue.ToString("o", CultureInfo.InvariantCulture);
item["timestamp"] = new AttributeValue { S = timestampFormatted };

// If DateTimeKind is Local
var timestampValue = entity.Timestamp.ToLocalTime();
var timestampFormatted = timestampValue.ToString("o", CultureInfo.InvariantCulture);
item["timestamp"] = new AttributeValue { S = timestampFormatted };

// If DateTimeKind is Unspecified (or not specified)
var timestampFormatted = entity.Timestamp.ToString("o", CultureInfo.InvariantCulture);
item["timestamp"] = new AttributeValue { S = timestampFormatted };
```

### Deserialization (FromDynamoDb)

When retrieving a DateTime with a specified Kind:

```csharp
// If DateTimeKind is Utc
if (DateTime.TryParse(item["timestamp"].S, out var parsed))
{
    entity.Timestamp = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
}

// If DateTimeKind is Local
if (DateTime.TryParse(item["timestamp"].S, out var parsed))
{
    entity.Timestamp = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
}

// If DateTimeKind is Unspecified
if (DateTime.TryParse(item["timestamp"].S, out var parsed))
{
    entity.Timestamp = parsed;  // Kind remains as parsed
}
```

## Best Practices

### 1. Always Use UTC for Timestamps

```csharp
public class Order
{
    [DynamoDbAttribute("created_at", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("shipped_at", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime? ShippedAt { get; set; }
}

// Always use UtcNow
var order = new Order
{
    CreatedAt = DateTime.UtcNow,
    ShippedAt = null
};
```

### 2. Convert to Local Time for Display Only

```csharp
// Store in UTC
[DynamoDbAttribute("created_at", DateTimeKind = DateTimeKind.Utc)]
public DateTime CreatedAt { get; set; }

// Convert to local for display
public string CreatedAtLocal => CreatedAt.ToLocalTime().ToString("g");
```

### 3. Use Date-Only Format with Unspecified Kind

```csharp
public class Appointment
{
    // Date only - no timezone needed
    [DynamoDbAttribute("appointment_date", DateTimeKind = DateTimeKind.Unspecified, Format = "yyyy-MM-dd")]
    public DateTime AppointmentDate { get; set; }
}
```

### 4. Document Timezone Assumptions

```csharp
public class Schedule
{
    /// <summary>
    /// Start time in UTC. Convert to local timezone for display.
    /// </summary>
    [DynamoDbAttribute("start_time", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime StartTime { get; set; }
}
```

## Common Scenarios

### Scenario 1: User Registration Timestamp

```csharp
public class User
{
    [PartitionKey]
    [DynamoDbAttribute("user_id")]
    public string UserId { get; set; }
    
    // Store registration time in UTC
    [DynamoDbAttribute("registered_at", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime RegisteredAt { get; set; }
    
    // Store last login in UTC
    [DynamoDbAttribute("last_login", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime? LastLogin { get; set; }
}

// Usage
var user = new User
{
    UserId = Guid.NewGuid().ToString(),
    RegisteredAt = DateTime.UtcNow,
    LastLogin = null
};

await table.PutItem(user).ExecuteAsync();
```

### Scenario 2: Scheduled Event with Timezone

```csharp
public class ScheduledEvent
{
    [PartitionKey]
    [DynamoDbAttribute("event_id")]
    public string EventId { get; set; }
    
    // Store in UTC for consistency
    [DynamoDbAttribute("scheduled_time", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime ScheduledTime { get; set; }
    
    // Store timezone separately for display
    [DynamoDbAttribute("timezone")]
    public string TimeZone { get; set; }
}

// Usage
var evt = new ScheduledEvent
{
    EventId = Guid.NewGuid().ToString(),
    ScheduledTime = DateTime.UtcNow.AddDays(7),
    TimeZone = "America/New_York"
};

// Display in user's timezone
var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(evt.TimeZone);
var localTime = TimeZoneInfo.ConvertTimeFromUtc(evt.ScheduledTime, userTimeZone);
```

### Scenario 3: Date-Only Field

```csharp
public class Invoice
{
    [PartitionKey]
    [DynamoDbAttribute("invoice_id")]
    public string InvoiceId { get; set; }
    
    // Date only - no time component needed
    [DynamoDbAttribute("invoice_date", DateTimeKind = DateTimeKind.Unspecified, Format = "yyyy-MM-dd")]
    public DateTime InvoiceDate { get; set; }
    
    // Due date - also date only
    [DynamoDbAttribute("due_date", DateTimeKind = DateTimeKind.Unspecified, Format = "yyyy-MM-dd")]
    public DateTime DueDate { get; set; }
}

// Usage
var invoice = new Invoice
{
    InvoiceId = "INV-001",
    InvoiceDate = new DateTime(2024, 3, 15),  // Date only
    DueDate = new DateTime(2024, 4, 15)       // Date only
};
```

## Migration from Existing Code

### Scenario: Adding DateTimeKind to Existing Properties

If you have existing DateTime properties without DateTimeKind specified:

**Before:**
```csharp
public class Order
{
    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }
}
```

**After:**
```csharp
public class Order
{
    [DynamoDbAttribute("created_at", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    public DateTime CreatedAt { get; set; }
}
```

**Migration Steps:**

1. **Assess Current Data**: Determine what timezone your existing DateTime values represent
2. **Add DateTimeKind**: Add the appropriate DateTimeKind to your attribute
3. **Test Thoroughly**: Verify that existing data is read correctly
4. **No Data Migration Needed**: The DateTimeKind only affects how new data is written and how the Kind property is set on read

**Important**: If your existing data is already in UTC but stored without timezone information, adding `DateTimeKind = DateTimeKind.Utc` will correctly set the Kind property on deserialization without requiring data migration.

### Handling Mixed Data

If you have mixed data (some UTC, some local, some unspecified):

```csharp
public class Order
{
    [DynamoDbAttribute("created_at", DateTimeKind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
}

// Custom deserialization to handle legacy data
public static Order FromDynamoDbWithMigration(Dictionary<string, AttributeValue> item)
{
    var order = OrderMapper.FromDynamoDb(item);
    
    // If CreatedAt doesn't have UTC kind, assume it's UTC and fix it
    if (order.CreatedAt.Kind != DateTimeKind.Utc)
    {
        order.CreatedAt = DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc);
    }
    
    return order;
}
```

## Troubleshooting

### Issue: DateTime.Kind is Unspecified After Retrieval

**Cause**: DateTimeKind not specified in DynamoDbAttribute

**Solution**: Add DateTimeKind parameter
```csharp
[DynamoDbAttribute("timestamp", DateTimeKind = DateTimeKind.Utc)]
public DateTime Timestamp { get; set; }
```

### Issue: Times Are Off by Several Hours

**Cause**: Mixing UTC and local times without proper conversion

**Solution**: 
1. Store all timestamps in UTC
2. Convert to local only for display
3. Use DateTimeKind.Utc consistently

```csharp
// Store in UTC
[DynamoDbAttribute("created_at", DateTimeKind = DateTimeKind.Utc)]
public DateTime CreatedAt { get; set; }

// Always use UtcNow
entity.CreatedAt = DateTime.UtcNow;

// Convert to local for display
var localTime = entity.CreatedAt.ToLocalTime();
```

### Issue: Daylight Saving Time Issues

**Cause**: Using DateTimeKind.Local during DST transitions

**Solution**: Use DateTimeKind.Utc instead
```csharp
// Don't use Local for timestamps
[DynamoDbAttribute("timestamp", DateTimeKind = DateTimeKind.Local)]  // ❌ Avoid

// Use UTC instead
[DynamoDbAttribute("timestamp", DateTimeKind = DateTimeKind.Utc)]    // ✅ Recommended
```

## Performance Considerations

DateTime Kind conversion has minimal performance impact:

- **ToUniversalTime()**: ~10-20 nanoseconds
- **ToLocalTime()**: ~10-20 nanoseconds
- **SpecifyKind()**: ~5 nanoseconds

The overhead is negligible compared to DynamoDB network latency (typically 1-10 milliseconds).

## See Also

- [Format Strings Guide](format-strings-guide.md) - Control DateTime string representation
- [DynamoDbAttribute API Reference](../reference/dynamodb-attribute.md) - Complete attribute documentation
- [.NET DateTime Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/datetime/choosing-between-datetime) - Official .NET guidance
