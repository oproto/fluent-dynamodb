using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for custom format string handling in DateOnly and TimeOnly serialization.
/// These tests verify Requirements 5.1, 5.2, and 5.3 from the date-time-type-serialization spec.
/// </summary>
[Trait("Category", "Unit")]
public class DateTimeFormatStringTests
{
    /// <summary>
    /// Verifies that DateOnly with custom format "MM/dd/yyyy" generates correct serialization code.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Fact]
    public void DateOnly_WithCustomFormat_GeneratesCorrectSerializationCode()
    {
        // Arrange: Create entity with DateOnly property using custom format
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
                    PropertyName = "DisplayDate",
                    AttributeName = "display_date",
                    PropertyType = "DateOnly",
                    Format = "MM/dd/yyyy"
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Generated code should use the custom format string
        Assert.Contains(".ToString(\"MM/dd/yyyy\", System.Globalization.CultureInfo.InvariantCulture)", result);
        Assert.Contains("DateOnly.TryParseExact", result);
        Assert.Contains("\"MM/dd/yyyy\"", result);
    }

    /// <summary>
    /// Verifies that DateOnly with custom format generates code that compiles successfully.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Fact]
    public void DateOnly_WithCustomFormat_CompilesSuccessfully()
    {
        // Arrange: Create entity with DateOnly property using custom format
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
                    PropertyName = "DisplayDate",
                    AttributeName = "display_date",
                    PropertyType = "DateOnly",
                    Format = "MM/dd/yyyy"
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
    /// Verifies that TimeOnly with custom format "h:mm tt" generates correct serialization code.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Fact]
    public void TimeOnly_WithCustomFormat_GeneratesCorrectSerializationCode()
    {
        // Arrange: Create entity with TimeOnly property using custom format
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
                    PropertyName = "DisplayTime",
                    AttributeName = "display_time",
                    PropertyType = "TimeOnly",
                    Format = "h:mm tt"
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Generated code should use the custom format string
        Assert.Contains(".ToString(\"h:mm tt\", System.Globalization.CultureInfo.InvariantCulture)", result);
        Assert.Contains("TimeOnly.TryParseExact", result);
        Assert.Contains("\"h:mm tt\"", result);
    }

    /// <summary>
    /// Verifies that TimeOnly with custom format generates code that compiles successfully.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Fact]
    public void TimeOnly_WithCustomFormat_CompilesSuccessfully()
    {
        // Arrange: Create entity with TimeOnly property using custom format
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
                    PropertyName = "DisplayTime",
                    AttributeName = "display_time",
                    PropertyType = "TimeOnly",
                    Format = "h:mm tt"
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
    /// Verifies that DateOnly without format uses default ISO 8601 format.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Fact]
    public void DateOnly_WithoutFormat_UsesDefaultIso8601Format()
    {
        // Arrange: Create entity with DateOnly property without custom format
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
                    // No Format specified - should use default "O"
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Generated code should use default ISO 8601 format "O"
        Assert.Contains(".ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)", result);
        Assert.Contains("DateOnly.ParseExact", result);
        Assert.Contains("\"O\"", result);
    }

    /// <summary>
    /// Verifies that TimeOnly without format uses default ISO 8601 format.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Fact]
    public void TimeOnly_WithoutFormat_UsesDefaultIso8601Format()
    {
        // Arrange: Create entity with TimeOnly property without custom format
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
                    // No Format specified - should use default "O"
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Generated code should use default ISO 8601 format "O"
        Assert.Contains(".ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)", result);
        Assert.Contains("TimeOnly.ParseExact", result);
        Assert.Contains("\"O\"", result);
    }

    /// <summary>
    /// Verifies that DateOnly with ISO date format "yyyy-MM-dd" generates correct code.
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Fact]
    public void DateOnly_WithIsoDateFormat_GeneratesCorrectCode()
    {
        // Arrange: Create entity with DateOnly property using ISO date format
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
                    PropertyName = "IsoDate",
                    AttributeName = "iso_date",
                    PropertyType = "DateOnly",
                    Format = "yyyy-MM-dd"
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Generated code should use the ISO date format
        Assert.Contains(".ToString(\"yyyy-MM-dd\", System.Globalization.CultureInfo.InvariantCulture)", result);
        Assert.Contains("DateOnly.TryParseExact", result);
        Assert.Contains("\"yyyy-MM-dd\"", result);
    }

    /// <summary>
    /// Verifies that TimeOnly with 24-hour format "HH:mm:ss" generates correct code.
    /// **Validates: Requirements 5.2, 5.3**
    /// </summary>
    [Fact]
    public void TimeOnly_With24HourFormat_GeneratesCorrectCode()
    {
        // Arrange: Create entity with TimeOnly property using 24-hour format
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
                    PropertyName = "MilitaryTime",
                    AttributeName = "military_time",
                    PropertyType = "TimeOnly",
                    Format = "HH:mm:ss"
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Generated code should use the 24-hour format
        Assert.Contains(".ToString(\"HH:mm:ss\", System.Globalization.CultureInfo.InvariantCulture)", result);
        Assert.Contains("TimeOnly.TryParseExact", result);
        Assert.Contains("\"HH:mm:ss\"", result);
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
