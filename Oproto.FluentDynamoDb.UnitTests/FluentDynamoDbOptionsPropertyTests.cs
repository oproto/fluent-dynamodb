using System.Collections.Concurrent;
using Amazon.DynamoDBv2;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests;

/// <summary>
/// Property-based tests for FluentDynamoDbOptions.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class FluentDynamoDbOptionsPropertyTests
{
    /// <summary>
    /// 
    /// For any FluentDynamoDbOptions instance, calling With* methods SHALL return a new instance
    /// without modifying the original instance's properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithLogger_ReturnsNewInstance_WithoutModifyingOriginal()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange
                var originalLogger = NoOpLogger.Instance;
                var newLogger = Substitute.For<IDynamoDbLogger>();
                var original = new FluentDynamoDbOptions();
                
                // Act
                var modified = original.WithLogger(newLogger);
                
                // Assert
                var originalUnchanged = ReferenceEquals(original.Logger, originalLogger);
                var newHasNewLogger = ReferenceEquals(modified.Logger, newLogger);
                var differentInstances = !ReferenceEquals(original, modified);
                
                return (originalUnchanged && newHasNewLogger && differentInstances).ToProperty()
                    .Label($"WithLogger should return new instance without modifying original. " +
                           $"OriginalUnchanged: {originalUnchanged}, NewHasNewLogger: {newHasNewLogger}, DifferentInstances: {differentInstances}");
            });
    }
    
    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithBlobStorage_ReturnsNewInstance_WithoutModifyingOriginal()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange
                var newProvider = Substitute.For<IBlobStorageProvider>();
                var original = new FluentDynamoDbOptions();
                var originalProvider = original.BlobStorageProvider;
                
                // Act
                var modified = original.WithBlobStorage(newProvider);
                
                // Assert
                var originalUnchanged = original.BlobStorageProvider == originalProvider;
                var newHasNewProvider = ReferenceEquals(modified.BlobStorageProvider, newProvider);
                var differentInstances = !ReferenceEquals(original, modified);
                
                return (originalUnchanged && newHasNewProvider && differentInstances).ToProperty()
                    .Label($"WithBlobStorage should return new instance without modifying original. " +
                           $"OriginalUnchanged: {originalUnchanged}, NewHasNewProvider: {newHasNewProvider}, DifferentInstances: {differentInstances}");
            });
    }
    
    /// <summary>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithEncryption_ReturnsNewInstance_WithoutModifyingOriginal()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange
                var newEncryptor = Substitute.For<IFieldEncryptor>();
                var original = new FluentDynamoDbOptions();
                var originalEncryptor = original.FieldEncryptor;
                
                // Act
                var modified = original.WithEncryption(newEncryptor);
                
                // Assert
                var originalUnchanged = original.FieldEncryptor == originalEncryptor;
                var newHasNewEncryptor = ReferenceEquals(modified.FieldEncryptor, newEncryptor);
                var differentInstances = !ReferenceEquals(original, modified);
                
                return (originalUnchanged && newHasNewEncryptor && differentInstances).ToProperty()
                    .Label($"WithEncryption should return new instance without modifying original. " +
                           $"OriginalUnchanged: {originalUnchanged}, NewHasNewEncryptor: {newHasNewEncryptor}, DifferentInstances: {differentInstances}");
            });
    }
    
    /// <summary>
    /// 
    /// Chaining multiple With* calls should preserve all previously set values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChainedWithMethods_PreserveAllValues()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange
                var logger = Substitute.For<IDynamoDbLogger>();
                var blobProvider = Substitute.For<IBlobStorageProvider>();
                var encryptor = Substitute.For<IFieldEncryptor>();
                
                // Act - chain all With* methods
                var options = new FluentDynamoDbOptions()
                    .WithLogger(logger)
                    .WithBlobStorage(blobProvider)
                    .WithEncryption(encryptor);
                
                // Assert - all values should be preserved
                var hasLogger = ReferenceEquals(options.Logger, logger);
                var hasBlobProvider = ReferenceEquals(options.BlobStorageProvider, blobProvider);
                var hasEncryptor = ReferenceEquals(options.FieldEncryptor, encryptor);
                
                return (hasLogger && hasBlobProvider && hasEncryptor).ToProperty()
                    .Label($"Chained With* methods should preserve all values. " +
                           $"HasLogger: {hasLogger}, HasBlobProvider: {hasBlobProvider}, HasEncryptor: {hasEncryptor}");
            });
    }
    
    /// <summary>
    /// 
    /// Calling With* methods in any order should produce equivalent results.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithMethods_OrderIndependent()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange
                var logger = Substitute.For<IDynamoDbLogger>();
                var blobProvider = Substitute.For<IBlobStorageProvider>();
                var encryptor = Substitute.For<IFieldEncryptor>();
                
                // Act - create options in different orders
                var options1 = new FluentDynamoDbOptions()
                    .WithLogger(logger)
                    .WithBlobStorage(blobProvider)
                    .WithEncryption(encryptor);
                
                var options2 = new FluentDynamoDbOptions()
                    .WithEncryption(encryptor)
                    .WithLogger(logger)
                    .WithBlobStorage(blobProvider);
                
                var options3 = new FluentDynamoDbOptions()
                    .WithBlobStorage(blobProvider)
                    .WithEncryption(encryptor)
                    .WithLogger(logger);
                
                // Assert - all should have the same values
                var allHaveSameLogger = 
                    ReferenceEquals(options1.Logger, logger) &&
                    ReferenceEquals(options2.Logger, logger) &&
                    ReferenceEquals(options3.Logger, logger);
                
                var allHaveSameBlobProvider = 
                    ReferenceEquals(options1.BlobStorageProvider, blobProvider) &&
                    ReferenceEquals(options2.BlobStorageProvider, blobProvider) &&
                    ReferenceEquals(options3.BlobStorageProvider, blobProvider);
                
                var allHaveSameEncryptor = 
                    ReferenceEquals(options1.FieldEncryptor, encryptor) &&
                    ReferenceEquals(options2.FieldEncryptor, encryptor) &&
                    ReferenceEquals(options3.FieldEncryptor, encryptor);
                
                return (allHaveSameLogger && allHaveSameBlobProvider && allHaveSameEncryptor).ToProperty()
                    .Label($"With* methods should be order-independent. " +
                           $"AllHaveSameLogger: {allHaveSameLogger}, AllHaveSameBlobProvider: {allHaveSameBlobProvider}, AllHaveSameEncryptor: {allHaveSameEncryptor}");
            });
    }
    
    /// <summary>
    /// 
    /// WithLogger(null) should use NoOpLogger.Instance as default.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithLogger_Null_UsesNoOpLogger()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange
                var original = new FluentDynamoDbOptions();
                
                // Act
                var modified = original.WithLogger(null);
                
                // Assert
                var usesNoOpLogger = ReferenceEquals(modified.Logger, NoOpLogger.Instance);
                
                return usesNoOpLogger.ToProperty()
                    .Label($"WithLogger(null) should use NoOpLogger.Instance. UsesNoOpLogger: {usesNoOpLogger}");
            });
    }
    
    /// <summary>
    /// 
    /// For any FluentDynamoDbOptions with a configured logger, and for any request builder
    /// created from a table using those options, the configured logger SHALL be used for logging operations.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LoggerPropagation_TableToRequestBuilders()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange
                var mockLogger = Substitute.For<IDynamoDbLogger>();
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var options = new FluentDynamoDbOptions().WithLogger(mockLogger);
                
                // Act - create a table with the options
                var table = new TestTableForLoggerPropagation(mockClient, tableName, options);
                
                // Assert - verify the table has the correct logger
                var tableHasCorrectLogger = table.GetLoggerForTest() == mockLogger;
                
                // Verify request builders receive the logger by checking they can be created
                // The logger is passed to request builders in the table's Query, Get, Update, etc. methods
                var queryBuilder = table.Query<TestEntity>();
                var getBuilder = table.Get<TestEntity>();
                var updateBuilder = table.Update<TestEntity>();
                var deleteBuilder = table.Delete<TestEntity>();
                var putBuilder = table.Put<TestEntity>();
                var scanBuilder = table.Scan<TestEntity>();
                
                // All builders should be created successfully (they receive the logger in their constructors)
                var allBuildersCreated = queryBuilder != null && getBuilder != null && 
                                         updateBuilder != null && deleteBuilder != null && 
                                         putBuilder != null && scanBuilder != null;
                
                return (tableHasCorrectLogger && allBuildersCreated).ToProperty()
                    .Label($"Logger should propagate from options to table and request builders. " +
                           $"TableHasCorrectLogger: {tableHasCorrectLogger}, AllBuildersCreated: {allBuildersCreated}");
            });
    }
    
    /// <summary>
    /// 
    /// For any table created with default options (no logger configured),
    /// the NoOpLogger.Instance SHALL be used.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LoggerPropagation_DefaultOptions_UsesNoOpLogger()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                
                // Act - create a table with default options (no logger)
                var table = new TestTableForLoggerPropagation(mockClient, tableName, null);
                
                // Assert - verify the table uses NoOpLogger
                var usesNoOpLogger = table.GetLoggerForTest() == NoOpLogger.Instance;
                
                return usesNoOpLogger.ToProperty()
                    .Label($"Table with default options should use NoOpLogger.Instance. UsesNoOpLogger: {usesNoOpLogger}");
            });
    }
    
    /// <summary>
    /// 
    /// For any table created with new FluentDynamoDbOptions() (explicit default options),
    /// the NoOpLogger.Instance SHALL be used.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LoggerPropagation_ExplicitDefaultOptions_UsesNoOpLogger()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var options = new FluentDynamoDbOptions(); // Explicit default options
                
                // Act - create a table with explicit default options
                var table = new TestTableForLoggerPropagation(mockClient, tableName, options);
                
                // Assert - verify the table uses NoOpLogger
                var usesNoOpLogger = table.GetLoggerForTest() == NoOpLogger.Instance;
                
                return usesNoOpLogger.ToProperty()
                    .Label($"Table with explicit default options should use NoOpLogger.Instance. UsesNoOpLogger: {usesNoOpLogger}");
            });
    }
}

