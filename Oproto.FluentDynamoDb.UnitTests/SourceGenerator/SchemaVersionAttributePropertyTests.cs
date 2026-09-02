using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for FluentDynamoDbSchemaVersionAttribute correctness properties.
/// Feature: schema-version-attribute
/// </summary>
public class SchemaVersionAttributePropertyTests
{
    /// <summary>
    /// **Validates: Requirements 1.3, 1.4**
    /// Property 1: Constructor value round-trip.
    /// For any valid major (>= 1) and minor (>= 0), constructing the attribute yields matching Major/Minor.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "1")]
    public Property Constructor_RoundTrips_ValidMajorAndMinor()
    {
        var validMajorGen = Gen.Choose(1, 1000);
        var validMinorGen = Gen.Choose(0, 1000);

        return Prop.ForAll(
            validMajorGen.ToArbitrary(),
            validMinorGen.ToArbitrary(),
            (major, minor) =>
            {
                var attribute = new FluentDynamoDbSchemaVersionAttribute(major, minor);
                return (attribute.Major == major && attribute.Minor == minor)
                    .Label($"Expected Major={major}, Minor={minor} but got Major={attribute.Major}, Minor={attribute.Minor}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.6**
    /// Property 2: Constructor invalid input rejection (invalid major).
    /// For any major less than 1 with valid minor (>= 0), constructing the attribute throws ArgumentOutOfRangeException.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "2")]
    public Property Constructor_ThrowsArgumentOutOfRangeException_ForInvalidMajor()
    {
        var invalidMajorGen = Gen.Choose(int.MinValue, 0);
        var validMinorGen = Gen.Choose(0, 1000);

        return Prop.ForAll(
            invalidMajorGen.ToArbitrary(),
            validMinorGen.ToArbitrary(),
            (major, minor) =>
            {
                var threw = false;
                try
                {
                    _ = new FluentDynamoDbSchemaVersionAttribute(major, minor);
                }
                catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "major")
                {
                    threw = true;
                }

                return threw.Label($"Expected ArgumentOutOfRangeException for major={major}, minor={minor}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.6**
    /// Property 2: Constructor invalid input rejection (invalid minor).
    /// For any valid major (>= 1) with minor less than 0, constructing the attribute throws ArgumentOutOfRangeException.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "2")]
    public Property Constructor_ThrowsArgumentOutOfRangeException_ForInvalidMinor()
    {
        var validMajorGen = Gen.Choose(1, 1000);
        var invalidMinorGen = Gen.Choose(int.MinValue, -1);

        return Prop.ForAll(
            validMajorGen.ToArbitrary(),
            invalidMinorGen.ToArbitrary(),
            (major, minor) =>
            {
                var threw = false;
                try
                {
                    _ = new FluentDynamoDbSchemaVersionAttribute(major, minor);
                }
                catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "minor")
                {
                    threw = true;
                }

                return threw.Label($"Expected ArgumentOutOfRangeException for major={major}, minor={minor}");
            });
    }
}
