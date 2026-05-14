using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for MetadataInheritanceStrategy.
/// 
/// **Feature: projection-interface-enhancement, Property 2: Projection metadata inheritance consistency**
/// **Validates: Requirements 2.4, 5.1, 5.2, 5.4**
/// </summary>
public class MetadataInheritancePropertyTests
{
    /// <summary>
    /// Property 2: For any generated projection, its metadata SHALL contain the same table name
    /// as its source entity.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldInheritTableName_FromSourceEntity()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var sourceEntity = CreateTestSourceEntity(cleanEntityName, cleanTableName);
                var projection = CreateTestProjection(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - table name should be inherited from source entity
                return metadata.TableName == sourceEntity.TableName;
            });
    }

    /// <summary>
    /// Property 2: For any generated projection, its metadata SHALL contain the same partition key
    /// information as its source entity.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldInheritPartitionKey_FromSourceEntity()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (pkAttributeName, entityName) =>
            {
                // Arrange
                var cleanPkAttributeName = SanitizeAttributeName(pkAttributeName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var sourceEntity = CreateTestSourceEntityWithPk(cleanEntityName, "TestTable", cleanPkAttributeName);
                var projection = CreateTestProjection(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - partition key attribute name should be inherited from source entity
                return metadata.PartitionKeyAttributeName == sourceEntity.PartitionKeyProperty?.AttributeName;
            });
    }

    /// <summary>
    /// Property 2: For any generated projection, its metadata SHALL contain the same sort key
    /// information as its source entity if applicable.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldInheritSortKey_FromSourceEntity()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (pkAttributeName, skAttributeName, entityName) =>
            {
                // Arrange
                var cleanPkAttributeName = SanitizeAttributeName(pkAttributeName.Get);
                var cleanSkAttributeName = SanitizeAttributeName(skAttributeName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var sourceEntity = CreateTestSourceEntityWithPkSk(cleanEntityName, "TestTable", cleanPkAttributeName, cleanSkAttributeName);
                var projection = CreateTestProjection(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - sort key attribute name should be inherited from source entity
                return metadata.SortKeyAttributeName == sourceEntity.SortKeyProperty?.AttributeName;
            });
    }

    /// <summary>
    /// Property 2: For any generated projection, its metadata SHALL inherit discriminator
    /// metadata from its source entity if applicable.
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldInheritDiscriminator_FromSourceEntity()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (discriminatorValue, entityName) =>
            {
                // Arrange
                var cleanDiscriminatorValue = SanitizeName(discriminatorValue.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var sourceEntity = CreateTestSourceEntityWithDiscriminator(cleanEntityName, "TestTable", cleanDiscriminatorValue);
                var projection = CreateTestProjection(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - discriminator should be inherited from source entity
                if (sourceEntity.Discriminator == null)
                    return metadata.Discriminator == null;
                
                return metadata.Discriminator != null &&
                       metadata.Discriminator.ExactValue == sourceEntity.Discriminator.ExactValue &&
                       metadata.Discriminator.PropertyName == sourceEntity.Discriminator.PropertyName;
            });
    }

    /// <summary>
    /// Property 2: For any generated projection, its properties SHALL only include
    /// attributes that are part of the projection.
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldFilterProperties_ToProjectedAttributesOnly()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (entityName) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Create source entity with multiple properties
                var sourceEntity = CreateTestSourceEntityWithMultipleProperties(cleanEntityName, "TestTable");
                
                // Create projection with only a subset of properties
                var projection = CreateTestProjectionWithSubset(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - metadata properties should only include projected attributes
                var projectedAttributeNames = projection.Properties
                    .Where(p => !string.IsNullOrEmpty(p.AttributeName))
                    .Select(p => p.AttributeName)
                    .ToHashSet();
                
                return metadata.Properties.All(p => projectedAttributeNames.Contains(p.AttributeName));
            });
    }

    /// <summary>
    /// Property 2: For any generated projection, the source entity reference should be correctly set.
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldSetSourceEntityReference_Correctly()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (entityName, namespaceName) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanNamespace = SanitizeNamespace(namespaceName.Get);
                
                var sourceEntity = CreateTestSourceEntity(cleanEntityName, "TestTable");
                sourceEntity.Namespace = cleanNamespace;
                var projection = CreateTestProjection(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - source entity reference should be correctly set
                return metadata.SourceEntityClassName == sourceEntity.ClassName &&
                       metadata.SourceEntityNamespace == sourceEntity.Namespace;
            });
    }

    #region Helper Methods

    private static string SanitizeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Test" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeAttributeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "attr";
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeNamespace(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_.]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "TestNamespace";
        }
        return sanitized.Length > 100 ? sanitized.Substring(0, 100) : sanitized;
    }

    private static EntityModel CreateTestSourceEntity(string entityName, string tableName)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateTestSourceEntityWithPk(string entityName, string tableName, string pkAttributeName)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = pkAttributeName,
                    IsPartitionKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateTestSourceEntityWithPkSk(string entityName, string tableName, string pkAttributeName, string skAttributeName)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = pkAttributeName,
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = "string",
                    AttributeName = skAttributeName,
                    IsSortKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateTestSourceEntityWithDiscriminator(string entityName, string tableName, string discriminatorValue)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "entityType",
                ExactValue = discriminatorValue,
                Strategy = DiscriminatorStrategy.ExactMatch
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateTestSourceEntityWithMultipleProperties(string entityName, string tableName)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = "string",
                    AttributeName = "sk",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    PropertyType = "string",
                    AttributeName = "name"
                },
                new PropertyModel
                {
                    PropertyName = "Status",
                    PropertyType = "string",
                    AttributeName = "status"
                },
                new PropertyModel
                {
                    PropertyName = "Amount",
                    PropertyType = "decimal",
                    AttributeName = "amount"
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static ProjectionModel CreateTestProjection(string sourceEntityName, EntityModel sourceEntity)
    {
        // Create a projection that includes all properties from source entity
        var projectionProperties = sourceEntity.Properties
            .Select(p => new ProjectionPropertyModel
            {
                PropertyName = p.PropertyName,
                PropertyType = p.PropertyType,
                AttributeName = p.AttributeName,
                IsNullable = p.IsNullable,
                SourceProperty = p
            })
            .ToArray();

        return new ProjectionModel
        {
            ClassName = $"{sourceEntityName}Projection",
            Namespace = "TestNamespace",
            SourceEntityType = sourceEntityName,
            Properties = projectionProperties,
            ProjectionExpression = string.Join(", ", projectionProperties.Select(p => p.AttributeName))
        };
    }

    private static ProjectionModel CreateTestProjectionWithSubset(string sourceEntityName, EntityModel sourceEntity)
    {
        // Create a projection that includes only a subset of properties (pk and name)
        var projectionProperties = sourceEntity.Properties
            .Where(p => p.AttributeName == "pk" || p.AttributeName == "name")
            .Select(p => new ProjectionPropertyModel
            {
                PropertyName = p.PropertyName,
                PropertyType = p.PropertyType,
                AttributeName = p.AttributeName,
                IsNullable = p.IsNullable,
                SourceProperty = p
            })
            .ToArray();

        return new ProjectionModel
        {
            ClassName = $"{sourceEntityName}Projection",
            Namespace = "TestNamespace",
            SourceEntityType = sourceEntityName,
            Properties = projectionProperties,
            ProjectionExpression = string.Join(", ", projectionProperties.Select(p => p.AttributeName))
        };
    }

    #endregion
}


