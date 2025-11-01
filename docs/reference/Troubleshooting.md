---
title: "Troubleshooting Guide"
category: "reference"
order: 4
keywords: ["troubleshooting", "issues", "errors", "source generator", "build", "compilation", "debugging"]
related: ["ErrorHandling.md", "AttributeReference.md"]
---

[Documentation](../README.md) > [Reference](README.md) > Troubleshooting

# Troubleshooting Guide

---

This guide helps you diagnose and resolve common issues when using Oproto.FluentDynamoDb. Each issue includes the error message, cause, solution, and related documentation.

## Source Generator Issues

### Source Generator Not Running

**Symptoms:**
- No generated code appears
- Field constants and key builders are missing
- Compiler errors about missing types

**Error Message:**
```
error CS0103: The name 'UserFields' does not exist in the current context
error CS0103: The name 'UserKeys' does not exist in the current context
```

**Cause:**
- Source generator not installed or not running
- Entity class not marked as `partial`
- Missing `[DynamoDbTable]` attribute
- Build cache issues

**Solution:**

1. **Verify the source generator package is installed:**
```bash
dotnet list package | grep Oproto.FluentDynamoDb.SourceGenerator
```

If not installed:
```bash
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
```

2. **Ensure your entity class is marked as `partial`:**
```csharp
// ❌ Wrong
[DynamoDbTable("users")]
public class User
{
    // ...
}

// ✅ Correct
[DynamoDbTable("users")]
public partial class User
{
    // ...
}
```

3. **Clean and rebuild:**
```bash
dotnet clean
dotnet build
```

4. **Check IDE-specific issues:**

**Visual Studio:**
- Close and reopen the solution
- Delete `.vs` folder and restart
- Check Tools → Options → Text Editor → C# → Advanced → Enable source generators

**Rider:**
- Invalidate caches: File → Invalidate Caches / Restart
- Ensure source generators are enabled in settings

**VS Code:**
- Reload window (Cmd/Ctrl + Shift + P → "Reload Window")
- Delete `obj` and `bin` folders

**See Also:**
- [Installation Guide](../getting-started/Installation.md)
- [First Entity Guide](../getting-started/FirstEntity.md)


### Partial Class Required Error

**Error Message:**
```
error DYNAMO001: Entity class 'User' must be marked as partial to allow source generation
```

**Cause:**
The source generator requires classes to be `partial` so it can extend them with generated code.

**Solution:**

Add the `partial` keyword to your class declaration:

```csharp
// Before
[DynamoDbTable("users")]
public class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}

// After
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}
```

**See Also:**
- [Entity Definition](../core-features/EntityDefinition.md#partial-classes)

### Missing Partition Key Error

**Error Message:**
```
error DYNAMO002: Entity 'User' must have exactly one property marked with [PartitionKey]
```

**Cause:**
Every DynamoDB entity requires exactly one partition key.

**Solution:**

Add the `[PartitionKey]` attribute to one property:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    // Add [PartitionKey] attribute
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
}
```

**See Also:**
- [Attribute Reference](AttributeReference.md#partitionkey)

### Multiple Partition Keys Error

**Error Message:**
```
error DYNAMO003: Entity 'User' has multiple properties marked with [PartitionKey]. Only one is allowed.
```

**Cause:**
An entity can only have one partition key.

**Solution:**

Remove `[PartitionKey]` from all but one property. If you need a composite key, use `[Computed]`:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [DynamoDbAttribute("tenant_id")]
    public string TenantId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    // Composite partition key
    [PartitionKey]
    [Computed(nameof(TenantId), nameof(UserId), Format = "{0}#{1}")]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
}
```

