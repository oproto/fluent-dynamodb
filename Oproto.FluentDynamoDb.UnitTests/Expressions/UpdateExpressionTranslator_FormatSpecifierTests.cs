using System.Globalization;
using System.Linq.Expressions;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Unit tests for UpdateExpressionTranslator format specifier handling in computed field recomputation.
/// Validates that format specifiers like {0:yyyy-MM-dd}, {0:D4}, {0:G} produce correctly formatted values
/// when typed values are passed through string.Format with CultureInfo.InvariantCulture.
/// </summary>
public class UpdateExpressionTranslator_FormatSpecifierTests
{
    #region Test Entity Classes

    private enum TestStatus
    {
        Active,
        Inactive,
        Pending
    }

    /// <summary>
    /// Entity with DateTime and string source properties for date format specifier tests.
    /// </summary>
    private class DateTimeEntity
    {
        public string Id { get; set; } = string.Empty;
        public DateTime? EventDate { get; set; }
        public string? Category { get; set; }
        public string? ComputedField { get; set; }
    }

    private class DateTimeUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<DateTime?> EventDate { get; } = new();
        public UpdateExpressionProperty<string?> Category { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
    }

    private class DateTimeUpdateModel
    {
        public string? Id { get; set; }
        public DateTime? EventDate { get; set; }
        public string? Category { get; set; }
        public string? ComputedField { get; set; }
    }

    /// <summary>
    /// Entity with int and string source properties for numeric format specifier tests.
    /// </summary>
    private class IntEntity
    {
        public string Id { get; set; } = string.Empty;
        public int? Priority { get; set; }
        public string? Name { get; set; }
        public string? ComputedField { get; set; }
    }

    private class IntUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<int?> Priority { get; } = new();
        public UpdateExpressionProperty<string?> Name { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
    }

    private class IntUpdateModel
    {
        public string? Id { get; set; }
        public int? Priority { get; set; }
        public string? Name { get; set; }
        public string? ComputedField { get; set; }
    }

    /// <summary>
    /// Entity with enum and string source properties for enum format specifier tests.
    /// </summary>
    private class EnumEntity
    {
        public string Id { get; set; } = string.Empty;
        public TestStatus? Status { get; set; }
        public string? ItemId { get; set; }
        public string? ComputedField { get; set; }
    }

    private class EnumUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<TestStatus?> Status { get; } = new();
        public UpdateExpressionProperty<string?> ItemId { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
    }

    private class EnumUpdateModel
    {
        public string? Id { get; set; }
        public TestStatus? Status { get; set; }
        public string? ItemId { get; set; }
        public string? ComputedField { get; set; }
    }

