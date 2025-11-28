using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

public class DiagnosticTest
{
    private readonly ITestOutputHelper _output;
    
    public DiagnosticTest(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public void DiagnoseDateLineCrossing()
    {
        var searchCenter = new GeoLocation(0.0, 179.0);
        var radiusKm = 200.0;
        var level = 8; // Use level 8 (~18km cells) with 200km radius to stay within 500 cell limit
        
        _output.WriteLine($"Search center: ({searchCenter.Latitude}, {searchCenter.Longitude})");
        _output.WriteLine($"Radius: {radiusKm} km");
        
        // Create bounding box
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(searchCenter, radiusKm);
        _output.WriteLine($"Bounding box: SW=({bbox.Southwest.Latitude:F4}, {bbox.Southwest.Longitude:F4}), NE=({bbox.Northeast.Latitude:F4}, {bbox.Northeast.Longitude:F4})");
        _output.WriteLine($"Crosses date line: {bbox.CrossesDateLine()}");
        
        // Test store locations
        var storeLocations = new[]
        {
            ("West Side Store 1", new GeoLocation(0.0, 179.5)),
            ("East Side Store 1", new GeoLocation(0.0, -179.5)),
            ("Center Store", new GeoLocation(0.0, 179.0)),
        };
        
        _output.WriteLine("\nStore locations and their cells:");
        foreach (var (name, loc) in storeLocations)
        {
            var cell = S2Encoder.Encode(loc.Latitude, loc.Longitude, level);
            var inBbox = bbox.Contains(loc);
            var distance = loc.DistanceToKilometers(searchCenter);
            _output.WriteLine($"  {name}: ({loc.Latitude}, {loc.Longitude}) -> cell={cell}, inBbox={inBbox}, distance={distance:F2}km");
        }
        
        // Get cell covering
        var cells = S2CellCovering.GetCellsForRadius(searchCenter, radiusKm, level, maxCells: 500);
        _output.WriteLine($"\nCell covering has {cells.Count} cells");
        
        // Check if store cells are in covering
        _output.WriteLine("\nStore cells in covering:");
        foreach (var (name, loc) in storeLocations)
        {
            var cell = S2Encoder.Encode(loc.Latitude, loc.Longitude, level);
            var inCovering = cells.Contains(cell);
            _output.WriteLine($"  {name}: {cell} -> in covering: {inCovering}");
        }
        
        // If date line crossing, check split boxes
        if (bbox.CrossesDateLine())
        {
            var (western, eastern) = bbox.SplitAtDateLine();
            _output.WriteLine($"\nWestern box: SW=({western.Southwest.Latitude:F4}, {western.Southwest.Longitude:F4}), NE=({western.Northeast.Latitude:F4}, {western.Northeast.Longitude:F4})");
            _output.WriteLine($"Eastern box: SW=({eastern.Southwest.Latitude:F4}, {eastern.Southwest.Longitude:F4}), NE=({eastern.Northeast.Latitude:F4}, {eastern.Northeast.Longitude:F4})");
            
            foreach (var (name, loc) in storeLocations)
            {
                var inWestern = western.Contains(loc);
                var inEastern = eastern.Contains(loc);
                _output.WriteLine($"  {name}: in western={inWestern}, in eastern={inEastern}");
            }
            
            // Get cells for each split box separately
            var westernCells = S2CellCovering.GetCellsForBoundingBox(western, level, 250);
            var easternCells = S2CellCovering.GetCellsForBoundingBox(eastern, level, 250);
            _output.WriteLine($"\nWestern cells: {westernCells.Count}");
            _output.WriteLine($"Eastern cells: {easternCells.Count}");
            
            // Check if store cells are in the individual coverings
            _output.WriteLine("\nStore cells in individual coverings:");
            foreach (var (name, loc) in storeLocations)
            {
                var cell = S2Encoder.Encode(loc.Latitude, loc.Longitude, level);
                var inWesternCells = westernCells.Contains(cell);
                var inEasternCells = easternCells.Contains(cell);
                _output.WriteLine($"  {name}: {cell} -> in western cells: {inWesternCells}, in eastern cells: {inEasternCells}");
            }
            
            // Show some sample cells from each covering
            _output.WriteLine($"\nFirst 5 western cells: {string.Join(", ", westernCells.Take(5))}");
            _output.WriteLine($"First 5 eastern cells: {string.Join(", ", easternCells.Take(5))}");
            
            // Decode the store cells to see their locations
            _output.WriteLine("\nStore cell centers:");
            foreach (var (name, loc) in storeLocations)
            {
                var cell = S2Encoder.Encode(loc.Latitude, loc.Longitude, level);
                var (cellLat, cellLon) = S2Encoder.Decode(cell);
                _output.WriteLine($"  {name}: cell={cell}, center=({cellLat:F4}, {cellLon:F4})");
            }
            
            // Decode some covering cells to see their locations
            _output.WriteLine("\nFirst 3 western cell centers:");
            foreach (var cell in westernCells.Take(3))
            {
                var (cellLat, cellLon) = S2Encoder.Decode(cell);
                _output.WriteLine($"  {cell}: ({cellLat:F4}, {cellLon:F4})");
            }
            
            _output.WriteLine("\nFirst 3 eastern cell centers:");
            foreach (var cell in easternCells.Take(3))
            {
                var (cellLat, cellLon) = S2Encoder.Decode(cell);
                _output.WriteLine($"  {cell}: ({cellLat:F4}, {cellLon:F4})");
            }
            
            // Check the expanded bounding box
            var cellSizeKm = 85000.0 / Math.Pow(2, level);
            var cellSizeDegrees = cellSizeKm / 111.0;
            var expansionDegrees = cellSizeDegrees * 2;
            
            // Manually compute expanded western box the same way as the fix
            var expandedSwLat = Math.Max(-90, western.Southwest.Latitude - expansionDegrees);
            var expandedSwLon = Math.Max(-180, western.Southwest.Longitude - expansionDegrees);
            var expandedNeLat = Math.Min(90, western.Northeast.Latitude + expansionDegrees);
            var expandedNeLon = Math.Min(180, western.Northeast.Longitude + expansionDegrees);
            var expandedWestern = new GeoBoundingBox(
                new GeoLocation(expandedSwLat, expandedSwLon),
                new GeoLocation(expandedNeLat, expandedNeLon));
            
            _output.WriteLine($"\nExpanded western box (fixed): SW=({expandedWestern.Southwest.Latitude:F4}, {expandedWestern.Southwest.Longitude:F4}), NE=({expandedWestern.Northeast.Latitude:F4}, {expandedWestern.Northeast.Longitude:F4})");
            _output.WriteLine($"Expanded western crosses date line: {expandedWestern.CrossesDateLine()}");
            
            // Check if store cell centers are in expanded box
            foreach (var (name, loc) in storeLocations)
            {
                var cell = S2Encoder.Encode(loc.Latitude, loc.Longitude, level);
                var (cellLat, cellLon) = S2Encoder.Decode(cell);
                var cellCenter = new GeoLocation(cellLat, cellLon);
                var inExpanded = expandedWestern.Contains(cellCenter);
                _output.WriteLine($"  {name} cell center ({cellLat:F4}, {cellLon:F4}) in expanded western: {inExpanded}");
            }
            
            // Check grid sampling
            var latRange = western.Northeast.Latitude - western.Southwest.Latitude;
            var lonRange = western.Northeast.Longitude - western.Southwest.Longitude;
            var sampleInterval = cellSizeDegrees / 2.0;
            var latSamples = Math.Max(2, (int)Math.Ceiling(latRange / sampleInterval) + 1);
            var lonSamples = Math.Max(2, (int)Math.Ceiling(lonRange / sampleInterval) + 1);
            _output.WriteLine($"\nGrid sampling: latSamples={latSamples}, lonSamples={lonSamples}");
            _output.WriteLine($"Lat range: {latRange:F4}, Lon range: {lonRange:F4}");
            _output.WriteLine($"Sample interval: {sampleInterval:F6}");
            
            // Sample at 179.5 longitude
            var testLon = 179.5;
            var testCell = S2Encoder.Encode(0.0, testLon, level);
            _output.WriteLine($"\nTest cell at (0, {testLon}): {testCell}");
            _output.WriteLine($"Is test cell in western cells: {westernCells.Contains(testCell)}");
        }
    }
    
    [Fact]
    public void DiagnoseTestStoreCells()
    {
        // Use level 10 (~4.5km cells) with 5km radius to stay within 500 cell limit
        var level = 10;
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        var radiusKm = 5.0;
        
        var locations = new[] {
            ("Downtown", new GeoLocation(37.7749, -122.4194)),
            ("Mission", new GeoLocation(37.7759, -122.4204)),
            ("Marina", new GeoLocation(37.7739, -122.4184)),
            ("Haight", new GeoLocation(37.7769, -122.4214)),
        };
        
        _output.WriteLine("Test store S2 cells:");
        foreach (var (name, loc) in locations)
        {
            var cell = S2Encoder.Encode(loc.Latitude, loc.Longitude, level);
            _output.WriteLine($"  {name}: ({loc.Latitude}, {loc.Longitude}) -> {cell}");
        }
        
        // Get the cell covering
        var coveringCells = S2CellCovering.GetCellsForRadius(searchCenter, radiusKm, level, maxCells: 500);
        _output.WriteLine($"Cell covering has {coveringCells.Count} cells");
        
        // Check if each store's cell is in the covering
        _output.WriteLine("Store cells in covering:");
        foreach (var (name, loc) in locations)
        {
            var cell = S2Encoder.Encode(loc.Latitude, loc.Longitude, level);
            var inCovering = coveringCells.Contains(cell);
            _output.WriteLine($"  {name}: {cell} -> in covering: {inCovering}");
            
            if (!inCovering)
            {
                // Check neighbors
                var neighbors = S2Encoder.GetNeighbors(cell);
                var neighborsInCovering = neighbors.Where(n => coveringCells.Contains(n)).ToList();
                _output.WriteLine($"    Neighbors in covering: {neighborsInCovering.Count}");
                foreach (var n in neighborsInCovering)
                {
                    _output.WriteLine($"      {n}");
                }
                
                // Check distance from center
                var distance = loc.DistanceToKilometers(searchCenter);
                _output.WriteLine($"    Distance from center: {distance:F3} km");
            }
        }
    }
    
    [Fact]
    public void DiagnoseS2CellCovering()
    {
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        var marchStore = new GeoLocation(37.7699, -122.4144);
        
        // Use level 10 (~4.5km cells) with 5km radius to stay within 500 cell limit
        var radiusKm = 5.0;
        var level = 10;
        
        // Get the cell for March store
        var marchCell = S2Encoder.Encode(marchStore.Latitude, marchStore.Longitude, level);
        _output.WriteLine($"March store cell: {marchCell}");
        
        // Get cells for the search
        var cells = S2CellCovering.GetCellsForRadius(searchCenter, radiusKm, level, maxCells: 100);
        _output.WriteLine($"Number of cells (maxCells=100): {cells.Count}");
        _output.WriteLine($"March store in cells: {cells.Contains(marchCell)}");
        
        // Try with higher maxCells
        var cells500 = S2CellCovering.GetCellsForRadius(searchCenter, radiusKm, level, maxCells: 500);
        _output.WriteLine($"Number of cells (maxCells=500): {cells500.Count}");
        _output.WriteLine($"March store in cells (500): {cells500.Contains(marchCell)}");
        
        // Check cell prefixes
        var prefixes = cells.Select(c => c.Substring(0, 6)).Distinct().OrderBy(p => p).ToList();
        _output.WriteLine($"Cell prefixes in result: {string.Join(", ", prefixes)}");
        _output.WriteLine($"March cell prefix: {marchCell.Substring(0, 6)}");
        
        // List all 808f7e cells
        var marchPrefixCells = cells500.Where(c => c.StartsWith("808f7e")).OrderBy(c => c).ToList();
        _output.WriteLine($"Cells with March prefix (808f7e): {marchPrefixCells.Count}");
        
        // Check neighbors of March cell
        var marchNeighbors = S2Encoder.GetNeighbors(marchCell);
        _output.WriteLine($"March cell neighbors:");
        foreach (var n in marchNeighbors)
        {
            var inCells = cells500.Contains(n);
            var (lat, lon) = S2Encoder.Decode(n);
            _output.WriteLine($"  {n} -> ({lat:F6}, {lon:F6}) in cells: {inCells}");
        }
        
        // Check if any of the March cell's neighbors' neighbors are in the cells
        _output.WriteLine($"March cell neighbors' neighbors in cells:");
        foreach (var n in marchNeighbors)
        {
            var nn = S2Encoder.GetNeighbors(n);
            foreach (var nnn in nn)
            {
                if (cells500.Contains(nnn))
                {
                    _output.WriteLine($"  {n} -> {nnn} (in cells)");
                }
            }
        }
        
        // Check bounding box
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(searchCenter, radiusKm);
        _output.WriteLine($"Bounding box: SW=({bbox.Southwest.Latitude:F6}, {bbox.Southwest.Longitude:F6}), NE=({bbox.Northeast.Latitude:F6}, {bbox.Northeast.Longitude:F6})");
        _output.WriteLine($"March store in bbox: {bbox.Contains(marchStore)}");
        
        // Calculate expected sampling
        var cellSizeKm = 85000.0 / Math.Pow(2, level);
        var cellSizeDegrees = cellSizeKm / 111.0;
        var sampleInterval = cellSizeDegrees / 5.0;
        var latRange = bbox.Northeast.Latitude - bbox.Southwest.Latitude;
        var lonRange = bbox.Northeast.Longitude - bbox.Southwest.Longitude;
        var latSamples = (int)Math.Ceiling(latRange / sampleInterval) + 1;
        var lonSamples = (int)Math.Ceiling(lonRange / sampleInterval) + 1;
        
        _output.WriteLine($"Cell size: {cellSizeKm:F3} km = {cellSizeDegrees:F6} degrees");
        _output.WriteLine($"Sample interval: {sampleInterval:F6} degrees");
        _output.WriteLine($"Lat range: {latRange:F6}, Lon range: {lonRange:F6}");
        _output.WriteLine($"Expected samples: {latSamples} x {lonSamples} = {latSamples * lonSamples}");
        
        // Check if March store location would be sampled
        var marchLatOffset = marchStore.Latitude - bbox.Southwest.Latitude;
        var marchLonOffset = marchStore.Longitude - bbox.Southwest.Longitude;
        var marchLatIndex = marchLatOffset / (latRange / (latSamples - 1));
        var marchLonIndex = marchLonOffset / (lonRange / (lonSamples - 1));
        _output.WriteLine($"March store would be at sample index: ({marchLatIndex:F2}, {marchLonIndex:F2})");
        
        // Find closest cell to March store
        var closestCell = cells
            .Select(c => {
                var (lat, lon) = S2Encoder.Decode(c);
                return new { Cell = c, Distance = marchStore.DistanceToKilometers(new GeoLocation(lat, lon)) };
            })
            .OrderBy(x => x.Distance)
            .First();
        _output.WriteLine($"Closest cell to March store: {closestCell.Cell} at {closestCell.Distance:F3} km");
        
        // Manually sample the March store location
        var latStep = latRange / (latSamples - 1);
        var lonStep = lonRange / (lonSamples - 1);
        
        // Find the sample point closest to March store
        var marchLatIdx = (int)Math.Round((marchStore.Latitude - bbox.Southwest.Latitude) / latStep);
        var marchLonIdx = (int)Math.Round((marchStore.Longitude - bbox.Southwest.Longitude) / lonStep);
        var sampleLat = bbox.Southwest.Latitude + marchLatIdx * latStep;
        var sampleLon = bbox.Southwest.Longitude + marchLonIdx * lonStep;
        var sampleCell = S2Encoder.Encode(sampleLat, sampleLon, level);
        _output.WriteLine($"Sample at index ({marchLatIdx}, {marchLonIdx}): ({sampleLat:F6}, {sampleLon:F6}) -> cell {sampleCell}");
        _output.WriteLine($"Sample cell matches March cell: {sampleCell == marchCell}");
        
        // Try encoding the exact March store location
        var exactMarchCell = S2Encoder.Encode(marchStore.Latitude, marchStore.Longitude, level);
        _output.WriteLine($"Exact March store encode: {exactMarchCell}");
        
        // Decode the March cell to see its center
        var (marchCellLat, marchCellLon) = S2Encoder.Decode(marchCell);
        _output.WriteLine($"March cell center: ({marchCellLat:F6}, {marchCellLon:F6})");
        
        // Check if March cell center is in the bounding box
        var marchCellCenter = new GeoLocation(marchCellLat, marchCellLon);
        _output.WriteLine($"March cell center in bbox: {bbox.Contains(marchCellCenter)}");
        
        // Check if March cell center would be sampled
        var marchCellLatIdx = (marchCellLat - bbox.Southwest.Latitude) / latStep;
        var marchCellLonIdx = (marchCellLon - bbox.Southwest.Longitude) / lonStep;
        _output.WriteLine($"March cell center would be at sample index: ({marchCellLatIdx:F2}, {marchCellLonIdx:F2})");
        
        // Sample at the March cell center's nearest grid point
        var nearestLatIdx = (int)Math.Round(marchCellLatIdx);
        var nearestLonIdx = (int)Math.Round(marchCellLonIdx);
        var nearestSampleLat = bbox.Southwest.Latitude + nearestLatIdx * latStep;
        var nearestSampleLon = bbox.Southwest.Longitude + nearestLonIdx * lonStep;
        var nearestSampleCell = S2Encoder.Encode(nearestSampleLat, nearestSampleLon, level);
        _output.WriteLine($"Nearest sample to March cell center: ({nearestSampleLat:F6}, {nearestSampleLon:F6}) -> {nearestSampleCell}");
    }
}
