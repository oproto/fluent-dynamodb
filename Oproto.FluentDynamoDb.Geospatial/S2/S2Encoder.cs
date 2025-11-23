// This implementation is based on algorithms from Google's S2 Geometry Library (Apache 2.0).
// See THIRD-PARTY-NOTICES.md for full attribution and license information.
// Original: https://github.com/google/s2geometry
// C# Reference: https://github.com/alas/s2-geometry-library-csharp

namespace Oproto.FluentDynamoDb.Geospatial.S2;

/// <summary>
/// Internal class for encoding and decoding S2 cell tokens.
/// Implements the S2 geometry algorithm for spherical spatial indexing.
/// </summary>
internal static class S2Encoder
{
    private const int MaxLevel = 30;
    private const ulong FaceBits = 3;
    private const int MaxSize = 1 << MaxLevel;  // 2^30 = 1073741824

    /// <summary>
    /// Encodes a geographic location into an S2 cell token.
    /// </summary>
    /// <param name="latitude">The latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">The longitude in degrees (-180 to 180).</param>
    /// <param name="level">The S2 cell level (0-30). Higher levels provide more precision.</param>
    /// <returns>An S2 cell token as a hexadecimal string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when level is outside the range 0-30.
    /// </exception>
    public static string Encode(double latitude, double longitude, int level)
    {
        if (level < 0 || level > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                $"S2 level must be between 0 and {MaxLevel}. " +
                "Common values: 10 (city ~100km), 16 (neighborhood ~600m), 20 (building ~75m)");
        }

        // Convert lat/lon to 3D point on unit sphere
        var (x, y, z) = LatLonToXYZ(latitude, longitude);
        
        // Determine which cube face the point is on
        var face = XYZToFace(x, y, z);
        
        // Project to UV coordinates on the cube face
        var (u, v) = XYZToFaceUV(face, x, y, z);
        
        // Transform UV to ST coordinates (0 to 1 range)
        var s = UVToST(u);
        var t = UVToST(v);
        
        // Convert ST to integer IJ coordinates
        var (i, j) = STToIJ(s, t, level);
        
        // Encode as Hilbert curve position
        var cellId = FaceIJToCellId(face, i, j, level);
        
