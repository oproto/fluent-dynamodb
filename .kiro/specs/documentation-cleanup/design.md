# Design Document

## Overview

This design addresses documentation cleanup to correct inaccurate API examples and reorganize the STSIntegration.md document into two focused topics. The changes ensure documentation accurately reflects the current API patterns for batch operations, transactions, and client configuration.

## Architecture

The documentation cleanup involves:

1. **Pattern Corrections**: Replace incorrect constructor-based patterns with static entry point patterns
2. **Document Reorganization**: Split STSIntegration.md into two focused documents
3. **Changelog Updates**: Add new entries documenting all corrections

### Incorrect vs Correct Patterns

| Operation | Incorrect Pattern | Correct Pattern |
|-----------|-------------------|-----------------|
| Batch Write | `new BatchWriteItemRequestBuilder(client)` | `DynamoDbBatch.Write.Add(...).ExecuteAsync()` |
| Batch Get | `new BatchGetItemRequestBuilder(client)` | `DynamoDbBatch.Get.Add(...).ExecuteAsync()` |
| Transaction Write | `new TransactWriteItemsRequestBuilder(client)` | `DynamoDbTransactions.Write.Add(...).ExecuteAsync()` |
| Transaction Get | `new TransactGetItemsRequestBuilder(client)` | `DynamoDbTransactions.Get.Add(...).ExecuteAsync()` |
| Transaction Execute | `.CommitAsync()` | `.ExecuteAsync()` |
| Custom Client | Constructor parameter | `.WithClient(client)` or `ExecuteAsync(client)` |

## Components and Interfaces

### New Documents

#### ClientConfiguration.md
Covers client configuration scenarios that are applied at table creation time:
- Custom timeouts and retry settings
- LocalStack/DynamoDB Local for development
- Multi-region deployments (static routing)
- Proxy settings
- Connection pooling configuration

#### ScopedSecurity.md
Covers the `WithClient()` method for per-request client customization:
- STS-scoped credentials for multi-tenancy
- Per-request client swapping
- Tenant isolation patterns
- Security best practices
- Credential caching strategies

### Files to Update

| File | Changes Required |
|------|------------------|
| `docs/advanced-topics/STSIntegration.md` | Delete after content migration |
| `docs/advanced-topics/README.md` | Update to reference new documents |
| `docs/core-features/BasicOperations.md` | Fix batch operation examples |
| `docs/advanced-topics/PerformanceOptimization.md` | Fix batch operation examples |
| `docs/advanced-topics/GlobalSecondaryIndexes.md` | Fix batch get example |
| `docs/advanced-topics/CompositeEntities.md` | Fix batch/transaction examples, CommitAsync |
| `docs/advanced-topics/MultiEntityTables.md` | Fix CommitAsync to ExecuteAsync |
| `docs/getting-started/SingleEntityTables.md` | Fix CommitAsync to ExecuteAsync |
| `docs/QUICK_REFERENCE.md` | Fix batch/transaction examples |
| `docs/DeveloperGuide.md` | Fix transaction examples |
| `docs/DOCUMENTATION_CHANGELOG.md` | Add new correction entries |

## Data Models

No data model changes required - this is documentation only.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Since this is a documentation cleanup task, the correctness properties are verification-based rather than property-based tests:

**Property 1: No incorrect batch builder patterns**
*For any* documentation file in the docs folder, searching for `new BatchWriteItemRequestBuilder` or `new BatchGetItemRequestBuilder` should return zero matches.
**Validates: Requirements 1.1, 1.2**

**Property 2: No incorrect transaction builder patterns**
*For any* documentation file in the docs folder, searching for `new TransactWriteItemsRequestBuilder` or `new TransactGetItemsRequestBuilder` should return zero matches.
**Validates: Requirements 2.1, 2.2**

**Property 3: No CommitAsync in documentation**
*For any* documentation file in the docs folder (excluding DOCUMENTATION_CHANGELOG.md historical entries), searching for `CommitAsync` should return zero matches.
**Validates: Requirements 2.4**

**Property 4: STSIntegration.md removed**
*For any* file listing of docs/advanced-topics, the file `STSIntegration.md` should not exist.
**Validates: Requirements 3.3**

**Property 5: New documents exist**
*For any* file listing of docs/advanced-topics, both `ClientConfiguration.md` and `ScopedSecurity.md` should exist.
**Validates: Requirements 3.1, 3.2**