/// <summary>
/// Test table class for verifying logger propagation.
/// </summary>
internal class TestTableForLoggerPropagation : DynamoDbTableBase
{
    public TestTableForLoggerPropagation(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)
        : base(client, tableName, options)
    {
    }
    
    /// <summary>
    /// Exposes the logger for testing purposes.
    /// </summary>
    public IDynamoDbLogger GetLoggerForTest() => Logger;
    
    /// <summary>
    /// Creates a new Scan operation builder for this table.
    /// Added for testing purposes to verify Scan builder creation.
    /// </summary>
    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class =>
        new ScanRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);
}

/// <summary>
/// Simple test entity for property tests.
/// </summary>
internal class TestEntity
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Property-based tests for configuration isolation.
/// Validates that table instances maintain independent configurations.
/// </summary>
public class ConfigurationIsolationPropertyTests
{
    /// <summary>
    /// 
    /// For any two table instances created with different FluentDynamoDbOptions,
    /// the configuration of one table SHALL NOT affect the configuration of the other table.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TwoTables_WithDifferentOptions_MaintainIsolatedConfigurations()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            (tableName1, tableName2) =>
            {
                // Arrange - create two distinct loggers
                var logger1 = Substitute.For<IDynamoDbLogger>();
                var logger2 = Substitute.For<IDynamoDbLogger>();
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                
                var options1 = new FluentDynamoDbOptions().WithLogger(logger1);
                var options2 = new FluentDynamoDbOptions().WithLogger(logger2);
                
                // Act - create two tables with different options
                var table1 = new TestTableForIsolation(mockClient, tableName1, options1);
                var table2 = new TestTableForIsolation(mockClient, tableName2, options2);
                
                // Assert - each table should have its own logger
                var table1HasLogger1 = ReferenceEquals(table1.GetLoggerForTest(), logger1);
                var table2HasLogger2 = ReferenceEquals(table2.GetLoggerForTest(), logger2);
                var loggersAreDifferent = !ReferenceEquals(table1.GetLoggerForTest(), table2.GetLoggerForTest());
                
                return (table1HasLogger1 && table2HasLogger2 && loggersAreDifferent).ToProperty()
                    .Label($"Tables should maintain isolated configurations. " +
                           $"Table1HasLogger1: {table1HasLogger1}, Table2HasLogger2: {table2HasLogger2}, LoggersAreDifferent: {loggersAreDifferent}");
            });
    }
    
    /// <summary>
    /// 
    /// For any two table instances created with different FluentDynamoDbOptions,
    /// modifying one options instance after table creation SHALL NOT affect the other table.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ModifyingOptions_AfterTableCreation_DoesNotAffectOtherTables()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange
                var originalLogger = Substitute.For<IDynamoDbLogger>();
                var newLogger = Substitute.For<IDynamoDbLogger>();
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                
                var options = new FluentDynamoDbOptions().WithLogger(originalLogger);
                
                // Act - create table, then create new options with different logger
                var table = new TestTableForIsolation(mockClient, tableName, options);
                var modifiedOptions = options.WithLogger(newLogger);
                
                // Assert - table should still have original logger (immutability)
                var tableHasOriginalLogger = ReferenceEquals(table.GetLoggerForTest(), originalLogger);
                var modifiedOptionsHasNewLogger = ReferenceEquals(modifiedOptions.Logger, newLogger);
                var originalOptionsUnchanged = ReferenceEquals(options.Logger, originalLogger);
                
                return (tableHasOriginalLogger && modifiedOptionsHasNewLogger && originalOptionsUnchanged).ToProperty()
                    .Label($"Modifying options should not affect existing tables. " +
                           $"TableHasOriginalLogger: {tableHasOriginalLogger}, ModifiedOptionsHasNewLogger: {modifiedOptionsHasNewLogger}, OriginalOptionsUnchanged: {originalOptionsUnchanged}");
            });
    }
    
    /// <summary>
    /// 
    /// For any two table instances with different blob storage providers,
    /// each table SHALL use its own provider independently.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TwoTables_WithDifferentBlobProviders_MaintainIsolatedProviders()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            (tableName1, tableName2) =>
            {
                // Arrange
                var blobProvider1 = Substitute.For<IBlobStorageProvider>();
                var blobProvider2 = Substitute.For<IBlobStorageProvider>();
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                
                var options1 = new FluentDynamoDbOptions().WithBlobStorage(blobProvider1);
                var options2 = new FluentDynamoDbOptions().WithBlobStorage(blobProvider2);
                
                // Act
                var table1 = new TestTableForIsolation(mockClient, tableName1, options1);
                var table2 = new TestTableForIsolation(mockClient, tableName2, options2);
                
                // Assert
                var table1HasProvider1 = ReferenceEquals(table1.GetOptionsForTest().BlobStorageProvider, blobProvider1);
                var table2HasProvider2 = ReferenceEquals(table2.GetOptionsForTest().BlobStorageProvider, blobProvider2);
                var providersAreDifferent = !ReferenceEquals(
                    table1.GetOptionsForTest().BlobStorageProvider, 
                    table2.GetOptionsForTest().BlobStorageProvider);
                
                return (table1HasProvider1 && table2HasProvider2 && providersAreDifferent).ToProperty()
                    .Label($"Tables should maintain isolated blob providers. " +
                           $"Table1HasProvider1: {table1HasProvider1}, Table2HasProvider2: {table2HasProvider2}, ProvidersAreDifferent: {providersAreDifferent}");
            });
    }
    
    /// <summary>
    /// 
    /// For any two table instances with different field encryptors,
    /// each table SHALL use its own encryptor independently.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TwoTables_WithDifferentEncryptors_MaintainIsolatedEncryptors()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            (tableName1, tableName2) =>
            {
                // Arrange
                var encryptor1 = Substitute.For<IFieldEncryptor>();
                var encryptor2 = Substitute.For<IFieldEncryptor>();
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                
                var options1 = new FluentDynamoDbOptions().WithEncryption(encryptor1);
                var options2 = new FluentDynamoDbOptions().WithEncryption(encryptor2);
                
                // Act
                var table1 = new TestTableForIsolation(mockClient, tableName1, options1);
                var table2 = new TestTableForIsolation(mockClient, tableName2, options2);
                
                // Assert
                var table1HasEncryptor1 = ReferenceEquals(table1.GetEncryptorForTest(), encryptor1);
                var table2HasEncryptor2 = ReferenceEquals(table2.GetEncryptorForTest(), encryptor2);
                var encryptorsAreDifferent = !ReferenceEquals(table1.GetEncryptorForTest(), table2.GetEncryptorForTest());
                
                return (table1HasEncryptor1 && table2HasEncryptor2 && encryptorsAreDifferent).ToProperty()
                    .Label($"Tables should maintain isolated encryptors. " +
                           $"Table1HasEncryptor1: {table1HasEncryptor1}, Table2HasEncryptor2: {table2HasEncryptor2}, EncryptorsAreDifferent: {encryptorsAreDifferent}");
            });
    }
    
    /// <summary>
    /// 
    /// For any number of table instances created with different configurations,
    /// each table SHALL maintain its complete configuration independently.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleTables_WithFullyDifferentConfigs_MaintainCompleteIsolation()
    {
        return Prop.ForAll(
            Arb.Default.PositiveInt().Filter(n => n.Get >= 2 && n.Get <= 10),
            tableCount =>
            {
                // Arrange - create N different configurations
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var tables = new List<TestTableForIsolation>();
                var expectedLoggers = new List<IDynamoDbLogger>();
                var expectedEncryptors = new List<IFieldEncryptor>();
                
                for (int i = 0; i < tableCount.Get; i++)
                {
                    var logger = Substitute.For<IDynamoDbLogger>();
                    var encryptor = Substitute.For<IFieldEncryptor>();
                    expectedLoggers.Add(logger);
                    expectedEncryptors.Add(encryptor);
                    
                    var options = new FluentDynamoDbOptions()
                        .WithLogger(logger)
                        .WithEncryption(encryptor);
                    
                    tables.Add(new TestTableForIsolation(mockClient, $"Table{i}", options));
                }
                
                // Assert - each table should have its own configuration
                var allLoggersCorrect = true;
                var allEncryptorsCorrect = true;
                
                for (int i = 0; i < tableCount.Get; i++)
                {
                    if (!ReferenceEquals(tables[i].GetLoggerForTest(), expectedLoggers[i]))
                        allLoggersCorrect = false;
                    if (!ReferenceEquals(tables[i].GetEncryptorForTest(), expectedEncryptors[i]))
                        allEncryptorsCorrect = false;
                }
                
                return (allLoggersCorrect && allEncryptorsCorrect).ToProperty()
                    .Label($"All {tableCount.Get} tables should maintain isolated configurations. " +
                           $"AllLoggersCorrect: {allLoggersCorrect}, AllEncryptorsCorrect: {allEncryptorsCorrect}");
            });
    }
    
    /// <summary>
    /// 
    /// Configuration SHALL NOT be stored in static mutable fields.
    /// Creating a new table with different options SHALL NOT affect previously created tables.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreatingNewTable_DoesNotAffectExistingTables()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange
                var logger1 = Substitute.For<IDynamoDbLogger>();
                var logger2 = Substitute.For<IDynamoDbLogger>();
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                
                var options1 = new FluentDynamoDbOptions().WithLogger(logger1);
                
                // Act - create first table
                var table1 = new TestTableForIsolation(mockClient, tableName + "_1", options1);
                var table1LoggerBefore = table1.GetLoggerForTest();
                
                // Create second table with different options
                var options2 = new FluentDynamoDbOptions().WithLogger(logger2);
                var table2 = new TestTableForIsolation(mockClient, tableName + "_2", options2);
                
                // Check table1's logger after table2 creation
                var table1LoggerAfter = table1.GetLoggerForTest();
                
                // Assert - table1's logger should not have changed
                var table1LoggerUnchanged = ReferenceEquals(table1LoggerBefore, table1LoggerAfter);
                var table1StillHasLogger1 = ReferenceEquals(table1LoggerAfter, logger1);
                var table2HasLogger2 = ReferenceEquals(table2.GetLoggerForTest(), logger2);
                
                return (table1LoggerUnchanged && table1StillHasLogger1 && table2HasLogger2).ToProperty()
                    .Label($"Creating new table should not affect existing tables. " +
                           $"Table1LoggerUnchanged: {table1LoggerUnchanged}, Table1StillHasLogger1: {table1StillHasLogger1}, Table2HasLogger2: {table2HasLogger2}");
            });
    }
}

