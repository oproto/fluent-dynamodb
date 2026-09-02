using System.Linq.Expressions;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Preservation property tests for the multi-computed-field-target fix.
/// These tests MUST PASS on unfixed code — they capture baseline behavior that
/// must remain unchanged after the fix is applied.
///
/// Property 2: Preservation — Single-Target and Non-Source Behavior Unchanged
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
[Trait("Category", "Preservation")]
public class MultiComputedFieldTargetPreservationTests
{
    #region Test Entity Classes

    private class PreservationEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? SourceProp { get; set; }
        public string? OtherSource { get; set; }
        public string? NonSourceProp { get; set; }
        public string? ComputedTarget { get; set; }
        public string? ExtractedProp { get; set; }
    }

    private class PreservationUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> SourceProp { get; } = new();
        public UpdateExpressionProperty<string?> OtherSource { get; } = new();
        public UpdateExpressionProperty<string?> NonSourceProp { get; } = new();
        public UpdateExpressionProperty<string?> ComputedTarget { get; } = new();
        public UpdateExpressionProperty<string?> ExtractedProp { get; } = new();
    }

    private class PreservationUpdateModel
    {
        public string? Id { get; set; }
        public string? SourceProp { get; set; }
        public string? OtherSource { get; set; }
        public string? NonSourceProp { get; set; }
        public string? ComputedTarget { get; set; }
        public string? ExtractedProp { get; set; }
    }

    #endregion

    #region Helpers

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext(EntityMetadata metadata)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    /// <summary>
    /// Creates metadata for single-target source property scenario.
    /// SourceProp and OtherSource are sources of a single non-key computed field.
    /// </summary>
    private EntityMetadata CreateSingleTargetMetadata(string sourceName, string otherSourceName, string targetName)
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = sourceName,
                    AttributeName = sourceName.ToLowerInvariant(),
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { targetName }
                },
                new PropertyMetadata
                {
                    PropertyName = otherSourceName,
                    AttributeName = otherSourceName.ToLowerInvariant(),
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { targetName }
                },
                new PropertyMetadata
                {
                    PropertyName = "NonSourceProp",
                    AttributeName = "nonsourceprop",
                    PropertyType = typeof(string)
                },
                new PropertyMetadata
                {
                    PropertyName = targetName,
                    AttributeName = targetName.ToLowerInvariant(),
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { sourceName, otherSourceName },
                        Format = "{0}#{1}"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates metadata for non-source property scenario.
    /// NonSourceProp is not referenced by any computed field's SourceProperties.
    /// </summary>
    private EntityMetadata CreateNonSourceMetadata(string nonSourceName)
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = nonSourceName,
                    AttributeName = nonSourceName.ToLowerInvariant(),
                    PropertyType = typeof(string)
                    // No ComputedFieldTargets — not a source
                },
                new PropertyMetadata
                {
                    PropertyName = "ComputedTarget",
                    AttributeName = "computedtarget",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "SomeOtherProperty" },
                        Format = "{0}"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates metadata for extracted property scenario.
    /// ExtractedProp targets a non-key computed field via the extracted-field path.
    /// </summary>
    private EntityMetadata CreateExtractedFieldMetadata(string extractedName, string computedFieldName)
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "SourceProp",
                    AttributeName = "sourceprop",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { computedFieldName }
                },
                new PropertyMetadata
                {
                    PropertyName = extractedName,
                    AttributeName = extractedName.ToLowerInvariant(),
                    PropertyType = typeof(string),
                    ExtractedField = new ExtractedFieldMetadata
                    {
                        SourceProperty = computedFieldName,
                        Index = 0
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = computedFieldName,
                    AttributeName = computedFieldName.ToLowerInvariant(),
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "SourceProp" },
                        Format = "{0}"
                    }
                    // Not a partition key or sort key — non-key computed field
                }
            }
        };
    }

    #endregion

    #region Property 2.1: Single-Target Source Properties Are Identified as Computed Sources

    /// <summary>
    /// Property 2.1: For all single-target source properties, IsComputedSourceProperty returns true.
    ///
    /// Observation: On unfixed code, a source property contributing to exactly one non-key computed
    /// field has ComputedFieldTarget set to that target name. When such a property is assigned in
    /// an update expression, the translator recognizes it as a computed source and stores the value
    /// for recomputation rather than generating a standard SET operation.
    ///
    /// Verification: Assigning all source properties of a single computed field triggers recomputation,
    /// producing a SET expression for the computed field's attribute with the concatenated value.
    ///
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property SingleTargetSourceProperty_IsIdentifiedAsComputedSource()
    {
        // Generate arbitrary source values
        var sourceValueGen = Gen.Elements(
            "Electronics", "Clothing", "Books", "Food", "Sports",
            "US-East", "EU-West", "Asia", "Pending", "Active");

        var otherSourceValueGen = Gen.Elements(
            "Premium", "Basic", "Standard", "Express", "Economy",
            "CategoryA", "CategoryB", "TypeX", "TypeY", "TypeZ");

        var inputGen = from sourceVal in sourceValueGen
                       from otherVal in otherSourceValueGen
                       select (sourceVal, otherVal);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (sourceValue, otherSourceValue) = input;

                // Set up metadata: SourceProp and OtherSource feed single computed field "ComputedTarget"
                var metadata = CreateSingleTargetMetadata("SourceProp", "OtherSource", "ComputedTarget");
                var translator = CreateTranslator();
                var context = CreateContext(metadata);

                // Assign all source properties (required to avoid FDDB072)
                Expression<Func<PreservationUpdateExpressions, PreservationUpdateModel>> expression =
                    x => new PreservationUpdateModel { SourceProp = sourceValue, OtherSource = otherSourceValue };

                // Act: translate the expression
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert: The translator should produce a SET for the computed field's attribute
                // (not individual SET operations for the source properties).
                // The recomputed value should be "sourceValue#otherSourceValue"
                var expectedRecomputedValue = $"{sourceValue}#{otherSourceValue}";

                var hasComputedFieldSet = context.AttributeNames.AttributeNames.Values
                    .Contains("computedtarget");
                var hasRecomputedValue = context.AttributeValues.AttributeValues.Values
                    .Any(av => av.S == expectedRecomputedValue);

                return (hasComputedFieldSet && hasRecomputedValue)
                    .Label($"Source values: '{sourceValue}', '{otherSourceValue}'. " +
                           $"Expected computed field 'computedtarget' with value '{expectedRecomputedValue}'. " +
                           $"HasComputedFieldSet={hasComputedFieldSet}, HasRecomputedValue={hasRecomputedValue}. " +
                           $"Result: {result}");
            });
    }

    #endregion

    #region Property 2.2: Non-Source Properties Are Not Identified as Computed Sources

    /// <summary>
    /// Property 2.2: For all non-source properties (not in any computed field's SourceProperties),
    /// IsComputedSourceProperty returns false.
    ///
    /// Observation: On unfixed code, a property with ComputedFieldTarget == null that is not in
    /// any computed field's SourceProperties list is treated as a normal property. Assigning it
    /// generates a standard SET operation.
    ///
    /// Verification: Assigning a non-source property produces a standard "SET #attrN = :pN" expression
    /// and does NOT trigger computed field recomputation.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property NonSourceProperty_IsNotIdentifiedAsComputedSource()
    {
        // Generate arbitrary values for non-source property
        var nonSourceValueGen = Gen.Elements(
            "Hello", "World", "Test", "Value", "Alpha",
            "Beta", "Gamma", "Delta", "Epsilon", "Zeta");

        return Prop.ForAll(
            nonSourceValueGen.ToArbitrary(),
            nonSourceValue =>
            {
                // Set up metadata: NonSourceProp is NOT listed in any computed field's SourceProperties
                var metadata = CreateNonSourceMetadata("NonSourceProp");
                var translator = CreateTranslator();
                var context = CreateContext(metadata);

                // Assign the non-source property
                Expression<Func<PreservationUpdateExpressions, PreservationUpdateModel>> expression =
                    x => new PreservationUpdateModel { NonSourceProp = nonSourceValue };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert: Should produce a standard SET expression for nonsourceprop
                var isStandardSet = result.StartsWith("SET ");
                var hasNonSourceAttribute = context.AttributeNames.AttributeNames.Values
                    .Contains("nonsourceprop");
                var hasAssignedValue = context.AttributeValues.AttributeValues.Values
                    .Any(av => av.S == nonSourceValue);

                // Should NOT contain the computed field's attribute
                var doesNotTriggerComputed = !context.AttributeNames.AttributeNames.Values
                    .Contains("computedtarget");

                return (isStandardSet && hasNonSourceAttribute && hasAssignedValue && doesNotTriggerComputed)
                    .Label($"Non-source value: '{nonSourceValue}'. " +
                           $"IsStandardSet={isStandardSet}, HasAttribute={hasNonSourceAttribute}, " +
                           $"HasValue={hasAssignedValue}, NoComputedTriggered={doesNotTriggerComputed}. " +
                           $"Result: {result}");
            });
    }

    #endregion

    #region Property 2.3: Extracted Properties Targeting Non-Key Computed Fields

    /// <summary>
    /// Property 2.3: For all extracted properties targeting non-key computed fields,
    /// IsComputedSourceProperty returns true via the extracted-field path.
    ///
    /// Observation: On unfixed code, when a property has ExtractedField metadata pointing to a
    /// non-key computed field, IsComputedSourceProperty detects it as a computed source via the
    /// extracted-field path (checking ExtractedField.SourceProperty → ComputedField != null
    /// and not PK/SK). The translator stores it in pendingComputedAssignments and later
    /// ValidateAndProcessComputedFields generates SET operations for the computed field,
    /// the source properties, AND the extracted properties (since they have their own attributes).
    ///
    /// Verification: Assigning an extracted property that targets a non-key computed field
    /// triggers the computed field recomputation path. The computed target attribute gets a
    /// SET with the recomputed value. This confirms IsComputedSourceProperty returns true
    /// for the extracted property (otherwise it would go through normal classification and
    /// never trigger recomputation).
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ExtractedPropertyTargetingNonKeyComputedField_IsIdentifiedAsComputedSource()
    {
        // Generate arbitrary values for the extracted property and the direct source
        var extractedValueGen = Gen.Elements(
            "Part1", "Part2", "Part3", "SegA", "SegB",
            "Comp1", "Comp2", "Comp3", "Elem1", "Elem2");

        var sourceValueGen = Gen.Elements(
            "FullValue1", "FullValue2", "Combined1", "Combined2",
            "Source1", "Source2", "Input1", "Input2", "Data1", "Data2");

        var inputGen = from extractedVal in extractedValueGen
                       from sourceVal in sourceValueGen
                       select (extractedVal, sourceVal);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (extractedValue, sourceValue) = input;

                // Set up metadata: ExtractedProp targets non-key computed field "ComputedTarget"
                // ExtractedProp has index 0 → maps to first source property ("SourceProp")
                var metadata = CreateExtractedFieldMetadata("ExtractedProp", "ComputedTarget");
                var translator = CreateTranslator();
                var context = CreateContext(metadata);

                // Assign both the extracted property and the direct source.
                // The extracted property at index 0 maps to the same source slot as "SourceProp".
                // Both go into pendingComputedAssignments via the IsComputedSourceProperty path.
                Expression<Func<PreservationUpdateExpressions, PreservationUpdateModel>> expression =
                    x => new PreservationUpdateModel { ExtractedProp = extractedValue, SourceProp = sourceValue };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert: The extracted property should be treated as a computed source.
                // Key indicator: computedtarget gets a recomputed SET value, proving that
                // IsComputedSourceProperty returned true for ExtractedProp (otherwise the
                // recomputation path would never be triggered).
                var hasComputedFieldSet = context.AttributeNames.AttributeNames.Values
                    .Contains("computedtarget");

                // The extracted property also gets a SET (because ValidateAndProcessComputedFields
                // generates SETs for extracted properties that have their own attribute names).
                // This confirms it went through the COMPUTED path, not the normal SET path.
                var hasExtractedPropSet = context.AttributeNames.AttributeNames.Values
                    .Contains("extractedprop");

                // The result should be a SET expression (not empty / not an error)
                var isSetExpression = result.StartsWith("SET ");

                return (hasComputedFieldSet && hasExtractedPropSet && isSetExpression)
                    .Label($"Extracted value: '{extractedValue}', Source value: '{sourceValue}'. " +
                           $"HasComputedFieldSet={hasComputedFieldSet}, " +
                           $"HasExtractedPropSet={hasExtractedPropSet}, " +
                           $"IsSetExpression={isSetExpression}. " +
                           $"Result: {result}");
            });
    }

    #endregion
}