## Error Handling

Not applicable for documentation changes.

## Testing Strategy

### Verification Approach

Since this is documentation cleanup, testing consists of:

1. **Pattern Search Verification**: Use grep to verify incorrect patterns are removed
2. **File Existence Verification**: Verify new files exist and old file is removed
3. **Link Verification**: Ensure all internal documentation links remain valid

### Verification Commands

```bash
# Verify no incorrect batch patterns
grep -r "new BatchWriteItemRequestBuilder\|new BatchGetItemRequestBuilder" docs/

# Verify no incorrect transaction patterns  
grep -r "new TransactWriteItemsRequestBuilder\|new TransactGetItemsRequestBuilder" docs/

# Verify no CommitAsync (excluding changelog historical entries)
grep -r "CommitAsync" docs/ --include="*.md" | grep -v "DOCUMENTATION_CHANGELOG.md"

# Verify STSIntegration.md is removed
ls docs/advanced-topics/STSIntegration.md 2>/dev/null && echo "FAIL: File still exists" || echo "PASS: File removed"

# Verify new files exist
ls docs/advanced-topics/ClientConfiguration.md docs/advanced-topics/ScopedSecurity.md
```

## Additional Documentation Style Issues

### Issue: Non-existent Generic Type
`docs/reference/AdvancedTypesMigration.md` uses `DynamoDbTableBase<Product>` which doesn't exist. `DynamoDbTableBase` is not generic.

### Issue: Verbose API Patterns
Several documentation files use verbose patterns instead of the preferred concise patterns:

| Verbose Pattern | Preferred Pattern |
|-----------------|-------------------|
| `table.Get<User>().WithKey(...)` | `table.Users.Get(userId)` |
| `table.Query<User>().Where($"...")` | `table.Users.Query(x => x.Property == value)` |
| `table.Put.WithItem(user)` | `table.Users.Put(user)` |
| `UserKeys.Pk(userId)` (when no prefix) | `userId` (simple string) |

### Issue: DynamoDbTableBase as Field Type
Several files use `DynamoDbTableBase` as a field type when a concrete typed table class would be more realistic:
- `docs/reference/ErrorHandling.md`
- `docs/reference/AdoptionGuide.md`
- `docs/advanced-topics/PerformanceOptimization.md`
- `docs/CodeExamples.md`

### API Style Priority (from documentation.md steering)
1. **Lambda expressions** (preferred) - Type-safe with IntelliSense
2. **Format strings** (alternative) - Concise with placeholders
3. **Manual WithValue** (explicit control) - For complex scenarios

### Files Requiring Style Updates

| File | Issues |
|------|--------|
| `docs/reference/AdvancedTypesMigration.md` | Non-existent `DynamoDbTableBase<T>` generic |
| `docs/advanced-topics/ScopedSecurity.md` | Already rewritten - verify patterns |
| `docs/CodeExamples.md` | Direct DynamoDbTableBase instantiation |
| `docs/reference/ErrorHandling.md` | DynamoDbTableBase field type |
| `docs/reference/AdoptionGuide.md` | DynamoDbTableBase field type |
| `docs/advanced-topics/PerformanceOptimization.md` | DynamoDbTableBase field type |

## Document Content Outline

### ClientConfiguration.md Structure

```markdown
# Client Configuration

## Overview
- When to use custom client configuration
- Configuration vs per-request client swapping

## Development Environments
- DynamoDB Local setup
- LocalStack configuration
- Custom endpoints

## Custom Client Settings
- Timeout configuration
- Retry settings
- Connection pooling

## Multi-Region Deployments
- Static region routing
- Regional client creation

## Proxy Configuration
- Proxy settings for corporate environments
```

### ScopedSecurity.md Structure

```markdown
# Scoped Security with WithClient()

## Overview
- Purpose of WithClient() method
- Per-request client customization

## STS-Scoped Credentials
- Multi-tenancy with IAM role assumption
- Tenant isolation patterns
- Complete implementation example

## Using WithClient() in Operations
- Get, Put, Query, Update, Delete examples
- Batch operations with custom client
- Transaction operations with custom client

## Performance Considerations
- Client reuse strategies
- Credential caching
- Connection pooling

## Security Best Practices
- Principle of least privilege
- Session tags for audit
- External ID for cross-account
- Short-lived credentials
- Tenant access validation

## Troubleshooting
- Common errors and solutions
```
