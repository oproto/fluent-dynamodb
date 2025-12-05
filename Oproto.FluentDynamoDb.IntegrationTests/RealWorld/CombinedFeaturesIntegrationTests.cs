using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for combined features (DateTime Kind + Format Strings).
/// These tests verify that DateTime Kind and format strings work correctly together.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "CombinedFeatures")]
public class CombinedFeaturesIntegrationTests : IntegrationTestBase
{
    public CombinedFeaturesIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateTableAsync<CombinedFeaturesEntity>();
    }
    
    [Fact]
    public async Task SaveAndLoad_DateTimeKindAndFormat_WorksTogether()
    {
        // Arrange - DateTime with both Kind and Format
        var entity = new CombinedFeaturesEntity
        {
            Id = "combined-test-1",
            Type = "user",
            CreatedUtc = new DateTime(2024, 11, 9, 15, 30, 45, DateTimeKind.Local), // Will be converted to UTC
            UpdatedLocal = new DateTime(2024, 11, 9, 10, 20, 30, DateTimeKind.Utc) // Will be converted to Local
        };
        
        // Act - Save and load
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify Kind is preserved and format is applied
        loaded.CreatedUtc.Should().NotBeNull();
        loaded.CreatedUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        loaded.CreatedUtc.Value.Date.Should().Be(entity.CreatedUtc!.Value.ToUniversalTime().Date);
        
        loaded.UpdatedLocal.Should().NotBeNull();
        loaded.UpdatedLocal!.Value.Kind.Should().Be(DateTimeKind.Local);
    }
    
    [Fact]
    public async Task SaveAndLoad_DecimalWithFormat_AppliesFormatCorrectly()
    {
        // Arrange - Decimal property with format string
        var entity = new CombinedFeaturesEntity
        {
            Id = "combined-test-2",
            Type = "user",
            Name = "John Doe",
            Salary = 75000.5678m // Should be formatted to 75000.57
        };
        
        // Act - Save and load
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify format was applied
        loaded.Salary.Should().NotBeNull();
        loaded.Salary!.Value.Should().Be(75000.57m);
    }
    
    [Fact]
    public async Task SaveAndLoad_DateTimeWithKindOnly_PreservesKind()
    {
        // Arrange - DateTime with Kind but no format
        var entity = new CombinedFeaturesEntity
        {
            Id = "combined-test-3",
            Type = "user",
            Name = "Jane Smith",
            BirthDateUtc = new DateTime(1990, 5, 15, 10, 30, 0, DateTimeKind.Local) // Will be converted to UTC
        };
        
        // Act - Save and load
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify Kind is preserved
        loaded.BirthDateUtc.Should().NotBeNull();
        loaded.BirthDateUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
    
    [Fact]
    public async Task SaveAndLoad_DateTimeKindAndFormat_BothApplied()
    {
        // Arrange - Property with DateTime Kind + Format
        var entity = new CombinedFeaturesEntity
        {
            Id = "combined-test-4",
            Type = "user",
            Name = "Bob Johnson",
            LastLogin = new DateTime(2024, 11, 9, 14, 25, 30, 789, DateTimeKind.Local) // Will be converted to UTC and formatted
        };
        
        // Act - Save and load
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify both features were applied
        loaded.LastLogin.Should().NotBeNull();
        loaded.LastLogin!.Value.Kind.Should().Be(DateTimeKind.Utc);
        // Format strips milliseconds
        loaded.LastLogin.Value.Millisecond.Should().Be(0);
    }
    
    [Fact]
    public async Task SaveAndLoad_MultiplePropertiesWithDifferentFeatures_AllWorkCorrectly()
    {
        // Arrange - Entity with various combinations of features
        var entity = new CombinedFeaturesEntity
        {
            Id = "combined-test-5",
            Type = "user",
            Name = "Charlie Davis",
            CreatedUtc = new DateTime(2024, 11, 9, 10, 0, 0, DateTimeKind.Utc),
            UpdatedLocal = new DateTime(2024, 11, 9, 15, 30, 0, DateTimeKind.Local),
            Salary = 85000.9999m,
            BirthDateUtc = new DateTime(1985, 3, 20, 8, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTime(2025, 12, 31, 23, 59, 59),
            CreditLimit = 50000.123456m,
            LastLogin = new DateTime(2024, 11, 9, 14, 0, 0, DateTimeKind.Utc)
        };
        
        // Act - Save and load
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify all properties with different feature combinations
        loaded.CreatedUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        loaded.UpdatedLocal!.Value.Kind.Should().Be(DateTimeKind.Local);
        loaded.Salary!.Value.Should().Be(85001.00m); // F2 format
        loaded.BirthDateUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        loaded.ExpiryDate!.Value.Date.Should().Be(new DateTime(2025, 12, 31));
        loaded.CreditLimit!.Value.Should().Be(50000.1235m); // F4 format
        loaded.LastLogin!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
    
    [Fact]
    public async Task SaveAndLoad_DateTimeFormatOnly_AppliesFormatWithoutKind()
    {
        // Arrange - DateTime with format but no Kind specified
        var entity = new CombinedFeaturesEntity
        {
            Id = "combined-test-6",
            Type = "user",
            Name = "David Wilson",
            ExpiryDate = new DateTime(2025, 12, 31, 23, 59, 59, 999) // Time and milliseconds should be stripped
        };
        
        // Act - Save and load
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify format is applied (date only)
        loaded.ExpiryDate.Should().NotBeNull();
        loaded.ExpiryDate!.Value.Date.Should().Be(new DateTime(2025, 12, 31));
        loaded.ExpiryDate.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
    }
    
    [Fact]
    public async Task SaveAndLoad_NullValuesWithAllFeatures_HandlesCorrectly()
    {
        // Arrange - Entity with null values for properties with features
        var entity = new CombinedFeaturesEntity
        {
            Id = "combined-test-7",
            Type = "user",
            Name = "Grace Lee",
            CreatedUtc = null,
            Salary = null,
            LastLogin = null
        };
        
        // Act - Save and load
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify null values are preserved
        loaded.CreatedUtc.Should().BeNull();
        loaded.Salary.Should().BeNull();
        loaded.LastLogin.Should().BeNull();
    }
    
    [Fact]
    public async Task SaveAndLoad_EdgeCaseValues_HandlesCorrectly()
    {
        // Arrange - Entity with edge case values
        var entity = new CombinedFeaturesEntity
        {
            Id = "combined-test-8",
            Type = "user",
            Name = "Henry Taylor",
            Salary = 0.01m, // Very small value
            CreditLimit = 9999999.9999m, // Large value
            LastLogin = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) // Start of year
        };
        
        // Act - Save and load
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify edge cases are handled correctly
        loaded.Salary!.Value.Should().Be(0.01m);
        loaded.CreditLimit!.Value.Should().Be(9999999.9999m);
        loaded.LastLogin!.Value.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        loaded.LastLogin.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
