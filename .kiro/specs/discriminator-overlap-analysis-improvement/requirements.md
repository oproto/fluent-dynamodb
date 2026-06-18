# Requirements Document

## Introduction

This document specifies the requirements for improving the discriminator pattern overlap analysis in the Oproto.FluentDynamoDb source generator. The current `PatternOverlapAnalyzer` uses overly conservative overlap detection for Contains and Complex strategy patterns, producing false-positive DISC004 errors for common multi-entity table designs. These improvements refine the overlap detection algorithms to perform structural analysis of literal segments, eliminating false positives while maintaining correctness for genuinely ambiguous patterns.

## Glossary

- **Pattern_Overlap_Analyzer**: The static analysis component (`PatternOverlapAnalyzer`) within the source generator that determines whether two discriminator patterns can match the same DynamoDB attribute value.
- **DISC004_Diagnostic**: An error-severity diagnostic emitted when two overlapping discriminator patterns have the same specificity score and cannot be automatically resolved.
- **DISC005_Diagnostic**: An informational diagnostic emitted when overlapping discriminator patterns are resolved by specificity ordering, with the less-specific entity receiving exclusion guards.
- **Contains_Strategy**: A discriminator matching strategy for patterns of the form `*literal*`, matching any string containing the literal.
- **Complex_Strategy**: A discriminator matching strategy for patterns with multiple wildcards (e.g., `PREFIX#*#SUFFIX#*`), matching structured multi-segment key values.
- **StartsWith_Strategy**: A discriminator matching strategy for patterns of the form `literal*`, matching strings beginning with the literal.
- **EndsWith_Strategy**: A discriminator matching strategy for patterns of the form `*literal`, matching strings ending with the literal.
- **Literal_Segment**: A non-empty substring between wildcard characters in a pattern, extracted by splitting the pattern on `*`.
- **Wildcard_Structure**: The arrangement of wildcards and literals in a pattern, specifically whether the pattern starts with a wildcard, ends with a wildcard, and the number of literal segments.
- **Distinguishing_Segment**: A pair of literal segments at the same structural position in two patterns where neither segment is a substring of the other, proving the patterns cannot match the same string.
- **Specificity_Score**: A numeric value representing how specific a discriminator pattern is; ExactMatch returns `int.MaxValue`, wildcard patterns return the count of non-empty literal segments.
- **Exclusion_Guard**: A condition added to a less-specific entity's `MatchesEntity` method that excludes items matching a more-specific overlapping pattern.

## Requirements

### Requirement 1: Contains Pattern Overlap Detection

**User Story:** As a developer using multi-entity DynamoDB tables with Contains-strategy discriminators, I want the source generator to correctly identify non-overlapping Contains patterns, so that I do not receive false DISC004 errors for patterns with unrelated literals.

#### Acceptance Criteria

1. WHEN two Contains-strategy patterns share the same discriminator property and neither literal is a substring of the other, THE Pattern_Overlap_Analyzer SHALL determine the patterns are non-overlapping and return false from `PatternsOverlap`.
2. WHEN two Contains-strategy patterns share the same discriminator property and one literal is a substring of the other, THE Pattern_Overlap_Analyzer SHALL determine the patterns are overlapping and return true from `PatternsOverlap`.
3. WHEN two Contains-strategy patterns have identical literals, THE Pattern_Overlap_Analyzer SHALL determine the patterns are overlapping and return true from `PatternsOverlap`.

### Requirement 2: Complex Pattern Overlap Detection with Same Wildcard Structure

**User Story:** As a developer using hierarchical sort key patterns (e.g., `EMPLOYEE#*#PAYRATE#*` and `EMPLOYEE#*#DEDUCTION#*`), I want the source generator to recognize that patterns with different distinguishing segments cannot overlap, so that sibling entity patterns do not produce false DISC004 errors.

#### Acceptance Criteria

