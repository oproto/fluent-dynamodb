# Bugfix Requirements Document

## Introduction

The `ExpressionTranslator` produces invalid DynamoDB expression syntax when translating bare boolean property access (`!x.IsDeleted`) and affirmative boolean property access (`x.IsActive`) in filter and condition expressions. DynamoDB rejects these with `Invalid FilterExpression: Syntax error; token: "<EOF>", near: ")"` because a bare attribute name placeholder is not a valid condition expression.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a negated bare boolean property `!x.IsDeleted` is used in a filter or condition expression THEN the system produces `NOT (#attr0)` which is invalid DynamoDB syntax and causes a runtime error

1.2 WHEN an affirmative bare boolean property `x.IsActive` is used as a standalone condition in a filter or condition expression THEN the system produces just `#attr0` which is not a valid DynamoDB condition expression

1.3 WHEN a negated nested boolean property `!x.Settings.IsEnabled` is used in a filter or condition expression THEN the system produces `NOT (#attr0.#attr1)` which is invalid DynamoDB syntax

1.4 WHEN an affirmative nested boolean property `x.Settings.IsEnabled` is used as a standalone condition in a filter or condition expression THEN the system produces just `#attr0.#attr1` which is not a valid DynamoDB condition expression

### Expected Behavior (Correct)

2.1 WHEN a negated bare boolean property `!x.IsDeleted` is used in a filter or condition expression THEN the system SHALL translate it to `#attr0 = :p0` where `:p0` has a BOOL value of `false`

2.2 WHEN an affirmative bare boolean property `x.IsActive` is used as a standalone condition in a filter or condition expression THEN the system SHALL translate it to `#attr0 = :p0` where `:p0` has a BOOL value of `true`

2.3 WHEN a negated nested boolean property `!x.Settings.IsEnabled` is used in a filter or condition expression THEN the system SHALL translate it to `#attr0.#attr1 = :p0` where `:p0` has a BOOL value of `false`

2.4 WHEN an affirmative nested boolean property `x.Settings.IsEnabled` is used as a standalone condition in a filter or condition expression THEN the system SHALL translate it to `#attr0.#attr1 = :p0` where `:p0` has a BOOL value of `true`

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a negated comparison expression `!(x.Age > 18)` is used THEN the system SHALL CONTINUE TO translate it to `NOT (#attr0 > :p0)` with correct attribute names and values

3.2 WHEN a negated equality expression `!(x.Status == "active")` is used THEN the system SHALL CONTINUE TO translate it to `NOT (#attr0 = :p0)` with correct attribute names and values

3.3 WHEN boolean properties are used within explicit comparisons `x.IsActive == true` or `x.IsDeleted == false` THEN the system SHALL CONTINUE TO translate them to `#attr0 = :p0` with correct boolean values

3.4 WHEN non-boolean expressions use the NOT operator `!(x.Name.Contains("test"))` THEN the system SHALL CONTINUE TO translate them using `NOT (...)` wrapping the valid condition expression

3.5 WHEN compound boolean expressions `x.IsActive && x.Age > 18` are used THEN the system SHALL CONTINUE TO translate them correctly with AND/OR operators
