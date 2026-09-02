using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Preservation property tests for the put-prefix-non-string-key-type bugfix.
///
/// These tests capture the baseline behavior of GenerateKeyPrefixApplication for cases
/// where isBugCondition returns false (non-buggy inputs). They verify:
///   - String key properties with a prefix pass typedEntity.{PropertyName} directly (no .ToString())
///   - Key properties without a prefix do NOT get ApplyKeyPrefix calls generated
///   - Computed key properties are excluded from prefix application
///   - Constant key properties are excluded from prefix application
///
/// These tests MUST PASS on the UNFIXED code, confirming baseline behavior to preserve.
///
/// **Validates: Requirements 3.1, 3.2, 3.3**
/// </summary>
public class PutPrefixNonStringKeyTypePreservationTests
{
    #region Generators

    /// <summary>
    /// Generates property names safe for C# identifiers.
    /// Names are chosen to NOT be DynamoDB reserved words or C# keywords,
    /// avoiding the '@' escaping that EscapePropertyName applies.
    /// </summary>
    private static Gen<string> GenPropertyName()
    {
        return Gen.Elements(
            "UserId", "CustomerId", "TenantId", "OrderId", "AccountId",
            "SessionId", "ProductId", "InvoiceId", "EventId", "EntityId",
            "MyField", "SortVal", "PartKey", "RowId", "RecordId");
    }

    /// <summary>
    /// Generates DynamoDB attribute names.
    /// </summary>
    private static Gen<string> GenAttributeName()
    {
        return Gen.Elements("pk", "sk", "gsi1pk", "gsi1sk", "id", "key", "sort");
    }

    /// <summary>
    /// Generates non-empty prefix strings.
    /// </summary>
    private static Gen<string> GenPrefix()
    {
        return Gen.Elements(
            "USER", "ORDER", "CUSTOMER", "TENANT", "INVOICE",
            "PRODUCT", "EVENT", "SESSION", "ACCT", "META");
    }

    /// <summary>
    /// Generates separator strings.
    /// </summary>
    private static Gen<string> GenSeparator()
    {
        return Gen.Elements("#", "_", "-", ":", "|");
    }

    /// <summary>
    /// Generates class names.
    /// </summary>
    private static Gen<string> GenClassName()
    {
        return Gen.Elements(
            "UserEntity", "OrderEntity", "CustomerEntity",
            "ProductEntity", "SessionEntity", "InvoiceEntity");
    }

    /// <summary>
    /// Generates non-string property types (for no-prefix tests).
    /// </summary>
    private static Gen<string> GenAnyPropertyType()
    {
        return Gen.Elements(
            "string", "Guid", "DateTime", "int", "long",
            "decimal", "MyEnum", "Ulid", "DateTimeOffset");
    }

    #endregion

