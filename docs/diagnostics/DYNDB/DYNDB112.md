# DYNDB112: Missing Geospatial package

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB112` |
| Severity | Warning |

## Message

`Property '{0}' has spatial index configuration but the Oproto.FluentDynamoDb.Geospatial package is not referenced; add the package reference to enable spatial indexing`

## Description

Spatial index configuration requires the Oproto.FluentDynamoDb.Geospatial package to provide GeoLocation type and spatial encoding functionality. Without this package, the spatial index attributes cannot function correctly.

## Example

The following code triggers this diagnostic:

```csharp
// Project does NOT reference Geospatial package
[DynamoDbTable("Stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [StoreCoordinates(SpatialIndexType = SpatialIndexType.GeoHash)]
    [DynamoDbAttribute("location")]
    public GeoLocation Location { get; set; }
}
```

## Fix

The corrected version:

```xml
<!-- Add to your .csproj file -->
<PackageReference Include="Oproto.FluentDynamoDb.Geospatial" />
```

```csharp
[DynamoDbTable("Stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [StoreCoordinates(SpatialIndexType = SpatialIndexType.GeoHash)]
    [DynamoDbAttribute("location")]
    public GeoLocation Location { get; set; }
}
```