**See Also:**
- [Computed Attribute](AttributeReference.md#computed)
- [Entity Definition](../core-features/EntityDefinition.md)


### Generated Code Not Visible

**Symptoms:**
- Build succeeds but generated types aren't available
- IntelliSense doesn't show generated members
- Code compiles but IDE shows errors

**Cause:**
- IDE not recognizing generated code
- Namespace mismatch
- Generated files not included in compilation

**Solution:**

1. **Check the generated files exist:**

Look in `obj/Debug/net8.0/generated/` for files like:
- `User.Fields.g.cs`
- `User.Keys.g.cs`
- `User.Mapper.g.cs`

2. **Verify namespace matches:**

Generated code uses the same namespace as your entity. Ensure you're using the correct namespace:

```csharp
using YourNamespace; // Must match entity namespace

var userId = UserFields.UserId; // Should work
```

3. **Rebuild the project:**
```bash
dotnet clean
dotnet build
```

4. **Check for compilation errors:**

Generated code won't be available if there are compilation errors. Fix all errors and rebuild.

**See Also:**
- [Source Generator Guide](../getting-started/FirstEntity.md#generated-code-overview)

## Runtime Errors

### Mapping Errors

**Error Message:**
```
DynamoDbMappingException: Failed to map property 'CreatedAt' from DynamoDB attribute 'created_at'. 
Expected type DateTime but got String.
```

**Cause:**
- Type mismatch between entity property and DynamoDB attribute
- Missing or incorrect data in DynamoDB
- Incompatible type conversion

**Solution:**

1. **Verify property types match DynamoDB data:**

```csharp
// If DynamoDB stores ISO 8601 strings
[DynamoDbAttribute("created_at")]
public DateTime CreatedAt { get; set; }

// If DynamoDB stores Unix timestamps (numbers)
[DynamoDbAttribute("created_at")]
public long CreatedAtTimestamp { get; set; }
```

2. **Use nullable types for optional data:**

```csharp
// If the attribute might not exist
[DynamoDbAttribute("last_login")]
public DateTime? LastLogin { get; set; }
```

3. **Check DynamoDB data format:**

Use AWS Console or CLI to inspect the actual data:
```bash
aws dynamodb get-item \
    --table-name users \
    --key '{"pk": {"S": "USER#user123"}}'
```

**See Also:**
- [Entity Definition](../core-features/EntityDefinition.md)
- [Error Handling](ErrorHandling.md#dynamodbmappingexception)

### Type Conversion Errors

**Error Message:**
```
InvalidCastException: Unable to cast object of type 'System.String' to type 'System.Int32'
```

**Cause:**
DynamoDB attribute type doesn't match entity property type.

**Solution:**

Ensure property types match DynamoDB attribute types:

| DynamoDB Type | C# Type |
|---------------|---------|
| String (S) | `string` |
| Number (N) | `int`, `long`, `decimal`, `double` |
| Binary (B) | `byte[]` |
| Boolean (BOOL) | `bool` |
| Null (NULL) | Nullable types |
| List (L) | `List<T>` |
| Map (M) | Complex objects |
| String Set (SS) | `HashSet<string>` |
| Number Set (NS) | `HashSet<int>`, `HashSet<long>` |

```csharp
// Correct type mappings
[DynamoDbAttribute("age")]
public int Age { get; set; } // DynamoDB Number

[DynamoDbAttribute("tags")]
public HashSet<string> Tags { get; set; } = new(); // DynamoDB String Set

[DynamoDbAttribute("metadata")]
public Dictionary<string, string> Metadata { get; set; } = new(); // DynamoDB Map
```


### Expression Format Errors

**Error Message:**
```
FormatException: Format string contains invalid parameter indices: -1. 
Parameter indices must be non-negative integers.
```

**Cause:**
Invalid format string syntax in expression formatting.

**Solution:**

Use correct placeholder syntax:

```csharp
// ❌ Wrong - negative index
.Where($"{UserFields.Status} = {{-1}}", "active")

// ✅ Correct - zero-based positive index
.Where($"{UserFields.Status} = {{0}}", "active")

// ❌ Wrong - missing closing brace
.Where($"{UserFields.Status} = {{0", "active")

// ✅ Correct - properly closed
.Where($"{UserFields.Status} = {{0}}", "active")

// ❌ Wrong - not enough arguments
.Where($"{UserFields.Status} = {{0}} AND {UserFields.Type} = {{1}}", "active")

// ✅ Correct - matching arguments
.Where($"{UserFields.Status} = {{0}} AND {UserFields.Type} = {{1}}", "active", "premium")
```

**See Also:**
- [Format Specifiers Reference](FormatSpecifiers.md)
- [Expression Formatting Guide](../core-features/ExpressionFormatting.md)

## LINQ Expression Errors

### InvalidKeyExpressionException

**Error Message:**
```
InvalidKeyExpressionException: Property 'Status' is not a key attribute and cannot be used in Query().Where(). 
Use WithFilter() instead.
```

**Cause:**
Attempting to use a non-key property in a Query().Where() expression. Key condition expressions can only reference partition key and sort key properties.

**Solution:**

Move non-key properties to WithFilter():

```csharp
// ❌ Wrong - Status is not a key attribute
await table.Query
    .Where<User>(x => x.PartitionKey == userId && x.Status == "active")
    .ExecuteAsync();

// ✅ Correct - Move Status to filter
await table.Query
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter<User>(x => x.Status == "active")
    .ExecuteAsync();
```

**Understanding the Difference:**
- **Where()** - Key condition expression (partition key and sort key only)
- **WithFilter()** - Filter expression (any property)

**See Also:**
- [LINQ Expressions Guide](../core-features/LinqExpressions.md#query-vs-filter-expressions)
- [Querying Data](../core-features/QueryingData.md)

### UnmappedPropertyException

**Error Message:**
```
UnmappedPropertyException: Property 'Email' on type 'User' does not map to a DynamoDB attribute.
```

**Cause:**
The property referenced in the expression doesn't have a `[DynamoDbAttribute]` mapping.

**Solution:**

Add the `[DynamoDbAttribute]` to the property:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    // Add [DynamoDbAttribute] to make it queryable
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
}

// Now this works
await table.Query
    .Where<User>(x => x.UserId == userId)
    .WithFilter<User>(x => x.Email.StartsWith("admin@"))
    .ExecuteAsync();
```

**See Also:**
- [Entity Definition](../core-features/EntityDefinition.md)
- [Attribute Reference](AttributeReference.md)

### UnsupportedExpressionException - Method Calls on Entity Properties

**Error Message:**
```
UnsupportedExpressionException: Method 'ToUpper' cannot be used on entity properties in DynamoDB expressions. 
DynamoDB expressions cannot execute C# methods on data.
```

**Cause:**
Attempting to call a C# method on an entity property. DynamoDB can't execute C# code on stored data.

**Solution:**

Transform values before the query:

```csharp
// ❌ Wrong - Can't call ToUpper() on entity property
await table.Query
    .WithFilter<User>(x => x.Name.ToUpper() == "JOHN")
    .ExecuteAsync();

// ✅ Correct - Transform the comparison value
var upperName = "JOHN";
await table.Query
    .WithFilter<User>(x => x.Name == upperName)
    .ExecuteAsync();

// ✅ Alternative - Store normalized data
// Add a computed property for case-insensitive queries
[DynamoDbAttribute("name_upper")]
[Computed(nameof(Name))]
public string NameUpper => Name.ToUpper();

// Then query the normalized field
await table.Query
    .WithFilter<User>(x => x.NameUpper == "JOHN")
    .ExecuteAsync();
```

**See Also:**
- [LINQ Expressions Guide](../core-features/LinqExpressions.md#valid-vs-invalid-patterns)

### UnsupportedExpressionException - Method References Entity Parameter

**Error Message:**
```
UnsupportedExpressionException: Method 'myFunction' cannot reference the entity parameter or its properties. 
DynamoDB expressions cannot execute C# methods with entity data.
```

**Cause:**
Attempting to pass the entity parameter or its properties to a method call. DynamoDB can't execute your C# methods.

**Solution:**

Evaluate the method before the query:

```csharp
// ❌ Wrong - Method references entity parameter
await table.Query
    .Where<User>(x => x.Id == ComputeId(x))
    .ExecuteAsync();

// ❌ Wrong - Method references entity property
await table.Query
    .Where<User>(x => x.Id == ComputeId(x.UserId))
    .ExecuteAsync();

// ✅ Correct - Evaluate method with captured values
var userId = GetCurrentUserId();
var computedId = ComputeId(userId);
await table.Query
    .Where<User>(x => x.Id == computedId)
    .ExecuteAsync();
```

**Valid Method Calls:**
You CAN call methods on captured values (not entity properties):

```csharp
// ✅ Valid - Method call on captured value
var userId = GetUserId();
await table.Query
    .Where<User>(x => x.Id == userId.ToString())
    .ExecuteAsync();

// ✅ Valid - Complex expression on captured value
var date = DateTime.Now;
await table.Query
    .WithFilter<Order>(x => x.CreatedDate > date.AddDays(-30))
    .ExecuteAsync();
```

**See Also:**
- [LINQ Expressions Guide](../core-features/LinqExpressions.md#value-capture)

### UnsupportedExpressionException - Assignment Expression

**Error Message:**
```
UnsupportedExpressionException: Assignment expressions are not supported in DynamoDB queries. 
Use comparison operators (==, <, >, etc.) instead of assignment (=).
```

**Cause:**
Using assignment operator (=) instead of comparison operator (==).

**Solution:**

Use comparison operators:

```csharp
// ❌ Wrong - Assignment operator
await table.Query
    .Where<User>(x => x.Id = "user123")
    .ExecuteAsync();

// ✅ Correct - Comparison operator
await table.Query
    .Where<User>(x => x.Id == "user123")
    .ExecuteAsync();
```

**See Also:**
- [LINQ Expressions Guide](../core-features/LinqExpressions.md#supported-operators)

### UnsupportedExpressionException - Unsupported Operator

**Error Message:**
```
UnsupportedExpressionException: The operator 'Modulo' is not supported in DynamoDB expressions. 
Supported operators: ==, !=, <, >, <=, >=, &&, ||, !
```

**Cause:**
Using an operator that DynamoDB doesn't support (like modulo %, bitwise operators, etc.).

**Solution:**

Filter in application code after retrieval:

```csharp
// ❌ Wrong - Modulo not supported
await table.Query
    .WithFilter<User>(x => x.Age % 2 == 0)
    .ExecuteAsync();

// ✅ Correct - Filter in application code
var response = await table.Query
    .Where<User>(x => x.PartitionKey == pk)
    .ExecuteAsync();

var evenAgeUsers = response.Items
    .Where(u => u.Age % 2 == 0)
    .ToList();
```

**Supported Operators:**
- Comparison: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Logical: `&&`, `||`, `!`
- DynamoDB functions: `StartsWith()`, `Contains()`, `Between()`, `AttributeExists()`, `AttributeNotExists()`, `Size()`

**See Also:**
- [LINQ Expressions Guide](../core-features/LinqExpressions.md#supported-operators)

### ExpressionTranslationException - Complex Expression

**Error Message:**
```
ExpressionTranslationException: Expression is too complex to translate. 
Consider using string-based expressions with Where(string) or WithFilter(string) for complex scenarios.
```

**Cause:**
The expression is too complex for the translator to handle.

**Solution:**

Use string-based expressions for complex scenarios:

```csharp
// ❌ Too complex for expression translator
await table.Query
    .WithFilter<User>(x => 
        x.Items.Where(i => i.Active).Select(i => i.Price).Sum() > 100)
    .ExecuteAsync();

// ✅ Use string-based expression
await table.Query
    .WithFilter($"size({UserFields.Items}) > {{0}}", 0)
    .ExecuteAsync();

// Then filter in application code
var response = await table.Query
    .Where<User>(x => x.PartitionKey == pk)
    .ExecuteAsync();

var filtered = response.Items
    .Where(u => u.Items.Where(i => i.Active).Sum(i => i.Price) > 100)
    .ToList();
```

**See Also:**
- [LINQ Expressions Guide](../core-features/LinqExpressions.md#when-to-use-string-based)
- [Expression Formatting Guide](../core-features/ExpressionFormatting.md)

### Reserved Word Errors

**Error Message:**
```
ValidationException: Invalid UpdateExpression: Attribute name is a reserved keyword; 
reserved keyword: status
```

**Cause:**
Using a DynamoDB reserved word without an attribute name placeholder.

**Solution:**

Use `WithAttributeName` for reserved words:

```csharp
// ❌ Wrong - 'status' is a reserved word
await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Status} = {{0}}", "active")
    .ExecuteAsync<User>();

// ✅ Correct - use attribute name placeholder
await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithAttributeName("#status", UserFields.Status)
    .Where($"#status = {{0}}", "active")
    .ExecuteAsync<User>();
```

**Common Reserved Words:**
- `status`, `name`, `type`, `data`, `timestamp`
- `year`, `month`, `day`, `hour`, `minute`
- `order`, `date`, `time`, `value`, `key`

**See Also:**
- [AWS DynamoDB Reserved Words](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ReservedWords.html)
- [Expression Formatting](../core-features/ExpressionFormatting.md#reserved-words)

## Performance Issues

### Slow Query Performance

**Symptoms:**
- Queries taking longer than expected
- High consumed capacity units
- Timeout errors

**Cause:**
- Scanning instead of querying
- Missing or inefficient indexes
- Large result sets without pagination
- Inefficient filter expressions

**Solution:**

1. **Use Query instead of Scan:**

```csharp
// ❌ Slow - scans entire table
await table.Scan
    .Where($"{UserFields.Status} = {{0}}", "active")
    .ExecuteAsync<User>();

// ✅ Fast - queries with partition key
await table.Query
    .WithKey(UserFields.Status, "active") // Requires GSI
    .ExecuteAsync<User>();
```

2. **Add appropriate indexes:**

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    // Add GSI for querying by status
    [GlobalSecondaryIndex("status-index", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```

3. **Use pagination:**

```csharp
// ❌ Loads all results at once
var response = await table.Query
    .WithKey(UserFields.Status, "active")
    .ExecuteAsync<User>();

// ✅ Paginate results
var response = await table.Query
    .WithKey(UserFields.Status, "active")
    .Take(100) // Limit page size
    .ExecuteAsync<User>();

// Process next page
if (response.LastEvaluatedKey != null)
{
    var nextPage = await table.Query
        .WithKey(UserFields.Status, "active")
        .Take(100)
        .WithExclusiveStartKey(response.LastEvaluatedKey)
        .ExecuteAsync<User>();
}
```

4. **Use projection expressions:**

```csharp
// ❌ Retrieves all attributes
var response = await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

// ✅ Only retrieves needed attributes
var response = await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithProjection($"{UserFields.UserId}, {UserFields.Email}, {UserFields.Name}")
    .ExecuteAsync<User>();
```

**See Also:**
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
- [Querying Data](../core-features/QueryingData.md)


### High Consumed Capacity

**Symptoms:**
- Unexpectedly high read/write capacity consumption
- Throttling errors
- High AWS costs

**Cause:**
- Inefficient queries or scans
- Not using batch operations
- Consistent reads when not needed
- Large items

**Solution:**

1. **Use batch operations:**

```csharp
// ❌ Multiple individual requests
foreach (var userId in userIds)
{
    await table.Get
        .WithKey(UserFields.UserId, UserKeys.Pk(userId))
        .ExecuteAsync<User>();
}

// ✅ Single batch request
var response = await table.BatchGet
    .FromTable("users", userIds.Select(id => 
        new Dictionary<string, AttributeValue>
        {
            [UserFields.UserId] = new AttributeValue { S = UserKeys.Pk(id) }
        }))
    .ExecuteAsync();
```

2. **Use eventually consistent reads:**

```csharp
// ❌ Consistent read (2x capacity)
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithConsistentRead(true)
    .ExecuteAsync<User>();

// ✅ Eventually consistent read (1x capacity)
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>(); // Consistent read is false by default
```

3. **Monitor item sizes:**

```csharp
// Check consumed capacity
var response = await table.Put
    .WithItem(user)
    .WithReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ExecuteAsync();

Console.WriteLine($"Consumed capacity: {response.ConsumedCapacity.CapacityUnits}");
```

**See Also:**
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
- [Batch Operations](../core-features/BatchOperations.md)

## Build and Compilation Issues

### Package Version Conflicts

**Error Message:**
```
error NU1107: Version conflict detected for AWSSDK.DynamoDBv2
```

**Cause:**
Multiple packages requiring different versions of AWS SDK.

**Solution:**

1. **Check installed packages:**
```bash
dotnet list package --include-transitive | grep AWSSDK
```

2. **Explicitly specify AWS SDK version:**
```xml
<ItemGroup>
  <PackageReference Include="AWSSDK.DynamoDBv2" Version="3.7.300" />
  <PackageReference Include="Oproto.FluentDynamoDb" Version="0.3.0" />
</ItemGroup>
```

3. **Update all packages:**
```bash
dotnet add package AWSSDK.DynamoDBv2
dotnet add package Oproto.FluentDynamoDb
```

### AOT Compatibility Issues

**Error Message:**
```
warning IL2026: Using member 'System.Reflection.MethodInfo.Invoke' which has 'RequiresUnreferencedCodeAttribute'
```

**Cause:**
Using reflection-based features incompatible with AOT compilation.

**Solution:**

The library is AOT-compatible when using source generation. Ensure you're:

1. **Using source-generated code:**
```csharp
// ✅ AOT-compatible - uses generated code
var user = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();
```

2. **Avoiding reflection-based patterns:**
```csharp
// ❌ Not AOT-compatible
var propertyInfo = typeof(User).GetProperty("UserId");
propertyInfo.SetValue(user, "user123");

// ✅ AOT-compatible
user.UserId = "user123";
```

**See Also:**
- [Installation Guide](../getting-started/Installation.md)

### Missing Dependencies

**Error Message:**
```
error CS0246: The type or namespace name 'Amazon' could not be found
```

**Cause:**
AWS SDK not installed.

**Solution:**

Install required packages:
```bash
dotnet add package AWSSDK.DynamoDBv2
dotnet add package Oproto.FluentDynamoDb
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
```

Verify installation:
```bash
dotnet list package
```


## Common Patterns and Solutions

### Debugging Generated Code

To inspect generated code:

1. **Enable source generator output:**

Add to your `.csproj`:
```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

2. **View generated files:**

After building, check:
```
obj/Generated/Oproto.FluentDynamoDb.SourceGenerator/
```

3. **Add generated files to source control (optional):**
```xml
<ItemGroup>
  <Compile Include="$(CompilerGeneratedFilesOutputPath)/**/*.cs" Visible="true" />
</ItemGroup>
```

### Testing with Local DynamoDB

For local development and testing:

1. **Install DynamoDB Local:**
```bash
docker run -p 8000:8000 amazon/dynamodb-local
```

2. **Configure client for local endpoint:**
```csharp
var config = new AmazonDynamoDBConfig
{
    ServiceURL = "http://localhost:8000"
};
var client = new AmazonDynamoDBClient(config);
var table = new DynamoDbTableBase(client, "users");
```

3. **Create test tables:**
```bash
aws dynamodb create-table \
    --table-name users \
    --attribute-definitions AttributeName=pk,AttributeType=S \
    --key-schema AttributeName=pk,KeyType=HASH \
    --billing-mode PAY_PER_REQUEST \
    --endpoint-url http://localhost:8000
```

### Handling Null Values

**Problem:** Null values causing issues in queries or updates.

**Solution:**

1. **Use nullable types:**
```csharp
[DynamoDbAttribute("middle_name")]
public string? MiddleName { get; set; }
```

2. **Check for null before operations:**
```csharp
if (user.MiddleName != null)
{
    await table.Update
        .WithKey(UserFields.UserId, UserKeys.Pk(user.UserId))
        .Set($"SET {UserFields.MiddleName} = {{0}}", user.MiddleName)
        .ExecuteAsync();
}
```

3. **Use attribute_exists for conditional operations:**
```csharp
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}", "New Name")
    .WithCondition($"attribute_exists({UserFields.UserId})")
    .ExecuteAsync();
```

### Working with Complex Types

**Problem:** Serializing complex objects to DynamoDB.

**Solution:**

DynamoDB supports nested objects (Maps):

```csharp
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    // Complex type stored as DynamoDB Map
    [DynamoDbAttribute("address")]
    public Address Address { get; set; } = new();
    
    // List of complex types
    [DynamoDbAttribute("previous_addresses")]
    public List<Address> PreviousAddresses { get; set; } = new();
}
```

## Testing Operation Context Assignments

### `DynamoDbOperationContext.Current` is always null in unit tests

**Symptoms**
- After awaiting `GetItemAsync`, `ToListAsync`, `PutAsync`, etc., `DynamoDbOperationContext.Current` is unexpectedly `null`
- FluentAssertions or xUnit assertions verifying context metadata fail

**Cause**
Unit test frameworks (e.g., xUnit) restore the original execution context when an awaited task completes. Because `DynamoDbOperationContext` uses `AsyncLocal`, the value assigned inside the library is lost once the framework resumes the test method.

**Solution**
Subscribe to the internal diagnostics event before invoking the operation and capture the context inside the same asynchronous flow. Remember to unsubscribe in a `finally` block.

```csharp
using Oproto.FluentDynamoDb.Storage;

