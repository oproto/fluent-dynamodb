using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for key-derived operations.
/// **Feature: api-enhancements-v0.9, Property 3: Partition key properties support equality operations**
/// **Feature: api-enhancements-v0.9, Property 4: Sort key properties support range operations**
/// **Validates: Requirements 3.3, 3.4**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
public class KeyDerivedOperationsPropertyTests
{
    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 3: Partition key properties support equality operations**
    /// 
    /// For any property marked with [PartitionKey], the generated PropertyMetadata 
    /// SHALL include DynamoDbOperation.Equals in SupportedOperations.
    /// 
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartitionKeyProperty_SupportsEqualityOperations()
    {
        return Prop.ForAll(
            GenerateValidPropertyName(),
            GenerateValidAttributeName(),
            (propertyName, attributeName) =>
            {
                // Arrange - Create an entity with a partition key property
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    Namespace = "TestNamespace",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = propertyName,
                            AttributeName = attributeName,
                            PropertyType = "string",
                            IsPartitionKey = true
                        }
                    }
                };

                // Act - Generate the entity implementation
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert - Verify that SupportedOperations includes Equals for partition key
                // The generated code should contain: SupportedOperations = new[] { DynamoDbOperation.Equals }
                var hasEqualsOperation = result.Contains("DynamoDbOperation.Equals");
                
                // Partition keys should NOT have range operations
                var hasBeginsWith = result.Contains("DynamoDbOperation.BeginsWith");
                var hasBetween = result.Contains("DynamoDbOperation.Between");
                var hasGreaterThan = result.Contains("DynamoDbOperation.GreaterThan");
                var hasLessThan = result.Contains("DynamoDbOperation.LessThan");
                
                // For partition key, we expect only Equals, not range operations
                var onlyEqualsForPk = hasEqualsOperation && !hasBeginsWith && !hasBetween && !hasGreaterThan && !hasLessThan;