/// <summary>
/// Test table class for verifying configuration isolation.
/// </summary>
internal class TestTableForIsolation : DynamoDbTableBase
{
    public TestTableForIsolation(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)
        : base(client, tableName, options)
    {
    }
    
    /// <summary>
    /// Exposes the logger for testing purposes.
    /// </summary>
    public IDynamoDbLogger GetLoggerForTest() => Logger;
    
    /// <summary>
    /// Exposes the options for testing purposes.
    /// </summary>
    public FluentDynamoDbOptions GetOptionsForTest() => Options;
    
    /// <summary>
    /// Exposes the field encryptor for testing purposes.
    /// </summary>
    public IFieldEncryptor? GetEncryptorForTest() => FieldEncryptor;
}

/// <summary>
/// Property-based tests for default options behavior.
/// Validates that default options use sensible defaults and core operations work correctly.
/// </summary>
public class DefaultOptionsPropertyTests
{
    /// <summary>
    /// 
    /// For any table created without explicit options or with new FluentDynamoDbOptions(),
    /// the table SHALL use NoOpLogger.Instance for logging, null for optional providers,
    /// and SHALL function correctly for core operations.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DefaultOptions_UsesNoOpLogger()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange & Act
                var options = new FluentDynamoDbOptions();
                
                // Assert - default logger should be NoOpLogger.Instance
                var usesNoOpLogger = ReferenceEquals(options.Logger, NoOpLogger.Instance);
                
