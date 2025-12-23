using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for ProjectionType propagation from index attributes to generated metadata.
/// **Feature: automatic-index-projections, Property 5: ProjectionType propagates to metadata**
/// **Validates: Requirements 2.4, 4.1**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
public class ProjectionTypePropertyTests
{
    /// <summary>
    /// **Feature: automatic-index-projections, Property 5: ProjectionType propagates to metadata**
    /// 
    /// For any index attribute with an explicit ProjectionType value, the generated 
    /// IndexMetadata.ProjectionType SHALL equal the specified value.
    /// 
    /// **Validates: Requirements 2.4, 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionType_PropagatesFromGsiToMetadata()
    {
        return Prop.ForAll(
            GenerateProjectionType(),
            GenerateValidIndexName(),
            (projectionType, indexName) =>
            {
                // Arrange - Create an entity with a GSI that has a specific ProjectionType
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
                            PropertyName = "GsiPk",
                            AttributeName = "gsi1pk",
                            PropertyType = "string",
                            GlobalSecondaryIndexes = new[]
                            {
                                new GlobalSecondaryIndexModel
                                {
                                    IndexName = indexName,
                                    IsPartitionKey = true,
                                    ProjectionType = projectionType
                                }
                            }
                        }
                    },
                    Indexes = new[]
                    {
                        new IndexModel
                        {
                            IndexName = indexName,
                            ResolvedPropertyName = ToPascalCase(indexName),
                            IndexType = IndexType.GlobalSecondaryIndex,
                            PartitionKeyProperty = "GsiPk",
                            PartitionKeyAttribute = "gsi1pk",
                            ProjectionType = projectionType
                        }
                    }
                };

                // Act - Generate the entity implementation code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert - Verify the generated code contains the correct ProjectionType
                var expectedProjectionType = projectionType switch
                {
                    ProjectionType.KeysOnly => "Oproto.FluentDynamoDb.Metadata.ProjectionType.KeysOnly",
                    ProjectionType.Include => "Oproto.FluentDynamoDb.Metadata.ProjectionType.Include",
                    _ => "Oproto.FluentDynamoDb.Metadata.ProjectionType.All"
                };

                var hasCorrectProjectionType = result.Contains($"ProjectionType = {expectedProjectionType}");

