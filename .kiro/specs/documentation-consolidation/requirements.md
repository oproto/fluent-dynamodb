# Requirements Document

## Introduction

This document defines the requirements for reorganizing and consolidating the Oproto.FluentDynamoDb documentation to prioritize modern, preferred usage patterns (source generation with expression formatting) while maintaining minimal documentation for legacy approaches. The goal is to provide clear guidance to users on the recommended way to use the library while keeping backward compatibility information accessible but de-emphasized.

## Glossary

- **Library**: The Oproto.FluentDynamoDb NuGet package and its components
- **Source Generation**: The automatic code generation feature that creates entity mapping, field constants, and key builders at compile time
- **Expression Formatting**: The string.Format-style parameter syntax for condition and filter expressions (e.g., `Where("pk = {0}", value)`)
- **Manual Table Pattern**: The lower-level DynamoDbTableBase approach requiring manual field name tracking, model conversion, and formatting
- **Manual Parameter Pattern**: The lower-level `.WithValue()` approach for parameters (e.g., `.WithValue(":pk", value)`)
- **Documentation**: All user-facing guides, examples, and reference materials in the repository
- **User**: A developer consuming the Oproto.FluentDynamoDb library
- **DynamoDB**: Amazon's NoSQL database service (always capitalized as "DynamoDB", never "dynamodb" or "Dynamodb" in prose)
- **AWS**: Amazon Web Services (always uppercase acronym)
- **NuGet**: The .NET package manager (always capitalized as "NuGet")
- **AOT**: Ahead-of-Time compilation (always uppercase acronym)
- **GSI**: Global Secondary Index (always uppercase acronym)
- **STS**: AWS Security Token Service (always uppercase acronym)
- **Partition Key**: The primary key component for data distribution in DynamoDB
- **Sort Key**: The optional key component for sorting items within a partition
- **Composite Entity**: An entity that spans multiple DynamoDB items
- **Related Entity**: An entity that is automatically populated based on sort key patterns

## Requirements

### Requirement 1: Prioritize Modern Approaches in Documentation

**User Story:** As a new user, I want to see the recommended modern approaches first, so that I learn best practices from the start.

#### Acceptance Criteria

1. WHEN THE User views the main README.md, THE Documentation SHALL present source generation with expression formatting as the primary approach
2. WHEN THE User views getting started sections, THE Documentation SHALL demonstrate source generation setup before any legacy patterns
3. WHEN THE User views code examples, THE Documentation SHALL show modern approaches first with legacy approaches clearly marked as "Legacy" or "Alternative"
4. WHERE examples demonstrate both approaches, THE Documentation SHALL visually distinguish modern from legacy with clear section headers
5. WHEN THE User searches for common operations, THE Documentation SHALL return modern approach examples in primary positions

### Requirement 2: Consolidate Redundant Documentation

**User Story:** As a user, I want consolidated documentation without repetition, so that I can find information efficiently without confusion.

#### Acceptance Criteria

1. WHEN THE Documentation contains duplicate explanations of the same feature, THE Library maintainers SHALL consolidate them into a single authoritative section
2. WHEN THE User views operation examples, THE Documentation SHALL provide one comprehensive example per operation type rather than scattered examples
3. WHERE format string support is documented, THE Documentation SHALL reference a single format specifier table rather than duplicating it
4. WHEN THE Documentation explains expression formatting, THE Documentation SHALL consolidate all format string examples into one dedicated section
5. WHILE maintaining backward compatibility information, THE Documentation SHALL avoid repeating the same legacy patterns across multiple files

### Requirement 3: Restructure Documentation Hierarchy

**User Story:** As a user, I want a clear documentation hierarchy, so that I can navigate from basic to advanced topics logically.

#### Acceptance Criteria

1. THE Documentation SHALL organize content into three tiers: Getting Started, Core Features, and Advanced Topics
2. WHEN THE User accesses Getting Started content, THE Documentation SHALL present source generation setup and basic CRUD operations with recommended patterns
3. WHEN THE User accesses Core Features content, THE Documentation SHALL cover queries, updates, batch operations, and transactions using recommended approaches
4. WHEN THE User accesses Advanced Topics content, THE Documentation SHALL cover multi-item entities, related entities, STS integration, and performance optimization
5. WHERE manual patterns are documented, THE Documentation SHALL place them in a separate "Manual/Lower-Level Patterns" section within Advanced Topics

### Requirement 4: Minimize Manual Pattern Documentation

**User Story:** As a user, I want manual patterns documented minimally, so that I'm not confused about which approach to use.

#### Acceptance Criteria

