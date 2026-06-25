using System.Linq.Expressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for computed field validation and recomputation in UpdateExpressionTranslator.
/// Tests cover Properties 6-11 from the update-model-computed-field-redesign design.
/// </summary>
public class UpdateExpressionTranslatorComputedFieldPropertyTests
{
    #region Test Infrastructure

    // --- Test entity classes for computed fields ---

    /// <summary>
    /// Update expressions type with computed field sources and a non-computed property.
    /// </summary>
    private class ComputedUpdateExpressions
    {
        public UpdateExpressionProperty<string?> Source1 { get; } = new();
        public UpdateExpressionProperty<string?> Source2 { get; } = new();
        public UpdateExpressionProperty<string?> Source3 { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
        public UpdateExpressionProperty<string?> RegularProp { get; } = new();
        public UpdateExpressionProperty<int> RegularInt { get; } = new();
    }

    private class ComputedUpdateModel
    {
        public string? Source1 { get; set; }
        public string? Source2 { get; set; }
        public string? Source3 { get; set; }
        public string? ComputedField { get; set; }
        public string? RegularProp { get; set; }
        public int? RegularInt { get; set; }
    }

    /// <summary>
    /// Update expressions type for multi-computed-field entity (Property 8 - independence).
    /// </summary>
    private class MultiComputedUpdateExpressions
    {
        public UpdateExpressionProperty<string?> SourceA1 { get; } = new();
        public UpdateExpressionProperty<string?> SourceA2 { get; } = new();
        public UpdateExpressionProperty<string?> SourceB1 { get; } = new();
        public UpdateExpressionProperty<string?> SourceB2 { get; } = new();
        public UpdateExpressionProperty<string?> ComputedA { get; } = new();
        public UpdateExpressionProperty<string?> ComputedB { get; } = new();
    }

    private class MultiComputedUpdateModel
    {
        public string? SourceA1 { get; set; }
        public string? SourceA2 { get; set; }
        public string? SourceB1 { get; set; }
        public string? SourceB2 { get; set; }
        public string? ComputedA { get; set; }
        public string? ComputedB { get; set; }
    }

    /// <summary>
    /// Update expressions type with extracted property targeting a computed field.
    /// </summary>
    private class ExtractedComputedUpdateExpressions
    {
        public UpdateExpressionProperty<string?> ExtractedProp { get; } = new();
        public UpdateExpressionProperty<string?> Source2 { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
    }

    private class ExtractedComputedUpdateModel
    {
        public string? ExtractedProp { get; set; }
        public string? Source2 { get; set; }
        public string? ComputedField { get; set; }
    }

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
    /// Creates metadata for a computed field entity with N source properties.
    /// </summary>
    private EntityMetadata CreateComputedFieldMetadata(
        string[] sourceProperties,
        string separator = "#",
        string? prefix = null,
        string? prefixSeparator = null)
    {
        var properties = new List<PropertyMetadata>();

        // Add the computed field itself
        properties.Add(new PropertyMetadata
        {
            PropertyName = "ComputedField",
            AttributeName = "computed_field",
            PropertyType = typeof(string),
            ComputedField = new ComputedFieldMetadata
            {
                SourceProperties = sourceProperties,
                Separator = separator,
                Prefix = prefix,
                PrefixSeparator = prefixSeparator
            }
        });

        // Add source properties
        foreach (var sourceName in sourceProperties)
        {
            properties.Add(new PropertyMetadata
            {
                PropertyName = sourceName,
                AttributeName = sourceName.ToLowerInvariant(),
                PropertyType = typeof(string),
                ComputedFieldTarget = "ComputedField"
            });
        }

        // Add a regular (non-computed) property
        properties.Add(new PropertyMetadata
        {
            PropertyName = "RegularProp",
            AttributeName = "regular_prop",
            PropertyType = typeof(string)
        });

        // Add a regular int property
        properties.Add(new PropertyMetadata
        {
            PropertyName = "RegularInt",
            AttributeName = "regular_int",
            PropertyType = typeof(int)
        });

        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = properties.ToArray()
        };
    }

