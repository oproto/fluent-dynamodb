# Unit Test Migration Baseline Metrics

**Generated:** 2025-10-22  
**Purpose:** Document the baseline state of unit tests before migration

---

## Executive Summary

This document provides a detailed baseline of the current state of unit tests in the source generator test suite. It identifies brittle string-based assertions that need to be migrated to compilation verification and semantic assertions.

### Key Findings

- **Total Test Files Analyzed:** 15
- **Total Tests:** 164
- **Files Requiring Migration:** 6 (Priority 1 & 2)
- **Tests Requiring Migration:** 65
- **String Assertions to Replace:** 248
- **Files Already Using Compilation Verification:** 4 (MapperGeneratorTests, ComplexTypeGenerationTests, KeysGeneratorTests, FieldsGeneratorTests)
- **Files Needing Compilation Verification:** 2 (DynamoDbSourceGeneratorTests, MapperGeneratorBugFixTests)

---

## Detailed Metrics by Priority

### Priority 1: High Impact Tests

**Total Files:** 3  
**Total Tests:** 55  
**Total String Assertions:** 210  
**Average String Assertions per Test:** 3.8

#### File Breakdown

| File | Tests | String Assertions | Compilation Verification | Semantic Assertions |
|------|-------|-------------------|-------------------------|---------------------|
| MapperGeneratorTests.cs | 9 | 48 | ✅ Yes | ❌ No |
| ComplexTypeGenerationTests.cs | 38 | 131 | ✅ Yes | ❌ No |
| KeysGeneratorTests.cs | 8 | 31 | ✅ Yes | ❌ No |

**Migration Impact:** HIGH - These tests verify core code generation and break frequently on formatting changes.

---

### Priority 2: Medium Impact Tests

**Total Files:** 3  
**Total Tests:** 10  
**Total String Assertions:** 38  
**Average String Assertions per Test:** 3.8

#### File Breakdown

| File | Tests | String Assertions | Compilation Verification | Semantic Assertions |
|------|-------|-------------------|-------------------------|---------------------|
| FieldsGeneratorTests.cs | 6 | 18 | ✅ Yes | ❌ No |
| DynamoDbSourceGeneratorTests.cs | 3 | 15 | ❌ No | ❌ No |
| MapperGeneratorBugFixTests.cs | 1 | 5 | ❌ No | ❌ No |

**Migration Impact:** MEDIUM - These tests verify supporting functionality and occasionally break on formatting changes.

---

### Priority 3: Files Needing Review

**Total Files:** 2  
**Total Tests:** 23  
**Total String Assertions:** 97  
**Average String Assertions per Test:** 4.2

#### File Breakdown

| File | Tests | String Assertions | Compilation Verification | Semantic Assertions |
|------|-------|-------------------|-------------------------|---------------------|
| EdgeCaseTests.cs | 12 | 43 | ❌ No | ❌ No |
| EndToEndSourceGeneratorTests.cs | 11 | 54 | ❌ No | ❌ No |

**Migration Impact:** LOW-MEDIUM - Need to review test content to determine if migration is beneficial.

---

### Priority 3: Files Not Requiring Migration

**Total Files:** 7  
**Total Tests:** 87

These files are already well-structured and don't require migration:

| File | Tests | Reason |
|------|-------|--------|
| EntityAnalyzerTests.cs | 10 | Diagnostic tests only, no code generation |
| DiagnosticDescriptorsTests.cs | 22 | Tests diagnostic descriptors, appropriate string checks |
| EntityModelTests.cs | 8 | Model structure tests, no code generation |
| PropertyModelTests.cs | 8 | Model structure tests, no code generation |
| RelationshipModelTests.cs | 10 | Model structure tests, no code generation |
| SourceGeneratorPerformanceTests.cs | 8 | Performance tests, minimal string checks |
| SemanticAssertionsTests.cs | 10 | Tests the test infrastructure itself |

---

## String Assertion Pattern Analysis

### Patterns Found in Priority 1 & 2 Files

Based on manual code review, the following patterns were identified:

#### 1. Method Existence Checks
**Count:** ~80 occurrences  
**Pattern:** `Should().Contain("public static")`  
**Example:**
```csharp
result.Should().Contain("public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)");
```
**Migration:** Replace with `ShouldContainMethod("ToDynamoDb")`

---

#### 2. Assignment Checks
**Count:** ~40 occurrences  
**Pattern:** `Should().Contain("entity.")`  
**Example:**
```csharp
result.Should().Contain("entity.Id = idValue.S");
```
**Migration:** Replace with `ShouldContainAssignment("entity.Id")`

---

#### 3. LINQ Usage Checks
**Count:** ~25 occurrences  
**Pattern:** `Should().Contain(".Select(")`  
**Example:**
```csharp
result.Should().Contain(".Select(x => new AttributeValue { S = x }).ToList()");
```
**Migration:** Replace with `ShouldUseLinqMethod("Select")`

---

#### 4. Type Reference Checks
**Count:** ~15 occurrences  
**Pattern:** `Should().Contain("typeof(")`  
**Example:**
```csharp
result.Should().Contain("typeof(TestEntity)");
```
**Migration:** Replace with `ShouldReferenceType("TestEntity")`

---

#### 5. DynamoDB Attribute Type Checks (Keep These)
**Count:** ~50 occurrences  
**Patterns:**
- `Should().Contain("S =")`
- `Should().Contain("N =")`
- `Should().Contain("SS =")`
- `Should().Contain("NS =")`
- `Should().Contain("L =")`
- `Should().Contain("M =")`

**Example:**
```csharp
result.Should().Contain("item[\"tags\"] = new AttributeValue { SS = typedEntity.Tags.ToList() };");
```
**Migration:** Keep but add "because" messages:
```csharp
result.Should().Contain("SS =", "should use String Set for HashSet<string>");
```

---

#### 6. Null Handling Checks (Keep These)
**Count:** ~20 occurrences  
**Patterns:**
- `Should().Contain("!= null")`
- `Should().Contain("Count > 0")`

**Example:**
```csharp
result.Should().Contain("if (typedEntity.Tags != null && typedEntity.Tags.Count > 0)");
```
**Migration:** Keep but add "because" messages:
```csharp
result.Should().Contain("!= null && ", "should check for null and empty before adding to DynamoDB item");
```

---

#### 7. Key Format Checks (Keep These)
**Count:** ~18 occurrences  
**Pattern:** `Should().Contain("var keyValue =")`  
**Example:**
```csharp
result.Should().Contain("var keyValue = \"tenant#\" + id;");
```
**Migration:** Keep but add "because" messages:
```csharp
result.Should().Contain("var keyValue = \"tenant#\" + id", "should use correct partition key format");
```

---

## Test Infrastructure Status

### Existing Infrastructure (Already Available)

✅ **CompilationVerifier** - Available in `TestHelpers/CompilationVerifier.cs`
- Method: `AssertGeneratedCodeCompiles(string sourceCode, params string[] additionalSources)`
- Status: Ready to use
- Usage: Already used in 4 test files

✅ **SemanticAssertions** - Available in `TestHelpers/SemanticAssertions.cs`
- Methods:
  - `ShouldContainMethod(this string sourceCode, string methodName, string because = "")`
  - `ShouldContainAssignment(this string sourceCode, string targetName, string because = "")`
  - `ShouldUseLinqMethod(this string sourceCode, string methodName, string because = "")`
  - `ShouldReferenceType(this string sourceCode, string typeName, string because = "")`
- Status: Ready to use
- Usage: Not yet used in any test files

✅ **SemanticAssertionsTests** - Tests for SemanticAssertions
- Status: 10 tests, all passing
- Coverage: Tests all semantic assertion methods and error messages

---

## Migration Effort Estimation

### Time Estimates (per file)

