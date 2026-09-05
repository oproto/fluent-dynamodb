# Bugfix Requirements Document

## Introduction

The source generator's typed convenience overload generation does not check whether a sort key (or partition key) is constant (`IsConstantKey`) before including it as a parameter and forwarding it in the delegation call. This produces uncompilable code (CS7036, CS1503) when an entity has a `[Computed]` partition key AND a constant sort key (expression-body `=>` or read-only auto-property `{ get; } =`). The raw Get/Delete/Update methods correctly detect the constant SK and emit single-parameter signatures, but the typed overloads incorrectly include the constant SK as a parameter and delegate to a 2-argument raw method that doesn't exist.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an entity has a `[Computed]` partition key and a constant sort key (expression-body or read-only auto-property) THEN the source generator emits typed convenience overloads that include the constant sort key as a `string sK` parameter, producing a method signature with an extra parameter that should not exist

1.2 WHEN the typed convenience overload for Get is emitted with the extra `sK` parameter THEN it delegates to `Get(computedPk, sK)` which does not exist (the raw method is `Get(string pk)` only), causing CS7036 compilation error

1.3 WHEN the typed convenience overload for Delete is emitted with the extra `sK` parameter THEN it delegates to `Delete(computedPk, sK)` which does not exist (the raw method is `Delete(string pk)` only), causing CS7036 compilation error

1.4 WHEN the typed convenience overload for Update is emitted with the extra `sK` parameter THEN it delegates to `Update(computedPk, sK)` which resolves to `Update(string, KeyCondition)` and the string `sK` cannot convert to `KeyCondition`, causing CS1503 compilation error

1.5 WHEN the typed convenience overload for ConditionCheck is emitted with the extra `sK` parameter THEN it delegates to `ConditionCheck(computedPk, sK)` which does not exist (the raw method omits the constant SK), causing a compilation error

1.6 WHEN `GetStandardOverloadParameters()` is called for an entity with a constant sort key THEN it unconditionally includes `sK` in the standard parameter list, producing an incorrect parameter count for ambiguity comparison (defensive issue — does not currently cause incorrect behavior because the count mismatch happens to prevent ambiguity suppression, but is logically wrong)

### Expected Behavior (Correct)

2.1 WHEN an entity has a `[Computed]` partition key and a constant sort key THEN the source generator SHALL emit typed convenience overloads that omit the constant sort key parameter entirely, matching the raw method's signature that also omits the constant SK

2.2 WHEN the typed convenience overload for Get is emitted for an entity with a constant sort key THEN it SHALL delegate to `Get(computedPk)` (single argument), which correctly auto-fills the constant SK value

2.3 WHEN the typed convenience overload for Delete is emitted for an entity with a constant sort key THEN it SHALL delegate to `Delete(computedPk)` (single argument), which correctly auto-fills the constant SK value

2.4 WHEN the typed convenience overload for Update is emitted for an entity with a constant sort key THEN it SHALL delegate to `Update(computedPk)` (single argument), which correctly auto-fills the constant SK value

2.5 WHEN the typed convenience overload for ConditionCheck is emitted for an entity with a constant sort key THEN it SHALL delegate to `ConditionCheck(computedPk)` (single argument), which correctly auto-fills the constant SK value

2.6 WHEN `GetStandardOverloadParameters()` is called for an entity with a constant sort key THEN it SHALL omit the constant sort key from the standard parameter list for defensive consistency with the raw method signatures

2.7 WHEN an entity has a constant partition key (defensive case, currently blocked by FDDB120 preventing constant+computed on same key) THEN `GetTypedOverloadParameters()` and `GetStandardOverloadParameters()` SHALL omit the constant partition key parameter for defensive correctness

### Unchanged Behavior (Regression Prevention)

3.1 WHEN an entity has a `[Computed]` partition key and a normal (non-constant, non-computed) sort key THEN the source generator SHALL CONTINUE TO emit typed convenience overloads that include the sort key as a `string sK` parameter and delegate with both `computedPk` and `sK` arguments

3.2 WHEN an entity has a `[Computed]` partition key and a `[Computed]` sort key THEN the source generator SHALL CONTINUE TO emit typed convenience overloads with all computed source property parameters for both keys and delegate with `computedPk` and `computedSk` arguments

3.3 WHEN an entity has no `[Computed]` keys THEN the source generator SHALL CONTINUE TO not emit typed convenience overloads (no change to non-computed entity behavior)

3.4 WHEN the typed overload would be ambiguous with the standard overload (same parameter count and types) THEN `WouldBeAmbiguous()` SHALL CONTINUE TO suppress the typed overload generation

3.5 WHEN the raw Get/Delete/Update/ConditionCheck methods handle constant sort keys THEN they SHALL CONTINUE TO emit single-parameter signatures with auto-filled constant SK values (no change to raw method generation)

3.6 WHEN typed async convenience methods (GetAsync, DeleteAsync) and FluentResults methods (GetAsyncResult, DeleteAsyncResult) are generated THEN they SHALL CONTINUE TO correctly delegate to the typed builder overloads using the same parameter list from `GetTypedOverloadParameters()`