    /// <summary>
    /// Entity with string source properties for backwards compatibility tests (no format specifiers).
    /// </summary>
    private class SimpleEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? Source1 { get; set; }
        public string? Source2 { get; set; }
        public string? ComputedField { get; set; }
    }

    private class SimpleUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> Source1 { get; } = new();
        public UpdateExpressionProperty<string?> Source2 { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
    }

    private class SimpleUpdateModel
    {
        public string? Id { get; set; }
        public string? Source1 { get; set; }
        public string? Source2 { get; set; }
        public string? ComputedField { get; set; }
    }

    /// <summary>
    /// Entity with a decimal source property for culture-sensitive format specifier tests.
    /// </summary>
    private class DecimalEntity
    {
        public string Id { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? Label { get; set; }
        public string? ComputedField { get; set; }
    }

    private class DecimalUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<decimal?> Amount { get; } = new();
        public UpdateExpressionProperty<string?> Label { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
    }

    private class DecimalUpdateModel
    {
        public string? Id { get; set; }
        public decimal? Amount { get; set; }
        public string? Label { get; set; }
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

    private EntityMetadata CreateDateTimeMetadata(string format)
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
                    PropertyName = "ComputedField",
                    AttributeName = "computed_field",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "EventDate", "Category" },
                        Format = format
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "EventDate",
                    AttributeName = "event_date",
                    PropertyType = typeof(DateTime),
                    ComputedFieldTargets = new[] { "ComputedField" }
                },
                new PropertyMetadata
                {
                    PropertyName = "Category",
                    AttributeName = "category",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedField" }
                }
            }
        };
    }

    private EntityMetadata CreateIntMetadata(string format)
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
                    PropertyName = "ComputedField",
                    AttributeName = "computed_field",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Priority", "Name" },
                        Format = format
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Priority",
                    AttributeName = "priority",
                    PropertyType = typeof(int),
                    ComputedFieldTargets = new[] { "ComputedField" }
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedField" }
                }
            }
        };
    }

    private EntityMetadata CreateEnumMetadata(string format)
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
                    PropertyName = "ComputedField",
                    AttributeName = "computed_field",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Status", "ItemId" },
                        Format = format
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Status",
                    AttributeName = "status",
                    PropertyType = typeof(TestStatus),
                    ComputedFieldTargets = new[] { "ComputedField" }
                },
                new PropertyMetadata
                {
                    PropertyName = "ItemId",
                    AttributeName = "item_id",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedField" }
                }
            }
        };
    }

    private EntityMetadata CreateSimpleMetadata(string format)
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
                    PropertyName = "ComputedField",
                    AttributeName = "computed_field",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Source1", "Source2" },
                        Format = format
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Source1",
                    AttributeName = "source1",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedField" }
                },
                new PropertyMetadata
                {
                    PropertyName = "Source2",
                    AttributeName = "source2",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedField" }
                }
            }
        };
    }

    private EntityMetadata CreateDecimalMetadata(string format)
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
                    PropertyName = "ComputedField",
                    AttributeName = "computed_field",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Amount", "Label" },
                        Format = format
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Amount",
                    AttributeName = "amount",
                    PropertyType = typeof(decimal),
                    ComputedFieldTargets = new[] { "ComputedField" }
                },
                new PropertyMetadata
                {
                    PropertyName = "Label",
                    AttributeName = "label",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedField" }
                }
            }
        };
    }

    #endregion

    #region DateTime Format Specifier Tests (Req 4.1, 4.4, 5.1)

    /// <summary>
    /// Validates: Requirements 4.1, 4.4, 5.1
    /// Format {0:yyyy-MM-dd}#{1} with DateTime 2024-03-15 + "CategoryA" produces "2024-03-15#CategoryA"
    /// </summary>
    [Fact]
    public void Recomputation_DateTimeFormatSpecifier_ProducesCorrectFormattedValue()
    {
        // Arrange
        var metadata = CreateDateTimeMetadata("{0:yyyy-MM-dd}#{1}");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<DateTimeUpdateExpressions, DateTimeUpdateModel>> expression =
            x => new DateTimeUpdateModel
            {
                EventDate = new DateTime(2024, 3, 15),
                Category = "CategoryA"
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "2024-03-15#CategoryA");
    }

    #endregion

    #region Int Format Specifier Tests (Req 4.1, 5.2)

    /// <summary>
    /// Validates: Requirements 4.1, 5.2
    /// Format {0:D4}#{1} with int 42 + "Name" produces "0042#Name"
    /// </summary>
    [Fact]
    public void Recomputation_IntFormatSpecifier_ProducesCorrectZeroPaddedValue()
    {
        // Arrange
        var metadata = CreateIntMetadata("{0:D4}#{1}");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<IntUpdateExpressions, IntUpdateModel>> expression =
            x => new IntUpdateModel
            {
                Priority = 42,
                Name = "Name"
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "0042#Name");
    }

    #endregion

    #region Enum Format Specifier Tests (Req 4.1, 5.3)

    /// <summary>
    /// Validates: Requirements 4.1, 5.3
    /// Format {0:G}#{1} with enum Active + "id123" produces "Active#id123"
    /// </summary>
    [Fact]
    public void Recomputation_EnumFormatSpecifier_ProducesCorrectNamedValue()
    {
        // Arrange
        var metadata = CreateEnumMetadata("{0:G}#{1}");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<EnumUpdateExpressions, EnumUpdateModel>> expression =
            x => new EnumUpdateModel
            {
                Status = TestStatus.Active,
                ItemId = "id123"
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "Active#id123");
    }

    #endregion

    #region Backwards Compatibility Tests (Req 4.2)

    /// <summary>
    /// Validates: Requirements 4.2
    /// Format {0}#{1} (no specifiers) still calls .ToString() on values (backwards compat)
    /// </summary>
    [Fact]
    public void Recomputation_NoFormatSpecifiers_UsesToStringForBackwardsCompatibility()
    {
        // Arrange: Use simple format without specifiers
        var metadata = CreateSimpleMetadata("{0}#{1}");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<SimpleUpdateExpressions, SimpleUpdateModel>> expression =
            x => new SimpleUpdateModel
            {
                Source1 = "Hello",
                Source2 = "World"
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "Hello#World");
    }

    #endregion

    #region Null Source Value Tests (Req 4.3, 5.5)

    /// <summary>
    /// Validates: Requirements 4.3, 5.5
    /// Null source value with format specifiers produces empty string substitution
    /// </summary>
    [Fact]
    public void Recomputation_NullSourceValueWithFormatSpecifiers_ProducesEmptyStringSubstitution()
    {
        // Arrange: Format with specifier, but one source value is null
        var metadata = CreateIntMetadata("{0:D4}#{1}");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<IntUpdateExpressions, IntUpdateModel>> expression =
            x => new IntUpdateModel
            {
                Priority = null,
                Name = "Test"
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Contain("SET");
        // Null value should be substituted with empty string
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "#Test");
    }

    #endregion

    #region InvariantCulture Tests (Req 5.4)

    /// <summary>
    /// Validates: Requirements 5.4
    /// Recomputation uses InvariantCulture (verify with culture-sensitive format like {0:N2})
    /// The N2 format produces "1,234.57" in InvariantCulture vs locale-dependent separators.
    /// </summary>
    [Fact]
    public void Recomputation_FormatSpecifier_UsesInvariantCulture()
    {
        // Arrange: Use N2 format which is culture-sensitive (thousands separator + 2 decimals)
        var metadata = CreateDecimalMetadata("{0:N2}#{1}");
        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<DecimalUpdateExpressions, DecimalUpdateModel>> expression =
            x => new DecimalUpdateModel
            {
                Amount = 1234.567m,
                Label = "USD"
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: InvariantCulture uses "," for thousands and "." for decimal
        result.Should().Contain("SET");
        var expectedValue = string.Format(CultureInfo.InvariantCulture, "{0:N2}#{1}", 1234.567m, "USD");
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == expectedValue);
        // The expected value is "1,234.57#USD" in InvariantCulture
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "1,234.57#USD");
    }

    #endregion
}
