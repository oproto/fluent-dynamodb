using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
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
        var builder = table.Scan<TestEntity>();
        
        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ScanRequestBuilder<TestEntity>>();
        
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("TestScannableTable");
    }
    
    public class TestEntity
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public decimal Price { get; set; }
    }
    
    [Fact]
    public void ExpressionBasedScan_AppliesFilterCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new TestScannableTable(client);
        
        // Act
        var builder = table.Scan<TestEntity>("status = {0}", "ACTIVE");
        
        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ScanRequestBuilder<TestEntity>>();
        
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
        var builder = table.Scan<TestEntity>("status = {0} AND price > {1}", "ACTIVE", 100m);
        
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
        var builder = table.Scan<TestEntity>()
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
        var builder = table.Scan<TestEntity>("status = {0}", "ACTIVE")
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
        var builder = table.Scan<TestEntity>();
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
        var builder = table.Scan<TestEntity>("status = {0}", "ACTIVE");
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
        var builder = table.Scan<TestEntity>()
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
        var builder = table.Scan<TestEntity>("status = {0}", "ACTIVE")
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
        var builder = table.Scan<TestEntity>()
            .WithSegment(0, 4);
        
        // Assert
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("TestScannableTable");
        request.Segment.Should().Be(0);
        request.TotalSegments.Should().Be(4);
    }
    
    #endregion
    
    #region Test 8.2: Manual Implementation Support
    
    [Fact]
    public void ManuallyImplementedScan_ParameterlessOverload_WorksCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ManualScannableTable(client);
        
        // Act
        var builder = table.Scan<TestEntity>();
        
        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ScanRequestBuilder<TestEntity>>();
        
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("ManualScannableTable");
    }
    
    [Fact]
    public void ManuallyImplementedScan_ExpressionBasedOverload_WorksCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ManualScannableTable(client);
        
        // Act
        var builder = table.Scan<TestEntity>("category = {0}", "Electronics");
        
        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ScanRequestBuilder<TestEntity>>();
        
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("ManualScannableTable");
        request.FilterExpression.Should().Be("category = :p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("Electronics");
    }
    
    [Fact]
    public void ManuallyImplementedScan_WithMethodChaining_WorksCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ManualScannableTable(client);
        
        // Act
        var builder = table.Scan<TestEntity>()
            .WithProjection("id, name, price")
            .Take(25)
            .UsingConsistentRead();
        
        // Assert
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("ManualScannableTable");
        request.ProjectionExpression.Should().Be("id, name, price");
        request.Limit.Should().Be(25);
        request.ConsistentRead.Should().BeTrue();
    }
    
    [Fact]
    public void ManuallyImplementedScan_WithCustomLogic_WorksCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var logger = Substitute.For<IDynamoDbLogger>();
        var options = new FluentDynamoDbOptions().WithLogger(logger);
        var table = new ManualScannableTable(client, options);
        
        // Act
        var builder = table.Scan<TestEntity>("price > {0}", 100m);
        
        // Assert
        builder.Should().NotBeNull();
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("ManualScannableTable");
        request.FilterExpression.Should().Be("price > :p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("100");
    }
    
    [Fact]
    public void ManuallyImplementedScan_FollowsSamePatternAsGeneratedCode()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var generatedTable = new TestScannableTable(client);
        var manualTable = new ManualScannableTable(client);
        
        // Act
        var generatedBuilder = generatedTable.Scan<TestEntity>("status = {0}", "ACTIVE");
        var manualBuilder = manualTable.Scan<TestEntity>("status = {0}", "ACTIVE");
        
        // Assert - Both should produce identical requests
        var generatedRequest = generatedBuilder.ToScanRequest();
        var manualRequest = manualBuilder.ToScanRequest();
        
        generatedRequest.FilterExpression.Should().Be(manualRequest.FilterExpression);
        generatedRequest.ExpressionAttributeValues.Should().BeEquivalentTo(manualRequest.ExpressionAttributeValues);
    }
    
    [Fact]
    public void ManuallyImplementedScan_WithParallelScan_WorksCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ManualScannableTable(client);
        
        // Act
        var builder = table.Scan<TestEntity>()
            .WithSegment(2, 5);
        
        // Assert
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("ManualScannableTable");
        request.Segment.Should().Be(2);
        request.TotalSegments.Should().Be(5);
    }
    
    [Fact]
    public void ManuallyImplementedScan_WithIndex_WorksCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ManualScannableTable(client);
        
        // Act
        var builder = table.Scan<TestEntity>()
            .UsingIndex("CategoryIndex");
        
        // Assert
        var request = builder.ToScanRequest();
        request.TableName.Should().Be("ManualScannableTable");
        request.IndexName.Should().Be("CategoryIndex");
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
        
        public TestScannableTable(IAmazonDynamoDB client, FluentDynamoDbOptions options) 
            : base(client, "TestScannableTable", options)
        {
        }
        
        /// <summary>
        /// Simulates the generated parameterless Scan() method.
        /// </summary>
        public new ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class => 
            new ScanRequestBuilder<TEntity>(DynamoDbClient, Logger).ForTable(Name);
        
        /// <summary>
        /// Simulates the generated expression-based Scan() method.
        /// </summary>
        public ScanRequestBuilder<TEntity> Scan<TEntity>(string filterExpression, params object[] values) where TEntity : class
        {
            var builder = Scan<TEntity>();
            return Oproto.FluentDynamoDb.Requests.Extensions.WithFilterExpressionExtensions.WithFilter(builder, filterExpression, values);
        }
    }
    
    /// <summary>
    /// Test table with manually implemented Scan() methods.
    /// This demonstrates how developers can manually implement scan operations
    /// without using source generation or the [Scannable] attribute.
    /// </summary>
    private class ManualScannableTable : DynamoDbTableBase
    {
        public ManualScannableTable(IAmazonDynamoDB client) 
            : base(client, "ManualScannableTable")
        {
        }
        
        public ManualScannableTable(IAmazonDynamoDB client, FluentDynamoDbOptions options) 
            : base(client, "ManualScannableTable", options)
        {
        }
        
        /// <summary>
        /// Manually implemented parameterless Scan() method.
        /// Creates a new Scan operation builder for this table.
        /// </summary>
        /// <returns>A ScanRequestBuilder configured for this table.</returns>
        public new ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class => 
            new ScanRequestBuilder<TEntity>(DynamoDbClient, Logger).ForTable(Name);
        
        /// <summary>
        /// Manually implemented expression-based Scan() method.
        /// Creates a new Scan operation builder with a filter expression.
        /// </summary>
        /// <param name="filterExpression">The filter expression with format placeholders.</param>
        /// <param name="values">The values to substitute into the expression.</param>
        /// <returns>A ScanRequestBuilder configured with the filter.</returns>
        public ScanRequestBuilder<TEntity> Scan<TEntity>(string filterExpression, params object[] values) where TEntity : class
        {
            var builder = Scan<TEntity>();
            return Oproto.FluentDynamoDb.Requests.Extensions.WithFilterExpressionExtensions.WithFilter(builder, filterExpression, values);
        }
    }
    
    #endregion
}
