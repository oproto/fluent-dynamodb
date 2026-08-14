# Requirements Document

## Introduction

Eliminate the split between `Pk()`/`Sk()` and `BuildPk()`/`BuildSk()` in the generated `Keys` class for the Oproto.FluentDynamoDb library. A single set of methods — `Pk()` and `Sk()` — should handle both prefix-based and computed key construction. Remove `BuildPk()`/`BuildSk()` and `Key()` entirely.

## Glossary

- **Keys_Class**: The source-generated static partial class nested inside each entity that provides key construction helper methods (e.g., `Pk()`, `Sk()`)
- **Generator**: The Roslyn source generator (`KeysGenerator.cs`) that emits the `Keys` class code at compile time
- **Prefix_Key**: A key where a fixed prefix string is prepended to the user-supplied value (e.g., `"CUSTOMER#" + value`)
- **Computed_Key**: A key constructed from multiple source properties via a format string (e.g., `string.Format("INVOICE#{0}#LINE#{1}", invoiceNumber, lineNumber)`)
- **Constant_Key**: A key whose value is fixed at compile time (expression-body property or read-only auto-property)
- **Bare_Key**: A key with no prefix and no `[Computed]` attribute — a passthrough method would be useless
- **GSI**: Global Secondary Index — a DynamoDB index with its own partition and sort key definitions
- **Typed_Overload**: Generated convenience methods for Get/Delete/Update that accept individual component parameters instead of raw key strings

## Requirements

### Requirement 1: Unified Pk()/Sk() for prefix-based keys

**User Story:** As a developer, I want `Pk()` and `Sk()` methods to handle prefix-based keys, so that I have a single consistent API for constructing keys.

#### Acceptance Criteria

1. WHEN a partition key property has `[PartitionKey(Prefix = "X")]` and no `[Computed]` attribute, THE Keys_Class SHALL generate a `Pk(value)` method that prepends the prefix to the value
2. WHEN a sort key property has `[SortKey(Prefix = "X")]` and no `[Computed]` attribute, THE Keys_Class SHALL generate an `Sk(value)` method that prepends the prefix to the value

### Requirement 2: Unified Pk()/Sk() for computed keys

**User Story:** As a developer, I want `Pk()` and `Sk()` methods to handle computed keys with multiple parameters, so that I do not need separate `BuildPk()`/`BuildSk()` methods.

#### Acceptance Criteria

1. WHEN a partition key property has a `[Computed]` attribute, THE Keys_Class SHALL generate a `Pk(component1, component2, ...)` method with one parameter per source property that applies the format string
2. WHEN a sort key property has a `[Computed]` attribute, THE Keys_Class SHALL generate an `Sk(component1, component2, ...)` method with one parameter per source property that applies the format string
3. THE Keys_Class SHALL use parameter names that match the source property names in camelCase
4. THE Keys_Class SHALL use parameter types that match the source property types

### Requirement 3: Remove BuildPk()/BuildSk()

**User Story:** As a developer, I want the `Build`-prefixed methods removed, so that there is only one way to construct keys.

#### Acceptance Criteria

1. THE Generator SHALL not contain a `GenerateComputedKeyBuilders` method
2. THE Keys_Class SHALL not contain any method with a "Build" prefix for main table keys

### Requirement 4: Remove Key() composite method

**User Story:** As a developer, I want the `Key()` composite method removed, so that I use `Pk()` and `Sk()` independently.

#### Acceptance Criteria

1. THE Generator SHALL not generate a `Key()` method in the Keys_Class via `GenerateCompositeKeyBuilder`
2. THE Keys_Class SHALL not contain a `Key()` method for any entity

### Requirement 5: No passthrough methods for computed keys

**User Story:** As a developer, I want to avoid useless single-param passthrough methods when a computed key exists, so that the API is not confusing.

#### Acceptance Criteria

1. WHEN a sort key property has `[Computed]` and no `[SortKey(Prefix)]`, THE Generator SHALL not produce a single-param `Sk(string)` passthrough method
2. WHEN a partition key property has `[Computed]` and no `[PartitionKey(Prefix)]`, THE Generator SHALL not produce a single-param `Pk(string)` passthrough method

### Requirement 6: Constant keys remain unchanged

**User Story:** As a developer, I want constant key generation to remain unchanged, so that existing constant key patterns continue to work.

#### Acceptance Criteria

1. WHEN a key property is a constant (expression-body or read-only auto-property), THE Keys_Class SHALL generate a static property (not a method) for that key

### Requirement 7: Bare keys produce no method

**User Story:** As a developer, I want bare keys (no prefix, no computed) to produce no method, so that useless passthrough methods are not generated.

#### Acceptance Criteria

1. WHEN a sort key has no prefix and no `[Computed]` attribute, THE Keys_Class SHALL not generate an `Sk()` method
2. WHEN a partition key has no prefix and no `[Computed]` attribute, THE Keys_Class SHALL not generate a `Pk()` method

### Requirement 8: GSI key builders follow the same unification

**User Story:** As a developer, I want GSI key builder nested classes to follow the same unified pattern, so that the API is consistent across main table and index keys.

#### Acceptance Criteria

1. THE Generator SHALL apply the same unification rules (no `Build` prefix, no passthrough, no `Key()`) to GSI key builder nested classes

### Requirement 9: Extraction helpers remain unchanged

**User Story:** As a developer, I want extraction helpers to remain unchanged, so that existing code using `ExtractPkComponents()`/`ExtractSkComponents()` continues to work.

#### Acceptance Criteria

1. THE Keys_Class SHALL continue to generate `ExtractPkComponents()` and `ExtractSkComponents()` methods without modification

### Requirement 10: Typed overload delegation

**User Story:** As a developer, I want typed Get/Delete/Update overloads to delegate to the unified `Pk()`/`Sk()` methods, so that all key construction flows through the same path.

#### Acceptance Criteria

1. THE Generator SHALL produce typed Get/Delete/Update overloads that delegate to `Keys.Pk(...)` and `Keys.Sk(...)` instead of `Keys.BuildPk(...)` and `Keys.BuildSk(...)`

### Requirement 11: Update consuming code

**User Story:** As a developer, I want all example projects, tests, and documentation updated to use the unified API, so that the codebase is consistent.

#### Acceptance Criteria

1. THE example projects SHALL use the unified `Pk()` and `Sk()` methods instead of `BuildPk()` and `BuildSk()`
2. THE test projects SHALL use the unified `Pk()` and `Sk()` methods instead of `BuildPk()` and `BuildSk()`
3. THE documentation and steering files SHALL reflect the unified API with no references to `BuildPk()` or `BuildSk()`
4. THE InvoiceManager tests and Program.cs SHALL use `Invoice.Keys.Sk(invoiceNumber)` which returns the formatted value
