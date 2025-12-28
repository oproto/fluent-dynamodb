using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for List&lt;DateOnly&gt; and List&lt;TimeOnly&gt; serialization in the MapperGenerator.
/// These tests verify the correctness properties defined in the design document
/// for the date-time-type-serialization feature.
/// </summary>
[Trait("Category", "Unit")]
public class DateTimeCollectionSerializationPropertyTests
{
    /// <summary>
    /// **Feature: date-time-type-serialization, Property 5: Collection Round-Trip Consistency (DateOnly)**
    /// *For any* valid List&lt;DateOnly&gt; collection, serializing it to a DynamoDB list AttributeValue 
    /// and then deserializing it back SHALL produce an equivalent collection with all elements preserved.
    /// **Validates: Requirements 4.1, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ListDateOnly_RoundTrip_ProducesEquivalentCollection()
    {
        return Prop.ForAll(
            GenerateDateOnlyList(),
            dateOnlyList =>
            {
                // Arrange: Create entity with List<DateOnly> property
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
                            PropertyName = "ImportantDates",
                            AttributeName = "important_dates",
                            PropertyType = "List<DateOnly>",
                            IsCollection = true
                        }
                    }
                };

                // Act: Generate code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: Generated code should use ISO 8601 format for collection elements
                var usesCorrectSerializationFormat = 
                    result.Contains("x.ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)");
                var usesCorrectDeserializationFormat = 
                    result.Contains("DateOnly.ParseExact(x.S, \"O\", System.Globalization.CultureInfo.InvariantCulture)");
                var serializesToList = result.Contains("L =");

                return (usesCorrectSerializationFormat && usesCorrectDeserializationFormat && serializesToList)
                    .ToProperty()
                    .Label($"List<DateOnly> count: {dateOnlyList.Count}, " +
                           $"Serialization: {usesCorrectSerializationFormat}, " +
                           $"Deserialization: {usesCorrectDeserializationFormat}, " +
                           $"ListType: {serializesToList}");
            });
    }

    /// <summary>
    /// **Feature: date-time-type-serialization, Property 5: Collection Round-Trip Consistency (TimeOnly)**
    /// *For any* valid List&lt;TimeOnly&gt; collection, serializing it to a DynamoDB list AttributeValue 
    /// and then deserializing it back SHALL produce an equivalent collection with all elements preserved.
    /// **Validates: Requirements 4.2, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ListTimeOnly_RoundTrip_ProducesEquivalentCollection()
    {
        return Prop.ForAll(
            GenerateTimeOnlyList(),
            timeOnlyList =>
            {
                // Arrange: Create entity with List<TimeOnly> property
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
                            PropertyName = "ScheduledTimes",
                            AttributeName = "scheduled_times",
                            PropertyType = "List<TimeOnly>",
                            IsCollection = true
                        }
                    }
                };

                // Act: Generate code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: Generated code should use ISO 8601 format for collection elements
                var usesCorrectSerializationFormat = 
                    result.Contains("x.ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)");
                var usesCorrectDeserializationFormat = 
                    result.Contains("TimeOnly.ParseExact(x.S, \"O\", System.Globalization.CultureInfo.InvariantCulture)");
                var serializesToList = result.Contains("L =");

                return (usesCorrectSerializationFormat && usesCorrectDeserializationFormat && serializesToList)
                    .ToProperty()
                    .Label($"List<TimeOnly> count: {timeOnlyList.Count}, " +
                           $"Serialization: {usesCorrectSerializationFormat}, " +
                           $"Deserialization: {usesCorrectDeserializationFormat}, " +
                           $"ListType: {serializesToList}");
            });
    }

    /// <summary>
    /// Verifies that the generated code for List&lt;DateOnly&gt; compiles successfully.
    /// This is a single compilation test that validates the generated code structure.
    /// </summary>
    [Fact]
    public void ListDateOnly_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange: Create entity with List<DateOnly> property
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
                    PropertyName = "ImportantDates",
                    AttributeName = "important_dates",
                    PropertyType = "List<DateOnly>",
                    IsCollection = true
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Verify the generated code compiles
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);
    }

    /// <summary>
    /// Verifies that the generated code for List&lt;TimeOnly&gt; compiles successfully.
    /// This is a single compilation test that validates the generated code structure.
    /// </summary>
    [Fact]
    public void ListTimeOnly_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange: Create entity with List<TimeOnly> property
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
                    PropertyName = "ScheduledTimes",
                    AttributeName = "scheduled_times",
                    PropertyType = "List<TimeOnly>",
                    IsCollection = true
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Verify the generated code compiles
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);
    }

    /// <summary>
    /// Generates arbitrary valid DateOnly values for property testing.
    /// </summary>
    private static Arbitrary<DateOnly> GenerateDateOnly()
    {
        return Arb.From(
            Gen.Choose(1, 9999)
                .SelectMany(year => Gen.Choose(1, 12)
                    .SelectMany(month => Gen.Choose(1, DateTime.DaysInMonth(year, month))
                        .Select(day => new DateOnly(year, month, day)))));
    }

    /// <summary>
    /// Generates arbitrary List&lt;DateOnly&gt; collections for property testing.
    /// </summary>
    private static Arbitrary<List<DateOnly>> GenerateDateOnlyList()
    {
        return Arb.From(
            Gen.Choose(0, 10)
                .SelectMany(count => Gen.ListOf(count, GenerateDateOnly().Generator)
                    .Select(items => items.ToList())));
    }

    /// <summary>
    /// Generates arbitrary valid TimeOnly values for property testing.
    /// TimeOnly constructor: (hour, minute, second, millisecond, microsecond)
    /// </summary>
    private static Arbitrary<TimeOnly> GenerateTimeOnly()
    {
        var gen = from hour in Gen.Choose(0, 23)
                  from minute in Gen.Choose(0, 59)
                  from second in Gen.Choose(0, 59)
                  from millisecond in Gen.Choose(0, 999)
                  from microsecond in Gen.Choose(0, 999)
                  select new TimeOnly(hour, minute, second, millisecond, microsecond);
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates arbitrary List&lt;TimeOnly&gt; collections for property testing.
    /// </summary>
    private static Arbitrary<List<TimeOnly>> GenerateTimeOnlyList()
    {
        return Arb.From(
            Gen.Choose(0, 10)
                .SelectMany(count => Gen.ListOf(count, GenerateTimeOnly().Generator)
                    .Select(items => items.ToList())));
    }

    /// <summary>
    /// Helper method to create entity source code from an EntityModel for compilation testing.
    /// </summary>
    private static string CreateEntitySource(EntityModel entity)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {entity.ClassName}");
        sb.AppendLine("    {");
        
        foreach (var prop in entity.Properties)
        {
            var propertyType = prop.PropertyType;
            if (prop.IsNullable && !propertyType.EndsWith("?") && !propertyType.Contains("<"))
            {
                propertyType += "?";
            }
            
            // Add default value for collections
            var defaultValue = propertyType.StartsWith("List<") ? " = new();" : "";
            sb.AppendLine($"        public {propertyType} {prop.PropertyName} {{ get; set; }}{defaultValue}");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}
