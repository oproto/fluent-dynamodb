# Requirements Document

## Introduction

This feature introduces a global assembly-level attribute (`FluentDynamoDbSchemaVersionAttribute`) that declares which source generator "schema version" a project targets. This provides an explicit contract between the consumer and the source generator, enabling graceful evolution of generated code shapes (interfaces, accessor methods, builder patterns) without silently breaking consumers on NuGet package upgrade. The schema version is independent of the NuGet package version — multiple package versions can support the same schema version.

## Glossary

- **Schema_Version**: A major.minor version pair that identifies the shape of generated code (interfaces, accessors, builders). Independent of the NuGet package version.
- **Source_Generator**: The Roslyn incremental source generator in the Oproto.FluentDynamoDb.SourceGenerator project that emits C# code at compile time based on entity attributes.
- **Consumer_Assembly**: The .NET assembly that references the Oproto.FluentDynamoDb NuGet package and uses the source generator.
- **Schema_Version_Attribute**: The `FluentDynamoDbSchemaVersionAttribute` assembly-level attribute that declares the targeted schema version.
- **Current_Schema_Version**: The latest schema version that the installed Source_Generator supports.
- **Minimum_Supported_Version**: The oldest schema version that the installed Source_Generator can still emit code for.
- **Diagnostic**: A Roslyn compiler diagnostic (warning, error, or info) emitted by the Source_Generator during compilation.
- **Dual_Targeting**: The ability of the Source_Generator to emit different code shapes based on the declared Schema_Version, allowing a migration window between breaking changes.

## Requirements

### Requirement 1: Schema Version Attribute Definition

**User Story:** As a library maintainer, I want to define an assembly-level attribute for declaring the schema version, so that consuming projects can explicitly state which generated code shape they target.

#### Acceptance Criteria

1. THE Schema_Version_Attribute SHALL be defined in the `Oproto.FluentDynamoDb.Attributes` namespace
2. THE Schema_Version_Attribute SHALL target `AttributeTargets.Assembly` with `AllowMultiple = false`
3. THE Schema_Version_Attribute SHALL accept two constructor parameters: `major` (int) and `minor` (int)
4. THE Schema_Version_Attribute SHALL expose `Major` and `Minor` as read-only integer properties
5. THE Schema_Version_Attribute SHALL be a sealed class inheriting from `System.Attribute`
6. THE Schema_Version_Attribute constructor SHALL throw `ArgumentOutOfRangeException` WHEN major is less than 1 or minor is less than 0

### Requirement 2: Schema Version Detection

**User Story:** As a source generator developer, I want the generator to detect and read the schema version attribute from the consumer assembly, so that it can determine which code shape to emit.

#### Acceptance Criteria

1. WHEN the Source_Generator begins incremental generation, THE Source_Generator SHALL inspect the Consumer_Assembly's compilation for an assembly-level Schema_Version_Attribute
2. WHEN the Schema_Version_Attribute is present on the Consumer_Assembly, THE Source_Generator SHALL extract the Major and Minor version values as non-negative integers
3. WHEN the Schema_Version_Attribute is not present on the Consumer_Assembly, THE Source_Generator SHALL default to schema version 1.0
4. IF the Schema_Version_Attribute is present but contains invalid values (non-integer or negative), THEN THE Source_Generator SHALL report a diagnostic error and halt code generation for the affected assembly

### Requirement 3: Missing Attribute Warning

**User Story:** As a consumer developer, I want to receive a compiler warning when I haven't declared a schema version, so that I'm aware of the contract and nudged to declare it explicitly.

#### Acceptance Criteria

1. WHEN the Schema_Version_Attribute is not present on the Consumer_Assembly, THE Source_Generator SHALL emit exactly one diagnostic FDDB110 with severity Warning per compilation
2. THE diagnostic FDDB110 SHALL include the message: "Assembly does not declare [FluentDynamoDbSchemaVersion]. Defaulting to schema version 1.0. Add [assembly: FluentDynamoDbSchemaVersion(1, 0)] to suppress this warning."
3. WHEN the Schema_Version_Attribute is present on the Consumer_Assembly, THE Source_Generator SHALL NOT emit diagnostic FDDB110
4. WHEN the Source_Generator emits diagnostic FDDB110, THE Source_Generator SHALL proceed with code generation using the default schema version 1.0

### Requirement 4: Unsupported Old Version Error

**User Story:** As a consumer developer, I want a clear error when my declared schema version is no longer supported by the installed package, so that I know I must migrate or pin to an older package version.

#### Acceptance Criteria

1. WHEN the declared Schema_Version is older than the Minimum_Supported_Version using major-then-minor numeric comparison (major compared first; if equal, minor compared), THE Source_Generator SHALL emit diagnostic FDDB111 with severity Error
2. THE diagnostic FDDB111 SHALL include the declared version formatted as "major.minor", the minimum supported version formatted as "major.minor", and a URL to a migration guide
3. THE Source_Generator SHALL report diagnostic FDDB111 at the location of the Schema_Version_Attribute declaration in the Consumer_Assembly source
4. WHEN the Source_Generator emits diagnostic FDDB111, THE Source_Generator SHALL NOT generate any entity code for the Consumer_Assembly
5. WHEN the Source_Generator emits diagnostic FDDB111, THE Source_Generator SHALL emit diagnostic FDDB111 exactly once regardless of how many entities are declared in the Consumer_Assembly

