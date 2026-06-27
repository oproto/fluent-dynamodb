# Diagnostics Reference

This reference documents all **110** diagnostic codes emitted by the Oproto.FluentDynamoDb source generator. Each code links to a detailed page with the message format, description, triggering example, and fix.

## Numbering Conventions

| Prefix | Code Range | Domain |
|--------|-----------|--------|
| DISC | 001–006 | Discriminator configuration and pattern matching |
| DYNDB | 001–036, 101–115, 120–127, 1001–1004 | Core DynamoDB entity validation and mapping |
| FDDB | 001–006, 0020–0021, 050–055, 060–062, 070–072, 080, 090, 100–103, 110–116 | Table/index generation and configuration |
| PROJ | 001–006, 101–102 | Projection model validation |
| SEC | 001–002 | Security and package dependency checks |

### Prefix Distinction: DYNDB vs FDDB

Both `DYNDB` and `FDDB` prefixes relate to the FluentDynamoDb source generator:

- **DYNDB** covers **core entity validation** — constraints on individual entity properties, key formats, type compatibility, and attribute correctness.
- **FDDB** covers **table and index generation** — multi-entity table configuration, index consolidation, projection wiring, and computed key format validation.

### DYNDB Range Bands

The `DYNDB` prefix uses range bands to group related diagnostics by functional area:

| Range | Area |
|-------|------|
| 001–036 | Core validation (keys, types, attributes, references) |
| 101–115 | Advanced type system (TTL, JSON, blobs, spatial, deprecation) |
| 120–127 | Index attribute validation (GSI/LSI configuration) |
| 1001–1004 | Extension method wrapper generation |

The varying digit widths within DYNDB (3-digit, 4-digit) reflect these logical groupings rather than an inconsistency.

### FDDB Numbering Inconsistency

Codes `FDDB0020` and `FDDB0021` use four-digit numbering while other FDDB codes use three-digit numbering. This is a known inconsistency retained for backward compatibility — these codes were introduced before the current naming convention was established and cannot be renumbered without breaking existing suppressions.

## DISC — Discriminator Configuration

| Code | Severity | Title |
|------|----------|-------|
| [DISC001](DISC/DISC001.md) | Warning | Both DiscriminatorValue and DiscriminatorPattern specified |
| [DISC002](DISC/DISC002.md) | Error | DiscriminatorValue or DiscriminatorPattern without DiscriminatorProperty |
| [DISC003](DISC/DISC003.md) | Error | Invalid discriminator pattern syntax |
| [DISC004](DISC/DISC004.md) | Error | Ambiguous overlapping discriminator patterns |
| [DISC005](DISC/DISC005.md) | Info | Overlapping discriminator pattern resolved |
| [DISC006](DISC/DISC006.md) | Error | Tautological exclusion guard detected |

## DYNDB — Core Entity Validation

