using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests verifying end-to-end source generation behavior for tautological
/// exclusion patterns. Validates that DISC006 is emitted for tautological cases, that
/// valid hierarchies still generate correct exclusion guards, and that generated code
/// compiles even when DISC006 fires.
/// </summary>
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class TautologicalExclusionIntegrationTests
{
    #region Entity Sources

    private const string TautologicalEntitySource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""*#ROLE#*"")]
    public partial class RoleItem
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""test-table"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""USER#*#ROLE#*"")]
    public partial class UserRole
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

    private const string ValidHierarchyEntitySource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""USER#*"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""test-table"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""USER#*#ROLE#*"")]
    public partial class UserRole
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

    private const string ThreeEntityTableSource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""SVCACCT#*"")]
    public partial class ServiceAccount
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""test-table"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""SVCACCT#*#ROLE#*"")]
    public partial class ServiceAccountRole
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""test-table"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""USER#*"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""test-table"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""USER#*#ROLE#*"")]
    public partial class UserRole
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

    #endregion

    #region Test 1: TautologicalPattern_EmitsDISC006_NoExclusionGuard

    [Fact]
    public void TautologicalPattern_EmitsDISC006_NoExclusionGuard()
    {
        // Arrange & Act — run the source generator on the tautological pattern pair
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(TautologicalEntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert — DISC006 should be present
        var disc006 = diagnostics.Where(d => d.Id == "DISC006").ToList();
        disc006.Should().NotBeEmpty("DISC006 should be emitted for tautological exclusion between RoleItem and UserRole");

        // Assert — DISC004 should NOT be present (they have different specificity scores)
        var disc004 = diagnostics.Where(d => d.Id == "DISC004").ToList();
        disc004.Should().BeEmpty("DISC004 should not be emitted — patterns have different specificity scores");

        // Assert — generated RoleItem code should NOT contain an exclusion guard for Contains("#ROLE#")
        var roleItemTree = outputCompilation.SyntaxTrees
            .Skip(1) // skip original source
            .FirstOrDefault(t => t.FilePath.Contains("RoleItem.g.cs"));

        roleItemTree.Should().NotBeNull("RoleItem.g.cs should be generated");
        var roleItemCode = roleItemTree!.GetText().ToString();

        // The RoleItem's MatchesEntity should NOT contain an exclusion guard that would
        // conflict with its own positive Contains("#ROLE#") check
        roleItemCode.Should().NotContain("!discriminatorValue.Contains(\"#ROLE#\")",
            "RoleItem should NOT have a tautological exclusion guard for Contains(\"#ROLE#\")");
    }

    #endregion

    #region Test 2: ValidHierarchy_GeneratesCorrectExclusionGuard

    [Fact]
    public void ValidHierarchy_GeneratesCorrectExclusionGuard()
    {
        // Arrange — compile with the valid hierarchy source
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            ValidHierarchyEntitySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var userType = compilationResult.Assembly.GetType("TestNamespace.User")
            ?? throw new InvalidOperationException("User type not found in compiled assembly");
        var userRoleType = compilationResult.Assembly.GetType("TestNamespace.UserRole")
            ?? throw new InvalidOperationException("UserRole type not found in compiled assembly");

        // Act & Assert — User.MatchesEntity matches "USER#123" but NOT "USER#123#ROLE#admin"
        var userItem = CreateItem("PK#1", "USER#123");
        InvokeMatchesEntity(userType, userItem).Should().BeTrue(
            "User should match sort key 'USER#123'");

        var userRoleItem = CreateItem("PK#1", "USER#123#ROLE#admin");
        InvokeMatchesEntity(userType, userRoleItem).Should().BeFalse(
            "User should NOT match sort key 'USER#123#ROLE#admin' (exclusion guard for UserRole)");

        // Act & Assert — UserRole.MatchesEntity matches "USER#123#ROLE#admin" but NOT "USER#123"
        InvokeMatchesEntity(userRoleType, userRoleItem).Should().BeTrue(
            "UserRole should match sort key 'USER#123#ROLE#admin'");

        InvokeMatchesEntity(userRoleType, userItem).Should().BeFalse(
            "UserRole should NOT match sort key 'USER#123'");
    }

    #endregion

    #region Test 3: ThreeEntityTable_MixedPatterns_CorrectBehavior

    [Fact]
    public void ThreeEntityTable_MixedPatterns_CorrectBehavior()
    {
        // Arrange — compile with the four-entity source
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            ThreeEntityTableSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        // Assert — no DISC006 in diagnostics (all patterns are valid hierarchies)
        var disc006 = compilationResult.Diagnostics.Where(d => d.Id == "DISC006").ToList();
        disc006.Should().BeEmpty("No DISC006 should be emitted — all patterns form valid hierarchies");

        var serviceAccountType = compilationResult.Assembly.GetType("TestNamespace.ServiceAccount")
            ?? throw new InvalidOperationException("ServiceAccount type not found");
        var serviceAccountRoleType = compilationResult.Assembly.GetType("TestNamespace.ServiceAccountRole")
            ?? throw new InvalidOperationException("ServiceAccountRole type not found");
        var userType = compilationResult.Assembly.GetType("TestNamespace.User")
            ?? throw new InvalidOperationException("User type not found");
        var userRoleType = compilationResult.Assembly.GetType("TestNamespace.UserRole")
            ?? throw new InvalidOperationException("UserRole type not found");

        // Define test keys and expected matches
        var testKeys = new[]
        {
            ("SVCACCT#001", "ServiceAccount"),
            ("SVCACCT#001#ROLE#admin", "ServiceAccountRole"),
            ("USER#123", "User"),
            ("USER#123#ROLE#editor", "UserRole"),
        };

        var entityTypes = new[]
        {
            (serviceAccountType, "ServiceAccount"),
            (serviceAccountRoleType, "ServiceAccountRole"),
            (userType, "User"),
            (userRoleType, "UserRole"),
        };

        // Assert mutual exclusivity — exactly one entity matches each test key
        foreach (var (sk, expectedMatch) in testKeys)
        {
            var item = CreateItem("PK#1", sk);
            var matches = entityTypes
                .Where(e => InvokeMatchesEntity(e.Item1, item))
                .Select(e => e.Item2)
                .ToList();

            matches.Should().HaveCount(1,
                $"exactly one entity should match '{sk}', but matched: [{string.Join(", ", matches)}]");
            matches[0].Should().Be(expectedMatch,
                $"'{sk}' should be matched by {expectedMatch}");
        }
    }

    #endregion

    #region Test 4: TautologicalPattern_GeneratedCode_StillCompiles

    [Fact]
    public void TautologicalPattern_GeneratedCode_StillCompiles()
    {
        // Arrange — run the source generator on the tautological pattern pair
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(TautologicalEntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Act — emit the compilation and verify it compiles without errors
        using var ms = new MemoryStream();
        var emitResult = outputCompilation.Emit(ms);

        // Assert — compilation should succeed (no emit errors) even though DISC006 fires
        var emitErrors = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        emitErrors.Should().BeEmpty(
            "Generated code should compile without errors even when DISC006 is emitted. " +
            $"Errors: {string.Join(", ", emitErrors.Select(e => e.ToString()))}");

        // Verify DISC006 was indeed emitted (confirms tautology was detected)
        diagnostics.Where(d => d.Id == "DISC006").Should().NotBeEmpty(
            "DISC006 should still be emitted for the tautological pattern");
    }

    #endregion

    #region Helper Methods

    private static Dictionary<string, AttributeValue> CreateItem(string pk, string sk)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
    }

    private static bool InvokeMatchesEntity(Type entityType, Dictionary<string, AttributeValue> item)
    {
        var method = entityType.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException(
                $"MatchesEntity method not found on type '{entityType.Name}'. " +
                "Ensure the source generator produced the expected code.");
        }

        var result = method.Invoke(null, new object[] { item });
        return (bool)result!;
    }

    #endregion
}
