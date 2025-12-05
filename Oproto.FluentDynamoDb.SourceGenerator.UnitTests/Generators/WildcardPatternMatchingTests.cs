using System.Text.RegularExpressions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for wildcard pattern matching edge cases in MapperGenerator.
/// 
/// **Validates: Requirements 4.1, 4.2, 4.3**
/// </summary>
[Trait("Category", "Unit")]
public class WildcardPatternMatchingTests
{
    /// <summary>
    /// Test pattern "INVOICE#*#LINE#*" matches "INVOICE#INV-001#LINE#1"
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Fact]
    public void Pattern_InvoiceLine_MatchesValidSortKey()
    {
        // Arrange
        var pattern = "INVOICE#*#LINE#*";
        var sortKey = "INVOICE#INV-001#LINE#1";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeTrue("pattern 'INVOICE#*#LINE#*' should match 'INVOICE#INV-001#LINE#1'");
    }

    /// <summary>
    /// Test pattern "INVOICE#*#LINE#*" does not match "INVOICE#INV-001"
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public void Pattern_InvoiceLine_DoesNotMatchIncompleteSortKey()
    {
        // Arrange
        var pattern = "INVOICE#*#LINE#*";
        var sortKey = "INVOICE#INV-001";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeFalse("pattern 'INVOICE#*#LINE#*' should not match 'INVOICE#INV-001' (missing LINE segment)");
    }

    /// <summary>
    /// Test pattern "INVOICE#*" matches "INVOICE#INV-001" (single segment after prefix)
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void Pattern_SingleWildcard_MatchesSingleSegment()
    {
        // Arrange
        var pattern = "INVOICE#*";
        var sortKey = "INVOICE#INV-001";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeTrue("pattern 'INVOICE#*' should match 'INVOICE#INV-001'");
    }

    /// <summary>
    /// Test pattern "INVOICE#*" does NOT match "INVOICE#INV-001#LINE#1" (multiple segments)
    /// This is the correct behavior for segment-based wildcard matching.
    /// Each * matches characters within a single segment (between # delimiters).
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void Pattern_SingleWildcard_DoesNotMatchMultipleSegments()
    {
        // Arrange
        var pattern = "INVOICE#*";
        var sortKey = "INVOICE#INV-001#LINE#1";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        // The wildcard matches [^#]* which means it should NOT match across # delimiters
        // This is intentional: segment-based matching allows precise entity type discrimination
        matches.Should().BeFalse("pattern 'INVOICE#*' should not match 'INVOICE#INV-001#LINE#1' because * only matches within a segment");
    }

    /// <summary>
    /// Test exact pattern matching (no wildcards)
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void Pattern_NoWildcard_MatchesExactly()
    {
        // Arrange
        var pattern = "INVOICE#META";
        var sortKey = "INVOICE#META";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeTrue("exact pattern should match exact sort key");
    }

    /// <summary>
    /// Test exact pattern does not match different sort key
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public void Pattern_NoWildcard_DoesNotMatchDifferentSortKey()
    {
        // Arrange
        var pattern = "INVOICE#META";
        var sortKey = "INVOICE#DATA";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeFalse("exact pattern should not match different sort key");
    }

    /// <summary>
    /// Test wildcard at end matches empty string
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void Pattern_WildcardAtEnd_MatchesEmptySegment()
    {
        // Arrange
        var pattern = "INVOICE#*";
        var sortKey = "INVOICE#";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeTrue("wildcard should match empty segment");
    }

    /// <summary>
    /// Test pattern with special regex characters is properly escaped
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void Pattern_WithSpecialCharacters_IsProperlyEscaped()
    {
        // Arrange - pattern with characters that are special in regex
        var pattern = "TYPE.NAME#*";
        var sortKey = "TYPE.NAME#value";
        var wrongSortKey = "TYPExNAME#value"; // . should not match any character
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matchesCorrect = Regex.IsMatch(sortKey, regexPattern);
        var matchesWrong = Regex.IsMatch(wrongSortKey, regexPattern);
        
        // Assert
        matchesCorrect.Should().BeTrue("pattern should match when . is literal");
        matchesWrong.Should().BeFalse("pattern should not match when . is treated as regex wildcard");
    }

    /// <summary>
    /// Test multiple consecutive wildcards in pattern
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void Pattern_MultipleWildcards_MatchesCorrectly()
    {
        // Arrange
        var pattern = "A#*#B#*#C#*";
        var sortKey = "A#1#B#2#C#3";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeTrue("pattern with multiple wildcards should match corresponding segments");
    }

    /// <summary>
    /// Test that generated regex is anchored (matches entire string)
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public void Pattern_IsAnchored_DoesNotMatchPartialString()
    {
        // Arrange
        var pattern = "INVOICE#*";
        var sortKey = "PREFIX#INVOICE#001"; // Pattern appears in middle
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeFalse("pattern should be anchored and not match partial strings");
    }

    /// <summary>
    /// Test pattern with underscore delimiter
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void Pattern_WithUnderscoreDelimiter_MatchesCorrectly()
    {
        // Arrange
        var pattern = "INVOICE_*_LINE_*";
        var sortKey = "INVOICE_INV-001_LINE_1";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeTrue("pattern with underscore delimiter should match");
    }

    /// <summary>
    /// Test pattern with colon delimiter
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void Pattern_WithColonDelimiter_MatchesCorrectly()
    {
        // Arrange
        var pattern = "INVOICE:*:LINE:*";
        var sortKey = "INVOICE:INV-001:LINE:1";
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeTrue("pattern with colon delimiter should match");
    }

    /// <summary>
    /// Test that delimiter inference works correctly
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void InferDelimiter_ReturnsCorrectDelimiter()
    {
        // Assert various delimiter patterns
        MapperGenerator.InferDelimiterFromPattern("INVOICE#*").Should().Be("#");
        MapperGenerator.InferDelimiterFromPattern("INVOICE_*").Should().Be("_");
        MapperGenerator.InferDelimiterFromPattern("INVOICE:*").Should().Be(":");
        MapperGenerator.InferDelimiterFromPattern("INVOICE|*").Should().Be("|");
        MapperGenerator.InferDelimiterFromPattern("*").Should().Be("#"); // Default when no delimiter before *
    }

    /// <summary>
    /// Test that underscore delimiter doesn't match hash delimiter
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public void Pattern_WithUnderscoreDelimiter_DoesNotMatchHashDelimiter()
    {
        // Arrange
        var pattern = "INVOICE_*_LINE_*";
        var sortKey = "INVOICE#INV-001#LINE#1"; // Wrong delimiter
        
        // Act
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var matches = Regex.IsMatch(sortKey, regexPattern);
        
        // Assert
        matches.Should().BeFalse("pattern with underscore delimiter should not match hash-delimited sort key");
    }
}