1. WHEN two Complex-strategy patterns have the same wildcard structure and at least one corresponding segment pair is a distinguishing segment, THE Pattern_Overlap_Analyzer SHALL determine the patterns are non-overlapping and return false from `PatternsOverlap`.
2. WHEN two Complex-strategy patterns have the same wildcard structure and all corresponding segment pairs have a substring relationship, THE Pattern_Overlap_Analyzer SHALL determine the patterns are overlapping and return true from `PatternsOverlap`.
3. WHEN two patterns share the same discriminator property and at least one is Complex-strategy, THE Pattern_Overlap_Analyzer SHALL route the comparison through the Complex pattern analysis logic rather than assuming overlap.

### Requirement 3: Complex Pattern Overlap Detection with Different Wildcard Structure

**User Story:** As a developer using parent-child entity patterns where a StartsWith parent pattern subsumes Complex child patterns (e.g., `EMPLOYEE#*` vs `EMPLOYEE#*#PAYRATE#*`), I want the source generator to correctly detect overlaps by structural subsumption, so that specificity-based resolution produces correct exclusion guards.

#### Acceptance Criteria

1. WHEN a Complex-strategy pattern is compared with a non-Complex pattern and all literal segments of the less-structured pattern appear as substrings in the more-structured pattern, THE Pattern_Overlap_Analyzer SHALL determine the patterns are overlapping and return true from `PatternsOverlap`.
2. WHEN a Complex-strategy pattern is compared with a non-Complex pattern and at least one literal segment of the less-structured pattern does not appear as a substring in the more-structured pattern, THE Pattern_Overlap_Analyzer SHALL determine the patterns are non-overlapping and return false from `PatternsOverlap`.
3. WHEN two patterns with different wildcard structures overlap and have different specificity scores, THE Pattern_Overlap_Analyzer SHALL emit a DISC005_Diagnostic and assign exclusion guards to the less-specific entity.

### Requirement 4: Symmetry of Overlap Detection

**User Story:** As a developer, I want the overlap detection to produce the same result regardless of the order in which two patterns are compared, so that the analysis is deterministic and consistent.

#### Acceptance Criteria

1. THE Pattern_Overlap_Analyzer SHALL produce the same overlap result for `PatternsOverlap(A, B)` and `PatternsOverlap(B, A)` for any two valid DiscriminatorConfig instances sharing the same discriminator property.

### Requirement 5: Backward Compatibility for StartsWith and EndsWith Strategies

**User Story:** As a developer using StartsWith or EndsWith discriminator patterns, I want the existing overlap detection behavior to remain unchanged, so that my current configurations continue to work correctly.

#### Acceptance Criteria

1. WHEN two StartsWith-strategy patterns share the same discriminator property, THE Pattern_Overlap_Analyzer SHALL determine overlap based on whether one literal is a prefix of the other.
2. WHEN two EndsWith-strategy patterns share the same discriminator property, THE Pattern_Overlap_Analyzer SHALL determine overlap based on whether one literal is a suffix of the other.
3. WHEN a StartsWith-strategy pattern and an EndsWith-strategy pattern share the same discriminator property, THE Pattern_Overlap_Analyzer SHALL determine overlap based on whether one literal is a substring of the other.

### Requirement 6: Real-World Employee Payroll Pattern

**User Story:** As a developer modeling an employee payroll system with Employee, PayRate, Deduction, and Garnishment entities sharing a single table, I want the source generator to correctly analyze the overlap relationships without false DISC004 errors, so that my multi-entity table design compiles cleanly.

#### Acceptance Criteria

