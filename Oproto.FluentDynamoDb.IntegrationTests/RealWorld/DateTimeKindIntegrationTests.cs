using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for DateTime Kind preservation during serialization and deserialization.
/// These tests verify that DateTimeKind specified in DynamoDbAttribute is correctly preserved
/// during round-trip operations with DynamoDB Local.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "DateTimeKind")]
public class DateTimeKindIntegrationTests : IntegrationTestBase
{
    public DateTimeKindIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateTableAsync<DateTimeKindEntity>();
    }
    
    [Fact]
    public async Task SaveAndLoad_WithUtcKind_PreservesUtcKind()
    {
        // Arrange - Create entity with UTC timestamp
        var utcNow = DateTime.UtcNow;
        var entity = new DateTimeKindEntity
        {
            Id = "test-utc-1",
            UtcTimestamp = utcNow
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify UTC kind is preserved
        loaded.UtcTimestamp.Should().NotBeNull();
        loaded.UtcTimestamp!.Value.Kind.Should().Be(DateTimeKind.Utc);
        loaded.UtcTimestamp.Value.Should().BeCloseTo(utcNow, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public async Task SaveAndLoad_WithLocalKind_PreservesLocalKind()
    {
        // Arrange - Create entity with Local timestamp
        var localNow = DateTime.Now;
        var entity = new DateTimeKindEntity
        {
            Id = "test-local-1",
            LocalTimestamp = localNow
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify Local kind is preserved
        loaded.LocalTimestamp.Should().NotBeNull();
        loaded.LocalTimestamp!.Value.Kind.Should().Be(DateTimeKind.Local);
        loaded.LocalTimestamp.Value.Should().BeCloseTo(localNow, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public async Task SaveAndLoad_WithUnspecifiedKind_PreservesUnspecifiedKind()
    {
        // Arrange - Create entity with Unspecified timestamp
        var unspecifiedTime = new DateTime(2024, 11, 9, 12, 30, 45, DateTimeKind.Unspecified);
        var entity = new DateTimeKindEntity
        {
            Id = "test-unspecified-1",
            UnspecifiedTimestamp = unspecifiedTime
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify Unspecified kind is preserved
        loaded.UnspecifiedTimestamp.Should().NotBeNull();
        loaded.UnspecifiedTimestamp!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
        loaded.UnspecifiedTimestamp.Value.Should().Be(unspecifiedTime);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithDefaultKind_UsesUnspecifiedKind()
    {
        // Arrange - Create entity with default timestamp (no DateTimeKind specified)
        var defaultTime = new DateTime(2024, 11, 9, 14, 15, 30, DateTimeKind.Utc);
        var entity = new DateTimeKindEntity
        {
            Id = "test-default-1",
            DefaultTimestamp = defaultTime
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify default behavior (should be Unspecified)
        loaded.DefaultTimestamp.Should().NotBeNull();
        loaded.DefaultTimestamp!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithUtcKindAndFormat_PreservesKindAndAppliesFormat()
    {
        // Arrange - Create entity with UTC date and format string
        var utcDate = new DateTime(2024, 11, 9, 10, 30, 45, DateTimeKind.Utc);
        var entity = new DateTimeKindEntity
        {
            Id = "test-utc-format-1",
            UtcDate = utcDate
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify UTC kind is preserved and format is applied (date only)
        loaded.UtcDate.Should().NotBeNull();
        loaded.UtcDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
        loaded.UtcDate.Value.Date.Should().Be(utcDate.Date);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithLocalKindAndFormat_PreservesKindAndAppliesFormat()
    {
        // Arrange - Create entity with Local datetime and format string
        var localDateTime = new DateTime(2024, 11, 9, 15, 45, 30, DateTimeKind.Local);
        var entity = new DateTimeKindEntity
        {
            Id = "test-local-format-1",
            LocalDateTime = localDateTime
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify Local kind is preserved and format is applied
        loaded.LocalDateTime.Should().NotBeNull();
        loaded.LocalDateTime!.Value.Kind.Should().Be(DateTimeKind.Local);
        // Format "yyyy-MM-ddTHH:mm:ss" preserves date and time but not milliseconds
        loaded.LocalDateTime.Value.Should().BeCloseTo(localDateTime, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public async Task SaveAndLoad_WithMultipleKinds_PreservesAllKinds()
    {
        // Arrange - Create entity with multiple DateTime properties with different kinds
        var utcTime = DateTime.UtcNow;
        var localTime = DateTime.Now;
        var unspecifiedTime = new DateTime(2024, 11, 9, 12, 0, 0, DateTimeKind.Unspecified);
        
        var entity = new DateTimeKindEntity
        {
            Id = "test-multiple-1",
            UtcTimestamp = utcTime,
            LocalTimestamp = localTime,
            UnspecifiedTimestamp = unspecifiedTime,
            DefaultTimestamp = new DateTime(2024, 11, 9, 13, 0, 0)
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify all kinds are preserved correctly
        loaded.UtcTimestamp.Should().NotBeNull();
        loaded.UtcTimestamp!.Value.Kind.Should().Be(DateTimeKind.Utc);
        
        loaded.LocalTimestamp.Should().NotBeNull();
        loaded.LocalTimestamp!.Value.Kind.Should().Be(DateTimeKind.Local);
        
        loaded.UnspecifiedTimestamp.Should().NotBeNull();
        loaded.UnspecifiedTimestamp!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
        
        loaded.DefaultTimestamp.Should().NotBeNull();
        loaded.DefaultTimestamp!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }
    
    [Fact]
    public async Task SaveAndLoad_UtcConversion_ConvertsToUtcBeforeSaving()
    {
        // Arrange - Create entity with Local time that should be converted to UTC
        var localTime = new DateTime(2024, 11, 9, 15, 30, 45, DateTimeKind.Local);
        var expectedUtc = localTime.ToUniversalTime();
        
        var entity = new DateTimeKindEntity
        {
            Id = "test-utc-conversion-1",
            UtcTimestamp = localTime // Passing Local time to UTC property
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify time was converted to UTC
        loaded.UtcTimestamp.Should().NotBeNull();
        loaded.UtcTimestamp!.Value.Kind.Should().Be(DateTimeKind.Utc);
        loaded.UtcTimestamp.Value.Should().BeCloseTo(expectedUtc, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public async Task SaveAndLoad_LocalConversion_ConvertsToLocalBeforeSaving()
    {
        // Arrange - Create entity with UTC time that should be converted to Local
        var utcTime = new DateTime(2024, 11, 9, 20, 30, 45, DateTimeKind.Utc);
        var expectedLocal = utcTime.ToLocalTime();
        
        var entity = new DateTimeKindEntity
        {
            Id = "test-local-conversion-1",
            LocalTimestamp = utcTime // Passing UTC time to Local property
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify time was converted to Local
        loaded.LocalTimestamp.Should().NotBeNull();
        loaded.LocalTimestamp!.Value.Kind.Should().Be(DateTimeKind.Local);
        loaded.LocalTimestamp.Value.Should().BeCloseTo(expectedLocal, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public async Task SaveAndLoad_WithNullDateTime_HandlesNullCorrectly()
    {
        // Arrange - Create entity with null DateTime values
        var entity = new DateTimeKindEntity
        {
            Id = "test-null-1",
            UtcTimestamp = null,
            LocalTimestamp = null,
            UnspecifiedTimestamp = null,
            DefaultTimestamp = null
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify null values are preserved
        loaded.UtcTimestamp.Should().BeNull();
        loaded.LocalTimestamp.Should().BeNull();
        loaded.UnspecifiedTimestamp.Should().BeNull();
        loaded.DefaultTimestamp.Should().BeNull();
    }
}
