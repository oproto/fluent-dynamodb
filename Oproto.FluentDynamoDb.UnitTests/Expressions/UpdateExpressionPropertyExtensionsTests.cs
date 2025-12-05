using Oproto.FluentDynamoDb.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for UpdateExpressionPropertyExtensions to verify type safety and runtime behavior.
/// </summary>
/// <remarks>
/// These tests verify that:
/// 1. Extension methods are only available on appropriate property types (compile-time check)
/// 2. All extension methods throw InvalidOperationException when called directly (runtime check)
/// 3. Type constraints prevent incompatible operations (compile-time check)
/// 
/// Note: IntelliSense behavior must be verified manually in the IDE.
/// </remarks>
public class UpdateExpressionPropertyExtensionsTests
{
    #region Add() Method Tests - Numeric Types

    [Fact]
    public void Add_Int_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<int>();

        // Act
        Action act = () => property.Add(1);
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void Add_Long_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<long>();

        // Act
        Action act = () => property.Add(1L);
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void Add_Decimal_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<decimal>();

        // Act
        Action act = () => property.Add(1.5m);
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void Add_Double_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<double>();

        // Act
        Action act = () => property.Add(1.5);
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void Add_Int_WithNegativeValue_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<int>();

        // Act
        Action act = () => property.Add(-10);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion

    #region Add() Method Tests - Set Types

    [Fact]
    public void Add_HashSetString_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<string>>();

        // Act
        Action act = () => property.Add("tag1", "tag2");
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void Add_HashSetInt_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<int>>();

        // Act
        Action act = () => property.Add(1, 2, 3);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Add_HashSetString_WithSingleElement_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<string>>();

        // Act
        Action act = () => property.Add("tag1");
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Add_HashSetString_WithEmptyArray_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<string>>();

        // Act
        Action act = () => property.Add();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion

    #region Remove() Method Tests

    [Fact]
    public void Remove_String_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<string>();

        // Act
        Action act = () => property.Remove();
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void Remove_Int_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<int>();

        // Act
        Action act = () => property.Remove();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Remove_HashSet_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<string>>();

        // Act
        Action act = () => property.Remove();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Remove_List_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<string>>();

        // Act
        Action act = () => property.Remove();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Remove_CustomType_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<TestCustomType>();

        // Act
        Action act = () => property.Remove();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion

    #region Delete() Method Tests

    [Fact]
    public void Delete_HashSetString_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<string>>();

        // Act
        Action act = () => property.Delete("tag1", "tag2");
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void Delete_HashSetInt_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<int>>();

        // Act
        Action act = () => property.Delete(1, 2, 3);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Delete_HashSetString_WithSingleElement_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<string>>();

        // Act
        Action act = () => property.Delete("tag1");
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Delete_HashSetString_WithEmptyArray_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<string>>();

        // Act
        Action act = () => property.Delete();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion

    #region IfNotExists() Method Tests

    [Fact]
    public void IfNotExists_String_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<string>();

        // Act
        Action act = () => property.IfNotExists("default");
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void IfNotExists_Int_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<int>();

        // Act
        Action act = () => property.IfNotExists(0);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void IfNotExists_HashSet_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<HashSet<string>>();
        var defaultValue = new HashSet<string> { "default" };

        // Act
        Action act = () => property.IfNotExists(defaultValue);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void IfNotExists_List_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<string>>();
        var defaultValue = new List<string> { "default" };

        // Act
        Action act = () => property.IfNotExists(defaultValue);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void IfNotExists_CustomType_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<TestCustomType>();
        var defaultValue = new TestCustomType { Value = "default" };

        // Act
        Action act = () => property.IfNotExists(defaultValue);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion

    #region ListAppend() Method Tests

    [Fact]
    public void ListAppend_ListString_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<string>>();

        // Act
        Action act = () => property.ListAppend("item1", "item2");
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void ListAppend_ListInt_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<int>>();

        // Act
        Action act = () => property.ListAppend(1, 2, 3);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void ListAppend_ListString_WithSingleElement_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<string>>();

        // Act
        Action act = () => property.ListAppend("item1");
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void ListAppend_ListString_WithEmptyArray_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<string>>();

        // Act
        Action act = () => property.ListAppend();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void ListAppend_ListCustomType_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<TestCustomType>>();
        var item = new TestCustomType { Value = "test" };

        // Act
        Action act = () => property.ListAppend(item);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion

    #region ListPrepend() Method Tests

    [Fact]
    public void ListPrepend_ListString_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<string>>();

        // Act
        Action act = () => property.ListPrepend("item1", "item2");
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void ListPrepend_ListInt_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<int>>();

        // Act
        Action act = () => property.ListPrepend(1, 2, 3);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void ListPrepend_ListString_WithSingleElement_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<string>>();

        // Act
        Action act = () => property.ListPrepend("item1");
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void ListPrepend_ListString_WithEmptyArray_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<string>>();

