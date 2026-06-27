# DYNDB108: S2Level specified without S2 index type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB108` |
| Severity | Error |

## Message

`Property '{0}' has S2Level specified but SpatialIndexType is not S2; set SpatialIndexType = SpatialIndexType.S2 to use S2 indexing`

## Description

S2Level can only be used when SpatialIndexType is set to S2. Either set SpatialIndexType = SpatialIndexType.S2 or remove the S2Level property. The S2Level parameter configures the precision of Google S2 cell-based spatial indexing.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Locations")]
public partial class Location
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [StoreCoordinates(S2Level = 12)]
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

    [StoreCoordinates(SpatialIndexType = SpatialIndexType.S2, S2Level = 12)]
    [DynamoDbAttribute("coordinates")]
    public GeoLocation Coordinates { get; set; }
}
```
