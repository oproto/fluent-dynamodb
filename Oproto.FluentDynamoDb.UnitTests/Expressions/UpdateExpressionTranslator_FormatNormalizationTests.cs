using System.Linq.Expressions;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Unit tests for UpdateExpressionTranslator format normalization.
/// Validates that ValidateAndProcessComputedFields correctly uses string.Format(cf.Format, parts)
/// where null values are substituted with string.Empty.
/// </summary>
public class UpdateExpressionTranslator_FormatNormalizationTests
{
    #region Test Entity Classes

    private class FormatTestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? Source1 { get; set; }
        public string? Source2 { get; set; }
        public string? Source3 { get; set; }
        public string? ComputedField { get; set; }
    }

    private class FormatTestUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> Source1 { get; } = new();
        public UpdateExpressionProperty<string?> Source2 { get; } = new();
        public UpdateExpressionProperty<string?> Source3 { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
    }

    private class FormatTestUpdateModel
    {
        public string? Id { get; set; }
        public string? Source1 { get; set; }
        public string? Source2 { get; set; }
        public string? Source3 { get; set; }
        public string? ComputedField { get; set; }
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
    /// Creates metadata for a computed field with a given format string and source properties.
    /// </summary>
    private EntityMetadata CreateMetadata(string format, params string[] sourceProperties)
    {
        var properties = new List<PropertyMetadata>();

        // Partition key
        properties.Add(new PropertyMetadata
        {
            PropertyName = "Id",
            AttributeName = "pk",
            PropertyType = typeof(string),
            IsPartitionKey = true
        });

        // Computed field
        properties.Add(new PropertyMetadata
        {
            PropertyName = "ComputedField",
            AttributeName = "computed_field",
            PropertyType = typeof(string),
            ComputedField = new ComputedFieldMetadata
            {
                SourceProperties = sourceProperties,
                Format = format
            }
        });

        // Source properties
        foreach (var sourceName in sourceProperties)
        {
            properties.Add(new PropertyMetadata
            {
                PropertyName = sourceName,
                AttributeName = sourceName.ToLowerInvariant(),
                PropertyType = typeof(string),
                ComputedFieldTargets = new[] { "ComputedField" }
            });
        }

        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = properties.ToArray()
        };
    }

    #endregion

    #region Separator-Based Config Produces Correct Recomputed Value (Req 3.1, 5.1)

    [Fact]
    public void SeparatorBasedConfig_TwoSources_ProducesCorrectRecomputedValue()
    {
        // Arrange: Separator="#", 2 sources ["foo", "bar"] → Format="{0}#{1}" → result="foo#bar"
        var metadata = CreateMetadata("{0}#{1}", "Source1", "Source2");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "foo", Source2 = "bar" };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "foo#bar");
    }

    [Fact]
    public void SeparatorBasedConfig_ThreeSources_ProducesCorrectRecomputedValue()
    {
        // Arrange: Separator="#", 3 sources → Format="{0}#{1}#{2}" → result="a#b#c"
        var metadata = CreateMetadata("{0}#{1}#{2}", "Source1", "Source2", "Source3");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "a", Source2 = "b", Source3 = "c" };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "a#b#c");
    }

    #endregion

    #region Explicit Format Produces Correct Recomputed Value (Req 3.1, 5.2, 5.3)

    [Fact]
    public void ExplicitFormat_TenantUser_ProducesCorrectRecomputedValue()
    {
        // Arrange: Explicit Format="TENANT#{0}#USER#{1}#", values ["t1", "u1"] → "TENANT#t1#USER#u1#"
        var metadata = CreateMetadata("TENANT#{0}#USER#{1}#", "Source1", "Source2");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "t1", Source2 = "u1" };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "TENANT#t1#USER#u1#");
    }

    [Fact]
    public void ExplicitFormat_WithConcreteValues_ProducesExpectedOutput()
    {
        // Arrange: Format="TENANT#{0}#USER#{1}#", values ["tenantValue", "userValue"] → "TENANT#tenantValue#USER#userValue#"
        var metadata = CreateMetadata("TENANT#{0}#USER#{1}#", "Source1", "Source2");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "tenantValue", Source2 = "userValue" };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "TENANT#tenantValue#USER#userValue#");
    }

    #endregion

    #region Null Source Value Substitutes String.Empty (Req 3.4, 5.4)

    [Fact]
    public void NullSourceValue_SubstitutesStringEmpty()
    {
        // Arrange: Format="{0}#{1}", values ["foo", null] → "foo#"
        var metadata = CreateMetadata("{0}#{1}", "Source1", "Source2");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "foo", Source2 = null };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "foo#");
    }

    [Fact]
    public void AllNullSourceValues_ProducesFormatWithEmptyPlaceholders()
    {
        // Arrange: Format="{0}#{1}", values [null, null] → "#"
        var metadata = CreateMetadata("{0}#{1}", "Source1", "Source2");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = null, Source2 = null };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "#");
    }

    [Fact]
    public void NullSourceValue_WithExplicitFormat_SubstitutesStringEmpty()
    {
        // Arrange: Format="TENANT#{0}#USER#{1}#", values ["t1", null] → "TENANT#t1#USER##"
        var metadata = CreateMetadata("TENANT#{0}#USER#{1}#", "Source1", "Source2");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "t1", Source2 = null };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "TENANT#t1#USER##");
    }

    #endregion

    #region Multi-Source With Prefix Produces Correct Value (Req 3.1, 3.3, 5.1)

    [Fact]
    public void MultiSourceWithPrefix_ProducesCorrectRecomputedValue()
    {
        // Arrange: Format="ORDER#{0}#{1}", values ["a", "b"] → "ORDER#a#b"
        var metadata = CreateMetadata("ORDER#{0}#{1}", "Source1", "Source2");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "a", Source2 = "b" };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "ORDER#a#b");
    }

    [Fact]
    public void MultiSourceWithPrefix_ThreeSources_ProducesCorrectRecomputedValue()
    {
        // Arrange: Format="ORDER#{0}#{1}#{2}", values ["us-east", "engineering", "software"] → "ORDER#us-east#engineering#software"
        var metadata = CreateMetadata("ORDER#{0}#{1}#{2}", "Source1", "Source2", "Source3");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "us-east", Source2 = "engineering", Source3 = "software" };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "ORDER#us-east#engineering#software");
    }

    [Fact]
    public void MultiSourceWithPrefix_NullValue_SubstitutesEmpty()
    {
        // Arrange: Format="ORDER#{0}#{1}", values ["a", null] → "ORDER#a#"
        var metadata = CreateMetadata("ORDER#{0}#{1}", "Source1", "Source2");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<FormatTestUpdateExpressions, FormatTestUpdateModel>> expression =
            x => new FormatTestUpdateModel { Source1 = "a", Source2 = null };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "ORDER#a#");
    }

    #endregion
}
