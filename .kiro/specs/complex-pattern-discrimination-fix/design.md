# Technical Design: Complex Pattern Discrimination Fix

## Overview

Fix the source generator's Complex pattern discrimination to use positional `IndexOf` checks instead of tautological `Contains` checks on both the exclusion side (less-specific entity rejecting more-specific items) and the positive side (more-specific entity verifying its own structural requirement). This ensures mutual exclusivity across all three discrimination scenarios: SK-only, compound PK+SK, and custom discriminators (unaffected).

## Bug Details

The source generator produces incorrect `MatchesEntity` code for Complex discriminator patterns (multi-wildcard patterns like `"CAP#*#*"`) in two ways:

1. **Exclusion side**: `CreateExclusionPattern()` in PatternOverlapAnalyzer generates `Contains("#")` to exclude more-specific items — but this is always true when `StartsWith("CAP#")` already passed. The less-specific entity becomes invisible.

2. **Positive side**: `GenerateComplexPatternCheck()` in MapperGenerator generates `Contains("#")` as a structural check — but it adds zero discrimination. The existing partial fix removes this check, but doesn't replace it with a working positional equivalent. The more-specific entity now falsely claims items belonging to the less-specific entity in GSI/Scan/CompoundEntityResult scenarios.

Three scenarios must all work correctly:
- **Same PK, SK-only discrimination**: e.g., `ORDER#*` vs `ORDER#*#*` with identical PK patterns
- **Different PKs, compound discrimination**: e.g., `CAP#*#*` entities with different PK structures (CompoundPromotionPass handles peer disambiguation, but structural SK check is still needed for cross-tier correctness)
- **Custom discriminator**: User-specified DiscriminatorProperty/DiscriminatorValue/DiscriminatorPattern — must remain unaffected

## Expected Behavior

For Complex patterns where internal segments are bare separators (contained in the prefix):
- **Exclusion checks** emit `IndexOf(separator, prefixLength) >= 0` to verify the separator exists BEYOND the prefix boundary
- **Positive checks** emit `IndexOf(separator, prefixLength) >= 0` (or `< 0` in negated mode) to structurally verify multi-segment values

For Complex patterns with meaningful internal segments (NOT contained in the prefix):
- Continue generating standard `Contains("segment")` checks as today

Verification with `"CAP#*#*"` (prefix = `"CAP#"`, length 4, bare segment = `"#"`):
- `"CAP#cap1"` → `IndexOf("#", 4)` = -1 → single-segment (belongs to `CAP#*` entity)
- `"CAP#svc1#cap1"` → `IndexOf("#", 4)` = 8 → multi-segment (belongs to `CAP#*#*` entity)

## Hypothesized Root Cause

Pattern `"CAP#*#*"` split on `*` produces segments `["CAP#", "#", ""]`. The non-empty segments are `["CAP#", "#"]`. The internal segment `"#"` is the literal text between two adjacent wildcards — just the separator character between variable portions.

The `Contains("#")` check finds this character inside the prefix `"CAP#"` itself (at position 3), rather than verifying it appears in the variable portion beyond the prefix. This is separator-agnostic: `"X_*_*"` produces `Contains("_")` with the same bug for separator `"_"`.

The determination of whether a segment is "bare" (non-discriminating) is: `prefixSegment.Contains(internalSegment)`. When true, a positional check must be used instead of Contains.

## Glossary

| Term | Definition |
|------|-----------|
| Complex pattern | A discriminator pattern with 2+ wildcards that doesn't match `*text*` form (e.g., `"CAP#*#*"`, `"A#*#B#*"`) |
| Bare separator segment | An internal segment (between wildcards) that is already contained within the prefix segment, making a Contains check tautological |
| Meaningful segment | An internal segment NOT contained in the prefix (e.g., `"#LINE#"` from `"INVOICE#*#LINE#*"`) — Contains check works correctly |
| Positional check | `IndexOf(literal, offset) >= 0` — verifies the literal exists at a position BEYOND the prefix boundary |
| Prefix length | The character length of the first non-empty segment (the fixed prefix). Used as the offset for positional checks |

## Correctness Properties

