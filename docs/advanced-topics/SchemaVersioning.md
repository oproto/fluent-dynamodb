---
title: "Schema Versioning"
category: "advanced-topics"
order: 65
keywords: ["schema", "version", "versioning", "migration", "breaking-change", "FDDB110", "FDDB111", "FDDB112", "FDDB113", "FDDB114", "FDDB115", "FDDB116"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Schema Versioning

# Schema Versioning

Declare a target schema version for your project so the source generator knows which code shape to emit. This enables the library to evolve its generated output without silently breaking your code on NuGet package upgrade.

## Overview

The **schema version** is a major.minor pair that identifies the shape of generated code (interfaces, accessors, builders). It is independent of the NuGet package version — multiple package versions can support the same schema version.

By declaring a schema version, you create an explicit contract:
- **The generator promises** to emit code compatible with the declared version
- **You promise** to use only APIs from that version

This means you can update the NuGet package for bug fixes and performance improvements without being forced to adopt new generated code shapes until you're ready.

### Key Concepts

| Concept | Description |
|---------|-------------|
| Schema Version | A major.minor pair (e.g., `1.0`) identifying the generated code shape |
| Current Version | The latest version the installed generator can emit |
| Minimum Supported Version | The oldest version the generator still supports |
| Support Window | The generator supports at most two concurrent major versions (N and N-1) |

## Table of Contents

- [Getting Started](#getting-started)
- [How It Works](#how-it-works)
- [Versioning Semantics](#versioning-semantics)
- [Support Window Policy](#support-window-policy)
- [Diagnostic Reference](#diagnostic-reference)
- [Migration Guidance](#migration-guidance)
- [FAQ](#faq)

---

## Getting Started

Add the schema version attribute to any file in your project (commonly `AssemblyInfo.cs` or `GlobalUsings.cs`):

```csharp
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```

That's it. This tells the source generator you're targeting schema version 1.0. The generator will emit code compatible with that version and warn you if a newer version becomes available.

### Why Add It Now?

Without the attribute, the generator defaults to schema version 1.0 and emits a compiler warning on every build. Adding the attribute:

1. **Suppresses the warning** — cleaner build output
2. **Makes the contract explicit** — future you (or teammates) will immediately see which version the project targets
3. **Enables safe upgrades** — when schema version 2.0 arrives, you can update the NuGet package without being forced to adopt new code shapes. Bump the attribute when you're ready to migrate.

---

## How It Works

When the source generator runs, it performs the following steps before generating any entity code:

```
1. Look for [assembly: FluentDynamoDbSchemaVersion(major, minor)]
2. If missing → default to 1.0, emit FDDB110 warning
3. If present → validate major >= 1 and minor >= 0
4. Compare declared version against supported range:
   - Below minimum supported → FDDB111 error, halt generation
   - Above current → FDDB112 error, halt generation
   - Older but supported → FDDB113 info, generate with declared version shape
   - Matches current → generate with current shape
```

Error diagnostics halt code generation entirely. Warning and info diagnostics allow generation to proceed.

---

## Versioning Semantics

Schema versions follow semantic conventions similar to SemVer but applied to the **shape of generated code**, not the library's runtime behavior.

### Major Version (Breaking Changes)

The major version is incremented when any of the following breaking changes are introduced to generated code:

| Change Type | Example |
|-------------|---------|
| Interface removal | Removing `IUserEntity` from generated output |
| Method or property removal | Removing `GetPartitionKey()` from generated entity |
| Rename | Renaming `ToDynamoDb()` to `Serialize()` |
| Return type change | Changing `Task<User>` to `ValueTask<User>` |
| Parameter list change | Adding a required parameter to a generated method |
| Base type change | Changing the base class of a generated table class |
| Interface change | Removing an implemented interface from a generated class |

**When major is incremented, minor resets to 0.**

### Minor Version (Additive Changes)

The minor version is incremented for additive-only changes that do not break existing code:

| Change Type | Example |
|-------------|---------|
| New method | Adding `ToJsonAsync()` to generated entities |
| New interface | Adding `ISerializableEntity` as an additional implemented interface |
| New class/record | Adding a new generated helper class |

**Key guarantee:** Code that compiled against any prior minor version within the same major version continues to compile without modification when the minor version is incremented.

---

## Support Window

The current schema version is **1.0**. Since no breaking changes have been introduced yet, only version 1.0 is recognized.

The generator includes a `MinimumSupported` and `Current` version pair. When a future major version is introduced, the support window policy (how many prior versions remain supported and for how long) will be defined at that time. The infrastructure is in place to support older versions gracefully — the `FDDB111` and `FDDB113` diagnostics handle the "too old" and "older but still supported" cases respectively.

For now, declare `[assembly: FluentDynamoDbSchemaVersion(1, 0)]` and you're set.

---

## Diagnostic Reference

All diagnostics use category `FluentDynamoDb` and are emitted at most once per compilation.

### FDDB110 — Missing Schema Version Attribute

| Property | Value |
|----------|-------|
| **Severity** | Warning |
| **Message** | "Assembly does not declare [FluentDynamoDbSchemaVersion]. Defaulting to schema version 1.0. Add [assembly: FluentDynamoDbSchemaVersion(1, 0)] to suppress this warning." |
| **Trigger** | No `[assembly: FluentDynamoDbSchemaVersion]` found in the compilation |
| **Effect** | Generation proceeds using default version 1.0 |

**Resolution:**

```csharp
// Add to any file in your project (e.g., AssemblyInfo.cs)
[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```

---

### FDDB111 — Unsupported Old Version

| Property | Value |
|----------|-------|
| **Severity** | Error |
| **Message** | "Declared schema version {declared} is no longer supported. Minimum supported version is {minimum}. See {url} for migration guidance." |
| **Trigger** | Declared version is below the minimum supported version |
| **Effect** | Code generation halted — no entity code is produced |

**Resolution:**

1. Update your schema version attribute to the minimum supported version (or higher)
2. Follow the [migration guidance](#migration-guidance) for the version you're upgrading to
3. Alternatively, pin to an older NuGet package version that still supports your declared schema version

---

### FDDB112 — Unrecognized Future Version

| Property | Value |
|----------|-------|
| **Severity** | Error |
| **Message** | "Declared schema version {declared} is not recognized. Maximum supported version is {maximum}. Update the Oproto.FluentDynamoDb package to a version that supports schema {declared}." |
| **Trigger** | Declared version is newer than what the installed generator supports |
| **Effect** | Code generation halted — no entity code is produced |

**Resolution:**

Update the `Oproto.FluentDynamoDb` NuGet package to a version that supports the declared schema version:

```bash
dotnet add package Oproto.FluentDynamoDb
```

If you intentionally declared a version that doesn't exist yet, lower the attribute to match the installed generator's current version.

---

### FDDB113 — Older-but-Supported Version

| Property | Value |
|----------|-------|
| **Severity** | Info |
| **Message** | "Schema version {declared} is supported but not current. Consider upgrading to {current} for the latest generated code improvements. See {url}." |
| **Trigger** | Declared version is older than current but within the support window |
| **Effect** | Generation proceeds using the declared version's code shape |

**Resolution:**

This is informational only. No action required. When you're ready to adopt newer generated code shapes:

1. Review the upgrade guide linked in the diagnostic
2. Update the attribute: `[assembly: FluentDynamoDbSchemaVersion(2, 0)]`
3. Fix any compilation errors from changed generated code shapes

---

### FDDB114 — Invalid Major Version

| Property | Value |
|----------|-------|
| **Severity** | Error |
| **Message** | "FluentDynamoDbSchemaVersion major version must be at least 1, but was {value}." |
| **Trigger** | Major version in the attribute is less than 1 |
| **Effect** | Code generation halted — no entity code is produced |

**Resolution:**

Fix the attribute to use a major version of 1 or greater:

```csharp
// ❌ Invalid
[assembly: FluentDynamoDbSchemaVersion(0, 1)]

// ✅ Valid
[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```

---

### FDDB115 — Invalid Minor Version

| Property | Value |
|----------|-------|
| **Severity** | Error |
| **Message** | "FluentDynamoDbSchemaVersion minor version must be at least 0, but was {value}." |
| **Trigger** | Minor version in the attribute is less than 0 |
| **Effect** | Code generation halted — no entity code is produced |

**Resolution:**

Fix the attribute to use a minor version of 0 or greater:

```csharp
// ❌ Invalid
[assembly: FluentDynamoDbSchemaVersion(1, -1)]

// ✅ Valid
[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```

---

### FDDB116 — Duplicate Attribute

| Property | Value |
|----------|-------|
| **Severity** | Error |
| **Message** | "Multiple [FluentDynamoDbSchemaVersion] attributes detected. Remove duplicate declarations." |
| **Trigger** | More than one `[assembly: FluentDynamoDbSchemaVersion]` found (possible via IL manipulation since `AllowMultiple = false` prevents this in normal C#) |
| **Effect** | Code generation halted — no entity code is produced |

**Resolution:**

Remove duplicate schema version attributes. Ensure only one declaration exists across all files in the project:

```bash
# Find all declarations
grep -r "FluentDynamoDbSchemaVersion" --include="*.cs" .
```

Keep only one and remove the rest.

---

### Diagnostic Summary Table

| Code | Severity | Halts Generation | Description |
|------|----------|:----------------:|-------------|
| FDDB110 | Warning | No | No schema version declared, defaulting to 1.0 |
| FDDB111 | Error | Yes | Declared version too old (below minimum) |
| FDDB112 | Error | Yes | Declared version too new (above current) |
| FDDB113 | Info | No | Declared version is older but still supported |
| FDDB114 | Error | Yes | Major version invalid (< 1) |
| FDDB115 | Error | Yes | Minor version invalid (< 0) |
| FDDB116 | Error | Yes | Multiple attributes found — ambiguous declaration |

---

## Migration Guidance

When a new major schema version is released, follow this process to upgrade:

### Step 1: Read the Release Notes

Each major schema version bump includes a detailed migration guide listing every breaking change. The guide URL is included in the FDDB113 diagnostic message.

### Step 2: Update the NuGet Package

```bash
dotnet add package Oproto.FluentDynamoDb
```

Ensure the new package version supports your target schema version.

### Step 3: Build Without Changing the Attribute

Your project should still compile because the new package supports your current (N-1) version. You'll see the FDDB113 info diagnostic suggesting an upgrade.

### Step 4: Update the Attribute

```csharp
// Before
[assembly: FluentDynamoDbSchemaVersion(1, 0)]

// After
[assembly: FluentDynamoDbSchemaVersion(2, 0)]
```

### Step 5: Fix Compilation Errors

The generated code now uses the new shape. Common changes include:
- Renamed methods or properties
- Changed return types
- New required interfaces to implement
- Modified builder patterns

Follow the migration guide for specific instructions on each change.

### Step 6: Run Tests

```bash
dotnet test
```

Verify all tests pass with the new generated code shape.

### Migration Template (for future version bumps)

```markdown
## Migrating from Schema Version X.0 to Y.0

### Breaking Changes
1. [Change description] — [What to do]
2. [Change description] — [What to do]

### New Features (available after upgrading)
1. [Feature description]
2. [Feature description]

### Steps
1. Update NuGet package: `dotnet add package Oproto.FluentDynamoDb --version Z.Z.Z`
2. Change attribute: `[assembly: FluentDynamoDbSchemaVersion(Y, 0)]`
3. [Specific fix instructions per breaking change]
4. Run tests: `dotnet test`
```

---

## FAQ

### Does the schema version affect runtime behavior?

No. The schema version only affects what code the source generator produces at compile time. There is no runtime cost or behavior change.

### Can I skip major versions?

Yes. You can upgrade directly from schema version 1.0 to 3.0 as long as the installed generator supports it. However, you'll need to address all breaking changes from every skipped version.

### What happens if I don't add the attribute?

The generator defaults to schema version 1.0 and emits a FDDB110 warning on every build. Everything works, but you'll have a persistent warning.

### Is the schema version stored in the compiled assembly?

Yes. The `[assembly: FluentDynamoDbSchemaVersion(1, 0)]` attribute is embedded in the assembly metadata like any other assembly-level attribute. However, it is only used at compile time by the source generator.

### Can different projects in a solution use different schema versions?

Yes. Each project declares its own schema version independently. This can be useful during incremental migration of a large solution.

### What if the NuGet package version and schema version get out of sync?

The NuGet package version and schema version are intentionally independent. A single NuGet package version supports a range of schema versions (the current and at least one prior major). Update the NuGet package for bug fixes; update the schema version when you're ready to adopt new code shapes.

---

## See Also

- **[Diagnostic Reference: FDDB110](../diagnostics/FDDB/FDDB110.md)** — Missing schema version attribute
- **[Diagnostic Reference: FDDB111](../diagnostics/FDDB/FDDB111.md)** — Unsupported old version
- **[Diagnostic Reference: FDDB112](../diagnostics/FDDB/FDDB112.md)** — Unrecognized future version
- **[Diagnostic Reference: FDDB113](../diagnostics/FDDB/FDDB113.md)** — Older-but-supported version
- **[Diagnostic Reference: FDDB114](../diagnostics/FDDB/FDDB114.md)** — Invalid major version
- **[Diagnostic Reference: FDDB115](../diagnostics/FDDB/FDDB115.md)** — Invalid minor version
- **[Diagnostic Reference: FDDB116](../diagnostics/FDDB/FDDB116.md)** — Duplicate attribute
- **[Source Generator Guide](../SourceGeneratorGuide.md)** — How the source generator works
- **[Attribute Reference](../reference/AttributeReference.md)** — Complete attribute documentation

---

[Back to Advanced Topics](README.md) | [Back to Documentation Home](../README.md)
