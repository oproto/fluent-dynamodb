# Documentation Standards

This steering file defines documentation standards for Oproto.FluentDynamoDb to ensure consistency, accuracy, and proper attribution across all documentation.

## API Style Priority

When documenting DynamoDB operations, always present examples in this priority order:

### 1. Lambda Expressions (PREFERRED)
Type-safe approach with full IntelliSense support. Always show this first.

```csharp
// PREFERRED: Lambda expression - type-safe with IntelliSense
var users = await table.Users.Query()
    .Where(x => x.PartitionKey == tenantId && x.SortKey.StartsWith("USER#"))
    .WithFilter(x => x.Status == "active")
    .ExecuteAsync();
```

### 2. Format Strings (ALTERNATIVE)
Concise approach using placeholders. Show second as an alternative.

```csharp
// ALTERNATIVE: Format string - concise with placeholders
var users = await table.Users.Query()
    .Where($"{UserFields.PartitionKey} = {0} AND begins_with({UserFields.SortKey}, {1})", tenantId, "USER#")
    .WithFilter($"{UserFields.Status} = {0}", "active")
    .ExecuteAsync();
```

### 3. Manual WithValue (EXPLICIT CONTROL)
Low-level approach for complex scenarios requiring explicit control. Show third.

```csharp
// EXPLICIT CONTROL: Manual - for complex scenarios
var users = await table.Users.Query()
    .Where("#pk = :pk AND begins_with(#sk, :skPrefix)")
    .WithAttribute("#pk", "pk")
    .WithAttribute("#sk", "sk")
    .WithValue(":pk", tenantId)
    .WithValue(":skPrefix", "USER#")
    .ExecuteAsync();
```

## Method Verification Rules

When documenting or verifying API methods exist, check ALL of these sources:

1. **Generated Code**: Check `obj/Debug/net8.0/generated/` for source-generated methods
2. **Base Classes**: Check `DynamoDbTableBase`, `DynamoDbIndex`, and other base classes
3. **Extension Methods**: Check `Requests/Extensions/` for extension method definitions
4. **Entity Accessors**: Check generated entity-specific accessor properties
5. **Request Builders**: Check builder classes in `Requests/` folder

### Verification Process
- Never assume a method doesn't exist based on checking only one source
- Generated code creates type-specific versions of generic extension methods
- Direct async methods (e.g., `GetAsync`, `QueryAsync`) are generated shortcuts
- Builder methods may be inherited from base classes or interfaces


## Organization Attribution Requirements

All main documentation files (README.md, docs/README.md) must include proper attribution.

### Required Attribution Elements

