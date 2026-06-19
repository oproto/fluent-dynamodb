# Computed-Extracted Circular Dependency False Positive - Bugfix Design

## Overview

DYNDB033 "Circular dependency detected between computed properties" is incorrectly triggered when `[Computed]` and `[Extracted]` are used on the same pair of properties in a bidirectional mapping pattern. The `[Computed]` attribute operates on the write path (ToDynamoDb — composing source properties into a computed key) while `[Extracted]` operates on the read path (FromDynamoDb — decomposing the computed key back into its components). These attributes operate in opposite data-flow directions and cannot form a circular dependency. The fix removes the direct Computed↔Extracted cross-check from `ValidateExtractedProperty`, relying on the existing DFS-based `ValidateComputedKeyCircularDependencies` method which correctly catches genuine Computed→Computed cycles.

## Glossary

- **Bug_Condition (C)**: A property has `[Extracted(nameof(Source), index)]` AND the source property has `[Computed(nameof(ExtractedProp), ...)]` where the extracted property is listed as a source of the computed property — forming a valid bidirectional mapping that is incorrectly flagged
- **Property (P)**: The system should allow the Computed↔Extracted round-trip pattern without emitting DYNDB033, recognizing that write-path and read-path never form a cycle
- **Preservation**: Genuine Computed→Computed circular dependencies must continue to be detected by the DFS-based `ValidateComputedKeyCircularDependencies` check; other Extracted validations (source exists, valid index) remain unchanged
- **EntityAnalyzer**: The class in `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs` that performs entity model validation including key dependency checks
- **ValidateExtractedProperty**: The method that validates individual extracted properties, currently containing the erroneous Computed↔Extracted cross-check
- **ValidateComputedKeyCircularDependencies**: The DFS-based method that detects genuine circular dependencies between computed properties by building a dependency graph of Computed→Computed edges only
- **ComputedKey**: Model representing a `[Computed]` attribute with `SourceProperties` (the properties combined on write) and `Separator`
- **ExtractedKey**: Model representing an `[Extracted]` attribute with `SourceProperty` (the computed property to decompose) and `Index` (which segment to extract)

## Bug Details

### Bug Condition

The bug manifests when a property has `[Extracted(nameof(ComputedProp), index)]` and the computed property has `[Computed(nameof(ExtractedProp), ...)]` — meaning the extracted property's name appears in the computed property's source list. The `ValidateExtractedProperty` method in `EntityAnalyzer.cs` contains a direct check that treats this as a circular dependency, but it is actually the intended round-trip pattern: source properties are composed into a computed key on write, and extracted back from that key on read.

**Formal Specification:**
```
FUNCTION isBugCondition(extractedProperty, entityModel)
  INPUT: extractedProperty of type PropertyModel (has ExtractedKey set)
         entityModel of type EntityModel (all properties in the entity)
  OUTPUT: boolean
  
  LET sourcePropertyName = extractedProperty.ExtractedKey.SourceProperty
  LET sourceProperty = entityModel.Properties.Find(p => p.PropertyName == sourcePropertyName)
  
  RETURN sourceProperty != null
         AND sourceProperty.IsComputed == true
         AND sourceProperty.ComputedKey.SourceProperties.Contains(extractedProperty.PropertyName)
END FUNCTION
```

### Examples

- **Standard round-trip pattern (false positive)**: Entity with `[Computed("Year", "Month", "Day", Separator = "#")] string Pk` and `[Extracted("Pk", 0)] int Year` — the system incorrectly reports DYNDB033 "Circular dependency: Year -> Pk -> Year" even though `[Computed]` only runs on write and `[Extracted]` only runs on read
- **Multi-segment extraction (false positive)**: Entity with `[Computed("TenantId", "UserId")] string Pk` and both `[Extracted("Pk", 0)] string TenantId` and `[Extracted("Pk", 1)] string UserId` — DYNDB033 fires for each extracted property that appears in the computed source list
- **Partial extraction (false positive)**: Entity with `[Computed("Category", "SubCategory", "ItemId")] string Sk` and `[Extracted("Sk", 0)] string Category` — DYNDB033 fires because "Category" appears in Sk's source list
- **Non-overlapping extraction (no bug)**: Entity with `[Computed("A", "B")] string Pk` and `[Extracted("Pk", 0)] string C` where C is NOT in the computed source list — no false positive because the check `computedSourceProperties.Contains("C")` is false

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Genuine Computed→Computed cycles (e.g., property A computed from B, property B computed from A) must continue to trigger DYNDB033 via `ValidateComputedKeyCircularDependencies`
- Multi-hop Computed chains forming cycles (A→B→C→A all via `[Computed]`) must continue to be detected
- Self-referencing computed properties (property computed from itself) must continue to trigger DYNDB034 via the existing self-reference check in `ValidateComputedProperty`
- Extracted properties referencing non-existent source properties must continue to trigger the appropriate diagnostic
- Extracted properties with invalid (negative) indices must continue to trigger the appropriate diagnostic
- All other EntityAnalyzer validations must remain completely unaffected

