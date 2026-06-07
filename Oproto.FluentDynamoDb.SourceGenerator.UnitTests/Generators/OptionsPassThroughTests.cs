using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Regression tests verifying that all generated builder instantiations pass FluentDynamoDbOptions.
///
/// Bug: Generated accessor Scan() and ConditionCheck() methods created builders without passing options,
/// causing encrypted entities to fail on read because the hydrator couldn't access the field encryptor.
///
/// Rule: Every "new *Builder" instantiation in generated table code MUST include Options/GetOptions().
/// If this test fails, a builder is being created without the options parameter.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "OptionsPassThrough")]
public class OptionsPassThroughTests
{
    /// <summary>
    /// Every builder instantiation in a single-entity generated table must pass options.
    /// </summary>
    [Fact]
    public void SingleEntityTable_AllBuilderInstantiations_IncludeOptions()
    {
        // Arrange
        var entity = CreateEntityWithEncryption();

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert — find all "new *Builder" instantiations and verify they include Options
        AssertAllBuildersReceiveOptions(result);
    }

    /// <summary>
    /// Every builder instantiation in a multi-entity generated table must pass options.
    /// </summary>
    [Fact]
    public void MultiEntityTable_AllBuilderInstantiations_IncludeOptions()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            CreateEntityWithEncryption(),
            CreatePlainEntity()
        };

        // Act
        var result = TableGenerator.GenerateTableClass("shared-table", entities);

        // Assert
        AssertAllBuildersReceiveOptions(result);
    }

    /// <summary>
    /// Specifically verify Scan accessor passes options (the bug that was found).
    /// </summary>
    [Fact]
    public void ScannableEntity_ScanBuilder_PassesOptions()
    {
        // Arrange
        var entity = CreateScannableEntityWithEncryption();

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert — Scan should create builder with options
        result.Should().Contain("new ScanRequestBuilder<SecureEntity>(DynamoDbClient, Options).ForTable(Name)",
            "Scan builder must be created with Options parameter");
    }

    /// <summary>
    /// Verify ConditionCheck passes options.
    /// </summary>
    [Fact]
    public void SingleEntityTable_ConditionCheck_PassesOptions()
    {
        // Arrange
        var entity = CreateEntityWithEncryption();

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert
        result.Should().Contain("new ConditionCheckBuilder<TEntity>(DynamoDbClient, Name, Options)",
            "Generic ConditionCheck must pass Options");
    }

    private static void AssertAllBuildersReceiveOptions(string generatedCode)
    {
        // Find all lines with "new *Builder" instantiations
        var lines = generatedCode.Split('\n');
        var builderInstantiations = lines
            .Select((line, index) => (line: line.Trim(), lineNumber: index + 1))
            .Where(x => x.line.Contains("new ") && x.line.Contains("Builder"))
            .Where(x => !x.line.StartsWith("//") && !x.line.StartsWith("///") && !x.line.StartsWith("*"))
            .ToList();

        var violations = builderInstantiations
            .Where(x => !x.line.Contains("Options") && !x.line.Contains("GetOptions"))
            .ToList();

        violations.Should().BeEmpty(
            $"All builder instantiations must include Options or GetOptions(). " +
            $"Violations found:\n{string.Join("\n", violations.Select(v => $"  Line {v.lineNumber}: {v.line}"))}");
    }

    #region Entity Factories

    private static EntityModel CreateEntityWithEncryption() => new()
    {
        ClassName = "SecureEntity",
        Namespace = "TestNamespace",
        TableName = "secure-table",
        IsDefault = true,
        Properties = new[]
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                AttributeName = "pk",
                PropertyType = "string",
                IsPartitionKey = true
            },
            new PropertyModel
            {
                PropertyName = "Secret",
                AttributeName = "secret",
                PropertyType = "string",
                Security = new SecurityInfo { IsEncrypted = true }
            }
        }
    };

    private static EntityModel CreateScannableEntityWithEncryption() => new()
    {
        ClassName = "SecureEntity",
        Namespace = "TestNamespace",
        TableName = "secure-table",
        IsDefault = true,
        IsScannable = true,
        Properties = new[]
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                AttributeName = "pk",
                PropertyType = "string",
                IsPartitionKey = true
            },
            new PropertyModel
            {
                PropertyName = "Secret",
                AttributeName = "secret",
                PropertyType = "string",
                Security = new SecurityInfo { IsEncrypted = true }
            }
        }
    };

    private static EntityModel CreatePlainEntity() => new()
    {
        ClassName = "PlainEntity",
        Namespace = "TestNamespace",
        TableName = "shared-table",
        IsDefault = false,
        EntityPropertyConfig = new EntityPropertyConfig { Generate = true },
        Properties = new[]
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                AttributeName = "pk",
                PropertyType = "string",
                IsPartitionKey = true
            },
            new PropertyModel
            {
                PropertyName = "Name",
                AttributeName = "name",
                PropertyType = "string"
            }
        }
    };

    #endregion
}
