# FluentDynamoDb AOT Compatibility Tests

This project tests the AOT (Ahead-of-Time) compatibility of the FluentDynamoDb expression support feature.

## Purpose

The tests verify that:
1. Expression translation works correctly in Native AOT environments
2. Closure captures (local variables, fields, nested closures) function properly
3. All operators and DynamoDB functions translate correctly
4. Generic method expressions work with AOT compilation
5. The trimmed binary contains all necessary code and functions correctly

## Running the Tests

### Standard Build and Run
```bash
dotnet run --project Oproto.FluentDynamoDb.AotTests
```

### AOT Publish and Run
```bash
# Publish as Native AOT
dotnet publish Oproto.FluentDynamoDb.AotTests -c Release

# Run the published binary (path varies by OS)
# On macOS/Linux:
./Oproto.FluentDynamoDb.AotTests/bin/Release/net8.0/osx-arm64/publish/Oproto.FluentDynamoDb.AotTests

# On Windows:
.\Oproto.FluentDynamoDb.AotTests\bin\Release\net8.0\win-x64\publish\Oproto.FluentDynamoDb.AotTests.exe
```

### Check for Trim Warnings
```bash
dotnet publish Oproto.FluentDynamoDb.AotTests -c Release /p:TreatWarningsAsErrors=true
```

## Test Suites

### 1. Closure Capture Tests
Tests that verify closure captures work correctly in AOT:
- Local variable capture
- Static field capture
- Nested closure captures
- Complex closure scenarios with multiple captures

### 2. Expression Translation Tests
Tests that verify expression translation works in AOT:
- All operator types (==, !=, <, >, <=, >=, &&, ||, !)
- DynamoDB functions (begins_with, contains, BETWEEN, attribute_exists, etc.)
- Value capture with various types (string, int, DateTime, enum)
- Validation and error handling

### 3. Generic Method Tests
Tests that verify generic types work in AOT:
- Generic entity types
- Generic property types
- Generic method calls in expressions

### 4. Trimming Compatibility Tests
Tests that verify the trimmed binary works correctly:
- Core types are present after trimming
- Expression translation functions correctly
- Extension methods are available and work

### 5. Update Expression AOT Tests
Tests that verify expression-based update operations work in AOT:
- UpdateExpressionProperty<T> instantiation
- Source-generated UpdateExpressions and UpdateModel classes
- Simple SET operations
- ADD operations (atomic increment/decrement, set union)
- REMOVE operations (attribute deletion)
- DELETE operations (set element removal)
- DynamoDB functions (if_not_exists, list_append, list_prepend)
- Combined operations (SET + ADD + REMOVE + DELETE)
- Generic type resolution at compile time
- No runtime code generation (Expression.Compile() not used)

## Expected Output

When all tests pass, you should see:
```
=== FluentDynamoDb AOT Compatibility Tests ===

Running Closure Capture Tests...
  ✓ Local variable capture
  ✓ Static field capture
  ✓ Nested closure capture
  ✓ Complex closure with multiple captures

Running Expression Translation Tests...
  ✓ All operator translations
  ✓ All DynamoDB function translations
  ✓ Value capture with various types
  ✓ Validation and error handling

Running Generic Method Tests...
  ✓ Generic entity types
  ✓ Generic property types
  ✓ Generic method calls in expressions

Running Trimming Compatibility Tests...
  ✓ Core types present after trimming
  ✓ Expression translation works in trimmed binary
  ✓ Extension methods work in trimmed binary

Running Update Expression AOT Tests...
  ✓ UpdateExpressionProperty instantiation
  ✓ Source-generated classes
  ✓ Simple SET operations
  ✓ ADD operations
  ✓ REMOVE operations
  ✓ DELETE operations
  ✓ DynamoDB functions
  ✓ Combined operations
  ✓ Generic type resolution
  ✓ No runtime code generation

✓ All AOT compatibility tests passed!
```

## AOT Configuration

The project is configured with:
- `PublishAot=true`: Enables Native AOT compilation
- `InvariantGlobalization=true`: Reduces binary size
- `IsAotCompatible=true`: Marks the project as AOT-compatible
- `EnableTrimAnalyzer=true`: Enables trim analysis
- `IsTrimmable=true`: Allows trimming

## Troubleshooting

### Trim Warnings
If you see trim warnings during publish, it means some code may not be AOT-compatible. Review the warnings and ensure:
- No dynamic code generation is used
- No reflection-emit is used
- All types are statically referenced

### Runtime Errors in AOT
If tests pass in JIT but fail in AOT, it may indicate:
- Use of Expression.Compile() (not allowed in AOT)
- Dynamic type resolution
- Missing type preservation attributes

### Performance
AOT binaries should have:
- Faster startup time
- Lower memory usage
- Slightly larger binary size
