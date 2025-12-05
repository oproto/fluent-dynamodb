# Known Limitations

This document describes known limitations of the DynamoDB Source Generator and workarounds where applicable.

## 1. Computed Properties (Read-Only Properties)

**Issue**: The source generator cannot deserialize data into computed properties (properties with only a getter).

**Technical Details**: 
- During deserialization in `FromDynamoDb<TSelf>()`, the generator creates property assignments like:
  ```csharp
  entity.PropertyName = value;
  ```
- For computed properties (properties with only `{ get; }` or `=> expression`), this assignment fails at compile time because you cannot assign to a read-only property.

**Example of Unsupported Pattern**:
```csharp
[DynamoDbTable("MyTable")]
public partial class MyEntity
{
    [DynamoDbPartitionKey]
    public string Id { get; set; }
    
    // ❌ This will NOT work - computed property cannot be assigned during deserialization
    public double Latitude { get; }
    
    // ❌ This will NOT work - expression-bodied property
    public double Longitude => _internalLongitude;
}
```

**Workaround**: Use properties with both getter and setter:
```csharp
[DynamoDbTable("MyTable")]
public partial class MyEntity
{
    [DynamoDbPartitionKey]
    public string Id { get; set; }
    
    // ✅ This works - property has both get and set
    public double Latitude { get; set; }
    
    // ✅ This works - property has both get and set
    public double Longitude { get; set; }
}
```

**Impact**: 
- Entities with computed properties will fail to compile with errors about assigning to read-only properties
- This affects scenarios where you might want to derive property values from stored data
- Particularly relevant for geospatial entities where you might want to compute coordinates from stored index values

**Affected Test Entities**:
- `S2StoreWithComputedPropsEntity` - Has computed `Latitude` and `Longitude` properties derived from `Location`
- Tests in `S2StorageIntegrationTests.cs` section 24.7 are written but do not compile
- These tests are preserved in the codebase to be enabled once the limitation is resolved

**Status**: Known limitation. Future enhancement may support computed properties through alternative deserialization strategies (e.g., constructor injection, init-only properties, or skipping computed properties during deserialization).

---

## 2. Multi-Item Entities with Blob References

**Issue**: Multi-item entities with blob reference properties are not fully supported.

**Technical Details**:
- The async `FromDynamoDbAsync` method for multi-item entities currently only processes the first item
- Blob references across multiple items in a composite entity are not yet handled

**Workaround**: Use single-item entities for entities with blob references.

**Status**: Known limitation. Future enhancement planned.

---

## 3. Circular Dependencies in Computed Keys

**Issue**: The analyzer detects but does not resolve circular dependencies in computed key patterns.

**Example of Unsupported Pattern**:
```csharp
[DynamoDbPartitionKey(Computed = true, Components = new[] { "SortKey" })]
public string PartitionKey { get; set; }

[DynamoDbSortKey(Computed = true, Components = new[] { "PartitionKey" })]
public string SortKey { get; set; }
```

**Workaround**: Ensure computed keys do not have circular dependencies. Use extracted keys instead where appropriate.

**Status**: Validation in place. Compile-time error reported.

---

## Reporting Issues

If you encounter additional limitations or have suggestions for workarounds, please:
1. Check this document first to see if it's a known limitation
2. Review the main README.md for architectural context
3. Open an issue on the project repository with:
   - Description of the limitation
   - Minimal reproduction example
   - Expected vs actual behavior
   - Any workarounds you've discovered

## Future Enhancements

The following enhancements are being considered to address current limitations:

1. **Computed Property Support**: 
   - Constructor-based deserialization
   - Init-only property support
   - Selective property skipping during deserialization

2. **Multi-Item Blob References**:
   - Full support for blob references across composite entities
   - Parallel blob retrieval for performance

3. **Enhanced Validation**:
   - More comprehensive compile-time validation
   - Better error messages with suggested fixes
