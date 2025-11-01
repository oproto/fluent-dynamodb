using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

public class TableOperationTests
{
    [Fact]
    public void HasGetValue()
    {
        // Act
        var value = TableOperation.Get;

        // Assert
        value.Should().Be(TableOperation.Get);
        ((int)value).Should().Be(1);
    }

    [Fact]
    public void HasQueryValue()
    {
        // Act
        var value = TableOperation.Query;

        // Assert
        value.Should().Be(TableOperation.Query);
        ((int)value).Should().Be(2);
    }

    [Fact]
    public void HasScanValue()
    {
        // Act
        var value = TableOperation.Scan;

        // Assert
        value.Should().Be(TableOperation.Scan);
        ((int)value).Should().Be(4);
    }

    [Fact]
    public void HasPutValue()
    {
        // Act
        var value = TableOperation.Put;

        // Assert
        value.Should().Be(TableOperation.Put);
        ((int)value).Should().Be(8);
    }

    [Fact]
    public void HasDeleteValue()
    {
        // Act
        var value = TableOperation.Delete;

        // Assert
        value.Should().Be(TableOperation.Delete);
        ((int)value).Should().Be(16);
    }

    [Fact]
    public void HasUpdateValue()
    {
        // Act
        var value = TableOperation.Update;

        // Assert
        value.Should().Be(TableOperation.Update);
        ((int)value).Should().Be(32);
    }

    [Fact]
    public void AllValueIncludesAllOperations()
    {
        // Act
        var all = TableOperation.All;

        // Assert
        all.Should().HaveFlag(TableOperation.Get);
        all.Should().HaveFlag(TableOperation.Query);
        all.Should().HaveFlag(TableOperation.Scan);
        all.Should().HaveFlag(TableOperation.Put);
        all.Should().HaveFlag(TableOperation.Delete);
        all.Should().HaveFlag(TableOperation.Update);
    }

    [Fact]
    public void AllValueEqualsOrOfAllOperations()
    {
        // Act
        var expected = TableOperation.Get | TableOperation.Query | TableOperation.Scan | 
                      TableOperation.Put | TableOperation.Delete | TableOperation.Update;

        // Assert
        TableOperation.All.Should().Be(expected);
    }

    [Fact]
    public void CanCombineOperationsWithBitwiseOr()
    {
        // Act
        var combined = TableOperation.Get | TableOperation.Query;

        // Assert
        combined.Should().HaveFlag(TableOperation.Get);
        combined.Should().HaveFlag(TableOperation.Query);
        combined.Should().NotHaveFlag(TableOperation.Put);
    }

    [Fact]
    public void CanCombineMultipleOperations()
    {
        // Act
        var combined = TableOperation.Get | TableOperation.Query | TableOperation.Scan;

        // Assert
        combined.Should().HaveFlag(TableOperation.Get);
        combined.Should().HaveFlag(TableOperation.Query);
        combined.Should().HaveFlag(TableOperation.Scan);
        combined.Should().NotHaveFlag(TableOperation.Put);
        combined.Should().NotHaveFlag(TableOperation.Delete);
        combined.Should().NotHaveFlag(TableOperation.Update);
    }

    [Fact]
    public void CanCheckIndividualFlagsInCombination()
    {
        // Arrange
        var operations = TableOperation.Put | TableOperation.Delete | TableOperation.Update;

        // Act & Assert
        operations.HasFlag(TableOperation.Put).Should().BeTrue();
        operations.HasFlag(TableOperation.Delete).Should().BeTrue();
        operations.HasFlag(TableOperation.Update).Should().BeTrue();
        operations.HasFlag(TableOperation.Get).Should().BeFalse();
        operations.HasFlag(TableOperation.Query).Should().BeFalse();
        operations.HasFlag(TableOperation.Scan).Should().BeFalse();
    }

    [Fact]
    public void HasFlagsAttribute()
    {
        // Arrange
        var enumType = typeof(TableOperation);

        // Act
        var hasFlagsAttribute = enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Any();

        // Assert
        hasFlagsAttribute.Should().BeTrue();
    }

    [Fact]
    public void AllIndividualValuesArePowersOfTwo()
    {
        // Act & Assert
        IsPowerOfTwo((int)TableOperation.Get).Should().BeTrue();
        IsPowerOfTwo((int)TableOperation.Query).Should().BeTrue();
        IsPowerOfTwo((int)TableOperation.Scan).Should().BeTrue();
        IsPowerOfTwo((int)TableOperation.Put).Should().BeTrue();
        IsPowerOfTwo((int)TableOperation.Delete).Should().BeTrue();
        IsPowerOfTwo((int)TableOperation.Update).Should().BeTrue();
    }

    [Fact]
    public void CanConvertToString()
    {
        // Act & Assert
        TableOperation.Get.ToString().Should().Be("Get");
        TableOperation.Query.ToString().Should().Be("Query");
        TableOperation.All.ToString().Should().Be("All");
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }
}
