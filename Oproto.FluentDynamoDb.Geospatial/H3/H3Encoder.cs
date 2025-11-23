using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Oproto.FluentDynamoDb.Geospatial.UnitTests")]

namespace Oproto.FluentDynamoDb.Geospatial.H3;

/// <summary>
/// Internal class for encoding and decoding H3 cell indices.
/// Implements the H3 hexagonal hierarchical spatial indexing algorithm.
/// </summary>
internal static class H3Encoder
{
    private const int MaxResolution = 15;
    private const int NumBaseCells = 122;
    private const int NumIcosaFaces = 20;
    
    // H3 uses aperture 7 hexagons (7 children per parent)
    private const int ChildrenPerCell = 7;
    
    // Pentagon base cells (12 pentagons in H3)
    private static readonly HashSet<int> PentagonBaseCells = new()
    {
        4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117
    };

    /// <summary>
    /// Encodes a geographic location into an H3 cell index.
    /// </summary>
    /// <param name="latitude">The latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">The longitude in degrees (-180 to 180).</param>
    /// <param name="resolution">The H3 resolution (0-15). Higher resolutions provide more precision.</param>
    /// <returns>An H3 cell index as a hexadecimal string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when resolution is outside the range 0-15.
    /// </exception>
    public static string Encode(double latitude, double longitude, int resolution)
    {
        if (resolution < 0 || resolution > MaxResolution)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution,
                $"H3 resolution must be between 0 and {MaxResolution}. " +
                "Common values: 5 (city ~20km), 9 (neighborhood ~174m), 12 (building ~22m)");
        }

        // Validate latitude and longitude
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees");
        }
        
        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees");
        }

        // Perform the core encoding
        return EncodeCore(latitude, longitude, resolution);
    }

    private static string EncodeCore(double latitude, double longitude, int resolution)
    {
        // Use the proper H3 encoding algorithm: go directly from lat/lon to hex2d at target resolution
        var (face, hex2d) = GeoToHex2d(latitude, longitude, resolution);
        
        // Convert hex2d to IJK coordinates
        var ijk = Hex2dToCoordIJK(hex2d);
        var fijk = new FaceIJK(face, ijk);
        
        // Build H3 index by working backwards from target resolution to resolution 0
        var h3Index = BuildH3IndexFromFaceIJK(fijk, resolution);
        
        return H3IndexToString(h3Index);
    }

    /// <summary>
    /// Decodes an H3 cell index to its center point coordinates.
    /// </summary>
    /// <param name="h3Index">The H3 cell index to decode.</param>
    /// <returns>A tuple containing the latitude and longitude of the center point.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the H3 index is null, empty, or invalid.
    /// </exception>
    public static (double Latitude, double Longitude) Decode(string h3Index)
    {
        if (string.IsNullOrEmpty(h3Index))
        {
            throw new ArgumentException("H3 index cannot be null or empty", nameof(h3Index));
        }

        var index = StringToH3Index(h3Index);
        var (baseCell, resolution, face, i, j) = ParseH3IndexWithFace(index);
        
        // Convert IJ to IJK coordinates
        var ijk = new CoordIJK(i, j, 0);
        IJKNormalize(ref ijk);
        
        // Convert IJK to hex2d
        var hex2d = IJKToHex2d(ijk);
        
        // Convert hex2d to geographic coordinates using proper inverse transformation
        return Hex2dToGeo(hex2d, face, resolution);
    }

    /// <summary>
    /// Decodes an H3 cell index to its bounding box coordinates.
    /// </summary>
    /// <param name="h3Index">The H3 cell index to decode.</param>
    /// <returns>A tuple containing the minimum and maximum latitude and longitude values.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the H3 index is null, empty, or invalid.
    /// </exception>
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) DecodeBounds(string h3Index)
    {
        if (string.IsNullOrEmpty(h3Index))
        {
            throw new ArgumentException("H3 index cannot be null or empty", nameof(h3Index));
        }

        var index = StringToH3Index(h3Index);
        var (baseCell, resolution, face, i, j) = ParseH3IndexWithFace(index);
        
        // Get the vertices of the hexagon (or pentagon)
        var isPentagon = IsPentagon(baseCell);
        var numVertices = isPentagon ? 5 : 6;
        
        var vertices = new (double lat, double lon)[numVertices];
        
        // Calculate vertices by offsetting from the cell center in hex2d space
        // H3 hexagons have vertices at 30° offsets (flat-topped orientation)
        // Pentagons have vertices at 72° offsets
        for (var v = 0; v < numVertices; v++)
        {
            // Calculate vertex angle
            // For hexagons: vertices at 30°, 90°, 150°, 210°, 270°, 330° (flat-topped)
            // For pentagons: vertices at 0°, 72°, 144°, 216°, 288°
            double angle;
            if (isPentagon)
            {
                angle = 2.0 * Math.PI * v / 5.0;
            }
            else
            {
                // Flat-topped hexagon: first vertex at 30° (π/6)
                angle = (Math.PI / 6.0) + (2.0 * Math.PI * v / 6.0);
            }
            
            // Get vertex coordinates in hex2d space
            var (vertexFace, vfx, vfy) = GetVertexCoords(face, i, j, resolution, angle, isPentagon);
            
            // Convert face coordinates to geographic coordinates
            var (x, y, z) = FaceCoordsToXYZ(vertexFace, vfx, vfy);
            vertices[v] = XYZToLatLon(x, y, z);
        }
        
        var minLat = vertices.Min(v => v.lat);
        var maxLat = vertices.Max(v => v.lat);
        var minLon = vertices.Min(v => v.lon);
        var maxLon = vertices.Max(v => v.lon);
        
        return (minLat, maxLat, minLon, maxLon);
    }

    /// <summary>
    /// Gets the neighboring H3 cells for a given H3 index.
    /// </summary>
    /// <param name="h3Index">The H3 cell index.</param>
    /// <returns>An array of H3 cell indices representing the neighboring cells (6 for hexagons, 5 for pentagons).</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the H3 index is null, empty, or invalid.
    /// </exception>
    public static string[] GetNeighbors(string h3Index)
    {
        if (string.IsNullOrEmpty(h3Index))
        {
            throw new ArgumentException("H3 index cannot be null or empty", nameof(h3Index));
        }

        var index = StringToH3Index(h3Index);
        var (baseCell, resolution, face, i, j) = ParseH3IndexWithFace(index);
        
        // Convert IJ to IJK coordinates
        var ijk = new CoordIJK(i, j, 0);
        IJKNormalize(ref ijk);
        
        // Create FaceIJK for the current cell
        var fijk = new FaceIJK(face, ijk);
        
        // Check if this is a pentagon
        var isPentagon = IsPentagon(baseCell);
        
        // For hexagons, we have 6 neighbors (directions 1-6)
        // For pentagons, we have 5 neighbors (skip one direction)
        var neighbors = new List<string>();
        
        // The 6 hexagonal directions are represented by digits 1-6
        // Direction 0 is center (no movement)
        // For pentagons, direction 1 (K_AXES_DIGIT) is the deleted subsequence
        var startDirection = isPentagon ? 2 : 1;  // Skip K direction for pentagons
        var endDirection = 7;  // Directions are 1-6, so we go up to 7 (exclusive)
        
        for (var direction = startDirection; direction < endDirection; direction++)
        {
            // Create a copy of the current cell's FaceIJK
            var neighborFijk = new FaceIJK(fijk.Face, new CoordIJK(fijk.Coord.I, fijk.Coord.J, fijk.Coord.K));
            
            // Move one step in the specified direction
            Neighbor(ref neighborFijk.Coord, direction);
            
            // Adjust for face overage (cells that cross face boundaries)
            // This is critical for cells near face edges
            var overage = AdjustOverageClassII(ref neighborFijk, resolution, false, false);
            
            // Build the H3 index for the neighbor
            try
            {
                var neighborIndex = BuildH3IndexFromFaceIJK(neighborFijk, resolution);
                var neighborString = H3IndexToString(neighborIndex);
                neighbors.Add(neighborString);
            }
            catch
            {
                // If we can't build a valid neighbor (shouldn't happen in normal cases),
                // skip it rather than failing the entire operation
                continue;
            }
        }
        
        return neighbors.ToArray();
    }

    // ===== Coordinate Conversion Methods =====

    private static (double x, double y, double z) LatLonToXYZ(double latitude, double longitude)
    {
        var latRad = DegreesToRadians(latitude);
        var lonRad = DegreesToRadians(longitude);
        
        var cosLat = Math.Cos(latRad);
        var x = Math.Cos(lonRad) * cosLat;
        var y = Math.Sin(lonRad) * cosLat;
        var z = Math.Sin(latRad);
        
        return (x, y, z);
    }

    private static (double latitude, double longitude) XYZToLatLon(double x, double y, double z)
    {
        var latitude = RadiansToDegrees(Math.Atan2(z, Math.Sqrt(x * x + y * y)));
        var longitude = RadiansToDegrees(Math.Atan2(y, x));
        
        return (latitude, longitude);
    }

    // ===== Icosahedron Face Selection =====
    
    // Gnomonic projection scaling constants from H3 reference implementation
    // RES0_U_GNOMONIC is the scaling factor for resolution 0 base cells
    // It represents the gnomonic projection distance from face center to base cell center
    private const double RES0_U_GNOMONIC = 0.38196601125010500003;
    private const double INV_RES0_U_GNOMONIC = 2.61803398874989588842;
    private const double M_SQRT7 = 2.6457513110645905905;
    private const double M_RSQRT7 = 0.3779644730092272272;
    private const double M_AP7_ROT_RADS = 0.333473172251832115336090755351601070065900389;
    
    /// <summary>
    /// Icosahedron face centers in lat/lon (radians).
    /// These are the precise coordinates from the H3 reference implementation.
    /// </summary>
    private static readonly (double lat, double lon)[] FaceCenterGeo = new[]
    {
        (0.803582649718989942, 1.248397419617396099),    // face  0
        (1.307747883455638156, 2.536945009877921159),    // face  1
        (1.054751253523952054, -1.347517358900396623),   // face  2
        (0.600191595538186799, -0.450603909469755746),   // face  3
        (0.491715428198773866, 0.401988202911306943),    // face  4
        (0.172745327415618701, 1.678146885280433686),    // face  5
        (0.605929321571350690, 2.953923329812411617),    // face  6
        (0.427370518328979641, -1.888876200336285401),   // face  7
        (-0.079066118549212831, -0.733429513380867741),  // face  8
        (-0.230961644455383637, 0.506495587332349035),   // face  9
        (0.079066118549212831, 2.408163338779814689),    // face 10
        (0.230961644455383637, -2.635097066257444203),   // face 11
        (-0.172745327415618701, -1.463445768309359553),  // face 12
        (-0.605929321571350690, -0.187669323777381622),  // face 13
        (-0.427370518328979641, 1.252716093845271365),   // face 14
        (-0.600191595538186799, 2.690988744120037492),   // face 15
        (-0.491715428198773866, -2.739604450403433234),  // face 16
        (-0.803582649718989942, -1.893195233972397139),  // face 17
        (-1.307747883455638156, -0.604647643711872080),  // face 18
        (-1.054751253523952054, 1.794075294689396615),   // face 19
    };
    
    /// <summary>
    /// Icosahedron face axes as azimuth in radians from face center to vertex 0/1/2.
    /// From H3 reference implementation (Class II orientation).
    /// </summary>
    private static readonly double[][] FaceAxesAzRadsCII = new[]
    {
        new[] { 5.619958268523939882, 3.525563166130744542, 1.431168063737548730 },  // face  0
        new[] { 5.760339081714187279, 3.665943979320991689, 1.571548876927796127 },  // face  1
        new[] { 0.780213654393430055, 4.969003859179821079, 2.874608756786625655 },  // face  2
        new[] { 0.430469363979999913, 4.619259568766391033, 2.524864466373195467 },  // face  3
        new[] { 6.130269123335111400, 4.035874020941915804, 1.941478918548720291 },  // face  4
        new[] { 2.692877706530642877, 0.598482604137447119, 4.787272808923838195 },  // face  5
        new[] { 2.982963003477243874, 0.888567901084048369, 5.077358105870439581 },  // face  6
        new[] { 3.532912002790141181, 1.438516900396945656, 5.627307105183336758 },  // face  7
        new[] { 3.494305004259568154, 1.399909901866372864, 5.588700106652763840 },  // face  8
        new[] { 3.003214169499538391, 0.908819067106342928, 5.097609271892733906 },  // face  9
        new[] { 5.930472956509811562, 3.836077854116615875, 1.741682751723420374 },  // face 10
        new[] { 0.138378484090254847, 4.327168688876645809, 2.232773586483450311 },  // face 11
        new[] { 0.448714947059150361, 4.637505151845541521, 2.543110049452346120 },  // face 12
        new[] { 0.158629650112549365, 4.347419854898940135, 2.253024752505744869 },  // face 13
        new[] { 5.891865957979238535, 3.797470855586042958, 1.703075753192847583 },  // face 14
        new[] { 2.711123289609793325, 0.616728187216597771, 4.805518392002988683 },  // face 15
        new[] { 3.294508837434268316, 1.200113735041072948, 5.388903939827463911 },  // face 16
        new[] { 3.804819692245439833, 1.710424589852244509, 5.899214794638635174 },  // face 17
        new[] { 3.664438879055192436, 1.570043776661997111, 5.758833981448388027 },  // face 18
        new[] { 2.361378999196363184, 0.266983896803167583, 4.455774101589558636 },  // face 19
    };
    
    /// <summary>
    /// Icosahedron face centers in x/y/z on the unit sphere.
    /// These are the precise coordinates from the H3 reference implementation.
    /// </summary>
    private static readonly (double x, double y, double z)[] FaceCenterPoints = new[]
    {
        (0.2199307791404606, 0.6583691780274996, 0.7198475378926182),     // face  0
        (-0.2139234834501421, 0.1478171829550703, 0.9656017935214205),    // face  1
        (0.1092625278784797, -0.4811951572873210, 0.8697775121287253),    // face  2
        (0.7428567301586791, -0.3593941678278028, 0.5648005936517033),    // face  3
        (0.8112534709140969, 0.3448953237639384, 0.4721387736413930),     // face  4
        (-0.1055498149613921, 0.9794457296411413, 0.1718874610009365),    // face  5
        (-0.8075407579970092, 0.1533552485898818, 0.5695261994882688),    // face  6
        (-0.2846148069787907, -0.8644080972654206, 0.4144792552473539),   // face  7
        (0.7405621473854482, -0.6673299564565524, -0.0789837646326737),   // face  8
        (0.8512303986474293, 0.4722343788582681, -0.2289137388687808),    // face  9
        (-0.7405621473854481, 0.6673299564565524, 0.0789837646326737),    // face 10
        (-0.8512303986474292, -0.4722343788582682, 0.2289137388687808),   // face 11
        (0.1055498149613919, -0.9794457296411413, -0.1718874610009365),   // face 12
        (0.8075407579970092, -0.1533552485898819, -0.5695261994882688),   // face 13
        (0.2846148069787908, 0.8644080972654204, -0.4144792552473539),    // face 14
        (-0.7428567301586791, 0.3593941678278027, -0.5648005936517033),   // face 15
        (-0.8112534709140971, -0.3448953237639382, -0.4721387736413930),  // face 16
        (-0.2199307791404607, -0.6583691780274996, -0.7198475378926182),  // face 17
        (0.2139234834501420, -0.1478171829550704, -0.9656017935214205),   // face 18
        (-0.1092625278784796, 0.4811951572873210, -0.8697775121287253),   // face 19
    };

    /// <summary>
    /// Selects the icosahedron face that is closest to the given point on the unit sphere.
    /// Uses the squared Euclidean distance to find the nearest face center.
    /// This is the proper H3 face selection algorithm from the reference implementation.
    /// </summary>
    private static int XYZToFace(double x, double y, double z)
    {
        var closestFace = 0;
        var minSquaredDistance = 5.0; // Maximum possible squared distance is 4.0, so start with 5.0
        
        for (var face = 0; face < NumIcosaFaces; face++)
        {
            var faceCenter = FaceCenterPoints[face];
            var squaredDistance = PointSquareDistance(x, y, z, faceCenter.x, faceCenter.y, faceCenter.z);
            
            if (squaredDistance < minSquaredDistance)
            {
                closestFace = face;
                minSquaredDistance = squaredDistance;
            }
        }
        
        return closestFace;
    }
    
    /// <summary>
    /// Calculates the squared Euclidean distance between two 3D points.
    /// </summary>
    private static double PointSquareDistance(double x1, double y1, double z1, double x2, double y2, double z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>
    /// Converts 3D coordinates on the unit sphere to 2D face-local coordinates using gnomonic projection.
    /// This implements the proper H3 gnomonic projection from the reference implementation.
    /// </summary>
    private static (double fx, double fy) XYZToFaceCoords(int face, double x, double y, double z)
    {
        // Convert XYZ to lat/lon (returns degrees)
        var (latDeg, lonDeg) = XYZToLatLon(x, y, z);
        
        // Convert to radians for calculations
        var lat = DegreesToRadians(latDeg);
        var lon = DegreesToRadians(lonDeg);
        
        // Get face center in lat/lon (already in radians)
        var faceCenterGeo = FaceCenterGeo[face];
        
        // Calculate great circle distance from face center to point
        var sqd = GreatCircleDistanceSquared(faceCenterGeo.lat, faceCenterGeo.lon, lat, lon);
        
        // cos(r) = 1 - 2 * sin^2(r/2) = 1 - sqd/2
        var r = Math.Acos(1.0 - sqd * 0.5);
        
        // If point is at face center, return (0, 0)
        if (r < 1e-10)
        {
            return (0.0, 0.0);
        }
        
        // Calculate azimuth from face center to point
        var azimuth = GeoAzimuthRads(faceCenterGeo.lat, faceCenterGeo.lon, lat, lon);
        
        // Calculate theta (angle from face i-axis, CCW)
        // In H3: theta = posAngleRads(faceIAxisAzimuth - pointAzimuth)
        // This gives the angle in the face coordinate system
        var theta = PosAngleRads(FaceAxesAzRadsCII[face][0] - azimuth);
        
        // Perform gnomonic scaling of r
        r = Math.Tan(r);
        
        // Convert to local x,y coordinates
        var fx = r * Math.Cos(theta);
        var fy = r * Math.Sin(theta);
        
        return (fx, fy);
    }

    /// <summary>
    /// Converts 2D face-local coordinates back to 3D coordinates on the unit sphere.
    /// This implements the inverse gnomonic projection from the H3 reference implementation.
    /// 
    /// The process (from H3's _hex2dToGeo):
    /// 1. Calculate r (magnitude) and theta (angle) from face coordinates
    /// 2. Apply inverse gnomonic projection: r = atan(r)
    /// 3. Calculate azimuth from face center: azimuth = face_i_axis - theta
    /// 4. Find point at distance r and azimuth from face center
    /// 
    /// CRITICAL: This function receives face coordinates that are in the RESOLUTION 0 coordinate system,
    /// not adjusted for the resolution. The resolution parameter is NOT passed here because the
    /// HexToFaceCoords function already handles the resolution scaling.
    /// </summary>
    private static (double x, double y, double z) FaceCoordsToXYZ(int face, double fx, double fy)
    {
        // Get face center in lat/lon (radians)
        var faceCenterGeo = FaceCenterGeo[face];
        
        // Calculate (r, theta) from face coordinates
        var r = Math.Sqrt(fx * fx + fy * fy);
        
        // If at face center, return face center XYZ
        if (r < 1e-10)
        {
            return LatLonToXYZ(RadiansToDegrees(faceCenterGeo.lat), RadiansToDegrees(faceCenterGeo.lon));
        }
        
        var theta = Math.Atan2(fy, fx);
        
        // Perform inverse gnomonic projection: r = atan(r)
        // This converts from gnomonic projection distance to great circle distance
        r = Math.Atan(r);
        
        // Calculate azimuth from face center
        // The face i-axis azimuth is stored in FaceAxesAzRadsCII[face][0]
        // In H3: azimuth = posAngleRads(faceIAxisAzimuth - theta)
        var azimuth = PosAngleRads(FaceAxesAzRadsCII[face][0] - theta);
        
        // Find the point at distance r and azimuth from the face center
        var (lat, lon) = GeoAzDistanceRads(faceCenterGeo.lat, faceCenterGeo.lon, azimuth, r);
        
        // Convert lat/lon (radians) to degrees, then to XYZ
        return LatLonToXYZ(RadiansToDegrees(lat), RadiansToDegrees(lon));
    }

    /// <summary>
    /// Gets the precise face center coordinates for the given icosahedron face.
    /// </summary>
    private static (double x, double y, double z) GetFaceCenter(int face)
    {
        if (face < 0 || face >= NumIcosaFaces)
        {
            throw new ArgumentOutOfRangeException(nameof(face), face, 
                $"Face must be between 0 and {NumIcosaFaces - 1}");
        }
        
        return FaceCenterPoints[face];
    }

    // ===== Base Cell Mapping =====
    
    /// <summary>
    /// Base cell data structure matching H3 reference implementation.
    /// Contains the home face and IJK coordinates for each base cell.
    /// </summary>
    private readonly struct BaseCellData
    {
        public int Face { get; }
        public int I { get; }
        public int J { get; }
        public int K { get; }
        public bool IsPentagon { get; }
        public int CwOffsetFace1 { get; }
        public int CwOffsetFace2 { get; }
        
        public BaseCellData(int face, int i, int j, int k, bool isPentagon, int cwOffsetFace1, int cwOffsetFace2)
        {
            Face = face;
            I = i;
            J = j;
            K = k;
            IsPentagon = isPentagon;
            CwOffsetFace1 = cwOffsetFace1;
            CwOffsetFace2 = cwOffsetFace2;
        }
    }
    
    /// <summary>
    /// Resolution 0 base cell data table from H3 reference implementation.
    /// Maps each of the 122 base cells to their home face, IJK coordinates, and pentagon CW offset faces.
    /// </summary>
    private static readonly BaseCellData[] BaseCellDataTable = new[]
    {
        new BaseCellData(1, 1, 0, 0, false, 0, 0),  // base cell 0
        new BaseCellData(2, 1, 1, 0, false, 0, 0),  // base cell 1
        new BaseCellData(1, 0, 0, 0, false, 0, 0),  // base cell 2
        new BaseCellData(2, 1, 0, 0, false, 0, 0),  // base cell 3
        new BaseCellData(0, 2, 0, 0, true, -1, -1),  // base cell 4
        new BaseCellData(1, 1, 1, 0, false, 0, 0),  // base cell 5
        new BaseCellData(1, 0, 0, 1, false, 0, 0),  // base cell 6
        new BaseCellData(2, 0, 0, 0, false, 0, 0),  // base cell 7
        new BaseCellData(0, 1, 0, 0, false, 0, 0),  // base cell 8
        new BaseCellData(2, 0, 1, 0, false, 0, 0),  // base cell 9
        new BaseCellData(1, 0, 1, 0, false, 0, 0),  // base cell 10
        new BaseCellData(1, 0, 1, 1, false, 0, 0),  // base cell 11
        new BaseCellData(3, 1, 0, 0, false, 0, 0),  // base cell 12
        new BaseCellData(3, 1, 1, 0, false, 0, 0),  // base cell 13
        new BaseCellData(11, 2, 0, 0, true, 2, 6),  // base cell 14
        new BaseCellData(4, 1, 0, 0, false, 0, 0),  // base cell 15
        new BaseCellData(0, 0, 0, 0, false, 0, 0),  // base cell 16
        new BaseCellData(6, 0, 1, 0, false, 0, 0),  // base cell 17
        new BaseCellData(0, 0, 0, 1, false, 0, 0),  // base cell 18
        new BaseCellData(2, 0, 1, 1, false, 0, 0),  // base cell 19
        new BaseCellData(7, 0, 0, 1, false, 0, 0),  // base cell 20
        new BaseCellData(2, 0, 0, 1, false, 0, 0),  // base cell 21
        new BaseCellData(0, 1, 1, 0, false, 0, 0),  // base cell 22
        new BaseCellData(6, 0, 0, 1, false, 0, 0),  // base cell 23
        new BaseCellData(10, 2, 0, 0, true, 1, 5),  // base cell 24
        new BaseCellData(6, 0, 0, 0, false, 0, 0),  // base cell 25
        new BaseCellData(3, 0, 0, 0, false, 0, 0),  // base cell 26
        new BaseCellData(11, 1, 0, 0, false, 0, 0),  // base cell 27
        new BaseCellData(4, 1, 1, 0, false, 0, 0),  // base cell 28
        new BaseCellData(3, 0, 1, 0, false, 0, 0),  // base cell 29
        new BaseCellData(0, 0, 1, 1, false, 0, 0),  // base cell 30
        new BaseCellData(4, 0, 0, 0, false, 0, 0),  // base cell 31
        new BaseCellData(5, 0, 1, 0, false, 0, 0),  // base cell 32
        new BaseCellData(0, 0, 1, 0, false, 0, 0),  // base cell 33
        new BaseCellData(7, 0, 1, 0, false, 0, 0),  // base cell 34
        new BaseCellData(11, 1, 1, 0, false, 0, 0),  // base cell 35
        new BaseCellData(7, 0, 0, 0, false, 0, 0),  // base cell 36
        new BaseCellData(10, 1, 0, 0, false, 0, 0),  // base cell 37
        new BaseCellData(12, 2, 0, 0, true, 3, 7),  // base cell 38
        new BaseCellData(6, 1, 0, 1, false, 0, 0),  // base cell 39
        new BaseCellData(7, 1, 0, 1, false, 0, 0),  // base cell 40
        new BaseCellData(4, 0, 0, 1, false, 0, 0),  // base cell 41
        new BaseCellData(3, 0, 0, 1, false, 0, 0),  // base cell 42
        new BaseCellData(3, 0, 1, 1, false, 0, 0),  // base cell 43
        new BaseCellData(4, 0, 1, 0, false, 0, 0),  // base cell 44
        new BaseCellData(6, 1, 0, 0, false, 0, 0),  // base cell 45
        new BaseCellData(11, 0, 0, 0, false, 0, 0),  // base cell 46
        new BaseCellData(8, 0, 0, 1, false, 0, 0),  // base cell 47
        new BaseCellData(5, 0, 0, 1, false, 0, 0),  // base cell 48
        new BaseCellData(14, 2, 0, 0, true, 0, 9),  // base cell 49
        new BaseCellData(5, 0, 0, 0, false, 0, 0),  // base cell 50
        new BaseCellData(12, 1, 0, 0, false, 0, 0),  // base cell 51
        new BaseCellData(10, 1, 1, 0, false, 0, 0),  // base cell 52
        new BaseCellData(4, 0, 1, 1, false, 0, 0),  // base cell 53
        new BaseCellData(12, 1, 1, 0, false, 0, 0),  // base cell 54
        new BaseCellData(7, 1, 0, 0, false, 0, 0),  // base cell 55
        new BaseCellData(11, 0, 1, 0, false, 0, 0),  // base cell 56
        new BaseCellData(10, 0, 0, 0, false, 0, 0),  // base cell 57
        new BaseCellData(13, 2, 0, 0, true, 4, 8),  // base cell 58
        new BaseCellData(10, 0, 0, 1, false, 0, 0),  // base cell 59
        new BaseCellData(11, 0, 0, 1, false, 0, 0),  // base cell 60
        new BaseCellData(9, 0, 1, 0, false, 0, 0),  // base cell 61
        new BaseCellData(8, 0, 1, 0, false, 0, 0),  // base cell 62
        new BaseCellData(6, 2, 0, 0, true, 11, 15),  // base cell 63
        new BaseCellData(8, 0, 0, 0, false, 0, 0),  // base cell 64
        new BaseCellData(9, 0, 0, 1, false, 0, 0),  // base cell 65
        new BaseCellData(14, 1, 0, 0, false, 0, 0),  // base cell 66
        new BaseCellData(5, 1, 0, 1, false, 0, 0),  // base cell 67
        new BaseCellData(16, 0, 1, 1, false, 0, 0),  // base cell 68
        new BaseCellData(8, 1, 0, 1, false, 0, 0),  // base cell 69
        new BaseCellData(5, 1, 0, 0, false, 0, 0),  // base cell 70
        new BaseCellData(12, 0, 0, 0, false, 0, 0),  // base cell 71
        new BaseCellData(7, 2, 0, 0, true, 12, 16),  // base cell 72
        new BaseCellData(12, 0, 1, 0, false, 0, 0),  // base cell 73
        new BaseCellData(10, 0, 1, 0, false, 0, 0),  // base cell 74
        new BaseCellData(9, 0, 0, 0, false, 0, 0),  // base cell 75
        new BaseCellData(13, 1, 0, 0, false, 0, 0),  // base cell 76
        new BaseCellData(16, 0, 0, 1, false, 0, 0),  // base cell 77
        new BaseCellData(15, 0, 1, 1, false, 0, 0),  // base cell 78
        new BaseCellData(15, 0, 1, 0, false, 0, 0),  // base cell 79
        new BaseCellData(16, 0, 1, 0, false, 0, 0),  // base cell 80
        new BaseCellData(14, 1, 1, 0, false, 0, 0),  // base cell 81
        new BaseCellData(13, 1, 1, 0, false, 0, 0),  // base cell 82
        new BaseCellData(5, 2, 0, 0, true, 10, 19),  // base cell 83
        new BaseCellData(8, 1, 0, 0, false, 0, 0),  // base cell 84
        new BaseCellData(14, 0, 0, 0, false, 0, 0),  // base cell 85
        new BaseCellData(9, 1, 0, 1, false, 0, 0),  // base cell 86
        new BaseCellData(14, 0, 0, 1, false, 0, 0),  // base cell 87
        new BaseCellData(17, 0, 0, 1, false, 0, 0),  // base cell 88
        new BaseCellData(12, 0, 0, 1, false, 0, 0),  // base cell 89
        new BaseCellData(16, 0, 0, 0, false, 0, 0),  // base cell 90
        new BaseCellData(17, 0, 1, 1, false, 0, 0),  // base cell 91
        new BaseCellData(15, 0, 0, 1, false, 0, 0),  // base cell 92
        new BaseCellData(16, 1, 0, 1, false, 0, 0),  // base cell 93
        new BaseCellData(9, 1, 0, 0, false, 0, 0),  // base cell 94
        new BaseCellData(15, 0, 0, 0, false, 0, 0),  // base cell 95
        new BaseCellData(13, 0, 0, 0, false, 0, 0),  // base cell 96
        new BaseCellData(8, 2, 0, 0, true, 13, 17),  // base cell 97
        new BaseCellData(13, 0, 1, 0, false, 0, 0),  // base cell 98
        new BaseCellData(17, 1, 0, 1, false, 0, 0),  // base cell 99
        new BaseCellData(19, 0, 1, 0, false, 0, 0),  // base cell 100
        new BaseCellData(14, 0, 1, 0, false, 0, 0),  // base cell 101
        new BaseCellData(19, 0, 1, 1, false, 0, 0),  // base cell 102
        new BaseCellData(17, 0, 1, 0, false, 0, 0),  // base cell 103
        new BaseCellData(13, 0, 0, 1, false, 0, 0),  // base cell 104
        new BaseCellData(17, 0, 0, 0, false, 0, 0),  // base cell 105
        new BaseCellData(16, 1, 0, 0, false, 0, 0),  // base cell 106
        new BaseCellData(9, 2, 0, 0, true, 14, 18),  // base cell 107
        new BaseCellData(15, 1, 0, 1, false, 0, 0),  // base cell 108
        new BaseCellData(15, 1, 0, 0, false, 0, 0),  // base cell 109
        new BaseCellData(18, 0, 1, 1, false, 0, 0),  // base cell 110
        new BaseCellData(18, 0, 0, 1, false, 0, 0),  // base cell 111
        new BaseCellData(19, 0, 0, 1, false, 0, 0),  // base cell 112
        new BaseCellData(17, 1, 0, 0, false, 0, 0),  // base cell 113
        new BaseCellData(19, 0, 0, 0, false, 0, 0),  // base cell 114
        new BaseCellData(18, 0, 1, 0, false, 0, 0),  // base cell 115
        new BaseCellData(18, 1, 0, 1, false, 0, 0),  // base cell 116
        new BaseCellData(19, 2, 0, 0, true, -1, -1),  // base cell 117
        new BaseCellData(19, 1, 0, 0, false, 0, 0),  // base cell 118
        new BaseCellData(18, 0, 0, 0, false, 0, 0),  // base cell 119
        new BaseCellData(19, 1, 0, 1, false, 0, 0),  // base cell 120
        new BaseCellData(18, 1, 0, 0, false, 0, 0)  // base cell 121
    };
    
    private readonly struct BaseCellRotation
    {
        public int BaseCell { get; }
        public int CcwRot60 { get; }
        
        public BaseCellRotation(int baseCell, int ccwRot60)
        {
            BaseCell = baseCell;
            CcwRot60 = ccwRot60;
        }
    }
    
    /// <summary>
    /// Resolution 0 base cell lookup table for each face with required rotation into base cell coordinates.
    /// Maps face + IJK coordinates (0-2) to the base cell and number of CCW 60° rotations.
    /// </summary>
    private static readonly BaseCellRotation[,,,] FaceIjkBaseCells = new BaseCellRotation[20, 3, 3, 3]
    {
        { // face 0
            {
                { new BaseCellRotation(16, 0), new BaseCellRotation(18, 0), new BaseCellRotation(24, 0) },
                { new BaseCellRotation(33, 0), new BaseCellRotation(30, 0), new BaseCellRotation(32, 3) },
                { new BaseCellRotation(49, 1), new BaseCellRotation(48, 3), new BaseCellRotation(50, 3) },
            },
            {
                { new BaseCellRotation(8, 0), new BaseCellRotation(5, 5), new BaseCellRotation(10, 5) },
                { new BaseCellRotation(22, 0), new BaseCellRotation(16, 0), new BaseCellRotation(18, 0) },
                { new BaseCellRotation(41, 1), new BaseCellRotation(33, 0), new BaseCellRotation(30, 0) },
            },
            {
                { new BaseCellRotation(4, 0), new BaseCellRotation(0, 5), new BaseCellRotation(2, 5) },
                { new BaseCellRotation(15, 1), new BaseCellRotation(8, 0), new BaseCellRotation(5, 5) },
                { new BaseCellRotation(31, 1), new BaseCellRotation(22, 0), new BaseCellRotation(16, 0) },
            },
        },
        { // face 1
            {
                { new BaseCellRotation(2, 0), new BaseCellRotation(6, 0), new BaseCellRotation(14, 0) },
                { new BaseCellRotation(10, 0), new BaseCellRotation(11, 0), new BaseCellRotation(17, 3) },
                { new BaseCellRotation(24, 1), new BaseCellRotation(23, 3), new BaseCellRotation(25, 3) },
            },
            {
                { new BaseCellRotation(0, 0), new BaseCellRotation(1, 5), new BaseCellRotation(9, 5) },
                { new BaseCellRotation(5, 0), new BaseCellRotation(2, 0), new BaseCellRotation(6, 0) },
                { new BaseCellRotation(18, 1), new BaseCellRotation(10, 0), new BaseCellRotation(11, 0) },
            },
            {
                { new BaseCellRotation(4, 1), new BaseCellRotation(3, 5), new BaseCellRotation(7, 5) },
                { new BaseCellRotation(8, 1), new BaseCellRotation(0, 0), new BaseCellRotation(1, 5) },
                { new BaseCellRotation(16, 1), new BaseCellRotation(5, 0), new BaseCellRotation(2, 0) },
            },
        },
        { // face 2
            {
                { new BaseCellRotation(7, 0), new BaseCellRotation(21, 0), new BaseCellRotation(38, 0) },
                { new BaseCellRotation(9, 0), new BaseCellRotation(19, 0), new BaseCellRotation(34, 3) },
                { new BaseCellRotation(14, 1), new BaseCellRotation(20, 3), new BaseCellRotation(36, 3) },
            },
            {
                { new BaseCellRotation(3, 0), new BaseCellRotation(13, 5), new BaseCellRotation(29, 5) },
                { new BaseCellRotation(1, 0), new BaseCellRotation(7, 0), new BaseCellRotation(21, 0) },
                { new BaseCellRotation(6, 1), new BaseCellRotation(9, 0), new BaseCellRotation(19, 0) },
            },
            {
                { new BaseCellRotation(4, 2), new BaseCellRotation(12, 5), new BaseCellRotation(26, 5) },
                { new BaseCellRotation(0, 1), new BaseCellRotation(3, 0), new BaseCellRotation(13, 5) },
                { new BaseCellRotation(2, 1), new BaseCellRotation(1, 0), new BaseCellRotation(7, 0) },
            },
        },
        { // face 3
            {
                { new BaseCellRotation(26, 0), new BaseCellRotation(42, 0), new BaseCellRotation(58, 0) },
                { new BaseCellRotation(29, 0), new BaseCellRotation(43, 0), new BaseCellRotation(62, 3) },
                { new BaseCellRotation(38, 1), new BaseCellRotation(47, 3), new BaseCellRotation(64, 3) },
            },
            {
                { new BaseCellRotation(12, 0), new BaseCellRotation(28, 5), new BaseCellRotation(44, 5) },
                { new BaseCellRotation(13, 0), new BaseCellRotation(26, 0), new BaseCellRotation(42, 0) },
                { new BaseCellRotation(21, 1), new BaseCellRotation(29, 0), new BaseCellRotation(43, 0) },
            },
            {
                { new BaseCellRotation(4, 3), new BaseCellRotation(15, 5), new BaseCellRotation(31, 5) },
                { new BaseCellRotation(3, 1), new BaseCellRotation(12, 0), new BaseCellRotation(28, 5) },
                { new BaseCellRotation(7, 1), new BaseCellRotation(13, 0), new BaseCellRotation(26, 0) },
            },
        },
        { // face 4
            {
                { new BaseCellRotation(31, 0), new BaseCellRotation(41, 0), new BaseCellRotation(49, 0) },
                { new BaseCellRotation(44, 0), new BaseCellRotation(53, 0), new BaseCellRotation(61, 3) },
                { new BaseCellRotation(58, 1), new BaseCellRotation(65, 3), new BaseCellRotation(75, 3) },
            },
            {
                { new BaseCellRotation(15, 0), new BaseCellRotation(22, 5), new BaseCellRotation(33, 5) },
                { new BaseCellRotation(28, 0), new BaseCellRotation(31, 0), new BaseCellRotation(41, 0) },
                { new BaseCellRotation(42, 1), new BaseCellRotation(44, 0), new BaseCellRotation(53, 0) },
            },
            {
                { new BaseCellRotation(4, 4), new BaseCellRotation(8, 5), new BaseCellRotation(16, 5) },
                { new BaseCellRotation(12, 1), new BaseCellRotation(15, 0), new BaseCellRotation(22, 5) },
                { new BaseCellRotation(26, 1), new BaseCellRotation(28, 0), new BaseCellRotation(31, 0) },
            },
        },
        { // face 5
            {
                { new BaseCellRotation(50, 0), new BaseCellRotation(48, 0), new BaseCellRotation(49, 3) },
                { new BaseCellRotation(32, 0), new BaseCellRotation(30, 3), new BaseCellRotation(33, 3) },
                { new BaseCellRotation(24, 3), new BaseCellRotation(18, 3), new BaseCellRotation(16, 3) },
            },
            {
                { new BaseCellRotation(70, 0), new BaseCellRotation(67, 0), new BaseCellRotation(66, 3) },
                { new BaseCellRotation(52, 3), new BaseCellRotation(50, 0), new BaseCellRotation(48, 0) },
                { new BaseCellRotation(37, 3), new BaseCellRotation(32, 0), new BaseCellRotation(30, 3) },
            },
            {
                { new BaseCellRotation(83, 0), new BaseCellRotation(87, 3), new BaseCellRotation(85, 3) },
                { new BaseCellRotation(74, 3), new BaseCellRotation(70, 0), new BaseCellRotation(67, 0) },
                { new BaseCellRotation(57, 1), new BaseCellRotation(52, 3), new BaseCellRotation(50, 0) },
            },
        },
        { // face 6
            {
                { new BaseCellRotation(25, 0), new BaseCellRotation(23, 0), new BaseCellRotation(24, 3) },
                { new BaseCellRotation(17, 0), new BaseCellRotation(11, 3), new BaseCellRotation(10, 3) },
                { new BaseCellRotation(14, 3), new BaseCellRotation(6, 3), new BaseCellRotation(2, 3) },
            },
            {
                { new BaseCellRotation(45, 0), new BaseCellRotation(39, 0), new BaseCellRotation(37, 3) },
                { new BaseCellRotation(35, 3), new BaseCellRotation(25, 0), new BaseCellRotation(23, 0) },
                { new BaseCellRotation(27, 3), new BaseCellRotation(17, 0), new BaseCellRotation(11, 3) },
            },
            {
                { new BaseCellRotation(63, 0), new BaseCellRotation(59, 3), new BaseCellRotation(57, 3) },
                { new BaseCellRotation(56, 3), new BaseCellRotation(45, 0), new BaseCellRotation(39, 0) },
                { new BaseCellRotation(46, 3), new BaseCellRotation(35, 3), new BaseCellRotation(25, 0) },
            },
        },
        { // face 7
            {
                { new BaseCellRotation(36, 0), new BaseCellRotation(20, 0), new BaseCellRotation(14, 3) },
                { new BaseCellRotation(34, 0), new BaseCellRotation(19, 3), new BaseCellRotation(9, 3) },
                { new BaseCellRotation(38, 3), new BaseCellRotation(21, 3), new BaseCellRotation(7, 3) },
            },
            {
                { new BaseCellRotation(55, 0), new BaseCellRotation(40, 0), new BaseCellRotation(27, 3) },
                { new BaseCellRotation(54, 3), new BaseCellRotation(36, 0), new BaseCellRotation(20, 0) },
                { new BaseCellRotation(51, 3), new BaseCellRotation(34, 0), new BaseCellRotation(19, 3) },
            },
            {
                { new BaseCellRotation(72, 0), new BaseCellRotation(60, 3), new BaseCellRotation(46, 3) },
                { new BaseCellRotation(73, 3), new BaseCellRotation(55, 0), new BaseCellRotation(40, 0) },
                { new BaseCellRotation(71, 3), new BaseCellRotation(54, 3), new BaseCellRotation(36, 0) },
            },
        },
        { // face 8
            {
                { new BaseCellRotation(64, 0), new BaseCellRotation(47, 0), new BaseCellRotation(38, 3) },
                { new BaseCellRotation(62, 0), new BaseCellRotation(43, 3), new BaseCellRotation(29, 3) },
                { new BaseCellRotation(58, 3), new BaseCellRotation(42, 3), new BaseCellRotation(26, 3) },
            },
            {
                { new BaseCellRotation(84, 0), new BaseCellRotation(69, 0), new BaseCellRotation(51, 3) },
                { new BaseCellRotation(82, 3), new BaseCellRotation(64, 0), new BaseCellRotation(47, 0) },
                { new BaseCellRotation(76, 3), new BaseCellRotation(62, 0), new BaseCellRotation(43, 3) },
            },
            {
                { new BaseCellRotation(97, 0), new BaseCellRotation(89, 3), new BaseCellRotation(71, 3) },
                { new BaseCellRotation(98, 3), new BaseCellRotation(84, 0), new BaseCellRotation(69, 0) },
                { new BaseCellRotation(96, 3), new BaseCellRotation(82, 3), new BaseCellRotation(64, 0) },
            },
        },
        { // face 9
            {
                { new BaseCellRotation(75, 0), new BaseCellRotation(65, 0), new BaseCellRotation(58, 3) },
                { new BaseCellRotation(61, 0), new BaseCellRotation(53, 3), new BaseCellRotation(44, 3) },
                { new BaseCellRotation(49, 3), new BaseCellRotation(41, 3), new BaseCellRotation(31, 3) },
            },
            {
                { new BaseCellRotation(94, 0), new BaseCellRotation(86, 0), new BaseCellRotation(76, 3) },
                { new BaseCellRotation(81, 3), new BaseCellRotation(75, 0), new BaseCellRotation(65, 0) },
                { new BaseCellRotation(66, 3), new BaseCellRotation(61, 0), new BaseCellRotation(53, 3) },
            },
            {
                { new BaseCellRotation(107, 0), new BaseCellRotation(104, 3), new BaseCellRotation(96, 3) },
                { new BaseCellRotation(101, 3), new BaseCellRotation(94, 0), new BaseCellRotation(86, 0) },
                { new BaseCellRotation(85, 3), new BaseCellRotation(81, 3), new BaseCellRotation(75, 0) },
            },
        },
        { // face 10
            {
                { new BaseCellRotation(57, 0), new BaseCellRotation(59, 0), new BaseCellRotation(63, 3) },
                { new BaseCellRotation(74, 0), new BaseCellRotation(78, 3), new BaseCellRotation(79, 3) },
                { new BaseCellRotation(83, 3), new BaseCellRotation(92, 3), new BaseCellRotation(95, 3) },
            },
            {
                { new BaseCellRotation(37, 0), new BaseCellRotation(39, 3), new BaseCellRotation(45, 3) },
                { new BaseCellRotation(52, 0), new BaseCellRotation(57, 0), new BaseCellRotation(59, 0) },
                { new BaseCellRotation(70, 3), new BaseCellRotation(74, 0), new BaseCellRotation(78, 3) },
            },
            {
                { new BaseCellRotation(24, 0), new BaseCellRotation(23, 3), new BaseCellRotation(25, 3) },
                { new BaseCellRotation(32, 3), new BaseCellRotation(37, 0), new BaseCellRotation(39, 3) },
                { new BaseCellRotation(50, 3), new BaseCellRotation(52, 0), new BaseCellRotation(57, 0) },
            },
        },
        { // face 11
            {
                { new BaseCellRotation(46, 0), new BaseCellRotation(60, 0), new BaseCellRotation(72, 3) },
                { new BaseCellRotation(56, 0), new BaseCellRotation(68, 3), new BaseCellRotation(80, 3) },
                { new BaseCellRotation(63, 3), new BaseCellRotation(77, 3), new BaseCellRotation(90, 3) },
            },
            {
                { new BaseCellRotation(27, 0), new BaseCellRotation(40, 3), new BaseCellRotation(55, 3) },
                { new BaseCellRotation(35, 0), new BaseCellRotation(46, 0), new BaseCellRotation(60, 0) },
                { new BaseCellRotation(45, 3), new BaseCellRotation(56, 0), new BaseCellRotation(68, 3) },
            },
            {
                { new BaseCellRotation(14, 0), new BaseCellRotation(20, 3), new BaseCellRotation(36, 3) },
                { new BaseCellRotation(17, 3), new BaseCellRotation(27, 0), new BaseCellRotation(40, 3) },
                { new BaseCellRotation(25, 3), new BaseCellRotation(35, 0), new BaseCellRotation(46, 0) },
            },
        },
        { // face 12
            {
                { new BaseCellRotation(71, 0), new BaseCellRotation(89, 0), new BaseCellRotation(97, 3) },
                { new BaseCellRotation(73, 0), new BaseCellRotation(91, 3), new BaseCellRotation(103, 3) },
                { new BaseCellRotation(72, 3), new BaseCellRotation(88, 3), new BaseCellRotation(105, 3) },
            },
            {
                { new BaseCellRotation(51, 0), new BaseCellRotation(69, 3), new BaseCellRotation(84, 3) },
                { new BaseCellRotation(54, 0), new BaseCellRotation(71, 0), new BaseCellRotation(89, 0) },
                { new BaseCellRotation(55, 3), new BaseCellRotation(73, 0), new BaseCellRotation(91, 3) },
            },
            {
                { new BaseCellRotation(38, 0), new BaseCellRotation(47, 3), new BaseCellRotation(64, 3) },
                { new BaseCellRotation(34, 3), new BaseCellRotation(51, 0), new BaseCellRotation(69, 3) },
                { new BaseCellRotation(36, 3), new BaseCellRotation(54, 0), new BaseCellRotation(71, 0) },
            },
        },
        { // face 13
            {
                { new BaseCellRotation(96, 0), new BaseCellRotation(104, 0), new BaseCellRotation(107, 3) },
                { new BaseCellRotation(98, 0), new BaseCellRotation(110, 3), new BaseCellRotation(115, 3) },
                { new BaseCellRotation(97, 3), new BaseCellRotation(111, 3), new BaseCellRotation(119, 3) },
            },
            {
                { new BaseCellRotation(76, 0), new BaseCellRotation(86, 3), new BaseCellRotation(94, 3) },
                { new BaseCellRotation(82, 0), new BaseCellRotation(96, 0), new BaseCellRotation(104, 0) },
                { new BaseCellRotation(84, 3), new BaseCellRotation(98, 0), new BaseCellRotation(110, 3) },
            },
            {
                { new BaseCellRotation(58, 0), new BaseCellRotation(65, 3), new BaseCellRotation(75, 3) },
                { new BaseCellRotation(62, 3), new BaseCellRotation(76, 0), new BaseCellRotation(86, 3) },
                { new BaseCellRotation(64, 3), new BaseCellRotation(82, 0), new BaseCellRotation(96, 0) },
            },
        },
        { // face 14
            {
                { new BaseCellRotation(85, 0), new BaseCellRotation(87, 0), new BaseCellRotation(83, 3) },
                { new BaseCellRotation(101, 0), new BaseCellRotation(102, 3), new BaseCellRotation(100, 3) },
                { new BaseCellRotation(107, 3), new BaseCellRotation(112, 3), new BaseCellRotation(114, 3) },
            },
            {
                { new BaseCellRotation(66, 0), new BaseCellRotation(67, 3), new BaseCellRotation(70, 3) },
                { new BaseCellRotation(81, 0), new BaseCellRotation(85, 0), new BaseCellRotation(87, 0) },
                { new BaseCellRotation(94, 3), new BaseCellRotation(101, 0), new BaseCellRotation(102, 3) },
            },
            {
                { new BaseCellRotation(49, 0), new BaseCellRotation(48, 3), new BaseCellRotation(50, 3) },
                { new BaseCellRotation(61, 3), new BaseCellRotation(66, 0), new BaseCellRotation(67, 3) },
                { new BaseCellRotation(75, 3), new BaseCellRotation(81, 0), new BaseCellRotation(85, 0) },
            },
        },
        { // face 15
            {
                { new BaseCellRotation(95, 0), new BaseCellRotation(92, 0), new BaseCellRotation(83, 0) },
                { new BaseCellRotation(79, 0), new BaseCellRotation(78, 0), new BaseCellRotation(74, 3) },
                { new BaseCellRotation(63, 1), new BaseCellRotation(59, 3), new BaseCellRotation(57, 3) },
            },
            {
                { new BaseCellRotation(109, 0), new BaseCellRotation(108, 0), new BaseCellRotation(100, 5) },
                { new BaseCellRotation(93, 1), new BaseCellRotation(95, 0), new BaseCellRotation(92, 0) },
                { new BaseCellRotation(77, 1), new BaseCellRotation(79, 0), new BaseCellRotation(78, 0) },
            },
            {
                { new BaseCellRotation(117, 4), new BaseCellRotation(118, 5), new BaseCellRotation(114, 5) },
                { new BaseCellRotation(106, 1), new BaseCellRotation(109, 0), new BaseCellRotation(108, 0) },
                { new BaseCellRotation(90, 1), new BaseCellRotation(93, 1), new BaseCellRotation(95, 0) },
            },
        },
        { // face 16
            {
                { new BaseCellRotation(90, 0), new BaseCellRotation(77, 0), new BaseCellRotation(63, 0) },
                { new BaseCellRotation(80, 0), new BaseCellRotation(68, 0), new BaseCellRotation(56, 3) },
                { new BaseCellRotation(72, 1), new BaseCellRotation(60, 3), new BaseCellRotation(46, 3) },
            },
            {
                { new BaseCellRotation(106, 0), new BaseCellRotation(93, 0), new BaseCellRotation(79, 5) },
                { new BaseCellRotation(99, 1), new BaseCellRotation(90, 0), new BaseCellRotation(77, 0) },
                { new BaseCellRotation(88, 1), new BaseCellRotation(80, 0), new BaseCellRotation(68, 0) },
            },
            {
                { new BaseCellRotation(117, 3), new BaseCellRotation(109, 5), new BaseCellRotation(95, 5) },
                { new BaseCellRotation(113, 1), new BaseCellRotation(106, 0), new BaseCellRotation(93, 0) },
                { new BaseCellRotation(105, 1), new BaseCellRotation(99, 1), new BaseCellRotation(90, 0) },
            },
        },
        { // face 17
            {
                { new BaseCellRotation(105, 0), new BaseCellRotation(88, 0), new BaseCellRotation(72, 0) },
                { new BaseCellRotation(103, 0), new BaseCellRotation(91, 0), new BaseCellRotation(73, 3) },
                { new BaseCellRotation(97, 1), new BaseCellRotation(89, 3), new BaseCellRotation(71, 3) },
            },
            {
                { new BaseCellRotation(113, 0), new BaseCellRotation(99, 0), new BaseCellRotation(80, 5) },
                { new BaseCellRotation(116, 1), new BaseCellRotation(105, 0), new BaseCellRotation(88, 0) },
                { new BaseCellRotation(111, 1), new BaseCellRotation(103, 0), new BaseCellRotation(91, 0) },
            },
            {
                { new BaseCellRotation(117, 2), new BaseCellRotation(106, 5), new BaseCellRotation(90, 5) },
                { new BaseCellRotation(121, 1), new BaseCellRotation(113, 0), new BaseCellRotation(99, 0) },
                { new BaseCellRotation(119, 1), new BaseCellRotation(116, 1), new BaseCellRotation(105, 0) },
            },
        },
        { // face 18
            {
                { new BaseCellRotation(119, 0), new BaseCellRotation(111, 0), new BaseCellRotation(97, 0) },
                { new BaseCellRotation(115, 0), new BaseCellRotation(110, 0), new BaseCellRotation(98, 3) },
                { new BaseCellRotation(107, 1), new BaseCellRotation(104, 3), new BaseCellRotation(96, 3) },
            },
            {
                { new BaseCellRotation(121, 0), new BaseCellRotation(116, 0), new BaseCellRotation(103, 5) },
                { new BaseCellRotation(120, 1), new BaseCellRotation(119, 0), new BaseCellRotation(111, 0) },
                { new BaseCellRotation(112, 1), new BaseCellRotation(115, 0), new BaseCellRotation(110, 0) },
            },
            {
                { new BaseCellRotation(117, 1), new BaseCellRotation(113, 5), new BaseCellRotation(105, 5) },
                { new BaseCellRotation(118, 1), new BaseCellRotation(121, 0), new BaseCellRotation(116, 0) },
                { new BaseCellRotation(114, 1), new BaseCellRotation(120, 1), new BaseCellRotation(119, 0) },
            },
        },
        { // face 19
            {
                { new BaseCellRotation(114, 0), new BaseCellRotation(112, 0), new BaseCellRotation(107, 0) },
                { new BaseCellRotation(100, 0), new BaseCellRotation(102, 0), new BaseCellRotation(101, 3) },
                { new BaseCellRotation(83, 1), new BaseCellRotation(87, 3), new BaseCellRotation(85, 3) },
            },
            {
                { new BaseCellRotation(118, 0), new BaseCellRotation(120, 0), new BaseCellRotation(115, 5) },
                { new BaseCellRotation(108, 1), new BaseCellRotation(114, 0), new BaseCellRotation(112, 0) },
                { new BaseCellRotation(92, 1), new BaseCellRotation(100, 0), new BaseCellRotation(102, 0) },
            },
            {
                { new BaseCellRotation(117, 0), new BaseCellRotation(121, 5), new BaseCellRotation(119, 5) },
                { new BaseCellRotation(109, 1), new BaseCellRotation(118, 0), new BaseCellRotation(120, 0) },
                { new BaseCellRotation(95, 1), new BaseCellRotation(108, 1), new BaseCellRotation(114, 0) },
            },
        },
    };

    // Face neighbor direction constants
    // Array indices for FaceNeighbors: [0]=central, [1]=IJ, [2]=KI, [3]=JK
    private const int IJ = 1;
    private const int KI = 2;  // FIXED: was JK = 2
    private const int JK = 3;  // FIXED: was KI = 3
    
    /// <summary>
    /// Face orientation and neighbor information.
    /// </summary>
    private struct FaceOrientIJK
    {
        public int Face;
        public CoordIJK Translate;
        public int CcwRot60;
        
        public FaceOrientIJK(int face, int translateI, int translateJ, int translateK, int ccwRot60)
        {
            Face = face;
            Translate = new CoordIJK(translateI, translateJ, translateK);
            CcwRot60 = ccwRot60;
        }
    }
    
    /// <summary>
    /// Definition of which faces neighbor each other.
    /// From H3 reference implementation faceijk.c
    /// </summary>
    private static readonly FaceOrientIJK[][] FaceNeighbors = new[]
    {
        // face 0
        new[]
        {
            new FaceOrientIJK(0, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(4, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(1, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(5, 0, 2, 2, 3)   // jk quadrant
        },
        // face 1
        new[]
        {
            new FaceOrientIJK(1, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(0, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(2, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(6, 0, 2, 2, 3)   // jk quadrant
        },
        // face 2
        new[]
        {
            new FaceOrientIJK(2, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(1, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(3, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(7, 0, 2, 2, 3)   // jk quadrant
        },
        // face 3
        new[]
        {
            new FaceOrientIJK(3, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(2, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(4, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(8, 0, 2, 2, 3)   // jk quadrant
        },
        // face 4
        new[]
        {
            new FaceOrientIJK(4, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(3, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(0, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(9, 0, 2, 2, 3)   // jk quadrant
        },
        // face 5
        new[]
        {
            new FaceOrientIJK(5, 0, 0, 0, 0),   // central face
            new FaceOrientIJK(10, 2, 2, 0, 3),  // ij quadrant
            new FaceOrientIJK(14, 2, 0, 2, 3),  // ki quadrant
            new FaceOrientIJK(0, 0, 2, 2, 3)    // jk quadrant
        },
        // face 6
        new[]
        {
            new FaceOrientIJK(6, 0, 0, 0, 0),   // central face
            new FaceOrientIJK(11, 2, 2, 0, 3),  // ij quadrant
            new FaceOrientIJK(10, 2, 0, 2, 3),  // ki quadrant
            new FaceOrientIJK(1, 0, 2, 2, 3)    // jk quadrant
        },
        // face 7
        new[]
        {
            new FaceOrientIJK(7, 0, 0, 0, 0),   // central face
            new FaceOrientIJK(12, 2, 2, 0, 3),  // ij quadrant
            new FaceOrientIJK(11, 2, 0, 2, 3),  // ki quadrant
            new FaceOrientIJK(2, 0, 2, 2, 3)    // jk quadrant
        },
        // face 8
        new[]
        {
            new FaceOrientIJK(8, 0, 0, 0, 0),   // central face
            new FaceOrientIJK(13, 2, 2, 0, 3),  // ij quadrant
            new FaceOrientIJK(12, 2, 0, 2, 3),  // ki quadrant
            new FaceOrientIJK(3, 0, 2, 2, 3)    // jk quadrant
        },
        // face 9
        new[]
        {
            new FaceOrientIJK(9, 0, 0, 0, 0),   // central face
            new FaceOrientIJK(14, 2, 2, 0, 3),  // ij quadrant
            new FaceOrientIJK(13, 2, 0, 2, 3),  // ki quadrant
            new FaceOrientIJK(4, 0, 2, 2, 3)    // jk quadrant
        },
        // face 10
        new[]
        {
            new FaceOrientIJK(10, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(5, 2, 2, 0, 3),   // ij quadrant
            new FaceOrientIJK(6, 2, 0, 2, 3),   // ki quadrant
            new FaceOrientIJK(15, 0, 2, 2, 3)   // jk quadrant
        },
        // face 11
        new[]
        {
            new FaceOrientIJK(11, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(6, 2, 2, 0, 3),   // ij quadrant
            new FaceOrientIJK(7, 2, 0, 2, 3),   // ki quadrant
            new FaceOrientIJK(16, 0, 2, 2, 3)   // jk quadrant
        },
        // face 12
        new[]
        {
            new FaceOrientIJK(12, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(7, 2, 2, 0, 3),   // ij quadrant
            new FaceOrientIJK(8, 2, 0, 2, 3),   // ki quadrant
            new FaceOrientIJK(17, 0, 2, 2, 3)   // jk quadrant
        },
        // face 13
        new[]
        {
            new FaceOrientIJK(13, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(8, 2, 2, 0, 3),   // ij quadrant
            new FaceOrientIJK(9, 2, 0, 2, 3),   // ki quadrant
            new FaceOrientIJK(18, 0, 2, 2, 3)   // jk quadrant
        },
        // face 14
        new[]
        {
            new FaceOrientIJK(14, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(9, 2, 2, 0, 3),   // ij quadrant
            new FaceOrientIJK(5, 2, 0, 2, 3),   // ki quadrant
            new FaceOrientIJK(19, 0, 2, 2, 3)   // jk quadrant
        },
        // face 15
        new[]
        {
            new FaceOrientIJK(15, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(16, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(19, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(10, 0, 2, 2, 3)   // jk quadrant
        },
        // face 16
        new[]
        {
            new FaceOrientIJK(16, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(17, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(15, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(11, 0, 2, 2, 3)   // jk quadrant
        },
        // face 17
        new[]
        {
            new FaceOrientIJK(17, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(18, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(16, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(12, 0, 2, 2, 3)   // jk quadrant
        },
        // face 18
        new[]
        {
            new FaceOrientIJK(18, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(19, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(17, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(13, 0, 2, 2, 3)   // jk quadrant
        },
        // face 19
        new[]
        {
            new FaceOrientIJK(19, 0, 0, 0, 0),  // central face
            new FaceOrientIJK(15, 2, 0, 2, 1),  // ij quadrant
            new FaceOrientIJK(18, 2, 2, 0, 5),  // ki quadrant
            new FaceOrientIJK(14, 0, 2, 2, 3)   // jk quadrant
        }
    };
    
    /// <summary>
    /// Maximum dimension values for Class II resolutions (overage distance table).
    /// From H3 reference implementation.
    /// </summary>
    private static readonly int[] MaxDimByCIIRes = new[]
    {
        2,        // res  0
        -1,       // res  1
        14,       // res  2
        -1,       // res  3
        98,       // res  4
        -1,       // res  5
        686,      // res  6
        -1,       // res  7
        4802,     // res  8
        -1,       // res  9
        33614,    // res  10
        -1,       // res  11
        235298,   // res  12
        -1,       // res  13
        1647086,  // res  14
        -1,       // res  15
        11529602  // res  16
    };
    
    /// <summary>
    /// Unit scale distance table for Class II resolutions.
    /// From H3 reference implementation.
    /// </summary>
    private static readonly int[] UnitScaleByCIIRes = new[]
    {
        1,       // res  0
        -1,      // res  1
        7,       // res  2
        -1,      // res  3
        49,      // res  4
        -1,      // res  5
        343,     // res  6
        -1,      // res  7
        2401,    // res  8
        -1,      // res  9
        16807,   // res  10
        -1,      // res  11
        117649,  // res  12
        -1,      // res  13
        823543,  // res  14
        -1,      // res  15
        5764801  // res  16
    };

    /// <summary>
    /// Converts face coordinates to a base cell using the proper H3 lookup table.
    /// This implements the accurate base cell selection from the H3 reference implementation.
    /// The process follows H3's _geoToFaceIjk: face coords -> Vec2d -> IJK -> base cell.
    /// 
    /// CRITICAL: The face coordinates from XYZToFaceCoords are in gnomonic projection space.
    /// We need to scale them by 1/RES0_U_GNOMONIC to convert to the hex2d coordinate system
    /// where Hex2dToCoordIJK expects them.
    /// </summary>
    private static int FaceCoordsToBaseCell(int face, double fx, double fy)
    {
        // Scale face coordinates from gnomonic projection to hex2d coordinate system
        // In H3, resolution 0 base cells are at distance RES0_U_GNOMONIC from face center
        // So we divide by this to get the hex2d coordinates
        var scaledX = fx * INV_RES0_U_GNOMONIC;
        var scaledY = fy * INV_RES0_U_GNOMONIC;
        
        // Create a Vec2d from scaled coordinates
        var v = new Vec2d(scaledX, scaledY);
        
        // Convert Vec2d to IJK coordinates at resolution 0
        // This uses the same Hex2dToCoordIJK function that's used for all resolutions
        var ijk = Hex2dToCoordIJK(v);
        
        // Look up base cell from the FaceIJK table
        // The table is indexed by [face][i][j][k]
        // We need to ensure IJK coordinates are in valid range [0, 2]
        var i = Math.Max(0, Math.Min(2, ijk.I));
        var j = Math.Max(0, Math.Min(2, ijk.J));
        var k = Math.Max(0, Math.Min(2, ijk.K));
        
        return FaceIjkBaseCells[face, i, j, k].BaseCell;
    }

    /// <summary>
    /// Looks up the base cell for a given FaceIJK.
    /// Based on H3 reference implementation's _faceIjkToBaseCell.
    /// </summary>
    private static int FaceIJKToBaseCell(FaceIJK fijk)
    {
        // Ensure IJK coordinates are in valid range [0, 2]
        var i = Math.Max(0, Math.Min(2, fijk.Coord.I));
        var j = Math.Max(0, Math.Min(2, fijk.Coord.J));
        var k = Math.Max(0, Math.Min(2, fijk.Coord.K));
        
        return FaceIjkBaseCells[fijk.Face, i, j, k].BaseCell;
    }

    /// <summary>
    /// Returns the number of CCW 60° rotations needed to align the face IJK coordinate system
    /// with the canonical orientation of the base cell at that location.
    /// </summary>
    private static int FaceIJKToBaseCellCCWRot60(FaceIJK fijk)
    {
        var i = Math.Max(0, Math.Min(2, fijk.Coord.I));
        var j = Math.Max(0, Math.Min(2, fijk.Coord.J));
        var k = Math.Max(0, Math.Min(2, fijk.Coord.K));

        return FaceIjkBaseCells[fijk.Face, i, j, k].CcwRot60;
    }

    /// <summary>
    /// Returns the home face for a given base cell.
    /// </summary>
    private static int BaseCellToFace(int baseCell)
    {
        if (baseCell < 0 || baseCell >= NumBaseCells)
        {
            throw new ArgumentOutOfRangeException(nameof(baseCell), baseCell,
                $"Base cell must be between 0 and {NumBaseCells - 1}");
        }
        
        return BaseCellDataTable[baseCell].Face;
    }

    /// <summary>
    /// Returns true if the specified face is a CW offset face for the given pentagon base cell.
    /// </summary>
    private static bool BaseCellIsCwOffset(int baseCell, int face)
    {
        var data = BaseCellDataTable[baseCell];
        return data.CwOffsetFace1 == face || data.CwOffsetFace2 == face;
    }

    // ===== Hexagonal Grid Coordinates =====
    
    // Constants for hexagonal grid calculations
    private const double M_SQRT3_2 = 0.8660254037844386;  // sqrt(3)/2
    private const double M_RSIN60 = 1.1547005383792515;   // 1/sin(60°) = 2/sqrt(3)
    private const double M_ONESEVENTH = 0.14285714285714285714285714285714;  // 1/7

    /// <summary>
    /// IJK coordinate structure for H3 hexagonal grid.
    /// H3 uses IJK+ coordinates where i + j + k = 0 (after normalization).
    /// </summary>
    private struct CoordIJK
    {
        public int I;
        public int J;
        public int K;
        
        public CoordIJK(int i, int j, int k)
        {
            I = i;
            J = j;
            K = k;
        }
    }
    
    /// <summary>
    /// Face IJK structure combining face number with IJK coordinates.
    /// </summary>
    private struct FaceIJK
    {
        public int Face;
        public CoordIJK Coord;
        
        public FaceIJK(int face, CoordIJK coord)
        {
            Face = face;
            Coord = coord;
        }
    }
    
    /// <summary>
    /// Overage status for face boundary handling.
    /// </summary>
    private enum Overage
    {
        NoOverage = 0,
        FaceEdge = 1,
        NewFace = 2
    }
    
    /// <summary>
    /// 2D vector structure for hexagonal grid calculations.
    /// </summary>
    private struct Vec2d
    {
        public double X;
        public double Y;
        
        public Vec2d(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Converts face coordinates to hexagonal grid IJK coordinates.
    /// This implements the proper H3 coordinate transformation with aperture-7 hierarchy.
    /// Uses the appropriate down-aperture operation based on resolution class.
    /// Note: At resolution 0, this returns the base cell IJK coordinates directly.
    /// </summary>
    private static (int i, int j) FaceCoordsToHex(double fx, double fy, int resolution)
    {
        // Convert face coordinates to 2D hex coordinates
        var v = new Vec2d(fx, fy);
        
        // Convert to IJK coordinates at resolution 0 scale
        var ijk = Hex2dToCoordIJK(v);
        
        // If we're at resolution 0, we're done - return the base cell IJK
        if (resolution == 0)
        {
            var i0 = ijk.I - ijk.K;
            var j0 = ijk.J - ijk.K;
            return (i0, j0);
        }
        
        // For higher resolutions, we need to scale down using aperture-7 hierarchy
        // Apply down-aperture operations to get to the target resolution
        // Each down-aperture operation moves us one resolution level finer
        for (var r = 1; r <= resolution; r++)
        {
            if (IsResolutionClassIII(r))
            {
                // Class III (odd resolutions) == rotate ccw
                DownAp7(ref ijk);
            }
            else
            {
                // Class II (even resolutions) == rotate cw
                DownAp7r(ref ijk);
            }
        }
        
        // Convert IJK+ to IJ coordinates
        var i = ijk.I - ijk.K;
        var j = ijk.J - ijk.K;
        
        return (i, j);
    }

    /// <summary>
    /// Converts hexagonal grid IJK coordinates back to face coordinates.
    /// This implements the inverse of the coordinate transformation in _geoToHex2d.
    /// 
    /// From H3 reference _hex2dToGeo:
    /// - Start with hex2d coordinates (from _ijkToHex2d)
    /// - Calculate r = magnitude of hex2d vector
    /// - Scale DOWN by M_RSQRT7 for each resolution level
    /// - Scale by RES0_U_GNOMONIC
    /// - Adjust theta for Class III resolutions (add M_AP7_ROT_RADS)
    /// - Apply inverse gnomonic: r = atan(r)
    /// - Calculate azimuth and find point on sphere
    /// 
    /// CRITICAL: The IJK coordinates from ParseH3Index are in an expanded coordinate
    /// system where each DownAp7/DownAp7r operation multiplies coordinates.
    /// We scale down by 1/sqrt(7) per resolution level to get back to face coordinates.
    /// 
    /// NOTE: Face overage handling is NOT needed here because the H3 index already
    /// encodes the correct face through the digit path. The ParseH3Index function
    /// traverses the hierarchy correctly, so the IJK coordinates are already relative
    /// to the correct face.
    /// </summary>
    internal static (int face, double fx, double fy) HexToFaceCoordsWithFace(int baseCellFace, int i, int j, int resolution)
    {
        // Convert IJ to IJK+ coordinates
        var ijk = new CoordIJK(i, j, 0);
        IJKNormalize(ref ijk);
        
        // Convert IJK to 2D hex coordinates
        var vec = IJKToHex2d(ijk);
        
        // Calculate the magnitude (r in polar coordinates)
        var r = Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
        
        // If at origin, return (0, 0)
        if (r < 1e-10)
        {
            return (baseCellFace, 0.0, 0.0);
        }
        
        // Calculate theta (angle)
        var theta = Math.Atan2(vec.Y, vec.X);
        
        // Scale for current resolution length u
        // Each resolution level divides by sqrt(7) in the hex2d space
        for (var res = 0; res < resolution; res++)
        {
            r *= M_RSQRT7;
        }
        
        // Scale by RES0_U_GNOMONIC to get gnomonic projection distance
        r *= RES0_U_GNOMONIC;
        
        // CRITICAL FIX: Adjust theta for Class III resolutions
        // Class III resolutions (odd) are rotated by M_AP7_ROT_RADS relative to Class II
        // From H3 reference _hex2dToGeo: if (!substrate && isResolutionClassIII(res))
        //     theta = _posAngleRads(theta + M_AP7_ROT_RADS);
        if (IsResolutionClassIII(resolution))
        {
            theta = PosAngleRads(theta + M_AP7_ROT_RADS);
        }
        
        // Convert back to cartesian coordinates
        var fx = r * Math.Cos(theta);
        var fy = r * Math.Sin(theta);
        
        return (baseCellFace, fx, fy);
    }
    
    /// <summary>
    /// Finds the normalized IJK coordinates of the hex centered on the indicated
    /// hex at the next finer aperture 3 counter-clockwise resolution. Works in place.
    /// </summary>
    private static void DownAp3(ref CoordIJK ijk)
    {
        // res r unit vectors in res r+1
        var iVec = new CoordIJK(2, 0, 1);
        var jVec = new CoordIJK(1, 2, 0);
        var kVec = new CoordIJK(0, 1, 2);
        
        IJKScale(ref iVec, ijk.I);
        IJKScale(ref jVec, ijk.J);
        IJKScale(ref kVec, ijk.K);
        
        IJKAdd(iVec, jVec, out ijk);
        IJKAdd(ijk, kVec, out ijk);
        
        IJKNormalize(ref ijk);
    }
    
    /// <summary>
    /// Finds the normalized IJK coordinates of the hex centered on the indicated
    /// hex at the next finer aperture 3 clockwise resolution. Works in place.
    /// </summary>
    private static void DownAp3r(ref CoordIJK ijk)
    {
        // res r unit vectors in res r+1
        var iVec = new CoordIJK(2, 1, 0);
        var jVec = new CoordIJK(0, 2, 1);
        var kVec = new CoordIJK(1, 0, 2);
        
        IJKScale(ref iVec, ijk.I);
        IJKScale(ref jVec, ijk.J);
        IJKScale(ref kVec, ijk.K);
        
        IJKAdd(iVec, jVec, out ijk);
        IJKAdd(ijk, kVec, out ijk);
        
        IJKNormalize(ref ijk);
    }

    /// <summary>
    /// Determines the containing hex in IJK+ coordinates for a 2D cartesian coordinate vector.
    /// This is the core H3 coordinate transformation from the reference implementation.
    /// </summary>
    private static CoordIJK Hex2dToCoordIJK(Vec2d v)
    {
        var h = new CoordIJK(0, 0, 0);
        
        var a1 = Math.Abs(v.X);
        var a2 = Math.Abs(v.Y);
        
        // First do a reverse conversion
        var x2 = a2 * M_RSIN60;
        var x1 = a1 + x2 / 2.0;
        
        // Check if we have the center of a hex
        var m1 = (int)x1;
        var m2 = (int)x2;
        
        // Otherwise round correctly
        var r1 = x1 - m1;
        var r2 = x2 - m2;
        
        if (r1 < 0.5)
        {
            if (r1 < 1.0 / 3.0)
            {
                if (r2 < (1.0 + r1) / 2.0)
                {
                    h.I = m1;
                    h.J = m2;
                }
                else
                {
                    h.I = m1;
                    h.J = m2 + 1;
                }
            }
            else
            {
                if (r2 < (1.0 - r1))
                {
                    h.J = m2;
                }
                else
                {
                    h.J = m2 + 1;
                }
                
                if ((1.0 - r1) <= r2 && r2 < (2.0 * r1))
                {
                    h.I = m1 + 1;
                }
                else
                {
                    h.I = m1;
                }
            }
        }
        else
        {
            if (r1 < 2.0 / 3.0)
            {
                if (r2 < (1.0 - r1))
                {
                    h.J = m2;
                }
                else
                {
                    h.J = m2 + 1;
                }
                
                if ((2.0 * r1 - 1.0) < r2 && r2 < (1.0 - r1))
                {
                    h.I = m1;
                }
                else
                {
                    h.I = m1 + 1;
                }
            }
            else
            {
                if (r2 < (r1 / 2.0))
                {
                    h.I = m1 + 1;
                    h.J = m2;
                }
                else
                {
                    h.I = m1 + 1;
                    h.J = m2 + 1;
                }
            }
        }
        
        // Now fold across the axes if necessary
        if (v.X < 0.0)
        {
            if ((h.J % 2) == 0)  // even
            {
                var axisi = h.J / 2;
                var diff = h.I - axisi;
                h.I = (int)(h.I - 2.0 * diff);
            }
            else
            {
                var axisi = (h.J + 1) / 2;
                var diff = h.I - axisi;
                h.I = (int)(h.I - (2.0 * diff + 1));
            }
        }
        
        if (v.Y < 0.0)
        {
            h.I = h.I - (2 * h.J + 1) / 2;
            h.J = -1 * h.J;
        }
        
        IJKNormalize(ref h);
        return h;
    }

    /// <summary>
    /// Finds the center point in 2D cartesian coordinates of a hex.
    /// </summary>
    private static Vec2d IJKToHex2d(CoordIJK h)
    {
        var i = h.I - h.K;
        var j = h.J - h.K;
        
        return new Vec2d(i - 0.5 * j, j * M_SQRT3_2);
    }

    /// <summary>
    /// Normalizes IJK coordinates by setting the components to the smallest possible values.
    /// Works in place. H3 uses the constraint that i + j + k = 0 after normalization.
    /// </summary>
    private static void IJKNormalize(ref CoordIJK c)
    {
        // Remove any negative values
        if (c.I < 0)
        {
            c.J -= c.I;
            c.K -= c.I;
            c.I = 0;
        }
        
        if (c.J < 0)
        {
            c.I -= c.J;
            c.K -= c.J;
            c.J = 0;
        }
        
        if (c.K < 0)
        {
            c.I -= c.K;
            c.J -= c.K;
            c.K = 0;
        }
        
        // Remove the min value if needed
        var min = Math.Min(c.I, Math.Min(c.J, c.K));
        if (min > 0)
        {
            c.I -= min;
            c.J -= min;
            c.K -= min;
        }
    }

    /// <summary>
    /// Finds the normalized IJK coordinates of the indexing parent of a cell in a
    /// counter-clockwise aperture 7 grid. Works in place.
    /// </summary>
    private static void UpAp7(ref CoordIJK ijk)
    {
        // Convert to CoordIJ
        var i = ijk.I - ijk.K;
        var j = ijk.J - ijk.K;
        
        ijk.I = (int)Math.Round((3 * i - j) * M_ONESEVENTH);
        ijk.J = (int)Math.Round((i + 2 * j) * M_ONESEVENTH);
        ijk.K = 0;
        IJKNormalize(ref ijk);
    }

    /// <summary>
    /// Finds the normalized IJK coordinates of the hex centered on the indicated
    /// hex at the next finer aperture 7 counter-clockwise resolution. Works in place.
    /// </summary>
    private static void DownAp7(ref CoordIJK ijk)
    {
        // res r unit vectors in res r+1
        var iVec = new CoordIJK(3, 0, 1);
        var jVec = new CoordIJK(1, 3, 0);
        var kVec = new CoordIJK(0, 1, 3);
        
        IJKScale(ref iVec, ijk.I);
        IJKScale(ref jVec, ijk.J);
        IJKScale(ref kVec, ijk.K);
        
        IJKAdd(iVec, jVec, out ijk);
        IJKAdd(ijk, kVec, out ijk);
        
        IJKNormalize(ref ijk);
    }

    /// <summary>
    /// Uniformly scales IJK coordinates by a scalar. Works in place.
    /// </summary>
    private static void IJKScale(ref CoordIJK c, int factor)
    {
        c.I *= factor;
        c.J *= factor;
        c.K *= factor;
    }

    /// <summary>
    /// Adds two IJK coordinates.
    /// </summary>
    private static void IJKAdd(CoordIJK h1, CoordIJK h2, out CoordIJK sum)
    {
        sum = new CoordIJK(h1.I + h2.I, h1.J + h2.J, h1.K + h2.K);
    }

    // ===== Neighbor Calculation =====
    
    /// <summary>
    /// Unit vectors for the 6 hexagonal directions in IJK coordinates.
    /// Direction: 0=center, 1=K, 2=J, 3=JK, 4=I, 5=IK, 6=IJ
    /// </summary>
    private static readonly CoordIJK[] UnitVectors = new[]
    {
        new CoordIJK(0, 0, 0),  // 0: CENTER_DIGIT
        new CoordIJK(0, 0, 1),  // 1: K_AXES_DIGIT
        new CoordIJK(0, 1, 0),  // 2: J_AXES_DIGIT
        new CoordIJK(0, 1, 1),  // 3: JK_AXES_DIGIT
        new CoordIJK(1, 0, 0),  // 4: I_AXES_DIGIT
        new CoordIJK(1, 0, 1),  // 5: IK_AXES_DIGIT
        new CoordIJK(1, 1, 0),  // 6: IJ_AXES_DIGIT
    };

    /// <summary>
    /// Converts a unit IJK vector to its corresponding digit (0-6).
    /// Based on H3 reference implementation's _unitIjkToDigit.
    /// </summary>
    private static int UnitIJKToDigit(CoordIJK ijk)
    {
        // Match against the unit vectors
        for (var d = 0; d < UnitVectors.Length; d++)
        {
            if (ijk.I == UnitVectors[d].I && 
                ijk.J == UnitVectors[d].J && 
                ijk.K == UnitVectors[d].K)
            {
                return d;
            }
        }
        
        // If no match found, return center digit
        return 0;
    }

    private static (int i, int j) GetNeighborCoords(int i, int j, int direction)
    {
        // Convert IJ to IJK+
        var ijk = new CoordIJK(i, j, 0);
        IJKNormalize(ref ijk);
        
        // Add the unit vector for the direction (skip center, use 1-6)
        if (direction >= 0 && direction < 6)
        {
            var unitVec = UnitVectors[direction + 1];  // +1 to skip center
            IJKAdd(ijk, unitVec, out ijk);
            IJKNormalize(ref ijk);
        }
        
        // Convert back to IJ
        var ni = ijk.I - ijk.K;
        var nj = ijk.J - ijk.K;
        
        return (ni, nj);
    }

    // ===== Pentagon Handling =====

    private static bool IsPentagon(int baseCell)
    {
        return PentagonBaseCells.Contains(baseCell);
    }

    // ===== Vertex Calculation =====

    /// <summary>
    /// Calculates the coordinates of a vertex for an H3 cell.
    /// Based on the H3 reference implementation's cellToBoundary function.
    /// </summary>
    /// <param name="face">The icosahedron face</param>
    /// <param name="i">The I coordinate</param>
    /// <param name="j">The J coordinate</param>
    /// <param name="resolution">The H3 resolution</param>
    /// <param name="angle">The angle of the vertex in radians</param>
    /// <param name="isPentagon">Whether this is a pentagon cell</param>
    /// <returns>Tuple of (face, fx, fy) coordinates for the vertex</returns>
    private static (int face, double fx, double fy) GetVertexCoords(int face, int i, int j, int resolution, double angle, bool isPentagon)
    {
        // Convert IJ to IJK coordinates
        var ijk = new CoordIJK(i, j, 0);
        IJKNormalize(ref ijk);
        
        // Convert IJK to hex2d coordinates (cell center)
        var centerHex2d = IJKToHex2d(ijk);
        
        // Calculate the vertex radius in hex2d space
        // In H3's hex2d coordinate system, cells at each resolution have a specific size
        // The hex2d space is already scaled for the resolution, so we just need the
        // geometric offset from center to vertex
        
        // For a regular hexagon in hex2d space, the distance from center to vertex
        // is 1.0 (this is the circumradius in the hex2d coordinate system)
        // For pentagons, we use a similar unit distance
        var radius = 1.0;
        
        // Calculate vertex position in hex2d space
        var vx = centerHex2d.X + radius * Math.Cos(angle);
        var vy = centerHex2d.Y + radius * Math.Sin(angle);
        
        // Convert hex2d coordinates back to face coordinates
        // This is the inverse of Hex2dToCoordIJK followed by IJKToHex2d
        // We need to convert from hex2d space to gnomonic face coordinates
        
        // Calculate polar coordinates in hex2d space
        var hex2dR = Math.Sqrt(vx * vx + vy * vy);
        if (hex2dR < 1e-10)
        {
            return (face, 0.0, 0.0);
        }
        
        var hex2dTheta = Math.Atan2(vy, vx);
        
        // Convert hex2d distance to gnomonic distance
        // The hex2d space is scaled by resolution, so we need to scale back
        // Start with the base resolution 0 scaling
        var gnomonicR = hex2dR * RES0_U_GNOMONIC;
        
        // Scale down for each resolution level
        for (var res = 0; res < resolution; res++)
        {
            gnomonicR *= M_RSQRT7;  // Scale down by 1/sqrt(7) for each resolution level
        }
        
        // Adjust theta for Class III resolutions
        var gnomonicTheta = hex2dTheta;
        if (IsResolutionClassIII(resolution))
        {
            gnomonicTheta = PosAngleRads(gnomonicTheta + M_AP7_ROT_RADS);
        }
        
        // Convert back to Cartesian face coordinates
        var fx = gnomonicR * Math.Cos(gnomonicTheta);
        var fy = gnomonicR * Math.Sin(gnomonicTheta);
        
        return (face, fx, fy);
    }

    // ===== H3 Index Encoding/Decoding =====
    
    // H3 index constants matching the reference implementation
    private const int MaxH3Resolution = 15;
    private const int H3PerDigitOffset = 3;  // 3 bits per digit
    private const int H3ModeOffset = 59;
    private const int H3ResOffset = 52;
    private const int H3BcOffset = 45;
    private const int H3ReservedOffset = 56;
    private const ulong H3DigitMask = 0x7UL;  // 3 bits: 0b111
    
    /// <summary>
    /// H3 index with mode 0, res 0, base cell 0, and 7 for all index digits.
    /// Typically used to initialize the creation of an H3 cell index, which
    /// expects all direction digits to be 7 beyond the cell's resolution.
    /// </summary>
    private const ulong H3Init = 35184372088831UL;

    /// <summary>
    /// Builds an H3 index from lat/lon coordinates.
    /// This follows the H3 reference implementation (_geoToFaceIjk -> _faceIjkToH3).
    /// 
    /// The proper approach from H3:
    /// 1. Convert lat/lon to FaceIJK at the TARGET resolution using _geoToHex2d
    /// 2. Work BACKWARDS from target resolution to resolution 0 using _faceIjkToH3
    /// 3. At each step, apply up-aperture and calculate the digit
    /// </summary>
    private static ulong BuildH3IndexFromFaceCoords(int baseCell, int face, double fx, double fy, int resolution)
    {
        // Start with H3_INIT which has all digits set to 7
        ulong index = H3Init;
        
        // Set mode to 1 (cell mode)
        index = (index & ~(0xFUL << H3ModeOffset)) | (1UL << H3ModeOffset);
        
        // Set resolution
        index = (index & ~(0xFUL << H3ResOffset)) | ((ulong)resolution << H3ResOffset);
        
        // If resolution is 0, just set base cell and return
        if (resolution == 0)
        {
            index = (index & ~(0x7FUL << H3BcOffset)) | ((ulong)baseCell << H3BcOffset);
            index = index & ~(0x7UL << H3ReservedOffset);
            return index;
        }
        
        // Convert gnomonic face coordinates (fx, fy) to hex2d at the target resolution
        // Following H3's _geoToHex2d logic:
        // 1. r and theta are already in gnomonic projection (fx, fy are Cartesian from polar)
        // 2. Scale r for resolution: r *= INV_RES0_U_GNOMONIC; for (i=0; i<res; i++) r *= M_SQRT7
        // 3. Adjust theta for Class III
        // 4. Convert back to Cartesian: v.x = r * cos(theta), v.y = r * sin(theta)
        
        // Convert Cartesian back to polar
        var r = Math.Sqrt(fx * fx + fy * fy);
        var theta = Math.Atan2(fy, fx);
        
        // Scale r for hex2d coordinate system and resolution
        r *= INV_RES0_U_GNOMONIC;
        for (var i = 0; i < resolution; i++)
        {
            r *= M_SQRT7;
        }
        
        // Adjust theta for Class III resolutions (odd resolutions are rotated)
        if (IsResolutionClassIII(resolution))
        {
            theta -= M_AP7_ROT_RADS;
        }
        
        // Convert back to Cartesian hex2d coordinates
        var vx = r * Math.Cos(theta);
        var vy = r * Math.Sin(theta);
        
        // Convert hex2d to IJK at target resolution
        var v = new Vec2d(vx, vy);
        var fijk = new FaceIJK(face, Hex2dToCoordIJK(v));
        
        // Build the H3 index from finest resolution up
        // Work backwards from resolution-1 to 0
        var ijk = fijk.Coord;
        for (var res = resolution - 1; res >= 0; res--)
        {
            var lastIJK = ijk;
            CoordIJK lastCenter;
            
            if (IsResolutionClassIII(res + 1))
            {
                // Class III: rotate ccw (up-aperture 7)
                UpAp7(ref ijk);
                lastCenter = ijk;
                DownAp7(ref lastCenter);
            }
            else
            {
                // Class II: rotate cw (up-aperture 7 reverse)
                UpAp7r(ref ijk);
                lastCenter = ijk;
                DownAp7r(ref lastCenter);
            }
            
            // Calculate difference between child and parent center
            var diff = new CoordIJK(
                lastIJK.I - lastCenter.I,
                lastIJK.J - lastCenter.J,
                lastIJK.K - lastCenter.K
            );
            IJKNormalize(ref diff);
            
            // Convert difference to digit
            var digit = UnitIJKToDigit(diff);
            
            // Store digit in index
            var digitOffset = (MaxH3Resolution - (res + 1)) * H3PerDigitOffset;
            index = (index & ~(H3DigitMask << digitOffset)) | ((ulong)digit << digitOffset);
        }
        
        // Now ijk should hold the IJK of the base cell
        // Look up the base cell from FaceIJK
        var baseCellFijk = new FaceIJK(face, ijk);
        var actualBaseCell = FaceIJKToBaseCell(baseCellFijk);
        
        // Set base cell
        index = (index & ~(0x7FUL << H3BcOffset)) | ((ulong)actualBaseCell << H3BcOffset);
        
        // Set reserved bits to 0
        index = index & ~(0x7UL << H3ReservedOffset);
        
        // TODO: Handle pentagon rotation if needed
        
        return index;
    }
    
    /// <summary>
    /// Builds an H3 index from FaceIJK coordinates at the target resolution.
    /// This is the proper H3 encoding algorithm from the reference implementation.
    /// Based on H3 reference _faceIjkToH3 function.
    /// </summary>
    /// <param name="fijk">FaceIJK coordinates at target resolution</param>
    /// <param name="resolution">Target resolution (0-15)</param>
    /// <returns>H3 index as ulong</returns>
    private static ulong BuildH3IndexFromFaceIJK(FaceIJK fijk, int resolution)
    {
        // Start with H3_INIT which has all digits set to 7
        ulong index = H3Init;
        
        // Set mode to 1 (cell mode)
        index = (index & ~(0xFUL << H3ModeOffset)) | (1UL << H3ModeOffset);
        
        // Set resolution
        index = (index & ~(0xFUL << H3ResOffset)) | ((ulong)resolution << H3ResOffset);
        
        // Work backwards from target resolution to resolution 0
        // extracting digits along the way
        var ijk = fijk.Coord;
        
        for (var res = resolution - 1; res >= 0; res--)
        {
            var lastIJK = ijk;
            CoordIJK lastCenter;
            
            // Apply up-aperture based on resolution class
            if (IsResolutionClassIII(res + 1))
            {
                // Class III: rotate counter-clockwise (up-aperture 7)
                UpAp7(ref ijk);
                lastCenter = ijk;
                DownAp7(ref lastCenter);
            }
            else
            {
                // Class II: rotate clockwise (up-aperture 7 reverse)
                UpAp7r(ref ijk);
                lastCenter = ijk;
                DownAp7r(ref lastCenter);
            }
            
            // Calculate difference between child and parent center
            var diff = new CoordIJK(
                lastIJK.I - lastCenter.I,
                lastIJK.J - lastCenter.J,
                lastIJK.K - lastCenter.K
            );
            IJKNormalize(ref diff);
            
            // Convert difference to digit (0-6)
            var digit = UnitIJKToDigit(diff);
            
            // Store digit in the H3 index
            var digitOffset = (MaxH3Resolution - (res + 1)) * H3PerDigitOffset;
            index = (index & ~(H3DigitMask << digitOffset)) | ((ulong)digit << digitOffset);
        }
        
        // After working backwards, ijk now holds the base cell IJK coordinates
        // Look up the base cell from FaceIJK
        var baseCellFijk = new FaceIJK(fijk.Face, ijk);
        var baseCell = FaceIJKToBaseCell(baseCellFijk);
        
        // Set base cell in the index
        index = (index & ~(0x7FUL << H3BcOffset)) | ((ulong)baseCell << H3BcOffset);
        
        // Set reserved bits to 0
        index = index & ~(0x7UL << H3ReservedOffset);
        
        // Rotate into canonical orientation for this base cell
        var numRots = FaceIJKToBaseCellCCWRot60(baseCellFijk);
        var isPentagon = IsPentagon(baseCell);

        if (isPentagon)
        {
            // If the leading non-zero digit is 1, rotate around the pentagon's missing sequence
            if (GetLeadingNonZeroDigit(index, resolution) == 1)
            {
                index = BaseCellIsCwOffset(baseCell, baseCellFijk.Face)
                    ? RotateH3Index60cw(index, resolution)
                    : RotateH3Index60ccw(index, resolution);
            }

            for (var i = 0; i < numRots; i++)
            {
                index = RotatePentagon60ccw(index, resolution);
            }
        }
        else
        {
            for (var i = 0; i < numRots; i++)
            {
                index = RotateH3Index60ccw(index, resolution);
            }
        }
        
        return index;
    }

    /// <summary>
    /// Parses an H3 index to extract the base cell, resolution, face, and IJK coordinates.
    /// This implements the proper H3 index decoding by traversing the digit path
    /// and handling face overage.
    /// Based on the H3 reference implementation's _h3ToFaceIjk function.
    /// </summary>
    private static (int baseCell, int resolution, int face, int i, int j) ParseH3IndexWithFace(ulong index)
    {
        // Extract resolution and base cell from the index
        var resolution = (int)((index >> H3ResOffset) & 0xF);
        var baseCell = (int)((index >> H3BcOffset) & 0x7F);
        
        // Validate base cell
        if (baseCell < 0 || baseCell >= NumBaseCells)
        {
            throw new ArgumentException($"Invalid base cell {baseCell} in H3 index", nameof(index));
        }
        
        // Handle pentagonal missing sequence adjustment
        // If this is a pentagon base cell and the leading non-zero digit is 5,
        // we need to rotate the index 60 degrees clockwise
        var isPentagon = IsPentagon(baseCell);
        if (isPentagon && GetLeadingNonZeroDigit(index, resolution) == 5)
        {
            index = RotateH3Index60cw(index, resolution);
        }
        
        // Start with the base cell's home FaceIJK
        var baseCellData = BaseCellDataTable[baseCell];
        var fijk = new FaceIJK(baseCellData.Face, new CoordIJK(baseCellData.I, baseCellData.J, baseCellData.K));
        
        // If resolution is 0, we're at the base cell level
        if (resolution == 0)
        {
            var i0 = fijk.Coord.I - fijk.Coord.K;
            var j0 = fijk.Coord.J - fijk.Coord.K;
            return (baseCell, resolution, fijk.Face, i0, j0);
        }
        
        // Check if overage is possible
        // Center base cell hierarchy is entirely on this face (no overage possible)
        // This check is done BEFORE traversing the digit path
        var baseCellIsCenter = (baseCellData.I == 0 && baseCellData.J == 0 && baseCellData.K == 0);
        var possibleOverage = isPentagon || !baseCellIsCenter;
        
        // Traverse the digit path from resolution 1 to target resolution
        // H3 alternates between Class II (rotate CW) and Class III (rotate CCW) resolutions
        for (var r = 1; r <= resolution; r++)
        {
            // Get the digit for this resolution level
            var digitOffset = (MaxH3Resolution - r) * H3PerDigitOffset;
            var digit = (int)((index >> digitOffset) & H3DigitMask);
            
            // Apply the appropriate down-aperture operation based on resolution class
            if (IsResolutionClassIII(r))
            {
                // Class III: rotate counter-clockwise
                DownAp7(ref fijk.Coord);
            }
            else
            {
                // Class II: rotate clockwise
                DownAp7r(ref fijk.Coord);
            }
            
            // Apply the neighbor offset for this digit
            Neighbor(ref fijk.Coord, digit);
        }
        
        // Handle face overage if possible
        if (possibleOverage)
        {
            var origIJK = fijk.Coord;
            var adjRes = resolution;
            
            // If we're in Class III, drop into the next finer Class II grid
            if (IsResolutionClassIII(resolution))
            {
                DownAp7r(ref fijk.Coord);
                adjRes++;
            }
            
            // Adjust for overage if needed
            // A pentagon base cell with a leading 4 digit requires special handling
            var pentLeading4 = isPentagon && GetLeadingNonZeroDigit(index, resolution) == 4;
            var overage = AdjustOverageClassII(ref fijk, adjRes, pentLeading4, false);
            
            if (overage != Overage.NoOverage)
            {
                // If the base cell is a pentagon we have the potential for secondary overages
                if (isPentagon)
                {
                    while (AdjustOverageClassII(ref fijk, adjRes, false, false) != Overage.NoOverage)
                    {
                        // Keep adjusting until no more overage
                    }
                }
                
                // If we changed resolution, go back up
                if (adjRes != resolution)
                {
                    UpAp7r(ref fijk.Coord);
                }
            }
            else if (adjRes != resolution)
            {
                // No overage but we changed resolution, restore original
                fijk.Coord = origIJK;
            }
        }
        
        // Convert IJK+ to IJ coordinates
        var i = fijk.Coord.I - fijk.Coord.K;
        var j = fijk.Coord.J - fijk.Coord.K;
        
        return (baseCell, resolution, fijk.Face, i, j);
    }
    
    /// <summary>
    /// Determines if a resolution is Class III (odd resolutions).
    /// Class III resolutions use counter-clockwise rotation (DownAp7).
    /// Class II resolutions (even) use clockwise rotation (DownAp7r).
    /// </summary>
    private static bool IsResolutionClassIII(int resolution)
    {
        return (resolution % 2) == 1;
    }
    
    /// <summary>
    /// Gets the leading (first) non-zero digit from an H3 index.
    /// Used for pentagon handling.
    /// </summary>
    private static int GetLeadingNonZeroDigit(ulong index, int resolution)
    {
        for (var r = 1; r <= resolution; r++)
        {
            var digitOffset = (MaxH3Resolution - r) * H3PerDigitOffset;
            var digit = (int)((index >> digitOffset) & H3DigitMask);
            if (digit != 0)
            {
                return digit;
            }
        }
        return 0;
    }
    
    /// <summary>
    /// Rotates an H3 index 60 degrees clockwise.
    /// Used for pentagon handling when the leading non-zero digit is 5.
    /// </summary>
    private static ulong RotateH3Index60cw(ulong index, int resolution)
    {
        var newIndex = index;
        
        for (var r = 1; r <= resolution; r++)
        {
            var digitOffset = (MaxH3Resolution - r) * H3PerDigitOffset;
            var digit = (int)((index >> digitOffset) & H3DigitMask);
            
            // Rotate the digit 60 degrees clockwise
            // Digit rotation: 1->5, 2->1, 3->2, 4->3, 5->4, 6->6 (unchanged)
            var rotatedDigit = digit switch
            {
                1 => 5,
                2 => 1,
                3 => 2,
                4 => 3,
                5 => 4,
                6 => 6,
                _ => 0
            };
            
            // Clear the old digit and set the new one
            newIndex = (newIndex & ~(H3DigitMask << digitOffset)) | ((ulong)rotatedDigit << digitOffset);
        }
        
        return newIndex;
    }

    /// <summary>
    /// Rotates an H3 index 60 degrees counter-clockwise (hexagons).
    /// </summary>
    private static ulong RotateH3Index60ccw(ulong index, int resolution)
    {
        var newIndex = index;

        for (var r = 1; r <= resolution; r++)
        {
            var digitOffset = (MaxH3Resolution - r) * H3PerDigitOffset;
            var digit = (int)((newIndex >> digitOffset) & H3DigitMask);

            var rotatedDigit = digit switch
            {
                1 => 2,
                2 => 3,
                3 => 4,
                4 => 5,
                5 => 1,
                6 => 6,
                _ => 0
            };

            newIndex = (newIndex & ~(H3DigitMask << digitOffset)) | ((ulong)rotatedDigit << digitOffset);
        }

        return newIndex;
    }

    /// <summary>
    /// Rotates an H3 pentagon index 60 degrees counter-clockwise, handling the missing k-axis.
    /// </summary>
    private static ulong RotatePentagon60ccw(ulong index, int resolution)
    {
        var newIndex = index;
        var foundFirstNonZero = false;

        for (var r = 1; r <= resolution; r++)
        {
            var digitOffset = (MaxH3Resolution - r) * H3PerDigitOffset;
            var digit = (int)((newIndex >> digitOffset) & H3DigitMask);

            digit = digit switch
            {
                1 => 2,
                2 => 3,
                3 => 4,
                4 => 5,
                5 => 1,
                6 => 6,
                _ => 0
            };

            newIndex = (newIndex & ~(H3DigitMask << digitOffset)) | ((ulong)digit << digitOffset);

            if (!foundFirstNonZero && digit != 0)
            {
                foundFirstNonZero = true;

                // Adjust for deleted k-axis sequence
                if (GetLeadingNonZeroDigit(newIndex, resolution) == 1)
                {
                    newIndex = RotateH3Index60ccw(newIndex, resolution);
                }
            }
        }

        return newIndex;
    }
    
    /// <summary>
    /// Finds the normalized IJK coordinates of the hex centered on the indicated
    /// hex at the next finer aperture 7 clockwise resolution. Works in place.
    /// This is the Class II version (rotate clockwise).
    /// </summary>
    private static void DownAp7r(ref CoordIJK ijk)
    {
        // res r unit vectors in res r+1 (clockwise rotation)
        var iVec = new CoordIJK(3, 1, 0);
        var jVec = new CoordIJK(0, 3, 1);
        var kVec = new CoordIJK(1, 0, 3);
        
        IJKScale(ref iVec, ijk.I);
        IJKScale(ref jVec, ijk.J);
        IJKScale(ref kVec, ijk.K);
        
        IJKAdd(iVec, jVec, out ijk);
        IJKAdd(ijk, kVec, out ijk);
        
        IJKNormalize(ref ijk);
    }
    
    /// <summary>
    /// Finds the normalized IJK coordinates of the indexing parent of a cell in a
    /// clockwise aperture 7 grid. Works in place.
    /// This is the inverse of DownAp7r.
    /// </summary>
    private static void UpAp7r(ref CoordIJK ijk)
    {
        // Convert to CoordIJ
        var i = ijk.I - ijk.K;
        var j = ijk.J - ijk.K;
        
        ijk.I = (int)Math.Round((2 * i + j) * M_ONESEVENTH);
        ijk.J = (int)Math.Round((3 * j - i) * M_ONESEVENTH);
        ijk.K = 0;
        IJKNormalize(ref ijk);
    }
    
    /// <summary>
    /// Rotates IJK coordinates 60 degrees clockwise.
    /// From H3 reference implementation _ijkRotate60cw.
    /// </summary>
    private static void IJKRotate60cw(ref CoordIJK ijk)
    {
        // Unit vector rotations from H3 reference
        var iVec = new CoordIJK(1, 0, 1);
        var jVec = new CoordIJK(1, 1, 0);
        var kVec = new CoordIJK(0, 1, 1);
        
        IJKScale(ref iVec, ijk.I);
        IJKScale(ref jVec, ijk.J);
        IJKScale(ref kVec, ijk.K);
        
        IJKAdd(iVec, jVec, out ijk);
        IJKAdd(ijk, kVec, out ijk);
        
        IJKNormalize(ref ijk);
    }
    
    /// <summary>
    /// Moves the IJK coordinates one step in the specified direction.
    /// Direction is a digit from 0-6 representing the hexagonal direction.
    /// 0 = center (no movement), 1-6 = the six hexagonal directions.
    /// </summary>
    private static void Neighbor(ref CoordIJK ijk, int digit)
    {
        if (digit > 0 && digit < 7)
        {
            IJKAdd(ijk, UnitVectors[digit], out ijk);
            IJKNormalize(ref ijk);
        }
        // digit == 0 means center, no movement
    }
    
    /// <summary>
    /// Rotates IJK coordinates 60 degrees counter-clockwise.
    /// From H3 reference implementation _ijkRotate60ccw.
    /// </summary>
    private static void IJKRotate60ccw(ref CoordIJK ijk)
    {
        // Unit vector rotations from H3 reference
        var iVec = new CoordIJK(1, 1, 0);
        var jVec = new CoordIJK(0, 1, 1);
        var kVec = new CoordIJK(1, 0, 1);
        
        IJKScale(ref iVec, ijk.I);
        IJKScale(ref jVec, ijk.J);
        IJKScale(ref kVec, ijk.K);
        
        IJKAdd(iVec, jVec, out ijk);
        IJKAdd(ijk, kVec, out ijk);
        
        IJKNormalize(ref ijk);
    }
    
    /// <summary>
    /// Adjusts a FaceIJK address in place so that the resulting cell address is
    /// relative to the correct icosahedral face.
    /// This is the critical function for handling face overage (cells that cross face boundaries).
    /// From H3 reference implementation _adjustOverageClassII.
    /// </summary>
    private static Overage AdjustOverageClassII(ref FaceIJK fijk, int res, bool pentLeading4, bool substrate)
    {
        var overage = Overage.NoOverage;
        
        // Get the maximum dimension value; scale if a substrate grid
        var maxDim = MaxDimByCIIRes[res];
        if (substrate)
        {
            maxDim *= 3;
        }
        
        var sum = fijk.Coord.I + fijk.Coord.J + fijk.Coord.K;
        
        // Check for overage
        if (substrate && sum == maxDim)  // on edge
        {
            overage = Overage.FaceEdge;
        }
        else if (sum > maxDim)  // overage
        {
            overage = Overage.NewFace;
            
            FaceOrientIJK fijkOrient;
            if (fijk.Coord.K > 0)
            {
                if (fijk.Coord.J > 0)  // jk "quadrant"
                {
                    fijkOrient = FaceNeighbors[fijk.Face][JK];
                }
                else  // ik "quadrant"
                {
                    fijkOrient = FaceNeighbors[fijk.Face][KI];
                    
                    // Adjust for the pentagonal missing sequence
                    if (pentLeading4)
                    {
                        // Translate origin to center of pentagon
                        var origin = new CoordIJK(maxDim, 0, 0);
                        var tmp = new CoordIJK(
                            fijk.Coord.I - origin.I,
                            fijk.Coord.J - origin.J,
                            fijk.Coord.K - origin.K
                        );
                        
                        // Rotate to adjust for the missing sequence
                        // CRITICAL FIX: H3 reference uses _ijkRotate60cw, not ccw
                        IJKRotate60cw(ref tmp);
                        
                        // Translate the origin back to the center of the triangle
                        fijk.Coord.I = tmp.I + origin.I;
                        fijk.Coord.J = tmp.J + origin.J;
                        fijk.Coord.K = tmp.K + origin.K;
                    }
                }
            }
            else  // ij "quadrant"
            {
                fijkOrient = FaceNeighbors[fijk.Face][IJ];
            }
            
            fijk.Face = fijkOrient.Face;
            
            // Rotate and translate for adjacent face
            for (var i = 0; i < fijkOrient.CcwRot60; i++)
            {
                IJKRotate60ccw(ref fijk.Coord);
            }
            
            var transVec = fijkOrient.Translate;
            var unitScale = UnitScaleByCIIRes[res];
            if (substrate)
            {
                unitScale *= 3;
            }
            IJKScale(ref transVec, unitScale);
            
            fijk.Coord.I += transVec.I;
            fijk.Coord.J += transVec.J;
            fijk.Coord.K += transVec.K;
            IJKNormalize(ref fijk.Coord);
            
            // Overage points on pentagon boundaries can end up on edges
            if (substrate && fijk.Coord.I + fijk.Coord.J + fijk.Coord.K == maxDim)
            {
                overage = Overage.FaceEdge;
            }
        }
        
        return overage;
    }

    private static string H3IndexToString(ulong index)
    {
        // H3 indices don't have leading zeros
        // Format as hex without padding
        return index.ToString("x");
    }

    private static ulong StringToH3Index(string h3Index)
    {
        // H3 indices can be 15 or 16 characters (leading zero may be omitted)
        if (h3Index.Length < 1 || h3Index.Length > 16)
        {
            throw new ArgumentException(
                $"Invalid H3 index '{h3Index}'. H3 indices must be 1-16 character hexadecimal strings.",
                nameof(h3Index));
        }

        try
        {
            return Convert.ToUInt64(h3Index, 16);
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                $"Invalid H3 index '{h3Index}'. H3 indices must contain only hexadecimal characters (0-9, a-f).",
                nameof(h3Index));
        }
    }

    // ===== H3 Encoding Helper Methods (from reference implementation) =====
    
    /// <summary>
    /// Finds the closest icosahedron face to a geographic point.
    /// Based on H3 reference implementation's _geoToClosestFace.
    /// </summary>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="longitude">Longitude in degrees</param>
    /// <returns>Tuple of (face index, squared distance to face center)</returns>
    private static (int face, double sqd) GeoToClosestFace(double latitude, double longitude)
    {
        // Convert to 3D point on unit sphere
        var (x, y, z) = LatLonToXYZ(latitude, longitude);
        
        var closestFace = 0;
        var minSqd = 5.0; // Maximum possible squared distance is 4.0
        
        for (var face = 0; face < NumIcosaFaces; face++)
        {
            var faceCenter = FaceCenterPoints[face];
            var sqd = PointSquareDistance(x, y, z, faceCenter.x, faceCenter.y, faceCenter.z);
            
            if (sqd < minSqd)
            {
                closestFace = face;
                minSqd = sqd;
            }
        }
        
        return (closestFace, minSqd);
    }
    
    /// <summary>
    /// Converts hex2d coordinates back to geographic coordinates.
    /// This is the inverse of GeoToHex2d.
    /// Based on H3 reference _hex2dToGeo function.
    /// </summary>
    /// <param name="hex2d">Hex2d coordinates</param>
    /// <param name="face">Icosahedron face index</param>
    /// <param name="resolution">H3 resolution</param>
    /// <returns>Tuple of (latitude, longitude) in degrees</returns>
    private static (double latitude, double longitude) Hex2dToGeo(Vec2d hex2d, int face, int resolution)
    {
        // Calculate (r, theta) in hex2d
        var r = Math.Sqrt(hex2d.X * hex2d.X + hex2d.Y * hex2d.Y);
        
        // If at origin, return face center
        if (r < 1e-10)
        {
            var faceCenter = FaceCenterGeo[face];
            return (RadiansToDegrees(faceCenter.lat), RadiansToDegrees(faceCenter.lon));
        }
        
        var theta = Math.Atan2(hex2d.Y, hex2d.X);
        
        // Scale r down for resolution (inverse of encoding)
        for (var i = 0; i < resolution; i++)
        {
            r *= M_RSQRT7;
        }
        
        // Scale by RES0_U_GNOMONIC
        r *= RES0_U_GNOMONIC;
        
        // Perform inverse gnomonic projection
        r = Math.Atan(r);
        
        // Adjust theta for Class III resolutions (inverse of encoding)
        if (IsResolutionClassIII(resolution))
        {
            theta = PosAngleRads(theta + M_AP7_ROT_RADS);
        }
        
        // Convert theta to azimuth
        theta = PosAngleRads(FaceAxesAzRadsCII[face][0] - theta);
        
        // Get face center
        var faceCenterGeo = FaceCenterGeo[face];
        
        // Find the point at (r, theta) from face center using spherical geometry
        var (lat, lon) = GeoAzDistanceRads(faceCenterGeo.lat, faceCenterGeo.lon, theta, r);
        
        return (RadiansToDegrees(lat), RadiansToDegrees(lon));
    }
    
    /// <summary>
    /// Converts a geographic coordinate to hex2d coordinates at a specific resolution.
    /// This is the proper H3 encoding algorithm from the reference implementation.
    /// Based on H3 reference _geoToHex2d function.
    /// </summary>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="longitude">Longitude in degrees</param>
    /// <param name="resolution">H3 resolution (0-15)</param>
    /// <returns>Tuple of (face index, hex2d coordinates)</returns>
    private static (int face, Vec2d hex2d) GeoToHex2d(double latitude, double longitude, int resolution)
    {
        // Find the closest icosahedron face
        var (face, sqd) = GeoToClosestFace(latitude, longitude);
        
        // Calculate great circle distance: cos(r) = 1 - sqd/2
        var r = Math.Acos(1.0 - sqd * 0.5);
        
        // If at face center, return origin
        if (r < 1e-10)
        {
            return (face, new Vec2d(0, 0));
        }
        
        // Convert to radians for calculations
        var lat = DegreesToRadians(latitude);
        var lon = DegreesToRadians(longitude);
        
        // Get face center coordinates (already in radians)
        var faceCenterGeo = FaceCenterGeo[face];
        
        // Calculate azimuth from face center to point
        var azimuth = GeoAzimuthRads(faceCenterGeo.lat, faceCenterGeo.lon, lat, lon);
        
        // Calculate theta (angle from face i-axis, counter-clockwise)
        // This uses the face axes azimuth for Class II orientation
        var theta = PosAngleRads(FaceAxesAzRadsCII[face][0] - azimuth);
        
        // Adjust theta for Class III resolutions (odd resolutions)
        if (IsResolutionClassIII(resolution))
        {
            theta = PosAngleRads(theta - M_AP7_ROT_RADS);
        }
        
        // Apply gnomonic projection
        r = Math.Tan(r);
        
        // Scale for resolution
        r *= INV_RES0_U_GNOMONIC;
        for (var i = 0; i < resolution; i++)
        {
            r *= M_SQRT7;
        }
        
        // Convert to Cartesian hex2d coordinates
        var x = r * Math.Cos(theta);
        var y = r * Math.Sin(theta);
        
        return (face, new Vec2d(x, y));
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
    
    /// <summary>
    /// Normalizes an angle in radians to the range [0, 2π).
    /// </summary>
    private static double PosAngleRads(double rads)
    {
        var twoPi = Math.PI * 2.0;
        var tmp = rads % twoPi;
        if (tmp < 0.0) tmp += twoPi;
        return tmp;
    }
    
    /// <summary>
    /// Calculates the squared great circle distance between two points on the sphere.
    /// Returns 2 * sin^2(distance/2), which is proportional to the squared chord distance.
    /// </summary>
    private static double GreatCircleDistanceSquared(double lat1, double lon1, double lat2, double lon2)
    {
        var sinLat = Math.Sin((lat2 - lat1) / 2.0);
        var sinLon = Math.Sin((lon2 - lon1) / 2.0);
        
        var a = sinLat * sinLat + Math.Cos(lat1) * Math.Cos(lat2) * sinLon * sinLon;
        
        return 2.0 * a;
    }
    
    /// <summary>
    /// Calculates the azimuth (bearing) from point p1 to point p2 in radians.
    /// </summary>
    private static double GeoAzimuthRads(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = lon2 - lon1;
        
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        
        return Math.Atan2(y, x);
    }
    
    /// <summary>
    /// Finds a point at a given azimuth and distance from a starting point.
    /// Implements the direct geodetic problem on a sphere.
    /// Based on H3 reference implementation's _geoAzDistanceRads function.
    /// </summary>
    private static (double lat, double lon) GeoAzDistanceRads(double lat1, double lon1, double azimuth, double distance)
    {
        const double epsilon = 1e-10;
        
        if (distance < epsilon)
        {
            return (lat1, lon1);
        }
        
        azimuth = PosAngleRads(azimuth);
        
        // Check for due north/south azimuth
        if (azimuth < epsilon || Math.Abs(azimuth - Math.PI) < epsilon)
        {
            double lat2;
            if (azimuth < epsilon)  // due north
                lat2 = lat1 + distance;
            else  // due south
                lat2 = lat1 - distance;
            
            // Check for poles
            if (Math.Abs(lat2 - Math.PI / 2.0) < epsilon)  // north pole
            {
                return (Math.PI / 2.0, 0.0);
            }
            else if (Math.Abs(lat2 + Math.PI / 2.0) < epsilon)  // south pole
            {
                return (-Math.PI / 2.0, 0.0);
            }
            else
            {
                return (lat2, ConstrainLng(lon1));
            }
        }
        else  // not due north or south
        {
            var sinLat = Math.Sin(lat1) * Math.Cos(distance) + Math.Cos(lat1) * Math.Sin(distance) * Math.Cos(azimuth);
            
            // Clamp to valid range
            if (sinLat > 1.0) sinLat = 1.0;
            if (sinLat < -1.0) sinLat = -1.0;
            
            var lat2 = Math.Asin(sinLat);
            
            // CRITICAL FIX: Check for poles after calculating lat2 (from H3 reference)
            if (Math.Abs(lat2 - Math.PI / 2.0) < epsilon)  // north pole
            {
                return (Math.PI / 2.0, 0.0);
            }
            else if (Math.Abs(lat2 + Math.PI / 2.0) < epsilon)  // south pole
            {
                return (-Math.PI / 2.0, 0.0);
            }
            else
            {
                // Calculate longitude using the inverse cosine of lat2
                var invCosLat2 = 1.0 / Math.Cos(lat2);
                var sinLon = Math.Sin(azimuth) * Math.Sin(distance) * invCosLat2;
                var cosLon = (Math.Cos(distance) - Math.Sin(lat1) * Math.Sin(lat2)) / Math.Cos(lat1) * invCosLat2;
                
                // Clamp to valid range (from H3 reference)
                if (sinLon > 1.0) sinLon = 1.0;
                if (sinLon < -1.0) sinLon = -1.0;
                if (cosLon > 1.0) cosLon = 1.0;
                if (cosLon < -1.0) cosLon = -1.0;
                
                var lon2 = ConstrainLng(lon1 + Math.Atan2(sinLon, cosLon));
                
                return (lat2, lon2);
            }
        }
    }
    
    /// <summary>
    /// Constrains longitude to the range [-π, π].
    /// </summary>
    private static double ConstrainLng(double lng)
    {
        while (lng > Math.PI)
        {
            lng -= 2.0 * Math.PI;
        }
        while (lng < -Math.PI)
        {
            lng += 2.0 * Math.PI;
        }
        return lng;
    }
}