                return usesNoOpLogger.ToProperty()
                    .Label($"Default options should use NoOpLogger.Instance. UsesNoOpLogger: {usesNoOpLogger}");
            });
    }
    
    /// <summary>
    /// 
    /// For any FluentDynamoDbOptions created with default constructor,
    /// all optional providers SHALL be null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DefaultOptions_HasNullOptionalProviders()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange & Act
                var options = new FluentDynamoDbOptions();
                
                // Assert - all optional providers should be null
                var geospatialIsNull = options.GeospatialProvider == null;
                var blobStorageIsNull = options.BlobStorageProvider == null;
                var encryptorIsNull = options.FieldEncryptor == null;
                
                return (geospatialIsNull && blobStorageIsNull && encryptorIsNull).ToProperty()
                    .Label($"Default options should have null optional providers. " +
                           $"GeospatialIsNull: {geospatialIsNull}, BlobStorageIsNull: {blobStorageIsNull}, EncryptorIsNull: {encryptorIsNull}");
            });
    }
    
    /// <summary>
    /// 
    /// For any table created with null options parameter,
    /// the table SHALL use sensible defaults (NoOpLogger, no optional features).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TableWithNullOptions_UsesSensibleDefaults()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                
                // Act - create table with null options
                var table = new TestTableForDefaultOptions(mockClient, tableName, null);
                
                // Assert
                var usesNoOpLogger = ReferenceEquals(table.GetLoggerForTest(), NoOpLogger.Instance);
                var encryptorIsNull = table.GetEncryptorForTest() == null;
                var optionsNotNull = table.GetOptionsForTest() != null;
                
                return (usesNoOpLogger && encryptorIsNull && optionsNotNull).ToProperty()
                    .Label($"Table with null options should use sensible defaults. " +
                           $"UsesNoOpLogger: {usesNoOpLogger}, EncryptorIsNull: {encryptorIsNull}, OptionsNotNull: {optionsNotNull}");
            });
    }
    
    /// <summary>
    /// 
    /// For any table created with explicit default options (new FluentDynamoDbOptions()),
    /// the table SHALL use the same defaults as when created with null options.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TableWithExplicitDefaultOptions_EquivalentToNullOptions()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                
                // Act - create tables with null and explicit default options
                var tableWithNull = new TestTableForDefaultOptions(mockClient, tableName + "_null", null);
                var tableWithDefault = new TestTableForDefaultOptions(mockClient, tableName + "_default", new FluentDynamoDbOptions());
                
                // Assert - both should have equivalent configurations
                var bothUseNoOpLogger = 
                    ReferenceEquals(tableWithNull.GetLoggerForTest(), NoOpLogger.Instance) &&
                    ReferenceEquals(tableWithDefault.GetLoggerForTest(), NoOpLogger.Instance);
                var bothHaveNullEncryptor = 
                    tableWithNull.GetEncryptorForTest() == null &&
                    tableWithDefault.GetEncryptorForTest() == null;
                var bothHaveNullBlobProvider = 
                    tableWithNull.GetOptionsForTest().BlobStorageProvider == null &&
                    tableWithDefault.GetOptionsForTest().BlobStorageProvider == null;
                var bothHaveNullGeospatialProvider = 
                    tableWithNull.GetOptionsForTest().GeospatialProvider == null &&
                    tableWithDefault.GetOptionsForTest().GeospatialProvider == null;
                
                return (bothUseNoOpLogger && bothHaveNullEncryptor && bothHaveNullBlobProvider && bothHaveNullGeospatialProvider).ToProperty()
                    .Label($"Tables with null and explicit default options should be equivalent. " +
                           $"BothUseNoOpLogger: {bothUseNoOpLogger}, BothHaveNullEncryptor: {bothHaveNullEncryptor}, " +
                           $"BothHaveNullBlobProvider: {bothHaveNullBlobProvider}, BothHaveNullGeospatialProvider: {bothHaveNullGeospatialProvider}");
            });
    }
    
    /// <summary>
    /// 
    /// For any table created without optional packages (geospatial, blob storage),
    /// core DynamoDB operations (Query, Scan, Get, Put, Update, Delete) SHALL function correctly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CoreOperations_WorkWithoutOptionalPackages()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange - create table with default options (no optional packages)
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var options = new FluentDynamoDbOptions(); // No optional packages
                var table = new TestTableForDefaultOptions(mockClient, tableName, options);
                
                // Act - verify all core operations can be created
                var queryBuilder = table.Query<TestEntityForDefaultOptions>();
                var getBuilder = table.Get<TestEntityForDefaultOptions>();
                var updateBuilder = table.Update<TestEntityForDefaultOptions>();
                var deleteBuilder = table.Delete<TestEntityForDefaultOptions>();
                var putBuilder = table.Put<TestEntityForDefaultOptions>();
                var scanBuilder = table.Scan<TestEntityForDefaultOptions>();
                var conditionCheckBuilder = table.ConditionCheck<TestEntityForDefaultOptions>();
                
                // Assert - all builders should be created successfully
                var allBuildersCreated = 
                    queryBuilder != null && 
                    getBuilder != null && 
                    updateBuilder != null && 
                    deleteBuilder != null && 
                    putBuilder != null && 
                    scanBuilder != null &&
                    conditionCheckBuilder != null;
                
                // Verify table name is set correctly on the table
                var tableNameCorrect = table.Name == tableName;
                
                return (allBuildersCreated && tableNameCorrect).ToProperty()
                    .Label($"Core operations should work without optional packages. " +
                           $"AllBuildersCreated: {allBuildersCreated}, TableNameCorrect: {tableNameCorrect}");
            });
    }
    
    /// <summary>
    /// 
    /// For any combination of optional packages that are registered,
    /// core DynamoDB operations SHALL function correctly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CoreOperations_WorkWithAnyOptionalPackageCombination()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Test all 8 combinations of optional packages (2^3)
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var allCombinationsWork = true;
                
                for (int i = 0; i < 8; i++)
                {
                    var withLogger = (i & 1) != 0;
                    var withBlobStorage = (i & 2) != 0;
                    var withEncryption = (i & 4) != 0;
                    
                    var options = new FluentDynamoDbOptions();
                    
                    if (withLogger)
                    {
                        var logger = Substitute.For<IDynamoDbLogger>();
                        options = options.WithLogger(logger);
                    }
                    
                    if (withBlobStorage)
                    {
                        var blobProvider = Substitute.For<IBlobStorageProvider>();
                        options = options.WithBlobStorage(blobProvider);
                    }
                    
                    if (withEncryption)
                    {
                        var encryptor = Substitute.For<IFieldEncryptor>();
                        options = options.WithEncryption(encryptor);
                    }
                    
                    var table = new TestTableForDefaultOptions(mockClient, $"{tableName}_{i}", options);
                    
                    // Verify all core operations can be created
                    var queryBuilder = table.Query<TestEntityForDefaultOptions>();
                    var getBuilder = table.Get<TestEntityForDefaultOptions>();
                    var updateBuilder = table.Update<TestEntityForDefaultOptions>();
                    var deleteBuilder = table.Delete<TestEntityForDefaultOptions>();
                    var putBuilder = table.Put<TestEntityForDefaultOptions>();
                    var scanBuilder = table.Scan<TestEntityForDefaultOptions>();
                    
                    var buildersCreated = 
                        queryBuilder != null && 
                        getBuilder != null && 
                        updateBuilder != null && 
                        deleteBuilder != null && 
                        putBuilder != null && 
                        scanBuilder != null;
                    
                    if (!buildersCreated)
                    {
                        allCombinationsWork = false;
                        break;
                    }
                }
                
                return allCombinationsWork.ToProperty()
                    .Label($"Core operations should work with all 8 optional package combinations. " +
                           $"AllCombinationsWork: {allCombinationsWork}");
            });
    }
    
    /// <summary>
    /// 
    /// For any table created with only a logger configured (no other optional packages),
    /// core DynamoDB operations SHALL function correctly and use the configured logger.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CoreOperations_WorkWithOnlyLoggerConfigured()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s)),
            tableName =>
            {
                // Arrange - create table with only logger configured
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var mockLogger = Substitute.For<IDynamoDbLogger>();
                var options = new FluentDynamoDbOptions().WithLogger(mockLogger);
                var table = new TestTableForDefaultOptions(mockClient, tableName, options);
                
                // Act - verify core operations work
                var queryBuilder = table.Query<TestEntityForDefaultOptions>();
                var getBuilder = table.Get<TestEntityForDefaultOptions>();
                var updateBuilder = table.Update<TestEntityForDefaultOptions>();
                var deleteBuilder = table.Delete<TestEntityForDefaultOptions>();
                var putBuilder = table.Put<TestEntityForDefaultOptions>();
                var scanBuilder = table.Scan<TestEntityForDefaultOptions>();
                
                // Assert
                var allBuildersCreated = 
                    queryBuilder != null && 
                    getBuilder != null && 
                    updateBuilder != null && 
                    deleteBuilder != null && 
                    putBuilder != null && 
                    scanBuilder != null;
                
                var tableUsesConfiguredLogger = ReferenceEquals(table.GetLoggerForTest(), mockLogger);
                var optionalProvidersAreNull = 
                    table.GetOptionsForTest().BlobStorageProvider == null &&
                    table.GetOptionsForTest().GeospatialProvider == null &&
                    table.GetEncryptorForTest() == null;
                
                return (allBuildersCreated && tableUsesConfiguredLogger && optionalProvidersAreNull).ToProperty()
                    .Label($"Core operations should work with only logger configured. " +
                           $"AllBuildersCreated: {allBuildersCreated}, TableUsesConfiguredLogger: {tableUsesConfiguredLogger}, " +
                           $"OptionalProvidersAreNull: {optionalProvidersAreNull}");
            });
    }
}

