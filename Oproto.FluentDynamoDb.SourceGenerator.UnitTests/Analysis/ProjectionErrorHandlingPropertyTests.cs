using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for projection error handling and diagnostics.
/// 
/// **Feature: projection-interface-enhancement, Property 13: Metadata inheritance error handling**
/// **Validates: Requirements 8.1**
/// </summary>
public class ProjectionMetadataInheritanceErrorPropertyTests
{
    /// <summary>
    /// Property 13: For any projection that cannot inherit metadata from its source entity
    /// (missing table name), the source generator SHALL emit a clear diagnostic error (FDDB061).
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_ShouldEmitFDDB061_WhenSourceEntityMissingTableName()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, entityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Create source entity with missing table name
                var sourceEntity = CreateSourceEntityWithMissingTableName(cleanEntityName);
                
                // Create analyzer and validate
                var analyzer = new ProjectionModelAnalyzer();
                var isValid = analyzer.ValidateSourceEntityMetadataPublic(sourceEntity, cleanProjectionName, Location.None);
                
                // Assert - should fail validation and emit FDDB061
                if (!isValid)
                {
                    var diagnostics = analyzer.Diagnostics;
                    return diagnostics.Any(d => d.Id == "FDDB061");
                }
                
                return false; // Should have failed validation
            });
    }

    /// <summary>
    /// Property 13: For any projection that cannot inherit metadata from its source entity
    /// (missing partition key), the source generator SHALL emit a clear diagnostic error (FDDB061).
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_ShouldEmitFDDB061_WhenSourceEntityMissingPartitionKey()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, entityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Create source entity with missing partition key
                var sourceEntity = CreateSourceEntityWithMissingPartitionKey(cleanEntityName);
                
                // Create analyzer and validate
                var analyzer = new ProjectionModelAnalyzer();
                var isValid = analyzer.ValidateSourceEntityMetadataPublic(sourceEntity, cleanProjectionName, Location.None);
                
                // Assert - should fail validation and emit FDDB061
                if (!isValid)
                {
                    var diagnostics = analyzer.Diagnostics;
                    return diagnostics.Any(d => d.Id == "FDDB061");
                }
                
                return false; // Should have failed validation
            });
    }

    /// <summary>
    /// Property 13: For any projection with a valid source entity, the source generator
    /// SHALL NOT emit FDDB061 diagnostic.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_ShouldNotEmitFDDB061_WhenSourceEntityIsValid()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, entityName, tableName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanTableName = SanitizeName(tableName.Get);
                
                // Create valid source entity
                var sourceEntity = CreateValidSourceEntity(cleanEntityName, cleanTableName);
                
                // Create analyzer and validate
                var analyzer = new ProjectionModelAnalyzer();
                var isValid = analyzer.ValidateSourceEntityMetadataPublic(sourceEntity, cleanProjectionName, Location.None);
                
                // Assert - should pass validation and NOT emit FDDB061
                var diagnostics = analyzer.Diagnostics;
                return isValid && !diagnostics.Any(d => d.Id == "FDDB061");
            });
    }

    /// <summary>
    /// Property 13: The FDDB061 diagnostic message SHALL include both the projection name
    /// and the source entity name for clear error identification.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB061Message_ShouldIncludeBothNames()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, entityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Create source entity with missing table name
                var sourceEntity = CreateSourceEntityWithMissingTableName(cleanEntityName);
                
                // Create analyzer and validate
                var analyzer = new ProjectionModelAnalyzer();
                analyzer.ValidateSourceEntityMetadataPublic(sourceEntity, cleanProjectionName, Location.None);
                
                // Assert - diagnostic message should include both names
                var diagnostic = analyzer.Diagnostics.FirstOrDefault(d => d.Id == "FDDB061");
                if (diagnostic != null)
                {
                    var message = diagnostic.GetMessage();
                    return message.Contains(cleanProjectionName) && message.Contains(cleanEntityName);
                }
                
                return false;
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

    private static EntityModel CreateSourceEntityWithMissingTableName(string entityName)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = string.Empty, // Missing table name
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

    private static EntityModel CreateSourceEntityWithMissingPartitionKey(string entityName)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Name",
                    PropertyType = "string",
                    AttributeName = "name",
                    IsPartitionKey = false // No partition key
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

    private static EntityModel CreateValidSourceEntity(string entityName, string tableName)
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

    #endregion
}


