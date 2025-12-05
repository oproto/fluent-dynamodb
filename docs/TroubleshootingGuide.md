# Troubleshooting Guide

This guide helps you diagnose and resolve common issues when using the DynamoDB source generator.

## Table of Contents
- [Source Generator Issues](#source-generator-issues)
- [Compilation Errors](#compilation-errors)
- [Runtime Errors](#runtime-errors)
- [Performance Issues](#performance-issues)
- [Configuration Problems](#configuration-problems)
- [Diagnostic Messages](#diagnostic-messages)

## Source Generator Issues

### Source Generator Not Running

**Symptoms:**
- No generated code files appear
- IntelliSense doesn't show generated methods
- Build succeeds but generated functionality is missing

**Causes and Solutions:**

#### 1. Missing `partial` Keyword
```csharp
// ❌ Wrong - missing partial keyword
[DynamoDbTable("users")]
public class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
}

// ✅ Correct - class marked as partial
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
}
```

#### 2. Missing DynamoDbTable Attribute
```csharp
// ❌ Wrong - no table attribute
public partial class User
{
    [PartitionKey]
    public string Id { get; set; }
}

// ✅ Correct - table attribute present
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
}
```

#### 3. Clean and Rebuild
```bash
# Clean solution
dotnet clean

# Restore packages
dotnet restore

# Rebuild
dotnet build
```

#### 4. Check Generated Files Location
Generated files are typically located at:
- **Visual Studio**: Dependencies → Analyzers → Oproto.FluentDynamoDb.SourceGenerator
- **Rider**: External Libraries → Generated Files
- **VS Code**: Check `.generated` folder in project directory

### Source Generator Crashes

**Symptoms:**
- Build fails with analyzer errors
- "Source generator threw an exception" messages

**Diagnostic Steps:**

#### 1. Enable Detailed Logging
Add to your `.csproj` file:
```xml
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

#### 2. Check MSBuild Output
```bash
dotnet build -v detailed
```

Look for source generator error messages in the output.

#### 3. Isolate the Problem
Create a minimal test entity:
```csharp
[DynamoDbTable("test")]
public partial class TestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
}
```

If this works, gradually add complexity to identify the problematic configuration.

## Compilation Errors

### CS0246: Type or Namespace Not Found

**Error Message:**
```
CS0246: The type or namespace name 'UserFields' could not be found
```

**Cause:** Source generator didn't run or generated code wasn't included in compilation.

**Solutions:**

#### 1. Verify Entity Configuration
```csharp
// Ensure all required attributes are present
[DynamoDbTable("users")]  // ✅ Required
public partial class User  // ✅ Must be partial
{
    [PartitionKey]           // ✅ Required - exactly one per entity
    [DynamoDbAttribute("pk")] // ✅ Required for mapped properties
    public string Id { get; set; } = string.Empty;
}
```

#### 2. Check Build Order
Ensure the source generator project builds before projects that use it:
```xml
<ProjectReference Include="..\Oproto.FluentDynamoDb\Oproto.FluentDynamoDb.csproj" />
```

#### 3. Force Regeneration
```bash
# Delete bin and obj folders
rm -rf bin obj

# Rebuild
dotnet build
```

### CS0103: Name Does Not Exist in Current Context

**Error Message:**
```
CS0103: The name 'UserKeys' does not exist in the current context
```

**Cause:** Generated key builder class not found.

**Solution:**
Verify the entity has proper key attributes:
```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]  // ✅ This generates UserKeys.Pk() method
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [SortKey]       // ✅ This generates UserKeys.Sk() method
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}

// Generated UserKeys class will be available
var pk = UserKeys.Pk("user123");
var sk = UserKeys.Sk("profile");
```

### CS0534: Does Not Implement Interface Member

**Error Message:**
```
CS0534: 'User' does not implement interface member 'IDynamoDbEntity.ToDynamoDb<TSelf>(TSelf)'
```

**Cause:** Entity class doesn't properly implement `IDynamoDbEntity` interface.

**Solution:**
Ensure the entity is properly configured for source generation:
```csharp
[DynamoDbTable("users")]
public partial class User  // ✅ Must be partial for interface implementation
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    // Source generator will implement IDynamoDbEntity methods
}
```

## Runtime Errors

### DynamoDbMappingException

**Error Message:**
```
DynamoDbMappingException: Failed to map DynamoDB item to User. Property 'Name' not found in item.
```

**Causes and Solutions:**

#### 1. Missing Property in DynamoDB Item
```csharp
// ❌ Problem: Property marked as required but missing from DynamoDB
[DynamoDbAttribute("name")]
public string Name { get; set; } = string.Empty;

// ✅ Solution: Make property nullable if it might be missing
[DynamoDbAttribute("name")]
public string? Name { get; set; }
```

#### 2. Attribute Name Mismatch
```csharp
// ❌ Problem: Attribute name doesn't match DynamoDB
[DynamoDbAttribute("user_name")]  // DynamoDB has "name"
public string Name { get; set; } = string.Empty;

// ✅ Solution: Use correct attribute name
[DynamoDbAttribute("name")]       // Matches DynamoDB attribute
public string Name { get; set; } = string.Empty;
```

#### 3. Type Conversion Issues
```csharp
// ❌ Problem: Type mismatch
[DynamoDbAttribute("created_at")]
public DateTime CreatedAt { get; set; }  // DynamoDB stores as string

// ✅ Solution: Handle type conversion properly
[DynamoDbAttribute("created_at")]
public DateTime CreatedAt { get; set; }  // Generator handles ISO string conversion
```

### InvalidOperationException: Multiple Partition Keys

**Error Message:**
```
InvalidOperationException: Entity 'User' has multiple partition keys defined
```

**Cause:** More than one property marked with `[PartitionKey]`.

**Solution:**
```csharp
// ❌ Wrong - multiple partition keys
public partial class User
{
    [PartitionKey]
    public string TenantId { get; set; } = string.Empty;

    [PartitionKey]  // ❌ Second partition key
    public string UserId { get; set; } = string.Empty;
}

// ✅ Correct - use computed composite key
public partial class User
{
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(UserId))]
    public string Pk { get; set; } = string.Empty;
}
```

### ArgumentException: Invalid Key Format

**Error Message:**
```
ArgumentException: Invalid key format for computed property 'Pk'
```

**Cause:** Computed key source properties are null or invalid.

**Solution:**
```csharp
// ✅ Validate source properties before mapping
public async Task<User> CreateUserAsync(User user)
{
    if (string.IsNullOrEmpty(user.TenantId))
        throw new ArgumentException("TenantId is required");
    
    if (string.IsNullOrEmpty(user.UserId))
        throw new ArgumentException("UserId is required");

    await _table.Put.WithItem(user).ExecuteAsync();
    return user;
}
```

### ConditionalCheckFailedException

**Error Message:**
```
ConditionalCheckFailedException: The conditional request failed
```

**Cause:** Condition expression evaluated to false.

**Common Scenarios and Solutions:**

#### 1. Item Already Exists
```csharp
try
{
    await _table.Put
        .WithItem(user)
        .WithConditionExpression($"attribute_not_exists({UserFields.Id})")
        .ExecuteAsync();
}
catch (ConditionalCheckFailedException)
{
    throw new InvalidOperationException($"User with ID {user.Id} already exists");
}
```

#### 2. Item Doesn't Exist for Update
```csharp
try
{
    await _table.Update
        .WithKey(UserFields.Id, UserKeys.Pk(userId))
        .Set(UserFields.Name, newName)
        .WithConditionExpression($"attribute_exists({UserFields.Id})")
        .ExecuteAsync();
}
catch (ConditionalCheckFailedException)
{
    throw new InvalidOperationException($"User with ID {userId} not found");
}
```

#### 3. Version Conflict (Optimistic Locking)
```csharp
try
{
    await _table.Update
        .WithKey(UserFields.Id, UserKeys.Pk(userId))
        .Set($"SET {UserFields.Name} = {{0}}, {UserFields.Version} = {{1}}", newName, currentVersion + 1)
        .Where($"{UserFields.Version} = {{0}}", currentVersion)
        .ExecuteAsync();
}
catch (ConditionalCheckFailedException)
{
    throw new InvalidOperationException("User was modified by another process. Please refresh and try again.");
}
```

## Performance Issues

### Slow Query Performance

**Symptoms:**
- Queries take longer than expected
- High consumed capacity units

**Diagnostic Steps:**

#### 1. Enable Capacity Monitoring
```csharp
var response = await _table.Query
    .Where($"{UserFields.TenantId} = {{0}}", tenantId)
    .WithReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ToListAsync<User>();