| Code | Severity | Title |
|------|----------|-------|
| [DYNDB001](DYNDB/DYNDB001.md) | Error | Missing partition key |
| [DYNDB002](DYNDB/DYNDB002.md) | Error | Multiple partition keys |
| [DYNDB003](DYNDB/DYNDB003.md) | Error | Multiple sort keys |
| [DYNDB004](DYNDB/DYNDB004.md) | Error | Invalid key format |
| [DYNDB005](DYNDB/DYNDB005.md) | Error | Conflicting entity types |
| [DYNDB006](DYNDB/DYNDB006.md) | Error | Invalid GSI configuration |
| [DYNDB007](DYNDB/DYNDB007.md) | Error | Missing DynamoDbAttribute |
| [DYNDB008](DYNDB/DYNDB008.md) | Warning | Ambiguous related entity pattern |
| [DYNDB009](DYNDB/DYNDB009.md) | Error | Unsupported property type |
| [DYNDB010](DYNDB/DYNDB010.md) | Error | Entity must be partial |
| [DYNDB011](DYNDB/DYNDB011.md) | Error | Multi-item entity missing partition key |
| [DYNDB012](DYNDB/DYNDB012.md) | Warning | Multi-item entity missing sort key |
| [DYNDB013](DYNDB/DYNDB013.md) | Error | Collection property cannot be key |
| [DYNDB014](DYNDB/DYNDB014.md) | Warning | Multi-item entity partition key format |
| [DYNDB015](DYNDB/DYNDB015.md) | Error | Invalid related entity type |
| [DYNDB016](DYNDB/DYNDB016.md) | Warning | Related entities require sort key |
| [DYNDB017](DYNDB/DYNDB017.md) | Warning | Conflicting related entity patterns |
| [DYNDB018](DYNDB/DYNDB018.md) | Error | Invalid key format syntax |
| [DYNDB019](DYNDB/DYNDB019.md) | Warning | Potential key collision |
| [DYNDB020](DYNDB/DYNDB020.md) | Error | Circular reference detected |
| [DYNDB021](DYNDB/DYNDB021.md) | Warning | Reserved word usage |
| [DYNDB022](DYNDB/DYNDB022.md) | Error | Invalid DynamoDB configuration |
| [DYNDB023](DYNDB/DYNDB023.md) | Warning | Performance warning |
| [DYNDB024](DYNDB/DYNDB024.md) | Error | Missing required attribute |
| [DYNDB025](DYNDB/DYNDB025.md) | Warning | Potential data loss |
| [DYNDB026](DYNDB/DYNDB026.md) | Error | Invalid GSI projection |
| [DYNDB027](DYNDB/DYNDB027.md) | Warning | Scalability warning |
| [DYNDB028](DYNDB/DYNDB028.md) | Error | Unsupported type conversion |
| [DYNDB029](DYNDB/DYNDB029.md) | Warning | Too many attributes |
| [DYNDB030](DYNDB/DYNDB030.md) | Error | Invalid attribute name |
| [DYNDB031](DYNDB/DYNDB031.md) | Error | Invalid computed key source |
| [DYNDB032](DYNDB/DYNDB032.md) | Error | Invalid extracted key source |
| [DYNDB033](DYNDB/DYNDB033.md) | Error | Circular key dependency |
| [DYNDB034](DYNDB/DYNDB034.md) | Error | Self-referencing computed key |
| [DYNDB035](DYNDB/DYNDB035.md) | Error | Invalid extracted key index |
| [DYNDB036](DYNDB/DYNDB036.md) | Warning | Invalid computed key format |
| [DYNDB101](DYNDB/DYNDB101.md) | Error | Invalid TTL property type |
| [DYNDB102](DYNDB/DYNDB102.md) | Error | Missing JSON serializer package |
| [DYNDB103](DYNDB/DYNDB103.md) | Error | Missing blob provider package |
| [DYNDB104](DYNDB/DYNDB104.md) | Error | Incompatible attribute combination |
| [DYNDB105](DYNDB/DYNDB105.md) | Error | Multiple TTL fields |
| [DYNDB106](DYNDB/DYNDB106.md) | Error | Unsupported collection type |
| [DYNDB107](DYNDB/DYNDB107.md) | Error | Nested map type missing [DynamoDbEntity] |
| [DYNDB108](DYNDB/DYNDB108.md) | Error | S2Level specified without S2 index type |
| [DYNDB109](DYNDB/DYNDB109.md) | Error | H3Resolution specified without H3 index type |
| [DYNDB110](DYNDB/DYNDB110.md) | Error | GeoHashPrecision specified without GeoHash index type |
| [DYNDB111](DYNDB/DYNDB111.md) | Error | Spatial index configuration on non-GeoLocation property |
| [DYNDB112](DYNDB/DYNDB112.md) | Warning | Missing Geospatial package |
| [DYNDB113](DYNDB/DYNDB113.md) | Warning | Deprecated [Queryable] attribute |
| [DYNDB115](DYNDB/DYNDB115.md) | Error | BlobStorage requires BlobData<T> type |
| [DYNDB120](DYNDB/DYNDB120.md) | Error | GSI sort key without partition key |
| [DYNDB121](DYNDB/DYNDB121.md) | Error | Duplicate GSI partition keys |
| [DYNDB122](DYNDB/DYNDB122.md) | Error | Duplicate GSI sort keys |
| [DYNDB123](DYNDB/DYNDB123.md) | Error | Duplicate LSI sort keys |
| [DYNDB124](DYNDB/DYNDB124.md) | Error | Empty GsiPartitionKey index name |
| [DYNDB125](DYNDB/DYNDB125.md) | Error | Empty GsiSortKey index name |
| [DYNDB126](DYNDB/DYNDB126.md) | Error | Empty LsiSortKey index name |
| [DYNDB127](DYNDB/DYNDB127.md) | Error | GSI/LSI index name conflict |
| [DYNDB1001](DYNDB/DYNDB1001.md) | Error | Invalid GenerateWrapper usage |
| [DYNDB1002](DYNDB/DYNDB1002.md) | Error | Invalid extension method |
| [DYNDB1003](DYNDB/DYNDB1003.md) | Warning | Interface not found |
| [DYNDB1004](DYNDB/DYNDB1004.md) | Warning | Interface not implemented |