    /// <summary>
    /// Property: For all string-typed key properties with a prefix, the generated output passes
    /// typedEntity.{PropertyName} directly to ApplyKeyPrefix (no .ToString() wrapper).
    ///
    /// This confirms that string key properties with prefix are handled correctly on unfixed code
    /// and should remain unchanged after the fix.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-prefix-non-string-key-type")]
    [Trait("Property", "2-preservation")]
    public Property StringKeyWithPrefix_PassesDirectlyToApplyKeyPrefix_WithoutToStringConversion()
    {
        var testCaseGen = from propertyName in GenPropertyName()
                          from prefix in GenPrefix()
                          from separator in GenSeparator()
                          from className in GenClassName()
                          from isPartitionKey in Gen.Elements(true, false)
                          select (propertyName, prefix, separator, className, isPartitionKey);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (propertyName, prefix, separator, className, isPartitionKey) = testCase;

                // Arrange: Create entity with a string key property that has a prefix.
                // Always include both a partition key and a sort key so the entity is valid.
                var properties = isPartitionKey
                    ? new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = propertyName,
                            AttributeName = "pk",
                            PropertyType = "string",
                            IsPartitionKey = true,
                            IsSortKey = false,
                            KeyFormat = new KeyFormatModel { Prefix = prefix, Separator = separator }
                        },
                        new PropertyModel
                        {
                            PropertyName = "SortValue",
                            AttributeName = "sk",
                            PropertyType = "string",
                            IsPartitionKey = false,
                            IsSortKey = true
                        }
                    }
                    : new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "PartitionId",
                            AttributeName = "pk",
                            PropertyType = "string",
                            IsPartitionKey = true,
                            IsSortKey = false,
                            KeyFormat = new KeyFormatModel { Prefix = "PK", Separator = "#" }
                        },
                        new PropertyModel
                        {
                            PropertyName = propertyName,
                            AttributeName = "sk",
                            PropertyType = "string",
                            IsPartitionKey = false,
                            IsSortKey = true,
                            KeyFormat = new KeyFormatModel { Prefix = prefix, Separator = separator }
                        }
                    };

                var entity = new EntityModel
                {
                    ClassName = className,
                    Namespace = "TestNamespace",
                    TableName = "test-table",
                    Properties = properties
                };

                // Act: Generate the entity implementation
                var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: The generated code passes the string value directly (no .ToString())
                var expectedDirect = $"ApplyKeyPrefix(typedEntity.{propertyName},";
                var unexpectedToString = $"ApplyKeyPrefix(typedEntity.{propertyName}.ToString()";

                var containsDirectPass = generatedSource.Contains(expectedDirect);
                var doesNotContainToString = !generatedSource.Contains(unexpectedToString);

                return (containsDirectPass && doesNotContainToString).ToProperty()
                    .Label($"className='{className}', property='{propertyName}', type='string', " +
                           $"prefix='{prefix}', separator='{separator}', " +
                           $"containsDirectPass={containsDirectPass}, doesNotContainToString={doesNotContainToString}");
            });
    }

    /// <summary>
    /// Property: For all key properties without a prefix configured, GenerateKeyPrefixApplication
    /// does not emit any ApplyKeyPrefix call for that property.
    ///
    /// This confirms that key properties without prefix are unaffected by prefix application
    /// logic on unfixed code, and should remain unchanged after the fix.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-prefix-non-string-key-type")]
    [Trait("Property", "2-preservation")]
    public Property KeyWithoutPrefix_DoesNotGenerateApplyKeyPrefixCall()
    {
        var testCaseGen = from propertyName in GenPropertyName()
                          from attributeName in GenAttributeName()
                          from propertyType in GenAnyPropertyType()
                          from className in GenClassName()
                          from isPartitionKey in Gen.Elements(true, false)
                          from hasNullKeyFormat in Gen.Elements(true, false)
                          select (propertyName, attributeName, propertyType, className, isPartitionKey, hasNullKeyFormat);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (propertyName, attributeName, propertyType, className, isPartitionKey, hasNullKeyFormat) = testCase;

                // Arrange: Create entity with a key property that has NO prefix
                // Two cases: KeyFormat is null, or KeyFormat has empty/null prefix
                KeyFormatModel? keyFormat = hasNullKeyFormat
                    ? null
                    : new KeyFormatModel { Prefix = null, Separator = "#" };

                var entity = new EntityModel
                {
                    ClassName = className,
                    Namespace = "TestNamespace",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = propertyName,
                            AttributeName = attributeName,
                            PropertyType = propertyType,
                            IsPartitionKey = isPartitionKey,
                            IsSortKey = !isPartitionKey,
                            KeyFormat = keyFormat
                        }
                    }
                };

                // Act: Generate the entity implementation
                var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: No ApplyKeyPrefix call is generated for this property
                var unexpectedCall = $"ApplyKeyPrefix(typedEntity.{propertyName}";
                var doesNotContainApplyKeyPrefix = !generatedSource.Contains(unexpectedCall);

                return doesNotContainApplyKeyPrefix.ToProperty()
                    .Label($"className='{className}', property='{propertyName}', type='{propertyType}', " +
                           $"hasNullKeyFormat={hasNullKeyFormat}, " +
                           $"doesNotContainApplyKeyPrefix={doesNotContainApplyKeyPrefix}");
            });
    }

    /// <summary>
    /// Property: For computed key properties, the prefix application path is not invoked
    /// regardless of type or prefix configuration.
    ///
    /// Computed keys have their own separate code path and should never enter
    /// GenerateKeyPrefixApplication.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-prefix-non-string-key-type")]
    [Trait("Property", "2-preservation")]
    public Property ComputedKeyProperty_NeverGeneratesApplyKeyPrefixCall()
    {
        var testCaseGen = from propertyName in GenPropertyName()
                          from attributeName in GenAttributeName()
                          from propertyType in GenAnyPropertyType()
                          from prefix in GenPrefix()
                          from separator in GenSeparator()
                          from className in GenClassName()
                          from isPartitionKey in Gen.Elements(true, false)
                          select (propertyName, attributeName, propertyType, prefix, separator, className, isPartitionKey);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (propertyName, attributeName, propertyType, prefix, separator, className, isPartitionKey) = testCase;

                // Arrange: Create entity with a computed key property that has a prefix
                var entity = new EntityModel
                {
                    ClassName = className,
                    Namespace = "TestNamespace",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = propertyName,
                            AttributeName = attributeName,
                            PropertyType = propertyType,
                            IsPartitionKey = isPartitionKey,
                            IsSortKey = !isPartitionKey,
                            // Mark as computed key
                            ComputedKey = new ComputedKeyModel
                            {
                                SourceProperties = new[] { "Prop1", "Prop2" },
                                Separator = "#"
                            },
                            KeyFormat = new KeyFormatModel { Prefix = prefix, Separator = separator }
                        }
                    }
                };

                // Act: Generate the entity implementation
                var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: No ApplyKeyPrefix call is generated for computed key properties
                var unexpectedCall = $"ApplyKeyPrefix(typedEntity.{propertyName}";
                var doesNotContainApplyKeyPrefix = !generatedSource.Contains(unexpectedCall);

                return doesNotContainApplyKeyPrefix.ToProperty()
                    .Label($"className='{className}', property='{propertyName}', type='{propertyType}', " +
                           $"prefix='{prefix}', isComputed=true, " +
                           $"doesNotContainApplyKeyPrefix={doesNotContainApplyKeyPrefix}");
            });
    }

    /// <summary>
    /// Property: For constant key properties, the prefix application path is not invoked
    /// regardless of type or prefix configuration.
    ///
    /// Constant keys (properties with a ConstantKeyValue set) are excluded from prefix
    /// application and should never generate ApplyKeyPrefix calls.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-prefix-non-string-key-type")]
    [Trait("Property", "2-preservation")]
    public Property ConstantKeyProperty_NeverGeneratesApplyKeyPrefixCall()
    {
        var testCaseGen = from propertyName in GenPropertyName()
                          from attributeName in GenAttributeName()
                          from prefix in GenPrefix()
                          from separator in GenSeparator()
                          from className in GenClassName()
                          from isPartitionKey in Gen.Elements(true, false)
                          from constantValue in Gen.Elements("PROFILE", "META", "SETTINGS", "CONFIG")
                          select (propertyName, attributeName, prefix, separator, className, isPartitionKey, constantValue);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (propertyName, attributeName, prefix, separator, className, isPartitionKey, constantValue) = testCase;

                // Arrange: Create entity with a constant key property that has a prefix
                var entity = new EntityModel
                {
                    ClassName = className,
                    Namespace = "TestNamespace",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = propertyName,
                            AttributeName = attributeName,
                            PropertyType = "string",
                            IsPartitionKey = isPartitionKey,
                            IsSortKey = !isPartitionKey,
                            // Mark as constant key
                            ConstantKeyValue = constantValue,
                            KeyFormat = new KeyFormatModel { Prefix = prefix, Separator = separator }
                        }
                    }
                };

                // Act: Generate the entity implementation
                var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert: No ApplyKeyPrefix call is generated for constant key properties
                var unexpectedCall = $"ApplyKeyPrefix(typedEntity.{propertyName}";
                var doesNotContainApplyKeyPrefix = !generatedSource.Contains(unexpectedCall);

                return doesNotContainApplyKeyPrefix.ToProperty()
                    .Label($"className='{className}', property='{propertyName}', " +
                           $"constantValue='{constantValue}', prefix='{prefix}', " +
                           $"doesNotContainApplyKeyPrefix={doesNotContainApplyKeyPrefix}");
            });
    }
}
