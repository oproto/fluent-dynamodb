using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Validation;

namespace Oproto.FluentDynamoDb.UnitTests.Validation;

/// <summary>
/// Property-based tests for schema validation types.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class SchemaValidationPropertyTests
{
    /// <summary>
    /// **Feature: schema-validation, Property 12: IndexType correctly identifies GSI vs LSI**
    /// 
    /// For any entity with [GsiPartitionKey]/[GsiSortKey] attributes, the generated IndexMetadata SHALL have 
    /// IndexType = GlobalSecondaryIndex. For any entity with [LsiSortKey] attributes, 
    /// the generated IndexMetadata SHALL have IndexType = LocalSecondaryIndex.
    /// 
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndexType_HasDistinctValues_ForGsiAndLsi()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange & Act
                var gsiValue = IndexType.GlobalSecondaryIndex;
                var lsiValue = IndexType.LocalSecondaryIndex;
                
                // Assert - GSI and LSI should be distinct enum values
                var valuesAreDifferent = gsiValue != lsiValue;
                
                // Assert - enum values should be defined
                var gsiIsDefined = Enum.IsDefined(typeof(IndexType), gsiValue);
                var lsiIsDefined = Enum.IsDefined(typeof(IndexType), lsiValue);
                
                // Assert - enum should have exactly 2 values (GSI and LSI)
                var enumValues = Enum.GetValues(typeof(IndexType));
                var hasExactlyTwoValues = enumValues.Length == 2;
                
                return (valuesAreDifferent && gsiIsDefined && lsiIsDefined && hasExactlyTwoValues).ToProperty()
                    .Label($"IndexType should have distinct GSI and LSI values. " +
                           $"ValuesAreDifferent: {valuesAreDifferent}, GsiIsDefined: {gsiIsDefined}, " +
                           $"LsiIsDefined: {lsiIsDefined}, HasExactlyTwoValues: {hasExactlyTwoValues}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 12: IndexType correctly identifies GSI vs LSI**
    /// 
    /// For any IndexMetadata instance, setting IndexType to GlobalSecondaryIndex SHALL correctly
    /// identify the index as a GSI, and setting it to LocalSecondaryIndex SHALL correctly
    /// identify the index as an LSI.
    /// 
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndexMetadata_IndexType_CorrectlyIdentifiesGsiVsLsi()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            indexName =>
            {
                // Arrange - create two IndexMetadata instances
                var gsiMetadata = new IndexMetadata
                {
                    IndexName = indexName,
                    IndexType = IndexType.GlobalSecondaryIndex
                };
                
                var lsiMetadata = new IndexMetadata
                {
                    IndexName = indexName,
                    IndexType = IndexType.LocalSecondaryIndex
                };
                
                // Assert - GSI metadata should identify as GSI
                var gsiIsCorrectlyIdentified = gsiMetadata.IndexType == IndexType.GlobalSecondaryIndex;
                
                // Assert - LSI metadata should identify as LSI
                var lsiIsCorrectlyIdentified = lsiMetadata.IndexType == IndexType.LocalSecondaryIndex;
                
                // Assert - they should be different
                var typesAreDifferent = gsiMetadata.IndexType != lsiMetadata.IndexType;
                
                return (gsiIsCorrectlyIdentified && lsiIsCorrectlyIdentified && typesAreDifferent).ToProperty()
                    .Label($"IndexMetadata.IndexType should correctly identify GSI vs LSI. " +
                           $"IndexName: {indexName}, GsiIsCorrectlyIdentified: {gsiIsCorrectlyIdentified}, " +
                           $"LsiIsCorrectlyIdentified: {lsiIsCorrectlyIdentified}, TypesAreDifferent: {typesAreDifferent}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 12: IndexType correctly identifies GSI vs LSI**
    /// 
    /// For any IndexMetadata instance, the default IndexType SHALL be GlobalSecondaryIndex
    /// to maintain backward compatibility with existing code.
    /// 
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndexMetadata_DefaultIndexType_IsGlobalSecondaryIndex()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            indexName =>
            {
                // Arrange & Act - create IndexMetadata without setting IndexType
                var metadata = new IndexMetadata
                {
                    IndexName = indexName
                };
                
                // Assert - default should be GlobalSecondaryIndex for backward compatibility
                var defaultIsGsi = metadata.IndexType == IndexType.GlobalSecondaryIndex;
                
                return defaultIsGsi.ToProperty()
                    .Label($"IndexMetadata default IndexType should be GlobalSecondaryIndex. " +
                           $"IndexName: {indexName}, DefaultIsGsi: {defaultIsGsi}, ActualValue: {metadata.IndexType}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 13: Default projection type is ALL**
    /// 
    /// For any index without a defined projection model, the generated IndexMetadata SHALL have 
    /// ProjectionType = All.
    /// 
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndexMetadata_DefaultProjectionType_IsAll()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            indexName =>
            {
                // Arrange & Act - create IndexMetadata without setting ProjectionType
                var metadata = new IndexMetadata
                {
                    IndexName = indexName
                };
                
                // Assert - default should be ProjectionType.All
                var defaultIsAll = metadata.ProjectionType == ProjectionType.All;
                
                return defaultIsAll.ToProperty()
                    .Label($"IndexMetadata default ProjectionType should be All. " +
                           $"IndexName: {indexName}, DefaultIsAll: {defaultIsAll}, ActualValue: {metadata.ProjectionType}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 13: Default projection type is ALL**
    /// 
    /// For any IndexMetadata instance, the ProjectionType enum SHALL have exactly three values:
    /// All, KeysOnly, and Include.
    /// 
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectionType_HasCorrectEnumValues()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange & Act
                var allValue = ProjectionType.All;
                var keysOnlyValue = ProjectionType.KeysOnly;
                var includeValue = ProjectionType.Include;
                
                // Assert - all values should be defined
                var allIsDefined = Enum.IsDefined(typeof(ProjectionType), allValue);
                var keysOnlyIsDefined = Enum.IsDefined(typeof(ProjectionType), keysOnlyValue);
                var includeIsDefined = Enum.IsDefined(typeof(ProjectionType), includeValue);
                
                // Assert - enum should have exactly 3 values
                var enumValues = Enum.GetValues(typeof(ProjectionType));
                var hasExactlyThreeValues = enumValues.Length == 3;
                
                // Assert - all values should be distinct
                var valuesAreDistinct = allValue != keysOnlyValue && 
                                        allValue != includeValue && 
                                        keysOnlyValue != includeValue;
                
                return (allIsDefined && keysOnlyIsDefined && includeIsDefined && 
                        hasExactlyThreeValues && valuesAreDistinct).ToProperty()
                    .Label($"ProjectionType should have exactly 3 distinct values (All, KeysOnly, Include). " +
                           $"AllIsDefined: {allIsDefined}, KeysOnlyIsDefined: {keysOnlyIsDefined}, " +
                           $"IncludeIsDefined: {includeIsDefined}, HasExactlyThreeValues: {hasExactlyThreeValues}, " +
                           $"ValuesAreDistinct: {valuesAreDistinct}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 13: Default projection type is ALL**
    /// 
    /// For any IndexMetadata instance, when HasProjectionModel is false (default), 
    /// the ProjectionType should default to All.
    /// 
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndexMetadata_WithoutProjectionModel_DefaultsToAllProjection()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            (indexName, partitionKeyProperty) =>
            {
                // Arrange & Act - create IndexMetadata without setting projection-related properties
                var metadata = new IndexMetadata
                {
                    IndexName = indexName,
                    PartitionKeyProperty = partitionKeyProperty
                };
                
                // Assert - HasProjectionModel should default to false
                var hasProjectionModelDefaultsFalse = metadata.HasProjectionModel == false;
                
                // Assert - ProjectionType should default to All
                var projectionTypeDefaultsToAll = metadata.ProjectionType == ProjectionType.All;
                
                return (hasProjectionModelDefaultsFalse && projectionTypeDefaultsToAll).ToProperty()
                    .Label($"IndexMetadata without projection model should default to ProjectionType.All. " +
                           $"IndexName: {indexName}, HasProjectionModelDefaultsFalse: {hasProjectionModelDefaultsFalse}, " +
                           $"ProjectionTypeDefaultsToAll: {projectionTypeDefaultsToAll}, " +
                           $"ActualProjectionType: {metadata.ProjectionType}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 11: ThrowOnError throws only when errors exist**
    /// 
    /// For any validation result, calling ThrowOnError() SHALL throw SchemaValidationException 
    /// if and only if IsValid = false.
    /// 
    /// **Validates: Requirements 9.2, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ThrowOnError_ThrowsOnlyWhenErrorsExist()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            Arb.Default.PositiveInt(),
            (hasErrors, errorCount) =>
            {
                // Arrange - create a validation result with or without errors
                var errors = new List<SchemaValidationError>();
                var warnings = new List<SchemaValidationWarning>();
                
                if (hasErrors)
                {
                    // Add some errors
                    var count = Math.Min(errorCount.Get, 10); // Cap at 10 for performance
                    for (var i = 0; i < count; i++)
                    {
                        errors.Add(new SchemaValidationError(
                            SchemaValidationErrorCode.PartitionKeyNameMismatch,
                            $"element_{i}",
                            $"expected_{i}",
                            $"actual_{i}",
                            $"Error message {i}"));
                    }
                }
                
                // Always add a warning to ensure warnings don't affect ThrowOnError
                warnings.Add(new SchemaValidationWarning(
                    SchemaValidationWarningCode.UnexpectedGsi,
                    "test_gsi",
                    "Unexpected GSI found"));
                
                var result = new SchemaValidationResult(errors, warnings);
                
                // Act & Assert
                var threwException = false;
                SchemaValidationException? caughtException = null;
                
                try
                {
                    result.ThrowOnError();
                }
                catch (SchemaValidationException ex)
                {
                    threwException = true;
                    caughtException = ex;
                }
                
                // Property: ThrowOnError throws if and only if IsValid is false
                var throwsOnlyWhenInvalid = threwException == !result.IsValid;
                
                // Property: When thrown, exception contains the validation result
                var exceptionContainsResult = !threwException || 
                    (caughtException != null && caughtException.ValidationResult == result);
                
                // Property: IsValid is false if and only if there are errors
                var isValidMatchesErrors = result.IsValid == (errors.Count == 0);
                
                return (throwsOnlyWhenInvalid && exceptionContainsResult && isValidMatchesErrors).ToProperty()
                    .Label($"ThrowOnError should throw only when errors exist. " +
                           $"HasErrors: {hasErrors}, ErrorCount: {errors.Count}, " +
                           $"IsValid: {result.IsValid}, ThrewException: {threwException}, " +
                           $"ThrowsOnlyWhenInvalid: {throwsOnlyWhenInvalid}, " +
                           $"ExceptionContainsResult: {exceptionContainsResult}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 11: ThrowOnError throws only when errors exist**
    /// 
    /// For any validation result with no errors, calling ThrowOnError() SHALL NOT throw,
    /// regardless of how many warnings exist.
    /// 
    /// **Validates: Requirements 9.2, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ThrowOnError_DoesNotThrowForWarningsOnly()
    {
        return Prop.ForAll(
            Arb.Default.PositiveInt(),
            warningCount =>
            {
                // Arrange - create a validation result with only warnings
                var errors = new List<SchemaValidationError>();
                var warnings = new List<SchemaValidationWarning>();
                
                var count = Math.Min(warningCount.Get, 10); // Cap at 10 for performance
                for (var i = 0; i < count; i++)
                {
                    warnings.Add(new SchemaValidationWarning(
                        SchemaValidationWarningCode.UnexpectedGsi,
                        $"gsi_{i}",
                        $"Warning message {i}"));
                }
                
                var result = new SchemaValidationResult(errors, warnings);
                
                // Act & Assert
                var threwException = false;
                
                try
                {
                    result.ThrowOnError();
                }
                catch (SchemaValidationException)
                {
                    threwException = true;
                }
                
                // Property: Should not throw when there are no errors
                var doesNotThrow = !threwException;
                
                // Property: IsValid should be true when there are no errors
                var isValid = result.IsValid;
                
                return (doesNotThrow && isValid).ToProperty()
                    .Label($"ThrowOnError should not throw for warnings only. " +
                           $"WarningCount: {warnings.Count}, IsValid: {isValid}, " +
                           $"ThrewException: {threwException}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 11: ThrowOnError throws only when errors exist**
    /// 
    /// For any validation result with errors, the thrown SchemaValidationException SHALL
    /// contain the same validation result that was used to throw.
    /// 
    /// **Validates: Requirements 9.2, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ThrowOnError_ExceptionContainsValidationResult()
    {
        return Prop.ForAll(
            Arb.Default.PositiveInt(),
            errorCount =>
            {
                // Arrange - create a validation result with errors
                var errors = new List<SchemaValidationError>();
                
                var count = Math.Max(1, Math.Min(errorCount.Get, 10)); // At least 1, cap at 10
                for (var i = 0; i < count; i++)
                {
                    errors.Add(new SchemaValidationError(
                        SchemaValidationErrorCode.GsiNotFound,
                        $"gsi_{i}",
                        $"expected_{i}",
                        "not found",
                        $"GSI not found: gsi_{i}"));
                }
                
                var result = new SchemaValidationResult(errors, new List<SchemaValidationWarning>());
                
                // Act
                SchemaValidationException? caughtException = null;
                
                try
                {
                    result.ThrowOnError();
                }
                catch (SchemaValidationException ex)
                {
                    caughtException = ex;
                }
                
                // Assert
                var exceptionWasThrown = caughtException != null;
                var exceptionContainsResult = caughtException?.ValidationResult == result;
                var exceptionMessageContainsCount = caughtException?.Message.Contains(count.ToString()) ?? false;
                
                return (exceptionWasThrown && exceptionContainsResult && exceptionMessageContainsCount).ToProperty()
                    .Label($"Exception should contain the validation result. " +
                           $"ErrorCount: {count}, ExceptionWasThrown: {exceptionWasThrown}, " +
                           $"ExceptionContainsResult: {exceptionContainsResult}, " +
                           $"MessageContainsCount: {exceptionMessageContainsCount}");
            });
    }
}