/// <summary>
/// Test table class for verifying default options behavior.
/// </summary>
internal class TestTableForDefaultOptions : DynamoDbTableBase
{
    public TestTableForDefaultOptions(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)
        : base(client, tableName, options)
    {
    }
    
    /// <summary>
    /// Exposes the logger for testing purposes.
    /// </summary>
    public IDynamoDbLogger GetLoggerForTest() => Logger;
    
    /// <summary>
    /// Exposes the options for testing purposes.
    /// </summary>
    public FluentDynamoDbOptions GetOptionsForTest() => Options;
    
    /// <summary>
    /// Exposes the field encryptor for testing purposes.
    /// </summary>
    public IFieldEncryptor? GetEncryptorForTest() => FieldEncryptor;
    
    /// <summary>
    /// Creates a new Scan operation builder for this table.
    /// Added for testing purposes to verify Scan builder creation.
    /// </summary>
    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class =>
        new ScanRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);
}

/// <summary>
/// Simple test entity for default options property tests.
/// </summary>
internal class TestEntityForDefaultOptions
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Tests for parallel test execution to verify no cross-contamination between configurations.
/// </summary>
public class ParallelConfigurationTests
{
    /// <summary>
    /// Verifies that multiple tables created in parallel with different configurations
    /// maintain their isolated configurations without cross-contamination.
    /// </summary>
    [Fact]
    public async Task ParallelTableCreation_WithDifferentConfigs_MaintainsIsolation()
    {
        // Arrange
        const int tableCount = 50;
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var tables = new ConcurrentBag<(TestTableForIsolation Table, IDynamoDbLogger ExpectedLogger, IFieldEncryptor ExpectedEncryptor)>();
        
        // Act - create tables in parallel with different configurations
        var tasks = Enumerable.Range(0, tableCount).Select(async i =>
        {
            // Simulate some async work to increase chance of race conditions
            await Task.Yield();
            
            var logger = Substitute.For<IDynamoDbLogger>();
            var encryptor = Substitute.For<IFieldEncryptor>();
            var options = new FluentDynamoDbOptions()
                .WithLogger(logger)
                .WithEncryption(encryptor);
            
            var table = new TestTableForIsolation(mockClient, $"ParallelTable{i}", options);
            tables.Add((table, logger, encryptor));
        });
        
        await Task.WhenAll(tasks);
        
        // Assert - verify each table has its own configuration
        foreach (var (table, expectedLogger, expectedEncryptor) in tables)
        {
            Assert.Same(expectedLogger, table.GetLoggerForTest());
            Assert.Same(expectedEncryptor, table.GetEncryptorForTest());
        }
    }
    
