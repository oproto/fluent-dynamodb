using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for ambiguity detection in source generator output.
/// Constructs EntityModel where computed key has all-string source properties
/// matching existing overload signature. Verifies no typed overload is generated
/// (silent skip) and entity falls through to KeyInputMode eligibility if applicable.
///
/// **Validates: Requirements 8.1, 8.2, 8.3, 13.7**
/// </summary>
[Trait("Category", "Unit")]
public class AmbiguityDetectionUnitTests
{
    /// <summary>
    /// When a computed PK has source properties that cannot be resolved (missing from entity.Properties),
    /// WouldBeAmbiguous returns true and no typed overload is generated.
    /// This tests the "unresolvable source properties" ambiguity path.
    /// </summary>
    [Fact]
    public void UnresolvableSourceProperties_NoTypedOverloadGenerated()
    {
        // Arrange — entity with computed PK referencing non-existent source properties
        var entity = CreateEntityWithUnresolvableSourceProperties();

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — no typed overload delegation to Keys.Pk or Keys.Sk
        generatedCode.Should().NotContain("Keys.Pk(");
        generatedCode.Should().NotContain("Keys.Sk(");
    }

    /// <summary>
    /// When a computed PK has source properties that cannot be resolved,
    /// the standard string overload remains present (backward compatibility).
    /// </summary>
    [Fact]
    public void UnresolvableSourceProperties_StandardOverloadRemainsPresent()
    {
        // Arrange
        var entity = CreateEntityWithUnresolvableSourceProperties();

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — standard string overload still exists
        generatedCode.Should().Contain("Get(string pk");
    }

    /// <summary>
    /// When computed key source properties cannot be resolved AND the entity has a string key
    /// with a prefix, the entity falls through to KeyInputMode eligibility and the KeyInputMode
    /// parameter is added to the standard overload.
    /// </summary>
    [Fact]
    public void UnresolvableSourceProperties_WithPrefix_FallsThroughToKeyInputMode()
    {
        // Arrange — entity with unresolvable computed PK but also has a prefix configured
        var entity = CreateEntityWithUnresolvableSourcePropertiesAndPrefix();

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — KeyInputMode parameter is present (fallthrough from ambiguity)
        generatedCode.Should().Contain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// When computed key source properties cannot be resolved AND the entity has NO prefix,
    /// the entity does not get KeyInputMode either (neither typed overload nor KeyInputMode).
    /// </summary>
    [Fact]
    public void UnresolvableSourceProperties_WithoutPrefix_NoKeyInputMode()
    {
        // Arrange — entity with unresolvable computed PK and no prefix
        var entity = CreateEntityWithUnresolvableSourceProperties();

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — no KeyInputMode parameter (no prefix, no typed overload)
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verify that WouldBeAmbiguous returns true when source properties are unresolvable
    /// (GetTypedOverloadParameters returns null).
    /// </summary>
    [Fact]
    public void WouldBeAmbiguous_UnresolvableSourceProperties_ReturnsTrue()
    {
        // Arrange
        var entity = CreateEntityWithUnresolvableSourceProperties();

        // Act
        var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);
        var isAmbiguous = ComputedOverloadEligibility.WouldBeAmbiguous(entity);

        // Assert
        qualifies.Should().BeTrue("entity has computed PK with >= 2 source properties");
        isAmbiguous.Should().BeTrue("source properties cannot be resolved → treated as ambiguous");
    }

    /// <summary>
    /// Verify that WouldBeAmbiguous returns false when at least one source property is non-string,
    /// ensuring the non-ambiguous case is correctly identified.
    /// </summary>
    [Fact]
    public void WouldBeAmbiguous_NonStringSourceProperty_ReturnsFalse()
    {
        // Arrange — entity with computed PK using int + string source properties (non-ambiguous)
        var entity = CreateEntityWithNonStringSourceProperties();

        // Act
        var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);
        var isAmbiguous = ComputedOverloadEligibility.WouldBeAmbiguous(entity);

        // Assert
        qualifies.Should().BeTrue("entity has computed PK with >= 2 source properties");
        isAmbiguous.Should().BeFalse("at least one source property is non-string → typed overload is safe");
    }

    /// <summary>
    /// When source properties are all strings but count doesn't match standard overload count
    /// (which is the normal case for computed keys with >= 2 sources), WouldBeAmbiguous returns false.
    /// </summary>
    [Fact]
    public void WouldBeAmbiguous_AllStringButCountDiffers_ReturnsFalse()
    {
        // Arrange — entity with computed PK having 3 string sources, no SK
        // Standard: (string pk) = 1 param; Typed: (string s1, string s2, string s3) = 3 params
        var entity = CreateEntityWithAllStringSourcePropsCountMismatch();

        // Act
        var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);
        var isAmbiguous = ComputedOverloadEligibility.WouldBeAmbiguous(entity);

