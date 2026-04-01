using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Bug condition exploration tests for the encryption pipeline fix.
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists.
///
/// Bug Condition: isBugCondition(input) = hasEncrypted AND NOT hasBlobStorage AND operation IN [Put, Get, Query, Scan]
///
/// **Validates: Requirements 1.1, 1.2, 1.3**
/// </summary>
[Trait("Category", "BugExploration")]
[Collection("OperationContext")]
public class EncryptionPipelineBugExplorationTests
{
    /// <summary>
    /// Defect 1.2: ToDynamoDbAsync(entity, blobProvider: null) throws ArgumentNullException
    /// for encryption-only entities.
    ///
    /// Expected behavior: ToDynamoDbAsync should accept null blobProvider for encryption-only
    /// entities without throwing, since no blob storage is needed.
    ///
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property ToDynamoDbAsync_ShouldNotThrow_ForEncryptionOnlyEntityWithNullBlobProvider()
    {
        var nameGen = Gen.Elements("Alice", "Bob", "Charlie", "Diana");
        var ssnGen = Gen.Elements("123-45-6789", "987-65-4321", "555-12-3456");

        return Prop.ForAll(
            nameGen.ToArbitrary(),
            ssnGen.ToArbitrary(),
            (name, ssn) =>
            {
                var entity = new EncryptionOnlyTestEntity
                {
                    Pk = "test-pk-001",
                    Name = name,
                    SocialSecurityNumber = ssn
                };

                var encryptor = Substitute.For<IFieldEncryptor>();
                encryptor.EncryptAsync(
                    Arg.Any<byte[]>(),
                    Arg.Any<string>(),
                    Arg.Any<Oproto.FluentDynamoDb.Providers.Encryption.FieldEncryptionContext>(),
                    Arg.Any<CancellationToken>())
                    .Returns(callInfo => Task.FromResult(callInfo.ArgAt<byte[]>(0)));

                var options = new FluentDynamoDbOptions().WithEncryption(encryptor);

                // Expected behavior: calling ToDynamoDbAsync with null blobProvider should NOT throw
                // for encryption-only entities (no blob storage properties)
                Exception? caughtException = null;
                try
                {
                    // Pass null for blobProvider since this entity has no blob storage
                    var task = EncryptionOnlyTestEntity.ToDynamoDbAsync<EncryptionOnlyTestEntity>(
                        entity,
                        null!,
                        encryptor,
                        options);
                    task.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }

                return (caughtException == null).ToProperty()
                    .Label($"ToDynamoDbAsync should not throw for encryption-only entity with null blobProvider, " +
                           $"but threw {caughtException?.GetType().Name}: {caughtException?.Message}");
            });
    }

    /// <summary>
    /// Defect 1.3: PutItemRequestBuilder.WithItem(encryptedEntity) throws NotSupportedException
    /// from the synchronous ToDynamoDb stub.
    ///
    /// Expected behavior: WithItem() should not throw NotSupportedException for encrypted entities.
    /// It should either defer serialization or handle the async requirement gracefully.
    ///
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property WithItem_ShouldNotThrowNotSupportedException_ForEncryptedEntities()
    {
        var nameGen = Gen.Elements("Alice", "Bob", "Charlie", "Diana");
        var ssnGen = Gen.Elements("123-45-6789", "987-65-4321", "555-12-3456");

        return Prop.ForAll(
            nameGen.ToArbitrary(),
            ssnGen.ToArbitrary(),
            (name, ssn) =>
            {
                var entity = new EncryptionOnlyTestEntity
                {
                    Pk = "test-pk-001",
                    Name = name,
                    SocialSecurityNumber = ssn
                };

                var client = Substitute.For<IAmazonDynamoDB>();
                var encryptor = Substitute.For<IFieldEncryptor>();
                var options = new FluentDynamoDbOptions().WithEncryption(encryptor);
                var builder = new PutItemRequestBuilder<EncryptionOnlyTestEntity>(client, options);
                builder.ForTable("test-table");

                // Expected behavior: WithItem should NOT throw NotSupportedException
                // for encrypted entities (should defer serialization to async execution time)
                Exception? caughtException = null;
                try
                {
                    builder.WithItem(entity);
                }
                catch (NotSupportedException ex)
                {
                    caughtException = ex;
                }

                return (caughtException == null).ToProperty()
                    .Label($"WithItem() should not throw NotSupportedException for encrypted entity, " +
                           $"but threw: {caughtException?.Message}");
            });
    }

    /// <summary>
    /// Defect 1.1 (runtime verification): The hydrator registry should contain a hydrator
    /// for encryption-only entity types after registration.
    ///
    /// This test verifies the runtime consequence of Defect 1.1: since no hydrator is generated,
    /// the registry has no hydrator for encryption-only entities, causing the read path to fail.
    ///
    /// Expected behavior: A hydrator should be generated and registerable for encryption-only entities.
    ///
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void HydratorRegistry_ShouldContainHydrator_ForEncryptionOnlyEntityType()
    {
        // Arrange
        var registry = new DefaultEntityHydratorRegistry();

        // Act - Try to get a hydrator for the encryption-only entity
        // If the source generator correctly generates a hydrator, we should be able to register it.
        // Since RequiresHydrator() returns false for encryption-only entities,
        // no hydrator class is generated, so there's nothing to register.
        var hydrator = registry.GetHydrator<EncryptionOnlyTestEntity>();

        // The fact that we can't even register a hydrator (because none was generated)
        // is the root cause of the read path failure.
        // For now, we verify the registry returns null (confirming the bug).
        // After the fix, a generated hydrator should be registerable and retrievable.

        // Expected behavior: after fix, a hydrator should exist for encryption-only entities
        // This assertion encodes the expected behavior - it will fail on unfixed code
        // because no hydrator is generated for encryption-only entities.
        //
        // Note: We can't directly test registration of a generated hydrator here because
        // the hydrator class doesn't exist yet (that's the bug). Instead, we verify
        // that the generated extension method exists by checking if the type has a
        // hydrator registration extension.
        var hydratorType = Type.GetType(
            $"{typeof(EncryptionOnlyTestEntity).Namespace}.EncryptionOnlyTestEntityHydrator, " +
            $"{typeof(EncryptionOnlyTestEntity).Assembly.GetName().Name}");

        hydratorType.Should().NotBeNull(
            "A hydrator class should be generated for encryption-only entities " +
            "so the read path can use FromDynamoDbAsync for decryption");
    }
}

/// <summary>
/// Test entity with [Encrypted] properties but NO blob storage.
/// This represents the bug condition: hasEncrypted AND NOT hasBlobStorage.
///
/// The source generator will generate ToDynamoDb (sync stub that throws NotSupportedException)
/// and ToDynamoDbAsync (which requires non-null blobProvider).
/// </summary>
[DynamoDbTable("encryption-only-test")]
public partial class EncryptionOnlyTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string? Name { get; set; }

    [Encrypted]
    [DynamoDbAttribute("ssn")]
    public string? SocialSecurityNumber { get; set; }
}
