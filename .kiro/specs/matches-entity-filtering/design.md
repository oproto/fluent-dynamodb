# MatchesEntity Filtering Bugfix Design

## Overview

The `GenerateMatchesEntityMethod` in `MapperGenerator.cs` generates overly strict entity-type filtering that silently drops legitimate items from query/scan results. The fix replaces the current "check all non-nullable attributes" heuristic with a three-tier approach based on the entity's discriminator configuration and table context. This eliminates false negatives (dropped valid items) while improving type discrimination accuracy for multi-entity tables.

## Glossary

- **Bug_Condition (C)**: An entity has a discriminator configured (or is on a single-entity table) AND the generated `MatchesEntity` method checks all non-nullable property presence, causing false negatives when any attribute is missing from the DynamoDB item
- **Property (P)**: The desired behavior — `MatchesEntity` uses only the discriminator check (Tier 1), minimal key check (Tier 2), or key-attribute-only check (Tier 3) to determine entity type membership
- **Preservation**: Existing behavior that must remain unchanged — items that fail key-attribute checks or discriminator mismatch must still return false; method signature stays the same; call sites are unaffected
- **`GenerateMatchesEntityMethod`**: The method in `MapperGenerator.cs` (~line 3607) that emits the C# source for `MatchesEntity` at compile time
- **`DiscriminatorConfig`**: Model class populated by `DiscriminatorAnalyzer` containing `PropertyName`, `ExactValue`, `Pattern`, and `Strategy` (ExactMatch, StartsWith, EndsWith, Contains, Complex)
- **`EntityModel.Discriminator`**: The new discriminator config property (populated but currently unused by MatchesEntity)
- **`EntityModel.EntityDiscriminator`**: The deprecated legacy property (currently the only thing MatchesEntity checks)
- **`entity.IsDefault`**: Indicates the entity is the default for its table (relevant for multi-entity tables)
- **Single-entity table**: A table where only one entity class references that `TableName`
- **Multi-entity table**: A table where multiple entity classes share the same `TableName`

## Bug Details

### Bug Condition

The bug manifests when `MatchesEntity` is generated for an entity that has a discriminator configured via `DiscriminatorProperty`/`DiscriminatorPattern`/`DiscriminatorValue`, OR when the entity is the sole entity on its table. In both cases, the generated code unnecessarily checks presence of ALL non-nullable properties (including collections, computed fields, and attributes added after existing items were written), causing legitimate items to be incorrectly filtered out.

**Formal Specification:**
```
FUNCTION isBugCondition(entity, item)
  INPUT: entity of type EntityModel, item of type Dictionary<string, AttributeValue>
  OUTPUT: boolean
  
  LET hasDiscriminator = entity.Discriminator != null AND entity.Discriminator.IsValid
  LET isSingleEntityTable = entity.TableEntityCount == 1
  LET hasNonKeyNonNullableProps = entity.Properties.Any(p => !p.IsPartitionKey AND !p.IsSortKey AND !p.IsNullable AND p.HasAttributeMapping)
  LET itemMissingNonKeyAttr = hasNonKeyNonNullableProps AND item does not contain at least one non-key non-nullable attribute
  LET itemMatchesDiscriminator = hasDiscriminator AND item matches entity.Discriminator
  LET itemHasKeyAttrs = item contains partition key (AND sort key if applicable)
  
  RETURN (hasDiscriminator OR isSingleEntityTable)
         AND itemHasKeyAttrs
         AND (itemMatchesDiscriminator OR isSingleEntityTable)
         AND itemMissingNonKeyAttr
         AND currentGeneratedCode returns false (due to non-key attribute checks)
END FUNCTION
```

### Examples

- **Empty collection**: Entity has `[DynamoDbMap] List<PhoneModel> Phones = new()`. Item stored without "phones" attribute (DynamoDB omits empty lists). Current code returns `false`. Should return `true` (discriminator matches).
- **Schema evolution**: Entity adds `[DynamoDbAttribute("middleName")] public string MiddleName { get; set; } = string.Empty`. Existing items without "middleName" attribute return `false` from MatchesEntity. Should return `true`.
- **Sparse write**: Item written with only pk, sk, and name. Current code checks all non-nullable attributes → `false`. With discriminator configured, should return `true`.
- **Single-entity table**: Entity is the only one on the table. Any item with the correct key attributes should return `true` regardless of data attribute presence.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Items missing required key attributes (partition key, sort key) MUST continue to return `false`
- Items where the discriminator value does NOT match the configured pattern/value MUST continue to return `false`
- The `MatchesEntity` method signature (`public static bool MatchesEntity(Dictionary<string, AttributeValue> item)`) MUST remain unchanged
- Call sites in `EntityExecuteAsyncExtensions.cs`, `CompoundEntityResult.cs`, and `PartiQLRequestBuilder.cs` MUST NOT require any changes
- The legacy `EntityDiscriminator` property MUST continue to work for backward compatibility (mapped through `DiscriminatorAnalyzer` to `DiscriminatorConfig` with `PropertyName = "entity_type"`)
- Items passed to `FromDynamoDb` after `MatchesEntity` returns `true` continue through the same hydration path

