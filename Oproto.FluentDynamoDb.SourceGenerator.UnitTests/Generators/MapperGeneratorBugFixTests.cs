// MIGRATION STATUS: Migrated to use CompilationVerifier and SemanticAssertions
// - Added compilation verification to all tests
// - Replaced type reference checks with semantic assertions
// - Preserved bug-specific string checks that verify the fix

using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

public class MapperGeneratorBugFixTests
{
    [Fact]
    public void GenerateEntityImplementation_WithPropertyNames_UsesEntityClassNameInTypeofExpressions()
    {
        // Arrange - This test specifically verifies the fix for the CS0246 error
        // where property names were being used as types instead of the entity class name
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
                    PropertyName = "Data",
                    AttributeName = "data",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify compilation succeeds
        var entitySource = @"
namespace TestNamespace
{
    public partial class TestEntity
    {
        public string Id { get; set; }
        public string Data { get; set; }
    }
}";
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Verify that the generated code uses typeof(TestEntity) instead of typeof(Id) or typeof(Data)
        result.ShouldReferenceType("TestEntity");
        
        // Keep bug-specific checks: verify typeof expressions use entity class name, not property names
        result.Should().Contain("typeof(TestEntity)", 
            "should use entity class name in typeof expressions, not property names");
        result.Should().NotContain("typeof(Id)", 
            "should not use property name 'Id' as a type (this was the bug)");
        result.Should().NotContain("typeof(Data)", 
            "should not use property name 'Data' as a type (this was the bug)");

        // Verify the specific error handling code is correct
        result.Should().Contain("throw DynamoDbMappingException.PropertyConversionFailed(",
            "should throw PropertyConversionFailed exception on conversion errors");
        result.Should().Contain("typeof(TestEntity),",
            "should pass entity type to exception constructor");
        result.Should().Contain("\"Id\",",
            "should pass property name as string to exception constructor");
        result.Should().Contain("typeof(string),",
            "should pass property type to exception constructor");
    }
}