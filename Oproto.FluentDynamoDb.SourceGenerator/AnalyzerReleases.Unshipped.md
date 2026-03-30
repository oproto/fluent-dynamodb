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
FDDB053 | DynamoDb | Error | Conflicting index partition key across entities
FDDB054 | DynamoDb | Error | Conflicting index sort key across entities
FDDB055 | DynamoDb | Error | Conflicting index type (GSI vs LSI) across entities
FDDB060 | DynamoDb | Error | Projection source entity not found
FDDB061 | DynamoDb | Error | Projection metadata inheritance failure
FDDB062 | DynamoDb | Error | Projection interface violation
FDDB070 | DynamoDb | Warning | Include projection without properties
FDDB072 | DynamoDb | Warning | KeysOnly with UseProjection
DYNDB113 | DynamoDb | Warning | Deprecated [Queryable] attribute usage
DYNDB115 | DynamoDb | Error | [BlobStorage] requires BlobData<T> property type
