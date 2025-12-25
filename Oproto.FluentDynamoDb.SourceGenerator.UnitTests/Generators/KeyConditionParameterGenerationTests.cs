using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for KeyCondition parameter generation in convenience methods.
/// Verifies that PutAsync, DeleteAsync, and Update methods include the optional
/// KeyCondition parameter for specifying key existence conditions.
/// </summary>
[Trait("Category", "Unit")]
public class KeyConditionParameterGenerationTests
{
    [Fact]
    public void PutAsync_HasKeyConditionParameter_SimpleKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // PutAsync should have KeyCondition parameter
        tableCode.Should().Contain("PutAsync(User entity, KeyCondition keyCondition = KeyCondition.None",
            "PutAsync should have optional KeyCondition parameter with default None");
        
        // Should apply key condition when not None
        tableCode.Should().Contain("if (keyCondition != KeyCondition.None)",
            "should check if keyCondition is not None");
        tableCode.Should().Contain("builder.WithKeyCondition(keyCondition)",
            "should apply key condition to builder");
    }

    [Fact]
    public void PutAsync_HasKeyConditionParameter_CompositeKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string CustomerId { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string OrderId { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // PutAsync should have KeyCondition parameter for composite key entity
        tableCode.Should().Contain("PutAsync(Order entity, KeyCondition keyCondition = KeyCondition.None",
            "PutAsync should have optional KeyCondition parameter for composite key entity");
    }

    [Fact]
    public void DeleteAsync_HasKeyConditionParameter_SimpleKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // DeleteAsync should have KeyCondition parameter
        tableCode.Should().Contain("DeleteAsync(string pk, KeyCondition keyCondition = KeyCondition.None",
            "DeleteAsync should have optional KeyCondition parameter with default None");
    }

    [Fact]
    public void DeleteAsync_HasKeyConditionParameter_CompositeKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string CustomerId { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string OrderId { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // DeleteAsync should have KeyCondition parameter for composite key
        tableCode.Should().Contain("DeleteAsync(string pk, string sk, KeyCondition keyCondition = KeyCondition.None",
            "DeleteAsync should have optional KeyCondition parameter for composite key entity");
    }

    [Fact]
    public void Update_HasKeyConditionParameter_SimpleKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Update should have KeyCondition parameter
        tableCode.Should().Contain("Update(string pk, KeyCondition keyCondition = KeyCondition.None)",
            "Update should have optional KeyCondition parameter with default None");
        
        // Should apply key condition when not None
        tableCode.Should().Contain("if (keyCondition != KeyCondition.None)",
            "should check if keyCondition is not None");
        tableCode.Should().Contain("builder.WithKeyCondition(keyCondition)",
            "should apply key condition to builder");
    }

    [Fact]
    public void Update_HasKeyConditionParameter_CompositeKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string CustomerId { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string OrderId { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Update should have KeyCondition parameter for composite key
        tableCode.Should().Contain("Update(string pk, string sk, KeyCondition keyCondition = KeyCondition.None)",
            "Update should have optional KeyCondition parameter for composite key entity");
    }

    [Fact]
    public void TableLevelUpdate_HasKeyConditionParameter_SimpleKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table-level Update should have KeyCondition parameter and delegate correctly
        tableCode.Should().Contain("public UserUpdateBuilder Update(string pk, KeyCondition keyCondition = KeyCondition.None)",
            "Table-level Update should have optional KeyCondition parameter");
        tableCode.Should().Contain("Users.Update(pk, keyCondition)",
            "Table-level Update should pass keyCondition to accessor");
    }

    [Fact]
    public void TableLevelUpdate_HasKeyConditionParameter_CompositeKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string CustomerId { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string OrderId { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table-level Update should have KeyCondition parameter for composite key
        tableCode.Should().Contain("public OrderUpdateBuilder Update(string pk, string sk, KeyCondition keyCondition = KeyCondition.None)",
            "Table-level Update should have optional KeyCondition parameter for composite key");
        tableCode.Should().Contain("Orders.Update(pk, sk, keyCondition)",
            "Table-level Update should pass keyCondition to accessor for composite key");
    }

    [Fact]
    public void TableLevelDeleteAsync_HasKeyConditionParameter_SimpleKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table-level DeleteAsync should have KeyCondition parameter and delegate correctly
        tableCode.Should().Contain("DeleteAsync(string pk, KeyCondition keyCondition = KeyCondition.None",
            "Table-level DeleteAsync should have optional KeyCondition parameter");
        tableCode.Should().Contain("Users.DeleteAsync(pk, keyCondition, cancellationToken)",
            "Table-level DeleteAsync should pass keyCondition to accessor");
    }

    [Fact]
    public void FluentResults_PutAsyncResult_HasKeyConditionParameter()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    [UseFluentResults]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // PutAsyncResult should have KeyCondition parameter
        tableCode.Should().Contain("PutAsyncResult(User entity, KeyCondition keyCondition = KeyCondition.None",
            "PutAsyncResult should have optional KeyCondition parameter");
    }

    [Fact]
    public void FluentResults_DeleteAsyncResult_HasKeyConditionParameter()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    [UseFluentResults]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // DeleteAsyncResult should have KeyCondition parameter
        tableCode.Should().Contain("DeleteAsyncResult(string pk, KeyCondition keyCondition = KeyCondition.None",
            "DeleteAsyncResult should have optional KeyCondition parameter");
    }

    [Fact]
    public void GeneratedCode_CompilesSuccessfully_WithKeyConditionParameter()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "generated code with KeyCondition parameter should compile without errors");
    }

    [Fact]
    public void PutAsync_HasCancellationTokenOnlyOverload_SimpleKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // PutAsync should have cancellation-token-only overload
        tableCode.Should().Contain("PutAsync(User entity, System.Threading.CancellationToken cancellationToken) =>",
            "PutAsync should have cancellation-token-only overload");
        tableCode.Should().Contain("PutAsync(entity, KeyCondition.None, cancellationToken)",
            "cancellation-token-only overload should delegate to full version");
    }

    [Fact]
    public void DeleteAsync_HasCancellationTokenOnlyOverload_SimpleKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // DeleteAsync should have cancellation-token-only overload
        tableCode.Should().Contain("DeleteAsync(string pk, System.Threading.CancellationToken cancellationToken) =>",
            "DeleteAsync should have cancellation-token-only overload");
        tableCode.Should().Contain("DeleteAsync(pk, KeyCondition.None, cancellationToken)",
            "cancellation-token-only overload should delegate to full version");
    }

    [Fact]
    public void DeleteAsync_HasCancellationTokenOnlyOverload_CompositeKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string CustomerId { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string OrderId { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // DeleteAsync should have cancellation-token-only overload for composite key
        tableCode.Should().Contain("DeleteAsync(string pk, string sk, System.Threading.CancellationToken cancellationToken) =>",
            "DeleteAsync should have cancellation-token-only overload for composite key");
        tableCode.Should().Contain("DeleteAsync(pk, sk, KeyCondition.None, cancellationToken)",
            "cancellation-token-only overload should delegate to full version for composite key");
    }

    [Fact]
    public void FluentResults_PutAsyncResult_HasCancellationTokenOnlyOverload()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    [UseFluentResults]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // PutAsyncResult should have cancellation-token-only overload
        tableCode.Should().Contain("PutAsyncResult(User entity, System.Threading.CancellationToken cancellationToken) =>",
            "PutAsyncResult should have cancellation-token-only overload");
        tableCode.Should().Contain("PutAsyncResult(entity, KeyCondition.None, cancellationToken)",
            "cancellation-token-only overload should delegate to full version");
    }

    [Fact]
    public void FluentResults_DeleteAsyncResult_HasCancellationTokenOnlyOverload()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    [UseFluentResults]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // DeleteAsyncResult should have cancellation-token-only overload
        tableCode.Should().Contain("DeleteAsyncResult(string pk, System.Threading.CancellationToken cancellationToken) =>",
            "DeleteAsyncResult should have cancellation-token-only overload");
        tableCode.Should().Contain("DeleteAsyncResult(pk, KeyCondition.None, cancellationToken)",
            "cancellation-token-only overload should delegate to full version");
    }

    [Fact]
    public void TableLevelDeleteAsync_HasCancellationTokenOnlyOverload_SimpleKey()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Table-level DeleteAsync should have cancellation-token-only overload
        tableCode.Should().Contain("public System.Threading.Tasks.Task DeleteAsync(string pk, System.Threading.CancellationToken cancellationToken) =>",
            "Table-level DeleteAsync should have cancellation-token-only overload");
        tableCode.Should().Contain("Users.DeleteAsync(pk, cancellationToken)",
            "Table-level cancellation-token-only overload should delegate to accessor");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source)
            },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }
}
