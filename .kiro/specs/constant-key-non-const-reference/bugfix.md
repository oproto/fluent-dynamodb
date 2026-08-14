# Bugfix Requirements Document

## Introduction

When a key property (`[PartitionKey]` or `[SortKey]`) uses expression-body (`=>`) or read-only auto-property (`{ get; }`) syntax with a reference to a `static readonly` field (rather than a `const` or string literal), the constant key detection in `EntityAnalyzer.DetectConstantKeyValue` silently fails. The generator then falls through to the normal code path, emitting property assignments in `FromDynamoDb()` that won't compile because the property has no setter, and generating CRUD methods that still require the key parameter instead of omitting it. This bugfix introduces diagnostic FDDB125 to make the requirement explicit and guards against generating uncompilable code for read-only key properties.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a key property uses expression-body syntax referencing a `static readonly` field (e.g., `public string Sk => DynamoDB.DefaultSortkeyValue;` where `DefaultSortkeyValue` is `static readonly`) THEN the system silently sets `IsConstantKey` to false and generates `entity.Sk = value;` in `FromDynamoDb()`, producing a compile error "Property or indexer cannot be assigned to -- it is read only"

1.2 WHEN a key property uses expression-body syntax referencing a non-const expression (property access, method call, or `static readonly` field) THEN the system generates `Get(pk, sk)`, `Delete(pk, sk)`, and `Update(pk, sk)` convenience methods requiring the key parameter instead of omitting it

1.3 WHEN a key property uses read-only auto-property syntax (`{ get; }`) with an initializer referencing a `static readonly` field (e.g., `public string Sk { get; } = DynamoDB.DefaultSortkeyValue;`) THEN the system silently sets `IsConstantKey` to false and generates an assignment to the read-only property in `FromDynamoDb()`, producing a compile error

1.4 WHEN `DetectConstantKeyValue` calls `SemanticModel.GetConstantValue()` on a `static readonly` field reference THEN the method returns null (only compile-time constants resolve), and no diagnostic is emitted to inform the user of the limitation

### Expected Behavior (Correct)

2.1 WHEN a key property uses expression-body syntax referencing a non-compile-time-constant expression AND the property is read-only (no setter) THEN the system SHALL emit diagnostic FDDB125 with severity Error on the property declaration, indicating that the value is not a compile-time constant and the user should use a string literal or `const` field reference

2.2 WHEN a key property uses read-only auto-property syntax with an initializer referencing a non-compile-time-constant expression THEN the system SHALL emit diagnostic FDDB125 with severity Error on the property declaration, with the same guidance message

2.3 WHEN diagnostic FDDB125 is emitted for a key property THEN the system SHALL NOT generate property assignment code in `FromDynamoDb()` for that property, preventing uncompilable output

2.4 WHEN diagnostic FDDB125 is emitted for a key property THEN the system SHALL NOT generate convenience methods that include that key as a parameter (generation for the entity should halt or gracefully degrade)

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a key property uses expression-body syntax returning a string literal (e.g., `public string Sk => "PROFILE";`) THEN the system SHALL CONTINUE TO detect it as a constant key with `IsConstantKey = true` and `ConstantKeyValue = "PROFILE"`

3.2 WHEN a key property uses expression-body syntax returning a reference to a `const` field (e.g., `public string Sk => Constants.ProfileKey;` where `ProfileKey` is `const string`) THEN the system SHALL CONTINUE TO resolve the value via `GetConstantValue()` and detect it as a constant key

3.3 WHEN a key property uses read-only auto-property syntax with a string literal initializer (e.g., `public string Sk { get; } = "PROFILE";`) THEN the system SHALL CONTINUE TO detect it as a constant key

3.4 WHEN a key property uses read-only auto-property syntax with a `const` field reference initializer THEN the system SHALL CONTINUE TO resolve the value and detect it as a constant key

3.5 WHEN a key property has a normal getter and setter (e.g., `public string Sk { get; set; }`) THEN the system SHALL CONTINUE TO treat it as a normal mutable key property with no constant key detection attempted

3.6 WHEN a key property is successfully detected as a constant key THEN the system SHALL CONTINUE TO omit it from convenience method parameters, skip assignment in `FromDynamoDb()`, and emit the constant value directly in `ToDynamoDb()`