    /// <summary>
    /// Verifies that concurrent access to tables with different configurations
    /// does not cause cross-contamination.
    /// </summary>
    [Fact]
    public async Task ConcurrentTableAccess_WithDifferentConfigs_NoContamination()
    {
        // Arrange
        const int iterationCount = 100;
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        var logger1 = Substitute.For<IDynamoDbLogger>();
        var logger2 = Substitute.For<IDynamoDbLogger>();
        var encryptor1 = Substitute.For<IFieldEncryptor>();
        var encryptor2 = Substitute.For<IFieldEncryptor>();
        
        var options1 = new FluentDynamoDbOptions()
            .WithLogger(logger1)
            .WithEncryption(encryptor1);
        var options2 = new FluentDynamoDbOptions()
            .WithLogger(logger2)
            .WithEncryption(encryptor2);
        
        var table1 = new TestTableForIsolation(mockClient, "Table1", options1);
        var table2 = new TestTableForIsolation(mockClient, "Table2", options2);
        
        var errors = new ConcurrentBag<string>();
        
        // Act - access both tables concurrently many times
        var tasks = Enumerable.Range(0, iterationCount).Select(async i =>
        {
            await Task.Yield();
            
            // Check table1's configuration
            if (!ReferenceEquals(table1.GetLoggerForTest(), logger1))
                errors.Add($"Iteration {i}: Table1 logger mismatch");
            if (!ReferenceEquals(table1.GetEncryptorForTest(), encryptor1))
                errors.Add($"Iteration {i}: Table1 encryptor mismatch");
            
            // Check table2's configuration
            if (!ReferenceEquals(table2.GetLoggerForTest(), logger2))
                errors.Add($"Iteration {i}: Table2 logger mismatch");
            if (!ReferenceEquals(table2.GetEncryptorForTest(), encryptor2))
                errors.Add($"Iteration {i}: Table2 encryptor mismatch");
        });
        
        await Task.WhenAll(tasks);
        
        // Assert
        Assert.Empty(errors);
    }
    