Property 1: For any two entities with overlapping auto-derived discriminator patterns on the same attribute, no DynamoDB item should match both entities' `MatchesEntity` methods (mutual exclusivity).
- **Validates: Requirements 2.1, 2.2**

Property 2: Every item that matches an entity's full pattern (all literal segments in correct positions) must pass that entity's `MatchesEntity` (completeness).
- **Validates: Requirements 2.1, 2.2, 2.4**

Property 3: The fix must produce correct results for any separator character — `#`, `_`, `:`, `-`, etc. (separator agnosticism).
- **Validates: Requirements 3.7**

Property 4: Changes to auto-derivation logic must not alter behavior for entities with explicit DiscriminatorProperty/DiscriminatorValue/DiscriminatorPattern (custom discriminator isolation).
- **Validates: Requirements 2.3, 3.3**

Property 5: Only bare-separator segments get positional treatment; meaningful segments continue using standard Contains (meaningful segment preservation).
- **Validates: Requirements 2.4, 3.1**

## Fix Implementation

### Files Modified

| File | Change |
|------|--------|
| `Generators/MapperGenerator.cs` | `GenerateComplexPatternCheck` "return" mode: replace bare Contains with positional IndexOf |
| `Generators/MapperGenerator.cs` | `GenerateComplexPatternCheck` "negated" mode: replace bare Contains with negated positional IndexOf |
| `Generators/MapperGenerator.cs` | `GenerateComplexExclusionCheck`: same positional replacement for Complex exclusion guards |
| `Analysis/PatternOverlapAnalyzer.cs` | `CreateExclusionPattern`: positional fallback for bare separators (**already done in partial fix**) |
| `Analysis/PatternOverlapAnalyzer.cs` | `IsTautologicalExclusion`: semantic subsumption check (**already done in partial fix**) |
| `Models/ExclusionPattern.cs` | `OffsetIndex` property (**already done in partial fix**) |

### Change 1: `GenerateComplexPatternCheck` — "return" mode

Current (partial fix — skips bare segments entirely):
```csharp
if (prefixSegment.Contains(nonEmptySegments[i]))
    continue;
conditions.Add($"discriminatorValue.S.Contains(\"{nonEmptySegments[i]}\")");
```

Fixed (replaces with positional IndexOf):
```csharp
if (prefixSegment.Contains(nonEmptySegments[i]))
{
    // Bare separator: positional check verifies segment exists beyond prefix
    conditions.Add($"discriminatorValue.S.IndexOf(\"{nonEmptySegments[i]}\", {prefixSegment.Length}) >= 0");
}
else
{
    // Meaningful segment: standard Contains
    conditions.Add($"discriminatorValue.S.Contains(\"{nonEmptySegments[i]}\")");
}
```

Generated output for `"CAP#*#*"`:
```csharp
return discriminatorValue.S.StartsWith("CAP#") 
    && discriminatorValue.S.IndexOf("#", 4) >= 0;
```

### Change 2: `GenerateComplexPatternCheck` — "negated" mode

Current (partial fix — skips bare segments entirely):
```csharp
if (prefixSegment.Contains(nonEmptySegments[i]))
    continue;
conditions.Add($"!discriminatorValue.S.Contains(\"{nonEmptySegments[i]}\")");
```

Fixed (replaces with negated positional IndexOf):
```csharp
if (prefixSegment.Contains(nonEmptySegments[i]))
{
    // Bare separator: positional check (negated — return false if NOT found beyond prefix)
    conditions.Add($"discriminatorValue.S.IndexOf(\"{nonEmptySegments[i]}\", {prefixSegment.Length}) < 0");
}
else
{
    // Meaningful segment: standard Contains (negated)
    conditions.Add($"!discriminatorValue.S.Contains(\"{nonEmptySegments[i]}\")");
}
```

Generated output for `"CAP#*#*"` in exclusion context:
```csharp
if (!discriminatorValue.S.StartsWith("CAP#") || discriminatorValue.S.IndexOf("#", 4) < 0)
    return false;
```

### Change 3: `GenerateComplexExclusionCheck`

Apply same positional logic when generating exclusion guards for Complex-strategy patterns:

```csharp
if (!pattern.StartsWith("*") && nonEmptySegments.Count > 0)
{
    var prefixSegment = nonEmptySegments[0];
    conditions.Add($"discriminatorValue.S.StartsWith(\"{prefixSegment}\")");
    for (int i = 1; i < nonEmptySegments.Count; i++)
    {
        if (prefixSegment.Contains(nonEmptySegments[i]))
        {
            conditions.Add($"discriminatorValue.S.IndexOf(\"{nonEmptySegments[i]}\", {prefixSegment.Length}) >= 0");
        }
        else
        {
            conditions.Add($"discriminatorValue.S.Contains(\"{nonEmptySegments[i]}\")");
        }
    }
}
```

### Generated Output Examples

**Scenario 1: Same PK, SK-only**

`OrderEntity` (SK=`ORDER#*`):
```csharp
if (!discriminatorValue.S.StartsWith("ORDER#")) return false;
if (discriminatorValue.S.IndexOf("#", 6) >= 0) return false;  // exclusion
return true;
```

`OrderLineEntity` (SK=`ORDER#*#*`):
```csharp
return discriminatorValue.S.StartsWith("ORDER#") 
    && discriminatorValue.S.IndexOf("#", 6) >= 0;  // structural positive
```

**Scenario 2: Different PKs, compound**

`CapabilityDefinitionEntity` (PK=SERVICE#*, SK=CAP#*):
```csharp
if (!discriminatorValue.S.StartsWith("CAP#")) return false;
if (discriminatorValue.S.IndexOf("#", 4) >= 0) return false;  // exclusion
return true;
```

`RoleCapabilityEntity` (PK=TENANT#*#ROLE#*, SK=CAP#*#*):
```csharp
if (!discriminatorValue.S.StartsWith("CAP#") || discriminatorValue.S.IndexOf("#", 4) < 0)
    return false;  // structural positive
if (item.TryGetValue("pk", out var compoundValue) && compoundValue.S != null
    && compoundValue.S.StartsWith("TENANT#PLATFORM#ROLE#"))
    return false;  // compound exclusion
return true;
```

**Scenario 3: Custom discriminator** — no change, untouched by this fix.

**Meaningful segments** (regression check — `"INVOICE#*#LINE#*"`):
```csharp
return discriminatorValue.S.StartsWith("INVOICE#") 
    && discriminatorValue.S.Contains("#LINE#");  // meaningful, unchanged
```

## Testing Strategy

**Unit Tests:**
- Pattern `"X#*#*"` positive check generates `IndexOf("#", 2) >= 0`
- Pattern `"X#*#*"` negated check generates `IndexOf("#", 2) < 0`
- Pattern `"A#*#B#*"` generates `Contains("#B#")` (meaningful segment)
- Pattern `"*#X#*"` generates `Contains("#X#")` (wildcard-first, no prefix)
- Various separators (`_`, `:`) produce correct offset values

**Property-Based Tests:**
- For any two entities with overlapping Complex patterns, generated checks are mutually exclusive
- For any Complex-pattern entity, its MatchesEntity accepts items matching its full structure and rejects prefix-only items

**Integration Tests:**
- Scan on multi-entity table with `CAP#*` and `CAP#*#*` returns correct items per entity
- GSI query returning mixed-PK items: each entity claims only its own

### Testing Strategy

**Unit Tests:**
- Pattern `"X#*#*"` positive check generates `IndexOf("#", 2) >= 0`
- Pattern `"X#*#*"` negated check generates `IndexOf("#", 2) < 0`
- Pattern `"A#*#B#*"` generates `Contains("#B#")` (meaningful segment)
- Pattern `"*#X#*"` generates `Contains("#X#")` (wildcard-first, no prefix)
- Various separators (`_`, `:`) produce correct offset values

**Property-Based Tests:**
- For any two entities with overlapping Complex patterns, generated checks are mutually exclusive
- For any Complex-pattern entity, its MatchesEntity accepts items matching its full structure and rejects prefix-only items

**Integration Tests:**
- Scan on multi-entity table with `CAP#*` and `CAP#*#*` returns correct items per entity
- GSI query returning mixed-PK items: each entity claims only its own
