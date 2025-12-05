# Design Document

## Overview

This design outlines the approach for migrating brittle unit tests in the source generator test suite from string-based assertions to compilation verification and semantic assertions. The migration will be incremental, prioritizing high-impact tests while maintaining full test coverage throughout the process.

The existing test infrastructure already provides the necessary tools (`CompilationVerifier` and `SemanticAssertions`), so this migration focuses on systematically applying these tools to existing tests.

## Architecture

### Test Migration Layers

The migration follows a three-layer approach:

1. **Compilation Layer**: Verifies generated code compiles without errors
2. **Semantic Layer**: Verifies code structure using syntax tree analysis
3. **Behavioral Layer**: Verifies DynamoDB-specific behavior with targeted string checks

```
┌─────────────────────────────────────────┐
│     Compilation Verification            │
│  (CompilationVerifier)                  │
│  - Catches breaking changes             │
│  - Validates type references            │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│     Semantic Assertions                 │
│  (SemanticAssertions)                   │
│  - Method existence                     │
│  - Assignment structure                 │
│  - LINQ usage                           │
│  - Type references                      │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│     Behavioral Checks                   │
│  (String assertions with "because")     │
│  - DynamoDB attribute types (S, N, SS)  │
│  - Format strings                       │
│  - Null handling patterns               │
└─────────────────────────────────────────┘
```

### Test File Prioritization

Tests will be migrated in the following order based on impact and brittleness:

**Priority 1 (High Impact)**:
- `MapperGeneratorTests.cs` - Core mapping logic, frequently breaks
- `ComplexTypeGenerationTests.cs` - Complex type handling, critical functionality
- `KeysGeneratorTests.cs` - Key generation logic

**Priority 2 (Medium Impact)**:
- `FieldsGeneratorTests.cs` - Field generation
- `DynamoDbSourceGeneratorTests.cs` - End-to-end generator tests
- `MapperGeneratorBugFixTests.cs` - Bug fix verification

**Priority 3 (Low Impact)**:
- `EntityAnalyzerTests.cs` - Mostly diagnostic checks (already good)
- `EdgeCaseTests.cs` - Edge cases, rarely break
- Model tests - Simple structure tests

## Components and Interfaces

### Existing Infrastructure

The test infrastructure already provides all necessary components:

#### CompilationVerifier

```csharp
public static class CompilationVerifier
{
    public static void AssertGeneratedCodeCompiles(
        string sourceCode, 
        params string[] additionalSources);
}
```

**Responsibilities**:
- Compile generated code using Roslyn
- Provide detailed error messages with line numbers
- Include source context for debugging
- Handle external type references

#### SemanticAssertions

```csharp
public static class SemanticAssertions
{
    public static void ShouldContainMethod(
        this string sourceCode, 
        string methodName, 
        string because = "");
    
    public static void ShouldContainAssignment(
        this string sourceCode, 
        string targetName, 
        string because = "");
    
    public static void ShouldUseLinqMethod(
        this string sourceCode, 
        string methodName, 
        string because = "");
    
    public static void ShouldReferenceType(
        this string sourceCode, 
        string typeName, 
        string because = "");
}
```

**Responsibilities**:
- Parse code into syntax trees
- Verify structural elements exist
- Provide clear error messages with available alternatives
- Show source context on failure

### Migration Patterns

#### Pattern 1: Add Compilation Verification

**Before**:
```csharp
[Fact]
public void Generator_WithBasicEntity_GeneratesCode()
{
    var source = @"...";
    var result = GenerateCode(source);
    var code = GetGeneratedSource(result, "Entity.g.cs");
    
    result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    code.Should().Contain("public static User FromDynamoDb");
}
```

**After**:
```csharp
[Fact]
public void Generator_WithBasicEntity_GeneratesCode()
{
    var source = @"...";
    var result = GenerateCode(source);
    var code = GetGeneratedSource(result, "Entity.g.cs");
    
    result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    CompilationVerifier.AssertGeneratedCodeCompiles(code, source); // Added
    
    code.ShouldContainMethod("FromDynamoDb"); // Changed
}
```

#### Pattern 2: Replace Method Checks

**Before**:
```csharp
code.Should().Contain("public static string Pk(string id)");
code.Should().Contain("public static Dictionary<string, AttributeValue> ToDynamoDb");
```

**After**:
```csharp
code.ShouldContainMethod("Pk");
code.ShouldContainMethod("ToDynamoDb");
```

