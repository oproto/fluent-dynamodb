using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for DayOfWeek enum serialization in the MapperGenerator.
/// These tests verify that the built-in DayOfWeek enum is correctly handled
/// by the enum serialization logic.
/// </summary>
[Trait("Category", "Unit")]
public class DayOfWeekSerializationTests
{
    /// <summary>
    /// Task 8.1: Verify DayOfWeek serializes to string (e.g., "Monday", "Tuesday")
    /// and deserializes from string back to enum value.
    /// Tests all seven days of the week.
    /// </summary>
    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    public void DayOfWeek_Serialization_ShouldUseToStringAndEnumParse(DayOfWeek dayOfWeek)
    {
        // Arrange: Create entity with DayOfWeek property
        var entity = new EntityModel
        {
            ClassName = "ScheduleEntity",
            Namespace = "TestNamespace",
            TableName = "schedules",
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
                    PropertyName = "MeetingDay",
                    AttributeName = "meeting_day",
                    PropertyType = "DayOfWeek"
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Verify serialization uses ToString()
        result.Should().Contain(".ToString()");
        result.Should().Contain("S ="); // Should serialize to string type
        
        // Assert: Verify deserialization uses Enum.Parse<DayOfWeek>
        result.Should().Contain("Enum.Parse<DayOfWeek>");
        
        // Verify the expected string representation
        var expectedString = dayOfWeek.ToString();
        expectedString.Should().Be(dayOfWeek switch
        {
            DayOfWeek.Sunday => "Sunday",
            DayOfWeek.Monday => "Monday",
            DayOfWeek.Tuesday => "Tuesday",
            DayOfWeek.Wednesday => "Wednesday",
            DayOfWeek.Thursday => "Thursday",
            DayOfWeek.Friday => "Friday",
            DayOfWeek.Saturday => "Saturday",
            _ => throw new ArgumentOutOfRangeException()
        });
    }

    /// <summary>
    /// Task 8.1: Verify generated code for DayOfWeek compiles successfully.
    /// </summary>
    [Fact]
    public void DayOfWeek_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange: Create entity with DayOfWeek property
        var entity = new EntityModel
        {
            ClassName = "ScheduleEntity",
            Namespace = "TestNamespace",
            TableName = "schedules",
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
                    PropertyName = "MeetingDay",
                    AttributeName = "meeting_day",
                    PropertyType = "DayOfWeek"
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
    /// Task 8.2: Verify null handling for nullable DayOfWeek?
    /// </summary>
    [Fact]
    public void NullableDayOfWeek_WithNullValue_ShouldSkipAttribute()
    {
        // Arrange: Create entity with nullable DayOfWeek property
        var entity = new EntityModel
        {
            ClassName = "ScheduleEntity",
            Namespace = "TestNamespace",
            TableName = "schedules",
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
                    PropertyName = "OptionalDay",
                    AttributeName = "optional_day",
                    PropertyType = "DayOfWeek?",
                    IsNullable = true
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Verify null check is generated
        result.Should().Contain("if (typedEntity.OptionalDay != null)");
        
        // Assert: Verify serialization uses ToString()
        result.Should().Contain(".ToString()");
        
        // Assert: Verify deserialization uses Enum.Parse<DayOfWeek>
        result.Should().Contain("Enum.Parse<DayOfWeek>");
    }

    /// <summary>
    /// Task 8.2: Verify non-null DayOfWeek? serializes correctly.
    /// </summary>
    [Fact]
    public void NullableDayOfWeek_WithValue_ShouldSerializeCorrectly()
    {
        // Arrange: Create entity with nullable DayOfWeek property
        var entity = new EntityModel
        {
            ClassName = "ScheduleEntity",
            Namespace = "TestNamespace",
            TableName = "schedules",
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
                    PropertyName = "OptionalDay",
                    AttributeName = "optional_day",
                    PropertyType = "DayOfWeek?",
                    IsNullable = true
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
    /// Task 8.2: Verify nullable DayOfWeek? generated code compiles successfully.
    /// </summary>
    [Fact]
    public void NullableDayOfWeek_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange: Create entity with nullable DayOfWeek property
        var entity = new EntityModel
        {
            ClassName = "ScheduleEntity",
            Namespace = "TestNamespace",
            TableName = "schedules",
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
                    PropertyName = "OptionalDay",
                    AttributeName = "optional_day",
                    PropertyType = "DayOfWeek?",
                    IsNullable = true
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
            
            // Add default value for non-nullable reference types
            var defaultValue = propertyType switch
            {
                "string" => " = string.Empty;",
                _ when propertyType.EndsWith("?") => "",
                _ => ""
            };
            
            sb.AppendLine($"        public {propertyType} {prop.PropertyName} {{ get; set; }}{defaultValue}");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}