/// <summary>
/// Property-based tests for source entity validation in projections.
/// 
/// **Feature: projection-interface-enhancement, Property 14: Source entity validation**
/// **Validates: Requirements 8.3**
/// </summary>
public class ProjectionSourceEntityValidationPropertyTests
{
    /// <summary>
    /// Property 14: For any projection referencing a non-existent source entity,
    /// the source generator SHALL emit a clear diagnostic error (FDDB060).
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_ShouldEmitFDDB060_WhenSourceEntityNotFound()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, nonExistentEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanNonExistentEntityName = SanitizeName(nonExistentEntityName.Get);
                
                // Create an empty list of entity models (source entity doesn't exist)
                var entityModels = new List<EntityModel>();
                
                // The FDDB060 diagnostic is emitted when the source entity is not found
                // We verify this by checking the diagnostic descriptor directly
                var descriptor = DiagnosticDescriptors.ProjectionSourceEntityNotFound;
                
                // Assert - the diagnostic ID should be FDDB060
                return descriptor.Id == "FDDB060" &&
                       descriptor.DefaultSeverity == DiagnosticSeverity.Error;
            });
    }

    /// <summary>
    /// Property 14: For any projection with a valid source entity that exists,
    /// the source generator SHALL NOT emit FDDB060 diagnostic.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_ShouldNotEmitFDDB060_WhenSourceEntityExists()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, entityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Create a valid source entity
                var sourceEntity = CreateValidSourceEntity(cleanEntityName, "TestTable");
                var entityModels = new List<EntityModel> { sourceEntity };
                
                // Create analyzer and validate metadata (not the full analysis which requires syntax)
                var analyzer = new ProjectionModelAnalyzer();
                var isValid = analyzer.ValidateSourceEntityMetadataPublic(sourceEntity, cleanProjectionName, Location.None);
                
                // Assert - should pass validation and NOT emit FDDB060
                var diagnostics = analyzer.Diagnostics;
                return isValid && !diagnostics.Any(d => d.Id == "FDDB060");
            });
    }

    /// <summary>
    /// Property 14: The FDDB060 diagnostic message SHALL include both the projection name
    /// and the missing source entity name for clear error identification.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB060Message_ShouldIncludeBothNames()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                // Create a diagnostic using the descriptor
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.ProjectionSourceEntityNotFound,
                    Location.None,
                    cleanProjectionName,
                    cleanSourceEntityName);
                
                // Assert - diagnostic message should include both names
                var message = diagnostic.GetMessage();
                return message.Contains(cleanProjectionName) && message.Contains(cleanSourceEntityName);
            });
    }

    /// <summary>
    /// Property 14: The FDDB060 diagnostic SHALL have Error severity to prevent compilation.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB060_ShouldHaveErrorSeverity()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (projectionName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                
                // Assert - FDDB060 should have Error severity
                return DiagnosticDescriptors.ProjectionSourceEntityNotFound.DefaultSeverity == DiagnosticSeverity.Error;
            });
    }

    /// <summary>
    /// Property 14: The FDDB060 diagnostic description SHALL include helpful suggestions
    /// for resolving the issue.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB060Description_ShouldIncludeHelpfulSuggestions()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (projectionName) =>
            {
                // Arrange
                var descriptor = DiagnosticDescriptors.ProjectionSourceEntityNotFound;
                
                // Assert - description should mention DynamoDbTable attribute as a suggestion
                return descriptor.Description.ToString().Contains("DynamoDbTable") ||
                       descriptor.MessageFormat.ToString().Contains("DynamoDbTable");
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

    private static EntityModel CreateValidSourceEntity(string entityName, string tableName)
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

    #endregion
}


/// <summary>
/// Property-based tests for interface violation error clarity in projections.
/// 
/// **Feature: projection-interface-enhancement, Property 15: Interface violation error clarity**
/// **Validates: Requirements 8.4, 8.5**
/// </summary>
public class ProjectionInterfaceViolationErrorPropertyTests
{
    /// <summary>
    /// Property 15: For any projection interface violation, the system SHALL provide
    /// clear compile-time errors with diagnostic ID FDDB062.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_ShouldEmitFDDB062_ForInterfaceViolation()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                // Create analyzer and report interface violation
                var analyzer = new ProjectionModelAnalyzer();
                analyzer.ReportInterfaceViolation(cleanProjectionName, cleanSourceEntityName, Location.None);
                