#### Pattern 3: Replace Assignment Checks

**Before**:
```csharp
code.Should().Contain("entity.Id = ");
code.Should().Contain("entity.Name = ");
code.Should().Contain("var keyValue = ");
```

**After**:
```csharp
code.ShouldContainAssignment("entity.Id");
code.ShouldContainAssignment("entity.Name");
code.ShouldContainAssignment("keyValue");
```

#### Pattern 4: Replace LINQ Checks

**Before**:
```csharp
code.Should().Contain(".Select(");
code.Should().Contain(".ToList()");
code.Should().Contain(".ToHashSet()");
```

**After**:
```csharp
code.ShouldUseLinqMethod("Select");
code.ShouldUseLinqMethod("ToList");
code.ShouldUseLinqMethod("ToHashSet");
```

#### Pattern 5: Keep DynamoDB-Specific Checks

**Before**:
```csharp
code.Should().Contain("SS =");
code.Should().Contain("NS =");
code.Should().Contain("entity.Tags != null && entity.Tags.Count > 0");
```

**After** (keep these, but add context):
```csharp
code.Should().Contain("SS =", "should use String Set for HashSet<string>");
code.Should().Contain("NS =", "should use Number Set for HashSet<int>");
code.Should().Contain("entity.Tags != null && entity.Tags.Count > 0",
    "should check for null and empty before adding to DynamoDB item");
```

## Data Models

### Test File Migration Status

Track migration progress for each test file:

```csharp
public class TestFileMigrationStatus
{
    public string FileName { get; set; }
    public int TotalTests { get; set; }
    public int MigratedTests { get; set; }
    public MigrationPriority Priority { get; set; }
    public List<string> RemainingTests { get; set; }
    public bool IsComplete => MigratedTests == TotalTests;
}

public enum MigrationPriority
{
    High,
    Medium,
    Low
}
```

### Migration Checklist

For each test file:

```markdown
## MapperGeneratorTests.cs

- [x] Added compilation verification to all tests
- [x] Replaced method existence checks
- [x] Replaced assignment checks
- [x] Replaced LINQ checks
- [x] Added "because" messages to DynamoDB checks
- [x] All tests pass
- [x] Verified error messages are clear
- [ ] Added file header comment documenting migration
```

## Error Handling

### Compilation Verification Failures

When `CompilationVerifier.AssertGeneratedCodeCompiles()` fails:

1. **Clear Error Message**: Shows exact compilation error with line number
2. **Source Context**: Displays 2 lines before and after the error
3. **Full Source**: Includes complete generated source for debugging
4. **Additional Sources**: Shows any additional source files passed to the verifier

Example error output:
```
Generated code failed to compile:

Error CS0246: The type or namespace name 'AttributeValue' could not be found
  at line 15, column 25

  Source context:
    13:     public static Dictionary<string, AttributeValue> ToDynamoDb(User entity)
    14:     {
>>> 15:         var item = new Dictionary<string, AttributeValue>();
    16:         item["pk"] = new AttributeValue { S = entity.Id };
    17:         return item;
```

### Semantic Assertion Failures

When semantic assertions fail:

1. **Clear Expectation**: States what was expected
2. **Available Alternatives**: Lists what was actually found
3. **Source Context**: Shows first 10 lines of source
4. **Because Message**: Includes optional explanation

Example error output:
```
Expected source code to contain method 'FromDynamoDb'
Because: should generate deserialization method

Available methods:
  - ToDynamoDb
  - GetPartitionKey
  - MatchesEntity

Source code context:
     1: namespace TestNamespace
     2: {
     3:     public partial class TestEntity
     4:     {
     5:         public static Dictionary<string, AttributeValue> ToDynamoDb(...)
```

### Migration Validation Failures

If a migrated test fails:

1. **Compare with Original**: Check if original test would have caught the issue
2. **Assess Coverage**: Determine if migration lost coverage
3. **Fix or Revert**: Either fix the migration or revert and document why

## Testing Strategy

### Validation Approach

For each migrated test:

1. **Positive Validation**: Run test suite to ensure all tests pass
2. **Negative Validation**: Intentionally break generated code to verify test catches it
3. **Formatting Validation**: Change generated code formatting to verify test still passes

### Test Categories

Tests fall into three categories:

#### Category 1: Structure Tests
Tests that verify code structure (methods, assignments, LINQ usage)
- **Migration**: Replace with semantic assertions
- **Validation**: Verify test passes with different formatting

