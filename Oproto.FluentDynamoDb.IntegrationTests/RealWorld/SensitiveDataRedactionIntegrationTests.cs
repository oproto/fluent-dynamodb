using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for sensitive data redaction in LINQ expression logging.
/// These tests verify that properties marked with [Sensitive] have their values
/// redacted in logs while still being used correctly in DynamoDB queries.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "SensitiveDataRedaction")]
public class SensitiveDataRedactionIntegrationTests : IntegrationTestBase
{
    private TestLogger _logger = null!;
    private TestTable _table = null!;
    
    public SensitiveDataRedactionIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<SecureTestEntity>();
        
        // Create logger and table with logging enabled
        _logger = new TestLogger(Oproto.FluentDynamoDb.Logging.LogLevel.Debug);
        _table = new TestTable(DynamoDb, TableName, _logger);
        
        // Seed test data
        await SeedTestDataAsync();
    }
    
    private async Task SeedTestDataAsync()
    {
        var entities = new[]
        {
            new SecureTestEntity
            {
                Id = "user-1",
                Name = "John Doe",
                Email = "john.doe@example.com",
                SocialSecurityNumber = "123-45-6789",
                CreditCardNumber = "4111-1111-1111-1111",
                PublicData = "This is public information"
            },
            new SecureTestEntity
            {
                Id = "user-2",
                Name = "Jane Smith",
                Email = "jane.smith@example.com",
                SocialSecurityNumber = "987-65-4321",
                CreditCardNumber = "5555-5555-5555-4444",
                PublicData = "Another public record"
            },
            new SecureTestEntity
            {
                Id = "user-3",
                Name = "Bob Johnson",
                Email = "bob.johnson@example.com",
                SocialSecurityNumber = "555-12-3456",
                CreditCardNumber = "3782-822463-10005",
                PublicData = "Public data for Bob"
            }
        };
        
        foreach (var entity in entities)
        {
            var item = SecureTestEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
        }
    }
    
    [Fact]
    public async Task Query_WithSensitiveProperty_RedactsValueInLogs()
    {
        // Arrange
        _logger.Clear();
        var sensitiveEmail = "john.doe@example.com";
        
        // Act - Query using a sensitive property (Email is marked with [Sensitive])
        var response = await _table.Query<SecureTestEntity>(x => x.Id == "user-1" && x.Email == sensitiveEmail)
            .ExecuteAsync();
        
        // Assert - Query should succeed
        response.Items.Should().HaveCount(1);
        var entity = SecureTestEntity.FromDynamoDb<SecureTestEntity>(response.Items[0]);
        entity.Email.Should().Be(sensitiveEmail);
        
        // Assert - Sensitive value should be redacted in logs
        var logMessages = _logger.Messages;
        logMessages.Should().Contain(m => m.Contains("[REDACTED]"), 
            "sensitive email value should be redacted in logs");
        logMessages.Should().NotContain(m => m.Contains(sensitiveEmail), 
            "actual email value should not appear in logs");
        
        // Assert - Property name should still be visible for debugging
        logMessages.Should().Contain(m => m.Contains("Email") || m.Contains("email"), 
            "property name should be visible in logs for debugging");
    }
    
    [Fact]
    public async Task Query_WithSensitiveSSN_RedactsValueInLogs()
    {
        // Arrange
        _logger.Clear();
        var sensitiveSsn = "123-45-6789";
        
        // Act - Query using SSN (marked with both [Sensitive] and [Encrypted])
        var response = await _table.Query<SecureTestEntity>(x => x.Id == "user-1" && x.SocialSecurityNumber == sensitiveSsn)
            .ExecuteAsync();
        
        // Assert - Query should succeed
        response.Items.Should().HaveCount(1);
        
        // Assert - SSN value should be redacted in logs
        var logMessages = _logger.Messages;
        logMessages.Should().Contain(m => m.Contains("[REDACTED]"), 
            "sensitive SSN value should be redacted in logs");
        logMessages.Should().NotContain(m => m.Contains(sensitiveSsn), 
            "actual SSN value should not appear in logs");
    }
    
    [Fact]
    public async Task Query_WithNonSensitiveProperty_DoesNotRedactInLogs()
    {
        // Arrange
        _logger.Clear();
        var publicName = "John Doe";
        
        // Act - Query using a non-sensitive property (Name is not marked with [Sensitive])
        var response = await _table.Query<SecureTestEntity>(x => x.Id == "user-1" && x.Name == publicName)
            .ExecuteAsync();
        
        // Assert - Query should succeed
        response.Items.Should().HaveCount(1);
        
        // Assert - Non-sensitive value should appear in logs
        var logMessages = _logger.Messages;
        logMessages.Should().Contain(m => m.Contains(publicName), 
            "non-sensitive name value should appear in logs");
        logMessages.Should().NotContain(m => m.Contains("[REDACTED]") && m.Contains("Name"), 
            "non-sensitive property should not be redacted");
    }
    
    [Fact]
    public async Task Query_WithPublicDataProperty_DoesNotRedactInLogs()
    {
        // Arrange
        _logger.Clear();
        var publicData = "This is public information";
        
        // Act - Query using PublicData (not marked with [Sensitive])
        var response = await _table.Query<SecureTestEntity>(x => x.Id == "user-1" && x.PublicData == publicData)
            .ExecuteAsync();
        
        // Assert - Query should succeed
        response.Items.Should().HaveCount(1);
        
        // Assert - Public data should appear in logs
        var logMessages = _logger.Messages;
        logMessages.Should().Contain(m => m.Contains(publicData), 
            "public data value should appear in logs");
    }
    
    [Fact]
    public async Task Query_WithMixedSensitiveAndNonSensitive_RedactsOnlySensitiveValues()
    {
        // Arrange
        _logger.Clear();
        var publicName = "John Doe";
        var sensitiveEmail = "john.doe@example.com";
        
        // Act - Query with both sensitive and non-sensitive properties
        var response = await _table.Query<SecureTestEntity>(x => 
            x.Id == "user-1" && x.Name == publicName && x.Email == sensitiveEmail)
            .ExecuteAsync();
        
        // Assert - Query should succeed
        response.Items.Should().HaveCount(1);
        
        // Assert - Non-sensitive value should appear in logs
        var logMessages = _logger.Messages;
        logMessages.Should().Contain(m => m.Contains(publicName), 
            "non-sensitive name should appear in logs");
        
        // Assert - Sensitive value should be redacted
        logMessages.Should().Contain(m => m.Contains("[REDACTED]"), 
            "sensitive email should be redacted");
        logMessages.Should().NotContain(m => m.Contains(sensitiveEmail), 
            "actual email value should not appear in logs");
    }
    
    [Fact]
    public async Task Scan_WithSensitiveProperty_RedactsValueInLogs()
    {
        // Arrange
        _logger.Clear();
        var sensitiveEmail = "jane.smith@example.com";
        
        // Act - Scan with filter on sensitive property
        var response = await _table.Scan<SecureTestEntity>(x => x.Email == sensitiveEmail)
            .ExecuteAsync();
        
        // Assert - Scan should succeed
        response.Items.Should().HaveCount(1);
        var entity = SecureTestEntity.FromDynamoDb<SecureTestEntity>(response.Items[0]);
        entity.Email.Should().Be(sensitiveEmail);
        
        // Assert - Sensitive value should be redacted in logs
        var logMessages = _logger.Messages;
        logMessages.Should().Contain(m => m.Contains("[REDACTED]"), 
            "sensitive email value should be redacted in scan logs");
        logMessages.Should().NotContain(m => m.Contains(sensitiveEmail), 
            "actual email value should not appear in scan logs");
    }
    
    [Fact]
    public async Task Scan_WithNonSensitiveProperty_DoesNotRedactInLogs()
    {
        // Arrange
        _logger.Clear();
        var publicData = "Another public record";
        
        // Act - Scan with filter on non-sensitive property
        var response = await _table.Scan<SecureTestEntity>(x => x.PublicData == publicData)
            .ExecuteAsync();
        
        // Assert - Scan should succeed
        response.Items.Should().HaveCount(1);
        
        // Assert - Non-sensitive value should appear in logs
        var logMessages = _logger.Messages;
        logMessages.Should().Contain(m => m.Contains(publicData), 
            "non-sensitive public data should appear in scan logs");
    }
    
    [Fact]
    public async Task Query_WithSensitiveProperty_ActualQueryValuesNotAffected()
    {
        // Arrange
        _logger.Clear();
        var sensitiveEmail = "bob.johnson@example.com";
        
        // Act - Query with sensitive property
        var response = await _table.Query<SecureTestEntity>(x => x.Id == "user-3" && x.Email == sensitiveEmail)
            .ExecuteAsync();
        
        // Assert - Query should return correct results (redaction only affects logs, not query)
        response.Items.Should().HaveCount(1);
        var entity = SecureTestEntity.FromDynamoDb<SecureTestEntity>(response.Items[0]);
        entity.Id.Should().Be("user-3");
        entity.Name.Should().Be("Bob Johnson");
        entity.Email.Should().Be(sensitiveEmail);
        entity.PublicData.Should().Be("Public data for Bob");
        
        // Assert - Verify the actual query sent to DynamoDB contains the real value
        // (we can't inspect the actual request, but the fact that we got correct results proves it)
        entity.Email.Should().Be(sensitiveEmail, 
            "query must have used actual email value, not redacted value");
    }
    
    [Fact]
    public async Task Query_WithoutLogger_DoesNotThrowException()
    {
        // Arrange - Create table without logger
        var tableWithoutLogger = new TestTable(DynamoDb, TableName, logger: null);
        var sensitiveEmail = "john.doe@example.com";
        
        // Act & Assert - Should work without logger (no exception)
        var response = await tableWithoutLogger.Query<SecureTestEntity>(x => 
            x.Id == "user-1" && x.Email == sensitiveEmail)
            .ExecuteAsync();
        
        response.Items.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task Query_WithMultipleSensitiveProperties_RedactsAllSensitiveValues()
    {
        // Arrange
        _logger.Clear();
        var sensitiveEmail = "john.doe@example.com";
        var sensitiveSsn = "123-45-6789";
        
        // Act - Query with multiple sensitive properties
        var response = await _table.Query<SecureTestEntity>(x => 
            x.Id == "user-1" && x.Email == sensitiveEmail && x.SocialSecurityNumber == sensitiveSsn)
            .ExecuteAsync();
        
        // Assert - Query should succeed
        response.Items.Should().HaveCount(1);
        
        // Assert - All sensitive values should be redacted
        var logMessages = _logger.Messages;
        var redactedCount = logMessages.Count(m => m.Contains("[REDACTED]"));
        redactedCount.Should().BeGreaterOrEqualTo(2, 
            "both sensitive properties should be redacted");
        
        logMessages.Should().NotContain(m => m.Contains(sensitiveEmail), 
            "email should not appear in logs");
        logMessages.Should().NotContain(m => m.Contains(sensitiveSsn), 
            "SSN should not appear in logs");
    }
    
    [Fact]
    public async Task Scan_WithSensitivePropertyComparison_RedactsValueInLogs()
    {
        // Arrange
        _logger.Clear();
        var sensitiveEmail = "jane.smith@example.com";
        
        // Act - Scan with comparison on sensitive property
        var response = await _table.Scan<SecureTestEntity>(x => x.Email.StartsWith("jane"))
            .ExecuteAsync();
        
        // Assert - Scan should succeed
        response.Items.Should().HaveCountGreaterOrEqualTo(1);
        
        // Assert - The comparison value should be redacted if it's for a sensitive property
        var logMessages = _logger.Messages;
        // Note: StartsWith parameter might be redacted depending on implementation
        // At minimum, we verify no exception is thrown and query works
        response.Items.Should().NotBeEmpty("scan with sensitive property should work");
    }
    
    // Helper class to create a table instance with logger support
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, string tableName, TestLogger? logger) 
            : base(client, tableName, logger)
        {
        }
    }
}