/// <summary>
/// Property-based tests for write-specific metadata exclusion in projections.
/// 
/// **Feature: projection-interface-enhancement, Property 10: Write-specific metadata exclusion**
/// **Validates: Requirements 5.5**
/// </summary>
public class WriteSpecificMetadataExclusionPropertyTests
{
    /// <summary>
    /// Property 10: For any generated projection, its metadata SHALL NOT include
    /// RequiresWriteTransaction (always false for projections).
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldAlwaysHaveRequiresWriteTransactionFalse()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<bool>(),
            (entityName, sourceRequiresTransaction) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Create source entity with RequiresWriteTransaction set to the random value
                var sourceEntity = CreateTestSourceEntityWithWriteTransaction(cleanEntityName, "TestTable", sourceRequiresTransaction);
                var projection = CreateTestProjection(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - RequiresWriteTransaction should always be false for projections
                // regardless of the source entity's setting
                return metadata.RequiresWriteTransaction == false;
            });
    }

    /// <summary>
    /// Property 10: For any generated projection, its metadata SHALL NOT include
    /// IsMultiItemEntity (always false for projections).
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldAlwaysHaveIsMultiItemEntityFalse()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<bool>(),
            (entityName, sourceIsMultiItem) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Create source entity with IsMultiItemEntity set to the random value
                var sourceEntity = CreateTestSourceEntityWithMultiItem(cleanEntityName, "TestTable", sourceIsMultiItem);
                var projection = CreateTestProjection(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - IsMultiItemEntity should always be false for projections
                // regardless of the source entity's setting
                return metadata.IsMultiItemEntity == false;
            });
    }

    /// <summary>
    /// Property 10: For any generated projection, write-specific metadata should be excluded
    /// even when source entity has all write-specific features enabled.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionMetadata_ShouldExcludeAllWriteSpecificMetadata_WhenSourceHasAll()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (entityName) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Create source entity with all write-specific features enabled
                var sourceEntity = CreateTestSourceEntityWithAllWriteFeatures(cleanEntityName, "TestTable");
                var projection = CreateTestProjection(cleanEntityName, sourceEntity);
                
                // Act
                var metadata = MetadataInheritanceStrategy.CreateProjectionMetadata(sourceEntity, projection);
                
                // Assert - all write-specific metadata should be excluded
                return metadata.RequiresWriteTransaction == false &&
                       metadata.IsMultiItemEntity == false;
            });
    }

    #region Helper Methods

    private static string SanitizeName(string name)
    {
        var sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Test" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static EntityModel CreateTestSourceEntityWithWriteTransaction(string entityName, string tableName, bool requiresWriteTransaction)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            RequiresWriteTransaction = requiresWriteTransaction,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateTestSourceEntityWithMultiItem(string entityName, string tableName, bool isMultiItemEntity)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            IsMultiItemEntity = isMultiItemEntity,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateTestSourceEntityWithAllWriteFeatures(string entityName, string tableName)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            RequiresWriteTransaction = true,
            IsMultiItemEntity = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static ProjectionModel CreateTestProjection(string sourceEntityName, EntityModel sourceEntity)
    {
        // Create a projection that includes all properties from source entity
        var projectionProperties = sourceEntity.Properties
            .Select(p => new ProjectionPropertyModel
            {
                PropertyName = p.PropertyName,
                PropertyType = p.PropertyType,
                AttributeName = p.AttributeName,
                IsNullable = p.IsNullable,
                SourceProperty = p
            })
            .ToArray();

        return new ProjectionModel
        {
            ClassName = $"{sourceEntityName}Projection",
            Namespace = "TestNamespace",
            SourceEntityType = sourceEntityName,
            Properties = projectionProperties,
            ProjectionExpression = string.Join(", ", projectionProperties.Select(p => p.AttributeName))
        };
    }

    #endregion
}
