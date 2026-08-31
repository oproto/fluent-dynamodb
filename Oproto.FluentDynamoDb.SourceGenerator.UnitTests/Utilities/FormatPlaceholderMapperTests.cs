using Oproto.FluentDynamoDb.SourceGenerator.Utilities;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Utilities;

/// <summary>
/// Unit tests for <see cref="FormatPlaceholderMapper"/>.
/// **Property 2: Placeholder-to-split-index mapping correctness**
/// **Validates: Requirements 2.1, 2.2, 2.3**
/// </summary>
public class FormatPlaceholderMapperTests
{
    [Fact]
    public void BuildPlaceholderToSplitIndexMap_SingleVariableWithLeadingAndTrailingConstants_MapsCorrectly()
    {
        // Arrange & Act
        var mapping = FormatPlaceholderMapper.BuildPlaceholderToSplitIndexMap("TENANT#{0}#EXTERNAL_ACCESS", '#');

        // Assert
        mapping.Should().HaveCount(1);
        mapping[0].Should().Be(1);
    }

    [Fact]
    public void BuildPlaceholderToSplitIndexMap_TwoVariablesWithInterspersedConstants_MapsCorrectly()
    {
        // Arrange & Act
        var mapping = FormatPlaceholderMapper.BuildPlaceholderToSplitIndexMap("TENANT#{0}#ROLE#{1}", '#');

        // Assert
        mapping.Should().HaveCount(2);
        mapping[0].Should().Be(1);
        mapping[1].Should().Be(3);
    }

    [Fact]
    public void BuildPlaceholderToSplitIndexMap_ThreeVariablesWithMultipleInterspersedConstants_MapsCorrectly()
    {
        // Arrange & Act
        var mapping = FormatPlaceholderMapper.BuildPlaceholderToSplitIndexMap("TENANT#{0}#SHARE#RESOURCE#{1}#{2}", '#');

        // Assert
        mapping.Should().HaveCount(3);
        mapping[0].Should().Be(1);
        mapping[1].Should().Be(4);
        mapping[2].Should().Be(5);
    }

    [Fact]
    public void BuildPlaceholderToSplitIndexMap_TwoVariablesWithSingleLeadingConstant_MapsCorrectly()
    {
        // Arrange & Act
        var mapping = FormatPlaceholderMapper.BuildPlaceholderToSplitIndexMap("CAP#{0}#{1}", '#');

        // Assert
        mapping.Should().HaveCount(2);
        mapping[0].Should().Be(1);
        mapping[1].Should().Be(2);
    }

    [Fact]
    public void BuildPlaceholderToSplitIndexMap_FormatSpecifierD4_MapsCorrectly()
    {
        // Arrange & Act
        var mapping = FormatPlaceholderMapper.BuildPlaceholderToSplitIndexMap("SEQ#{0:D4}", '#');

        // Assert
        mapping.Should().HaveCount(1);
        mapping[0].Should().Be(1);
    }

    [Fact]
    public void BuildPlaceholderToSplitIndexMap_DateFormatSpecifier_MapsCorrectly()
    {
        // Arrange & Act
        var mapping = FormatPlaceholderMapper.BuildPlaceholderToSplitIndexMap("ENTRY#{0:yyyy-MM-dd}", '#');

        // Assert
        mapping.Should().HaveCount(1);
        mapping[0].Should().Be(1);
    }

    [Fact]
    public void BuildPlaceholderToSplitIndexMap_NoConstants_PlaceholderIndicesEqualSplitIndices()
    {
        // Arrange & Act
        var mapping = FormatPlaceholderMapper.BuildPlaceholderToSplitIndexMap("{0}#{1}#{2}", '#');

        // Assert
        mapping.Should().HaveCount(3);
        mapping[0].Should().Be(0);
        mapping[1].Should().Be(1);
        mapping[2].Should().Be(2);
    }

    [Fact]
    public void GetSplitIndex_SingleVariableWithLeadingConstant_ReturnsCorrectSplitIndex()
    {
        // Arrange & Act
        var splitIndex = FormatPlaceholderMapper.GetSplitIndex("TENANT#{0}#EXTERNAL_ACCESS", '#', 0);

        // Assert
        splitIndex.Should().Be(1);
    }

    [Fact]
    public void GetSplitIndex_ThreeVariablesThirdPlaceholder_ReturnsCorrectSplitIndex()
    {
        // Arrange & Act
        var splitIndex = FormatPlaceholderMapper.GetSplitIndex("TENANT#{0}#SHARE#RESOURCE#{1}#{2}", '#', 2);

        // Assert
        splitIndex.Should().Be(5);
    }
}