## FDDB — Table/Index Generation

| Code | Severity | Title |
|------|----------|-------|
| [FDDB001](FDDB/FDDB001.md) | Error | No default entity specified |
| [FDDB002](FDDB/FDDB002.md) | Error | Multiple default entities |
| [FDDB003](FDDB/FDDB003.md) | Error | Conflicting accessor configuration |
| [FDDB004](FDDB/FDDB004.md) | Error | Empty entity property name |
| [FDDB005](FDDB/FDDB005.md) | Warning | Inconsistent discriminator properties |
| [FDDB006](FDDB/FDDB006.md) | Error | Conflicting table namespaces |
| [FDDB0020](FDDB/FDDB0020.md) | Error | EnableDynamicFields requires partial class |
| [FDDB0021](FDDB/FDDB0021.md) | Warning | DynamicFields property already exists |
| [FDDB050](FDDB/FDDB050.md) | Error | Conflicting index Name values |
| [FDDB051](FDDB/FDDB051.md) | Error | Non-partial table type |
| [FDDB052](FDDB/FDDB052.md) | Warning | Redundant index Name specification |
| [FDDB053](FDDB/FDDB053.md) | Error | Conflicting index partition key attribute |
| [FDDB054](FDDB/FDDB054.md) | Error | Conflicting index sort key attribute |
| [FDDB055](FDDB/FDDB055.md) | Error | Conflicting index type |
| [FDDB060](FDDB/FDDB060.md) | Error | Projection source entity not found |
| [FDDB061](FDDB/FDDB061.md) | Error | Projection metadata inheritance failure |
| [FDDB062](FDDB/FDDB062.md) | Error | Projection interface violation |
| [FDDB070](FDDB/FDDB070.md) | Warning | Include projection without properties |
| [FDDB072](FDDB/FDDB072.md) | Warning | KeysOnly with UseProjection |
| [FDDB080](FDDB/FDDB080.md) | Error | Unresolvable source property in computed key |
| [FDDB090](FDDB/FDDB090.md) | Error | Format placeholder count mismatch |
| [FDDB100](FDDB/FDDB100.md) | Error | Key prefix conflicts with explicit computed format |
| [FDDB101](FDDB/FDDB101.md) | Error | Explicit discriminator pattern conflicts with key format |
| [FDDB102](FDDB/FDDB102.md) | Warning | Overlapping auto-derived discriminator patterns |
| [FDDB103](FDDB/FDDB103.md) | Info | Redundant explicit discriminator pattern |
| [FDDB110](FDDB/FDDB110.md) | Warning | Missing schema version attribute |
| [FDDB111](FDDB/FDDB111.md) | Error | Declared version below minimum supported |
| [FDDB112](FDDB/FDDB112.md) | Error | Declared version above current |
| [FDDB113](FDDB/FDDB113.md) | Info | Older-but-supported version, upgrade available |
| [FDDB114](FDDB/FDDB114.md) | Error | Major version less than 1 |
| [FDDB115](FDDB/FDDB115.md) | Error | Minor version less than 0 |
| [FDDB116](FDDB/FDDB116.md) | Error | Multiple schema version attributes detected |

## PROJ — Projection Model Validation

| Code | Severity | Title |
|------|----------|-------|
| [PROJ001](PROJ/PROJ001.md) | Error | Projection property not found |
| [PROJ002](PROJ/PROJ002.md) | Error | Projection property type mismatch |
| [PROJ003](PROJ/PROJ003.md) | Error | Invalid projection source entity |
| [PROJ004](PROJ/PROJ004.md) | Error | Projection must be partial |
| [PROJ005](PROJ/PROJ005.md) | Error | UseProjection references invalid type |
| [PROJ006](PROJ/PROJ006.md) | Error | Conflicting UseProjection attributes |
| [PROJ101](PROJ/PROJ101.md) | Warning | Projection includes all properties |
| [PROJ102](PROJ/PROJ102.md) | Warning | Projection has many properties |

## SEC — Security and Package Dependencies

| Code | Severity | Title |
|------|----------|-------|
| [SEC001](SEC/SEC001.md) | Warning | Missing Encryption.Kms package |
| [SEC002](SEC/SEC002.md) | Error | Missing Amazon.Lambda.DynamoDBEvents package |