**Scope:**
All inputs that do NOT trigger the bug condition should be completely unaffected by this fix. This includes:
- Items that legitimately belong to a different entity type (correct rejection)
- Items on multi-entity tables without discriminator where key attributes are missing
- The behavior of `ToDynamoDb`, `FromDynamoDb`, `GetPartitionKey`, and other generated methods

## Hypothesized Root Cause

Based on the code analysis, the root causes are:

1. **`entity.Discriminator` is populated but never consulted**: `DiscriminatorAnalyzer` correctly parses `DiscriminatorProperty`, `DiscriminatorValue`, and `DiscriminatorPattern` into a `DiscriminatorConfig` object on `entity.Discriminator`, but `GenerateMatchesEntityMethod` only checks the deprecated `entity.EntityDiscriminator` property. The new discriminator system is completely ignored.

2. **No single-entity table awareness**: `GenerateEntityImplementation` is called per-entity before the table grouping phase in `DynamoDbSourceGenerator.Execute()`. The method has no way to know whether this entity is the only one on its table, so it always generates the full attribute-presence check.

3. **Overly broad non-nullable check**: The current code treats ALL non-nullable properties as required for entity discrimination (`Where(p => p.HasAttributeMapping && (p.IsPartitionKey || !p.IsNullable))`), conflating "hydration safety" (can I deserialize?) with "type discrimination" (is this my entity type?).

4. **Legacy `EntityDiscriminator` only works for exact match on "EntityType" attribute**: The existing discriminator check hard-codes `item.TryGetValue("EntityType", ...)` with the literal attribute name, rather than using the configured `DiscriminatorProperty` name. It also only handles `ExactMatch` strategy, never pattern-based strategies.

## Correctness Properties

Property 1: Bug Condition - Discriminator-Configured Entities Accept Valid Items

_For any_ entity with a valid `DiscriminatorConfig` (entity.Discriminator.IsValid == true) and any DynamoDB item where the discriminator property exists and matches the configured value/pattern, the fixed `MatchesEntity` SHALL return `true` regardless of which other non-key attributes are present or absent in the item.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation - Non-Matching Items Are Rejected

_For any_ entity with a valid `DiscriminatorConfig` and any DynamoDB item where either (a) the discriminator property is missing, or (b) the discriminator property value does NOT match the configured value/pattern, the fixed `MatchesEntity` SHALL return `false`, preserving correct entity-type filtering.

**Validates: Requirements 3.1, 3.2**

Property 3: Bug Condition - Single-Entity Tables Accept Items With Key Attributes

_For any_ entity that is the sole entity on its table (TableEntityCount == 1) and any DynamoDB item that contains the required key attributes (partition key and sort key if applicable), the fixed `MatchesEntity` SHALL return `true` regardless of which other attributes are present or absent.

**Validates: Requirements 2.4**

Property 4: Preservation - Key Attribute Absence Causes Rejection

_For any_ entity (regardless of tier) and any DynamoDB item that is missing the partition key attribute (or sort key attribute when the entity defines one), the fixed `MatchesEntity` SHALL return `false`, preserving the structural requirement that items must have valid keys.

**Validates: Requirements 3.1**

Property 5: Preservation - Method Signature and Call Site Compatibility

_For any_ entity, the fixed code generation SHALL produce a method with the exact signature `public static bool MatchesEntity(Dictionary<string, AttributeValue> item)` and no additional required parameters, preserving call-site compatibility.

**Validates: Requirements 3.5**

## Fix Implementation

### Changes Required

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Models/EntityModel.cs`

**Change 1: Add table entity count property**

Add a property to `EntityModel` to track how many entities share the same table:

```csharp
/// <summary>
/// Gets or sets the number of entities sharing the same table.
/// Used by MatchesEntity generation to determine single-entity vs multi-entity table behavior.
/// </summary>
public int TableEntityCount { get; set; } = 1;
```

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/DynamoDbSourceGenerator.cs`

**Change 2: Populate TableEntityCount before generating entity implementations**

