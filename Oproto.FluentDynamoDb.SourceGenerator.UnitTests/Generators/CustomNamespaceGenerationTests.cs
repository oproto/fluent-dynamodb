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

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for custom namespace support in generated table classes.
/// **Feature: api-enhancements-v0.9, Property 2: Custom namespace is applied to generated table class**
/// **Validates: Requirements 2.1**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
public class CustomNamespaceGenerationTests
{
    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 2: Custom namespace is applied to generated table class**
    /// 
    /// For any entity with [DynamoDbTable(Namespace = "X")], the generated table class 
    /// SHALL be in namespace "X".
    /// 
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CustomNamespace_IsAppliedToGeneratedTableClass()
    {
        return Prop.ForAll(
            GenerateValidNamespace(),
            GenerateValidNamespace(),
            (entityNamespace, customTableNamespace) =>
            {
                // Skip if namespaces are the same (not testing custom namespace scenario)
                if (entityNamespace == customTableNamespace)
                {
                    return true.ToProperty().Label("Skipped: namespaces are identical");
                }

                // Arrange - Create an entity with a custom table namespace
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    Namespace = entityNamespace,
                    TableName = "test-table",
                    TableNamespace = customTableNamespace,
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Id",
                            AttributeName = "pk",
                            PropertyType = "string",
                            IsPartitionKey = true
                        }
                    }
                };

                // Act - Generate the table class
                var result = TableGenerator.GenerateTableClass(entity);

                // Assert - Verify the generated code uses the custom namespace
                var hasCustomNamespace = result.Contains($"namespace {customTableNamespace};");
                var hasEntityNamespaceUsing = result.Contains($"using {entityNamespace};");

