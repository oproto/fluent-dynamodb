using System.Linq.Expressions;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using EntityMetadata = Oproto.FluentDynamoDb.Metadata.EntityMetadata;
using PropertyMetadata = Oproto.FluentDynamoDb.Metadata.PropertyMetadata;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Integration test verifying that the Update path produces the same computed field value as the Put path.
/// Tests the full pipeline: ComputeFormatString (source generator) → ComputedFieldMetadata → UpdateExpressionTranslator.
/// 
/// Validates: Requirements 5.1, 5.2
/// </summary>
public class UpdateProducesSameAsPutIntegrationTests
{
    #region Test Entity Classes

    private class IntegrationTestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? Region { get; set; }
        public string? Department { get; set; }
        public string? Team { get; set; }
        public string? ComputedSortKey { get; set; }
    }

    private class IntegrationTestUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> Region { get; } = new();
        public UpdateExpressionProperty<string?> Department { get; } = new();
        public UpdateExpressionProperty<string?> Team { get; } = new();
        public UpdateExpressionProperty<string?> ComputedSortKey { get; } = new();
    }

    private class IntegrationTestUpdateModel
    {
        public string? Id { get; set; }
        public string? Region { get; set; }
        public string? Department { get; set; }
        public string? Team { get; set; }
        public string? ComputedSortKey { get; set; }
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
    /// Simulates the Put path for a separator-only computed field.
    /// The generated Put code produces: value1 + separator + value2 + separator + value3
    /// (direct concatenation, as seen in MapperGenerator.GenerateComputedKeyLogic)
    /// </summary>
    private static string SimulatePutPath(string separator, params string?[] sourceValues)
    {
        // The Put path uses direct string concatenation: source1 + sep + source2 + sep + source3
        // Values are read from entity properties directly (ToString() on non-null values)
        var stringValues = sourceValues.Select(v => v ?? string.Empty).ToArray();
        return string.Join(separator, stringValues);
    }

    /// <summary>
    /// Simulates the Put path for a computed field with an explicit format string.
    /// The generated Put code produces: string.Format(format, source1, source2, ...)
    /// </summary>
    private static string SimulatePutPathWithFormat(string format, params string?[] sourceValues)
    {
        var args = sourceValues.Select(v => (object)(v ?? string.Empty)).ToArray();
        return string.Format(format, args);
    }

    /// <summary>
    /// Creates entity metadata using the source generator's ComputeFormatString to derive the format string,
    /// exactly as the real pipeline would.
    /// </summary>
    private EntityMetadata CreateMetadataFromSourceGenerator(
        string separator,
        string[] sourcePropertyNames,
        string? prefix = null,
        string? keySeparator = null,
        string? explicitFormat = null)
    {
        // Build the ComputedKeyModel (what the source generator creates from attributes)
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = sourcePropertyNames,
            Separator = separator,
            Format = explicitFormat
        };

        // Build the KeyFormatModel if prefix is present
        KeyFormatModel? keyFormat = null;
        if (!string.IsNullOrEmpty(prefix))
        {
            keyFormat = new KeyFormatModel
            {
                Prefix = prefix,
                Separator = keySeparator ?? "#"
            };
        }

        // Use the source generator's ComputeFormatString — same code that runs at compile time
        var formatString = MapperGenerator.ComputeFormatString(computedKey, keyFormat);

        // Build the metadata (what gets emitted into the generated code)
        var properties = new List<PropertyMetadata>();

        properties.Add(new PropertyMetadata
        {
            PropertyName = "Id",
            AttributeName = "pk",
            PropertyType = typeof(string),
            IsPartitionKey = true
        });

        properties.Add(new PropertyMetadata
        {
            PropertyName = "ComputedSortKey",
            AttributeName = "sk",
            PropertyType = typeof(string),
            ComputedField = new ComputedFieldMetadata
            {
                SourceProperties = sourcePropertyNames,
                Format = formatString
            }
        });

        foreach (var sourceName in sourcePropertyNames)
        {
            properties.Add(new PropertyMetadata
            {
                PropertyName = sourceName,
                AttributeName = sourceName.ToLowerInvariant(),
                PropertyType = typeof(string),
                ComputedFieldTargets = new[] { "ComputedSortKey" }
            });
        }

        return new EntityMetadata
        {
            TableName = "IntegrationTestTable",
            Properties = properties.ToArray()
        };
    }

    /// <summary>
    /// Executes the Update path via UpdateExpressionTranslator and returns the recomputed value.
    /// </summary>
    private string ExecuteUpdatePath(EntityMetadata metadata, params (string propertyName, string? value)[] assignments)
    {
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        // Build the expression dynamically based on assignments
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            BuildUpdateExpression(assignments);

        translator.TranslateUpdateExpression(expression, context);

        // Find the recomputed value in the attribute values
        var computedAttrName = "#sk";
        var computedValue = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s != null && context.AttributeValues.AttributeValues.Values
                .Any(av => av.S == s));

        // Get the value assigned to the computed field (sk)
        // We look for the value that's not one of the source values
        var sourceValues = assignments.Select(a => a.value).ToHashSet();
        var recomputedValue = context.AttributeValues.AttributeValues.Values
            .Where(av => av.S != null && !sourceValues.Contains(av.S))
            .Select(av => av.S)
            .FirstOrDefault();

        return recomputedValue ?? string.Empty;
    }

    private Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>>
        BuildUpdateExpression(params (string propertyName, string? value)[] assignments)
    {
        // For simplicity, map directly to known assignments
        // The test cases use Region, Department, Team as source properties
        string? region = null, department = null, team = null;
        bool hasRegion = false, hasDepartment = false, hasTeam = false;

        foreach (var (prop, val) in assignments)
        {
            switch (prop)
            {
                case "Region": region = val; hasRegion = true; break;
                case "Department": department = val; hasDepartment = true; break;
                case "Team": team = val; hasTeam = true; break;
            }
        }

        if (hasRegion && hasDepartment && hasTeam)
            return x => new IntegrationTestUpdateModel { Region = region, Department = department, Team = team };
        if (hasRegion && hasDepartment)
            return x => new IntegrationTestUpdateModel { Region = region, Department = department };
        if (hasRegion)
            return x => new IntegrationTestUpdateModel { Region = region };

        throw new InvalidOperationException("Unsupported assignment combination");
    }

    #endregion

    #region Separator-Based: Update Produces Same As Put (Req 5.1)

    [Fact]
    public void SeparatorBased_TwoSources_UpdateMatchesPut()
    {
        // Arrange: Separator="#", sources=[Region, Department]
        var sourceProps = new[] { "Region", "Department" };
        var metadata = CreateMetadataFromSourceGenerator("#", sourceProps);

        var region = "us-east-1";
        var department = "engineering";

        // Act: Simulate Put path
        var putResult = SimulatePutPath("#", region, department);

        // Act: Execute Update path through translator
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = region, Department = department };
        translator.TranslateUpdateExpression(expression, context);

        // Find the computed value in the attribute values
        var updateResult = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s == putResult);

        // Assert: both paths produce identical results
        updateResult.Should().Be(putResult);
        putResult.Should().Be("us-east-1#engineering");
    }

    [Fact]
    public void SeparatorBased_ThreeSources_UpdateMatchesPut()
    {
        // Arrange: Separator="#", sources=[Region, Department, Team]
        var sourceProps = new[] { "Region", "Department", "Team" };
        var metadata = CreateMetadataFromSourceGenerator("#", sourceProps);

        var region = "us-west-2";
        var department = "product";
        var team = "platform";

        // Act: Simulate Put path
        var putResult = SimulatePutPath("#", region, department, team);

        // Act: Execute Update path through translator
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = region, Department = department, Team = team };
        translator.TranslateUpdateExpression(expression, context);

        // Find the computed value
        var updateResult = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s == putResult);

        // Assert
        updateResult.Should().Be(putResult);
        putResult.Should().Be("us-west-2#product#platform");
    }

    [Fact]
    public void SeparatorBased_UnderscoreSeparator_UpdateMatchesPut()
    {
        // Arrange: Separator="_", sources=[Region, Department]
        var sourceProps = new[] { "Region", "Department" };
        var metadata = CreateMetadataFromSourceGenerator("_", sourceProps);

        var region = "eu-central";
        var department = "sales";

        // Act: Put path
        var putResult = SimulatePutPath("_", region, department);

        // Act: Update path
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = region, Department = department };
        translator.TranslateUpdateExpression(expression, context);

        var updateResult = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s == putResult);

        // Assert
        updateResult.Should().Be(putResult);
        putResult.Should().Be("eu-central_sales");
    }

    [Fact]
    public void SeparatorBased_SingleSource_UpdateMatchesPut()
    {
        // Arrange: Separator="#", single source - Format becomes "{0}"
        var sourceProps = new[] { "Region" };
        var metadata = CreateMetadataFromSourceGenerator("#", sourceProps);

        var region = "ap-southeast-1";

        // Act: Put path (single source = just the value)
        var putResult = SimulatePutPath("#", region);

        // Act: Update path
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = region };
        translator.TranslateUpdateExpression(expression, context);

        var updateResult = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s == putResult);

        // Assert
        updateResult.Should().Be(putResult);
        putResult.Should().Be("ap-southeast-1");
    }

    #endregion

    #region Explicit Format: Update Produces Same As Put (Req 5.2)

    [Fact]
    public void ExplicitFormat_TenantUser_UpdateMatchesPut()
    {
        // Arrange: Explicit format = "TENANT#{0}#USER#{1}#"
        var sourceProps = new[] { "Region", "Department" };
        var metadata = CreateMetadataFromSourceGenerator("#", sourceProps, explicitFormat: "TENANT#{0}#USER#{1}#");

        var tenantValue = "tenantValue";
        var userValue = "userValue";

        // Act: Put path (uses string.Format directly)
        var putResult = SimulatePutPathWithFormat("TENANT#{0}#USER#{1}#", tenantValue, userValue);

        // Act: Update path
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = tenantValue, Department = userValue };
        translator.TranslateUpdateExpression(expression, context);

        var updateResult = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s == putResult);

        // Assert
        updateResult.Should().Be(putResult);
        putResult.Should().Be("TENANT#tenantValue#USER#userValue#");
    }

    [Fact]
    public void ExplicitFormat_CustomPattern_UpdateMatchesPut()
    {
        // Arrange: Explicit format = "ORG:{0}|DEPT:{1}"
        var sourceProps = new[] { "Region", "Department" };
        var metadata = CreateMetadataFromSourceGenerator("#", sourceProps, explicitFormat: "ORG:{0}|DEPT:{1}");

        var org = "acme-corp";
        var dept = "research";

        // Act: Put path
        var putResult = SimulatePutPathWithFormat("ORG:{0}|DEPT:{1}", org, dept);

        // Act: Update path
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = org, Department = dept };
        translator.TranslateUpdateExpression(expression, context);

        var updateResult = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s == putResult);

        // Assert
        updateResult.Should().Be(putResult);
        putResult.Should().Be("ORG:acme-corp|DEPT:research");
    }

    #endregion

    #region With Prefix: Update Produces Same As Put (Req 5.1)

    [Fact]
    public void WithPrefix_SeparatorBased_UpdateMatchesPut()
    {
        // Arrange: Separator="#", Prefix="ORDER", KeySeparator="#"
        // Format generated by source generator: "ORDER#{0}#{1}"
        var sourceProps = new[] { "Region", "Department" };
        var metadata = CreateMetadataFromSourceGenerator("#", sourceProps, prefix: "ORDER", keySeparator: "#");

        var region = "us-east-1";
        var department = "fulfillment";

        // Act: Put path - with prefix, the generated code for separator-only would be:
        // prefix + keySep + source1 + sep + source2 = "ORDER#us-east-1#fulfillment"
        // But since the format string is "ORDER#{0}#{1}", string.Format produces the same
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = sourceProps,
            Separator = "#",
            Format = null
        };
        var keyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" };
        var formatString = MapperGenerator.ComputeFormatString(computedKey, keyFormat);
        var putResult = string.Format(formatString, region, department);

        // Act: Update path
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = region, Department = department };
        translator.TranslateUpdateExpression(expression, context);

        var updateResult = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s == putResult);

        // Assert
        updateResult.Should().Be(putResult);
        putResult.Should().Be("ORDER#us-east-1#fulfillment");
    }

    #endregion

    #region Null Handling: Both Paths Consistent (Req 5.1, 5.2)

    [Fact]
    public void NullSourceValue_BothPathsProduceSameResult()
    {
        // Arrange: Separator="#", one source is null
        var sourceProps = new[] { "Region", "Department" };
        var metadata = CreateMetadataFromSourceGenerator("#", sourceProps);

        var region = "us-east-1";
        string? department = null;

        // Act: Put path - null becomes empty string in the Join
        var putResult = SimulatePutPath("#", region, department);

        // Act: Update path
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = region, Department = department };
        translator.TranslateUpdateExpression(expression, context);

        var updateResult = context.AttributeValues.AttributeValues.Values
            .Select(av => av.S)
            .FirstOrDefault(s => s == putResult);

        // Assert
        updateResult.Should().Be(putResult);
        putResult.Should().Be("us-east-1#");
    }

    #endregion

    #region Full Pipeline: Source Generator Format → Metadata → Update (Req 5.1, 5.2)

    [Fact]
    public void FullPipeline_FormatStringFromSourceGenerator_UsedCorrectlyByUpdate()
    {
        // This test verifies the complete pipeline:
        // 1. ComputeFormatString produces the format string at compile time
        // 2. That format string is stored in ComputedFieldMetadata
        // 3. UpdateExpressionTranslator uses that format string to recompute

        var sourceProps = new[] { "Region", "Department", "Team" };
        var separator = "#";

        // Step 1: Source generator computes format string
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = sourceProps,
            Separator = separator,
            Format = null
        };
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, null);

        // Verify the format string is what we expect
        generatedFormat.Should().Be("{0}#{1}#{2}");

        // Step 2: The format string is embedded in metadata (simulating generated code)
        var metadata = new EntityMetadata
        {
            TableName = "PipelineTestTable",
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
                    PropertyName = "ComputedSortKey",
                    AttributeName = "sk",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = sourceProps,
                        Format = generatedFormat  // This is what the generated code emits
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Region",
                    AttributeName = "region",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedSortKey" }
                },
                new PropertyMetadata
                {
                    PropertyName = "Department",
                    AttributeName = "department",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedSortKey" }
                },
                new PropertyMetadata
                {
                    PropertyName = "Team",
                    AttributeName = "team",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedSortKey" }
                }
            }
        };

        // Step 3: Update path uses the format string
        var translator = CreateTranslator();
        var context = CreateContext(metadata);
        Expression<Func<IntegrationTestUpdateExpressions, IntegrationTestUpdateModel>> expression =
            x => new IntegrationTestUpdateModel { Region = "alpha", Department = "beta", Team = "gamma" };
        translator.TranslateUpdateExpression(expression, context);

        // Verify: the recomputed value matches what string.Format(generatedFormat, values) produces
        var expectedValue = string.Format(generatedFormat, "alpha", "beta", "gamma");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == expectedValue);
        expectedValue.Should().Be("alpha#beta#gamma");

        // Also verify it matches the Put path (separator-based concatenation)
        var putPathResult = SimulatePutPath(separator, "alpha", "beta", "gamma");
        expectedValue.Should().Be(putPathResult);
    }

    #endregion
}
