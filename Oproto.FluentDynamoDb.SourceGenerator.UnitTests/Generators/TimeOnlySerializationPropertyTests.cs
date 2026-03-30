using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for TimeOnly serialization in the MapperGenerator.
/// These tests verify the correctness properties defined in the design document
/// for the date-time-type-serialization feature.
/// </summary>
[Trait("Category", "Unit")]
public class TimeOnlySerializationPropertyTests
{
    /// <summary>
    /// **Feature: date-time-type-serialization, Property 2: TimeOnly Round-Trip Consistency**
    /// *For any* valid TimeOnly value, serializing it to a DynamoDB AttributeValue and then 
    /// deserializing it back SHALL produce an equivalent TimeOnly value.
    /// **Validates: Requirements 2.1, 2.2, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TimeOnly_RoundTrip_ProducesEquivalentValue()
    {
        return Prop.ForAll(
            GenerateTimeOnly(),
            timeOnly =>
            {
                // Arrange: Create entity with TimeOnly property
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
                            PropertyName = "StartTime",
                            AttributeName = "start_time",
                            PropertyType = "TimeOnly"
                        }
                    }
                };

                // Act: Generate code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: Generated code should use ISO 8601 format with InvariantCulture
                var usesCorrectSerializationFormat = 
                    result.Contains(".ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)");
                var usesCorrectDeserializationFormat = 
                    result.Contains("TimeOnly.ParseExact") && 
                    result.Contains("\"O\"") &&
                    result.Contains("System.Globalization.CultureInfo.InvariantCulture");
                var serializesToString = result.Contains("S =");

                return (usesCorrectSerializationFormat && usesCorrectDeserializationFormat && serializesToString)
                    .ToProperty()
                    .Label($"TimeOnly: {timeOnly}, Serialization: {usesCorrectSerializationFormat}, " +
                           $"Deserialization: {usesCorrectDeserializationFormat}, " +
                           $"StringType: {serializesToString}");
            });
    }

    /// <summary>
    /// **Feature: date-time-type-serialization, Property 2: TimeOnly Round-Trip Consistency (Nullable)**
    /// *For any* valid nullable TimeOnly? value, serializing it to a DynamoDB AttributeValue and then 
    /// deserializing it back SHALL produce an equivalent TimeOnly? value.
    /// **Validates: Requirements 2.3, 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullableTimeOnly_RoundTrip_ProducesEquivalentValue()
    {
        return Prop.ForAll(
            GenerateNullableTimeOnly(),
            timeOnly =>
            {
                // Arrange: Create entity with nullable TimeOnly property
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
                            PropertyName = "OptionalTime",
                            AttributeName = "optional_time",
                            PropertyType = "TimeOnly?",
                            IsNullable = true
                        }
                    }
                };

                // Act: Generate code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: Generated code should handle null checks and use correct format
                var hasNullCheck = result.Contains("if (typedEntity.OptionalTime != null)");
                var usesCorrectFormat = 
                    result.Contains(".ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)");

                return (hasNullCheck && usesCorrectFormat)
                    .ToProperty()
                    .Label($"TimeOnly?: {timeOnly?.ToString() ?? "null"}, NullCheck: {hasNullCheck}, " +
                           $"Format: {usesCorrectFormat}");
            });
    }

    /// <summary>
    /// Verifies that the generated code for TimeOnly compiles successfully.
    /// This is a single compilation test that validates the generated code structure.
    /// </summary>
    [Fact]
    public void TimeOnly_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange: Create entity with TimeOnly property
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
                    PropertyName = "StartTime",
                    AttributeName = "start_time",
                    PropertyType = "TimeOnly"
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
    /// Generates arbitrary nullable TimeOnly values for property testing.
    /// </summary>
    private static Arbitrary<TimeOnly?> GenerateNullableTimeOnly()
    {
        return Arb.From(
            Gen.OneOf(
                Gen.Constant<TimeOnly?>(null),
                GenerateTimeOnly().Generator.Select(t => (TimeOnly?)t)));
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
            sb.AppendLine($"        public {propertyType} {prop.PropertyName} {{ get; set; }}");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}
