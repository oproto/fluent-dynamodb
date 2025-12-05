# Unit Test Migration Status

## Overview

This document tracks the migration of brittle unit tests from string-based assertions to compilation verification and semantic assertions. The migration aims to make tests more resilient to formatting changes while maintaining full test coverage.

**Last Updated:** 2025-10-22  
**Total Test Files:** 15  
**Total Tests:** 167  
**Migrated Tests:** 80 (47.9%)

## Migration Priority Classification

### Priority 1: High Impact (Core Generator Tests)
These tests verify core code generation logic and break frequently on formatting changes.

### Priority 2: Medium Impact (Supporting Generator Tests)
These tests verify supporting functionality and occasionally break on formatting changes.

### Priority 3: Low Impact (Infrastructure & Model Tests)
These tests verify infrastructure, diagnostics, or simple models and rarely break.

---

## Test File Analysis

### Priority 1: High Impact Tests

#### 1. MapperGeneratorTests.cs ✅ COMPLETED
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/MapperGeneratorTests.cs`
- **Total Tests:** 9
- **Migration Status:** ✅ **COMPLETED** (2025-10-22)
- **Compilation Verification:** ✅ Already Added
- **Migration Changes Applied:**
  - ✅ Replaced method existence checks with `.ShouldContainMethod()`
  - ✅ Replaced assignment checks with `.ShouldContainAssignment()`
  - ✅ Replaced LINQ checks with `.ShouldUseLinqMethod()`
  - ✅ Replaced type reference checks with `.ShouldReferenceType()`
  - ✅ Preserved DynamoDB attribute type checks (S, N, SS, NS, L, M) with "because" messages
  - ✅ Preserved null handling checks with "because" messages
  - ✅ Preserved relationship mapping checks with "because" messages
  - ✅ Added file header comment documenting migration
  - ✅ All 9 tests passing
- **Notes:** 
  - Migration completed successfully
  - Tests now resilient to formatting changes
  - DynamoDB-specific behavior checks preserved
  - Helper methods `CreateEntitySource()` and `CreateRelatedEntitySources()` remain unchanged

**Test Breakdown:**
1. `GenerateEntityImplementation_WithBasicEntity_ProducesCorrectCode` - Basic entity mapping
2. `GenerateEntityImplementation_WithMultiItemEntity_GeneratesMultiItemMethods` - Multi-item entities
3. `GenerateEntityImplementation_WithRelatedEntities_GeneratesRelationshipMapping` - Relationship mapping
4. `GenerateEntityImplementation_WithGsiProperties_GeneratesCorrectMetadata` - GSI metadata
5. `GenerateEntityImplementation_WithNullableProperties_GeneratesNullChecks` - Nullable handling
6. `GenerateEntityImplementation_WithCollectionProperties_GeneratesNativeDynamoDbCollections` - Collection mapping
7. `GenerateEntityImplementation_WithDifferentPropertyTypes_GeneratesCorrectConversions` - Type conversions
8. `GenerateEntityImplementation_WithEntityDiscriminator_GeneratesDiscriminatorLogic` - Discriminator logic
9. `GenerateEntityImplementation_WithErrorHandling_GeneratesExceptionHandling` - Error handling

---

#### 2. ComplexTypeGenerationTests.cs ✅ COMPLETED
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ComplexTypeGenerationTests.cs`
- **Total Tests:** 38
- **Migration Status:** ✅ **COMPLETED** (2025-10-22)
- **Compilation Verification:** ✅ Already Added
- **Migration Changes Applied:**
  - ✅ Compilation verification already present in all tests
  - ✅ Replaced method existence checks with `.ShouldContainMethod()`
  - ✅ Replaced assignment checks with `.ShouldContainAssignment()`
  - ✅ Replaced LINQ checks with `.ShouldUseLinqMethod()`
  - ✅ Replaced type reference checks with `.ShouldReferenceType()`
  - ✅ Preserved DynamoDB attribute type checks (S, N, SS, NS, BS, L, M) with "because" messages
  - ✅ Preserved null and empty collection handling checks with "because" messages
  - ✅ Preserved JSON serialization checks with "because" messages
  - ✅ Preserved blob storage checks with "because" messages
  - ✅ Added file header comment documenting migration
  - ✅ All 38 tests passing