#### Category 2: Behavior Tests
Tests that verify DynamoDB-specific behavior
- **Migration**: Keep string checks, add "because" messages
- **Validation**: Verify test catches incorrect attribute types

#### Category 3: Diagnostic Tests
Tests that verify error reporting
- **Migration**: No changes needed (already good)
- **Validation**: Verify diagnostic IDs and messages are correct

### Regression Prevention

To prevent regressions during migration:

1. **One File at a Time**: Complete migration of one file before moving to next
2. **Run Full Suite**: Run entire test suite after each file migration
3. **Document Changes**: Add comments explaining migration decisions
4. **Peer Review**: Have another developer review migrated tests

## Implementation Plan Overview

The implementation will follow this high-level approach:

### Phase 1: High-Priority Tests (MapperGeneratorTests.cs, ComplexTypeGenerationTests.cs, KeysGeneratorTests.cs)

1. Analyze current test patterns
2. Add compilation verification to all tests
3. Replace structural checks with semantic assertions
4. Keep DynamoDB-specific checks with "because" messages
5. Validate migration success
6. Document migration status

### Phase 2: Medium-Priority Tests (FieldsGeneratorTests.cs, DynamoDbSourceGeneratorTests.cs, MapperGeneratorBugFixTests.cs)

1. Apply lessons learned from Phase 1
2. Follow same migration pattern
3. Validate and document

### Phase 3: Low-Priority Tests (EntityAnalyzerTests.cs, EdgeCaseTests.cs, Model tests)

1. Review if migration is needed (some may already be good)
2. Apply migration where beneficial
3. Document final status

### Phase 4: Documentation and Cleanup

1. Add file header comments to all migrated files
2. Update any related documentation
3. Create summary report of migration
4. Archive or remove any backup files

## Migration Decision Tree

```
For each test method:
├─ Does it verify generated code structure?
│  ├─ Yes → Add compilation verification
│  │       Replace string checks with semantic assertions
│  └─ No → Is it a diagnostic test?
│          ├─ Yes → Keep as-is (already good)
│          └─ No → Is it a model/data test?
│                  └─ Review if migration needed
│
└─ Does it verify DynamoDB-specific behavior?
   ├─ Yes → Keep string checks
   │        Add "because" messages
   │        Add compilation verification
   └─ No → Consider if test is still needed
```

## Success Criteria

A test file migration is considered successful when:

1. ✅ All tests pass after migration
2. ✅ Compilation verification added to all generator tests
3. ✅ Structural checks replaced with semantic assertions
4. ✅ DynamoDB-specific checks retained with "because" messages
5. ✅ Tests don't break on formatting changes
6. ✅ Tests still catch actual errors
7. ✅ Error messages are clear and actionable
8. ✅ Migration documented in file header or checklist

## Risk Mitigation

### Risk: Loss of Test Coverage

**Mitigation**:
- Validate each migrated test catches errors
- Compare assertions before and after migration
- Run negative tests (intentionally break code)

### Risk: Tests Become Too Permissive

**Mitigation**:
- Keep DynamoDB-specific checks as strings
- Use "because" messages to document intent
- Validate tests catch incorrect attribute types

### Risk: Migration Takes Too Long

**Mitigation**:
- Prioritize high-impact tests
- Complete one file at a time
- Accept that some low-priority tests may not need migration

### Risk: Unclear Error Messages

**Mitigation**:
- Test error messages by intentionally breaking code
- Add descriptive "because" messages
- Use semantic assertions that provide context

## Future Considerations

### Additional Semantic Assertions

Consider adding more semantic assertion methods if patterns emerge:

```csharp
// Potential future additions
code.ShouldContainProperty("PropertyName");
code.ShouldContainClass("ClassName");
code.ShouldImplementInterface("IInterfaceName");
code.ShouldHaveAttribute("AttributeName");
```

### Integration Test Coverage

Some unit tests may be better suited as integration tests:

- Tests verifying end-to-end functionality
- Tests requiring actual DynamoDB interaction
- Tests verifying round-trip data integrity

Consider creating integration tests for these scenarios rather than migrating unit tests.

### Automated Migration Tool

If migration patterns are consistent, consider creating a tool to automate parts of the migration:

- Detect string checks that can be replaced
- Suggest semantic assertion replacements
- Add compilation verification automatically

However, manual review is still essential to ensure correctness.
