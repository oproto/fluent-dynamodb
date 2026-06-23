using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Property-based test verifying path equivalence (round-trip) for computed key typed overloads.
///
/// Property 6: Path equivalence (round-trip)
/// For any valid set of source property component values, invoking the typed convenience overload
/// SHALL produce a DynamoDB request with key AttributeValue entries byte-for-byte identical to
/// manually calling Entity.Keys.BuildPk(...) with the same values and passing the results to the
/// standard accessor overload with the composed key strings.
///
/// **Validates: Requirements 3.5, 9.3**
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "PropertyTest")]
[Trait("Feature", "ComputedKeyOverloads")]
public class ComputedKeyPathEquivalencePropertyTests
{
    /// <summary>
    /// For any random int values for year, month, day, the typed overload
    /// table.ComputedPkOnlyEvents.Get(year, month, day) produces a DynamoDB request with
    /// key AttributeValue entries identical to manually calling
    /// ComputedPkOnlyEvent.Keys.BuildPk(year, month, day) and passing the result to the
    /// standard accessor overload.
    ///
    /// **Validates: Requirements 3.5, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_ProducesIdenticalKeys_AsManualBuildPkWithStandardOverload()
    {
        // Generate constrained int values that are valid for year/month/day components
        // We use positive ints to represent realistic key components
        var componentGen = from year in Gen.Choose(1, 9999)
                           from month in Gen.Choose(1, 12)
                           from day in Gen.Choose(1, 31)
                           select (year, month, day);

        return Prop.ForAll(componentGen.ToArbitrary(), components =>
        {
            var (year, month, day) = components;

            // Arrange
            var mockClient = Substitute.For<IAmazonDynamoDB>();
            var table = new TestComputedPkOnlyTable(mockClient, "test-table");

            // Act - Path 1: Typed convenience overload
            var typedRequest = table.ComputedPkOnlyEvents.Get(year, month, day).ToGetItemRequest();

            // Act - Path 2: Manual BuildPk + standard string overload
            var manualPk = ComputedPkOnlyEvent.Keys.BuildPk(year, month, day);
            var standardRequest = table.ComputedPkOnlyEvents.Get(manualPk).ToGetItemRequest();

            // Assert - Key AttributeValue entries must be byte-for-byte identical
            var keysMatch = typedRequest.Key["pk"].S == standardRequest.Key["pk"].S;

            return keysMatch.ToProperty()
                .Label($"year={year}, month={month}, day={day}: " +
                       $"typed=\"{typedRequest.Key["pk"].S}\", " +
                       $"standard=\"{standardRequest.Key["pk"].S}\"");
        });
    }
}
