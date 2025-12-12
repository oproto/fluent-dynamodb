; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FDDB006 | DynamoDb | Error | Conflicting table namespaces in multi-entity tables
FDDB0020 | DynamoDb | Error | [EnableDynamicFields] requires partial class
FDDB0021 | DynamoDb | Warning | DynamicFields property already exists
DYNDB113 | DynamoDb | Warning | Deprecated [Queryable] attribute usage
DYNDB115 | DynamoDb | Error | [BlobStorage] requires BlobData<T> property type
