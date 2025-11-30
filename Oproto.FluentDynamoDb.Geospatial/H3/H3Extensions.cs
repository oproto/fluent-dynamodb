// -----------------------------------------------------------------------
// H3 Attribution Notice
// -----------------------------------------------------------------------
// This file contains code derived from Uber's H3 library.
// Original project: https://github.com/uber/h3
// Copyright 2018 Uber Technologies, Inc.
// Licensed under the Apache License, Version 2.0
// See THIRD-PARTY-NOTICES.md for full license text.
// -----------------------------------------------------------------------

namespace Oproto.FluentDynamoDb.Geospatial.H3;

/// <summary>
/// Extension methods for converting between GeoLocation and H3 representations.
/// </summary>
public static class H3Extensions
{
    /// <summary>
    /// Converts a GeoLocation to an H3 cell index.
    /// </summary>
    /// <param name="location">The geographic location to encode.</param>
    /// <param name="resolution">The resolution (precision) for the H3 cell (0-15). Default is 9.</param>
    /// <returns>An H3 cell index as a hexadecimal string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when resolution is outside the range 0-15.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = new GeoLocation(37.7749, -122.4194);
    /// string index = location.ToH3Index(); // Uses default resolution 9
    /// string preciseIndex = location.ToH3Index(12); // Uses resolution 12
    /// </code>
    /// </example>
    public static string ToH3Index(this GeoLocation location, int resolution = 9)
    {
        return H3Encoder.Encode(location.Latitude, location.Longitude, resolution);
    }

    /// <summary>
    /// Creates a GeoLocation from an H3 cell index.
    /// Returns the center point of the H3 cell.
    /// </summary>
    /// <param name="h3Index">The H3 cell index to decode.</param>
    /// <returns>A GeoLocation representing the center point of the H3 cell.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the H3 index is null, empty, or invalid.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = H3Extensions.FromH3Index("8928308280fffff");
    /// Console.WriteLine($"Lat: {location.Latitude}, Lon: {location.Longitude}");
    /// </code>
    /// </example>
    public static GeoLocation FromH3Index(string h3Index)
    {
        var (latitude, longitude) = H3Encoder.Decode(h3Index);
        return new GeoLocation(latitude, longitude);
    }

    /// <summary>
    /// Converts a GeoLocation to an H3Cell.
    /// </summary>
    /// <param name="location">The geographic location to encode.</param>
    /// <param name="resolution">The resolution (precision) for the H3 cell (0-15). Default is 9.</param>
    /// <returns>An H3Cell representing the location with the specified resolution.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when resolution is outside the range 0-15.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = new GeoLocation(37.7749, -122.4194);
    /// var cell = location.ToH3Cell(10);
    /// Console.WriteLine($"Index: {cell.Index}, Resolution: {cell.Resolution}, Bounds: {cell.Bounds}");
    /// </code>
    /// </example>
    public static H3Cell ToH3Cell(this GeoLocation location, int resolution = 9)
    {
        return new H3Cell(location, resolution);
    }
}