### Requirement 5: Unrecognized Future Version Error

**User Story:** As a consumer developer, I want a clear error when my declared schema version is newer than what the installed package supports, so that I know I need to update the NuGet package.

#### Acceptance Criteria

1. WHEN the declared Schema_Version is newer than the Current_Schema_Version using major-then-minor numeric comparison (major compared first; if equal, minor compared), THE Source_Generator SHALL emit diagnostic FDDB112 with severity Error
2. THE diagnostic FDDB112 SHALL include the declared version formatted as "major.minor", the maximum supported version formatted as "major.minor", and a message instructing the consumer to update the Oproto.FluentDynamoDb package
3. THE Source_Generator SHALL report diagnostic FDDB112 at the location of the Schema_Version_Attribute declaration in the Consumer_Assembly source
4. WHEN the Source_Generator emits diagnostic FDDB112, THE Source_Generator SHALL NOT generate any entity code for the Consumer_Assembly
5. WHEN the Source_Generator emits diagnostic FDDB112, THE Source_Generator SHALL emit diagnostic FDDB112 exactly once regardless of how many entities are declared in the Consumer_Assembly

### Requirement 6: Older-but-Supported Version Info Diagnostic

**User Story:** As a consumer developer, I want an informational message when my declared version is older than current but still supported, so that I'm aware an upgrade path exists.

#### Acceptance Criteria

1. WHEN the declared Schema_Version is older than the Current_Schema_Version but at or above the Minimum_Supported_Version, THE Source_Generator SHALL emit exactly one diagnostic FDDB113 with severity Info per compilation
2. THE diagnostic FDDB113 SHALL include the declared version, the current version, and a URL to an upgrade guide
3. THE Source_Generator SHALL report diagnostic FDDB113 at the location of the Schema_Version_Attribute declaration in the Consumer_Assembly source
4. WHEN the Source_Generator emits diagnostic FDDB113, THE Source_Generator SHALL proceed with code generation using the declared Schema_Version shape

### Requirement 7: Version-Aware Code Generation

**User Story:** As a source generator developer, I want the generator to emit different code shapes based on the declared schema version, so that consumers can migrate at their own pace.

#### Acceptance Criteria

1. WHEN the declared Schema_Version matches the Current_Schema_Version (both major and minor are equal), THE Source_Generator SHALL generate code using the current schema shape
2. WHEN the declared Schema_Version has the same major version as the Current_Schema_Version but a lower minor version, THE Source_Generator SHALL generate code using the current major version shape (minor versions are additive-only and backward compatible within a major version)
3. WHEN the declared Schema_Version has an older major version than Current_Schema_Version but at or above Minimum_Supported_Version, THE Source_Generator SHALL generate code that compiles identically to code produced by a generator whose Current_Schema_Version matched the declared major version
4. THE Source_Generator SHALL support at most two concurrent major schema versions (current and one prior major version)

### Requirement 8: Schema Versioning Semantics

**User Story:** As a library maintainer, I want clear semantic rules for when to bump major vs minor versions, so that consumers understand the impact of version changes.

#### Acceptance Criteria

1. THE Schema_Version major component SHALL be incremented when any of the following breaking changes are introduced to generated code shapes: removal of a previously generated interface, method, or property; renaming of a previously generated interface, method, or property; change to the return type or parameter list of a previously generated method; change to the base type or implemented interfaces of a generated class
2. WHEN the Schema_Version major component is incremented, THE Schema_Version minor component SHALL be reset to 0
3. THE Schema_Version minor component SHALL be incremented when additive-only changes are made to generated code including: new methods added to existing generated classes, new interfaces implemented by generated classes, new generated classes or records that do not alter existing generated shapes
4. WHEN the Schema_Version minor component is incremented, THE Source_Generator SHALL ensure that code which compiled against any prior minor version within the same major version continues to compile without modification

### Requirement 9: Attribute Validation

**User Story:** As a consumer developer, I want the generator to validate my schema version declaration, so that obviously invalid values are caught at compile time.

#### Acceptance Criteria

1. WHEN the Major value in the Schema_Version_Attribute is less than 1, THE Source_Generator SHALL emit diagnostic FDDB114 with severity Error indicating the major version must be at least 1
2. WHEN the Minor value in the Schema_Version_Attribute is less than 0, THE Source_Generator SHALL emit diagnostic FDDB115 with severity Error indicating the minor version must be at least 0
3. WHEN both Major and Minor values are invalid, THE Source_Generator SHALL emit both diagnostic FDDB114 and FDDB115
4. WHEN the Consumer_Assembly contains more than one Schema_Version_Attribute (bypassing AllowMultiple via IL manipulation), THE Source_Generator SHALL use the first occurrence in attribute metadata order and emit diagnostic FDDB116 with severity Warning
5. WHEN the Source_Generator emits diagnostic FDDB114 or FDDB115, THE Source_Generator SHALL NOT generate any entity code for the Consumer_Assembly
