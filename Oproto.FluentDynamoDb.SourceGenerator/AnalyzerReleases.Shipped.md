; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DYNDB001 | DynamoDb | Error | Missing partition key
DYNDB002 | DynamoDb | Error | Multiple partition keys
DYNDB003 | DynamoDb | Error | Multiple sort keys
DYNDB004 | DynamoDb | Error | Invalid key format
DYNDB005 | DynamoDb | Error | Conflicting entity types
DYNDB006 | DynamoDb | Error | Invalid GSI configuration
DYNDB007 | DynamoDb | Error | Missing DynamoDbAttribute
DYNDB008 | DynamoDb | Warning | Ambiguous related entity pattern
DYNDB009 | DynamoDb | Error | Unsupported property type
DYNDB010 | DynamoDb | Error | Entity must be partial
DYNDB011 | DynamoDb | Error | Multi-item entity missing partition key
DYNDB012 | DynamoDb | Warning | Multi-item entity missing sort key
DYNDB013 | DynamoDb | Error | Collection property cannot be key
DYNDB014 | DynamoDb | Warning | Multi-item entity partition key format
DYNDB015 | DynamoDb | Error | Invalid related entity type
DYNDB016 | DynamoDb | Warning | Related entities require sort key
DYNDB017 | DynamoDb | Warning | Conflicting related entity patterns
DYNDB018 | DynamoDb | Error | Invalid key format syntax
DYNDB019 | DynamoDb | Warning | Potential key collision
DYNDB020 | DynamoDb | Error | Circular reference detected
DYNDB021 | DynamoDb | Warning | Reserved word usage
DYNDB022 | DynamoDb | Error | Invalid DynamoDB configuration
DYNDB023 | DynamoDb | Warning | Performance warning
DYNDB024 | DynamoDb | Error | Missing required attribute
DYNDB025 | DynamoDb | Warning | Potential data loss
DYNDB026 | DynamoDb | Error | Invalid GSI projection
DYNDB027 | DynamoDb | Warning | Scalability warning
DYNDB028 | DynamoDb | Error | Unsupported type conversion
DYNDB029 | DynamoDb | Warning | Too many attributes
DYNDB030 | DynamoDb | Error | Invalid attribute name
DYNDB031 | DynamoDb | Error | Invalid computed key source
DYNDB032 | DynamoDb | Error | Invalid extracted key source
DYNDB033 | DynamoDb | Error | Circular key dependency
DYNDB034 | DynamoDb | Error | Self-referencing computed key
DYNDB035 | DynamoDb | Error | Invalid extracted key index
DYNDB036 | DynamoDb | Warning | Invalid computed key format
DYNDB101 | DynamoDb | Error | Invalid TTL property type
DYNDB102 | DynamoDb | Error | Missing JSON serializer package
DYNDB103 | DynamoDb | Error | Missing blob provider package
DYNDB104 | DynamoDb | Error | Incompatible attribute combination
DYNDB105 | DynamoDb | Error | Multiple TTL fields
DYNDB106 | DynamoDb | Error | Unsupported collection type
DYNDB107 | DynamoDb | Error | Nested map type missing [DynamoDbEntity]
DYNDB108 | DynamoDb | Error | S2Level specified without S2 index type
DYNDB109 | DynamoDb | Error | H3Resolution specified without H3 index type
DYNDB110 | DynamoDb | Error | GeoHashPrecision specified without GeoHash index type
DYNDB111 | DynamoDb | Error | Spatial index configuration on non-GeoLocation property
DYNDB112 | DynamoDb | Warning | Missing Geospatial package
DYNDB1001 | DynamoDb | Error | Invalid GenerateWrapper usage
DYNDB1002 | DynamoDb | Error | Invalid extension method
DYNDB1003 | DynamoDb | Warning | Interface not found
DYNDB1004 | DynamoDb | Warning | Interface not implemented
PROJ001 | DynamoDb | Error | Projection property not found
PROJ002 | DynamoDb | Error | Projection property type mismatch
PROJ003 | DynamoDb | Error | Invalid projection source entity
PROJ004 | DynamoDb | Error | Projection must be partial
PROJ005 | DynamoDb | Error | UseProjection references invalid type
PROJ006 | DynamoDb | Error | Conflicting UseProjection attributes
PROJ101 | DynamoDb | Warning | Projection includes all properties
PROJ102 | DynamoDb | Warning | Projection has many properties
DISC001 | DynamoDb | Warning | Both DiscriminatorValue and DiscriminatorPattern specified
DISC002 | DynamoDb | Error | DiscriminatorValue or DiscriminatorPattern without DiscriminatorProperty
DISC003 | DynamoDb | Error | Invalid discriminator pattern syntax
SEC001 | DynamoDb | Warning | Missing Encryption.Kms package
SEC002 | DynamoDb | Error | Missing Amazon.Lambda.DynamoDBEvents package
FDDB001 | DynamoDb | Error | No default entity specified
FDDB002 | DynamoDb | Error | Multiple default entities
FDDB003 | DynamoDb | Error | Conflicting accessor configuration
FDDB004 | DynamoDb | Error | Empty entity property name
FDDB005 | DynamoDb | Warning | Inconsistent discriminator properties
