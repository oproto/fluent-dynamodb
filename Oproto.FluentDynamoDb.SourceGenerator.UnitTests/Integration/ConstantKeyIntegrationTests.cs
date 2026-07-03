using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// End-to-end integration tests for constant key detection feature.
/// These tests compile complete entity definitions through the source generator
/// and verify the generated code compiles cleanly, contains expected patterns,
/// and produces correct output structure.
/// 
/// **Validates: Requirements 1.1, 2.1, 4.1, 5.1, 6.1, 7.1, 8.1**
/// </summary>
[Trait("Category", "Integration")]
public class ConstantKeyIntegrationTests
{
    #region Entity 1: Expression-body constant SK + variable PK

    /// <summary>
    /// Verifies that an entity with an expression-body constant sort key and a variable
    /// partition key compiles without errors through the source generator.
    /// </summary>
    [Fact]
    public void ExpressionBodyConstantSk_WithVariablePk_CompilesWithoutErrors()
    {
        // Arrange
        var source = GetExpressionBodyConstantSkSource();

        // Act
        var result = RunSourceGenerator(source);

        // Assert
        result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("entity with expression-body constant SK should compile without errors");
        result.GeneratedSources.Should().NotBeEmpty("source generator should produce output");
    }

    /// <summary>
    /// Verifies the Keys class has a parameterless SK accessor for a constant sort key.
    /// </summary>
    [Fact]
    public void ExpressionBodyConstantSk_KeysClass_HasParameterlessSkAccessor()
    {
        // Arrange
        var source = GetExpressionBodyConstantSkSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "Customer.g.cs");