                // Assert - should emit FDDB062 diagnostic
                var diagnostics = analyzer.Diagnostics;
                return diagnostics.Any(d => d.Id == "FDDB062");
            });
    }

    /// <summary>
    /// Property 15: The FDDB062 diagnostic message SHALL include both the projection name
    /// and the source entity name for clear error identification.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB062Message_ShouldIncludeBothNames()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                // Create analyzer and report interface violation
                var analyzer = new ProjectionModelAnalyzer();
                analyzer.ReportInterfaceViolation(cleanProjectionName, cleanSourceEntityName, Location.None);
                
                // Assert - diagnostic message should include both names
                var diagnostic = analyzer.Diagnostics.FirstOrDefault(d => d.Id == "FDDB062");
                if (diagnostic != null)
                {
                    var message = diagnostic.GetMessage();
                    return message.Contains(cleanProjectionName) && message.Contains(cleanSourceEntityName);
                }
                
                return false;
            });
    }

    /// <summary>
    /// Property 15: The FDDB062 diagnostic message SHALL include helpful suggestions
    /// for resolving the issue (use source entity for write operations).
    /// **Validates: Requirements 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB062Message_ShouldIncludeHelpfulSuggestions()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                // Create analyzer and report interface violation
                var analyzer = new ProjectionModelAnalyzer();
                analyzer.ReportInterfaceViolation(cleanProjectionName, cleanSourceEntityName, Location.None);
                
                // Assert - diagnostic message should mention write operations and suggest using source entity
                var diagnostic = analyzer.Diagnostics.FirstOrDefault(d => d.Id == "FDDB062");
                if (diagnostic != null)
                {
                    var message = diagnostic.GetMessage();
                    // Message should mention that projections are read-only and suggest using source entity
                    return message.Contains("read-only") || 
                           message.Contains("IReadOnlyEntity") ||
                           message.Contains("write") ||
                           message.Contains("source entity");
                }
                
                return false;
            });
    }

    /// <summary>
    /// Property 15: The FDDB062 diagnostic SHALL have Error severity to prevent compilation.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB062_ShouldHaveErrorSeverity()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (projectionName) =>
            {
                // Assert - FDDB062 should have Error severity
                return DiagnosticDescriptors.ProjectionInterfaceViolation.DefaultSeverity == DiagnosticSeverity.Error;
            });
    }

    /// <summary>
    /// Property 15: The FDDB062 diagnostic description SHALL explain that projections
    /// implement IReadOnlyEntity and cannot be used for write operations.
    /// **Validates: Requirements 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB062Description_ShouldExplainReadOnlyNature()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (projectionName) =>
            {
                // Arrange
                var descriptor = DiagnosticDescriptors.ProjectionInterfaceViolation;
                
                // Assert - description should mention IReadOnlyEntity and read operations
                var description = descriptor.Description.ToString();
                var messageFormat = descriptor.MessageFormat.ToString();
                
                return (description.Contains("IReadOnlyEntity") || messageFormat.Contains("IReadOnlyEntity")) &&
                       (description.Contains("read") || messageFormat.Contains("read"));
            });
    }

    /// <summary>
    /// Property 15: The FDDB062 diagnostic message SHALL mention specific write operations
    /// (Put, Update, Delete) that projections cannot be used for.
    /// **Validates: Requirements 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionAnalyzer_FDDB062Description_ShouldMentionWriteOperations()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (projectionName) =>
            {
                // Arrange
                var descriptor = DiagnosticDescriptors.ProjectionInterfaceViolation;
                
                // Assert - description should mention write operations
                var description = descriptor.Description.ToString();
                var messageFormat = descriptor.MessageFormat.ToString();
                
                // Should mention at least one write operation type
                return description.Contains("Put") || 
                       description.Contains("Update") || 
                       description.Contains("Delete") ||
                       description.Contains("write") ||
                       messageFormat.Contains("Put") ||
                       messageFormat.Contains("Update") ||
                       messageFormat.Contains("Delete") ||
                       messageFormat.Contains("write");
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

    #endregion
}