Console.WriteLine($"Consumed RCU: {response.ConsumedCapacity?.ReadCapacityUnits}");
Console.WriteLine($"Items returned: {response.Count}");
```

#### 2. Check Query Patterns
```csharp
// ❌ Inefficient - scan operation (requires [Scannable] attribute)
var users = await _table.Scan()
    .WithFilter($"{UserFields.Status} = {{0}}", "active")
    .ToListAsync<User>();

// ✅ Efficient - query with GSI
var users = await _table.Query<User>()
    .UsingIndex("StatusIndex")
    .Where($"{User.Fields.Status} = {{0}}", "active")
    .ToListAsync();
```

#### 3. Optimize Projections
```csharp
// ❌ Inefficient - returns all attributes
var users = await _table.Query
    .Where($"{UserFields.TenantId} = {{0}}", tenantId)
    .ToListAsync<User>();

// ✅ Efficient - project only needed attributes
var userSummaries = await _table.Query
    .Where($"{UserFields.TenantId} = {{0}}", tenantId)
    .WithProjectionExpression($"{UserFields.Id}, {UserFields.Name}, {UserFields.Email}")
    .ExecuteAsync();
```

### Memory Usage Issues

**Symptoms:**
- High memory consumption
- OutOfMemoryException with large result sets

**Solutions:**

#### 1. Use Pagination
```csharp
public async Task<List<User>> GetAllUsersPagedAsync(string tenantId)
{
    var allUsers = new List<User>();
    Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

    do
    {
        var query = _table.Query<User>()
            .Where($"{User.Fields.TenantId} = {{0}}", tenantId)
            .Take(100); // Process in batches

        if (lastEvaluatedKey != null)
        {
            query = query.WithExclusiveStartKey(lastEvaluatedKey);
        }

        var response = await query.ExecuteAsync();
        
        // Process batch
        foreach (var item in response.Items)
        {
            if (User.MatchesEntity(item))
            {
                allUsers.Add(User.FromDynamoDb<User>(item));
            }
        }

        lastEvaluatedKey = response.LastEvaluatedKey;

    } while (lastEvaluatedKey?.Count > 0);

    return allUsers;
}
```

#### 2. Stream Processing
```csharp
public async IAsyncEnumerable<User> GetUsersStreamAsync(string tenantId)
{
    Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

    do
    {
        var query = _table.Query<User>()
            .Where($"{User.Fields.TenantId} = {{0}}", tenantId)
            .Take(50);

        if (lastEvaluatedKey != null)
        {
            query = query.WithExclusiveStartKey(lastEvaluatedKey);
        }

        var response = await query.ExecuteAsync();
        
        foreach (var item in response.Items)
        {
            if (User.MatchesEntity(item))
            {
                yield return User.FromDynamoDb<User>(item);
            }
        }

        lastEvaluatedKey = response.LastEvaluatedKey;

    } while (lastEvaluatedKey?.Count > 0);
}