                return hasCorrectProjectionType.ToProperty()
                    .Label($"Generated metadata should contain 'ProjectionType = {expectedProjectionType}' for index '{indexName}'. " +
                           $"HasCorrectProjectionType: {hasCorrectProjectionType}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 5: ProjectionType propagates to metadata**
    /// 
    /// For any LSI attribute with an explicit ProjectionType value, the generated 
    /// IndexMetadata.ProjectionType SHALL equal the specified value.
    /// 
    /// **Validates: Requirements 2.4, 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionType_PropagatesFromLsiToMetadata()
    {
        return Prop.ForAll(
            GenerateProjectionType(),
            GenerateValidIndexName(),
            (projectionType, indexName) =>
            {
                // Arrange - Create an entity with an LSI that has a specific ProjectionType
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
                            PropertyName = "Sk",
                            AttributeName = "sk",
                            PropertyType = "string",
                            IsSortKey = true
                        },
                        new PropertyModel
                        {
                            PropertyName = "LsiSk",
                            AttributeName = "lsi1sk",
                            PropertyType = "string",
                            LocalSecondaryIndexes = new[]
                            {
                                new LocalSecondaryIndexModel
                                {
                                    IndexName = indexName,
                                    ProjectionType = projectionType
                                }
                            }
                        }
                    },
                    Indexes = new[]
                    {
                        new IndexModel
                        {
                            IndexName = indexName,
                            ResolvedPropertyName = ToPascalCase(indexName),
                            IndexType = IndexType.LocalSecondaryIndex,
                            PartitionKeyProperty = "Id",
                            PartitionKeyAttribute = "pk",
                            SortKeyProperty = "LsiSk",
                            SortKeyAttribute = "lsi1sk",
                            ProjectionType = projectionType
                        }
                    }
                };

                // Act - Generate the entity implementation code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert - Verify the generated code contains the correct ProjectionType
                var expectedProjectionType = projectionType switch
                {
                    ProjectionType.KeysOnly => "Oproto.FluentDynamoDb.Metadata.ProjectionType.KeysOnly",
                    ProjectionType.Include => "Oproto.FluentDynamoDb.Metadata.ProjectionType.Include",
                    _ => "Oproto.FluentDynamoDb.Metadata.ProjectionType.All"
                };

                var hasCorrectProjectionType = result.Contains($"ProjectionType = {expectedProjectionType}");

                return hasCorrectProjectionType.ToProperty()
                    .Label($"Generated metadata should contain 'ProjectionType = {expectedProjectionType}' for LSI '{indexName}'. " +
                           $"HasCorrectProjectionType: {hasCorrectProjectionType}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 4: ProjectionType defaults to All**
    /// 
    /// For any index attribute without an explicit ProjectionType value, the generated 
    /// IndexMetadata.ProjectionType SHALL be ProjectionType.All.
    /// 
    /// **Validates: Requirements 2.3, 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionType_DefaultsToAll_WhenNotSpecified()
    {
        return Prop.ForAll(
            GenerateValidIndexName(),
            indexName =>
            {
                // Arrange - Create an entity with a GSI that has default ProjectionType
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
                            PropertyName = "GsiPk",
                            AttributeName = "gsi1pk",
                            PropertyType = "string",
                            GlobalSecondaryIndexes = new[]
                            {
                                new GlobalSecondaryIndexModel
                                {
                                    IndexName = indexName,
                                    IsPartitionKey = true
                                    // ProjectionType not specified - should default to All
                                }
                            }
                        }
                    },
                    Indexes = new[]
                    {
                        new IndexModel
                        {
                            IndexName = indexName,
                            ResolvedPropertyName = ToPascalCase(indexName),
                            IndexType = IndexType.GlobalSecondaryIndex,
                            PartitionKeyProperty = "GsiPk",
                            PartitionKeyAttribute = "gsi1pk"
                            // ProjectionType not specified - should default to All
                        }
                    }
                };

                // Act - Generate the entity implementation code
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert - Verify the generated code contains ProjectionType.All
                var hasDefaultProjectionType = result.Contains("ProjectionType = Oproto.FluentDynamoDb.Metadata.ProjectionType.All");

                return hasDefaultProjectionType.ToProperty()
                    .Label($"Generated metadata should contain 'ProjectionType = Oproto.FluentDynamoDb.Metadata.ProjectionType.All' for index '{indexName}' when not specified. " +
                           $"HasDefaultProjectionType: {hasDefaultProjectionType}");
            });
    }

    /// <summary>
    /// Generates a random ProjectionType value.
    /// </summary>
    private static Arbitrary<ProjectionType> GenerateProjectionType()
    {
        return Arb.From(Gen.Elements(ProjectionType.All, ProjectionType.KeysOnly, ProjectionType.Include));
    }

    /// <summary>
    /// Generates a valid index name (lowercase with hyphens).
    /// </summary>
    private static Arbitrary<string> GenerateValidIndexName()
    {
        var prefixes = new[] { "gsi", "lsi", "status", "email", "date", "category" };
        var suffixes = new[] { "index", "idx", "" };
        
        return Arb.From(
            from prefix in Gen.Elements(prefixes)
            from suffix in Gen.Elements(suffixes)
            from number in Gen.Choose(1, 9)
            select string.IsNullOrEmpty(suffix) 
                ? $"{prefix}{number}" 
                : $"{prefix}{number}-{suffix}");
    }

    /// <summary>
    /// Converts a string to PascalCase.
    /// </summary>
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var parts = input.Split('-', '_');
        return string.Concat(parts.Select(p => 
            string.IsNullOrEmpty(p) ? p : char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()));
    }
}
