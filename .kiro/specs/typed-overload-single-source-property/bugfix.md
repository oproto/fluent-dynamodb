# Bugfix Requirements Document

## Introduction

`ComputedOverloadEligibility.QualifiesForTypedOverload` incorrectly requires `ComputedKey.SourceProperties.Length >= 2` before considering an entity for typed parameter overloads. This prevents the source generator from emitting typed overloads for computed keys with a single non-string source property (e.g., `DateTime`), even though such overloads are clearly non-ambiguous with the standard `(string)` overload. The `WouldBeAmbiguous` method already handles true ambiguity cases, making the `>= 2` gate redundant and overly restrictive.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a computed key has exactly one non-string source property (e.g., `DateTime`) THEN the system does not generate a typed overload because `QualifiesForTypedOverload` returns `false` due to the `SourceProperties.Length >= 2` check

1.2 WHEN a computed key has exactly one string source property THEN the system does not generate a typed overload because `QualifiesForTypedOverload` returns `false` due to the `SourceProperties.Length >= 2` check (correct outcome but for the wrong reason — should be suppressed by `WouldBeAmbiguous` instead)

1.3 WHEN `OverloadParameterResolver.GetTypedOverloadParameters` encounters a computed key with exactly one source property THEN it falls through to the plain string parameter branch instead of resolving the source property's actual type

### Expected Behavior (Correct)

2.1 WHEN a computed key has exactly one non-string source property (e.g., `DateTime`) THEN the system SHALL generate a typed overload (Get, Delete, Update + async variants) since the overload signature differs from the standard `(string)` overload

2.2 WHEN a computed key has exactly one string source property THEN the system SHALL NOT generate a typed overload because `WouldBeAmbiguous` correctly detects that the typed parameter signature collides with the standard `(string)` overload

2.3 WHEN `OverloadParameterResolver.GetTypedOverloadParameters` encounters a computed key with one or more source properties THEN the system SHALL resolve each source property to its declared type regardless of the source property count

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a computed key has two or more source properties THEN the system SHALL CONTINUE TO generate typed overloads as it does today (existing multi-source behavior preserved)

3.2 WHEN a computed key has two or more source properties that all resolve to `string` THEN the system SHALL CONTINUE TO suppress the typed overload via `WouldBeAmbiguous`

3.3 WHEN an entity has no computed keys THEN the system SHALL CONTINUE TO skip typed overload generation entirely

3.4 WHEN `QualifiesForKeyInputMode` evaluates an entity that qualifies for typed overload but is ambiguous THEN the system SHALL CONTINUE TO proceed to prefix-based eligibility evaluation unchanged

3.5 WHEN `QualifiesForKeyInputMode` evaluates an entity with a non-ambiguous typed overload THEN the system SHALL CONTINUE TO return `false` (typed overload handles disambiguation)

---

### Bug Condition

```pascal
FUNCTION isBugCondition(X)
  INPUT: X of type EntityModel
  OUTPUT: boolean
  
  // Returns true when at least one key is computed with exactly one source property
  pk ← X.PartitionKeyProperty
  sk ← X.SortKeyProperty
  
  pkSingleSource ← (pk IS NOT NULL) AND (pk.IsComputed = true) AND (pk.ComputedKey.SourceProperties.Length = 1)
  skSingleSource ← (sk IS NOT NULL) AND (sk.IsComputed = true) AND (sk.ComputedKey.SourceProperties.Length = 1)
  
  RETURN pkSingleSource OR skSingleSource
END FUNCTION
```

### Fix Checking Property

```pascal
// Property: Fix Checking — Single non-string source property generates typed overload
FOR ALL X WHERE isBugCondition(X) DO
  result ← QualifiesForTypedOverload'(X)
  // After fix, eligibility is determined solely by IsComputed (gate removed)
  ASSERT result = true
  
  // Final overload emission depends on WouldBeAmbiguous
  IF NOT WouldBeAmbiguous(X) THEN
    ASSERT typed_overload_generated(X)
  ELSE
    ASSERT typed_overload_suppressed(X)
  END IF
END FOR
```

### Preservation Checking Property

```pascal
// Property: Preservation Checking — Multi-source and non-computed entities unchanged
FOR ALL X WHERE NOT isBugCondition(X) DO
  ASSERT QualifiesForTypedOverload(X) = QualifiesForTypedOverload'(X)
  ASSERT WouldBeAmbiguous(X) = WouldBeAmbiguous'(X)
  ASSERT QualifiesForKeyInputMode(X) = QualifiesForKeyInputMode'(X)
END FOR
```
