using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Manual trace through the overage logic to understand what's happening.
/// </summary>
public class H3OverageManualTraceTests
{
    private readonly ITestOutputHelper _output;

    public H3OverageManualTraceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Trace_8102bffffffffff_ManualCalculation()
    {
        // H3: 8102bffffffffff
        // Base cell 1: Face 2, IJK=(1,1,0)
        // Resolution 1, Digit 2
        // Expected: lat=73.6130161629, lon=-130.7435224238
        
        _output.WriteLine("=== Manual Trace for 8102bffffffffff ===");
        _output.WriteLine("Base cell 1: Face 2, IJK=(1,1,0)");
        _output.WriteLine("Resolution 1 (Class III - rotate CCW)");
        _output.WriteLine("Digit 2");
        _output.WriteLine("");
        
        // Step 1: Start with base cell IJK
        var i = 1;
        var j = 1;
        var k = 0;
        _output.WriteLine($"Start: i={i}, j={j}, k={k}, sum={i+j+k}");
        
        // Step 2: Apply DownAp7 (Class III - CCW rotation)
        // DownAp7 uses: iVec=(2,1,0), jVec=(0,2,1), kVec=(1,0,2)
        // Result = i*iVec + j*jVec + k*kVec
        // = 1*(2,1,0) + 1*(0,2,1) + 0*(1,0,2)
        // = (2,1,0) + (0,2,1) + (0,0,0)
        // = (2,3,1)
        i = 2;
        j = 3;
        k = 1;
        _output.WriteLine($"After DownAp7: i={i}, j={j}, k={k}, sum={i+j+k}");
        
        // Step 3: Apply Neighbor for digit 2
        // Digit 2 corresponds to unit vector (0,1,0)
        i = 2;
        j = 4;
        k = 1;
        _output.WriteLine($"After Neighbor(2): i={i}, j={j}, k={k}, sum={i+j+k}");
        
        // Step 4: Check for overage
        // Resolution 1 is Class III, so we drop into Class II (res 2)
        // Apply DownAp7r: iVec=(3,1,0), jVec=(0,3,1), kVec=(1,0,3)
        // = 2*(3,1,0) + 4*(0,3,1) + 1*(1,0,3)
        // = (6,2,0) + (0,12,4) + (1,0,3)
        // = (7,14,7)
        var i2 = 7;
        var j2 = 14;
        var k2 = 7;
        _output.WriteLine($"After DownAp7r (for overage check): i={i2}, j={j2}, k={k2}, sum={i2+j2+k2}");
        
        // Step 5: Check overage
        // MaxDim for resolution 2 is 14
        // Sum = 7+14+7 = 28 > 14, so we have overage!
        _output.WriteLine($"MaxDim for res 2: 14");
        _output.WriteLine($"Sum {i2+j2+k2} > 14, so OVERAGE detected");
        _output.WriteLine("");
        
        // Step 6: Determine quadrant
        _output.WriteLine($"Quadrant determination:");
        _output.WriteLine($"  k={k2} > 0? {k2 > 0}");
        if (k2 > 0)
        {
            _output.WriteLine($"  j={j2} > 0? {j2 > 0}");
            if (j2 > 0)
            {
                _output.WriteLine($"  -> JK quadrant");
            }
            else
            {
                _output.WriteLine($"  -> KI quadrant");
            }
        }
        else
        {
            _output.WriteLine($"  -> IJ quadrant");
        }
        
        // The actual decode
        var (actualLat, actualLon) = H3Encoder.Decode("8102bffffffffff");
        _output.WriteLine("");
        _output.WriteLine($"Actual decoded: lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Expected:       lat=73.6130161629, lon=-130.7435224238");
    }
    
    [Fact]
    public void Trace_8108bffffffffff_ManualCalculation()
    {
        // H3: 8108bffffffffff
        // Base cell 4: Face 0, IJK=(2,0,0) - PENTAGON!
        // Resolution 1, Digit 2
        // Expected: lat=60.1912180486, lon=18.2585301192
        
        _output.WriteLine("=== Manual Trace for 8108bffffffffff ===");
        _output.WriteLine("Base cell 4: Face 0, IJK=(2,0,0) - PENTAGON");
        _output.WriteLine("Resolution 1 (Class III - rotate CCW)");
        _output.WriteLine("Digit 2");
        _output.WriteLine("");
        
        // Step 1: Start with base cell IJK
        var i = 2;
        var j = 0;
        var k = 0;
        _output.WriteLine($"Start: i={i}, j={j}, k={k}, sum={i+j+k}");
        
        // Step 2: Apply DownAp7 (Class III - CCW rotation)
        // Result = 2*(2,1,0) + 0*(0,2,1) + 0*(1,0,2)
        // = (4,2,0)
        i = 4;
        j = 2;
        k = 0;
        _output.WriteLine($"After DownAp7: i={i}, j={j}, k={k}, sum={i+j+k}");
        
        // Step 3: Apply Neighbor for digit 2
        // Digit 2 corresponds to unit vector (0,1,0)
        i = 4;
        j = 3;
        k = 0;
        _output.WriteLine($"After Neighbor(2): i={i}, j={j}, k={k}, sum={i+j+k}");
        
        // Step 4: Check for overage
        // Resolution 1 is Class III, so we drop into Class II (res 2)
        // Apply DownAp7r: iVec=(3,1,0), jVec=(0,3,1), kVec=(1,0,3)
        // = 4*(3,1,0) + 3*(0,3,1) + 0*(1,0,3)
        // = (12,4,0) + (0,9,3) + (0,0,0)
        // = (12,13,3)
        var i2 = 12;
        var j2 = 13;
        var k2 = 3;
        _output.WriteLine($"After DownAp7r (for overage check): i={i2}, j={j2}, k={k2}, sum={i2+j2+k2}");
        
        // Step 5: Check overage
        // MaxDim for resolution 2 is 14
        // Sum = 12+13+3 = 28 > 14, so we have overage!
        _output.WriteLine($"MaxDim for res 2: 14");
        _output.WriteLine($"Sum {i2+j2+k2} > 14, so OVERAGE detected");
        _output.WriteLine($"Pentagon with leading digit 2 (not 4), so pentLeading4=false");
        _output.WriteLine("");
        
        // Step 6: Determine quadrant
        _output.WriteLine($"Quadrant determination:");
        _output.WriteLine($"  k={k2} > 0? {k2 > 0}");
        if (k2 > 0)
        {
            _output.WriteLine($"  j={j2} > 0? {j2 > 0}");
            if (j2 > 0)
            {
                _output.WriteLine($"  -> JK quadrant");
            }
            else
            {
                _output.WriteLine($"  -> KI quadrant");
            }
        }
        else
        {
            _output.WriteLine($"  -> IJ quadrant");
        }
        
        // The actual decode
        var (actualLat, actualLon) = H3Encoder.Decode("8108bffffffffff");
        _output.WriteLine("");
        _output.WriteLine($"Actual decoded: lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Expected:       lat=60.1912180486, lon=18.2585301192");
    }
}
