using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for index name derivation to valid C# identifiers.
/// 
/// **Feature: enhanced-index-table-generation, Property 2: Index name derivation to valid C# identifier**
/// **Validates: Requirements 1.3, 6.1**
/// </summary>
public class IndexNameDerivationPropertyTests
{
    /// <summary>
    /// Property: For any valid DynamoDB index name without a custom Name property,
    /// the derived property name SHALL be a valid C# identifier.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DerivedPropertyName_ShouldBeValidCSharpIdentifier()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize to valid DynamoDB index name
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                
                // Act
                var derivedName = EntityAnalyzer.ConvertToPascalCase(cleanIndexName);
                
                // Assert - result should be a valid C# identifier
                return IsValidCSharpIdentifier(derivedName);
            });
    }

    /// <summary>
    /// Property: For any DynamoDB index name, the derived property name SHALL use PascalCase.
    /// The first character should be uppercase.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DerivedPropertyName_ShouldStartWithUppercase()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize to valid DynamoDB index name
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                
                // Act
                var derivedName = EntityAnalyzer.ConvertToPascalCase(cleanIndexName);
                
                // Assert - first character should be uppercase letter
                return !string.IsNullOrEmpty(derivedName) && 
                       char.IsUpper(derivedName[0]) && 
                       char.IsLetter(derivedName[0]);
            });
    }

    /// <summary>
    /// Property: For any DynamoDB index name containing hyphens,
    /// the derived property name SHALL not contain hyphens.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DerivedPropertyName_ShouldNotContainHyphens()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - create index name with hyphens
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var indexNameWithHyphens = cleanIndexName + "-suffix";
                
                // Act
                var derivedName = EntityAnalyzer.ConvertToPascalCase(indexNameWithHyphens);
                
                // Assert - result should not contain hyphens
                return !derivedName.Contains('-');
            });
    }

    /// <summary>
    /// Property: For any DynamoDB index name containing underscores,
    /// the derived property name SHALL not contain underscores (except as first char if needed).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DerivedPropertyName_ShouldNotContainUnderscores()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - create index name with underscores
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var indexNameWithUnderscores = cleanIndexName + "_suffix";
                
                // Act
                var derivedName = EntityAnalyzer.ConvertToPascalCase(indexNameWithUnderscores);
                
                // Assert - result should not contain underscores
                return !derivedName.Contains('_');
            });
    }

    /// <summary>
    /// Property: For any DynamoDB index name, the derived property name SHALL only contain
    /// alphanumeric characters (valid C# identifier characters).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DerivedPropertyName_ShouldOnlyContainAlphanumericCharacters()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize to valid DynamoDB index name
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                
                // Act
                var derivedName = EntityAnalyzer.ConvertToPascalCase(cleanIndexName);
                
                // Assert - all characters should be alphanumeric
                return derivedName.All(c => char.IsLetterOrDigit(c));
            });
    }

    /// <summary>
    /// Property: For any DynamoDB index name starting with a digit,
    /// the derived property name SHALL start with a letter (prepend "Index").
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DerivedPropertyName_ShouldHandleNumericPrefix()
    {
        return Prop.ForAll(
            Arb.From<PositiveInt>(),
            number =>
            {
                // Arrange - create index name starting with digit
                var indexName = $"{number.Get}index";
                
                // Act
                var derivedName = EntityAnalyzer.ConvertToPascalCase(indexName);
                
                // Assert - result should start with a letter
                return !string.IsNullOrEmpty(derivedName) && char.IsLetter(derivedName[0]);
            });
    }

    /// <summary>
    /// Property: Empty or whitespace-only index names should return a default value.
    /// </summary>
    [Fact]
    public void DerivedPropertyName_ShouldReturnDefaultForEmptyInput()
    {
        // Act & Assert
        Assert.Equal("Index", EntityAnalyzer.ConvertToPascalCase(""));
        Assert.Equal("Index", EntityAnalyzer.ConvertToPascalCase(null!));
    }

    /// <summary>
    /// Specific examples to verify PascalCase conversion behavior.
    /// </summary>
    [Theory]
    [InlineData("gsi1", "Gsi1")]
    [InlineData("status-index", "StatusIndex")]
    [InlineData("user_email_index", "UserEmailIndex")]
    [InlineData("GSI-1", "Gsi1")]
    [InlineData("my-cool-index", "MyCoolIndex")]
    [InlineData("lsi1", "Lsi1")]
    [InlineData("created_at_index", "CreatedAtIndex")]
    public void DerivedPropertyName_ShouldMatchExpectedOutput(string indexName, string expected)
    {
        // Act
        var result = EntityAnalyzer.ConvertToPascalCase(indexName);
        
        // Assert
        Assert.Equal(expected, result);
    }

    private static string SanitizeToDynamoDbIndexName(string name)
    {
        // DynamoDB index names: 3-255 characters, alphanumeric, hyphens, underscores
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "gsi1";
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static bool IsValidCSharpIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // Must start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        // Rest must be letters, digits, or underscores
        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