        // Act
        Action act = () => property.ListPrepend();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void ListPrepend_ListCustomType_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<List<TestCustomType>>();
        var item = new TestCustomType { Value = "test" };

        // Act
        Action act = () => property.ListPrepend(item);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion

    #region Type Safety Compilation Tests

    /// <summary>
    /// This test verifies that Add() is available on numeric types at compile time.
    /// The fact that this code compiles proves the extension method is available.
    /// </summary>
    [Fact]
    public void Add_IsAvailableOnNumericTypes_CompilationTest()
    {
        // These should all compile successfully
        var intProperty = new UpdateExpressionProperty<int>();
        var longProperty = new UpdateExpressionProperty<long>();
        var decimalProperty = new UpdateExpressionProperty<decimal>();
        var doubleProperty = new UpdateExpressionProperty<double>();

        // Verify the methods exist and throw when called
        Action actInt = () => intProperty.Add(1);
        Action actLong = () => longProperty.Add(1L);
        Action actDecimal = () => decimalProperty.Add(1.0m);
        Action actDouble = () => doubleProperty.Add(1.0);
        
        actInt.Should().Throw<InvalidOperationException>();
        actLong.Should().Throw<InvalidOperationException>();
        actDecimal.Should().Throw<InvalidOperationException>();
        actDouble.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// This test verifies that Add() is available on HashSet types at compile time.
    /// The fact that this code compiles proves the extension method is available.
    /// </summary>
    [Fact]
    public void Add_IsAvailableOnHashSetTypes_CompilationTest()
    {
        // These should all compile successfully
        var stringSetProperty = new UpdateExpressionProperty<HashSet<string>>();
        var intSetProperty = new UpdateExpressionProperty<HashSet<int>>();

        // Verify the methods exist and throw when called
        Action actString = () => stringSetProperty.Add("tag");
        Action actInt = () => intSetProperty.Add(1);
        
        actString.Should().Throw<InvalidOperationException>();
        actInt.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// This test verifies that Delete() is only available on HashSet types at compile time.
    /// The fact that this code compiles proves the extension method is available.
    /// </summary>
    [Fact]
    public void Delete_IsOnlyAvailableOnHashSetTypes_CompilationTest()
    {
        // These should compile successfully
        var stringSetProperty = new UpdateExpressionProperty<HashSet<string>>();
        var intSetProperty = new UpdateExpressionProperty<HashSet<int>>();

        // Verify the methods exist and throw when called
        Action actString = () => stringSetProperty.Delete("tag");
        Action actInt = () => intSetProperty.Delete(1);
        
        actString.Should().Throw<InvalidOperationException>();
        actInt.Should().Throw<InvalidOperationException>();

        // Note: The following would NOT compile (type safety enforced at compile time):
        // var stringProperty = new UpdateExpressionProperty<string>();
        // stringProperty.Delete("value"); // Compile error: no such method
        
        // var listProperty = new UpdateExpressionProperty<List<string>>();
        // listProperty.Delete("value"); // Compile error: no such method
    }

    /// <summary>
    /// This test verifies that ListAppend() and ListPrepend() are only available on List types at compile time.
    /// The fact that this code compiles proves the extension methods are available.
    /// </summary>
    [Fact]
    public void ListAppendAndPrepend_AreOnlyAvailableOnListTypes_CompilationTest()
    {
        // These should compile successfully
        var stringListProperty = new UpdateExpressionProperty<List<string>>();
        var intListProperty = new UpdateExpressionProperty<List<int>>();

        // Verify the methods exist and throw when called
        Action actAppendString = () => stringListProperty.ListAppend("item");
        Action actPrependString = () => stringListProperty.ListPrepend("item");
        Action actAppendInt = () => intListProperty.ListAppend(1);
        Action actPrependInt = () => intListProperty.ListPrepend(1);
        
        actAppendString.Should().Throw<InvalidOperationException>();
        actPrependString.Should().Throw<InvalidOperationException>();
        actAppendInt.Should().Throw<InvalidOperationException>();
        actPrependInt.Should().Throw<InvalidOperationException>();

        // Note: The following would NOT compile (type safety enforced at compile time):
        // var stringProperty = new UpdateExpressionProperty<string>();
        // stringProperty.ListAppend("value"); // Compile error: no such method
        
        // var setProperty = new UpdateExpressionProperty<HashSet<string>>();
        // setProperty.ListAppend("value"); // Compile error: no such method
    }

    /// <summary>
    /// This test verifies that Remove() is available on all types at compile time.
    /// The fact that this code compiles proves the extension method is available.
    /// </summary>
    [Fact]
    public void Remove_IsAvailableOnAllTypes_CompilationTest()
    {
        // These should all compile successfully
        var stringProperty = new UpdateExpressionProperty<string>();
        var intProperty = new UpdateExpressionProperty<int>();
        var listProperty = new UpdateExpressionProperty<List<string>>();
        var setProperty = new UpdateExpressionProperty<HashSet<string>>();
        var customProperty = new UpdateExpressionProperty<TestCustomType>();

        // Verify the methods exist and throw when called
        Action actString = () => stringProperty.Remove();
        Action actInt = () => intProperty.Remove();
        Action actList = () => listProperty.Remove();
        Action actSet = () => setProperty.Remove();
        Action actCustom = () => customProperty.Remove();
        
        actString.Should().Throw<InvalidOperationException>();
        actInt.Should().Throw<InvalidOperationException>();
        actList.Should().Throw<InvalidOperationException>();
        actSet.Should().Throw<InvalidOperationException>();
        actCustom.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// This test verifies that IfNotExists() is available on all types at compile time.
    /// The fact that this code compiles proves the extension method is available.
    /// </summary>
    [Fact]
    public void IfNotExists_IsAvailableOnAllTypes_CompilationTest()
    {
        // These should all compile successfully
        var stringProperty = new UpdateExpressionProperty<string>();
        var intProperty = new UpdateExpressionProperty<int>();
        var listProperty = new UpdateExpressionProperty<List<string>>();
        var setProperty = new UpdateExpressionProperty<HashSet<string>>();
        var customProperty = new UpdateExpressionProperty<TestCustomType>();

        // Verify the methods exist and throw when called
        Action actString = () => stringProperty.IfNotExists("default");
        Action actInt = () => intProperty.IfNotExists(0);
        Action actList = () => listProperty.IfNotExists(new List<string>());
        Action actSet = () => setProperty.IfNotExists(new HashSet<string>());
        Action actCustom = () => customProperty.IfNotExists(new TestCustomType());
        
        actString.Should().Throw<InvalidOperationException>();
        actInt.Should().Throw<InvalidOperationException>();
        actList.Should().Throw<InvalidOperationException>();
        actSet.Should().Throw<InvalidOperationException>();
        actCustom.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Nullable Add() Method Tests

    [Fact]
    public void Add_NullableInt_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<int?>();

        // Act
        Action act = () => property.Add(1);
        
        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("only for use in update expressions");
        exception.Message.Should().Contain("should not be called directly");
    }

    [Fact]
    public void Add_NullableLong_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<long?>();

        // Act
        Action act = () => property.Add(1L);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Add_NullableDecimal_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<decimal?>();

        // Act
        Action act = () => property.Add(1.5m);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    [Fact]
    public void Add_NullableDouble_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<double?>();

        // Act
        Action act = () => property.Add(1.5);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion

    #region Nullable IfNotExists() Method Tests

    [Fact]
    public void IfNotExists_NullableInt_ThrowsInvalidOperationException()
    {
        // Arrange
        var property = new UpdateExpressionProperty<int?>();

        // Act
        Action act = () => property.IfNotExists(0);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for use in update expressions*");
    }

    #endregion



    #region Nullable Type Safety Compilation Tests

    /// <summary>
    /// This test verifies that Add() is available on nullable numeric types at compile time.
    /// The fact that this code compiles proves the extension method is available.
    /// </summary>
    [Fact]
    public void Add_IsAvailableOnNullableNumericTypes_CompilationTest()
    {
        // These should all compile successfully
        var intProperty = new UpdateExpressionProperty<int?>();
        var longProperty = new UpdateExpressionProperty<long?>();
        var decimalProperty = new UpdateExpressionProperty<decimal?>();
        var doubleProperty = new UpdateExpressionProperty<double?>();

        // Verify the methods exist and throw when called
        Action actInt = () => intProperty.Add(1);
        Action actLong = () => longProperty.Add(1L);
        Action actDecimal = () => decimalProperty.Add(1.0m);
        Action actDouble = () => doubleProperty.Add(1.0);
        
        actInt.Should().Throw<InvalidOperationException>();
        actLong.Should().Throw<InvalidOperationException>();
        actDecimal.Should().Throw<InvalidOperationException>();
        actDouble.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// This test verifies that IfNotExists() is available on nullable value types at compile time.
    /// The fact that this code compiles proves the extension method is available.
    /// </summary>
    [Fact]
    public void IfNotExists_IsAvailableOnNullableValueTypes_CompilationTest()
    {
        // These should compile successfully
        var intProperty = new UpdateExpressionProperty<int?>();
        var longProperty = new UpdateExpressionProperty<long?>();
        var decimalProperty = new UpdateExpressionProperty<decimal?>();
        var doubleProperty = new UpdateExpressionProperty<double?>();

        // Verify the methods exist and throw when called
        Action actInt = () => intProperty.IfNotExists(0);
        Action actLong = () => longProperty.IfNotExists(0L);
        Action actDecimal = () => decimalProperty.IfNotExists(0.0m);
        Action actDouble = () => doubleProperty.IfNotExists(0.0);
        
        actInt.Should().Throw<InvalidOperationException>();
        actLong.Should().Throw<InvalidOperationException>();
        actDecimal.Should().Throw<InvalidOperationException>();
        actDouble.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Helper Classes

    private class TestCustomType
    {
        public string Value { get; set; } = string.Empty;
    }

    #endregion
}