1. WHEN THE Documentation presents manual patterns, THE Documentation SHALL include a clear notice stating "You can also use this lower-level approach. The source generation approach is recommended for most use cases"
2. THE Documentation SHALL limit manual pattern examples to one representative example per operation type
3. WHEN THE User views the main README.md, THE Documentation SHALL not include manual pattern examples in the primary flow
4. WHERE manual patterns are documented, THE Documentation SHALL place them in a dedicated "Manual/Lower-Level Patterns" section
5. THE Documentation SHALL remove redundant manual examples that duplicate information available in the manual patterns reference section

### Requirement 5: Create Clear Adoption Guidance

**User Story:** As a user choosing between approaches, I want clear guidance on when to use each pattern, so that I can make informed decisions.

#### Acceptance Criteria

1. THE Documentation SHALL provide guidance explaining when source generation is recommended versus when manual patterns might be appropriate
2. WHEN THE User views approach comparisons, THE Documentation SHALL show side-by-side code examples highlighting the differences
3. THE Documentation SHALL explain which patterns can be mixed together in the same codebase
4. WHEN THE User adopts source generation, THE Documentation SHALL provide a checklist of setup steps
5. THE Documentation SHALL include guidance for scenarios where manual patterns may be necessary (e.g., dynamic table names, runtime schema)

### Requirement 6: Improve Documentation Discoverability

**User Story:** As a user, I want to quickly find relevant documentation, so that I can solve problems without extensive searching.

#### Acceptance Criteria

1. THE Documentation SHALL include a comprehensive table of contents in the main README.md with direct links to all major topics
2. WHEN THE User views any documentation file, THE Documentation SHALL include breadcrumb navigation showing the current location in the hierarchy
3. THE Documentation SHALL provide cross-references between related topics using consistent linking patterns
4. WHEN THE User searches for a specific operation, THE Documentation SHALL include searchable keywords and tags
5. THE Documentation SHALL maintain a quick reference section with common operations and their modern syntax

### Requirement 7: Standardize Code Example Format

**User Story:** As a user, I want consistent code examples, so that I can easily understand and copy patterns.

#### Acceptance Criteria

1. WHEN THE Documentation presents code examples, THE Documentation SHALL use consistent formatting with syntax highlighting
2. THE Documentation SHALL include inline comments explaining non-obvious code sections
3. WHEN THE Documentation shows entity definitions, THE Documentation SHALL include complete, runnable examples with all required attributes
4. THE Documentation SHALL mark optional code sections clearly with comments like "// Optional: ..."
5. WHERE examples demonstrate error handling, THE Documentation SHALL show realistic exception handling patterns

### Requirement 8: Update Main README Structure

**User Story:** As a user, I want the main README to guide me to the right documentation, so that I can get started quickly.

#### Acceptance Criteria

1. THE README.md SHALL begin with a brief overview of the library and its key benefits
2. WHEN THE User views the README.md, THE README SHALL present a "Quick Start" section using source generation and expression formatting
3. THE README.md SHALL include a "Documentation Guide" section explaining the documentation structure and where to find specific topics
4. THE README.md SHALL limit code examples to essential patterns, linking to detailed guides for comprehensive coverage
5. THE README.md SHALL include a clear "Approaches" section explaining the recommended source generation approach and when manual patterns might be used

### Requirement 9: Separate Concerns in Documentation Files

**User Story:** As a user, I want focused documentation files, so that I can read about one topic without distraction.

#### Acceptance Criteria

1. THE Documentation SHALL separate source generation documentation from manual fluent API documentation
2. WHEN THE Documentation covers expression formatting, THE Documentation SHALL maintain it in a dedicated file separate from manual parameter patterns
3. THE Documentation SHALL create separate files for batch operations, transactions, and stream processing
4. WHERE documentation covers multiple related topics, THE Documentation SHALL use clear section boundaries with navigation links
5. THE Documentation SHALL limit each file to a single primary concern with cross-references to related topics

### Requirement 10: Maintain Compatibility Information

**User Story:** As a user, I want to understand how different approaches work together, so that I can use the library flexibly.

#### Acceptance Criteria

1. THE Documentation SHALL explicitly state that all approaches (source generation and manual patterns) are fully supported
2. WHEN THE Documentation introduces recommended patterns, THE Documentation SHALL note that manual patterns remain available for specific use cases
3. THE Documentation SHALL provide a compatibility matrix showing which patterns work together
4. WHERE limitations exist for specific approaches, THE Documentation SHALL clearly document them with alternative solutions
5. THE Documentation SHALL include version information indicating when features were introduced
