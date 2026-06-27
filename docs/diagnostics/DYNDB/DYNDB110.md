# DYNDB110: GeoHashPrecision specified without GeoHash index type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB110` |
| Severity | Error |

## Message

`Property '{0}' has GeoHashPrecision specified but SpatialIndexType is not GeoHash; set SpatialIndexType = SpatialIndexType.GeoHash or remove the GeoHashPrecision property`

## Description

GeoHashPrecision can only be used when SpatialIndexType is set to GeoHash (or not specified, as GeoHash is the default). Either set SpatialIndexType = SpatialIndexType.GeoHash or remove the GeoHashPrecision property.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Locations")]
public partial class Location
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [StoreCoordinates(SpatialIndexType = SpatialIndexType.S2, GeoHashPrecision = 7)]
    [DynamoDbAttribute("coordinates")]
    public GeoLocation Coordinates { get; set; }
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Locations")]
public partial class Location
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [StoreCoordinates(SpatialIndexType = SpatialIndexType.GeoHash, GeoHashPrecision = 7)]
    [DynamoDbAttribute("coordinates")]
    public GeoLocation Coordinates { get; set; }
}
```
