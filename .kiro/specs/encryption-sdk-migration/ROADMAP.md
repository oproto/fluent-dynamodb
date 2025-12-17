# KMS Encryption Module Roadmap

This document captures the planned phases for the KMS encryption module overhaul. Each phase will have its own spec when implementation begins.

## Phase 1: Package Migration & Core Implementation (Current)

**Status:** In Progress

**Scope:**
- Migrate from `AWS.EncryptionSDK` to `AWS.Cryptography.EncryptionSDK`
- Implement actual encrypt/decrypt using new API
- Data key caching for cost reduction
- AOT compatibility verification
- Maintain backward compatibility with existing interfaces

**Spec:** `.kiro/specs/encryption-sdk-migration/`

---

## Phase 2: Per-Field Key Support (Future)

**Status:** Planned

**Problem Statement:**
Different fields within the same entity may need different encryption keys. For example:
- `SSN` field encrypted with tenant's KMS key (tenant controls access)
- `InternalNotes` field encrypted with platform KMS key (only platform can access)
- `HIPAAData` field encrypted with compliance-specific key

**Proposed Solution: Combination of KeyId hint + Custom Resolver**

### Option A: KeyId Hint on Attribute
```csharp
[Encrypted(KeyId = "tenant")]     // Resolver gets hint "tenant"
public string SSN { get; set; }

[Encrypted(KeyId = "platform")]   // Resolver gets hint "platform"  
public string InternalNotes { get; set; }

[Encrypted]                       // No hint, uses default behavior
public string DefaultEncrypted { get; set; }
```

### Option C: Custom Resolver per Field
```csharp
[Encrypted(Resolver = typeof(ComplianceKeyResolver))]
public string HIPAAData { get; set; }
```

### Combined Approach
- Most fields use `KeyId` with the default resolver
- Default resolver interprets `KeyId` however it wants (tenant lookup, static mapping, etc.)
- Fields with exotic requirements can specify their own resolver type

### Interface Evolution
```csharp
public interface IKmsKeyResolver
{
    // Existing (backward compatible)
    string ResolveKeyId(string? contextId);
    
    // New overload with key hint from attribute
    string ResolveKeyId(string? contextId, string? keyHint) => ResolveKeyId(contextId);
}
```

### EncryptedAttribute Changes
```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EncryptedAttribute : Attribute
{
    // Existing
    public int CacheTtlSeconds { get; set; } = 300;
    
    // New: Key identifier hint passed to resolver
    public string? KeyId { get; set; }
    
    // New: Custom resolver type (must implement IKmsKeyResolver)
    public Type? Resolver { get; set; }
}
```

### Source Generator Changes
- Detect `KeyId` and `Resolver` properties on `[Encrypted]` attribute
- Pass `KeyId` to resolver's new overload
- If `Resolver` is specified, instantiate that resolver instead of default

### Open Questions
- How to handle resolver instantiation? DI? Activator.CreateInstance?
- Should custom resolvers be singletons or per-operation?
- How to validate resolver type at compile time (source generator)?

---

## Phase 3: Batch/Transaction Multi-Key Support (Future)

**Status:** Planned

**Problem Statement:**
A transaction or batch operation may span multiple tables with different ownership domains, each requiring different encryption keys. For example:
- Table A: Tenant data with tenant's KMS key
- Table B: Platform audit log with platform's KMS key
- Table C: Compliance data with compliance KMS key

**Key Insight:**
Encryption happens **before** the DynamoDB operation, so we can:
1. Collect all fields needing encryption across all items in the batch/transaction
2. Resolve all keys upfront (fail fast if any key is unavailable)
3. Encrypt all fields (potentially in parallel)
4. Then execute the DynamoDB transaction/batch

**Proposed Approach:**

### Pre-Resolution Phase
```csharp
// Pseudocode for transaction execution
public async Task ExecuteAsync()
{
    // 1. Collect all encryption requirements
    var encryptionTasks = CollectEncryptionRequirements(items);
    
    // 2. Resolve all keys upfront
    var keyResolutions = await ResolveAllKeysAsync(encryptionTasks);
    
    // 3. Fail fast if any key resolution failed
    if (keyResolutions.Any(r => r.Failed))
        throw new FieldEncryptionException("Key resolution failed", ...);
    
    // 4. Encrypt all fields (can parallelize)
    await EncryptAllFieldsAsync(encryptionTasks, keyResolutions);
    
    // 5. Execute DynamoDB transaction
    await ExecuteDynamoDbTransactionAsync();
}
```

### Benefits
- All-or-nothing: Either all keys resolve and all fields encrypt, or nothing happens
- No partial state: DynamoDB transaction never starts if encryption fails
- Parallelizable: Can encrypt fields for different items concurrently
- Clear errors: Know exactly which field/key failed before any DynamoDB calls

### Considerations
- Memory: Need to hold all encrypted data in memory before transaction
- Latency: Key resolution + encryption happens before DynamoDB call
- Caching: Data key caching becomes even more important for performance

### Open Questions
- Should we support partial success modes? (Probably not for transactions)
- How to handle very large batches? (Memory pressure)
- Should encryption be parallelized? (Probably yes, with configurable concurrency)

---

## Dependencies Between Phases

```
Phase 1 (Package Migration)
    │
    ▼
Phase 2 (Per-Field Keys)
    │
    ▼
Phase 3 (Batch/Transaction Multi-Key)
```

- Phase 2 depends on Phase 1 being complete (need working encryption first)
- Phase 3 depends on Phase 2 (need per-field key resolution to handle mixed batches)

---

## Timeline Estimates

| Phase | Estimated Effort | Priority |
|-------|-----------------|----------|
| Phase 1 | 2-3 days | High (blocking) |
| Phase 2 | 3-5 days | Medium |
| Phase 3 | 3-5 days | Medium |

---

## Notes from Discussion

- AOT compatibility is critical for all phases
- Cost reduction via caching is important
- The A+C combination (KeyId hint + custom Resolver) was preferred over other options
- Batch/transaction operations are guaranteed to span different key domains in real usage
