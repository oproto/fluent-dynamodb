using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

public class FormatSpecifierHelperTests
{
    #region HasAnyFormatSpecifier - Returns true for format strings with specifiers (Requirements 3.1, 3.2)

    [Theory]
    [InlineData("{0:yyyy-MM-dd}#{1}")]
    [InlineData("{0:D4}#{1}")]
    [InlineData("{0:G}#{1}")]
    [InlineData("{0:HH:mm:ss}")]
    [InlineData("{0:N2}")]
    [InlineData("{0:yyyy-MM-dd}")]
    [InlineData("{1:D4}")]
    public void HasAnyFormatSpecifier_WithFormatSpecifiers_ReturnsTrue(string format)
    {
        var result = FormatSpecifierHelper.HasAnyFormatSpecifier(format);

        result.Should().BeTrue();
    }

    #endregion

    #region HasAnyFormatSpecifier - Returns false for simple placeholders (Requirements 3.1, 3.2)

    [Theory]
    [InlineData("{0}#{1}")]
    [InlineData("{0}")]
    [InlineData("{0}#{1}#{2}")]
    [InlineData("literal-only")]
    public void HasAnyFormatSpecifier_WithoutFormatSpecifiers_ReturnsFalse(string format)
    {
        var result = FormatSpecifierHelper.HasAnyFormatSpecifier(format);

        result.Should().BeFalse();
    }

    #endregion

    #region HasAnyFormatSpecifier - Returns false for null/empty (Requirements 3.1, 3.2)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HasAnyFormatSpecifier_NullOrEmpty_ReturnsFalse(string? format)
    {
        var result = FormatSpecifierHelper.HasAnyFormatSpecifier(format);

        result.Should().BeFalse();
    }

    #endregion

    #region HasFormatSpecifierForIndex - Returns true for correct index (Requirements 3.1, 3.2)

    [Fact]
    public void HasFormatSpecifierForIndex_IndexWithSpecifier_ReturnsTrue()
    {
        var result = FormatSpecifierHelper.HasFormatSpecifierForIndex("{0:yyyy-MM-dd}#{1}", 0);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasFormatSpecifierForIndex_SecondIndexWithSpecifier_ReturnsTrue()
    {
        var result = FormatSpecifierHelper.HasFormatSpecifierForIndex("{0}#{1:D4}", 1);

        result.Should().BeTrue();
    }

    #endregion

    #region HasFormatSpecifierForIndex - Returns false for index without specifier (Requirements 3.1, 3.2)

    [Fact]
    public void HasFormatSpecifierForIndex_IndexWithoutSpecifier_ReturnsFalse()
    {
        var result = FormatSpecifierHelper.HasFormatSpecifierForIndex("{0:yyyy-MM-dd}#{1}", 1);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasFormatSpecifierForIndex_SimpleFormat_ReturnsFalse()
    {
        var result = FormatSpecifierHelper.HasFormatSpecifierForIndex("{0}#{1}", 0);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasFormatSpecifierForIndex_NonExistentIndex_ReturnsFalse()
    {
        var result = FormatSpecifierHelper.HasFormatSpecifierForIndex("{0:yyyy-MM-dd}#{1}", 5);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HasFormatSpecifierForIndex_NullOrEmpty_ReturnsFalse(string? format)
    {
        var result = FormatSpecifierHelper.HasFormatSpecifierForIndex(format, 0);

        result.Should().BeFalse();
    }

    #endregion

    #region GetIndicesWithFormatSpecifiers - Returns correct set for mixed formats (Requirements 4.1, 5.4)

    [Fact]
    public void GetIndicesWithFormatSpecifiers_MixedFormat_ReturnsOnlySpecifierIndices()
    {
        var result = FormatSpecifierHelper.GetIndicesWithFormatSpecifiers("{0:yyyy-MM-dd}#{1}#{2:D4}");

        result.Should().BeEquivalentTo(new HashSet<int> { 0, 2 });
    }

    [Fact]
    public void GetIndicesWithFormatSpecifiers_AllWithSpecifiers_ReturnsAllIndices()
    {
        var result = FormatSpecifierHelper.GetIndicesWithFormatSpecifiers("{0:yyyy-MM-dd}#{1:G}");

        result.Should().BeEquivalentTo(new HashSet<int> { 0, 1 });
    }

    [Fact]
    public void GetIndicesWithFormatSpecifiers_NoSpecifiers_ReturnsEmptySet()
    {
        var result = FormatSpecifierHelper.GetIndicesWithFormatSpecifiers("{0}#{1}");

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetIndicesWithFormatSpecifiers_NullOrEmpty_ReturnsEmptySet(string? format)
    {
        var result = FormatSpecifierHelper.GetIndicesWithFormatSpecifiers(format);

        result.Should().BeEmpty();
    }

    #endregion

    #region Format specifiers with colons (Requirements 3.1, 5.4)

    [Fact]
    public void HasAnyFormatSpecifier_SpecifierWithColons_ReturnsTrue()
    {
        // Format specifier itself contains colons (e.g., HH:mm:ss)
        var result = FormatSpecifierHelper.HasAnyFormatSpecifier("{0:HH:mm:ss}");

        result.Should().BeTrue();
    }

    [Fact]
    public void HasFormatSpecifierForIndex_SpecifierWithColons_ReturnsTrue()
    {
        var result = FormatSpecifierHelper.HasFormatSpecifierForIndex("{0:HH:mm:ss}#{1}", 0);

        result.Should().BeTrue();
    }

    [Fact]
    public void GetIndicesWithFormatSpecifiers_SpecifierWithColons_ReturnsCorrectIndex()
    {
        var result = FormatSpecifierHelper.GetIndicesWithFormatSpecifiers("{0:HH:mm:ss}#{1}");

        result.Should().BeEquivalentTo(new HashSet<int> { 0 });
    }

    [Fact]
    public void HasFormatSpecifierForIndex_SpecifierWithMultipleColons_CorrectIndex()
    {
        // Verifies that colons in the format specifier don't confuse index parsing
        var result = FormatSpecifierHelper.HasFormatSpecifierForIndex("{0:HH:mm:ss}#{1:yyyy-MM-dd}", 1);

        result.Should().BeTrue();
    }

    #endregion
}
