using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Preservation property tests for the encryption pipeline fix.
/// These tests capture baseline behavior of all non-bug-condition entity types
/// on UNFIXED code to prevent regressions.
///
/// These tests MUST PASS on unfixed code — they encode current correct behavior.
///
/// **Validates: Requirements 3.1, 3.2, 3.3**
/// </summary>
[Trait("Category", "Preservation")]
public class EncryptionPipelinePreservationTests
{
    #region Generators

    /// <summary>
    /// Generates a plain entity model: no encryption, no blob storage.
    /// </summary>
    private static Gen<EntityModel> PlainEntityGen()
    {
        var plainPropertyGen = Gen.Elements("Name", "Email", "Status", "Description", "Phone")
            .Select(name => new PropertyModel
            {
                PropertyName = name,
                AttributeName = name.ToLowerInvariant(),
                PropertyType = "string"
            });

        return from plainCount in Gen.Choose(1, 4)
               from plainProps in Gen.ListOf(plainCount, plainPropertyGen)
               select new EntityModel
               {
                   ClassName = "PlainEntity",
                   Namespace = "TestNamespace",
                   TableName = "plain-table",
                   Properties = new[]
                   {
                       new PropertyModel
                       {
                           PropertyName = "Pk",
                           AttributeName = "pk",
                           PropertyType = "string",
                           IsPartitionKey = true
                       }
                   }.Concat(plainProps).ToArray()
               };
    }

    /// <summary>
    /// Generates a blob-only entity model: has blob storage, no encryption.
    /// </summary>
    private static Gen<EntityModel> BlobOnlyEntityGen()
    {
        var blobPropertyGen = Gen.Elements("Document", "Image", "Attachment", "Payload")
            .Select(name => new PropertyModel
            {
                PropertyName = name,
                AttributeName = name.ToLowerInvariant() + "_ref",
                PropertyType = "BlobData<byte[]>",
                ComplexType = new ComplexTypeInfo
                {
                    IsBlobStorage = true,
                    BlobDataInnerType = "byte[]"
                }
            });

        var plainPropertyGen = Gen.Elements("Title", "Label")
            .Select(name => new PropertyModel
            {
                PropertyName = name,
                AttributeName = name.ToLowerInvariant(),
                PropertyType = "string"
            });

        return from blobCount in Gen.Choose(1, 2)
               from blobProps in Gen.ListOf(blobCount, blobPropertyGen)
               from plainCount in Gen.Choose(0, 2)
               from plainProps in Gen.ListOf(plainCount, plainPropertyGen)
               select new EntityModel
               {
                   ClassName = "BlobOnlyEntity",
                   Namespace = "TestNamespace",
                   TableName = "blob-table",
                   Properties = new[]
                   {
                       new PropertyModel
                       {
                           PropertyName = "Pk",
                           AttributeName = "pk",
                           PropertyType = "string",
                           IsPartitionKey = true
                       }
                   }.Concat(blobProps).Concat(plainProps).ToArray()
               };
    }

    /// <summary>
    /// Generates a blob+encrypted entity model: has both blob storage and encryption.
    /// </summary>
    private static Gen<EntityModel> BlobPlusEncryptedEntityGen()
    {
        var blobPropertyGen = Gen.Constant(new PropertyModel
        {
            PropertyName = "LargeData",
            AttributeName = "large_data_ref",
            PropertyType = "BlobData<byte[]>",
            ComplexType = new ComplexTypeInfo
            {
                IsBlobStorage = true,
                BlobDataInnerType = "byte[]"
            }
        });

        var encryptedPropertyGen = Gen.Elements("Ssn", "CreditCard", "Secret")
            .Select(name => new PropertyModel
            {
                PropertyName = name,
                AttributeName = name.ToLowerInvariant(),
                PropertyType = "string",
                Security = new SecurityInfo { IsEncrypted = true }
            });

        return from blobProp in blobPropertyGen
               from encCount in Gen.Choose(1, 2)
               from encProps in Gen.ListOf(encCount, encryptedPropertyGen)
               select new EntityModel
               {
                   ClassName = "BlobEncryptedEntity",
                   Namespace = "TestNamespace",
                   TableName = "blob-encrypted-table",
                   Properties = new[]
                   {
                       new PropertyModel
                       {
                           PropertyName = "Pk",
                           AttributeName = "pk",
                           PropertyType = "string",
                           IsPartitionKey = true
                       },
                       blobProp
                   }.Concat(encProps).ToArray()
               };
    }

    #endregion

