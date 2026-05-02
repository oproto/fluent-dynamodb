using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Bug condition exploration tests for the encryption pipeline fix.
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists.
///
/// Bug Condition: isBugCondition(input) = hasEncrypted AND NOT hasBlobStorage
///
/// **Validates: Requirements 1.1, 1.2, 1.3**
/// </summary>
[Trait("Category", "BugExploration")]
public class EncryptionPipelineBugExplorationTests
{
    /// <summary>
    /// Defect 1.1: HydratorGenerator.RequiresHydrator() returns false for an entity
    /// with [Encrypted] properties but no blob storage.
    ///
    /// Expected behavior: RequiresHydrator() should return true for encryption-only entities
    /// so that a hydrator is generated for the read path (FromDynamoDbAsync).
    ///
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property RequiresHydrator_ShouldReturnTrue_ForEncryptionOnlyEntities()
    {
        // Generator: create entity models with at least one encrypted property and no blob storage
        var encryptedPropertyGen = Gen.Elements("SocialSecurityNumber", "CreditCard", "Salary", "Secret")
            .Select(name => new PropertyModel
            {
                PropertyName = name,
                AttributeName = name.ToLowerInvariant(),
                PropertyType = "string",
                Security = new SecurityInfo { IsEncrypted = true }
            });

        var plainPropertyGen = Gen.Elements("Id", "Name", "Email", "Status")
            .Select(name => new PropertyModel
            {
                PropertyName = name,
                AttributeName = name.ToLowerInvariant(),
                PropertyType = "string"
            });

        var entityGen = from encryptedCount in Gen.Choose(1, 3)
                        from encryptedProps in Gen.ListOf(encryptedCount, encryptedPropertyGen)
                        from plainCount in Gen.Choose(0, 2)
                        from plainProps in Gen.ListOf(plainCount, plainPropertyGen)
                        select new EntityModel
                        {
                            ClassName = "EncryptionOnlyEntity",
                            Namespace = "TestNamespace",
                            TableName = "test-table",
                            Properties = new[] { new PropertyModel
                            {
                                PropertyName = "Pk",
                                AttributeName = "pk",
                                PropertyType = "string",
                                IsPartitionKey = true
                            }}
                            .Concat(encryptedProps)
                            .Concat(plainProps)
                            .ToArray()
                        };

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                // Verify: entity has encrypted properties but no blob storage
                var hasEncrypted = entity.Properties.Any(p => p.Security?.IsEncrypted == true);
                var hasBlobStorage = entity.Properties.Any(p => p.ComplexType?.IsBlobStorage == true);

                // Precondition: this is an encryption-only entity
                return (hasEncrypted && !hasBlobStorage).ToProperty()
                    .When(hasEncrypted && !hasBlobStorage)
                    .And(
                        HydratorGenerator.RequiresHydrator(entity).ToProperty()
                            .Label($"RequiresHydrator() should return true for encryption-only entity " +
                                   $"with {entity.Properties.Count(p => p.Security?.IsEncrypted == true)} encrypted properties")
                    );
            });
    }

    /// <summary>
    /// Defect 1.1 (continued): GenerateHydrator() returns null for encryption-only entities,
    /// meaning no hydrator class is generated and the read path cannot use FromDynamoDbAsync.
    ///
    /// Expected behavior: GenerateHydrator() should return non-null (generated hydrator code)
    /// for encryption-only entities.
    ///
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property GenerateHydrator_ShouldReturnNonNull_ForEncryptionOnlyEntities()
    {
        var entityGen = Gen.Constant(new EntityModel
        {
            ClassName = "SecureRecord",
            Namespace = "TestNamespace",
            TableName = "secure-records",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                },
                new PropertyModel
                {
                    PropertyName = "Ssn",
                    AttributeName = "ssn",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsEncrypted = true }
                },
                new PropertyModel
                {
                    PropertyName = "CreditCard",
                    AttributeName = "credit_card",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsEncrypted = true }
                }
            }
        });

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var result = HydratorGenerator.GenerateHydrator(entity);

                return (result != null).ToProperty()
                    .Label("GenerateHydrator() should return non-null for encryption-only entity " +
                           "(hydrator is needed for async read/write path)");
            });
    }
}
