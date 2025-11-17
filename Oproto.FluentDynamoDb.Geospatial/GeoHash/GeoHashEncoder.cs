namespace Oproto.FluentDynamoDb.Geospatial.GeoHash;

/// <summary>
/// Internal class for encoding and decoding GeoHash strings.
/// Implements the standard GeoHash algorithm with base32 encoding.
/// </summary>
internal static class GeoHashEncoder
{
    /// <summary>
    /// Base32 character set used for GeoHash encoding.
    /// Uses lowercase letters and digits, excluding 'a', 'i', 'l', and 'o' to avoid confusion.
    /// </summary>
    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// Encodes a geographic location into a GeoHash string.
    /// </summary>
    /// <param name="latitude">The latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">The longitude in degrees (-180 to 180).</param>
    /// <param name="precision">The number of characters in the resulting GeoHash (1-12).</param>
    /// <returns>A GeoHash string of the specified precision.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when precision is outside the range 1-12.
    /// </exception>
    public static string Encode(double latitude, double longitude, int precision)
    {
        if (precision < 1 || precision > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision),
                precision,
                "Precision must be between 1 and 12");
        }

        var geohash = new char[precision];
        var bits = 0;
        var bitsTotal = 0;
        var hashIndex = 0;

        var minLat = -90.0;
        var maxLat = 90.0;
        var minLon = -180.0;
        var maxLon = 180.0;

        while (hashIndex < precision)
        {
            // Alternate between longitude and latitude
            if (bitsTotal % 2 == 0)
            {
                // Longitude
                var mid = (minLon + maxLon) / 2.0;
                if (longitude >= mid)
                {
                    bits |= (1 << (4 - (bitsTotal % 5)));
                    minLon = mid;
                }
                else
                {
                    maxLon = mid;
                }
            }
            else
            {
                // Latitude
                var mid = (minLat + maxLat) / 2.0;
                if (latitude >= mid)
                {
                    bits |= (1 << (4 - (bitsTotal % 5)));
                    minLat = mid;
                }
                else
                {
                    maxLat = mid;
                }
            }

            bitsTotal++;

            // Every 5 bits, convert to a base32 character
            if (bitsTotal % 5 == 0)
            {
                geohash[hashIndex++] = Base32[bits];
                bits = 0;
            }
        }

        return new string(geohash);
    }

    /// <summary>
    /// Decodes a GeoHash string to its center point coordinates.
    /// </summary>
    /// <param name="geohash">The GeoHash string to decode.</param>
    /// <returns>A tuple containing the latitude and longitude of the center point.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the GeoHash string is null, empty, or contains invalid characters.
    /// </exception>
    public static (double Latitude, double Longitude) Decode(string geohash)
    {
        if (string.IsNullOrEmpty(geohash))
        {
            throw new ArgumentException("GeoHash string cannot be null or empty", nameof(geohash));
        }

        var bounds = DecodeBounds(geohash);
        
        // Return the center point
        var latitude = (bounds.MinLat + bounds.MaxLat) / 2.0;
        var longitude = (bounds.MinLon + bounds.MaxLon) / 2.0;
        
        return (latitude, longitude);
    }

    /// <summary>
    /// Decodes a GeoHash string to its bounding box coordinates.
    /// </summary>
    /// <param name="geohash">The GeoHash string to decode.</param>
    /// <returns>A tuple containing the minimum and maximum latitude and longitude values.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the GeoHash string is null, empty, or contains invalid characters.
    /// </exception>
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) DecodeBounds(string geohash)
    {
        if (string.IsNullOrEmpty(geohash))
        {
            throw new ArgumentException("GeoHash string cannot be null or empty", nameof(geohash));
        }

        var minLat = -90.0;
        var maxLat = 90.0;
        var minLon = -180.0;
        var maxLon = 180.0;

        var isEven = true; // Start with longitude

        foreach (var c in geohash)
        {
            var charIndex = Base32.IndexOf(c);
            if (charIndex < 0)
            {
                throw new ArgumentException(
                    $"Invalid character '{c}' in GeoHash string. Valid characters are: {Base32}",
                    nameof(geohash));
            }

            // Process each of the 5 bits in the character
            for (var mask = 16; mask > 0; mask >>= 1)
            {
                if (isEven)
                {
                    // Longitude
                    var mid = (minLon + maxLon) / 2.0;
                    if ((charIndex & mask) != 0)
                    {
                        minLon = mid;
                    }
                    else
                    {
                        maxLon = mid;
                    }
                }
                else
                {
                    // Latitude
                    var mid = (minLat + maxLat) / 2.0;
                    if ((charIndex & mask) != 0)
                    {
                        minLat = mid;
                    }
                    else
                    {
                        maxLat = mid;
                    }
                }

                isEven = !isEven;
            }
        }

        return (minLat, maxLat, minLon, maxLon);
    }

    /// <summary>
    /// Gets the 8 neighboring GeoHash cells for a given GeoHash.
    /// </summary>
    /// <param name="geohash">The GeoHash string.</param>
    /// <returns>An array of 8 GeoHash strings representing the neighboring cells.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the GeoHash string is null, empty, or contains invalid characters.
    /// </exception>
    public static string[] GetNeighbors(string geohash)
    {
        if (string.IsNullOrEmpty(geohash))
        {
            throw new ArgumentException("GeoHash string cannot be null or empty", nameof(geohash));
        }

        // Neighbor and border lookup tables for base32 GeoHash
        var neighbors = new Dictionary<string, string[]>
        {
            ["right_even"] = new[] { "bc01fg45238967deuvhjyznpkmstqrwx" },
            ["left_even"] = new[] { "238967debc01fg45kmstqrwxuvhjyznp" },
            ["top_even"] = new[] { "p0r21436x8zb9dcf5h7kjnmqesgutwvy" },
            ["bottom_even"] = new[] { "14365h7k9dcfesgujnmqp0r2twvyx8zb" },
            ["right_odd"] = new[] { "p0r21436x8zb9dcf5h7kjnmqesgutwvy" },
            ["left_odd"] = new[] { "14365h7k9dcfesgujnmqp0r2twvyx8zb" },
            ["top_odd"] = new[] { "bc01fg45238967deuvhjyznpkmstqrwx" },
            ["bottom_odd"] = new[] { "238967debc01fg45kmstqrwxuvhjyznp" }
        };

        var borders = new Dictionary<string, string[]>
        {
            ["right_even"] = new[] { "bcfguvyz" },
            ["left_even"] = new[] { "0145hjnp" },
            ["top_even"] = new[] { "prxz" },
            ["bottom_even"] = new[] { "028b" },
            ["right_odd"] = new[] { "prxz" },
            ["left_odd"] = new[] { "028b" },
            ["top_odd"] = new[] { "bcfguvyz" },
            ["bottom_odd"] = new[] { "0145hjnp" }
        };

        var directions = new[] { "top", "bottom", "left", "right" };
        var neighborHashes = new List<string>();

        // Calculate the 4 direct neighbors
        var directNeighbors = new Dictionary<string, string>();
        foreach (var direction in directions)
        {
            directNeighbors[direction] = GetNeighbor(geohash, direction, neighbors, borders);
        }

        // Add all 8 neighbors: 4 direct + 4 diagonal
        neighborHashes.Add(directNeighbors["top"]);
        neighborHashes.Add(directNeighbors["bottom"]);
        neighborHashes.Add(directNeighbors["left"]);
        neighborHashes.Add(directNeighbors["right"]);
        neighborHashes.Add(GetNeighbor(directNeighbors["top"], "left", neighbors, borders));      // top-left
        neighborHashes.Add(GetNeighbor(directNeighbors["top"], "right", neighbors, borders));     // top-right
        neighborHashes.Add(GetNeighbor(directNeighbors["bottom"], "left", neighbors, borders));   // bottom-left
        neighborHashes.Add(GetNeighbor(directNeighbors["bottom"], "right", neighbors, borders));  // bottom-right

        return neighborHashes.ToArray();
    }

    private static string GetNeighbor(
        string geohash,
        string direction,
        Dictionary<string, string[]> neighbors,
        Dictionary<string, string[]> borders)
    {
        if (string.IsNullOrEmpty(geohash))
        {
            return geohash;
        }

        var lastChar = geohash[^1];
        var parent = geohash[..^1];
        var type = geohash.Length % 2 == 0 ? "even" : "odd";

        // Check if we're at a border
        if (borders[$"{direction}_{type}"][0].Contains(lastChar))
        {
            parent = GetNeighbor(parent, direction, neighbors, borders);
        }

        // Replace the last character
        var neighborChar = neighbors[$"{direction}_{type}"][0][Base32.IndexOf(lastChar)];
        return parent + neighborChar;
    }
}
