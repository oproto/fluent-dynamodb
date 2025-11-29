# Requirements Document

## Introduction

This specification addresses code quality improvements for the Oproto.FluentDynamoDb library, focusing on eliminating compiler warnings and removing reflection usage from the main library code. The library is designed to be AOT-compatible and trimmer-safe, making reflection usage problematic for production deployments. Additionally, hundreds of compiler warnings reduce code quality visibility and mask potential issues.

## Glossary

- **AOT (Ahead-of-Time)**: Compilation strategy where code is compiled to native code before runtime, incompatible with reflection-based dynamic code
- **Trimmer**: .NET tool that removes unused code from assemblies, which can break reflection-based code
- **Reflection**: Runtime type inspection and invocation mechanism that is incompatible with AOT compilation
- **InternalsVisibleTo**: Assembly attribute that exposes internal members to specified assemblies, enabling testing without reflection
- **IL3000**: Trimmer warning for Assembly.Location usage in single-file apps
- **IL2026**: Trimmer warning for RequiresUnreferencedCodeAttribute usage
- **IL2060/IL2070/IL2072/IL2075**: Trimmer warnings for reflection-based member access
- **IL3050**: AOT warning for RequiresDynamicCodeAttribute usage
- **CS0618**: Compiler warning for obsolete member usage
- **CS1998**: Compiler warning for async methods lacking await operators
- **xUnit1031**: xUnit analyzer warning for blocking task operations in tests

## Requirements

### Requirement 1

**User Story:** As a library maintainer, I want to categorize and address all compiler warnings, so that the build output is clean and potential issues are visible.

#### Acceptance Criteria

1. WHEN the solution is built THEN the Build_System SHALL produce zero warnings from main library projects (Oproto.FluentDynamoDb, Oproto.FluentDynamoDb.SourceGenerator, extension libraries)
2. WHEN the solution is built THEN the Build_System SHALL categorize test project warnings into actionable groups (IL3000, IL2026, CS1998, xUnit1031, CS0618)
3. WHEN IL3000 warnings exist in test projects THEN the Build_System SHALL either suppress them with justification or refactor the code to avoid Assembly.Location usage
4. WHEN CS0618 obsolete warnings exist THEN the Build_System SHALL update code to use non-obsolete alternatives
5. WHEN CS1998 async warnings exist THEN the Build_System SHALL add appropriate await operators or convert methods to synchronous

### Requirement 2

**User Story:** As a library consumer deploying to AOT environments, I want the main library to contain no reflection usage, so that the library works correctly in trimmed and AOT-compiled applications.

#### Acceptance Criteria

1. WHEN the main library code is analyzed THEN the Library SHALL contain zero usages of System.Reflection namespace in production code paths
2. WHEN MetadataResolver.cs requires type inspection THEN the Library SHALL use source-generated interfaces or compile-time known types instead of reflection
3. WHEN WithClientExtensions.cs copies builder state THEN the Library SHALL use public APIs, internal accessors, or clone methods instead of reflection-based field access
4. WHEN ProjectionExtensions.cs detects projection metadata THEN the Library SHALL use source-generated interfaces or marker interfaces instead of reflection-based property discovery
5. WHEN the library is trimmed THEN the Library SHALL produce zero IL2026, IL2060, IL2070, IL2072, IL2075, or IL3050 warnings

### Requirement 3

**User Story:** As a test author, I want unit tests to access internal members through InternalsVisibleTo rather than reflection, so that tests are compile-time verified and AOT-compatible.

#### Acceptance Criteria

1. WHEN test projects need access to internal members THEN the Test_Project SHALL use InternalsVisibleTo assembly attribute instead of reflection
2. WHEN tests use Assembly.GetType or Type.GetProperty with reflection THEN the Test_Project SHALL refactor to use direct type references or InternalsVisibleTo access
3. WHEN tests use Activator.CreateInstance with reflection THEN the Test_Project SHALL refactor to use direct constructor calls or factory methods
4. WHEN tests use MethodInfo.MakeGenericMethod THEN the Test_Project SHALL refactor to use compile-time generic instantiation
5. WHEN tests use Assembly.Load for dynamic compilation verification THEN the Test_Project SHALL document the reflection usage with appropriate suppression attributes

### Requirement 4

**User Story:** As a library maintainer, I want a consistent approach to handling unavoidable reflection in source generator tests, so that warnings are properly documented and suppressed.

#### Acceptance Criteria

1. WHEN source generator tests require dynamic assembly loading THEN the Test_Project SHALL use #pragma warning disable with documented justification
2. WHEN source generator tests verify generated code at runtime THEN the Test_Project SHALL use UnconditionalSuppressMessage attribute with clear rationale
3. WHEN reflection is unavoidable in test infrastructure THEN the Test_Project SHALL isolate reflection usage to dedicated helper classes with appropriate attributes
4. WHEN CompilationVerifier uses Assembly.Location THEN the Test_Project SHALL use AppContext.BaseDirectory or suppress with justification

### Requirement 5

**User Story:** As a library maintainer, I want request builders to support state cloning without reflection, so that WithClient extensions work in AOT environments.

#### Acceptance Criteria

1. WHEN a request builder needs to be cloned THEN the Builder SHALL expose a Clone() method or copy constructor
2. WHEN attribute helpers need to be copied between builders THEN the Builder SHALL expose internal accessors visible to extension assemblies
3. WHEN WithClientExtensions creates new builder instances THEN the Extension SHALL use public or internal APIs instead of reflection-based field access
4. WHEN builder state is copied THEN the Builder SHALL preserve all configuration including attribute mappings, conditions, and projections