OperationContextData? captured = null;
void Handler(OperationContextData? ctx) => captured = ctx;

DynamoDbOperationContextDiagnostics.ContextAssigned += Handler;
try
{
    await builder.ToListAsync<MyEntity>();

    captured.Should().NotBeNull();
    captured!.RawItems.Should().NotBeNull();
}
finally
{
    DynamoDbOperationContextDiagnostics.ContextAssigned -= Handler;
}
```

> **Warning**  
> `DynamoDbOperationContextDiagnostics` is intended for diagnostics and test scenarios only. Production code should continue to read metadata from `DynamoDbOperationContext.Current`.

## Getting Help

If you're still experiencing issues:

1. **Check existing issues:**
   - [GitHub Issues](https://github.com/oproto/Oproto.FluentDynamoDb/issues)

2. **Create a minimal reproduction:**
   - Isolate the problem
   - Create a small, complete example
   - Include error messages and stack traces

3. **Provide context:**
   - Library version
   - .NET version
   - AWS SDK version
   - Operating system
   - IDE and version

4. **Review documentation:**
   - [Getting Started](../getting-started/README.md)
   - [Core Features](../core-features/README.md)
   - [Advanced Topics](../advanced-topics/README.md)

## See Also

- [Error Handling Guide](ErrorHandling.md)
- [Attribute Reference](AttributeReference.md)
- [Format Specifiers](FormatSpecifiers.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
- [AWS DynamoDB Troubleshooting](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Programming.Errors.html)