                return (hasCustomNamespace && hasEntityNamespaceUsing).ToProperty()
                    .Label($"Table class should be in custom namespace '{customTableNamespace}' " +
                           $"and have using directive for entity namespace '{entityNamespace}'. " +
                           $"HasCustomNamespace: {hasCustomNamespace}, HasEntityNamespaceUsing: {hasEntityNamespaceUsing}");
            });
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 2: Custom namespace is applied to generated table class**
    /// 
    /// For any entity without a custom namespace specified, the generated table class 
    /// SHALL use the entity's namespace as the default.
    /// 
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoCustomNamespace_UsesEntityNamespace()
    {
        return Prop.ForAll(
            GenerateValidNamespace(),
            entityNamespace =>
            {
                // Arrange - Create an entity without a custom table namespace
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    Namespace = entityNamespace,
                    TableName = "test-table",
                    TableNamespace = null, // No custom namespace
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Id",
                            AttributeName = "pk",
                            PropertyType = "string",
                            IsPartitionKey = true
                        }
                    }
                };

                // Act - Generate the table class
                var result = TableGenerator.GenerateTableClass(entity);

                // Assert - Verify the generated code uses the entity's namespace
                var hasEntityNamespace = result.Contains($"namespace {entityNamespace};");
                
                // Should NOT have a using directive for the entity namespace (since it's the same)
                var hasUnnecessaryUsing = result.Contains($"using {entityNamespace};");

                return (hasEntityNamespace && !hasUnnecessaryUsing).ToProperty()
                    .Label($"Table class should be in entity namespace '{entityNamespace}' " +
                           $"without unnecessary using directive. " +
                           $"HasEntityNamespace: {hasEntityNamespace}, HasUnnecessaryUsing: {hasUnnecessaryUsing}");
            });
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 2: Custom namespace is applied to generated table class**
    /// 
    /// For multi-entity tables with a custom namespace, the generated table class 
    /// SHALL be in the custom namespace and include using directives for all entity namespaces.
    /// 
    /// **Validates: Requirements 2.1, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultiEntityTable_CustomNamespace_IncludesAllEntityUsings()
    {
        return Prop.ForAll(
            GenerateValidNamespace(),
            GenerateValidNamespace(),
            GenerateValidNamespace(),
            (customTableNamespace, entityNamespace1, entityNamespace2) =>
            {
                // Ensure all namespaces are different for meaningful test
                if (customTableNamespace == entityNamespace1 || 
                    customTableNamespace == entityNamespace2 ||
                    entityNamespace1 == entityNamespace2)
                {
                    return true.ToProperty().Label("Skipped: namespaces are not all unique");
                }

                // Arrange - Create multiple entities with different namespaces
                var entities = new List<EntityModel>
                {
                    new EntityModel
                    {
                        ClassName = "Entity1",
                        Namespace = entityNamespace1,
                        TableName = "shared-table",
                        TableNamespace = customTableNamespace,
                        IsDefault = true,
                        Properties = new[]
                        {
                            new PropertyModel
                            {
                                PropertyName = "Id",
                                AttributeName = "pk",
                                PropertyType = "string",
                                IsPartitionKey = true
                            }
                        }
                    },
                    new EntityModel
                    {
                        ClassName = "Entity2",
                        Namespace = entityNamespace2,
                        TableName = "shared-table",
                        Properties = new[]
                        {
                            new PropertyModel
                            {
                                PropertyName = "Id",
                                AttributeName = "pk",
                                PropertyType = "string",
                                IsPartitionKey = true
                            }
                        }
                    }
                };

                // Act - Generate the multi-entity table class
                var result = TableGenerator.GenerateTableClass("shared-table", entities);

                // Assert - Verify the generated code uses the custom namespace
                var hasCustomNamespace = result.Contains($"namespace {customTableNamespace};");
                var hasEntity1NamespaceUsing = result.Contains($"using {entityNamespace1};");
                var hasEntity2NamespaceUsing = result.Contains($"using {entityNamespace2};");

                return (hasCustomNamespace && hasEntity1NamespaceUsing && hasEntity2NamespaceUsing).ToProperty()
                    .Label($"Multi-entity table should be in custom namespace '{customTableNamespace}' " +
                           $"with using directives for both entity namespaces. " +
                           $"HasCustomNamespace: {hasCustomNamespace}, " +
                           $"HasEntity1Using: {hasEntity1NamespaceUsing}, " +
                           $"HasEntity2Using: {hasEntity2NamespaceUsing}");
            });
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 2: Custom namespace is applied to generated table class**
    /// 
    /// Direct test using EntityAnalyzer to verify that when analyzing source code with 
    /// [DynamoDbTable(Namespace = "X")], the TableNamespace property is correctly extracted.
    /// 
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Fact]
    public void EntityAnalyzer_WithCustomNamespace_ExtractsTableNamespace()
    {
        // Arrange - Source code with custom namespace
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace MyApp.Domain
{
    [DynamoDbTable(""orders"", Namespace = ""MyApp.Infrastructure.DynamoDb"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Namespace.Should().Be("MyApp.Domain", "entity namespace should be the declared namespace");
        result.TableNamespace.Should().Be("MyApp.Infrastructure.DynamoDb", "table namespace should be the custom namespace");
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 2: Custom namespace is applied to generated table class**
    /// 
    /// Direct test using EntityAnalyzer to verify that when analyzing source code without
    /// a custom namespace, the TableNamespace property is null.
    /// 
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Fact]
    public void EntityAnalyzer_WithoutCustomNamespace_TableNamespaceIsNull()
    {
        // Arrange - Source code without custom namespace
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace MyApp.Domain
{
    [DynamoDbTable(""orders"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Namespace.Should().Be("MyApp.Domain", "entity namespace should be the declared namespace");
        result.TableNamespace.Should().BeNull("table namespace should be null when not specified");
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 2: Custom namespace is applied to generated table class**
    /// 
    /// Direct test to verify that the generated table class is in the custom namespace
    /// and includes the appropriate using directive for the entity namespace.
    /// 
    /// **Validates: Requirements 2.1, 2.3**
    /// </summary>
    [Fact]
    public void TableGenerator_WithCustomNamespace_GeneratesCorrectCode()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Order",
            Namespace = "MyApp.Domain",
            TableName = "orders",
            TableNamespace = "MyApp.Infrastructure.DynamoDb",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                }
            }
        };

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert
        result.Should().Contain("namespace MyApp.Infrastructure.DynamoDb;", 
            "generated table class should be in the custom namespace");
        result.Should().Contain("using MyApp.Domain;", 
            "generated code should include using directive for entity namespace");
        result.Should().Contain("public partial class OrdersTable", 
            "generated table class should have the correct name");
    }

    /// <summary>
    /// Generates valid C# namespace names for testing.
    /// </summary>
    private static Arbitrary<string> GenerateValidNamespace()
    {
        return Arb.From(
            from segmentCount in Gen.Choose(1, 4)
            from segments in Gen.ArrayOf(segmentCount, GenerateNamespaceSegment())
            select string.Join(".", segments)
        );
    }

    /// <summary>
    /// Generates a single valid namespace segment.
    /// </summary>
    private static Gen<string> GenerateNamespaceSegment()
    {
        return from length in Gen.Choose(3, 12)
               from firstChar in Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                                              'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z')
               from restChars in Gen.ArrayOf(length - 1, Gen.Elements(
                   'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                   'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                   'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                   'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                   '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
               select firstChar + new string(restChars);
    }

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        return (classDecl, semanticModel);
    }
}