- **Notes:** 
  - Migration completed successfully
  - Tests now resilient to formatting changes
  - DynamoDB-specific behavior checks preserved for:
    - Dictionary/Map conversions (M attribute type)
    - HashSet/Set conversions (SS, NS, BS attribute types)
    - List conversions (L attribute type)
    - TTL Unix epoch conversions (N attribute type)
    - JSON blob serialization (System.Text.Json and Newtonsoft.Json)
    - Blob reference storage (async methods, IBlobStorageProvider)
  - Helper method `GenerateCode()` with multiple configuration options remains unchanged

**Test Categories:**
- Map Property Tests (Task 19.1): 4 tests ✅
- Set Property Tests (Task 19.2): 4 tests ✅
- List Property Tests (Task 19.3): 4 tests ✅
- TTL Property Tests (Task 19.4): 5 tests ✅
- JSON Blob Property Tests (Task 19.5): 10 tests ✅
- Blob Reference Property Tests (Task 19.6): 5 tests ✅
- Compilation Error Diagnostics Tests (Task 19.7): 6 tests ✅

---

#### 3. KeysGeneratorTests.cs ✅ COMPLETED
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/KeysGeneratorTests.cs`
- **Total Tests:** 8
- **Migration Status:** ✅ **COMPLETED** (2025-10-22)
- **Compilation Verification:** ✅ Already Added
- **Migration Changes Applied:**
  - ✅ Compilation verification already present in all tests
  - ✅ Replaced method existence checks with `.ShouldContainMethod()`
  - ✅ Preserved class name checks as DynamoDB-specific structural elements
  - ✅ Preserved key format string checks with "because" messages
  - ✅ Preserved null handling checks with "because" messages
  - ✅ Preserved type conversion checks (Guid.ToString(), DateTime formatting) with "because" messages
  - ✅ Added file header comment documenting migration
  - ✅ All 8 tests passing
- **Notes:**
  - Migration completed successfully
  - Tests now resilient to formatting changes
  - DynamoDB-specific behavior checks preserved for:
    - Partition key format strings (prefix + separator patterns)
    - Sort key format strings
    - GSI key builder class generation
    - Null parameter validation
    - Guid to string conversion
    - DateTime ISO 8601 formatting for sortable keys
    - Custom format string parsing

**Test Breakdown:**
1. `GenerateKeysClass_WithPartitionKeyOnly_GeneratesPartitionKeyBuilder` - Basic partition key
2. `GenerateKeysClass_WithPartitionAndSortKey_GeneratesAllKeyBuilders` - Composite keys
3. `GenerateKeysClass_WithGsi_GeneratesGsiKeyBuilders` - GSI key builders
4. `GenerateKeysClass_WithNullableTypes_GeneratesNullChecks` - Null validation
5. `GenerateKeysClass_WithGuidType_GeneratesToStringConversion` - Guid conversion
6. `GenerateKeysClass_WithDateTimeType_GeneratesFormattedString` - DateTime formatting
7. `GenerateKeysClass_WithNoKeys_GeneratesEmptyClass` - Empty class generation
8. `GenerateKeysClass_WithCustomKeyFormat_ParsesFormatCorrectly` - Custom format parsing

---

### Priority 2: Medium Impact Tests

#### 4. FieldsGeneratorTests.cs ✅ COMPLETED
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/FieldsGeneratorTests.cs`
- **Total Tests:** 6
- **Migration Status:** ✅ **COMPLETED** (2025-10-22)
- **Compilation Verification:** ✅ Already Added
- **Migration Changes Applied:**
  - ✅ Compilation verification already present in all tests
  - ✅ Added `.ShouldContainClass()` semantic assertion method to SemanticAssertions
  - ✅ Added `.ShouldContainConstant()` semantic assertion method to SemanticAssertions
  - ✅ Replaced class existence checks with `.ShouldContainClass()`
  - ✅ Replaced constant field checks with `.ShouldContainConstant()`
  - ✅ Preserved field constant value checks with "because" messages
  - ✅ Preserved attribute name mapping checks with "because" messages
  - ✅ Preserved reserved word handling checks with "because" messages
  - ✅ Added file header comment documenting migration
  - ✅ All 6 tests passing