        // Convert cell ID to token string
        return CellIdToToken(cellId);
    }

    /// <summary>
    /// Decodes an S2 cell token to its center point coordinates.
    /// </summary>
    /// <param name="s2Token">The S2 cell token to decode.</param>
    /// <returns>A tuple containing the latitude and longitude of the center point.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the S2 token is null, empty, or invalid.
    /// </exception>
    public static (double Latitude, double Longitude) Decode(string s2Token)
    {
        if (string.IsNullOrEmpty(s2Token))
        {
            throw new ArgumentException("S2 token cannot be null or empty", nameof(s2Token));
        }

        var cellId = TokenToCellId(s2Token);
        var (face, i, j, level) = CellIdToFaceIJ(cellId);
        
        // Convert IJ to ST coordinates with non-leaf cell center correction
        var (s, t) = IJToSTWithCorrection(i, j, level, cellId);
        
        // Convert ST to UV coordinates
        var u = STToUV(s);
        var v = STToUV(t);
        
        // Convert UV to XYZ on unit sphere
        var (x, y, z) = FaceUVToXYZ(face, u, v);
        
        // Convert XYZ to lat/lon
        return XYZToLatLon(x, y, z);
    }

    /// <summary>
    /// Decodes an S2 cell token to its bounding box coordinates.
    /// </summary>
    /// <param name="s2Token">The S2 cell token to decode.</param>
    /// <returns>A tuple containing the minimum and maximum latitude and longitude values.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the S2 token is null, empty, or invalid.
    /// </exception>
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) DecodeBounds(string s2Token)
    {
        if (string.IsNullOrEmpty(s2Token))
        {
            throw new ArgumentException("S2 token cannot be null or empty", nameof(s2Token));
        }

        var cellId = TokenToCellId(s2Token);
        var (face, i, j, level) = CellIdToFaceIJ(cellId);
        
        // Based on Google's S2 Geometry library (Apache 2.0 license)
        // Reference: s2-geometry-library-csharp/S2Geometry/S2Cell.cs RectBound property
        //
        // For level 0 cells (entire cube faces), the reference implementation returns
        // hardcoded bounds because the geometry is well-known and fixed.
        // For level > 0, we compute bounds from the cell corners.
        
        if (level == 0)
        {
            // Level 0: Return hardcoded bounds for each face
            // Reference: S2Cell.RectBound switch statement for level 0
            // PoleMinLat = arcsin(sqrt(1/3)) ≈ 35.26°
            const double PoleMinLat = 35.264389682754654;  // Math.Asin(Math.Sqrt(1.0/3.0))
            const double PiOver4Deg = 45.0;  // π/4 in degrees
            
            return face switch
            {
                0 => (-PiOver4Deg, PiOver4Deg, -PiOver4Deg, PiOver4Deg),        // +X face
                1 => (-PiOver4Deg, PiOver4Deg, PiOver4Deg, 3 * PiOver4Deg),     // +Y face
                2 => (PoleMinLat, 90.0, -180.0, 180.0),                          // +Z face (North Pole)
                3 => (-PiOver4Deg, PiOver4Deg, 3 * PiOver4Deg, -3 * PiOver4Deg), // -X face (wraps)
                4 => (-PiOver4Deg, PiOver4Deg, -3 * PiOver4Deg, -PiOver4Deg),   // -Y face
                5 => (-90.0, -PoleMinLat, -180.0, 180.0),                        // -Z face (South Pole)
                _ => throw new ArgumentException($"Invalid face: {face}")
            };
        }
        
        // For level > 0, compute bounds from cell corners
        var cellSize = 1 << (MaxLevel - level);  // Size of cell in leaf cell units
        
        // Compute the cell bounds in scaled (i,j) coordinates.
        // Use bitwise AND with -cellSize to round DOWN to the cell boundary.
        // This is equivalent to: (ij[d] / cellSize) * cellSize
        // Reference: var sijLo = (ij[d] & -cellSize) * 2 - MaxCellSize;
        var sijLo_i = (i & -cellSize) * 2 - MaxSize;
        var sijHi_i = sijLo_i + cellSize * 2;
        var sijLo_j = (j & -cellSize) * 2 - MaxSize;
        var sijHi_j = sijLo_j + cellSize * 2;
        
        // Convert scaled IJ coordinates to ST coordinates in the range [-1, 1]
        // Reference: _uv[d][0] = S2Projections.StToUv((1.0/MaxCellSize)*sijLo);
        const double kScale = 1.0 / MaxSize;
        var sLo = kScale * sijLo_i;
        var sHi = kScale * sijHi_i;
        var tLo = kScale * sijLo_j;
        var tHi = kScale * sijHi_j;
        
        // Convert ST to UV coordinates
        var uLo = STToUV(sLo);
        var uHi = STToUV(sHi);
        var vLo = STToUV(tLo);
        var vHi = STToUV(tHi);
        
        // Get the four corners of the cell in UV space
        // Vertices are returned in the order SW, SE, NE, NW
        // Reference: S2Cell.GetVertexRaw(k) returns FaceUvToXyz(_face, _uv[0][(k >> 1) ^ (k & 1)], _uv[1][k >> 1])
        var corners = new (double lat, double lon)[4];
        
        // k=0 (SW): u=uLo, v=vLo  (k>>1=0, (k>>1)^(k&1)=0, k>>1=0)
        var (x0, y0, z0) = FaceUVToXYZ(face, uLo, vLo);
        corners[0] = XYZToLatLon(x0, y0, z0);
        
        // k=1 (SE): u=uHi, v=vLo  (k>>1=0, (k>>1)^(k&1)=1, k>>1=0)
        var (x1, y1, z1) = FaceUVToXYZ(face, uHi, vLo);
        corners[1] = XYZToLatLon(x1, y1, z1);
        
        // k=2 (NE): u=uHi, v=vHi  (k>>1=1, (k>>1)^(k&1)=1, k>>1=1)
        var (x2, y2, z2) = FaceUVToXYZ(face, uHi, vHi);
        corners[2] = XYZToLatLon(x2, y2, z2);
        
        // k=3 (NW): u=uLo, v=vHi  (k>>1=1, (k>>1)^(k&1)=0, k>>1=1)
        var (x3, y3, z3) = FaceUVToXYZ(face, uLo, vHi);
        corners[3] = XYZToLatLon(x3, y3, z3);
        
        // Compute latitude bounds (straightforward)
        var minLat = corners.Min(c => c.lat);
        var maxLat = corners.Max(c => c.lat);
        
        // Check if the cell contains a pole
        // A cell contains the North Pole if it's on face 2 (+Z) and maxLat is close to 90°
        // A cell contains the South Pole if it's on face 5 (-Z) and minLat is close to -90°
        bool containsNorthPole = (face == 2 && maxLat > 89.9);
        bool containsSouthPole = (face == 5 && minLat < -89.9);
        
        double minLon, maxLon;
        if (containsNorthPole || containsSouthPole)
        {
            // Cells containing a pole span the full longitude range
            minLon = -180.0;
            maxLon = 180.0;
        }
        else
        {
            // Compute longitude bounds (must handle wrapping around the date line)
            // The reference implementation uses S1Interval.FromPointPair which handles wrapping.
            // A cell spans the date line if the longitude range is > 180°.
            //
            // IMPORTANT: GeoBoundingBox doesn't support wrapping (minLon > maxLon).
            // For cells that wrap around the date line, we return the full longitude range [-180, 180]
            // as an approximation. This is acceptable because:
            // 1. Such cells are rare (only near the date line)
            // 2. The bounds are used for approximate filtering, not exact containment
            // 3. The alternative would be to change GeoBoundingBox to support wrapping
            var lons = corners.Select(c => c.lon).ToArray();
            
            // Check for wrapping by looking at adjacent corners
            // If any two adjacent corners have a longitude difference > 180°, the cell wraps
            bool wraps = false;
            for (int cornerIdx = 0; cornerIdx < 4; cornerIdx++)
            {
                var nextIdx = (cornerIdx + 1) % 4;
                if (Math.Abs(lons[cornerIdx] - lons[nextIdx]) > 180.0)
                {
                    wraps = true;
                    break;
                }
            }
            
            if (wraps)
            {
                // Cell wraps around the date line
                // Return the full longitude range as an approximation
                minLon = -180.0;
                maxLon = 180.0;
            }
            else
            {
                // Normal case: cell doesn't wrap
                minLon = lons.Min();
                maxLon = lons.Max();
            }
        }
        
        return (minLat, maxLat, minLon, maxLon);
    }

    /// <summary>
    /// Gets the 8 neighboring S2 cells for a given S2 token.
    /// </summary>
    /// <param name="s2Token">The S2 cell token.</param>
    /// <returns>An array of 8 S2 cell tokens representing the neighboring cells.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the S2 token is null, empty, or invalid.
    /// </exception>
    public static string[] GetNeighbors(string s2Token)
    {
        if (string.IsNullOrEmpty(s2Token))
        {
            throw new ArgumentException("S2 token cannot be null or empty", nameof(s2Token));
        }

        var cellId = TokenToCellId(s2Token);
        var (face, i, j, level) = CellIdToFaceIJ(cellId);
        
        // Based on Google's S2 Geometry library (Apache 2.0 license)
        // Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs GetEdgeNeighbors and GetAllNeighbors
        //
        // CRITICAL FIX: The IJ coordinates returned by CellIdToFaceIJ are at LEAF cell resolution (MaxSize).
        // To get neighbors at the SAME level, we need to:
        // 1. Calculate the cell size at the current level
        // 2. Offset by the cell size in i and j directions
        // 3. Encode the neighbors at the SAME level (not leaf level)
        
        var size = 1 << (MaxLevel - level);  // Size of cell in leaf cell units
        
        var neighbors = new List<string>();
        
        // The 8 neighbors are: SW, S, SE, W, E, NW, N, NE
        // We compute them by offsetting by ±size in i and j directions
        var offsets = new[] 
        { 
            (-size, -size), (-size, 0), (-size, size),  // SW, S, SE
            (0, -size),                 (0, size),      // W, E
            (size, -size),  (size, 0),  (size, size)    // NW, N, NE
        };
        
        foreach (var (di, dj) in offsets)
        {
            var ni = i + di;
            var nj = j + dj;
            
            // Check if the neighbor is on the same face
            var sameFace = ni >= 0 && ni < MaxSize && nj >= 0 && nj < MaxSize;
            
            // Get the neighbor cell ID at the SAME level, handling face boundary transitions
            var neighborCellId = FromFaceIJSame(face, ni, nj, sameFace, level);
            neighbors.Add(CellIdToToken(neighborCellId));
        }
        
        return neighbors.ToArray();
    }

    // ===== Coordinate Conversion Methods =====

    internal static (double x, double y, double z) LatLonToXYZ(double latitude, double longitude)
    {
        var latRad = DegreesToRadians(latitude);
        var lonRad = DegreesToRadians(longitude);
        
        var cosLat = Math.Cos(latRad);
        var x = Math.Cos(lonRad) * cosLat;
        var y = Math.Sin(lonRad) * cosLat;
        var z = Math.Sin(latRad);
        
        return (x, y, z);
    }

    internal static (double latitude, double longitude) XYZToLatLon(double x, double y, double z)
    {
        // Use atan2 for latitude calculation as it's more accurate near poles
        // Reference: S2LatLng constructor in s2-geometry-library-csharp
        // Note: atan2(0, 0) is defined to be zero, which handles the pole case naturally
        var latitude = RadiansToDegrees(Math.Atan2(z, Math.Sqrt(x * x + y * y)));
        var longitude = RadiansToDegrees(Math.Atan2(y, x));
        
        return (latitude, longitude);
    }

    internal static int XYZToFace(double x, double y, double z)
    {
        var absX = Math.Abs(x);
        var absY = Math.Abs(y);
        var absZ = Math.Abs(z);
        
        if (absX > absY && absX > absZ)
            return x > 0 ? 0 : 3;  // +X or -X face
        if (absY > absZ)
            return y > 0 ? 1 : 4;  // +Y or -Y face
        return z > 0 ? 2 : 5;      // +Z or -Z face
    }

    internal static (double u, double v) XYZToFaceUV(int face, double x, double y, double z)
    {
        // Project point onto the appropriate face
        // UV coordinates are in the range [-1, 1]
        // Based on S2 reference implementation: s2-geometry-library-csharp/S2Geometry/S2Projections.cs ValidFaceXyzToUv
        return face switch
        {
            0 => (y / x, z / x),      // +X face (x > 0): u=y/x, v=z/x
            1 => (-x / y, z / y),     // +Y face (y > 0): u=-x/y, v=z/y
            2 => (-x / z, -y / z),    // +Z face (z > 0): u=-x/z, v=-y/z
            3 => (z / x, y / x),      // -X face (x < 0): u=z/x, v=y/x (note: x is negative)
            4 => (z / y, -x / y),     // -Y face (y < 0): u=z/y, v=-x/y (note: y is negative)
            5 => (-y / z, -x / z),    // -Z face (z < 0): u=-y/z, v=-x/z (note: z is negative)
            _ => throw new ArgumentException($"Invalid face: {face}")
        };
    }

    internal static (double x, double y, double z) FaceUVToXYZ(int face, double u, double v)
    {
        // Convert UV coordinates back to XYZ on unit sphere
        // This is the inverse of XYZToFaceUV
        // Each face has its own coordinate system
        // Based on S2 reference implementation: s2-geometry-library-csharp/S2Geometry/S2Projections.cs
        return face switch
        {
            0 => Normalize(1, u, v),      // +X face: x=1, y=u, z=v
            1 => Normalize(-u, 1, v),     // +Y face: x=-u, y=1, z=v
            2 => Normalize(-u, -v, 1),    // +Z face: x=-u, y=-v, z=1
            3 => Normalize(-1, -v, -u),   // -X face: x=-1, y=-v, z=-u
            4 => Normalize(v, -1, -u),    // -Y face: x=v, y=-1, z=-u
            5 => Normalize(v, u, -1),     // -Z face: x=v, y=u, z=-1
            _ => throw new ArgumentException($"Invalid face: {face}")
        };
    }

    private static (double x, double y, double z) Normalize(double x, double y, double z)
    {
        var length = Math.Sqrt(x * x + y * y + z * z);
        return (x / length, y / length, z / length);
    }

    // ===== UV to ST Transformation =====
    // S2 uses a non-linear transformation to improve area uniformity

    internal static double UVToST(double u)
    {
        // Quadratic projection (matches Google S2 reference implementation)
        // Reference: s2-geometry-library-csharp/S2Geometry/S2Projections.cs UvToSt method
        if (u >= 0)
        {
            return Math.Sqrt(1 + 3 * u) - 1;
        }
        else
        {
            return 1 - Math.Sqrt(1 - 3 * u);
        }
    }

    internal static double STToUV(double s)
    {
        // Quadratic projection (matches Google S2 reference implementation)
        // Reference: s2-geometry-library-csharp/S2Geometry/S2Projections.cs StToUv method
        if (s >= 0)
        {
            return (1.0 / 3.0) * ((1 + s) * (1 + s) - 1);
        }
        else
        {
            return (1.0 / 3.0) * (1 - (1 - s) * (1 - s));
        }
    }

    // ===== ST to IJ Conversion =====

    private static (int i, int j) STToIJ(double s, double t, int level)
    {
        // Based on Google's S2 Geometry library (Apache 2.0 license)
        // Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs StToIj method
        //
        // IMPORTANT: ST to IJ conversion always uses MaxSize (2^30), not level-specific scaling.
        // The IJ coordinates are always in the range [0, 2*m-1] where m = MaxSize/2.
        // The level information is encoded in the cell ID by FaceIJToCellId, not in the IJ coordinates.
        
        // Use the exact formula from the C# reference implementation
        // This provides better accuracy than simple floor(MaxSize * s)
        const int m = MaxSize / 2;  // scaling multiplier = 536870912
        var i = (int)Math.Max(0, Math.Min(2 * m - 1, Math.Round(m * s + (m - 0.5))));
        var j = (int)Math.Max(0, Math.Min(2 * m - 1, Math.Round(m * t + (m - 0.5))));
        
        return (i, j);
    }


    /// <summary>
    /// Converts IJ coordinates to ST coordinates with non-leaf cell center correction.
    /// This method is used during decoding to compute the exact center of a cell.
    /// </summary>
    /// <param name="i">The I coordinate</param>
    /// <param name="j">The J coordinate</param>
    /// <param name="level">The cell level</param>
    /// <param name="cellId">The cell ID (used for correction calculation)</param>
    /// <returns>A tuple containing the S and T coordinates in the range [-1, 1]</returns>
    internal static (double s, double t) IJToSTWithCorrection(int i, int j, int level, ulong cellId)
    {
        // Based on Google's S2 Geometry library (Apache 2.0 license)
        // Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs ToPointRaw method
        // Python reference: https://docs.s2cell.aliddell.com/en/stable/annotated_source.html
        
        // For non-leaf cells, we need to apply a correction to get the exact center.
        // The cell returned by CellIdToFaceIJ is always one of two leaf cells closest
        // to the center of the cell (unless the given cell is a leaf cell itself).
        //
        // Given a cell of size s >= 2 (i.e. not a leaf cell), and letting (imin, jmin)
        // be the coordinates of its lower left-hand corner, the leaf cell returned by
        // CellIdToFaceIJ() is either (imin + s/2, jmin + s/2) or (imin + s/2 - 1, jmin + s/2 - 1).
        //
        // We can distinguish these two cases by looking at the low bit of "i" or "j".
        // In the first case the low bit is zero, unless s == 2 (i.e. level 29, the level
        // just above leaf cells) in which case the low bit is one.
        
        var isLeaf = level == MaxLevel;
        
        // Calculate the correction delta:
        // - For leaf cells: delta = 1 (center of the cell)
        // - For non-leaf cells: delta = 2 if the correction bit is set, else 0
        //
        // The correction bit is determined by: (i ^ (cellId >> 2)) & 1
        // This checks if we need to adjust to the actual center of the non-leaf cell
        var delta = isLeaf ? 1 : (((i ^ ((int)(cellId >> 2))) & 1) != 0) ? 2 : 0;
        
        // Convert to Si/Ti coordinates (scaled by 2 to allow exact representation of centers)
        // Si and Ti are in the range [-MaxSize, MaxSize]
        var si = (i << 1) + delta - MaxSize;
        var ti = (j << 1) + delta - MaxSize;
        
        // CRITICAL FIX: Convert Si/Ti to ST coordinates in the range [-1, 1]
        // The formula is: ST = (1.0 / MaxSize) * Si
        // This maps [-MaxSize, MaxSize] to [-1, 1]
        // Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs line 1052-1053 (FaceSiTiToXyz)
        const double kScale = 1.0 / MaxSize;
        var s = kScale * si;
        var t = kScale * ti;
        
        return (s, t);
    }

    // ===== Hilbert Curve Encoding =====
    // S2 uses a Hilbert curve with orientation tracking for proper space-filling properties
    // Based on Google's S2 Geometry library (Apache 2.0 license)
    
    // Lookup tables for Hilbert curve transformations
    // These tables define how the Hilbert curve orientation changes at each level
    
    private const int SwapMask = 0x01;
    private const int InvertMask = 0x02;
    private const int LookupBits = 4;  // Process 4 levels (8 bits of I/J) at a time
    
    // Mapping Hilbert traversal order to orientation adjustment mask
    private static readonly int[] PosToOrientationMask = { SwapMask, 0, 0, InvertMask + SwapMask };
    
    // Position to IJ lookup for each orientation (0-3)
    // Maps Hilbert curve position to (i,j) coordinates for each orientation
    private static readonly int[,] PosToIJ = new int[4, 4]
    {
        { 0, 1, 3, 2 },  // Orientation 0: canonical order
        { 0, 2, 3, 1 },  // Orientation 1: axes swapped
        { 3, 2, 0, 1 },  // Orientation 2: bits inverted
        { 3, 1, 0, 2 }   // Orientation 3: swapped & inverted
    };
    
    // IJ to position lookup for each orientation
    // Inverse of PosToIJ - maps (i,j) coordinates to Hilbert curve position
    private static readonly int[,] IJToPos = new int[4, 4]
    {
        { 0, 1, 3, 2 },  // Orientation 0: canonical order
        { 0, 3, 1, 2 },  // Orientation 1: axes swapped
        { 2, 3, 1, 0 },  // Orientation 2: bits inverted
        { 2, 1, 3, 0 }   // Orientation 3: swapped & inverted
    };
    
    // 8-bit lookup tables for efficient Hilbert curve encoding/decoding
    // These tables process 4 levels (8 bits of I/J) at a time instead of 2 bits
    // LookupPos: maps (i,j,orientation) to (position,new_orientation)
    // LookupIJ: maps (position,orientation) to (i,j,new_orientation)
    // Each table has 1024 entries: 256 (8 bits) * 4 (orientations)
    private static readonly int[] LookupPos = new int[1 << (2 * LookupBits + 2)];
    private static readonly int[] LookupIJ = new int[1 << (2 * LookupBits + 2)];
    
    // Static constructor to initialize lookup tables
    static S2Encoder()
    {
        InitLookupCell(0, 0, 0, 0, 0, 0);
        InitLookupCell(0, 0, 0, SwapMask, 0, SwapMask);
        InitLookupCell(0, 0, 0, InvertMask, 0, InvertMask);
        InitLookupCell(0, 0, 0, SwapMask | InvertMask, 0, SwapMask | InvertMask);
    }
    
    /// <summary>
    /// Recursively builds the 8-bit lookup tables for Hilbert curve encoding/decoding.
    /// This method processes 4 levels (8 bits of I/J) at a time for efficient conversion.
    /// </summary>
    /// <param name="level">Current recursion level (0 to LookupBits)</param>
    /// <param name="i">Current I coordinate</param>
    /// <param name="j">Current J coordinate</param>
    /// <param name="origOrientation">Original orientation at the start of recursion</param>
    /// <param name="pos">Current position along the Hilbert curve</param>
    /// <param name="orientation">Current orientation</param>
    private static void InitLookupCell(int level, int i, int j, int origOrientation, int pos, int orientation)
    {
        if (level == LookupBits)
        {
            // Base case: we've processed LookupBits levels (8 bits of I/J)
            // Store the mapping in both lookup tables
            var ij = (i << LookupBits) + j;
            LookupPos[(ij << 2) + origOrientation] = (pos << 2) + orientation;
            LookupIJ[(pos << 2) + origOrientation] = (ij << 2) + orientation;
        }
        else
        {
            // Recursive case: process the next level
            level++;
            i <<= 1;
            j <<= 1;
            pos <<= 2;
            
            // Initialize each of the 4 sub-cells recursively
            for (var subPos = 0; subPos < 4; subPos++)
            {
                // Get the IJ index for this sub-position given the current orientation
                var ijIndex = PosToIJ[orientation, subPos];
                
                // Extract i and j bits from the IJ index
                var iBit = (ijIndex >> 1) & 1;
                var jBit = ijIndex & 1;
                
                // Get the orientation mask for this sub-position
                var orientationMask = PosToOrientationMask[subPos];
                
                // Recurse with updated coordinates and orientation
                InitLookupCell(level, i + iBit, j + jBit, origOrientation, pos + subPos, orientation ^ orientationMask);
            }
        }
    }

    public static ulong FaceIJToCellId(int face, int i, int j, int level)
    {
        // Based on Google's S2 Geometry library (Apache 2.0 license)
        // This implementation uses 8-bit lookup tables to process 4 levels (8 bits of I/J) at a time
        // Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs FromFaceIj method
        
        // Start with face bits in the high-order position
        // The cell ID format is: [3 face bits][61 position bits]
        // We shift face left by (PosBits - 1) = (2 * MaxLevel + 1 - 1) = 2 * MaxLevel
        var n = (ulong)face << (2 * MaxLevel);
        
        // Alternating faces have opposite Hilbert curve orientations
        // This is necessary for all faces to have a right-handed coordinate system
        var bits = face & SwapMask;
        
        unchecked
        {
            // Process I and J in 4-bit chunks (8 iterations for 30 levels + 1 partial)
            // Each iteration processes LookupBits (4) levels, which is 8 bits of I/J combined
            for (var k = 7; k >= 0; k--)
            {
                // Extract 4 bits from I and J at position k
                const int mask = (1 << LookupBits) - 1;  // 0x0F (15)
                
                // Build lookup key: [4 bits of I][4 bits of J][2 bits of orientation]
                // Total: 10 bits (values 0-1023)
                bits += ((i >> (k * LookupBits)) & mask) << (LookupBits + 2);  // I bits at positions 9-6
                bits += ((j >> (k * LookupBits)) & mask) << 2;                  // J bits at positions 5-2
                // Orientation bits already in positions 1-0
                
                // Look up the Hilbert curve position and new orientation
                // LookupPos returns: [8 bits of position][2 bits of new orientation]
                bits = LookupPos[bits];
                
                // Extract the 8 position bits and place them in the cell ID
                // Position k=7 handles bits 60-53, k=6 handles 52-45, ..., k=0 handles 12-5
                n |= (ulong)(bits >> 2) << (k * 2 * LookupBits);
                
                // Keep only the orientation bits for the next iteration
                bits &= (SwapMask | InvertMask);
            }
            
            // The result n now contains the face and Hilbert curve position for a leaf cell
            // Multiply by 2 and add 1 to create a leaf cell ID
            // Then convert to the appropriate level by calling ParentForLevel
            var leafCellId = n * 2 + 1;
            
            // If we want a non-leaf cell, we need to adjust to the correct level
            if (level < MaxLevel)
            {
                // Find the appropriate level marker position
                var newLsb = LowestOnBitForLevel(level);
                // Clear bits below the level marker and set the level marker
                leafCellId = (ulong)((long)leafCellId & -(long)newLsb) | newLsb;
            }
            
            return leafCellId;
        }
    }
    
    private static ulong LowestOnBitForLevel(int level)
    {
        return 1UL << (2 * (MaxLevel - level));
    }

    public static (int face, int i, int j, int level) CellIdToFaceIJ(ulong cellId)
    {
        // Based on Google's S2 Geometry library (Apache 2.0 license)
        // Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs ToFaceIjOrientation method
        
        // Extract face (top 3 bits)
        // PosBits = 2 * MaxLevel + 1 = 61, so face is in bits 63-61
        var face = (int)(cellId >> (2 * MaxLevel + 1));
        
        // Find level by locating the sentinel bit (lowest set bit)
        var level = MaxLevel;
        var lowestOnBit = (ulong)((long)cellId & -(long)cellId);
        
        // The level is determined by the position of the lowest set bit
        // For a cell at level k, the lowest set bit is at position 2 * (MaxLevel - k)
        for (var lv = 0; lv <= MaxLevel; lv++)
        {
            if (lowestOnBit == LowestOnBitForLevel(lv))
            {
                level = lv;
                break;
            }
        }
        
        // Alternating faces have opposite Hilbert curve orientations
        var bits = face & SwapMask;
        
        var i = 0;
        var j = 0;
        
        // Process 8 bits of Hilbert curve position at a time using lookup tables
        // Each iteration decodes 8 bits of position into 4 bits of I and 4 bits of J
        // The loop processes from k=7 down to k=0, handling 8 iterations total
        // This matches the reference implementation's GetBits1 method
        for (var k = 7; k >= 0; k--)
        {
            // Determine how many bits to process in this iteration
            // The first iteration (k=7) processes the remaining bits after 7 full iterations
            // For MaxLevel=30: 30 - 7*4 = 2 bits on first iteration, then 4 bits for k=6..0
            var nbits = (k == 7) ? (MaxLevel - 7 * LookupBits) : LookupBits;
            
            // Extract the position bits for this iteration
            // Shift right by (k * 2 * LookupBits + 1) to skip the sentinel bit
            // The +1 accounts for the sentinel bit at the end of the cell ID
            var positionBits = (int)((cellId >> (k * 2 * LookupBits + 1)) & ((1UL << (2 * nbits)) - 1));
            
            // Build lookup key: [position bits][orientation bits]
            // Position bits are shifted left by 2 to make room for orientation bits
            bits += positionBits << 2;
            
            // Look up the IJ coordinates and new orientation
            // LookupIJ returns: [4 bits of I][4 bits of J][2 bits of new orientation]
            bits = LookupIJ[bits];
            
            // Extract I and J bits and add them to the result
            // I bits are in the high 4 bits (positions 9-6)
            i = i + ((bits >> (LookupBits + 2)) << (k * LookupBits));
            // J bits are in positions 5-2
            j = j + ((((bits >> 2) & ((1 << LookupBits) - 1))) << (k * LookupBits));
            
            // Keep only the orientation bits (positions 1-0) for the next iteration
            bits &= (SwapMask | InvertMask);
        }
        
        return (face, i, j, level);
    }



    // ===== Cell ID to Token Conversion =====

    public static string CellIdToToken(ulong cellId)
    {
        // Convert to hex and remove trailing zeros (standard S2 token format)
        string hex = cellId.ToString("x16");
        return hex.TrimEnd('0');
    }

    public static ulong TokenToCellId(string s2Token)
    {
        if (s2Token.Length == 0 || s2Token.Length > 16)
        {
            throw new ArgumentException(
                $"Invalid S2 token '{s2Token}'. S2 tokens must be 1-16 character hexadecimal strings.",
                nameof(s2Token));
        }

        try
        {
            // Pad with zeros to 16 characters before parsing
            string paddedToken = s2Token.PadRight(16, '0');
            return Convert.ToUInt64(paddedToken, 16);
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                $"Invalid S2 token '{s2Token}'. S2 tokens must contain only hexadecimal characters (0-9, a-f).",
                nameof(s2Token));
        }
    }

    // ===== Face Boundary Wrapping =====

    /// <summary>
    /// Helper function that calls FromFaceIJ if sameFace is true, or FromFaceIJWrap if sameFace is false.
    /// Based on Google's S2 Geometry library (Apache 2.0 license).
    /// Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs FromFaceIjSame method
    /// </summary>
    private static ulong FromFaceIJSame(int face, int i, int j, bool sameFace, int level)
    {
        if (sameFace)
        {
            return FaceIJToCellId(face, i, j, level);
        }
        else
        {
            return FromFaceIJWrap(face, i, j, level);
        }
    }

    /// <summary>
    /// Converts IJ coordinates that may be outside the face boundary to a valid cell ID
    /// by wrapping to the adjacent face.
    /// Based on Google's S2 Geometry library (Apache 2.0 license).
    /// Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs FromFaceIjWrap method
    /// </summary>
    private static ulong FromFaceIJWrap(int face, int i, int j, int level)
    {
        // Convert i and j to the coordinates of a leaf cell just beyond the
        // boundary of this face. This prevents overflow and means we don't need
        // to worry about the distinction between (s,t) and (u,v).
        i = Math.Max(-1, Math.Min(MaxSize, i));
        j = Math.Max(-1, Math.Min(MaxSize, j));

        // Find the (s,t) coordinates corresponding to (i,j). At least one
        // of these coordinates will be just outside the range [-1, 1].
        const double kScale = 1.0 / MaxSize;
        var s = kScale * ((i << 1) + 1 - MaxSize);
        var t = kScale * ((j << 1) + 1 - MaxSize);

        // Convert ST to UV coordinates
        var u = STToUV(s);
        var v = STToUV(t);

        // Find the leaf cell coordinates on the adjacent face
        var (x, y, z) = FaceUVToXYZ(face, u, v);
        var newFace = XYZToFace(x, y, z);
        var (newU, newV) = XYZToFaceUV(newFace, x, y, z);
        
        // Convert back to ST coordinates on the new face
        var newS = UVToST(newU);
        var newT = UVToST(newV);
        
        // Convert ST to IJ coordinates
        var (newI, newJ) = STToIJ(newS, newT, level);
        
        return FaceIJToCellId(newFace, newI, newJ, level);
    }

    // ===== Utility Methods =====

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static double RadiansToDegrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }
}
