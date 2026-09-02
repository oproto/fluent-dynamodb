using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Bug condition exploration test for Extracted + DynamoDbAttribute conflict.
///
/// The source generator's ValidateExtractedProperty() method never checks HasAttributeMapping.
/// When a property has both [Extracted] and [DynamoDbAttribute], these attributes are semantically
/// conflicting: [Extracted] means the value is derived from a composite key at read time, while
/// [DynamoDbAttribute] maps the property to its own independent DynamoDB attribute.
///
/// This test asserts that FDDB124 is emitted when both attributes are present.
/// On UNFIXED code, this test is EXPECTED TO FAIL, confirming the bug exists.
///
/// **Validates: Requirements 1.1, 2.1, 2.2, 2.3**
/// </summary>
public class ExtractedDynamoDbAttributeConflictBugConditionTests
{
    private readonly object _analyzer;
    private readonly MethodInfo _validateExtractedProperty;
    private readonly FieldInfo _diagnosticsField;

    public ExtractedDynamoDbAttributeConflictBugConditionTests()
    {
        _analyzer = FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));

        // Initialize the _diagnostics field since GetUninitializedObject skips field initializers
        _diagnosticsField = typeof(EntityAnalyzer).GetField(
            "_diagnostics", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _diagnosticsField.SetValue(_analyzer, new List<Diagnostic>());

        _validateExtractedProperty = typeof(EntityAnalyzer).GetMethod(
            "ValidateExtractedProperty",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private List<Diagnostic> GetDiagnostics()
    {
        return (List<Diagnostic>)_diagnosticsField.GetValue(_analyzer)!;
    }

    private void ClearDiagnostics()
    {
        GetDiagnostics().Clear();
    }

    private void InvokeValidateExtractedProperty(PropertyModel extractedProperty, HashSet<string> propertyNames, EntityModel entityModel)
    {
        _validateExtractedProperty.Invoke(_analyzer, new object[] { extractedProperty, propertyNames, entityModel });
    }

    /// <summary>
    /// Test case 1: Basic conflict - [Extracted("Pk", 0)] [DynamoDbAttribute("year")] public int Year
    /// 
    /// Expected: FDDB124 emitted with Error severity, message contains "Year"
    /// On unfixed code: Test FAILS because no FDDB124 diagnostic exists yet.
    /// </summary>
    [Fact]
    public void FDDB124_Emitted_WhenExtractedPropertyHasAttributeMapping_BasicConflict()
    {
        // Arrange: Property with both [Extracted("Pk", 0)] and [DynamoDbAttribute("year")]
        var extractedProperty = new PropertyModel
        {
            PropertyName = "Year",
            AttributeName = "year", // HasAttributeMapping == true
            PropertyType = "int",
            ExtractedKey = new ExtractedKeyModel
            {
                SourceProperty = "Pk",
                Index = 0,
                Separator = "#"
            }
        };

        var computedSourceProperty = new PropertyModel
        {
            PropertyName = "Pk",
            AttributeName = "pk",
            PropertyType = "string",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = new[] { "Year", "Month" },
                Separator = "#"
            }
        };

        var entityModel = new EntityModel
        {
            ClassName = "Event",
            TableName = "events",
            Properties = new[] { computedSourceProperty, extractedProperty }
        };

        var propertyNames = new HashSet<string> { "Pk", "Year", "Month" };

        // Act
        InvokeValidateExtractedProperty(extractedProperty, propertyNames, entityModel);

        // Assert — FDDB124 should be emitted
        var diagnostics = GetDiagnostics();
        diagnostics.Should().ContainSingle(d => d.Id == "FDDB124",
            "FDDB124 should be emitted when a property has both [Extracted] and [DynamoDbAttribute]");

        var diagnostic = diagnostics.First(d => d.Id == "FDDB124");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error,
            "FDDB124 must be Error severity to prevent conflicting code generation");
        diagnostic.GetMessage().Should().Contain("Year",
            "FDDB124 message should contain the property name");
    }

    /// <summary>
    /// Test case 2: Multiple conflicting properties on same entity.
    /// Both Year and Month have [Extracted] + [DynamoDbAttribute].
    /// 
    /// Expected: FDDB124 emitted for each conflicting property.
    /// On unfixed code: Test FAILS because no FDDB124 diagnostic exists yet.
    /// </summary>
    [Fact]
    public void FDDB124_Emitted_ForEachConflictingProperty_MultipleConflicts()
    {
        // Arrange: Entity with two properties that both have [Extracted] + [DynamoDbAttribute]
        var yearProperty = new PropertyModel
        {
            PropertyName = "Year",
            AttributeName = "year", // HasAttributeMapping == true
            PropertyType = "int",
            ExtractedKey = new ExtractedKeyModel
            {
                SourceProperty = "Pk",
                Index = 0,
                Separator = "#"
            }
        };

        var monthProperty = new PropertyModel
        {
            PropertyName = "Month",
            AttributeName = "month", // HasAttributeMapping == true
            PropertyType = "int",
            ExtractedKey = new ExtractedKeyModel
            {
                SourceProperty = "Pk",
                Index = 1,
                Separator = "#"
            }
        };

        var computedSourceProperty = new PropertyModel
        {
            PropertyName = "Pk",
            AttributeName = "pk",
            PropertyType = "string",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = new[] { "Year", "Month" },
                Separator = "#"
            }
        };

        var entityModel = new EntityModel
        {
            ClassName = "Event",
            TableName = "events",
            Properties = new[] { computedSourceProperty, yearProperty, monthProperty }
        };

        var propertyNames = new HashSet<string> { "Pk", "Year", "Month" };

        // Act — validate first property
        InvokeValidateExtractedProperty(yearProperty, propertyNames, entityModel);

        // Assert — FDDB124 emitted for Year
        var diagnostics = GetDiagnostics();
        diagnostics.Should().Contain(d => d.Id == "FDDB124",
            "FDDB124 should be emitted for Year property");
        diagnostics.First(d => d.Id == "FDDB124").GetMessage().Should().Contain("Year");

        // Act — validate second property
        ClearDiagnostics();
        InvokeValidateExtractedProperty(monthProperty, propertyNames, entityModel);

        // Assert — FDDB124 emitted for Month
        diagnostics = GetDiagnostics();
        diagnostics.Should().Contain(d => d.Id == "FDDB124",
            "FDDB124 should be emitted for Month property");
        diagnostics.First(d => d.Id == "FDDB124").GetMessage().Should().Contain("Month");
    }

    /// <summary>
    /// Test case 3: Conflict where source property is valid and computed.
    /// The extracted property references a valid computed source, but also has [DynamoDbAttribute].
    /// FDDB124 should take precedence (fire before source/index checks).
    /// 
    /// Expected: FDDB124 emitted, no cascading source/index diagnostics.
    /// On unfixed code: Test FAILS because no FDDB124 diagnostic exists yet.
    /// </summary>
    [Fact]
    public void FDDB124_Emitted_BeforeOtherChecks_WhenSourcePropertyIsValidComputed()
    {
        // Arrange: Extracted property with valid computed source but also has [DynamoDbAttribute]
        var extractedProperty = new PropertyModel
        {
            PropertyName = "Year",
            AttributeName = "year", // HasAttributeMapping == true — conflict!
            PropertyType = "int",
            ExtractedKey = new ExtractedKeyModel
            {
                SourceProperty = "Pk",
                Index = 0,
                Separator = "#"
            }
        };

        var computedSourceProperty = new PropertyModel
        {
            PropertyName = "Pk",
            AttributeName = "pk",
            PropertyType = "string",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = new[] { "Year", "Month", "Day" },
                Separator = "#"
            }
        };

        var entityModel = new EntityModel
        {
            ClassName = "Event",
            TableName = "events",
            Properties = new[] { computedSourceProperty, extractedProperty }
        };

        var propertyNames = new HashSet<string> { "Pk", "Year", "Month", "Day" };

        // Act
        InvokeValidateExtractedProperty(extractedProperty, propertyNames, entityModel);

        // Assert — FDDB124 should be the only diagnostic (early return, no cascading)
        var diagnostics = GetDiagnostics();
        diagnostics.Should().ContainSingle(d => d.Id == "FDDB124",
            "Only FDDB124 should be emitted — early return prevents cascading diagnostics");

        var diagnostic = diagnostics.First(d => d.Id == "FDDB124");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Year");

        // Verify no other diagnostics leaked through (no FDDB122 or index errors)
        diagnostics.Should().NotContain(d => d.Id != "FDDB124",
            "FDDB124 should cause early return, preventing any other diagnostic emission");
    }
}
