using Amazon.DynamoDBv2;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

public class ScannableDynamoDbTableTests
{
    public class TestTable(IAmazonDynamoDB client) : DynamoDbTableBase(client, "TestTable")
    {
        public DynamoDbIndex Gsi1 => new DynamoDbIndex(this, "gsi1");
    }

    [Fact]
    public void AsScannable_ReturnsScannableInterface()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        
        var scannable = table.AsScannable();
        
        scannable.Should().NotBeNull();
        scannable.Should().BeAssignableTo<IScannableDynamoDbTable>();
    }

    [Fact]
    public void AsScannable_PreservesTableProperties()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        
        var scannable = table.AsScannable();
        
        scannable.Name.Should().Be("TestTable");
        scannable.DynamoDbClient.Should().Be(mockClient);
    }

    [Fact]
    public void AsScannable_UnderlyingTable_ReturnsOriginalTable()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        
        var scannable = table.AsScannable();
        
        scannable.UnderlyingTable.Should().Be(table);
    }

    [Fact]
    public void ScannableTable_Scan_ReturnsConfiguredScanBuilder()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        var scannable = table.AsScannable();
        
        var scanBuilder = scannable.Scan;
        
        scanBuilder.Should().NotBeNull();
        var req = scanBuilder.ToScanRequest();
        req.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void ScannableTable_Scan_ReturnsNewInstanceEachTime()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        var scannable = table.AsScannable();
        
        var scan1 = scannable.Scan;
        var scan2 = scannable.Scan;
        
        scan1.Should().NotBeSameAs(scan2);
    }

    [Fact]
    public void ScannableTable_PassesThroughAllCoreOperations()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        var scannable = table.AsScannable();
        
        // Test that all core operations are available and properly configured
        scannable.Get.Should().NotBeNull();
        scannable.Get.ToGetItemRequest().TableName.Should().Be("TestTable");
        
        scannable.Put.Should().NotBeNull();
        scannable.Put.ToPutItemRequest().TableName.Should().Be("TestTable");
        
        scannable.Update.Should().NotBeNull();
        scannable.Update.ToUpdateItemRequest().TableName.Should().Be("TestTable");
        
        scannable.Query.Should().NotBeNull();
        scannable.Query.ToQueryRequest().TableName.Should().Be("TestTable");
        
        scannable.Delete.Should().NotBeNull();
        scannable.Delete.ToDeleteItemRequest().TableName.Should().Be("TestTable");
    }

    [Fact]
    public void ScannableTable_CoreOperations_ReturnNewInstancesEachTime()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        var scannable = table.AsScannable();
        
        // Verify each operation returns new instances
        var get1 = scannable.Get;
        var get2 = scannable.Get;
        get1.Should().NotBeSameAs(get2);
        
        var put1 = scannable.Put;
        var put2 = scannable.Put;
        put1.Should().NotBeSameAs(put2);
        
        var update1 = scannable.Update;
        var update2 = scannable.Update;
        update1.Should().NotBeSameAs(update2);
        
        var query1 = scannable.Query;
        var query2 = scannable.Query;
        query1.Should().NotBeSameAs(query2);
        
        var delete1 = scannable.Delete;
        var delete2 = scannable.Delete;
        delete1.Should().NotBeSameAs(delete2);
    }

    [Fact]
    public void RegularTable_DoesNotExposeScanDirectly()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        
        // Verify that the regular table does not have a Scan property
        var tableType = typeof(TestTable);
        var scanProperty = tableType.GetProperty("Scan");
        scanProperty.Should().BeNull();
        
        // Verify that IDynamoDbTable interface does not include Scan
        var interfaceType = typeof(IDynamoDbTable);
        var interfaceScanProperty = interfaceType.GetProperty("Scan");
        interfaceScanProperty.Should().BeNull();
    }

    [Fact]
    public void ScannableTable_ImplementsIntentionalFriction()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        
        // Verify that scan operations require explicit call to AsScannable()
        // This test demonstrates the intentional friction pattern
        var scannable = table.AsScannable();
        var scanBuilder = scannable.Scan;
        
        scanBuilder.Should().NotBeNull();
        scanBuilder.ToScanRequest().TableName.Should().Be("TestTable");
    }

    [Fact]
    public void ScannableTable_AllowsAccessToCustomTableProperties()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        var scannable = table.AsScannable();
        
        // Access custom properties through UnderlyingTable
        var customTable = (TestTable)scannable.UnderlyingTable;
        customTable.Gsi1.Should().NotBeNull();
        customTable.Gsi1.Query.ToQueryRequest().IndexName.Should().Be("gsi1");
    }

    [Fact]
    public void ScannableTable_ScanWithComplexConfiguration()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        var scannable = table.AsScannable();
        
        var scanBuilder = scannable.Scan
            .WithFilter("#status = :status")
            .WithProjection("#pk, #sk")
            .UsingIndex("gsi1")
            .Take(10)
            .WithSegment(0, 4)
            .WithAttribute("#status", "status")
            .WithValue(":status", "active");
        
        var req = scanBuilder.ToScanRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
        req.FilterExpression.Should().Be("#status = :status");
        req.ProjectionExpression.Should().Be("#pk, #sk");
        req.IndexName.Should().Be("gsi1");
        req.Limit.Should().Be(10);
        req.Segment.Should().Be(0);
        req.TotalSegments.Should().Be(4);
        req.ExpressionAttributeNames["#status"].Should().Be("status");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void ScannableTable_MultipleScannableInstances_AreIndependent()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestTable(mockClient);
        
        var scannable1 = table.AsScannable();
        var scannable2 = table.AsScannable();
        
        scannable1.Should().NotBeSameAs(scannable2);
        scannable1.UnderlyingTable.Should().Be(scannable2.UnderlyingTable); // Same underlying table
        scannable1.Name.Should().Be(scannable2.Name);
        scannable1.DynamoDbClient.Should().Be(scannable2.DynamoDbClient);
    }
}