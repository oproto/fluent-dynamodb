using AwesomeAssertions;
using Xunit;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

/// <summary>
/// Tests for the SemanticAssertions utility class to ensure it correctly
/// identifies code structures and provides helpful error messages.
/// </summary>
public class SemanticAssertionsTests
{
    private const string SampleCode = @"
using System;
using System.Linq;
using System.Collections.Generic;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            var result = items.Select(x => x.Value).ToList();
            myProperty = ""test"";
        }

        public string GetValue()
        {
            return ""value"";
        }
    }
}";

    [Fact]
    public void ShouldContainMethod_WhenMethodExists_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        SampleCode.ShouldContainMethod("TestMethod");
        SampleCode.ShouldContainMethod("GetValue");
    }

    [Fact]
    public void ShouldContainMethod_WhenMethodDoesNotExist_ThrowsWithAvailableMethods()
    {
        // Act
        var exception = Assert.Throws<SemanticAssertionException>(() =>
            SampleCode.ShouldContainMethod("NonExistentMethod", "testing error messages"));

        // Assert
        exception.Message.Should().Contain("NonExistentMethod");
        exception.Message.Should().Contain("testing error messages");
        exception.Message.Should().Contain("Available methods:");
        exception.Message.Should().Contain("TestMethod");
        exception.Message.Should().Contain("GetValue");
    }

    [Fact]
    public void ShouldContainAssignment_WhenAssignmentExists_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        SampleCode.ShouldContainAssignment("myProperty");
    }

    [Fact]
    public void ShouldContainAssignment_WhenAssignmentDoesNotExist_ThrowsWithAvailableTargets()
    {
        // Act
        var exception = Assert.Throws<SemanticAssertionException>(() =>
            SampleCode.ShouldContainAssignment("nonExistentProperty", "testing assignment check"));

        // Assert
        exception.Message.Should().Contain("nonExistentProperty");
        exception.Message.Should().Contain("testing assignment check");
        exception.Message.Should().Contain("Available assignment targets:");
        exception.Message.Should().Contain("myProperty");
    }

    [Fact]
    public void ShouldUseLinqMethod_WhenLinqMethodExists_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        SampleCode.ShouldUseLinqMethod("Select");
        SampleCode.ShouldUseLinqMethod("ToList");
    }

    [Fact]
    public void ShouldUseLinqMethod_WhenLinqMethodDoesNotExist_ThrowsWithAvailableMethods()
    {
        // Act
        var exception = Assert.Throws<SemanticAssertionException>(() =>
            SampleCode.ShouldUseLinqMethod("Where", "testing LINQ method check"));

        // Assert
        exception.Message.Should().Contain("Where");
        exception.Message.Should().Contain("testing LINQ method check");
        exception.Message.Should().Contain("Available method calls:");
    }

    [Fact]
    public void ShouldReferenceType_WhenTypeExists_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        SampleCode.ShouldReferenceType("Generic"); // From System.Collections.Generic
        SampleCode.ShouldReferenceType("Select"); // LINQ method call
    }

    [Fact]
    public void ShouldReferenceType_WhenTypeDoesNotExist_ThrowsWithAvailableTypes()
    {
        // Act
        var exception = Assert.Throws<SemanticAssertionException>(() =>
            SampleCode.ShouldReferenceType("NonExistentType", "testing type reference check"));

        // Assert
        exception.Message.Should().Contain("NonExistentType");
        exception.Message.Should().Contain("testing type reference check");
        exception.Message.Should().Contain("Available type references:");
    }

    [Fact]
    public void ShouldContainMethod_WithEmptyCode_ProvidesHelpfulMessage()
    {
        // Arrange
        var emptyCode = "namespace Test { }";

        // Act
        var exception = Assert.Throws<SemanticAssertionException>(() =>
            emptyCode.ShouldContainMethod("AnyMethod"));

        // Assert
        exception.Message.Should().Contain("No methods found in the source code");
    }

    [Fact]
    public void ErrorMessages_IncludeSourceContext()
    {
        // Act
        var exception = Assert.Throws<SemanticAssertionException>(() =>
            SampleCode.ShouldContainMethod("NonExistent"));

        // Assert
        exception.Message.Should().Contain("Source code context:");
        exception.Message.Should().Contain("using System;");
    }
}