    /// <summary>
    /// Creates metadata for an entity with two independent computed fields.
    /// </summary>
    private EntityMetadata CreateMultiComputedFieldMetadata(string separator = "#")
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "ComputedA",
                    AttributeName = "computed_a",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "SourceA1", "SourceA2" },
                        Separator = separator
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "ComputedB",
                    AttributeName = "computed_b",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "SourceB1", "SourceB2" },
                        Separator = separator
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "SourceA1",
                    AttributeName = "source_a1",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedA"
                },
                new PropertyMetadata
                {
                    PropertyName = "SourceA2",
                    AttributeName = "source_a2",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedA"
                },
                new PropertyMetadata
                {
                    PropertyName = "SourceB1",
                    AttributeName = "source_b1",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedB"
                },
                new PropertyMetadata
                {
                    PropertyName = "SourceB2",
                    AttributeName = "source_b2",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedB"
                }
            }
        };
    }

    /// <summary>
    /// Creates metadata with an extracted property targeting a computed field.
    /// </summary>
    private EntityMetadata CreateExtractedComputedFieldMetadata(string separator = "#")
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "ComputedField",
                    AttributeName = "computed_field",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Source1", "Source2" },
                        Separator = separator
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Source1",
                    AttributeName = "source1",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedField"
                },
                new PropertyMetadata
                {
                    PropertyName = "Source2",
                    AttributeName = "source2",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedField"
                },
                new PropertyMetadata
                {
                    PropertyName = "ExtractedProp",
                    AttributeName = "extracted_prop",
                    PropertyType = typeof(string),
                    ExtractedField = new ExtractedFieldMetadata
                    {
                        SourceProperty = "ComputedField",
                        Index = 0
                    }
                }
            }
        };
    }

    /// <summary>
    /// FsCheck generator for valid property name segments (alphanumeric, non-empty).
    /// </summary>
    private static Gen<string> GenPropertyValue()
    {
        return Gen.Elements("Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta",
            "One", "Two", "Three", "Four", "Five",
            "Dept", "Cat", "Region", "Status", "Type");
    }

    /// <summary>
    /// FsCheck generator for separator strings.
    /// </summary>
    private static Gen<string> GenSeparator()
    {
        return Gen.Elements("#", "-", "_", "|", "::", "~");
    }

    /// <summary>
    /// FsCheck generator for optional prefix strings.
    /// </summary>
    private static Gen<string> GenPrefix()
    {
        return Gen.Elements("PREFIX", "ORDER", "CUSTOMER", "ITEM", "GSI1");
    }

    #endregion

    #region Property 6: Partial Source Assignment Validation (FDDB072)

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 6: Partial Source Assignment Validation (FDDB072)**
    /// 
    /// *For any* computed field with N source properties where K source properties (0 &lt; K &lt; N)
    /// are assigned in an update expression, the expression translator SHALL throw an
    /// InvalidOperationException whose message identifies the computed field name and lists the
    /// (N - K) missing source property names. When K = N, no exception SHALL be thrown.
    /// 
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartialSourceAssignment_WithMissingSources_ShouldThrowFDDB072()
    {
        // Generate two distinct values for Source1 and Source2
        // Only assign Source1, leaving Source2 missing
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            (string value1) =>
            {
                // Arrange: 2-source computed field, assign only Source1
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue = value1;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source1 = capturedValue };

                // Act & Assert
                try
                {
                    translator.TranslateUpdateExpression(expression, context);
                    return false; // Should have thrown
                }
                catch (InvalidOperationException ex)
                {
                    // Verify message contains computed field name and missing source
                    return ex.Message.Contains("ComputedField") &&
                           ex.Message.Contains("Source2") &&
                           ex.Message.Contains("Missing");
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 6: Partial Source Assignment Validation (FDDB072)**
    /// 
    /// When K = N (all sources assigned), no exception SHALL be thrown.
    /// 
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullSourceAssignment_WithAllSources_ShouldNotThrow()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            GenSeparator().ToArbitrary(),
            (string value1, string value2, string separator) =>
            {
                // Arrange: 2-source computed field, assign both sources
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" }, separator);
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue1 = value1;
                var capturedValue2 = value2;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source1 = capturedValue1, Source2 = capturedValue2 };

                // Act
                try
                {
                    var result = translator.TranslateUpdateExpression(expression, context);
                    // Should succeed without exception
                    return result.Contains("SET");
                }
                catch (InvalidOperationException)
                {
                    return false; // Should not throw
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 6: Partial Source Assignment Validation (FDDB072)**
    /// 
    /// For a 3-source computed field, assigning only 1 should list 2 missing properties.
    /// 
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartialSourceAssignment_ThreeSourceField_ListsAllMissing()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            (string value1) =>
            {
                // Arrange: 3-source computed field, assign only Source1
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2", "Source3" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue = value1;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source1 = capturedValue };

                // Act & Assert
                try
                {
                    translator.TranslateUpdateExpression(expression, context);
                    return false; // Should have thrown
                }
                catch (InvalidOperationException ex)
                {
                    // Verify message lists both missing sources
                    return ex.Message.Contains("Source2") &&
                           ex.Message.Contains("Source3") &&
                           ex.Message.Contains("ComputedField");
                }
            });
    }

    #endregion

    #region Property 7: Mixed Direct and Source Assignment Validation (FDDB073)

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 7: Mixed Direct and Source Assignment Validation (FDDB073)**
    /// 
    /// When both the computed field property and any of its source properties are assigned in the
    /// same update expression, the expression translator SHALL throw an InvalidOperationException
    /// identifying the computed field.
    /// 
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MixedAssignment_DirectAndSource_ShouldThrowFDDB073()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            (string directValue, string sourceValue) =>
            {
                // Arrange: assign both ComputedField directly AND its Source1
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedDirect = directValue;
                var capturedSource = sourceValue;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel
                    {
                        ComputedField = capturedDirect,
                        Source1 = capturedSource
                    };

                // Act & Assert
                try
                {
                    translator.TranslateUpdateExpression(expression, context);
                    return false; // Should have thrown
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("ComputedField") &&
                           ex.Message.Contains("source properties");
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 7: Mixed Direct and Source Assignment Validation (FDDB073)**
    /// 
    /// When only the computed field itself is assigned directly (without any source properties),
    /// no FDDB073 exception SHALL be thrown.
    /// 
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirectOnlyAssignment_NoSources_ShouldNotThrowFDDB073()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            (string directValue) =>
            {
                // Arrange: assign only ComputedField directly, no sources
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedDirect = directValue;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { ComputedField = capturedDirect };

                // Act
                try
                {
                    var result = translator.TranslateUpdateExpression(expression, context);
                    // Should succeed — direct assignment to computed field is valid
                    return result.Contains("SET");
                }
                catch (InvalidOperationException)
                {
                    return false; // Should not throw FDDB073
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 7: Mixed Direct and Source Assignment Validation (FDDB073)**
    /// 
    /// When only the source properties are assigned (without the computed field directly),
    /// no FDDB073 exception SHALL be thrown (FDDB072 may throw if partial, but not FDDB073).
    /// 
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SourceOnlyAssignment_NoDirectField_ShouldNotThrowFDDB073()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            (string value1, string value2) =>
            {
                // Arrange: assign all sources, no direct computed field assignment
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue1 = value1;
                var capturedValue2 = value2;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source1 = capturedValue1, Source2 = capturedValue2 };

                // Act
                try
                {
                    var result = translator.TranslateUpdateExpression(expression, context);
                    // Should succeed without FDDB073 (all sources assigned so no FDDB072 either)
                    return result.Contains("SET");
                }
                catch (InvalidOperationException ex)
                {
                    // If it throws, it should NOT be FDDB073 (mixed assignment)
                    return !ex.Message.Contains("source properties in the same update");
                }
            });
    }

    #endregion

    #region Property 8: Independent Computed Field Validation

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 8: Independent Computed Field Validation**
    /// 
    /// For any entity with multiple independent computed fields, a validation failure (FDDB072 or FDDB073)
    /// on one computed field SHALL NOT affect the processing of other computed fields in the same expression.
    /// 
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndependentComputedFields_FailureOnOneDoesNotAffectOther()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            (string valueB1, string valueB2) =>
            {
                // Arrange: Two computed fields (A and B).
                // Assign all sources of B (valid), assign only one source of A (invalid - FDDB072).
                // The exception for A should reference "ComputedA" and NOT "ComputedB".
                var metadata = CreateMultiComputedFieldMetadata();
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedB1 = valueB1;
                var capturedB2 = valueB2;

                // Assign SourceA1 only (partial for ComputedA) AND both SourceB1+SourceB2 (complete for ComputedB)
                Expression<Func<MultiComputedUpdateExpressions, MultiComputedUpdateModel>> expression =
                    x => new MultiComputedUpdateModel
                    {
                        SourceA1 = "partialA",
                        SourceB1 = capturedB1,
                        SourceB2 = capturedB2
                    };

                // Act & Assert
                try
                {
                    translator.TranslateUpdateExpression(expression, context);
                    return false; // Should throw FDDB072 for ComputedA
                }
                catch (InvalidOperationException ex)
                {
                    // The error should be about ComputedA (partial assignment) not ComputedB
                    return ex.Message.Contains("ComputedA") &&
                           !ex.Message.Contains("ComputedB");
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 8: Independent Computed Field Validation**
    /// 
    /// When both computed fields have all sources assigned, both should succeed independently.
    /// 
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndependentComputedFields_BothFullyAssigned_ShouldSucceed()
    {
        var gen = from a1 in GenPropertyValue()
                  from a2 in GenPropertyValue()
                  from b1 in GenPropertyValue()
                  from b2 in GenPropertyValue()
                  select (a1, a2, b1, b2);

        return Prop.ForAll(
            gen.ToArbitrary(),
            (tuple) =>
            {
                var (a1, a2, b1, b2) = tuple;

                // Arrange: assign all sources for both computed fields
                var metadata = CreateMultiComputedFieldMetadata();
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var ca1 = a1;
                var ca2 = a2;
                var cb1 = b1;
                var cb2 = b2;

                Expression<Func<MultiComputedUpdateExpressions, MultiComputedUpdateModel>> expression =
                    x => new MultiComputedUpdateModel
                    {
                        SourceA1 = ca1,
                        SourceA2 = ca2,
                        SourceB1 = cb1,
                        SourceB2 = cb2
                    };

                // Act
                try
                {
                    var result = translator.TranslateUpdateExpression(expression, context);
                    // Should produce SET with two computed field recomputations
                    return result.Contains("SET");
                }
                catch (InvalidOperationException)
                {
                    return false; // Should not throw
                }
            });
    }

    #endregion

    #region Property 9: Entity Parameter Reference Detection (FDDB071)

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 9: Entity Parameter Reference Detection (FDDB071)**
    /// 
    /// When a source property value references the entity lambda parameter, the expression translator
    /// SHALL throw an InvalidOperationException identifying the property name.
    /// 
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EntityParameterReference_InSourceProperty_ShouldThrowFDDB071()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            (string value2) =>
            {
                // Arrange: assign Source1 with entity parameter reference
                // Simulate: x => new Model { Source1 = x.Source1.ToString() }
                // The entity parameter is transitively referenced through x.Source1
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);

                // Build expression that references the entity parameter for Source1
                // We create: x => new ComputedUpdateModel { Source1 = x.Source1.ToString() }
                // This accesses x (the entity parameter) which should trigger FDDB071
                var parameter = Expression.Parameter(typeof(ComputedUpdateExpressions), "x");
                var source1Access = Expression.Property(parameter, "Source1");
                var toStringMethod = typeof(object).GetMethod("ToString")!;
                var toStringCall = Expression.Call(source1Access, toStringMethod);

                var source1Binding = Expression.Bind(
                    typeof(ComputedUpdateModel).GetProperty("Source1")!,
                    toStringCall);

                var memberInit = Expression.MemberInit(
                    Expression.New(typeof(ComputedUpdateModel)),
                    source1Binding);
                var lambda = Expression.Lambda<Func<ComputedUpdateExpressions, ComputedUpdateModel>>(
                    memberInit, parameter);

                // Act & Assert
                try
                {
                    translator.TranslateUpdateExpression(lambda, context);
                    return false; // Should have thrown FDDB071
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("Source1") &&
                           ex.Message.Contains("entity parameter") &&
                           ex.Message.Contains("client-side");
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 9: Entity Parameter Reference Detection (FDDB071)**
    /// 
    /// When the assigned value is a constant or local variable that does NOT reference the entity
    /// parameter, no FDDB071 exception SHALL be thrown.
    /// 
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSourceValue_NoEntityReference_ShouldNotThrowFDDB071()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            (string value1, string value2) =>
            {
                // Arrange: assign both sources with constant/local values (no entity param reference)
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue1 = value1;
                var capturedValue2 = value2;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source1 = capturedValue1, Source2 = capturedValue2 };

                // Act
                try
                {
                    translator.TranslateUpdateExpression(expression, context);
                    return true; // No exception is correct
                }
                catch (InvalidOperationException ex)
                {
                    // Should NOT get FDDB071 for constants/locals
                    return !ex.Message.Contains("entity parameter");
                }
            });
    }

    #endregion

    #region Property 10: Recomputation Correctness

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 10: Recomputation Correctness**
    /// 
    /// All sources assigned produces SET with concatenated value in correct order with separator.
    /// 
    /// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Recomputation_AllSourcesAssigned_ProducesCorrectConcatenation()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            GenSeparator().ToArbitrary(),
            (string value1, string value2, string separator) =>
            {
                // Arrange: 2-source computed field with specified separator
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" }, separator);
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue1 = value1;
                var capturedValue2 = value2;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source1 = capturedValue1, Source2 = capturedValue2 };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The recomputed value should be value1 + separator + value2
                var expectedValue = value1 + separator + value2;
                var hasSetOperation = result.Contains("SET");

                // The captured attribute value should contain the concatenated result
                var capturedValue = context.AttributeValues.AttributeValues.Values
                    .FirstOrDefault(v => v.S == expectedValue);
                var valueCorrect = capturedValue != null;

                // Should also have individual SET operations for source properties
                // that have their own DynamoDB attributes (non-empty AttributeName)
                var attributeNames = context.AttributeNames.AttributeNames.Values.ToList();
                var hasComputedFieldAttribute = attributeNames.Contains("computed_field");
                var hasSource1Attribute = attributeNames.Contains("source1");
                var hasSource2Attribute = attributeNames.Contains("source2");

                return hasSetOperation && valueCorrect && hasComputedFieldAttribute &&
                       hasSource1Attribute && hasSource2Attribute;
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 10: Recomputation Correctness**
    /// 
    /// When a prefix is configured, the recomputed value SHALL be prepended with the prefix and separator.
    /// 
    /// **Validates: Requirements 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Recomputation_WithPrefix_PrependsCorrectly()
    {
        var gen = from value1 in GenPropertyValue()
                  from value2 in GenPropertyValue()
                  from separator in GenSeparator()
                  from prefix in GenPrefix()
                  select (value1, value2, separator, prefix);

        return Prop.ForAll(
            gen.ToArbitrary(),
            (tuple) =>
            {
                var (value1, value2, separator, prefix) = tuple;

                // Arrange: computed field with prefix configured
                var metadata = CreateComputedFieldMetadata(
                    new[] { "Source1", "Source2" },
                    separator,
                    prefix: prefix,
                    prefixSeparator: null); // null means use the field separator
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue1 = value1;
                var capturedValue2 = value2;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source1 = capturedValue1, Source2 = capturedValue2 };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // With prefix, the value should be: prefix + separator + value1 + separator + value2
                var expectedValue = prefix + separator + value1 + separator + value2;
                var capturedAttributeValue = context.AttributeValues.AttributeValues.Values
                    .FirstOrDefault(v => v.S == expectedValue);

                return capturedAttributeValue != null && result.Contains("SET");
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 10: Recomputation Correctness**
    /// 
    /// When a prefix is configured with a custom prefix separator, it uses that separator.
    /// 
    /// **Validates: Requirements 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Recomputation_WithPrefixAndCustomPrefixSeparator_UsesCustomSeparator()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            (string value1, string value2) =>
            {
                // Arrange: computed field with prefix and custom prefix separator
                var metadata = CreateComputedFieldMetadata(
                    new[] { "Source1", "Source2" },
                    separator: "#",
                    prefix: "PREFIX",
                    prefixSeparator: "|"); // Custom prefix separator
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue1 = value1;
                var capturedValue2 = value2;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source1 = capturedValue1, Source2 = capturedValue2 };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // Value should be: PREFIX|value1#value2
                var expectedValue = "PREFIX" + "|" + value1 + "#" + value2;
                var capturedAttributeValue = context.AttributeValues.AttributeValues.Values
                    .FirstOrDefault(v => v.S == expectedValue);

                return capturedAttributeValue != null && result.Contains("SET");
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 10: Recomputation Correctness**
    /// 
    /// Source values are concatenated in the positional order defined by the Computed attribute.
    /// 
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Recomputation_OrderMatters_ConcatenatesInDefinedOrder()
    {
        // Generate two distinct values to make order observable
        var gen = from v1 in GenPropertyValue()
                  from v2 in GenPropertyValue()
                  where v1 != v2
                  from sep in GenSeparator()
                  select (v1, v2, sep);

        return Prop.ForAll(
            gen.ToArbitrary(),
            (tuple) =>
            {
                var (value1, value2, separator) = tuple;

                // Arrange
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" }, separator);
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue1 = value1;
                var capturedValue2 = value2;

                // Assign in reverse order in the expression (Source2 first, Source1 second)
                // The recomputation should still use the defined order (Source1, Source2)
                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { Source2 = capturedValue2, Source1 = capturedValue1 };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert: Order should be Source1+sep+Source2, not Source2+sep+Source1
                var expectedValue = value1 + separator + value2;
                var capturedAttributeValue = context.AttributeValues.AttributeValues.Values
                    .FirstOrDefault(v => v.S == expectedValue);

                return capturedAttributeValue != null;
            });
    }

    #endregion

    #region Property 11: Backwards Compatibility for Non-Computed Properties

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 11: Backwards Compatibility for Non-Computed Properties**
    /// 
    /// Assignments to non-computed properties produce the same expressions and no FDDB exceptions.
    /// 
    /// **Validates: Requirements 9.1, 9.2, 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonComputedProperties_ShouldProduceStandardSETExpressions()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            (string value) =>
            {
                // Arrange: entity with computed field metadata, but update only RegularProp
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue = value;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { RegularProp = capturedValue };

                // Act
                try
                {
                    var result = translator.TranslateUpdateExpression(expression, context);

                    // Assert
                    var hasSetOperation = result.Contains("SET");
                    var hasCorrectAttribute = context.AttributeNames.AttributeNames.Values
                        .Contains("regular_prop");
                    var hasCorrectValue = context.AttributeValues.AttributeValues.Values
                        .Any(v => v.S == value);

                    return hasSetOperation && hasCorrectAttribute && hasCorrectValue;
                }
                catch (InvalidOperationException)
                {
                    return false; // Should not throw any FDDB exceptions
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 11: Backwards Compatibility for Non-Computed Properties**
    /// 
    /// Non-computed properties should NOT trigger FDDB071, FDDB072, or FDDB073.
    /// Mixed assignment of non-computed property alongside computed source properties
    /// should not affect non-computed property behavior.
    /// 
    /// **Validates: Requirements 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonComputedProperties_WithComputedSources_NoFDDBExceptions()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            GenPropertyValue().ToArbitrary(),
            (string regularValue, string source1Value, string source2Value) =>
            {
                // Arrange: update both a regular property and all computed sources
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedRegular = regularValue;
                var capturedSource1 = source1Value;
                var capturedSource2 = source2Value;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel
                    {
                        RegularProp = capturedRegular,
                        Source1 = capturedSource1,
                        Source2 = capturedSource2
                    };

                // Act
                try
                {
                    var result = translator.TranslateUpdateExpression(expression, context);

                    // Assert: both regular SET and computed recomputation should be present
                    var hasSetOperation = result.Contains("SET");
                    var hasRegularAttribute = context.AttributeNames.AttributeNames.Values
                        .Contains("regular_prop");

                    return hasSetOperation && hasRegularAttribute;
                }
                catch (InvalidOperationException)
                {
                    return false; // Should not throw
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 11: Backwards Compatibility for Non-Computed Properties**
    /// 
    /// Arithmetic operations on non-computed numeric properties work as before.
    /// 
    /// **Validates: Requirements 9.2, 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonComputedProperties_ArithmeticOperations_WorkUnchanged()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 1000)),
            (int increment) =>
            {
                // Arrange: entity with computed field metadata, but do arithmetic on RegularInt
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);

                // Build arithmetic expression: RegularInt = x.RegularInt + increment
                var parameter = Expression.Parameter(typeof(ComputedUpdateExpressions), "x");
                var regularIntProperty = Expression.Property(parameter, "RegularInt");
                var addExpression = Expression.Add(regularIntProperty, Expression.Constant(increment));
                var binding = Expression.Bind(
                    typeof(ComputedUpdateModel).GetProperty("RegularInt")!,
                    Expression.Convert(addExpression, typeof(int?)));
                var memberInit = Expression.MemberInit(
                    Expression.New(typeof(ComputedUpdateModel)),
                    binding);
                var lambda = Expression.Lambda<Func<ComputedUpdateExpressions, ComputedUpdateModel>>(
                    memberInit, parameter);

                // Act
                try
                {
                    var result = translator.TranslateUpdateExpression(lambda, context);

                    // Assert: should produce SET #attr = #attr + :p0
                    var hasSetOperation = result.Contains("SET");
                    var hasArithmetic = result.Contains("+");

                    return hasSetOperation && hasArithmetic;
                }
                catch (InvalidOperationException)
                {
                    return false; // Should not throw FDDB exceptions for non-computed
                }
            });
    }

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 11: Backwards Compatibility for Non-Computed Properties**
    /// 
    /// Direct assignment to a non-key computed field produces standard SET expression.
    /// This is backwards-compatible with existing behavior.
    /// 
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirectComputedFieldAssignment_ProducesStandardSET()
    {
        return Prop.ForAll(
            GenPropertyValue().ToArbitrary(),
            (string directValue) =>
            {
                // Arrange: directly assign the computed field (not via sources)
                var metadata = CreateComputedFieldMetadata(new[] { "Source1", "Source2" });
                var translator = CreateTranslator();
                var context = CreateContext(metadata);
                var capturedValue = directValue;

                Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
                    x => new ComputedUpdateModel { ComputedField = capturedValue };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert: standard SET for computed_field attribute
                var hasSetOperation = result.Contains("SET");
                var hasCorrectAttribute = context.AttributeNames.AttributeNames.Values
                    .Contains("computed_field");
                var hasCorrectValue = context.AttributeValues.AttributeValues.Values
                    .Any(v => v.S == directValue);

                return hasSetOperation && hasCorrectAttribute && hasCorrectValue;
            });
    }

    #endregion
}