1. WHEN entities use Complex-strategy patterns `EMPLOYEE#*#PAYRATE#*`, `EMPLOYEE#*#DEDUCTION#*`, and `EMPLOYEE#*#GARNISHMENT#*` on the same discriminator property, THE Pattern_Overlap_Analyzer SHALL produce zero DISC004_Diagnostic errors between any pair of these three patterns.
2. WHEN a StartsWith-strategy pattern `EMPLOYEE#*` coexists with Complex-strategy patterns `EMPLOYEE#*#PAYRATE#*`, `EMPLOYEE#*#DEDUCTION#*`, and `EMPLOYEE#*#GARNISHMENT#*` on the same discriminator property, THE Pattern_Overlap_Analyzer SHALL emit a DISC005_Diagnostic for each overlapping pair resolved by specificity.
3. WHEN the StartsWith-strategy pattern `EMPLOYEE#*` overlaps with the three Complex-strategy child patterns, THE Pattern_Overlap_Analyzer SHALL assign exclusion guards to the Employee entity for each of the three more-specific child patterns.
4. WHEN entities use Contains-strategy patterns `*#DEDUCTION#*`, `*#GARNISHMENT#*`, and `*#PAYRATE#*` on the same discriminator property, THE Pattern_Overlap_Analyzer SHALL produce zero DISC004_Diagnostic errors between any pair of these three patterns.

### Requirement 7: Conservative Behavior for Ambiguous Patterns

**User Story:** As a developer, I want the overlap analyzer to conservatively report overlap when structural analysis is inconclusive, so that genuinely ambiguous patterns are never silently ignored.

#### Acceptance Criteria

1. WHEN a Complex-strategy pattern has null or empty literal segments after extraction, THE Pattern_Overlap_Analyzer SHALL conservatively assume overlap and return true from `PatternsOverlap`.
2. WHEN structural analysis of two Complex-strategy patterns with different wildcard structures cannot definitively prove non-overlap, THE Pattern_Overlap_Analyzer SHALL conservatively assume overlap and return true from `PatternsOverlap`.

### Requirement 8: Literal Segment Extraction

**User Story:** As a developer, I want the pattern analyzer to correctly decompose patterns into their constituent literal segments, so that structural comparisons operate on the correct data.

#### Acceptance Criteria

1. WHEN extracting literal segments from a pattern, THE Pattern_Overlap_Analyzer SHALL split the pattern on wildcard characters and return only non-empty segments in their original order.
2. WHEN extracting literal segments from pattern `EMPLOYEE#*#DEDUCTION#*`, THE Pattern_Overlap_Analyzer SHALL produce the segments `["EMPLOYEE#", "#DEDUCTION#"]`.
3. WHEN extracting literal segments from pattern `*#DEDUCTION#*`, THE Pattern_Overlap_Analyzer SHALL produce the segments `["#DEDUCTION#"]`.

### Requirement 9: Wildcard Structure Comparison

**User Story:** As a developer, I want the pattern analyzer to correctly identify when two patterns have the same wildcard structure, so that the appropriate comparison algorithm is selected.

#### Acceptance Criteria

1. WHEN comparing wildcard structures, THE Pattern_Overlap_Analyzer SHALL consider two patterns to have the same structure only when both patterns agree on whether they start with a wildcard AND both agree on whether they end with a wildcard AND both have the same number of literal segments.
2. WHEN patterns `EMPLOYEE#*#DEDUCTION#*` and `EMPLOYEE#*#GARNISHMENT#*` are compared, THE Pattern_Overlap_Analyzer SHALL identify them as having the same wildcard structure.
3. WHEN patterns `EMPLOYEE#*#DEDUCTION#*` and `*#DEDUCTION#*` are compared, THE Pattern_Overlap_Analyzer SHALL identify them as having different wildcard structures.

### Requirement 10: Public API Stability

**User Story:** As a developer consuming the source generator, I want the public API of `PatternOverlapAnalyzer` to remain unchanged, so that no breaking changes are introduced.

#### Acceptance Criteria

1. THE Pattern_Overlap_Analyzer SHALL maintain the existing method signatures for `ComputeSpecificityScore`, `PatternsOverlap`, and `Analyze` without modification.
2. THE Pattern_Overlap_Analyzer SHALL continue to return the same data types and use the same parameter types as the current implementation.