**Scope:**
All inputs that do NOT involve a `[Computed]`↔`[Extracted]` relationship on the same property pair should be completely unaffected by this fix. This includes:
- Entities with only `[Computed]` properties (no `[Extracted]`)
- Entities with only `[Extracted]` properties
- Entities with `[Extracted]` referencing a non-computed source
- Entities with `[Computed]` where source properties have no `[Extracted]` back-reference
- All other entity validation paths

## Hypothesized Root Cause

Based on the source code analysis, the root cause is a single over-broad check in `ValidateExtractedProperty`:

1. **Incorrect conflation of write-path and read-path**: Lines 2083–2092 of `EntityAnalyzer.cs` check whether an extracted property's name appears in the source property's computed source list. The comment says "This is allowed but we should check for circular dependencies" — however, this check conflates two orthogonal data-flow paths. A `[Computed]` attribute defines how source properties are combined into a target on **write** (ToDynamoDb). An `[Extracted]` attribute defines how to decompose a source property into components on **read** (FromDynamoDb). These never execute in the same direction.

2. **The DFS check already handles real cycles**: `ValidateComputedKeyCircularDependencies` builds a graph of only Computed→Computed edges and uses DFS to find cycles. It correctly ignores `[Extracted]` properties because they don't participate in the write-path dependency graph. This method is sufficient for detecting all genuine circular dependencies.

3. **The fix is a simple removal**: The entire `if (sourceProperty?.IsComputed == true) { ... }` block (lines 2082–2093) in `ValidateExtractedProperty` should be removed. No replacement logic is needed because the DFS method already provides the necessary circular dependency detection for Computed→Computed relationships.

## Correctness Properties

Property 1: Bug Condition - Bidirectional Mapping Allowed

_For any_ entity where a property has `[Extracted(nameof(Source), index)]` and the source property has `[Computed(nameof(ExtractedProp), ...)]` forming a valid bidirectional mapping (isBugCondition returns true), the fixed analyzer SHALL NOT report DYNDB033 for that property pair, allowing the intended round-trip pattern.

**Validates: Requirements 2.1, 2.2**

Property 2: Preservation - Genuine Circular Dependencies Detected

_For any_ entity where computed properties form a genuine cycle (property A computed from B, property B computed from A — all via `[Computed]` only), the fixed analyzer SHALL continue to report DYNDB033 via the existing DFS-based `ValidateComputedKeyCircularDependencies` check, preserving detection of real circular dependencies.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`

**Function**: `ValidateExtractedProperty`

**Specific Changes**:

1. **Remove the Computed↔Extracted cross-check block**: Delete the entire block starting at the comment `// Check if source property is also computed (potential circular dependency)` through the closing brace of the outer `if` statement (lines 2082–2093). This removes:
   ```csharp
   // Check if source property is also computed (potential circular dependency)
   var sourceProperty = entityModel.Properties.FirstOrDefault(p => p.PropertyName == extractedKey.SourceProperty);
   if (sourceProperty?.IsComputed == true)
   {
       // This is allowed but we should check for circular dependencies
       var computedSourceProperties = sourceProperty.ComputedKey?.SourceProperties ?? Array.Empty<string>();
       if (computedSourceProperties.Contains(extractedProperty.PropertyName))
       {
           ReportDiagnostic(DiagnosticDescriptors.CircularKeyDependency,
               extractedProperty.PropertyDeclaration?.Identifier.GetLocation(),
               $"{extractedProperty.PropertyName} -> {extractedKey.SourceProperty} -> {extractedProperty.PropertyName}");
       }
   }
   ```

2. **No replacement code needed**: The `ValidateComputedKeyCircularDependencies` method (called separately in `ValidateComputedAndExtractedKeys`) already correctly detects genuine Computed→Computed cycles using DFS graph traversal. It only includes Computed property edges in its dependency graph, so Extracted properties are inherently excluded from cycle detection — which is correct because Extracted operates on the read path only.