- **Notes:**
  - Migration completed successfully
  - Tests now resilient to formatting changes
  - DynamoDB-specific behavior checks preserved for:
    - Field constant values (attribute name mappings)
    - GSI nested class generation
    - Reserved word escaping (@ prefix)
    - XML documentation generation
  - Extended SemanticAssertions with two new methods for class and constant verification

**Test Breakdown:**
1. `GenerateFieldsClass_WithBasicEntity_ProducesCorrectCode` - Basic field constants
2. `GenerateFieldsClass_WithGsiProperties_GeneratesNestedGsiClasses` - GSI nested classes
3. `GenerateFieldsClass_WithReservedWords_HandlesCorrectly` - Reserved word handling
4. `GenerateFieldsClass_WithNoAttributeMappings_GeneratesEmptyClass` - Empty class generation
5. `GenerateFieldsClass_WithComplexGsiName_GeneratesSafeClassName` - Safe class name generation
6. `GenerateFieldsClass_WithMultipleGsis_GeneratesAllNestedClasses` - Multiple GSI classes

---

#### 5. DynamoDbSourceGeneratorTests.cs ✅ COMPLETED
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/DynamoDbSourceGeneratorTests.cs`
- **Total Tests:** 3
- **Migration Status:** ✅ **COMPLETED** (2025-10-22)
- **Compilation Verification:** ✅ Added
- **Migration Changes Applied:**
  - ✅ Added compilation verification to all tests that generate code
  - ✅ Replaced class existence checks with `.ShouldContainClass()`
  - ✅ Replaced method existence checks with `.ShouldContainMethod()`
  - ✅ Preserved namespace checks with "because" messages
  - ✅ Preserved constant field value checks with "because" messages (attribute name mappings)
  - ✅ Added file header comment documenting migration
  - ✅ All 3 tests passing
- **Notes:**
  - Migration completed successfully
  - Tests now resilient to formatting changes
  - End-to-end generator tests verify complete output (Entity + Fields + Keys)
  - DynamoDB-specific behavior checks preserved for:
    - Namespace generation
    - Attribute name mappings in field constants
    - GSI nested class generation

**Test Breakdown:**
1. `Generator_WithBasicEntity_ProducesCode` - Basic entity with reserved word warning
2. `Generator_WithoutDynamoDbTableAttribute_ProducesNoCode` - No generation without attribute
3. `Generator_WithGsiEntity_GeneratesFieldsWithGsiClasses` - GSI field generation

---

#### 6. MapperGeneratorBugFixTests.cs ✅ COMPLETED
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/MapperGeneratorBugFixTests.cs`
- **Total Tests:** 1
- **Migration Status:** ✅ **COMPLETED** (2025-10-22)
- **Compilation Verification:** ✅ Added
- **Migration Changes Applied:**
  - ✅ Added compilation verification to verify generated code compiles
  - ✅ Added `.ShouldReferenceType()` semantic assertion for TestEntity type reference
  - ✅ Preserved bug-specific string checks with "because" messages:
    - `typeof(TestEntity)` presence check (verifies fix)
    - `typeof(Id)` absence check (verifies bug doesn't reoccur)
    - `typeof(Data)` absence check (verifies bug doesn't reoccur)
    - Exception constructor parameter checks (verifies correct error handling)
  - ✅ Added file header comment documenting migration
  - ✅ Test passing
- **Notes:**
  - Migration completed successfully
  - Test now verifies both compilation and bug-specific behavior
  - Bug-specific checks preserved to ensure the CS0246 error fix remains in place
  - The bug was: property names were being used as types instead of entity class name
  - Test verifies `typeof(TestEntity)` is used, not `typeof(Id)` or `typeof(Data)`

**Test Breakdown:**
1. `GenerateEntityImplementation_WithPropertyNames_UsesEntityClassNameInTypeofExpressions` - Verifies CS0246 bug fix

---

### Priority 3: Low Impact Tests

#### 7. EntityAnalyzerTests.cs
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/EntityAnalyzerTests.cs`
- **Total Tests:** 10
- **Migration Status:** ✅ Good As-Is
- **Compilation Verification:** ❌ Not Needed
- **String Assertions Found:** None (diagnostic tests only)
- **Estimated Effort:** None (no migration needed)
- **Notes:**
  - Tests verify diagnostic generation (errors and warnings)
  - Uses `Diagnostics.Should().Contain(d => d.Id == "DYNDB021")`
  - No code generation verification needed
  - Already follows best practices

---

#### 8. EdgeCaseTests.cs ✅ COMPLETED
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/EdgeCases/EdgeCaseTests.cs`
- **Total Tests:** 15
- **Migration Status:** ✅ **COMPLETED** (2025-10-22)
- **Compilation Verification:** ✅ Added
- **Migration Changes Applied:**
  - ✅ Added compilation verification to all 15 tests
  - ✅ Replaced class/interface checks with `.ShouldReferenceType()`
  - ✅ Replaced method existence checks with `.ShouldContainMethod()`
  - ✅ Preserved DynamoDB-specific checks with "because" messages:
    - Field constant values (special characters, reserved keywords, Unicode)
    - Key format strings (prefix + separator patterns)
    - Attribute name mappings
    - Reserved keyword escaping (@ prefix)
    - Relationship metadata generation
  - ✅ Added file header comment documenting migration
  - ✅ All 15 tests passing
- **Notes:**
  - Migration completed successfully
  - Tests now resilient to formatting changes
  - Edge cases covered:
    - Empty entities
    - Special characters in names (dashes, underscores, dots, numbers)
    - Reserved keywords (C# and DynamoDB)
    - Very long names
    - Nested namespaces
    - Generic type constraints
    - Circular references
    - Unicode characters (Japanese, Spanish, Chinese)
    - Complex key formats
    - Empty attribute names
    - Duplicate attribute names
    - Very complex relationship patterns

**Test Breakdown:**
1. `SourceGenerator_WithEmptyEntity_GeneratesMinimalCode` - Minimal entity
2. `SourceGenerator_WithSpecialCharactersInNames_HandlesCorrectly` - Special chars
3. `SourceGenerator_WithReservedKeywords_EscapesCorrectly` - Reserved words
4. `SourceGenerator_WithVeryLongNames_HandlesCorrectly` - Long identifiers
5. `SourceGenerator_WithNestedNamespaces_HandlesCorrectly` - Deep namespaces
6. `SourceGenerator_WithGenericTypeConstraints_HandlesCorrectly` - Generics
7. `SourceGenerator_WithCircularReferences_HandlesGracefully` - Circular refs
8. `SourceGenerator_WithUnicodeCharacters_HandlesCorrectly` - Unicode
9. `SourceGenerator_WithComplexKeyFormats_ParsesCorrectly` - Complex keys
10. `SourceGenerator_WithEmptyAttributeNames_HandlesGracefully` - Empty attrs
11. `SourceGenerator_WithDuplicateAttributeNames_HandlesCorrectly` - Duplicates
12. `SourceGenerator_WithVeryComplexRelationshipPatterns_HandlesCorrectly` - Complex patterns

---

#### 9. DiagnosticDescriptorsTests.cs
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Diagnostics/DiagnosticDescriptorsTests.cs`
- **Total Tests:** 22
- **Migration Status:** ✅ Good As-Is
- **Compilation Verification:** ❌ Not Needed
- **String Assertions Found:** Minimal (only for message format verification)
- **Estimated Effort:** None (no migration needed)
- **Notes:**
  - Tests verify diagnostic descriptor properties
  - Uses `MessageFormat.ToString().Should().Contain()` for message verification
  - This is appropriate for diagnostic message testing

---

#### 10. EntityModelTests.cs
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Models/EntityModelTests.cs`
- **Total Tests:** 8
- **Migration Status:** ✅ Good As-Is
- **Compilation Verification:** ❌ Not Needed
- **Estimated Effort:** None (no migration needed)
- **Notes:**
  - Tests verify model structure and properties
  - No code generation involved

---

#### 11. PropertyModelTests.cs
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Models/PropertyModelTests.cs`
- **Total Tests:** 8
- **Migration Status:** ✅ Good As-Is
- **Compilation Verification:** ❌ Not Needed
- **Estimated Effort:** None (no migration needed)
- **Notes:**
  - Tests verify model structure and properties
  - No code generation involved

---

#### 12. RelationshipModelTests.cs
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Models/RelationshipModelTests.cs`
- **Total Tests:** 10
- **Migration Status:** ✅ Good As-Is
- **Compilation Verification:** ❌ Not Needed
- **Estimated Effort:** None (no migration needed)
- **Notes:**
  - Tests verify model structure and properties
  - No code generation involved

---

#### 13. EndToEndSourceGeneratorTests.cs
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/EndToEndSourceGeneratorTests.cs`
- **Total Tests:** 11
- **Migration Status:** 🔍 Review Needed
- **Compilation Verification:** Unknown
- **Estimated Effort:** Low-Medium (depends on test content)
- **Notes:**
  - Integration tests for end-to-end scenarios
  - May already have good test structure

---

#### 14. SourceGeneratorPerformanceTests.cs
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Performance/SourceGeneratorPerformanceTests.cs`
- **Total Tests:** 8
- **Migration Status:** ✅ Good As-Is
- **Compilation Verification:** ❌ Not Needed
- **String Assertions Found:** Minimal (only for verification of generated output)
- **Estimated Effort:** None (no migration needed)
- **Notes:**
  - Performance tests focus on timing and diagnostics
  - String checks are minimal and appropriate

---

#### 15. SemanticAssertionsTests.cs
- **Location:** `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/TestHelpers/SemanticAssertionsTests.cs`
- **Total Tests:** 10
- **Migration Status:** ✅ Good As-Is
- **Compilation Verification:** ❌ Not Needed
- **Estimated Effort:** None (no migration needed)
- **Notes:**
  - Tests verify the SemanticAssertions helper itself
  - Uses string assertions to verify error messages
  - This is appropriate for testing the test infrastructure

---

## Summary Statistics

### By Priority
- **Priority 1 (High Impact):** 3 files, 55 tests
- **Priority 2 (Medium Impact):** 3 files, 10 tests
- **Priority 3 (Low Impact):** 9 files, 102 tests

### By Migration Status
- **Completed:** 7 files, 80 tests ✅
- **Not Started:** 0 files, 0 tests
- **Good As-Is:** 7 files, 87 tests
- **Review Needed:** 1 file, 11 tests (EndToEndSourceGeneratorTests.cs)

### By Compilation Verification Status
- **Already Added:** 7 files (MapperGeneratorTests, ComplexTypeGenerationTests, KeysGeneratorTests, FieldsGeneratorTests, DynamoDbSourceGeneratorTests, MapperGeneratorBugFixTests, EdgeCaseTests)
- **Needs Adding:** 0 files
- **Not Needed:** 10 files (diagnostic, model, and infrastructure tests)

---

## Migration Checklist Template

Use this checklist for each file being migrated:

```markdown
## [FileName].cs

- [ ] Added compilation verification to all tests
- [ ] Replaced method existence checks with `.ShouldContainMethod()`
- [ ] Replaced assignment checks with `.ShouldContainAssignment()`
- [ ] Replaced LINQ checks with `.ShouldUseLinqMethod()`
- [ ] Replaced type reference checks with `.ShouldReferenceType()`
- [ ] Added "because" messages to DynamoDB-specific checks
- [ ] All tests pass
- [ ] Verified tests catch intentional errors
- [ ] Verified tests pass with formatting changes
- [ ] Added file header comment documenting migration
```

---

## String Assertion Patterns Found

### Patterns to Replace with Semantic Assertions

1. **Method Existence:**
   ```csharp
   // Before
   code.Should().Contain("public static string Pk(");
   
   // After
   code.ShouldContainMethod("Pk");
   ```

2. **Assignment Checks:**
   ```csharp
   // Before
   code.Should().Contain("entity.Id = ");
   
   // After
   code.ShouldContainAssignment("entity.Id");
   ```

3. **LINQ Usage:**
   ```csharp
   // Before
   code.Should().Contain(".Select(");
   
   // After
   code.ShouldUseLinqMethod("Select");
   ```

4. **Type References:**
   ```csharp
   // Before
   code.Should().Contain("typeof(TestEntity)");
   
   // After
   code.ShouldReferenceType("TestEntity");
   ```

### Patterns to Keep (DynamoDB-Specific)

1. **Attribute Types:**
   ```csharp
   // Keep with "because" message
   code.Should().Contain("S =", "should use String type for string properties");
   code.Should().Contain("N =", "should use Number type for numeric properties");
   code.Should().Contain("SS =", "should use String Set for HashSet<string>");
   code.Should().Contain("NS =", "should use Number Set for HashSet<int>");
   code.Should().Contain("L =", "should use List type for List<T>");
   code.Should().Contain("M =", "should use Map type for Dictionary<,>");
   ```

2. **Null Handling:**
   ```csharp
   // Keep with "because" message
   code.Should().Contain("!= null", "should check for null before adding to DynamoDB item");
   code.Should().Contain("Count > 0", "should check for empty collections before adding to DynamoDB item");
   ```

3. **Format Strings:**
   ```csharp
   // Keep with "because" message
   code.Should().Contain("var keyValue = \"tenant#\" + id", "should use correct partition key format");
   ```

---

## Next Steps

1. ✅ Complete this migration status document
2. ✅ **COMPLETED:** Priority 1: MapperGeneratorTests.cs (9 tests migrated)
3. ✅ **COMPLETED:** Priority 1: ComplexTypeGenerationTests.cs (38 tests migrated)
4. ✅ **COMPLETED:** Priority 1: KeysGeneratorTests.cs (8 tests migrated)
5. ✅ **COMPLETED:** Priority 2: FieldsGeneratorTests.cs (6 tests migrated)
6. ✅ **COMPLETED:** Priority 2: DynamoDbSourceGeneratorTests.cs (3 tests migrated)
7. ✅ **COMPLETED:** Priority 2: MapperGeneratorBugFixTests.cs (1 test migrated)
8. ✅ **COMPLETED:** Priority 3: EdgeCaseTests.cs (15 tests migrated)
9. ✅ **COMPLETED:** Priority 3: EntityAnalyzerTests.cs (reviewed - no migration needed)
10. ✅ **COMPLETED:** Priority 3: Model tests (reviewed - no migration needed)
11. ⏭️ Review Priority 3: EndToEndSourceGeneratorTests.cs (11 tests)
12. ⏭️ Create final migration summary report
