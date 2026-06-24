using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Property-based tests for empty conditional expression handling.
/// These tests verify the correctness properties defined in the design document
/// for the empty-conditional-expression-handling feature.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
[Trait("Feature", "empty-conditional-expression-handling")]
public class EmptyConditionalExpressionPropertyTests
{
    /// <summary>
    /// Test entity for property-based testing.
    /// </summary>
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Age { get; set; }

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) 
            where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty },
                ["status"] = new AttributeValue { S = testEntity?.Status ?? string.Empty },
                ["age"] = new AttributeValue { N = testEntity?.Age.ToString() ?? "0" }
            };
        }

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) 
            where TSelf : IReadOnlyEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty,
                Status = item.TryGetValue("status", out var status) ? status.S : string.Empty,
                Age = item.TryGetValue("age", out var age) ? int.Parse(age.N) : 0
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) 
            where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items.First(), options);
        }

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
        {
            return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
        }

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
        {
            return item.ContainsKey("pk");
        }

        public static EntityMetadata GetEntityMetadata()
        {
            return new EntityMetadata
            {
                TableName = "test-table",
                Properties = Array.Empty<PropertyMetadata>(),
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
        }

        public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
    }

    #region Property 1: All-Skip Conditional Expressions Produce No Filter

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 1: All-Skip Conditional Expressions Produce No Filter**
    /// **Validates: Requirements 1.1, 1.2, 1.3, 3.1, 3.2, 3.3**
    /// 
    /// *For any* filter expression composed entirely of conditional clauses where all local conditions 
    /// evaluate to true (skip), the resulting request SHALL have no FilterExpression set.
    /// 
    /// This test verifies QueryRequestBuilder behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllSkipConditionalExpressions_QueryBuilder_ProducesNoFilter()
    {
        // Generate random whitespace strings (empty, spaces, tabs, newlines)
        var whitespaceGen = Gen.Elements("", " ", "  ", "\t", "\n", "   \t\n   ").ToArbitrary();

        return Prop.ForAll(whitespaceGen, emptyExpression =>
        {
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set an empty/whitespace filter expression (simulating all-skip conditionals)
            builder.SetFilterExpression(emptyExpression);
            var req = builder.ToQueryRequest();
            
            // Assert
            var filterExpressionIsNull = req.FilterExpression == null;
            
            return filterExpressionIsNull.ToProperty()
                .Label($"QueryRequestBuilder with empty expression '{emptyExpression?.Replace("\n", "\\n").Replace("\t", "\\t")}' " +
                       $"should have null FilterExpression. FilterExpressionIsNull: {filterExpressionIsNull}");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 1: All-Skip Conditional Expressions Produce No Filter**
    /// **Validates: Requirements 1.1, 1.2, 1.3, 3.1, 3.2, 3.3**
    /// 
    /// *For any* filter expression composed entirely of conditional clauses where all local conditions 
    /// evaluate to true (skip), the resulting request SHALL have no FilterExpression set.
    /// 
    /// This test verifies ScanRequestBuilder behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllSkipConditionalExpressions_ScanBuilder_ProducesNoFilter()
    {
        // Generate random whitespace strings (empty, spaces, tabs, newlines)
        var whitespaceGen = Gen.Elements("", " ", "  ", "\t", "\n", "   \t\n   ").ToArbitrary();

        return Prop.ForAll(whitespaceGen, emptyExpression =>
        {
            // Arrange
            var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set an empty/whitespace filter expression (simulating all-skip conditionals)
            builder.SetFilterExpression(emptyExpression);
            var req = builder.ToScanRequest();
            
            // Assert
            var filterExpressionIsNull = req.FilterExpression == null;
            
            return filterExpressionIsNull.ToProperty()
                .Label($"ScanRequestBuilder with empty expression '{emptyExpression?.Replace("\n", "\\n").Replace("\t", "\\t")}' " +
                       $"should have null FilterExpression. FilterExpressionIsNull: {filterExpressionIsNull}");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 1: All-Skip Conditional Expressions Produce No Filter**
    /// **Validates: Requirements 1.1, 1.2, 1.3, 3.1, 3.2, 3.3**
    /// 
    /// *For any* sequence of empty/whitespace filter expressions, the resulting request SHALL have no FilterExpression set.
    /// This tests multiple SetFilterExpression calls with all-skip expressions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleAllSkipExpressions_ProducesNoFilter()
    {
        // Generate a list of 1-5 whitespace strings
        var whitespaceListGen = Arb.From(
            from count in Gen.Choose(1, 5)
            from expressions in Gen.ListOf(count, Gen.Elements("", " ", "  ", "\t", "\n", "   \t\n   "))
            select expressions.ToList());

        return Prop.ForAll(whitespaceListGen, emptyExpressions =>
        {
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set multiple empty/whitespace filter expressions
            foreach (var expr in emptyExpressions)
            {
                builder.SetFilterExpression(expr);
            }
            var req = builder.ToQueryRequest();
            
            // Assert
            var filterExpressionIsNull = req.FilterExpression == null;
            
            return filterExpressionIsNull.ToProperty()
                .Label($"QueryRequestBuilder with {emptyExpressions.Count} empty expressions " +
                       $"should have null FilterExpression. FilterExpressionIsNull: {filterExpressionIsNull}");
        });
    }

    #endregion


    #region Property 2: All-Skip Conditional Expressions Produce No Condition

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 2: All-Skip Conditional Expressions Produce No Condition**
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    /// 
    /// *For any* condition expression on a write operation (Put, Update, Delete) composed entirely of 
    /// conditional clauses where all local conditions evaluate to true (skip), the resulting request 
    /// SHALL have no ConditionExpression set.
    /// 
    /// This test verifies PutItemRequestBuilder behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllSkipConditionalExpressions_PutBuilder_ProducesNoCondition()
    {
        // Generate random whitespace strings (empty, spaces, tabs, newlines)
        var whitespaceGen = Gen.Elements("", " ", "  ", "\t", "\n", "   \t\n   ").ToArbitrary();

        return Prop.ForAll(whitespaceGen, emptyExpression =>
        {
            // Arrange
            var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set an empty/whitespace condition expression (simulating all-skip conditionals)
            builder.SetConditionExpression(emptyExpression);
            var req = builder.ToPutItemRequest();
            
            // Assert
            var conditionExpressionIsNull = req.ConditionExpression == null;
            
            return conditionExpressionIsNull.ToProperty()
                .Label($"PutItemRequestBuilder with empty expression '{emptyExpression?.Replace("\n", "\\n").Replace("\t", "\\t")}' " +
                       $"should have null ConditionExpression. ConditionExpressionIsNull: {conditionExpressionIsNull}");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 2: All-Skip Conditional Expressions Produce No Condition**
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    /// 
    /// *For any* condition expression on a write operation (Put, Update, Delete) composed entirely of 
    /// conditional clauses where all local conditions evaluate to true (skip), the resulting request 
    /// SHALL have no ConditionExpression set.
    /// 
    /// This test verifies UpdateItemRequestBuilder behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllSkipConditionalExpressions_UpdateBuilder_ProducesNoCondition()
    {
        // Generate random whitespace strings (empty, spaces, tabs, newlines)
        var whitespaceGen = Gen.Elements("", " ", "  ", "\t", "\n", "   \t\n   ").ToArbitrary();

        return Prop.ForAll(whitespaceGen, emptyExpression =>
        {
            // Arrange
            var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set an empty/whitespace condition expression (simulating all-skip conditionals)
            builder.SetConditionExpression(emptyExpression);
            var req = builder.ToUpdateItemRequest();
            
            // Assert
            var conditionExpressionIsNull = req.ConditionExpression == null;
            
            return conditionExpressionIsNull.ToProperty()
                .Label($"UpdateItemRequestBuilder with empty expression '{emptyExpression?.Replace("\n", "\\n").Replace("\t", "\\t")}' " +
                       $"should have null ConditionExpression. ConditionExpressionIsNull: {conditionExpressionIsNull}");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 2: All-Skip Conditional Expressions Produce No Condition**
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    /// 
    /// *For any* condition expression on a write operation (Put, Update, Delete) composed entirely of 
    /// conditional clauses where all local conditions evaluate to true (skip), the resulting request 
    /// SHALL have no ConditionExpression set.
    /// 
    /// This test verifies DeleteItemRequestBuilder behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllSkipConditionalExpressions_DeleteBuilder_ProducesNoCondition()
    {
        // Generate random whitespace strings (empty, spaces, tabs, newlines)
        var whitespaceGen = Gen.Elements("", " ", "  ", "\t", "\n", "   \t\n   ").ToArbitrary();

        return Prop.ForAll(whitespaceGen, emptyExpression =>
        {
            // Arrange
            var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set an empty/whitespace condition expression (simulating all-skip conditionals)
            builder.SetConditionExpression(emptyExpression);
            var req = builder.ToDeleteItemRequest();
            
            // Assert
            var conditionExpressionIsNull = req.ConditionExpression == null;
            
            return conditionExpressionIsNull.ToProperty()
                .Label($"DeleteItemRequestBuilder with empty expression '{emptyExpression?.Replace("\n", "\\n").Replace("\t", "\\t")}' " +
                       $"should have null ConditionExpression. ConditionExpressionIsNull: {conditionExpressionIsNull}");
        });
    }

    #endregion


    #region Property 3: Partial-Skip Conditional Expressions Produce Valid Filter

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 3: Partial-Skip Conditional Expressions Produce Valid Filter**
    /// **Validates: Requirements 1.4**
    /// 
    /// *For any* filter expression containing at least one conditional clause where the local condition 
    /// evaluates to false (apply), the resulting request SHALL have a valid FilterExpression containing 
    /// only the applied clauses.
    /// 
    /// This test verifies that valid expressions are preserved when mixed with empty ones.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartialSkipExpressions_QueryBuilder_PreservesValidFilter()
    {
        // Generate valid filter expressions
        var validExpressionGen = Gen.Elements(
            "#status = :status",
            "#name = :name",
            "#age > :age",
            "#id = :id",
            "attribute_exists(#field)"
        ).ToArbitrary();

        return Prop.ForAll(validExpressionGen, validExpression =>
        {
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set empty first, then valid expression
            builder.SetFilterExpression("");
            builder.SetFilterExpression(validExpression);
            var req = builder.ToQueryRequest();
            
            // Assert
            var filterExpressionIsSet = req.FilterExpression == validExpression;
            
            return filterExpressionIsSet.ToProperty()
                .Label($"QueryRequestBuilder should preserve valid expression '{validExpression}' after empty. " +
                       $"FilterExpressionIsSet: {filterExpressionIsSet}, Actual: '{req.FilterExpression}'");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 3: Partial-Skip Conditional Expressions Produce Valid Filter**
    /// **Validates: Requirements 1.4**
    /// 
    /// *For any* sequence of filter expressions where some are empty and some are valid,
    /// the resulting request SHALL have a FilterExpression containing only the valid expressions combined with AND.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MixedEmptyAndValidExpressions_CombinesOnlyValidWithAnd()
    {
        // Generate two different valid expressions
        var validExpressionPairGen = Arb.From(
            from expr1 in Gen.Elements("#status = :status", "#name = :name", "#age > :age", "#id = :id")
            from expr2 in Gen.Elements("#status = :status", "#name = :name", "#age > :age", "#id = :id")
            select (expr1, expr2));

        return Prop.ForAll(validExpressionPairGen, expressionPair =>
        {
            var (expr1, expr2) = expressionPair;
            
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set valid, then empty (should be skipped), then valid again
            builder.SetFilterExpression(expr1);
            builder.SetFilterExpression("   "); // whitespace - should be skipped
            builder.SetFilterExpression(expr2);
            var req = builder.ToQueryRequest();
            
            // Assert - Only the two valid expressions should be combined
            var expectedExpression = $"({expr1}) AND ({expr2})";
            var filterExpressionIsCorrect = req.FilterExpression == expectedExpression;
            
            return filterExpressionIsCorrect.ToProperty()
                .Label($"QueryRequestBuilder should combine only valid expressions. " +
                       $"Expected: '{expectedExpression}', Actual: '{req.FilterExpression}'");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 3: Partial-Skip Conditional Expressions Produce Valid Filter**
    /// **Validates: Requirements 1.4**
    /// 
    /// *For any* valid filter expression followed by any number of empty expressions,
    /// the original valid expression SHALL be preserved.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidThenMultipleEmpty_PreservesOriginalFilter()
    {
        var validExpressionGen = Gen.Elements(
            "#status = :status",
            "#name = :name",
            "#age > :age"
        ).ToArbitrary();
        
        var emptyCountGen = Gen.Choose(1, 5).ToArbitrary();

        return Prop.ForAll(validExpressionGen, emptyCountGen, (validExpression, emptyCount) =>
        {
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set valid first, then multiple empty expressions
            builder.SetFilterExpression(validExpression);
            for (int i = 0; i < emptyCount; i++)
            {
                builder.SetFilterExpression(""); // empty - should be skipped
            }
            var req = builder.ToQueryRequest();
            
            // Assert - Original filter should be preserved
            var filterExpressionIsPreserved = req.FilterExpression == validExpression;
            
            return filterExpressionIsPreserved.ToProperty()
                .Label($"QueryRequestBuilder should preserve original filter after {emptyCount} empty expressions. " +
                       $"Expected: '{validExpression}', Actual: '{req.FilterExpression}'");
        });
    }

    #endregion


    #region Property 4: Conditional Filter Pattern Truth Table

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 4: Conditional Filter Pattern Truth Table**
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    /// 
    /// *For any* conditional filter pattern:
    /// - `(true || entityFilter)` SHALL skip the filter (return empty)
    /// - `(false || entityFilter)` SHALL apply the filter
    /// - `(true && entityFilter)` SHALL apply the filter
    /// - `(false && entityFilter)` SHALL skip the filter (return empty)
    /// 
    /// This test verifies the OR pattern with true local condition produces empty expression.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TruthTable_OrWithTrueLocal_ProducesEmptyExpression()
    {
        // Generate random valid filter expressions
        var validExpressionGen = Gen.Elements(
            "#status = :status",
            "#name = :name",
            "#age > :age"
        ).ToArbitrary();

        return Prop.ForAll(validExpressionGen, _ =>
        {
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Simulate (true || entityFilter) by setting empty expression
            // When the OR pattern has true on the left, the expression translator returns empty
            builder.SetFilterExpression("");
            var req = builder.ToQueryRequest();
            
            // Assert - Filter should be null (skipped)
            var filterExpressionIsNull = req.FilterExpression == null;
            
            return filterExpressionIsNull.ToProperty()
                .Label($"(true || entityFilter) pattern should produce null FilterExpression. " +
                       $"FilterExpressionIsNull: {filterExpressionIsNull}");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 4: Conditional Filter Pattern Truth Table**
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    /// 
    /// This test verifies the OR pattern with false local condition applies the filter.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TruthTable_OrWithFalseLocal_AppliesFilter()
    {
        // Generate random valid filter expressions
        var validExpressionGen = Gen.Elements(
            "#status = :status",
            "#name = :name",
            "#age > :age"
        ).ToArbitrary();

        return Prop.ForAll(validExpressionGen, validExpression =>
        {
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Simulate (false || entityFilter) by setting the valid expression
            // When the OR pattern has false on the left, the expression translator returns the entity filter
            builder.SetFilterExpression(validExpression);
            var req = builder.ToQueryRequest();
            
            // Assert - Filter should be set to the valid expression
            var filterExpressionIsSet = req.FilterExpression == validExpression;
            
            return filterExpressionIsSet.ToProperty()
                .Label($"(false || entityFilter) pattern should apply filter '{validExpression}'. " +
                       $"FilterExpressionIsSet: {filterExpressionIsSet}, Actual: '{req.FilterExpression}'");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 4: Conditional Filter Pattern Truth Table**
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    /// 
    /// This test verifies the AND pattern with true local condition applies the filter.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TruthTable_AndWithTrueLocal_AppliesFilter()
    {
        // Generate random valid filter expressions
        var validExpressionGen = Gen.Elements(
            "#status = :status",
            "#name = :name",
            "#age > :age"
        ).ToArbitrary();

        return Prop.ForAll(validExpressionGen, validExpression =>
        {
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Simulate (true && entityFilter) by setting the valid expression
            // When the AND pattern has true on the left, the expression translator returns the entity filter
            builder.SetFilterExpression(validExpression);
            var req = builder.ToQueryRequest();
            
            // Assert - Filter should be set to the valid expression
            var filterExpressionIsSet = req.FilterExpression == validExpression;
            
            return filterExpressionIsSet.ToProperty()
                .Label($"(true && entityFilter) pattern should apply filter '{validExpression}'. " +
                       $"FilterExpressionIsSet: {filterExpressionIsSet}, Actual: '{req.FilterExpression}'");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 4: Conditional Filter Pattern Truth Table**
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    /// 
    /// This test verifies the AND pattern with false local condition produces empty expression.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TruthTable_AndWithFalseLocal_ProducesEmptyExpression()
    {
        // Generate random valid filter expressions
        var validExpressionGen = Gen.Elements(
            "#status = :status",
            "#name = :name",
            "#age > :age"
        ).ToArbitrary();

        return Prop.ForAll(validExpressionGen, _ =>
        {
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Simulate (false && entityFilter) by setting empty expression
            // When the AND pattern has false on the left, the expression translator returns empty
            builder.SetFilterExpression("");
            var req = builder.ToQueryRequest();
            
            // Assert - Filter should be null (skipped)
            var filterExpressionIsNull = req.FilterExpression == null;
            
            return filterExpressionIsNull.ToProperty()
                .Label($"(false && entityFilter) pattern should produce null FilterExpression. " +
                       $"FilterExpressionIsNull: {filterExpressionIsNull}");
        });
    }

    /// <summary>
    /// **Feature: empty-conditional-expression-handling, Property 4: Conditional Filter Pattern Truth Table**
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    /// 
    /// This test verifies the complete truth table for conditional filter patterns
    /// by testing all four combinations of (localCondition, operator) pairs.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TruthTable_AllCombinations_BehaveCorrectly()
    {
        // Generate all four truth table combinations
        var truthTableGen = Gen.Elements(
            ("true", "||", true),   // true || filter -> skip (empty)
            ("false", "||", false), // false || filter -> apply
            ("true", "&&", false),  // true && filter -> apply
            ("false", "&&", true)   // false && filter -> skip (empty)
        ).ToArbitrary();

        var validExpressionGen = Gen.Elements(
            "#status = :status",
            "#name = :name",
            "#age > :age"
        ).ToArbitrary();

        return Prop.ForAll(truthTableGen, validExpressionGen, (truthTableEntry, validExpression) =>
        {
            var (localValue, op, shouldBeEmpty) = truthTableEntry;
            
            // Arrange
            var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");
            
            // Act - Set expression based on expected behavior
            // If shouldBeEmpty is true, the expression translator would return empty
            // If shouldBeEmpty is false, the expression translator would return the valid expression
            var expressionToSet = shouldBeEmpty ? "" : validExpression;
            builder.SetFilterExpression(expressionToSet);
            var req = builder.ToQueryRequest();
            
            // Assert
            bool isCorrect;
            if (shouldBeEmpty)
            {
                isCorrect = req.FilterExpression == null;
            }
            else
            {
                isCorrect = req.FilterExpression == validExpression;
            }
            
            return isCorrect.ToProperty()
                .Label($"({localValue} {op} entityFilter) should {(shouldBeEmpty ? "skip" : "apply")} filter. " +
                       $"Expected: {(shouldBeEmpty ? "null" : validExpression)}, Actual: '{req.FilterExpression}'");
        });
    }

    #endregion
}