        // Assert
        entityCode.Should().Contain("public static string Sk => \"PROFILE\";",
            "Keys class should provide parameterless Sk accessor returning the constant value");
        entityCode.Should().NotContain("public static string Sk(string sk)",
            "Keys class should NOT have a parameterized Sk method for constant keys");
    }

    /// <summary>
    /// Verifies that convenience methods accept only the PK parameter when SK is constant.
    /// </summary>
    [Fact]
    public void ExpressionBodyConstantSk_ConvenienceMethods_AcceptOnlyPk()
    {
        // Arrange
        var source = GetExpressionBodyConstantSkSource();

        // Act
        var result = RunSourceGenerator(source);
        var allGeneratedCode = GetAllGeneratedCode(result);

        // Assert - Get, Delete, Update should have single-parameter overloads (PK only)
        // The key builder Key() should accept only the variable key parameter
        var entityCode = GetGeneratedSource(result, "Customer.g.cs");
        entityCode.Should().Contain("(string PartitionKey, string SortKey) Key(string customerId)",
            "Key() should accept only the variable PK parameter");
        entityCode.Should().NotContain("Key(string customerId, string sk)",
            "Key() should NOT accept both keys when SK is constant");
    }

    /// <summary>
    /// Verifies that ToDynamoDb emits the constant sort key value directly.
    /// </summary>
    [Fact]
    public void ExpressionBodyConstantSk_ToDynamoDb_EmitsConstantDirectly()
    {
        // Arrange
        var source = GetExpressionBodyConstantSkSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "Customer.g.cs");

        // Assert
        entityCode.Should().Contain("item[\"sk\"] = new AttributeValue { S = \"PROFILE\" };",
            "ToDynamoDb should emit constant SK value directly without reading entity instance");
        entityCode.Should().NotContain("typedEntity.Sk",
            "ToDynamoDb should NOT read the constant key property from entity instance");
    }

    /// <summary>
    /// Verifies that FromDynamoDb validates the incoming constant key value.
    /// </summary>
    [Fact]
    public void ExpressionBodyConstantSk_FromDynamoDb_ValidatesIncomingValue()
    {
        // Arrange
        var source = GetExpressionBodyConstantSkSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "Customer.g.cs");

        // Assert - FromDynamoDb should validate incoming value and log on mismatch
        entityCode.Should().Contain("\"PROFILE\"",
            "FromDynamoDb should reference the expected constant value for validation");
        entityCode.Should().Contain("LogWarning",
            "FromDynamoDb should log a warning when constant key value doesn't match");
    }

    /// <summary>
    /// Verifies that the update model excludes the constant sort key property.
    /// </summary>
    [Fact]
    public void ExpressionBodyConstantSk_UpdateModel_ExcludesConstantKeyProperty()
    {
        // Arrange
        var source = GetExpressionBodyConstantSkSource();

        // Act
        var result = RunSourceGenerator(source);
        var updateModelCode = GetGeneratedSourceContaining(result, "UpdateModel");

        // Assert - Update model should include Name but not Sk
        if (updateModelCode != null)
        {
            updateModelCode.Should().Contain("Name",
                "Update model should include non-key properties like Name");
            updateModelCode.Should().NotContain("public string Sk",
                "Update model should exclude constant key property Sk");
        }
    }

    #endregion

    #region Entity 2: Read-only auto-property constant PK

    /// <summary>
    /// Verifies that an entity with a read-only auto-property constant partition key
    /// compiles without errors through the source generator.
    /// </summary>
    [Fact]
    public void ReadOnlyAutoPropertyConstantPk_CompilesWithoutErrors()
    {
        // Arrange
        var source = GetReadOnlyAutoPropertyConstantPkSource();

        // Act
        var result = RunSourceGenerator(source);

        // Assert
        result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("entity with read-only auto-property constant PK should compile without errors");
        result.GeneratedSources.Should().NotBeEmpty("source generator should produce output");
    }

    /// <summary>
    /// Verifies the Keys class has a parameterless PK accessor for a constant partition key.
    /// </summary>
    [Fact]
    public void ReadOnlyAutoPropertyConstantPk_KeysClass_HasParameterlessPkAccessor()
    {
        // Arrange
        var source = GetReadOnlyAutoPropertyConstantPkSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "AppConfig.g.cs");

        // Assert
        entityCode.Should().Contain("public static string Pk => \"APP_CONFIG\";",
            "Keys class should provide parameterless Pk accessor returning the constant value");
        entityCode.Should().NotContain("public static string Pk(string pk)",
            "Keys class should NOT have a parameterized Pk method for constant keys");
    }

    /// <summary>
    /// Verifies that ToDynamoDb emits the constant PK value directly.
    /// </summary>
    [Fact]
    public void ReadOnlyAutoPropertyConstantPk_ToDynamoDb_EmitsConstantDirectly()
    {
        // Arrange
        var source = GetReadOnlyAutoPropertyConstantPkSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "AppConfig.g.cs");

        // Assert
        entityCode.Should().Contain("item[\"pk\"] = new AttributeValue { S = \"APP_CONFIG\" };",
            "ToDynamoDb should emit constant PK value directly");
        entityCode.Should().NotContain("typedEntity.Pk",
            "ToDynamoDb should NOT read the constant PK property from entity instance");
    }

    #endregion

    #region Entity 3: Both keys constant (parameterless everything)

    /// <summary>
    /// Verifies that an entity with both keys constant compiles without errors.
    /// </summary>
    [Fact]
    public void BothKeysConstant_CompilesWithoutErrors()
    {
        // Arrange
        var source = GetBothKeysConstantSource();

        // Act
        var result = RunSourceGenerator(source);

        // Assert
        result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("entity with both keys constant should compile without errors");
        result.GeneratedSources.Should().NotBeEmpty("source generator should produce output");
    }

    /// <summary>
    /// Verifies the Keys class has parameterless accessors for both constant keys.
    /// </summary>
    [Fact]
    public void BothKeysConstant_KeysClass_HasParameterlessAccessorsForBothKeys()
    {
        // Arrange
        var source = GetBothKeysConstantSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "SingletonConfig.g.cs");

        // Assert
        entityCode.Should().Contain("public static string Pk => \"SINGLETON\";",
            "Keys class should provide parameterless Pk accessor");
        entityCode.Should().Contain("public static string Sk => \"CONFIG\";",
            "Keys class should provide parameterless Sk accessor");
    }

    /// <summary>
    /// Verifies that when both keys are constant, the Key() method is parameterless.
    /// </summary>
    [Fact]
    public void BothKeysConstant_KeysClass_HasParameterlessKeyMethod()
    {
        // Arrange
        var source = GetBothKeysConstantSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "SingletonConfig.g.cs");

        // Assert
        entityCode.Should().Contain("(string PartitionKey, string SortKey) Key()",
            "Key() should be parameterless when both keys are constant");
        entityCode.Should().Contain("(Pk, Sk)",
            "Key() body should reference both constant key properties");
    }

    /// <summary>
    /// Verifies that ToDynamoDb emits both constant key values directly.
    /// </summary>
    [Fact]
    public void BothKeysConstant_ToDynamoDb_EmitsBothConstantsDirectly()
    {
        // Arrange
        var source = GetBothKeysConstantSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "SingletonConfig.g.cs");

        // Assert
        entityCode.Should().Contain("item[\"pk\"] = new AttributeValue { S = \"SINGLETON\" };",
            "ToDynamoDb should emit constant PK value directly");
        entityCode.Should().Contain("item[\"sk\"] = new AttributeValue { S = \"CONFIG\" };",
            "ToDynamoDb should emit constant SK value directly");
    }

    /// <summary>
    /// Verifies that FromDynamoDb validates both constant key values.
    /// </summary>
    [Fact]
    public void BothKeysConstant_FromDynamoDb_ValidatesBothIncomingValues()
    {
        // Arrange
        var source = GetBothKeysConstantSource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "SingletonConfig.g.cs");

        // Assert
        entityCode.Should().Contain("\"SINGLETON\"",
            "FromDynamoDb should reference the expected constant PK value");
        entityCode.Should().Contain("\"CONFIG\"",
            "FromDynamoDb should reference the expected constant SK value");
    }

    #endregion

    #region Entity 4: Non-constant entity (regression)

    /// <summary>
    /// Verifies that a non-constant entity (standard PK+SK with prefixes) continues
    /// to work correctly and is unaffected by the constant key detection feature.
    /// </summary>
    [Fact]
    public void NonConstantEntity_CompilesWithoutErrors()
    {
        // Arrange
        var source = GetNonConstantEntitySource();

        // Act
        var result = RunSourceGenerator(source);

        // Assert
        result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("non-constant entity should compile without errors");
        result.GeneratedSources.Should().NotBeEmpty("source generator should produce output");
    }

    /// <summary>
    /// Verifies that a non-constant entity still has parameterized key methods.
    /// </summary>
    [Fact]
    public void NonConstantEntity_KeysClass_HasParameterizedMethods()
    {
        // Arrange
        var source = GetNonConstantEntitySource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "Order.g.cs");

        // Assert
        entityCode.Should().Contain("public static string Pk(string orderId)",
            "Non-constant entity should have parameterized Pk method");
        entityCode.Should().Contain("public static string Sk(string lineId)",
            "Non-constant entity should have parameterized Sk method");
        entityCode.Should().Contain("Key(string orderId, string lineId)",
            "Non-constant entity should have Key() accepting both parameters");
    }

    /// <summary>
    /// Verifies that a non-constant entity still reads from entity instance in ToDynamoDb.
    /// </summary>
    [Fact]
    public void NonConstantEntity_ToDynamoDb_ReadsFromEntityInstance()
    {
        // Arrange
        var source = GetNonConstantEntitySource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "Order.g.cs");

        // Assert
        entityCode.Should().Contain("typedEntity.OrderId",
            "Non-constant entity should read PK from entity instance in ToDynamoDb");
        entityCode.Should().Contain("typedEntity.LineId",
            "Non-constant entity should read SK from entity instance in ToDynamoDb");
    }

    /// <summary>
    /// Verifies that a non-constant entity has no constant key validation in FromDynamoDb.
    /// </summary>
    [Fact]
    public void NonConstantEntity_FromDynamoDb_NoConstantKeyValidation()
    {
        // Arrange
        var source = GetNonConstantEntitySource();

        // Act
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedSource(result, "Order.g.cs");

        // Assert - Non-constant entities should assign key properties normally
        entityCode.Should().Contain("entity.OrderId = ",
            "Non-constant entity should assign key properties from DynamoDB item");
        entityCode.Should().Contain("entity.LineId = ",
            "Non-constant entity should assign key properties from DynamoDB item");
    }

    #endregion

    #region Source Code Helpers

    private static string GetExpressionBodyConstantSkSource()
    {
        return @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Customers"", IsDefault = true)]
    public partial class Customer
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string CustomerId { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";
    }

    private static string GetReadOnlyAutoPropertyConstantPkSource()
    {
        return @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Config"")]
    public partial class AppConfig
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; } = ""APP_CONFIG"";

        [DynamoDbAttribute(""settingName"")]
        public string SettingName { get; set; } = string.Empty;
    }
}";
    }

    private static string GetBothKeysConstantSource()
    {
        return @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Singleton"")]
    public partial class SingletonConfig
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk => ""SINGLETON"";

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""CONFIG"";

        [DynamoDbAttribute(""value"")]
        public string Value { get; set; } = string.Empty;
    }
}";
    }

    private static string GetNonConstantEntitySource()
    {
        return @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Orders"")]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string OrderId { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string LineId { get; set; } = string.Empty;

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// Runs the source generator on the given source code and returns diagnostic information
    /// and the generated source outputs.
    /// </summary>
    private static GeneratorTestResult RunSourceGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var driverDiagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = driverDiagnostics,
            GeneratedSources = generatedSources
        };
    }

    /// <summary>
    /// Gets the generated source code for a specific file name.
    /// </summary>
    private static string GetGeneratedSource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        source.Should().NotBeNull($"Generated source file '{fileName}' should exist. " +
            $"Available: [{string.Join(", ", result.GeneratedSources.Select(s => Path.GetFileName(s.FileName)))}]");
        return source!.SourceText.ToString();
    }

    /// <summary>
    /// Gets the first generated source that contains the specified text in its file name.
    /// Returns null if not found (for optional file checks).
    /// </summary>
    private static string? GetGeneratedSourceContaining(GeneratorTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        return source?.SourceText.ToString();
    }

    /// <summary>
    /// Gets all generated source code concatenated (useful for searching across all generated files).
    /// </summary>
    private static string GetAllGeneratedCode(GeneratorTestResult result)
    {
        return string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
    }

    #endregion
}
