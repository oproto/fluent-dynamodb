using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

/// <summary>
/// Functional tests for generated Scan() methods on tables marked with [Scannable] attribute.
/// Tests both parameterless and expression-based Scan() overloads.
/// </summary>
public class ScannableTableTests
{
    #region Test 8.1: Generated Scan() Method Functionality
    
    [Fact]
    public void ParameterlessScan_ReturnsConfiguredScanRequestBuilder()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan();
        
        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ScanRequestBuilder>();
        
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("TestScannableTable");
    }
    
    [Fact]
    public void ExpressionBasedScan_AppliesFilterCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan("status = {0}", "ACTIVE");
        
        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ScanRequestBuilder>();
        
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("TestScannableTable");
        request.FilterExpression.Should().Be("status = :p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("ACTIVE");
    }
    
    [Fact]
    public void ExpressionBasedScan_WithMultipleParameters_AppliesFilterCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan("status = {0} AND price > {1}", "ACTIVE", 100m);
        
        // Assert
        var request = builder.ToScanRequest();
        request.FilterExpression.Should().Be("status = :p0 AND price > :p1");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues[":p1"].N.Should().Be("100");
    }
    
    [Fact]
    public void ParameterlessScan_AllowsMethodChaining()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan()
            .WithProjection("id, name, status")
            .Take(10);
        
        // Assert
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("TestScannableTable");
        request.ProjectionExpression.Should().Be("id, name, status");
        request.Limit.Should().Be(10);
    }
    
    [Fact]
    public void ExpressionBasedScan_AllowsMethodChaining()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan("status = {0}", "ACTIVE")
            .WithProjection("id, name, status")
            .Take(10)
            .UsingConsistentRead();
        
        // Assert
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("TestScannableTable");
        request.FilterExpression.Should().Be("status = :p0");
        request.ProjectionExpression.Should().Be("id, name, status");
        request.Limit.Should().Be(10);
        request.ConsistentRead.Should().BeTrue();
    }
    
    [Fact]
    public void ParameterlessScan_PassesCorrectTableNameToBuilder()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan();
        var request = builder.ToScanRequest();
        
        // Assert
        request.TableName.Should().Be("TestScannableTable");
    }
    
    [Fact]
    public void ExpressionBasedScan_PassesCorrectTableNameToBuilder()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan("status = {0}", "ACTIVE");
        var request = builder.ToScanRequest();
        
        // Assert
        request.TableName.Should().Be("TestScannableTable");
    }
    
    [Fact]
    public void ParameterlessScan_WithIndex_ConfiguresIndexScan()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan()
            .UsingIndex("StatusIndex");
        
        // Assert
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("TestScannableTable");
        request.IndexName.Should().Be("StatusIndex");
    }
    
    [Fact]
    public void ExpressionBasedScan_WithAdditionalConfiguration_WorksCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan("status = {0}", "ACTIVE")
            .Take(10);
        
        // Assert
        var request = builder.ToScanRequest();
        request.FilterExpression.Should().Be("status = :p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("ACTIVE");
        request.Limit.Should().Be(10);
    }
    
    [Fact]
    public void ParameterlessScan_WithParallelScan_ConfiguresSegments()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan()
            .WithSegment(0, 4);
        
        // Assert
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("TestScannableTable");
        request.Segment.Should().Be(0);
        request.TotalSegments.Should().Be(4);
    }
    
    #endregion
    
    #region Helper Classes
    
    /// <summary>
    /// Test table that simulates a generated scannable table.
    /// This mimics what the source generator would produce for a table marked with [Scannable].
    /// </summary>
    private class TestScannableTable : DynamoDbTableBase
    {
        public TestScannableTable(IAmazonDynamoDB client) 
            : base(client, "TestScannableTable")
        {
        }
        
        public TestScannableTable(IAmazonDynamoDB client, IDynamoDbLogger logger) 
            : base(client, "TestScannableTable", logger)
        {
        }
        
        /// <summary>
        /// Simulates the generated parameterless Scan() method.
        /// </summary>
        public ScanRequestBuilder Scan() => 
            new ScanRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
        
        /// <summary>
        /// Simulates the generated expression-based Scan() method.
        /// </summary>
        public ScanRequestBuilder Scan(string filterExpression, params object[] values)
        {
            var builder = Scan();
            return Oproto.FluentDynamoDb.Requests.Extensions.WithFilterExpressionExtensions.WithFilter(builder, filterExpression, values);
        }
    }
    
    #endregion
}
