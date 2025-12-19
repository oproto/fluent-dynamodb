; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FDDB006 | DynamoDb | Error | Conflicting table namespaces in multi-entity tables
FDDB0020 | DynamoDb | Error | [EnableDynamicFields] requires partial class
FDDB0021 | DynamoDb | Warning | DynamicFields property already exists
FDDB050 | DynamoDb | Error | Conflicting index Name values across entities
FDDB051 | DynamoDb | Error | Non-partial table type in type-based table reference
FDDB052 | DynamoDb | Warning | Redundant index Name specification
FDDB060 | DynamoDb | Error | Projection source entity not found
FDDB061 | DynamoDb | Error | Projection metadata inheritance failure
FDDB062 | DynamoDb | Error | Projection interface violation
DYNDB113 | DynamoDb | Warning | Deprecated [Queryable] attribute usage
DYNDB115 | DynamoDb | Error | [BlobStorage] requires BlobData<T> property type
