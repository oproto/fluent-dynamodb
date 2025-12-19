using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Tests for static entry point classes DynamoDbTransactions and DynamoDbBatch.
/// </summary>
public class StaticEntryPointTests
{
    [Fact]
    public void DynamoDbTransactions_Write_ReturnsNewTransactionWriteBuilder()
    {
        // Act
        var builder = DynamoDbTransactions.Write;

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<TransactionWriteBuilder>();
    }

    [Fact]
    public void DynamoDbTransactions_Write_ReturnsNewInstanceEachTime()
    {
        // Act
        var builder1 = DynamoDbTransactions.Write;
        var builder2 = DynamoDbTransactions.Write;

        // Assert
        builder1.Should().NotBeNull();
        builder2.Should().NotBeNull();
        builder1.Should().NotBeSameAs(builder2);
    }

    [Fact]
    public void DynamoDbTransactions_Get_ReturnsNewTransactionGetBuilder()
    {
        // Act
        var builder = DynamoDbTransactions.Get;

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<TransactionGetBuilder>();
    }

    [Fact]
    public void DynamoDbTransactions_Get_ReturnsNewInstanceEachTime()
    {
        // Act
        var builder1 = DynamoDbTransactions.Get;
        var builder2 = DynamoDbTransactions.Get;

        // Assert
        builder1.Should().NotBeNull();
        builder2.Should().NotBeNull();
        builder1.Should().NotBeSameAs(builder2);
    }

    [Fact]
    public void DynamoDbBatch_Write_ReturnsNewBatchWriteBuilder()
    {
        // Act
        var builder = DynamoDbBatch.Write;

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<BatchWriteBuilder>();
    }

    [Fact]
    public void DynamoDbBatch_Write_ReturnsNewInstanceEachTime()
    {
        // Act
        var builder1 = DynamoDbBatch.Write;
        var builder2 = DynamoDbBatch.Write;

        // Assert
        builder1.Should().NotBeNull();
        builder2.Should().NotBeNull();
        builder1.Should().NotBeSameAs(builder2);
    }

    [Fact]
    public void DynamoDbBatch_Get_ReturnsNewBatchGetBuilder()
    {
        // Act
        var builder = DynamoDbBatch.Get;

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<BatchGetBuilder>();
    }

    [Fact]
    public void DynamoDbBatch_Get_ReturnsNewInstanceEachTime()
    {
        // Act
        var builder1 = DynamoDbBatch.Get;
        var builder2 = DynamoDbBatch.Get;

        // Assert
        builder1.Should().NotBeNull();
        builder2.Should().NotBeNull();
        builder1.Should().NotBeSameAs(builder2);
    }
}