    /// <summary>
    /// Verifies that creating and disposing tables in parallel does not affect other tables.
    /// </summary>
    [Fact]
    public async Task ParallelTableCreationAndDisposal_NoSideEffects()
    {
        // Arrange
        const int iterationCount = 20;
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Create a "stable" table that should not be affected
        var stableLogger = Substitute.For<IDynamoDbLogger>();
        var stableOptions = new FluentDynamoDbOptions().WithLogger(stableLogger);
        var stableTable = new TestTableForIsolation(mockClient, "StableTable", stableOptions);
        
        var errors = new ConcurrentBag<string>();
        
        // Act - create and "dispose" many tables in parallel while checking stable table
        var tasks = Enumerable.Range(0, iterationCount).Select(async i =>
        {
            await Task.Yield();
            
            // Create a temporary table with different config
            var tempLogger = Substitute.For<IDynamoDbLogger>();
            var tempOptions = new FluentDynamoDbOptions().WithLogger(tempLogger);
            var tempTable = new TestTableForIsolation(mockClient, $"TempTable{i}", tempOptions);
            
            // Verify temp table has correct config
            if (!ReferenceEquals(tempTable.GetLoggerForTest(), tempLogger))
                errors.Add($"Iteration {i}: TempTable logger mismatch");
            
            // Verify stable table still has correct config
            if (!ReferenceEquals(stableTable.GetLoggerForTest(), stableLogger))
                errors.Add($"Iteration {i}: StableTable logger contaminated");
            
            // Let the temp table go out of scope (simulating disposal)
            await Task.Yield();
            
            // Verify stable table still has correct config after temp table "disposal"
            if (!ReferenceEquals(stableTable.GetLoggerForTest(), stableLogger))
                errors.Add($"Iteration {i}: StableTable logger contaminated after disposal");
        });
        
        await Task.WhenAll(tasks);
        
        // Assert
        Assert.Empty(errors);
        Assert.Same(stableLogger, stableTable.GetLoggerForTest());
    }
    
