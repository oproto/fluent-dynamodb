using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Bug condition exploration test for BlobData internal methods issue.
/// 
/// The source generator emits code that directly calls internal methods on BlobData&lt;T&gt;
/// (FromReferenceKey, GetPendingValue, SetReferenceKey). Since generated code executes
/// in the consuming assembly, external NuGet consumers cannot access these internal members.
///
/// This test verifies that the generated code uses public BlobDataOperations helper methods
/// instead of calling internal methods directly. On UNFIXED code, this test is EXPECTED TO FAIL,
/// confirming the bug exists.
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3**
/// </summary>
public class BlobDataInternalMethodsBugConditionTests
{
    /// <summary>
    /// Creates an EntityModel with a BlobData&lt;byte[]&gt; property configured for blob storage.
    /// This simulates an entity that would trigger the bug condition.
    /// </summary>
    private static EntityModel CreateBlobStorageEntity()
    {
        return new EntityModel
        {
            ClassName = "TestBlobEntity",
            Namespace = "TestNamespace",
            TableName = "test-blob-table",
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
                    PropertyName = "Content",
                    AttributeName = "content",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        PropertyName = "Content",
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]",
                        BlobStorageLazyLoad = true
                    }
                }
            }
        };
    }

    [Fact]
    public void GeneratedCode_ShouldNotContain_DirectFromReferenceKeyCall()
    {
        // Arrange
        var entity = CreateBlobStorageEntity();
        var innerType = "byte[]";

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code must NOT call internal BlobData<T>.FromReferenceKey directly
        generatedSource.Should().NotContain(
            $"BlobData<{innerType}>.FromReferenceKey(",
            "Generated code should not call internal BlobData<T>.FromReferenceKey() directly — " +
            "external consuming assemblies cannot access internal methods");
    }

    [Fact]
    public void GeneratedCode_ShouldNotContain_DirectGetPendingValueCall()
    {
        // Arrange
        var entity = CreateBlobStorageEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code must NOT call internal .GetPendingValue() directly
        generatedSource.Should().NotContain(
            ".GetPendingValue()",
            "Generated code should not call internal .GetPendingValue() directly — " +
            "external consuming assemblies cannot access internal methods");
    }

    [Fact]
    public void GeneratedCode_ShouldNotContain_DirectSetReferenceKeyCall()
    {
        // Arrange
        var entity = CreateBlobStorageEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code must NOT call internal .SetReferenceKey( directly
        generatedSource.Should().NotContain(
            ".SetReferenceKey(",
            "Generated code should not call internal .SetReferenceKey() directly — " +
            "external consuming assemblies cannot access internal methods");
    }

    [Fact]
    public void GeneratedCode_ShouldContain_PublicBlobDataOperationsCreateFromReferenceKey()
    {
        // Arrange
        var entity = CreateBlobStorageEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code MUST use public BlobDataOperations.CreateFromReferenceKey<T>
        generatedSource.Should().Contain(
            "BlobDataOperations.CreateFromReferenceKey<",
            "Generated code should call public BlobDataOperations.CreateFromReferenceKey<T>() " +
            "instead of internal BlobData<T>.FromReferenceKey()");
    }

    [Fact]
    public void GeneratedCode_ShouldContain_PublicBlobDataOperationsGetBlobPendingValue()
    {
        // Arrange
        var entity = CreateBlobStorageEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code MUST use public BlobDataOperations.GetBlobPendingValue(
        generatedSource.Should().Contain(
            "BlobDataOperations.GetBlobPendingValue(",
            "Generated code should call public BlobDataOperations.GetBlobPendingValue() " +
            "instead of internal .GetPendingValue()");
    }

    [Fact]
    public void GeneratedCode_ShouldContain_PublicBlobDataOperationsSetBlobReferenceKey()
    {
        // Arrange
        var entity = CreateBlobStorageEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code MUST use public BlobDataOperations.SetBlobReferenceKey(
        generatedSource.Should().Contain(
            "BlobDataOperations.SetBlobReferenceKey(",
            "Generated code should call public BlobDataOperations.SetBlobReferenceKey() " +
            "instead of internal .SetReferenceKey()");
    }
}