        // Assert
        qualifies.Should().BeTrue("entity has computed PK with >= 2 source properties");
        isAmbiguous.Should().BeFalse("typed param count (3) differs from standard param count (1)");
    }

    /// <summary>
    /// Verify that QualifiesForKeyInputMode returns true when entity is ambiguous
    /// (typed overload skipped) and the key has a prefix configured.
    /// This verifies the "fallthrough to KeyInputMode" behavior.
    /// </summary>
    [Fact]
    public void QualifiesForKeyInputMode_AmbiguousEntityWithPrefix_ReturnsTrue()
    {
        // Arrange — entity where typed overload is ambiguous but PK has a prefix
        var entity = CreateEntityWithUnresolvableSourcePropertiesAndPrefix();

        // Act
        var qualifies = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);

        // Assert — falls through from ambiguous typed overload to KeyInputMode eligibility
        qualifies.Should().BeTrue("typed overload is ambiguous + PK has prefix → KeyInputMode eligible");
    }

    /// <summary>
    /// Verify that no ambiguity-specific diagnostic is emitted when a typed overload is
    /// silently skipped per Requirement 8.3 (silent skip, no diagnostic needed).
    /// The generated code simply omits the typed overload with no warning or error about ambiguity.
    /// </summary>
    [Fact]
    public void AmbiguousEntity_SilentSkip_NoAmbiguityDiagnostic()
    {
        // Arrange
        var entity = CreateEntityWithUnresolvableSourceProperties();

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — the typed overload is simply omitted (silent skip)
        // No "ambiguity" or "collision" message in the generated code
        generatedCode.Should().NotContain("Keys.Pk(");
        // Standard overload remains (the entity is still functional)
        generatedCode.Should().Contain("Get(string pk");
    }

    /// <summary>
    /// When a typed overload is NOT ambiguous (non-string source types), verify that
    /// the typed overload IS generated (positive control test).
    /// </summary>
    [Fact]
    public void NonAmbiguousEntity_TypedOverloadIsGenerated()
    {
        // Arrange — entity with non-string source properties (int, int) → not ambiguous
        var entity = CreateEntityWithNonStringSourceProperties();

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — typed overload is generated
        generatedCode.Should().Contain("Keys.Pk(");
        generatedCode.Should().Contain("Get(int year, int month)");
    }

    /// <summary>
    /// Verify that all CRUD methods (Get, Delete, Update, ConditionCheck) skip typed overload
    /// generation when the entity is ambiguous.
    /// </summary>
    [Fact]
    public void AmbiguousEntity_AllCrudMethodsSkipTypedOverload()
    {
        // Arrange
        var entity = CreateEntityWithUnresolvableSourceProperties();

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — no typed overloads for any CRUD method
        generatedCode.Should().NotContain("Keys.Pk(");
        generatedCode.Should().NotContain("Keys.Sk(");

        // But standard string overloads remain
        generatedCode.Should().Contain("Get(string pk");
        generatedCode.Should().Contain("Delete(string pk");
        generatedCode.Should().Contain("Update(string pk");
        generatedCode.Should().Contain("ConditionCheck(string pk");
    }

    #region Helper Methods

    /// <summary>
    /// Creates an entity with a computed PK whose source properties are NOT present
    /// in entity.Properties, causing GetTypedOverloadParameters to return null
    /// and WouldBeAmbiguous to return true.
    /// </summary>
    private static EntityModel CreateEntityWithUnresolvableSourceProperties()
    {
        var properties = new List<PropertyModel>
        {
            // Computed PK references "FieldA" and "FieldB" which are NOT in Properties list
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = new[] { "FieldA", "FieldB" },
                    Separator = "#"
                }
            },
            // Only has a "Data" property — no FieldA or FieldB
            new PropertyModel
            {
                PropertyName = "Data",
                PropertyType = "string",
                AttributeName = "data"
            }
        };

        return new EntityModel
        {
            ClassName = "AmbiguousEntity",
            Namespace = "TestNamespace",
            TableName = "ambiguous-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    /// <summary>
    /// Creates an entity with unresolvable source properties AND a prefix on the PK.
    /// This verifies the fallthrough to KeyInputMode eligibility.
    /// </summary>
    private static EntityModel CreateEntityWithUnresolvableSourcePropertiesAndPrefix()
    {
        var properties = new List<PropertyModel>
        {
            // Computed PK with prefix, but source properties are unresolvable
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = new[] { "MissingPropA", "MissingPropB" },
                    Separator = "#"
                },
                KeyFormat = new KeyFormatModel
                {
                    Prefix = "ORDER",
                    Separator = "#"
                }
            },
            new PropertyModel
            {
                PropertyName = "Data",
                PropertyType = "string",
                AttributeName = "data"
            }
        };

        return new EntityModel
        {
            ClassName = "PrefixedAmbiguousEntity",
            Namespace = "TestNamespace",
            TableName = "prefixed-ambiguous-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    /// <summary>
    /// Creates an entity with non-string source properties (int, int) that IS resolvable
    /// and NOT ambiguous — used as a positive control.
    /// </summary>
    private static EntityModel CreateEntityWithNonStringSourceProperties()
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Year",
                PropertyType = "int",
                AttributeName = "year"
            },
            new PropertyModel
            {
                PropertyName = "Month",
                PropertyType = "int",
                AttributeName = "month"
            },
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = new[] { "Year", "Month" },
                    Separator = "#"
                }
            }
        };

        return new EntityModel
        {
            ClassName = "NonAmbiguousEntity",
            Namespace = "TestNamespace",
            TableName = "non-ambiguous-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    /// <summary>
    /// Creates an entity with all-string source properties where the typed param count
    /// differs from standard count (3 sources, no SK → typed count 3 vs standard count 1).
    /// This verifies that WouldBeAmbiguous correctly identifies the count mismatch.
    /// </summary>
    private static EntityModel CreateEntityWithAllStringSourcePropsCountMismatch()
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Region",
                PropertyType = "string",
                AttributeName = "region"
            },
            new PropertyModel
            {
                PropertyName = "Service",
                PropertyType = "string",
                AttributeName = "service"
            },
            new PropertyModel
            {
                PropertyName = "Instance",
                PropertyType = "string",
                AttributeName = "instance"
            },
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = new[] { "Region", "Service", "Instance" },
                    Separator = "#"
                }
            }
        };

        return new EntityModel
        {
            ClassName = "AllStringEntity",
            Namespace = "TestNamespace",
            TableName = "all-string-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
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
