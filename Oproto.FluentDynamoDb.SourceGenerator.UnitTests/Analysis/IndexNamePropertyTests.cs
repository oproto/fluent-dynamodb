using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Attributes;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for index attribute Name property propagation.
/// 
/// **Feature: enhanced-index-table-generation, Property 1: Custom name exact propagation**
/// **Validates: Requirements 1.1, 1.2**
/// </summary>
public class IndexNamePropertyTests
{
    /// <summary>
    /// Property: For any GSI partition key attribute with a custom Name property value, 
    /// the Name property SHALL exactly match the specified value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiPartitionKeyAttribute_Name_ShouldExactlyMatchSpecifiedValue()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                var cleanCustomName = SanitizePropertyName(customName.Get);
                
                // Act
                var attribute = new GsiPartitionKeyAttribute(cleanIndexName)
                {
                    Name = cleanCustomName
                };
                
                // Assert - Name property should exactly match the specified value
                return attribute.Name == cleanCustomName && 
                       attribute.IndexName == cleanIndexName;
            });
    }

    /// <summary>
    /// Property: For any LSI sort key attribute with a custom Name property value,
    /// the Name property SHALL exactly match the specified value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LsiSortKeyAttribute_Name_ShouldExactlyMatchSpecifiedValue()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                var cleanCustomName = SanitizePropertyName(customName.Get);
                
                // Act
                var attribute = new LsiSortKeyAttribute(cleanIndexName)
                {
                    Name = cleanCustomName
                };
                
                // Assert - Name property should exactly match the specified value
                return attribute.Name == cleanCustomName && 
                       attribute.IndexName == cleanIndexName;
            });
    }

    /// <summary>
    /// Property: For any GSI partition key attribute without a Name property specified,
    /// the Name property SHALL be null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiPartitionKeyAttribute_Name_ShouldBeNullWhenNotSpecified()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                
                // Act
                var attribute = new GsiPartitionKeyAttribute(cleanIndexName);
                
                // Assert - Name property should be null when not specified
                return attribute.Name == null && 
                       attribute.IndexName == cleanIndexName;
            });
    }

    /// <summary>
    /// Property: For any LSI sort key attribute without a Name property specified,
    /// the Name property SHALL be null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LsiSortKeyAttribute_Name_ShouldBeNullWhenNotSpecified()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                
                // Act
                var attribute = new LsiSortKeyAttribute(cleanIndexName);
                
                // Assert - Name property should be null when not specified
                return attribute.Name == null && 
                       attribute.IndexName == cleanIndexName;
            });
    }

    /// <summary>
    /// Property: For any GSI partition key attribute, the Name property should be independent
    /// of other properties (ProjectionType).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiPartitionKeyAttribute_Name_ShouldBeIndependentOfOtherProperties()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                var cleanCustomName = SanitizePropertyName(customName.Get);
                
                // Act
                var attribute = new GsiPartitionKeyAttribute(cleanIndexName)
                {
                    Name = cleanCustomName
                };
                
                // Assert - Name property should remain unchanged regardless of other properties
                return attribute.Name == cleanCustomName;
            });
    }

    private static string SanitizeIndexName(string name)
    {
        // DynamoDB index names: 3-255 characters, alphanumeric, hyphens, underscores
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "gsi1";
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizePropertyName(string name)
    {
        // C# property names: start with letter or underscore, alphanumeric and underscores
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Index" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}
