# Bugfix Requirements Document

## Introduction

The source generator's `MapperGenerator.GenerateKeyPrefixApplication` emits code that passes non-string key property values directly to `KeyPrefixHelper.ApplyKeyPrefix(string, ...)` without first converting them to a string. This produces uncompilable generated code for any entity using a non-string key type (enum, DateTime, Guid, Ulid, numeric) with a prefix configured via `[PartitionKey(Prefix = "...")]` or `[SortKey(Prefix = "...")]`. The type-to-string serialization logic already exists in `KeysGenerator.GetValueExpression` and is used throughout key building, but the prefix application path in `MapperGenerator` does not use it.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a key property has a non-string type (e.g., enum) and a configured prefix THEN the system generates code that passes the raw typed value directly to `ApplyKeyPrefix(string, ...)`, producing a compilation error due to type mismatch

1.2 WHEN a key property has type `DateTime` or `DateTimeOffset` and a configured prefix THEN the system generates code that passes the raw DateTime/DateTimeOffset value to `ApplyKeyPrefix(string, ...)` without applying the expected format string conversion

1.3 WHEN a key property has type `Guid` or `Ulid` and a configured prefix THEN the system generates code that passes the raw Guid/Ulid value to `ApplyKeyPrefix(string, ...)` without calling `.ToString()`

1.4 WHEN a key property has a numeric type (int, long, decimal, etc.) and a configured prefix THEN the system generates code that passes the raw numeric value to `ApplyKeyPrefix(string, ...)` without calling `.ToString()`

### Expected Behavior (Correct)

2.1 WHEN a key property has a non-string type (e.g., enum) and a configured prefix THEN the system SHALL generate code that converts the value to a string using `.ToString()` before passing it to `ApplyKeyPrefix`

2.2 WHEN a key property has type `DateTime` or `DateTimeOffset` and a configured prefix THEN the system SHALL generate code that converts the value using the appropriate format string (e.g., `.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")`) before passing it to `ApplyKeyPrefix`

2.3 WHEN a key property has type `Guid` or `Ulid` and a configured prefix THEN the system SHALL generate code that converts the value using `.ToString()` before passing it to `ApplyKeyPrefix`

2.4 WHEN a key property has a numeric type (int, long, decimal, etc.) and a configured prefix THEN the system SHALL generate code that converts the value using `.ToString()` before passing it to `ApplyKeyPrefix`

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a key property has type `string` and a configured prefix THEN the system SHALL CONTINUE TO pass the value directly to `ApplyKeyPrefix` without any conversion

3.2 WHEN a key property has any type but no prefix configured THEN the system SHALL CONTINUE TO generate the existing key-building code without involving `ApplyKeyPrefix`

3.3 WHEN the `KeysGenerator` builds composite/concatenated key expressions THEN the system SHALL CONTINUE TO use `GetValueExpression` with the same conversion logic as before (no behavioral change to existing callers)
