using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for ListOperationExtensions methods.
/// These tests verify that the extension methods throw InvalidOperationException
/// when called directly, as they are only meant for use in expression trees.
/// </summary>
public class ListOperationExtensionsTests
{
    [Fact]
    public void Append_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<string> { "item1", "item2" };

        // Act
        var act = () => list.Append("item3");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void Prepend_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<string> { "item1", "item2" };

        // Act
        var act = () => list.Prepend("item0");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void AppendRange_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<string> { "item1", "item2" };
        var itemsToAdd = new[] { "item3", "item4" };

        // Act
        var act = () => list.AppendRange(itemsToAdd);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void PrependRange_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<string> { "item1", "item2" };
        var itemsToAdd = new[] { "item-1", "item0" };

        // Act
        var act = () => list.PrependRange(itemsToAdd);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void Append_WithIntList_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };

        // Act
        var act = () => list.Append(4);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void AppendRange_WithIntList_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };
        var itemsToAdd = new[] { 4, 5 };

        // Act
        var act = () => list.AppendRange(itemsToAdd);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void Append_WithComplexType_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<TestItem>();
        var newItem = new TestItem { Id = "1", Name = "Test" };

        // Act
        var act = () => list.Append(newItem);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void PrependRange_WithEmptyEnumerable_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<string> { "item1" };
        var emptyItems = Enumerable.Empty<string>();

        // Act
        var act = () => list.PrependRange(emptyItems);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void SetAt_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<string> { "item1", "item2" };

        // Act
        var act = () => list.SetAt(0, "updated");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void SetAt_WithIntList_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };

        // Act
        var act = () => list.SetAt(1, 99);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void SetAt_WithComplexType_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<TestItem> { new TestItem { Id = "1", Name = "Original" } };
        var newItem = new TestItem { Id = "1", Name = "Updated" };

        // Act
        var act = () => list.SetAt(0, newItem);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void RemoveAt_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<string> { "item1", "item2", "item3" };

        // Act - Must call as static method because List<T> has a built-in RemoveAt(int) method
        var act = () => ListOperationExtensions.RemoveAt(list, 1);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void RemoveAt_WithIntList_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };

        // Act - Must call as static method because List<T> has a built-in RemoveAt(int) method
        var act = () => ListOperationExtensions.RemoveAt(list, 0);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    [Fact]
    public void RemoveAt_WithComplexType_WhenCalledDirectly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<TestItem> { new TestItem { Id = "1", Name = "First" }, new TestItem { Id = "2", Name = "Second" } };

        // Act - Must call as static method because List<T> has a built-in RemoveAt(int) method
        var act = () => ListOperationExtensions.RemoveAt(list, 0);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*update expressions*");
    }

    private class TestItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
