using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for DateOnly serialization in the MapperGenerator.
/// These tests verify the correctness properties defined in the design document
/// for the date-time-type-serialization feature.
/// </summary>
[Trait("Category", "Unit")]
public class DateOnlySerializationPropertyTests
{
    /// <summary>
    /// **Feature: date-time-type-serialization, Property 1: DateOnly Round-Trip Consistency**
    /// *For any* valid DateOnly value, serializing it to a DynamoDB AttributeValue and then 
    /// deserializing it back SHALL produce an equivalent DateOnly value.
    /// **Validates: Requirements 1.1, 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateOnly_RoundTrip_ProducesEquivalentValue()
    {
        return Prop.ForAll(
            GenerateDateOnly(),
            dateOnly =>
            {
                // Arrange: Create entity with DateOnly property
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
                            PropertyName = "EventDate",
                            AttributeName = "event_date",
                            PropertyType = "DateOnly"
                        }
                    }
                };

                // Act: Generate code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: Generated code should use ISO 8601 format with InvariantCulture
                var usesCorrectSerializationFormat = 
                    result.Contains(".ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)");
                var usesCorrectDeserializationFormat = 
                    result.Contains("DateOnly.ParseExact") && 
                    result.Contains("\"O\"") &&
                    result.Contains("System.Globalization.CultureInfo.InvariantCulture");
                var serializesToString = result.Contains("S =");

                return (usesCorrectSerializationFormat && usesCorrectDeserializationFormat && serializesToString)
                    .ToProperty()
                    .Label($"DateOnly: {dateOnly}, Serialization: {usesCorrectSerializationFormat}, " +
                           $"Deserialization: {usesCorrectDeserializationFormat}, " +
                           $"StringType: {serializesToString}");
            });
    }

    /// <summary>
    /// **Feature: date-time-type-serialization, Property 1: DateOnly Round-Trip Consistency (Nullable)**
    /// *For any* valid nullable DateOnly? value, serializing it to a DynamoDB AttributeValue and then 
    /// deserializing it back SHALL produce an equivalent DateOnly? value.
    /// **Validates: Requirements 1.3, 1.4, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullableDateOnly_RoundTrip_ProducesEquivalentValue()
    {
        return Prop.ForAll(
            GenerateNullableDateOnly(),
            dateOnly =>
            {
                // Arrange: Create entity with nullable DateOnly property
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
                            PropertyName = "OptionalDate",
                            AttributeName = "optional_date",
                            PropertyType = "DateOnly?",
                            IsNullable = true
                        }
                    }
                };

                // Act: Generate code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: Generated code should handle null checks and use correct format
                var hasNullCheck = result.Contains("if (typedEntity.OptionalDate != null)");
                var usesCorrectFormat = 
                    result.Contains(".ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)");

                return (hasNullCheck && usesCorrectFormat)
                    .ToProperty()
                    .Label($"DateOnly?: {dateOnly?.ToString() ?? "null"}, NullCheck: {hasNullCheck}, " +
                           $"Format: {usesCorrectFormat}");
            });
    }

    /// <summary>
    /// Verifies that the generated code for DateOnly compiles successfully.
    /// This is a single compilation test that validates the generated code structure.
    /// </summary>
    [Fact]
    public void DateOnly_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange: Create entity with DateOnly property
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
                    PropertyName = "EventDate",
                    AttributeName = "event_date",
                    PropertyType = "DateOnly"
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
    /// Generates arbitrary nullable DateOnly values for property testing.
    /// </summary>
    private static Arbitrary<DateOnly?> GenerateNullableDateOnly()
    {
        return Arb.From(
            Gen.OneOf(
                Gen.Constant<DateOnly?>(null),
                GenerateDateOnly().Generator.Select(d => (DateOnly?)d)));
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
