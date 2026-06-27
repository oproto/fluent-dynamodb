# DYNDB111: Spatial index configuration on non-GeoLocation property

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB111` |
| Severity | Error |

## Message

`Property '{0}' has spatial index configuration but is not of type GeoLocation, and spatial index properties can only be used on GeoLocation properties`

## Description

Spatial index configuration properties can only be used on properties of type GeoLocation from the Oproto.FluentDynamoDb.Geospatial package. The spatial indexing system requires coordinate data that only GeoLocation provides.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Locations")]
public partial class Location
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [StoreCoordinates(SpatialIndexType = SpatialIndexType.GeoHash)]
    [DynamoDbAttribute("address")]
    public string Address { get; set; } = string.Empty;
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

    [StoreCoordinates(SpatialIndexType = SpatialIndexType.GeoHash)]
    [DynamoDbAttribute("coordinates")]
    public GeoLocation Coordinates { get; set; }

    [DynamoDbAttribute("address")]
    public string Address { get; set; } = string.Empty;
}
```