    /// <summary>
    /// Verifies that modifying options in parallel does not affect existing tables.
    /// </summary>
    [Fact]
    public async Task ParallelOptionsModification_DoesNotAffectExistingTables()
    {
        // Arrange
        const int iterationCount = 50;
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        var originalLogger = Substitute.For<IDynamoDbLogger>();
        var originalOptions = new FluentDynamoDbOptions().WithLogger(originalLogger);
        var table = new TestTableForIsolation(mockClient, "TestTable", originalOptions);
        
        var errors = new ConcurrentBag<string>();
        
        // Act - modify options in parallel while checking table's config
        var tasks = Enumerable.Range(0, iterationCount).Select(async i =>
        {
            await Task.Yield();
            
            // Create new options from original (this should not affect the table)
            var newLogger = Substitute.For<IDynamoDbLogger>();
            var newOptions = originalOptions.WithLogger(newLogger);
            
            // Verify the new options have the new logger
            if (!ReferenceEquals(newOptions.Logger, newLogger))
                errors.Add($"Iteration {i}: New options logger mismatch");
            
            // Verify the original options still have the original logger
            if (!ReferenceEquals(originalOptions.Logger, originalLogger))
                errors.Add($"Iteration {i}: Original options logger contaminated");
            
            // Verify the table still has the original logger
            if (!ReferenceEquals(table.GetLoggerForTest(), originalLogger))
                errors.Add($"Iteration {i}: Table logger contaminated");
        });
        
        await Task.WhenAll(tasks);
        
        // Assert
        Assert.Empty(errors);
        Assert.Same(originalLogger, table.GetLoggerForTest());
        Assert.Same(originalLogger, originalOptions.Logger);
    }
}