1. **Organization**: Oproto Inc
2. **Company Website**: https://oproto.com
3. **Developer Portal**: https://oproto.io
4. **Documentation Site**: https://fluentdynamodb.dev
5. **Maintainer**: Dan Guisinger (https://danguisinger.com)

### Standard Attribution Block

Use this format in README and documentation landing pages:

```markdown
## About

**Oproto.FluentDynamoDb** is developed and maintained by [Oproto Inc](https://oproto.com), 
a company building modern SaaS solutions for small business finance and accounting.

### Links
- 🏢 **Company**: [oproto.com](https://oproto.com)
- 👨‍💻 **Developer Portal**: [oproto.io](https://oproto.io)
- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev)

### Maintainer
- **Dan Guisinger** - [danguisinger.com](https://danguisinger.com)
```

### Attribution Placement
- Root `README.md`: Include full attribution block
- `docs/README.md`: Include consistent attribution mirroring root README
- Package metadata: Include organization in NuGet package properties


## Code Example Standards

### General Guidelines
- Always show lambda expression approach first (preferred)
- Include necessary `using` statements when relevant
- Use realistic, meaningful variable names
- Provide complete, compilable examples
- Add comments explaining the approach being demonstrated

### Example Structure
```csharp
// Brief comment explaining what this example demonstrates
using Oproto.FluentDynamoDb;

// Setup code if needed
var table = new MyTable(dynamoDbClient);

// The actual operation being demonstrated
var result = await table.Entity.Query()
    .Where(x => x.PartitionKey == "value")
    .ExecuteAsync();
```

### Code Block Formatting
- Use `csharp` language identifier for C# code blocks
- Use `bash` for command-line examples
- Use `json` for configuration examples
- Indent consistently (4 spaces)

## Documentation Update Guidelines

### When to Update Documentation

Documentation MUST be updated when:
1. **New Public APIs**: Any new public method, class, or interface
2. **Signature Changes**: Method parameter changes, return type changes
3. **Deprecations**: Mark deprecated APIs with migration guidance
4. **New Attributes**: Any new attribute that affects entity behavior
5. **Builder Modifications**: Changes to fluent builder patterns
6. **Breaking Changes**: Any change that affects existing user code

### Changelog Requirements

The `CHANGELOG.md` file MUST be updated as part of every pull request that includes:
- New features or functionality
- Bug fixes
- Breaking changes
- Deprecations
- Performance improvements
- Documentation improvements (significant ones)

**Changelog Format**: Follow [Keep a Changelog](https://keepachangelog.com/) conventions with sections for Added, Changed, Deprecated, Removed, Fixed, and Security.

**PR Requirement**: Pull requests will not be merged without an appropriate changelog entry.

### Documentation Changelog Requirements

In addition to the repository `CHANGELOG.md`, a separate **Documentation Changelog** is maintained at `docs/DOCUMENTATION_CHANGELOG.md`. This file is distinct from the repository changelog and serves a specific purpose:

**Purpose**: The documentation changelog tracks corrections and updates to documentation content, enabling teams maintaining derived documentation (e.g., website at fluentdynamodb.dev) to synchronize their content.

**When to Update**: The documentation changelog MUST be updated when:
- Correcting incorrect API method names or signatures in documentation
- Fixing outdated code patterns or examples
- Correcting return value access patterns
- Updating XML documentation comments in source code
- Any change that affects how users should write code based on documentation

**Entry Format**: Each entry MUST include:
1. **Date**: The date of the correction (YYYY-MM-DD format)
2. **File Path**: The path to the file that was corrected
3. **Before Pattern**: The incorrect code or text that was changed
4. **After Pattern**: The corrected code or text
5. **Reason**: Brief explanation of why the change was necessary

**Example Entry**:
```markdown
## [2024-12-01]

### File: docs/core-features/BasicOperations.md

**Before:**
```csharp
var response = await table.Users.Get(userId).ExecuteAsync();
```

**After:**
```csharp
var user = await table.Users.Get(userId).GetItemAsync();
```

**Reason:** ExecuteAsync() does not exist on GetItemRequestBuilder. The correct method is GetItemAsync().
```

**Important Distinction**: 
- `CHANGELOG.md` (repository root): Tracks code changes, features, bug fixes
- `docs/DOCUMENTATION_CHANGELOG.md`: Tracks documentation corrections only

### Documentation Review Checkpoints
- Before each release, review documentation accuracy
- After significant feature additions
- When user feedback indicates confusion
- Quarterly documentation audits

## Agent Hook Decision

### Decision: Manual Documentation Updates (No Automatic Hooks)

**Rationale**: After evaluation, automatic agent hooks for documentation updates are NOT implemented for the following reasons:

1. **Change Frequency**: Public API changes are infrequent enough that manual review is practical
2. **Quality Control**: Documentation requires human judgment for clarity and completeness
3. **Context Sensitivity**: Determining what documentation needs updating requires understanding the change's impact
4. **False Positives**: Automatic triggers would fire on internal changes that don't affect documentation

### Manual Review Process
Instead of automatic hooks, follow this manual process:
1. When making API changes, add documentation updates to the same PR
2. Use PR checklists to verify documentation is updated
3. Conduct periodic documentation audits (quarterly recommended)
4. Address user-reported documentation issues promptly

### Future Consideration
If the project grows significantly or API changes become more frequent, reconsider implementing:
- Hook on changes to `Attributes/` folder for new attribute documentation
- Hook on changes to `Requests/` folder for builder documentation
- Hook on changes to public interfaces for API reference updates