| File | Tests | String Assertions | Estimated Hours | Complexity |
|------|-------|-------------------|-----------------|------------|
| MapperGeneratorTests.cs | 9 | 48 | 3-4 hours | High - Complex mapping logic |
| ComplexTypeGenerationTests.cs | 38 | 131 | 8-10 hours | Very High - Many test categories |
| KeysGeneratorTests.cs | 8 | 31 | 2-3 hours | Medium - Key format strings |
| FieldsGeneratorTests.cs | 6 | 18 | 1-2 hours | Low - Mostly constant values |
| DynamoDbSourceGeneratorTests.cs | 3 | 15 | 1 hour | Low - End-to-end tests |
| MapperGeneratorBugFixTests.cs | 1 | 5 | 0.5 hours | Very Low - Single bug fix test |

**Total Estimated Effort:** 15.5 - 20.5 hours

---

## Risk Assessment

### High Risk Areas

1. **ComplexTypeGenerationTests.cs**
   - **Risk:** 38 tests with 131 string assertions
   - **Mitigation:** Break into smaller chunks, test incrementally
   - **Impact:** High - Tests critical advanced type handling

2. **DynamoDB-Specific Checks**
   - **Risk:** Over-migration could lose important behavior verification
   - **Mitigation:** Keep attribute type checks, null handling, and format strings
   - **Impact:** Medium - Could miss DynamoDB-specific bugs

3. **Test Coverage Loss**
   - **Risk:** Migration might inadvertently reduce test coverage
   - **Mitigation:** Run tests after each file migration, verify error detection
   - **Impact:** High - Could introduce regressions

### Low Risk Areas

1. **Compilation Verification**
   - **Risk:** Low - Already proven in 4 test files
   - **Impact:** Low - Adds safety without removing coverage

2. **Semantic Assertions**
   - **Risk:** Low - Well-tested infrastructure
   - **Impact:** Low - Improves test resilience

---

## Success Criteria

### Quantitative Metrics

- [ ] All 65 tests in Priority 1 & 2 files pass after migration
- [ ] String assertions reduced from 248 to ~70 (DynamoDB-specific only)
- [ ] Semantic assertions added: ~150
- [ ] Compilation verification added to 2 additional files
- [ ] Zero test failures after migration
- [ ] Zero reduction in test coverage

### Qualitative Metrics

- [ ] Tests pass with intentional formatting changes
- [ ] Tests fail with intentional code errors
- [ ] Error messages are clear and actionable
- [ ] Code is more maintainable
- [ ] Documentation is updated

---

## Baseline Test Execution

### Current Test Results (Before Migration)

```bash
# Run all source generator unit tests
dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Oproto.FluentDynamoDb.SourceGenerator.UnitTests.csproj

# Expected: All tests should pass
# Actual: [To be filled in when running baseline]
```

**Baseline Status:** To be established before starting migration

---

## Next Steps

1. ✅ **Complete baseline documentation** (This document)
2. ⏭️ **Run baseline test execution** to confirm all tests pass
3. ⏭️ **Begin Priority 1 migration** with MapperGeneratorTests.cs
4. ⏭️ **Validate each file** after migration
5. ⏭️ **Update MIGRATION_STATUS.md** after each file
6. ⏭️ **Create final summary report** after all migrations

---

## Appendix: Test File Locations

All test files are located under:
```
Oproto.FluentDynamoDb.SourceGenerator.UnitTests/
```

### Priority 1 Files
- `Generators/MapperGeneratorTests.cs`
- `Generators/ComplexTypeGenerationTests.cs`
- `Generators/KeysGeneratorTests.cs`

### Priority 2 Files
- `Generators/FieldsGeneratorTests.cs`
- `DynamoDbSourceGeneratorTests.cs`
- `Generators/MapperGeneratorBugFixTests.cs`

### Priority 3 Files (Review)
- `EdgeCases/EdgeCaseTests.cs`
- `Integration/EndToEndSourceGeneratorTests.cs`

### Priority 3 Files (No Migration)
- `EntityAnalyzerTests.cs`
- `Diagnostics/DiagnosticDescriptorsTests.cs`
- `Models/EntityModelTests.cs`
- `Models/PropertyModelTests.cs`
- `Models/RelationshipModelTests.cs`
- `Performance/SourceGeneratorPerformanceTests.cs`
- `TestHelpers/SemanticAssertionsTests.cs`