    /// <summary>
    /// Preservation 3.1: Plain entities (no encryption, no blob) → RequiresHydrator() returns false.
    /// Sync serialization path is used; no hydrator is generated.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property PlainEntity_RequiresHydrator_ReturnsFalse()
    {
        return Prop.ForAll(
            PlainEntityGen().ToArbitrary(),
            entity =>
            {
                var hasEncrypted = entity.Properties.Any(p => p.Security?.IsEncrypted == true);
                var hasBlobStorage = entity.Properties.Any(p => p.ComplexType?.IsBlobStorage == true);

                // Precondition: this is a plain entity
                return (!hasEncrypted && !hasBlobStorage).ToProperty()
                    .When(!hasEncrypted && !hasBlobStorage)
                    .And(
                        (!HydratorGenerator.RequiresHydrator(entity)).ToProperty()
                            .Label("RequiresHydrator() should return false for plain entities (no encryption, no blob)")
                    );
            });
    }

    /// <summary>
    /// Preservation 3.1: Plain entities → GenerateHydrator() returns null (no hydrator generated).
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property PlainEntity_GenerateHydrator_ReturnsNull()
    {
        return Prop.ForAll(
            PlainEntityGen().ToArbitrary(),
            entity =>
            {
                var result = HydratorGenerator.GenerateHydrator(entity);

                return (result == null).ToProperty()
                    .Label("GenerateHydrator() should return null for plain entities");
            });
    }

    /// <summary>
    /// Preservation 3.2: Blob-only entities → RequiresHydrator() returns true.
    /// Hydrator IS generated for async blob storage path.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property BlobOnlyEntity_RequiresHydrator_ReturnsTrue()
    {
        return Prop.ForAll(
            BlobOnlyEntityGen().ToArbitrary(),
            entity =>
            {
                var hasBlobStorage = entity.Properties.Any(p => p.ComplexType?.IsBlobStorage == true);

                return hasBlobStorage.ToProperty()
                    .When(hasBlobStorage)
                    .And(
                        HydratorGenerator.RequiresHydrator(entity).ToProperty()
                            .Label("RequiresHydrator() should return true for blob-only entities")
                    );
            });
    }

    /// <summary>
    /// Preservation 3.2: Blob-only entities → GenerateHydrator() returns non-null hydrator code
    /// with non-nullable blobProvider parameter and null guard.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property BlobOnlyEntity_GenerateHydrator_HasNonNullableBlobProvider()
    {
        return Prop.ForAll(
            BlobOnlyEntityGen().ToArbitrary(),
            entity =>
            {
                var result = HydratorGenerator.GenerateHydrator(entity);

                var isNonNull = result != null;
                var hasNonNullableBlobProvider = result?.Contains("IBlobStorageProvider? blobProvider,") == true;
                var hasNullGuard = result?.Contains("ArgumentNullException.ThrowIfNull(blobProvider)") == true;

                return isNonNull.ToProperty()
                    .Label("GenerateHydrator() should return non-null for blob-only entities")
                    .And(hasNonNullableBlobProvider.ToProperty()
                        .Label("Generated hydrator should have nullable blobProvider parameter matching interface"))
                    .And(hasNullGuard.ToProperty()
                        .Label("Generated hydrator should have null guard for blobProvider"));
            });
    }

    /// <summary>
    /// Preservation 3.3: Blob+encrypted entities → RequiresHydrator() returns true.
    /// Both providers are used in the async path.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property BlobPlusEncryptedEntity_RequiresHydrator_ReturnsTrue()
    {
        return Prop.ForAll(
            BlobPlusEncryptedEntityGen().ToArbitrary(),
            entity =>
            {
                var hasBlobStorage = entity.Properties.Any(p => p.ComplexType?.IsBlobStorage == true);
                var hasEncrypted = entity.Properties.Any(p => p.Security?.IsEncrypted == true);

                return (hasBlobStorage && hasEncrypted).ToProperty()
                    .When(hasBlobStorage && hasEncrypted)
                    .And(
                        HydratorGenerator.RequiresHydrator(entity).ToProperty()
                            .Label("RequiresHydrator() should return true for blob+encrypted entities")
                    );
            });
    }

    /// <summary>
    /// Preservation 3.3: Blob+encrypted entities → GenerateHydrator() returns non-null hydrator code
    /// that delegates to both blob provider and field encryptor.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property BlobPlusEncryptedEntity_GenerateHydrator_UsesBothProviders()
    {
        return Prop.ForAll(
            BlobPlusEncryptedEntityGen().ToArbitrary(),
            entity =>
            {
                var result = HydratorGenerator.GenerateHydrator(entity);

                var isNonNull = result != null;
                var hasBlobProvider = result?.Contains("IBlobStorageProvider? blobProvider,") == true;
                var hasFieldEncryptor = result?.Contains("options?.FieldEncryptor") == true;

                return isNonNull.ToProperty()
                    .Label("GenerateHydrator() should return non-null for blob+encrypted entities")
                    .And(hasBlobProvider.ToProperty()
                        .Label("Generated hydrator should pass blobProvider"))
                    .And(hasFieldEncryptor.ToProperty()
                        .Label("Generated hydrator should pass fieldEncryptor from options"));
            });
    }
}
