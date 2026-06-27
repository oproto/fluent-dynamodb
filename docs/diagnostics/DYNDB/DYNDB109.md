# DYNDB109: H3Resolution specified without H3 index type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB109` |
| Severity | Error |

## Message

`Property '{0}' has H3Resolution specified but SpatialIndexType is not H3; set SpatialIndexType = SpatialIndexType.H3 to use H3 indexing`

## Description

H3Resolution can only be used when SpatialIndexType is set to H3. Either set SpatialIndexType = SpatialIndexType.H3 or remove the H3Resolution property. The H3Resolution parameter configures the precision of Uber H3 hexagonal spatial indexing.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Locations")]
public partial class Location
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [StoreCoordinates(H3Resolution = 9)]
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

    [StoreCoordinates(SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
    [DynamoDbAttribute("coordinates")]
    public GeoLocation Coordinates { get; set; }
}
```