Move the table-grouping logic (or a pre-pass count) BEFORE the entity implementation generation loop, so each `EntityModel` knows its table's entity count:

```csharp
// Pre-pass: count entities per table for MatchesEntity tier determination
var entityCountByTable = validEntityModels
    .Where(e => !string.IsNullOrEmpty(e.TableName) && !e.TableName.StartsWith("_entity_"))
    .GroupBy(e => e.TableName)
    .ToDictionary(g => g.Key!, g => g.Count());

foreach (var entity in validEntityModels)
{
    if (!string.IsNullOrEmpty(entity.TableName) && entityCountByTable.TryGetValue(entity.TableName, out var count))
    {
        entity.TableEntityCount = count;
    }
}
```

This requires restructuring `Execute()` to do entity analysis in a first pass, then generation in a second pass. Alternatively, the entity count could be set during the validation phase that already groups by table.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Change 3: Rewrite `GenerateMatchesEntityMethod` with three-tier logic**

Replace the entire method body with:

```csharp
private static void GenerateMatchesEntityMethod(StringBuilder sb, EntityModel entity)
{
    sb.AppendLine();
    sb.AppendLine("        /// <summary>");
    sb.AppendLine("        /// Determines whether a DynamoDB item matches this entity type.");
    sb.AppendLine("        /// </summary>");
    sb.AppendLine("        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)");
    sb.AppendLine("        {");

    // Tier 1: Entity has discriminator configured → use discriminator as sole check
    if (entity.Discriminator != null && entity.Discriminator.IsValid)
    {
        GenerateDiscriminatorCheck(sb, entity);
    }
    // Tier 2: Single-entity table → minimal structural check (key attributes only)
    else if (entity.TableEntityCount == 1)
    {
        GenerateKeyAttributeOnlyCheck(sb, entity, "Single-entity table: key attributes are sufficient");
    }
    // Tier 3: Multi-entity table without discriminator → key-attribute-only check
    else
    {
        GenerateKeyAttributeOnlyCheck(sb, entity, "Multi-entity table without discriminator: key attributes only");
    }

    sb.AppendLine("        }");
}
```

**Change 4: Add `GenerateDiscriminatorCheck` helper**

```csharp
private static void GenerateDiscriminatorCheck(StringBuilder sb, EntityModel entity)
{
    var disc = entity.Discriminator!;
    var propertyName = disc.PropertyName;
    
    // First check key attributes exist
    GenerateKeyPresenceChecks(sb, entity);

    sb.AppendLine($"            // Discriminator check on \"{propertyName}\"");
    sb.AppendLine($"            if (!item.TryGetValue(\"{propertyName}\", out var discriminatorValue) || discriminatorValue.S == null)");
    sb.AppendLine("                return false;");
    sb.AppendLine();

    switch (disc.Strategy)
    {
        case DiscriminatorStrategy.ExactMatch:
            sb.AppendLine($"            return discriminatorValue.S == \"{disc.ExactValue}\";");
            break;

        case DiscriminatorStrategy.StartsWith:
            var startsWithText = DiscriminatorAnalyzer.GetPatternText(disc.Pattern!, disc.Strategy);
            sb.AppendLine($"            return discriminatorValue.S.StartsWith(\"{startsWithText}\");");
            break;

        case DiscriminatorStrategy.EndsWith:
            var endsWithText = DiscriminatorAnalyzer.GetPatternText(disc.Pattern!, disc.Strategy);
            sb.AppendLine($"            return discriminatorValue.S.EndsWith(\"{endsWithText}\");");
            break;

        case DiscriminatorStrategy.Contains:
            var containsText = DiscriminatorAnalyzer.GetPatternText(disc.Pattern!, disc.Strategy);
            sb.AppendLine($"            return discriminatorValue.S.Contains(\"{containsText}\");");
            break;

        case DiscriminatorStrategy.Complex:
            // For complex patterns, fall back to key-attribute check
            sb.AppendLine("            // Complex pattern: fall back to key check");
            sb.AppendLine("            return true;");
            break;

        default:
            sb.AppendLine("            return true;");
            break;
    }
}
```

**Change 5: Add `GenerateKeyAttributeOnlyCheck` helper**

```csharp
private static void GenerateKeyAttributeOnlyCheck(StringBuilder sb, EntityModel entity, string comment)
{
    sb.AppendLine($"            // {comment}");
    GenerateKeyPresenceChecks(sb, entity);
    sb.AppendLine("            return true;");
}
```

**Change 6: Add `GenerateKeyPresenceChecks` helper**

