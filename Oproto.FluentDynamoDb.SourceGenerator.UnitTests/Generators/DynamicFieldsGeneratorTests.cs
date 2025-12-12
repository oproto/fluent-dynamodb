using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for dynamic fields code generation in MapperGenerator.
/// </summary>
public class DynamicFieldsGeneratorTests
{
    [Fact]
    public void GenerateEntityImplementation_WithEnableDynamicFields_GeneratesDynamicFieldsProperty()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: true);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().Contain("public DynamicFieldCollection DynamicFields { get; set; } = new();");
    }

    [Fact]
    public void GenerateEntityImplementation_WithoutEnableDynamicFields_DoesNotGenerateDynamicFieldsProperty()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: false);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().NotContain("public DynamicFieldCollection DynamicFields");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEnableDynamicFields_GeneratesMappedAttributeNamesHashSet()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: true);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().Contain("private static readonly HashSet<string> _mappedAttributeNames");
        generatedCode.Should().Contain("\"pk\"");
        generatedCode.Should().Contain("\"name\"");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEnableDynamicFields_GeneratesDynamicFieldsCapture()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: true);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().Contain("// Capture dynamic fields (unmapped attributes)");
        generatedCode.Should().Contain("if (!_mappedAttributeNames.Contains(kvp.Key))");
        generatedCode.Should().Contain("entity.DynamicFields.SetRaw(kvp.Key, kvp.Value);");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEnableDynamicFields_GeneratesDynamicFieldsInclusion()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: true);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().Contain("// Include dynamic fields (skip any that conflict with mapped property names)");
        generatedCode.Should().Contain("foreach (var kvp in typedEntity.DynamicFields.ToDictionary())");
        generatedCode.Should().Contain("item[kvp.Key] = kvp.Value;");
    }

    [Fact]
    public void GenerateEntityImplementation_WithoutEnableDynamicFields_DoesNotGenerateDynamicFieldsCapture()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: false);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().NotContain("// Capture dynamic fields");
        generatedCode.Should().NotContain("entity.DynamicFields.SetRaw");
    }

    [Fact]
    public void GenerateEntityImplementation_WithoutEnableDynamicFields_DoesNotGenerateDynamicFieldsInclusion()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: false);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().NotContain("// Include dynamic fields");
        generatedCode.Should().NotContain("typedEntity.DynamicFields.ToDictionary()");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEnableDynamicFields_MappedAttributeNamesContainsAllMappedAttributes()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            EnableDynamicFields = true,
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Id", AttributeName = "pk", IsPartitionKey = true },
                new PropertyModel { PropertyName = "SortKey", AttributeName = "sk", IsSortKey = true },
                new PropertyModel { PropertyName = "Name", AttributeName = "name" },
                new PropertyModel { PropertyName = "Email", AttributeName = "email" },
                new PropertyModel { PropertyName = "CreatedAt", AttributeName = "created_at" }
            }
        };

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().Contain("\"pk\"");
        generatedCode.Should().Contain("\"sk\"");
        generatedCode.Should().Contain("\"name\"");
        generatedCode.Should().Contain("\"email\"");
        generatedCode.Should().Contain("\"created_at\"");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEnableDynamicFields_CallsStartTrackingChanges()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: true);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().Contain("entity.DynamicFields.StartTrackingChanges();",
            "should call StartTrackingChanges after populating dynamic fields");
        generatedCode.Should().Contain("// Start tracking changes for efficient updates",
            "should include comment explaining the purpose");
    }

    [Fact]
    public void GenerateEntityImplementation_WithoutEnableDynamicFields_DoesNotCallStartTrackingChanges()
    {
        // Arrange
        var entity = CreateTestEntity(enableDynamicFields: false);

        // Act
        var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        generatedCode.Should().NotContain("StartTrackingChanges",
            "should not call StartTrackingChanges when dynamic fields are not enabled");
    }

    private static EntityModel CreateTestEntity(bool enableDynamicFields)
    {
        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            EnableDynamicFields = enableDynamicFields,
            DynamicFieldsSensitiveLogging = true,
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Id", AttributeName = "pk", IsPartitionKey = true },
                new PropertyModel { PropertyName = "Name", AttributeName = "name" }
            }
        };
    }
}