// Usage
await foreach (var user in GetUsersStreamAsync(tenantId))
{
    // Process one user at a time
    ProcessUser(user);
}
```

## Configuration Problems

### Missing Package References

**Error Message:**
```
CS0246: The type or namespace name 'DynamoDbTableAttribute' could not be found
```

**Solution:**
Ensure proper package references in `.csproj`:
```xml
<PackageReference Include="Oproto.FluentDynamoDb" Version="0.2.0" />
```

### Wrong Target Framework

**Error Message:**
```
Source generators require .NET 5.0 or later
```

**Solution:**
Update target framework in `.csproj`:
```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>  <!-- ✅ .NET 8 -->
    <!-- <TargetFramework>netstandard2.1</TargetFramework>  ❌ Too old -->
</PropertyGroup>
```

### Missing Using Statements

**Error Message:**
```
CS0246: The type or namespace name 'DynamoDbTableAttribute' could not be found
```

**Solution:**
Add required using statements:
```csharp
using Oproto.FluentDynamoDb.Attributes;  // ✅ For attributes
using Oproto.FluentDynamoDb.Entities;    // ✅ For IDynamoDbEntity
```

Or enable global usings in `.csproj`:
```xml
<PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

## Diagnostic Messages

The source generator provides diagnostic messages to help identify configuration issues:

### DYNDB001: Missing Partition Key

**Message:** `Entity 'User' must have exactly one property marked with [PartitionKey]`

**Solution:**
```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]  // ✅ Add this
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
}
```

### DYNDB002: Multiple Partition Keys

**Message:** `Entity 'User' has multiple partition keys defined`

**Solution:**
Use computed composite key instead:
```csharp
[DynamoDbTable("users")]
public partial class User
{
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(UserId))]
    public string Pk { get; set; } = string.Empty;
}
```

### DYNDB021: Reserved Word Usage

**Message:** `Property 'Status' uses DynamoDB reserved word. Consider using [DynamoDbAttribute] with different name`

**Solution:**
```csharp
// ✅ Use attribute to map to different name
[DynamoDbAttribute("item_status")]  // Avoid reserved word "status"
public string Status { get; set; } = string.Empty;
```

### DYNDB027: Scalability Warning

**Message:** `Entity 'User' may have scalability issues. Consider using composite partition keys for better distribution`

**Solution:**
```csharp
// ❌ Potential hot partition
[PartitionKey]
[DynamoDbAttribute("pk")]
public string Status { get; set; } = string.Empty;  // All items have same PK

// ✅ Better distribution
[PartitionKey]
[DynamoDbAttribute("pk")]
[Computed(nameof(TenantId), nameof(Status))]
public string Pk { get; set; } = string.Empty;  // Distributed across tenants
```

### DYNDB010: Non-Partial Class

**Message:** `Class 'User' must be marked as partial for source generation`

**Solution:**
```csharp
// ❌ Missing partial keyword
[DynamoDbTable("users")]
public class User { }

// ✅ Add partial keyword
[DynamoDbTable("users")]
public partial class User { }
```

## Getting Help

### Enable Verbose Logging

Add to your application configuration:
```json
{
  "Logging": {
    "LogLevel": {
      "Oproto.FluentDynamoDb": "Debug",
      "Amazon.DynamoDBv2": "Information"
    }
  }
}
```

### Collect Diagnostic Information

When reporting issues, include:

1. **Entity Definition:**
```csharp
// Include your complete entity class
[DynamoDbTable("table-name")]
public partial class YourEntity
{
    // All properties with attributes
}
```

2. **Generated Code:**
Check the generated files in your IDE and include relevant snippets.

3. **Error Messages:**
Include complete error messages and stack traces.

4. **Environment Information:**
- .NET version
- Package versions
- IDE/Editor used

5. **Minimal Reproduction:**
Create the smallest possible example that reproduces the issue.

### Common Debugging Steps

1. **Start Simple:** Create a minimal entity and verify it works
2. **Add Complexity Gradually:** Add one feature at a time
3. **Check Generated Code:** Examine what the source generator produces
4. **Enable Logging:** Use verbose logging to see what's happening
5. **Compare Working Examples:** Use the code examples in this documentation as reference

This troubleshooting guide should help you resolve most common issues with the DynamoDB source generator. If you encounter issues not covered here, consider creating a minimal reproduction case and seeking help from the community.