3. **No changes to `ValidateComputedKeyCircularDependencies`**: This method already works correctly. It builds a dependency graph from `computedProperty.ComputedKey.SourceProperties` for each computed property and performs DFS cycle detection. Since Extracted properties are not computed, they never appear as nodes in this graph.

4. **No changes to `ValidateComputedProperty`**: The self-reference check and source-exists validation remain unchanged.

5. **No changes to `HasCircularDependency`**: The DFS helper remains unchanged.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the false positive on unfixed code, then verify the fix eliminates the false positive while preserving genuine cycle detection.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the false positive BEFORE implementing the fix. Confirm that DYNDB033 incorrectly fires for valid Computed↔Extracted pairs.

**Test Plan**: Write Roslyn source generator unit tests that define entities with the bidirectional mapping pattern (`[Computed]` on one property listing source properties that have `[Extracted]` back-references). Run the analyzer on these entities and assert that DYNDB033 is reported (proving the false positive exists on unfixed code).

**Test Cases**:
1. **Simple Round-Trip Test**: Entity with `[Computed("Year", "Month")] string Pk` and `[Extracted("Pk", 0)] int Year` → assert DYNDB033 fires on unfixed code (false positive)
2. **Full Round-Trip Test**: Entity with all source properties extracted back → all trigger DYNDB033 (false positive)
3. **Partial Round-Trip Test**: Entity where only some source properties have `[Extracted]` → those trigger DYNDB033 (false positive)

**Expected Counterexamples**:
- DYNDB033 reported with cycle path like "Year -> Pk -> Year" for entities using the standard bidirectional mapping pattern
- Diagnostic fires even though write path (Computed) and read path (Extracted) never execute in the same direction

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed analyzer does not report DYNDB033.

**Pseudocode:**
```
FOR ALL entity WHERE any property pair satisfies isBugCondition DO
  diagnostics := EntityAnalyzer_fixed.Analyze(entity)
  ASSERT DYNDB033 NOT IN diagnostics for the Computed↔Extracted pair
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed analyzer produces the same diagnostics as the original.

**Pseudocode:**
```
FOR ALL entity WHERE NO property pair satisfies isBugCondition DO
  ASSERT EntityAnalyzer_original.Analyze(entity) == EntityAnalyzer_fixed.Analyze(entity)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many entity configurations (varying combinations of Computed and non-Computed properties) automatically
- It catches edge cases like entities with multiple computed properties forming chains
- It provides strong guarantees that genuine cycle detection is unchanged

**Test Plan**: Observe diagnostic output on UNFIXED code for entities with genuine Computed→Computed cycles, then write tests verifying the FIXED code produces identical diagnostics for those cases.

**Test Cases**:
1. **Computed→Computed Cycle Preservation**: Entity with `[Computed("B")] string A` and `[Computed("A")] string B` → DYNDB033 must still fire after fix
2. **Multi-Hop Cycle Preservation**: Entity with A→B→C→A all via `[Computed]` → DYNDB033 must still fire after fix
3. **Self-Reference Preservation**: Entity with `[Computed("Pk")] string Pk` → DYNDB034 must still fire (different diagnostic, but verify no regression)
4. **Invalid Extracted Source Preservation**: Entity with `[Extracted("NonExistent", 0)]` → appropriate diagnostic must still fire

### Unit Tests

- Test that DYNDB033 is NOT reported for standard bidirectional mapping (Computed + Extracted on same property pair)
- Test that DYNDB033 is NOT reported when multiple properties are extracted from a computed property
- Test that DYNDB033 IS still reported for genuine Computed→Computed cycles (A computes from B, B computes from A)
- Test that DYNDB033 IS still reported for multi-hop Computed cycles
- Test that DYNDB034 IS still reported for self-referencing computed properties
- Test that invalid Extracted source property diagnostic IS still reported
- Test that negative index diagnostic IS still reported

### Property-Based Tests

- Generate random entity configurations with varying numbers of Computed and Extracted properties forming valid round-trips → verify no DYNDB033 is reported for any Computed↔Extracted pair
- Generate random entity configurations with genuine Computed→Computed cycles → verify DYNDB033 is always reported
- Generate entities mixing valid round-trip patterns with genuine cycles → verify only cycles are flagged, not round-trips

### Integration Tests

- Define a complete entity using the full bidirectional mapping pattern (e.g., Event with Year/Month/Day extracted from computed Pk), run the full source generator pipeline, and verify no diagnostics are emitted
- Define an entity with a genuine Computed→Computed cycle alongside valid Extracted properties, verify only the cycle is flagged
- Build a test project containing entities with bidirectional mappings and verify successful compilation with no analyzer warnings