```csharp
private static void GenerateKeyPresenceChecks(StringBuilder sb, EntityModel entity)
{
    var pkProperty = entity.PartitionKeyProperty;
    if (pkProperty != null)
    {
        sb.AppendLine($"            if (!item.ContainsKey(\"{pkProperty.AttributeName}\"))");
        sb.AppendLine("                return false;");
    }

    var skProperty = entity.SortKeyProperty;
    if (skProperty != null)
    {
        sb.AppendLine($"            if (!item.ContainsKey(\"{skProperty.AttributeName}\"))");
        sb.AppendLine("                return false;");
    }

    sb.AppendLine();
}
```

---

### Backward Compatibility with Legacy `EntityDiscriminator`

The `DiscriminatorAnalyzer.AnalyzeTableDiscriminator` method already handles the legacy case:

```csharp
// Handle legacy EntityDiscriminator property
if (!string.IsNullOrEmpty(legacyEntityDiscriminator) && string.IsNullOrEmpty(discriminatorProperty))
{
    discriminatorProperty = "entity_type";
    discriminatorValue = legacyEntityDiscriminator;
}
```

This means any entity using the deprecated `EntityDiscriminator = "MyType"` will have `entity.Discriminator` populated with `PropertyName = "entity_type"`, `ExactValue = "MyType"`, `Strategy = ExactMatch`. The new Tier 1 code will generate:

```csharp
if (!item.TryGetValue("entity_type", out var discriminatorValue) || discriminatorValue.S == null)
    return false;
return discriminatorValue.S == "MyType";
```

This is functionally equivalent to the current legacy behavior (which checks `item.TryGetValue("EntityType", ...)`) but uses the correct attribute name from the analyzer. **Note**: The current code hard-codes `"EntityType"` while the analyzer maps legacy to `"entity_type"`. We need to verify which is the actual DynamoDB attribute name used in practice. If existing consumers use `"EntityType"` as the literal attribute name, the analyzer's mapping should reflect that. This should be confirmed during implementation.

### Tier Decision Flowchart

```
GenerateMatchesEntityMethod(entity)
    │
    ├── entity.Discriminator?.IsValid == true?
    │       │
    │       YES → Tier 1: Generate discriminator-only check
    │              - Check key attributes present
    │              - Check discriminator property present with non-null .S
    │              - Match based on Strategy (ExactMatch/StartsWith/EndsWith/Contains)
    │
    ├── entity.TableEntityCount == 1?
    │       │
    │       YES → Tier 2: Single-entity table
    │              - Check key attributes present (pk, sk if applicable)
    │              - Return true
    │
    └── else → Tier 3: Multi-entity table without discriminator
               - Check key attributes present (pk, sk if applicable)
               - Return true
               (Accepts risk of wrong-type hydration; better than silent data loss)
```

### Generated Code Examples

**Tier 1 — Entity with `DiscriminatorPattern = "EMPLOYEE#*"` on property "sk":**
```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    if (!item.ContainsKey("pk"))
        return false;
    if (!item.ContainsKey("sk"))
        return false;

    // Discriminator check on "sk"
    if (!item.TryGetValue("sk", out var discriminatorValue) || discriminatorValue.S == null)
        return false;

    return discriminatorValue.S.StartsWith("EMPLOYEE#");
}
```

**Tier 1 — Entity with `DiscriminatorValue = "ORDER"` on property "entity_type":**
```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    if (!item.ContainsKey("pk"))
        return false;
    if (!item.ContainsKey("sk"))
        return false;

    // Discriminator check on "entity_type"
    if (!item.TryGetValue("entity_type", out var discriminatorValue) || discriminatorValue.S == null)
        return false;

    return discriminatorValue.S == "ORDER";
}
```

**Tier 2 — Single-entity table (entity is sole occupant):**
```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    // Single-entity table: key attributes are sufficient
    if (!item.ContainsKey("pk"))
        return false;
    if (!item.ContainsKey("sk"))
        return false;

    return true;
}
```

**Tier 3 — Multi-entity table, no discriminator configured:**
```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    // Multi-entity table without discriminator: key attributes only
    if (!item.ContainsKey("pk"))
        return false;
    if (!item.ContainsKey("sk"))
        return false;

    return true;
}
```

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Generate `EntityModel` objects with various discriminator configurations and verify that `GenerateMatchesEntityMethod` produces code that fails for legitimate items with missing non-key attributes. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **Discriminator Configured + Missing Collection**: Entity with `Discriminator.IsValid == true` and a `List<T>` property. Generate code, evaluate against an item matching the discriminator but missing the list attribute. Current code returns `false` (will fail on unfixed code).
2. **Single-Entity Table + Schema Evolution**: Entity with `TableEntityCount == 1` and a new non-nullable string property. Generate code, evaluate against item missing the new attribute. Current code returns `false` (will fail on unfixed code).
3. **Legacy EntityDiscriminator + Sparse Write**: Entity with deprecated `EntityDiscriminator = "USER"`. Generate code, evaluate against item with only pk/sk/EntityType attributes. Current code checks all non-nullable properties (will fail on unfixed code).
4. **Multi-Entity No Discriminator + Missing Optional Data**: Entity without discriminator on a multi-entity table. Generate code, evaluate against item missing a non-nullable data attribute. Current code returns `false` (will fail on unfixed code).

