using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests for resolution 3 cells using data from H3 reference implementation.
/// Test data extracted from h3/tests/inputfiles/res03ic.txt
/// 
/// Resolution 3 cells are the third level of subdivision.
/// Each resolution 2 cell is divided into 7 resolution 3 cells.
/// This tests even deeper digit path encoding/decoding and coordinate transformations.
/// </summary>
public class H3Resolution3Tests
{
    private readonly ITestOutputHelper _output;

    public H3Resolution3Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("830000fffffffff", 79.2423985098, 38.0234070080)]
    [InlineData("830001fffffffff", 80.1167714103, 34.2693230234)]
    [InlineData("830002fffffffff", 79.2475621066, 43.7509884749)]
    [InlineData("830003fffffffff", 80.1909572143, 40.4948806727)]
    [InlineData("830004fffffffff", 78.2899792591, 35.9758018082)]
    [InlineData("830005fffffffff", 79.1345859133, 32.4136499107)]
    [InlineData("830006fffffffff", 78.3321141500, 41.2006063338)]
    [InlineData("830008fffffffff", 81.9574923092, 31.7572277199)]
    [InlineData("830009fffffffff", 82.7481064668, 25.8634454744)]
    [InlineData("83000afffffffff", 82.0750475075, 39.4444064635)]
    public void Decode_Resolution3_First10Cells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert - allow small tolerance for floating point differences
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    [Theory]
    [InlineData("83000bfffffffff", 82.9716761664, 34.2943092514)]
    [InlineData("83000cfffffffff", 80.9431988406, 29.8082873051)]
    [InlineData("83000dfffffffff", 81.7059805422, 24.4920890159)]
    [InlineData("83000efffffffff", 81.0988307976, 36.5589999990)]
    [InlineData("830010fffffffff", 78.1281546999, 51.7045382695)]
    [InlineData("830011fffffffff", 79.1467886841, 49.4843331183)]
    [InlineData("830012fffffffff", 77.8817671649, 56.8109541954)]
    [InlineData("830013fffffffff", 78.9400950736, 55.1107102926)]
    [InlineData("830014fffffffff", 77.2916448675, 48.7725476999)]
    [InlineData("830015fffffffff", 78.2786504484, 46.4729694675)]
    public void Decode_Resolution3_Next10Cells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    [Theory]
    [InlineData("830016fffffffff", 77.0992128940, 53.5691049421)]
    [InlineData("830018fffffffff", 81.0292761569, 50.5046347695)]
    [InlineData("830019fffffffff", 82.0480552257, 47.2999849303)]
    [InlineData("83001afffffffff", 80.7995397425, 57.2983839901)]
    [InlineData("83001bfffffffff", 81.8736786232, 55.0354884812)]
    [InlineData("83001cfffffffff", 80.1496516456, 46.8050556226)]
    [InlineData("83001dfffffffff", 81.1292607319, 43.5242155013)]
    [InlineData("83001efffffffff", 79.9909254109, 53.0490713717)]
    [InlineData("830020fffffffff", 77.1842902821, 29.5986973277)]
    [InlineData("830021fffffffff", 77.9367492104, 25.9914857659)]
    public void Decode_Resolution3_HighLatitudes_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    [Theory]
    [InlineData("830040fffffffff", 83.6576608516, -7.3190411494)]
    [InlineData("830041fffffffff", 83.7419387644, -17.3245782879)]
    [InlineData("830042fffffffff", 84.4199058398, -0.4045340732)]
    [InlineData("830043fffffffff", 84.6224650028, -11.8308209949)]
    [InlineData("830044fffffffff", 82.6745626162, -4.0386955339)]
    [InlineData("830045fffffffff", 82.8349473996, -12.6346995705)]
    [InlineData("830046fffffffff", 83.3887263208, 2.1739410031)]
    [InlineData("830048fffffffff", 84.3625365404, -34.7989745100)]
    [InlineData("830049fffffffff", 83.9330898583, -44.7000991016)]
    [InlineData("83004afffffffff", 85.3802902387, -32.2127854366)]
    public void Decode_Resolution3_NegativeLongitudes_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    [Theory]
    [InlineData("830080fffffffff", 77.1530287166, 74.8802719453)]
    [InlineData("830081fffffffff", 78.2800927913, 74.9846465804)]
    [InlineData("830082fffffffff", 76.5057686217, 78.9329031987)]
    [InlineData("830083fffffffff", 77.6294824097, 79.4069970220)]
    [InlineData("830084fffffffff", 76.6110792331, 70.7387457599)]
    [InlineData("830085fffffffff", 77.7289205029, 70.4545031178)]
    [InlineData("830086fffffffff", 76.0321068741, 74.7929186237)]
    [InlineData("830088fffffffff", 79.8901758817, 80.6766062479)]
    [InlineData("830089fffffffff", 81.0245679044, 81.5535119924)]
    [InlineData("83008afffffffff", 79.1492856584, 85.4199548035)]
    public void Decode_Resolution3_AsiaRegion_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    [Theory]
    [InlineData("830200fffffffff", 79.2209863563, -107.4292022430)]
    [InlineData("830201fffffffff", 78.7309116985, -102.5944500324)]
    [InlineData("830202fffffffff", 78.5693822791, -112.0509388477)]
    [InlineData("830203fffffffff", 78.1528221615, -107.3128381228)]
    [InlineData("830204fffffffff", 80.2890977047, -107.5708327102)]
    [InlineData("830205fffffffff", 79.7957242563, -102.2576001684)]
    [InlineData("830206fffffffff", 79.6294255627, -112.6787601978)]
    [InlineData("830208fffffffff", 77.0836853085, -98.7524973334)]
    [InlineData("830209fffffffff", 76.4457035155, -94.9473806498)]
    [InlineData("83020afffffffff", 76.5684196613, -103.1158922559)]
    public void Decode_Resolution3_NorthAmerica_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }
}