                return onlyEqualsForPk.ToProperty()
                    .Label($"Partition key '{propertyName}' should only support Equals operation. " +
                           $"HasEquals: {hasEqualsOperation}, HasBeginsWith: {hasBeginsWith}, " +
                           $"HasBetween: {hasBetween}, HasGreaterThan: {hasGreaterThan}, HasLessThan: {hasLessThan}");
            });
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 4: Sort key properties support range operations**
    /// 
    /// For any property marked with [SortKey], the generated PropertyMetadata 
    /// SHALL include [Equals, BeginsWith, Between, GreaterThan, LessThan] in SupportedOperations.
    /// 
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortKeyProperty_SupportsRangeOperations()
    {
        return Prop.ForAll(
            GenerateValidPropertyName(),
            GenerateValidAttributeName(),
            (propertyName, attributeName) =>
            {
                // Arrange - Create an entity with partition key and sort key
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    Namespace = "TestNamespace",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Id",
                            AttributeName = "pk",
                            PropertyType = "string",
                            IsPartitionKey = true
                        },
                        new PropertyModel
                        {
                            PropertyName = propertyName,
                            AttributeName = attributeName,
                            PropertyType = "string",
                            IsSortKey = true
                        }
                    }
                };

                // Act - Generate the entity implementation
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert - Verify that SupportedOperations includes all range operations for sort key
                // The generated code should contain all these operations for the sort key property
                var hasEqualsOperation = result.Contains("DynamoDbOperation.Equals");
                var hasBeginsWith = result.Contains("DynamoDbOperation.BeginsWith");
                var hasBetween = result.Contains("DynamoDbOperation.Between");
                var hasGreaterThan = result.Contains("DynamoDbOperation.GreaterThan");
                var hasLessThan = result.Contains("DynamoDbOperation.LessThan");
                
                // Sort key should have all range operations
                var hasAllRangeOperations = hasEqualsOperation && hasBeginsWith && hasBetween && hasGreaterThan && hasLessThan;

                return hasAllRangeOperations.ToProperty()
                    .Label($"Sort key '{propertyName}' should support all range operations. " +
                           $"HasEquals: {hasEqualsOperation}, HasBeginsWith: {hasBeginsWith}, " +
                           $"HasBetween: {hasBetween}, HasGreaterThan: {hasGreaterThan}, HasLessThan: {hasLessThan}");
            });
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 3: Partition key properties support equality operations**
    /// 
    /// Direct test using source code analysis to verify partition key operations.
    /// 
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void PartitionKeyProperty_GeneratesEqualsOperation_InMetadata()
    {
        // Arrange - Create an entity with a partition key
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "UserId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                }
            }
        };

        // Act - Generate the entity implementation
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify the generated code contains the correct SupportedOperations
        result.Should().Contain("SupportedOperations = new[] { DynamoDbOperation.Equals }",
            "Partition key should only support Equals operation");
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 4: Sort key properties support range operations**
    /// 
    /// Direct test using source code analysis to verify sort key operations.
    /// 
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Fact]
    public void SortKeyProperty_GeneratesRangeOperations_InMetadata()
    {
        // Arrange - Create an entity with partition key and sort key
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "UserId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Timestamp",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            }
        };

        // Act - Generate the entity implementation
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify the generated code contains all range operations for sort key
        result.Should().Contain("DynamoDbOperation.Equals", "Sort key should support Equals");
        result.Should().Contain("DynamoDbOperation.BeginsWith", "Sort key should support BeginsWith");
        result.Should().Contain("DynamoDbOperation.Between", "Sort key should support Between");
        result.Should().Contain("DynamoDbOperation.GreaterThan", "Sort key should support GreaterThan");
        result.Should().Contain("DynamoDbOperation.LessThan", "Sort key should support LessThan");
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 3 &amp; 4: Key-derived operations**
    /// 
    /// Verifies that when [Queryable] is NOT used, operations are still derived from key attributes.
    /// This ensures backward compatibility and that the deprecation doesn't break functionality.
    /// 
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// </summary>
    [Fact]
    public void EntityWithoutQueryable_StillDerivesOperationsFromKeys()
    {
        // Arrange - Create an entity without [Queryable] attribute
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "PartitionId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    Queryable = null // No [Queryable] attribute
                },
                new PropertyModel
                {
                    PropertyName = "SortId",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    Queryable = null // No [Queryable] attribute
                },
                new PropertyModel
                {
                    PropertyName = "Data",
                    AttributeName = "data",
                    PropertyType = "string",
                    IsPartitionKey = false,
                    IsSortKey = false,
                    Queryable = null // No [Queryable] attribute
                }
            }
        };

        // Act - Generate the entity implementation
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify operations are derived from key attributes
        // Partition key: only Equals
        // Sort key: Equals, BeginsWith, Between, GreaterThan, LessThan
        // Non-key: Equals, GreaterThan, LessThan, Contains, In
        
        result.Should().Contain("SupportedOperations = new[] { DynamoDbOperation.Equals }",
            "Partition key should have Equals operation derived from [PartitionKey]");
        
        result.Should().Contain("DynamoDbOperation.BeginsWith",
            "Sort key should have BeginsWith operation derived from [SortKey]");
        
        result.Should().Contain("DynamoDbOperation.Contains",
            "Non-key property should have Contains operation for filter expressions");
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 3: Partition key properties support equality operations**
    /// 
    /// Verifies that deprecated [Queryable] attribute still works for backward compatibility,
    /// but operations are overridden by key attributes when both are present.
    /// 
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public void QueryableWithKeyAttribute_KeyAttributeTakesPrecedence()
    {
        // Arrange - Create an entity with both [Queryable] and [PartitionKey]
        // The [Queryable] has custom operations, but [PartitionKey] should derive Equals
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "UserId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    // [Queryable] with explicit operations - but since it has no operations set,
                    // the key attribute derivation should take precedence
                    Queryable = new QueryableModel
                    {
                        SupportedOperations = Array.Empty<DynamoDbOperation>()
                    }
                }
            }
        };

        // Act - Generate the entity implementation
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify that partition key operations are used (Equals only)
        // When Queryable has no operations, key attribute derivation takes precedence
        result.Should().Contain("SupportedOperations = new[] { DynamoDbOperation.Equals }",
            "Partition key should derive Equals operation even when [Queryable] is present with no operations");
    }

    /// <summary>
    /// Generates valid C# property names for testing.
    /// </summary>
    private static Arbitrary<string> GenerateValidPropertyName()
    {
        return Arb.From(
            from length in Gen.Choose(3, 20)
            from firstChar in Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 
                                           'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z')
            from restChars in Gen.ArrayOf(length - 1, Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
            select firstChar + new string(restChars)
        );
    }

    /// <summary>
    /// Generates valid DynamoDB attribute names for testing.
    /// </summary>
    private static Arbitrary<string> GenerateValidAttributeName()
    {
        return Arb.From(
            from length in Gen.Choose(3, 20)
            from chars in Gen.ArrayOf(length, Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                '_', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
            let name = new string(chars)
            where !char.IsDigit(name[0]) // Attribute names shouldn't start with a digit
            select name
        );
    }
}