**Expected Counterexamples**:
- Generated code contains `if (!item.ContainsKey("phones")) return false;` even when discriminator is configured
- Generated code never references `entity.Discriminator.PropertyName` or uses `StartsWith`/`EndsWith`/`Contains`
- Possible causes: `entity.Discriminator` is populated but never read; the method only checks `entity.EntityDiscriminator`

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL entity WHERE entity.Discriminator.IsValid OR entity.TableEntityCount == 1 DO
  FOR ALL item WHERE itemHasKeyAttributes(item, entity)
                 AND (entity.Discriminator.IsValid => itemMatchesDiscriminator(item, entity)) DO
    generatedCode := GenerateMatchesEntityMethod_fixed(entity)
    result := evaluate(generatedCode, item)
    ASSERT result == true
  END FOR
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL entity, item WHERE NOT isBugCondition(entity, item) DO
  ASSERT GenerateMatchesEntityMethod_original(entity)(item) == GenerateMatchesEntityMethod_fixed(entity)(item)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many random entity configurations (with/without discriminators, various property counts, various attribute presence combinations)
- It catches edge cases around key attribute absence, discriminator mismatch, and complex pattern strategies
- It provides strong guarantees that rejection behavior is unchanged for items that should NOT pass

**Test Plan**: Observe behavior on UNFIXED code first for items that should be rejected (missing key attributes, discriminator mismatch), then write property-based tests capturing that behavior.

**Test Cases**:
1. **Key Attribute Missing Preservation**: Generate random entities and items missing pk/sk. Verify both old and new code return `false`.
2. **Discriminator Mismatch Preservation**: Generate entities with various discriminators and items with non-matching discriminator values. Verify both return `false`.
3. **Method Signature Preservation**: For any entity, verify generated code contains exact method signature.
4. **Non-Affected Entities Preservation**: For entities without discriminators on multi-entity tables (Tier 3), verify key-attribute-only check still rejects keyless items.

### Unit Tests

- Test Tier 1 code generation for each `DiscriminatorStrategy`: ExactMatch, StartsWith, EndsWith, Contains, Complex
- Test Tier 2 code generation for single-entity tables (PK only, PK+SK)
- Test Tier 3 code generation for multi-entity tables without discriminator
- Test backward compatibility: entity with legacy `EntityDiscriminator` generates correct check via `DiscriminatorConfig`
- Test edge case: entity with `Discriminator` having `Strategy = None` (invalid config) falls through to Tier 2/3
- Test that discriminator property name uses the actual DynamoDB attribute name, not the C# property name

### Property-Based Tests

- Generate random `EntityModel` objects with varying `Discriminator` configs (null, valid ExactMatch, valid StartsWith, valid EndsWith, valid Contains, invalid) and varying `TableEntityCount` (1, 2, 5). Verify the generated code contains discriminator-based checks for Tier 1, key-only checks for Tier 2/3, and never checks non-key non-nullable attributes.
- Generate random DynamoDB items (dictionaries with various attribute combinations) and verify: items with matching discriminator + key attributes → code returns `true`; items with mismatching discriminator → code returns `false`; items missing key attributes → code returns `false`.
- Generate random pattern strings and verify `DiscriminatorAnalyzer.GetPatternText` combined with `DeterminePatternStrategy` produces the correct C# string method call (StartsWith/EndsWith/Contains).

### Integration Tests

- End-to-end source generator test: Compile an entity class with `DiscriminatorPattern = "EMP#*"` on sort key, run the full source generator pipeline, verify the generated `MatchesEntity` uses `StartsWith("EMP#")`.
- Compile a single-entity table definition, verify generated `MatchesEntity` only checks key attributes.
- Compile a multi-entity table (2+ entities sharing same table name, no discriminators), verify generated `MatchesEntity` only checks key attributes for each entity.
- Runtime integration: Create items via `Put`, query them back, verify no items are silently dropped when collections are empty or new properties exist.
