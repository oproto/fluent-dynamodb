using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Requests;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Property-based tests for PartiQLRequestBuilder.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// 
/// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
/// **Validates: Requirements 3.2, 3.3, 3.4**
/// </summary>
public class PartiQLRequestBuilderPropertyTests
{
    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any PartiQL statement with format placeholders, all placeholders should be replaced
    /// with ? for PartiQL positional parameters.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_FormatPlaceholders_AreReplacedWithQuestionMarks()
    {
        return Prop.ForAll(
            GenerateStatementWithPlaceholders(),
            input =>
            {
                // Act - create request
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement(input.Statement, input.Parameters);
                var request = builder.ToRequest();
                
                // Assert - no format placeholders should remain
                var noPlaceholdersRemain = !System.Text.RegularExpressions.Regex.IsMatch(
                    request.Statement, @"\{\d+\}");
                
                // Count question marks should equal number of placeholders used
                var questionMarkCount = request.Statement.Count(c => c == '?');
                var expectedCount = input.PlaceholderCount;
                var correctQuestionMarkCount = questionMarkCount == expectedCount;
                
                return (noPlaceholdersRemain && correctQuestionMarkCount).ToProperty()
                    .Label($"Format placeholders should be replaced with ?. " +
                           $"NoPlaceholdersRemain: {noPlaceholdersRemain}, " +
                           $"QuestionMarkCount: {questionMarkCount}, Expected: {expectedCount}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any PartiQL statement with parameters, the number of parameters in the request
    /// should match the number of placeholders used in the statement.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_ParameterCount_MatchesPlaceholderCount()
    {
        return Prop.ForAll(
            GenerateStatementWithPlaceholders(),
            input =>
            {
                // Act - create request
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement(input.Statement, input.Parameters);
                var request = builder.ToRequest();
                
                // Assert - parameter count should match placeholder count
                var parameterCount = request.Parameters?.Count ?? 0;
                var expectedCount = input.PlaceholderCount;
                var countsMatch = parameterCount == expectedCount;
                
                return countsMatch.ToProperty()
                    .Label($"Parameter count should match placeholder count. " +
                           $"ParameterCount: {parameterCount}, Expected: {expectedCount}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any string parameter, the converted AttributeValue should have the S property set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_StringParameter_ConvertsToStringAttributeValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            stringValue =>
            {
                // Act - create request with string parameter
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement("SELECT * FROM Test WHERE pk = {0}", stringValue.Get);
                var request = builder.ToRequest();
                
                // Assert - parameter should be S type
                var parameter = request.Parameters?.FirstOrDefault();
                var isStringType = parameter?.S == stringValue.Get;
                
                return isStringType.ToProperty()
                    .Label($"String parameter should convert to S type. " +
                           $"IsStringType: {isStringType}, Value: {parameter?.S}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any integer parameter, the converted AttributeValue should have the N property set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_IntegerParameter_ConvertsToNumberAttributeValue()
    {
        return Prop.ForAll(
            Arb.Default.Int32(),
            intValue =>
            {
                // Act - create request with integer parameter
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement("SELECT * FROM Test WHERE count = {0}", intValue);
                var request = builder.ToRequest();
                
                // Assert - parameter should be N type
                var parameter = request.Parameters?.FirstOrDefault();
                var isNumberType = parameter?.N == intValue.ToString();
                
                return isNumberType.ToProperty()
                    .Label($"Integer parameter should convert to N type. " +
                           $"IsNumberType: {isNumberType}, Value: {parameter?.N}, Expected: {intValue}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any boolean parameter, the converted AttributeValue should have the BOOL property set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_BooleanParameter_ConvertsToBoolAttributeValue()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            boolValue =>
            {
                // Act - create request with boolean parameter
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement("SELECT * FROM Test WHERE active = {0}", boolValue);
                var request = builder.ToRequest();
                
                // Assert - parameter should be BOOL type
                var parameter = request.Parameters?.FirstOrDefault();
                var isBoolType = parameter?.IsBOOLSet == true && parameter.BOOL == boolValue;
                
                return isBoolType.ToProperty()
                    .Label($"Boolean parameter should convert to BOOL type. " +
                           $"IsBoolType: {isBoolType}, Value: {parameter?.BOOL}, Expected: {boolValue}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any null parameter, the converted AttributeValue should have the NULL property set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_NullParameter_ConvertsToNullAttributeValue()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Act - create request with null parameter
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement("SELECT * FROM Test WHERE value = {0}", new object?[] { null });
                var request = builder.ToRequest();
                
                // Assert - parameter should be NULL type
                var parameter = request.Parameters?.FirstOrDefault();
                var isNullType = parameter?.NULL == true;
                
                return isNullType.ToProperty()
                    .Label($"Null parameter should convert to NULL type. IsNullType: {isNullType}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any DateTime parameter with :o format specifier, the converted AttributeValue 
    /// should have the S property set with ISO 8601 format.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_DateTimeWithFormatSpecifier_ConvertsToFormattedString()
    {
        return Prop.ForAll(
            Arb.Default.DateTime(),
            dateTime =>
            {
                // Act - create request with DateTime parameter and format specifier
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement("SELECT * FROM Test WHERE created > {0:o}", dateTime);
                var request = builder.ToRequest();
                
                // Assert - parameter should be S type with ISO 8601 format
                var parameter = request.Parameters?.FirstOrDefault();
                var expectedValue = dateTime.ToString("o");
                var isCorrectFormat = parameter?.S == expectedValue;
                
                return isCorrectFormat.ToProperty()
                    .Label($"DateTime with :o format should convert to ISO 8601 string. " +
                           $"IsCorrectFormat: {isCorrectFormat}, Value: {parameter?.S}, Expected: {expectedValue}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any statement without placeholders, the statement should remain unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_StatementWithoutPlaceholders_RemainsUnchanged()
    {
        return Prop.ForAll(
            GenerateStatementWithoutPlaceholders(),
            statement =>
            {
                // Act - create request without parameters
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement(statement);
                var request = builder.ToRequest();
                
                // Assert - statement should be unchanged
                var isUnchanged = request.Statement == statement;
                var noParameters = request.Parameters?.Count == 0 || request.Parameters == null;
                
                return (isUnchanged && noParameters).ToProperty()
                    .Label($"Statement without placeholders should remain unchanged. " +
                           $"IsUnchanged: {isUnchanged}, NoParameters: {noParameters}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 4: PartiQL hydration consistency**
    /// **Validates: Requirements 3.2, 3.3, 3.4**
    /// 
    /// For any Guid parameter, the converted AttributeValue should have the S property set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartiQL_GuidParameter_ConvertsToStringAttributeValue()
    {
        return Prop.ForAll(
            Arb.Default.Guid(),
            guidValue =>
            {
                // Act - create request with Guid parameter
                var builder = new PartiQLRequestBuilder<TestEntity>(
                    NSubstitute.Substitute.For<Amazon.DynamoDBv2.IAmazonDynamoDB>());
                builder.WithStatement("SELECT * FROM Test WHERE id = {0}", guidValue);
                var request = builder.ToRequest();
                
                // Assert - parameter should be S type with Guid string
                var parameter = request.Parameters?.FirstOrDefault();
                var isCorrectValue = parameter?.S == guidValue.ToString();
                
                return isCorrectValue.ToProperty()
                    .Label($"Guid parameter should convert to S type. " +
                           $"IsCorrectValue: {isCorrectValue}, Value: {parameter?.S}, Expected: {guidValue}");
            });
    }

    #region Test Entity

    /// <summary>
    /// Simple test entity for property tests.
    /// </summary>
    private class TestEntity : Oproto.FluentDynamoDb.Entities.IDynamoDbEntity
    {
        public string Pk { get; set; } = string.Empty;
        public string Sk { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
            where TSelf : Oproto.FluentDynamoDb.Entities.IDynamoDbEntity
        {
            if (entity is not TestEntity testEntity)
                throw new InvalidOperationException();
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity.Pk },
                ["sk"] = new AttributeValue { S = testEntity.Sk }
            };
        }

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : Oproto.FluentDynamoDb.Entities.IDynamoDbEntity => ToDynamoDb(entity, options);

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
            where TSelf : Oproto.FluentDynamoDb.Entities.IReadOnlyEntity
        {
            var entity = new TestEntity
            {
                Pk = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Sk = item.TryGetValue("sk", out var sk) ? sk.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
            where TSelf : Oproto.FluentDynamoDb.Entities.IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items[0], options);
        }

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
            => item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;

        public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : Oproto.FluentDynamoDb.Entities.IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));

        public static Oproto.FluentDynamoDb.Metadata.EntityMetadata GetEntityMetadata()
            => new Oproto.FluentDynamoDb.Metadata.EntityMetadata
            {
                TableName = "Test",
                Properties = Array.Empty<Oproto.FluentDynamoDb.Metadata.PropertyMetadata>(),
                Indexes = Array.Empty<Oproto.FluentDynamoDb.Metadata.IndexMetadata>(),
                Relationships = Array.Empty<Oproto.FluentDynamoDb.Metadata.RelationshipMetadata>()
            };
    }

    #endregion

    #region Generators

    /// <summary>
    /// Input record for statement with placeholders test.
    /// </summary>
    private record StatementWithPlaceholdersInput(string Statement, object[] Parameters, int PlaceholderCount);

    /// <summary>
    /// Generates a PartiQL statement with format placeholders and matching parameters.
    /// </summary>
    private static Arbitrary<StatementWithPlaceholdersInput> GenerateStatementWithPlaceholders()
    {
        return Arb.From(
            from paramCount in Gen.Choose(1, 5)
            from parameters in Gen.ListOf(paramCount, GenerateRandomParameter())
            let statement = GenerateStatementWithNPlaceholders(paramCount)
            select new StatementWithPlaceholdersInput(statement, parameters.ToArray(), paramCount));
    }

    /// <summary>
    /// Generates a statement with N placeholders.
    /// </summary>
    private static string GenerateStatementWithNPlaceholders(int count)
    {
        var conditions = Enumerable.Range(0, count)
            .Select(i => $"field{i} = {{{i}}}")
            .ToArray();
        return $"SELECT * FROM Test WHERE {string.Join(" AND ", conditions)}";
    }

    /// <summary>
    /// Generates a random parameter value.
    /// </summary>
    private static Gen<object> GenerateRandomParameter()
    {
        return Gen.OneOf(
            Arb.Default.NonEmptyString().Generator.Select(s => (object)s.Get),
            Arb.Default.Int32().Generator.Select(i => (object)i),
            Arb.Default.Bool().Generator.Select(b => (object)b),
            Arb.Default.Decimal().Generator.Select(d => (object)d)
        );
    }

    /// <summary>
    /// Generates a PartiQL statement without placeholders.
    /// </summary>
    private static Arbitrary<string> GenerateStatementWithoutPlaceholders()
    {
        return Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from fieldName in Arb.Default.NonEmptyString().Generator
            select $"SELECT * FROM {tableName.Get.Replace("{", "").Replace("}", "")} WHERE {fieldName.Get.Replace("{", "").Replace("}", "")} = 'value'");
    }

    #endregion
}
