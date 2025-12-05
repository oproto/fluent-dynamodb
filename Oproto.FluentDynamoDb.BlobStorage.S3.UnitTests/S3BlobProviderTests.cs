using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.BlobStorage.S3;
using System.Text;

namespace Oproto.FluentDynamoDb.BlobStorage.S3.UnitTests;

public class S3BlobProviderTests
{
    [Fact]
    public async Task StoreAsync_WithSuggestedKey_StoresDataAndReturnsKey()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");
        var data = Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(data);

        // Act
        var reference = await provider.StoreAsync(stream, "test-key");

        // Assert
        reference.Should().Be("test-key");
        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == "test-key" &&
                r.InputStream == stream),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAsync_WithoutSuggestedKey_GeneratesUniqueKey()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");
        var data = Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(data);

        // Act
        var reference = await provider.StoreAsync(stream);

        // Assert
        reference.Should().NotBeNullOrEmpty();
        Guid.TryParse(reference, out _).Should().BeTrue("generated key should be a valid GUID");
        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == reference),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAsync_WithKeyPrefix_PrependsPrefix()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket", "my-prefix");
        var data = Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(data);

        // Act
        var reference = await provider.StoreAsync(stream, "test-key");

        // Assert
        reference.Should().Be("my-prefix/test-key");
        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == "my-prefix/test-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAsync_WithKeyPrefixTrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket", "my-prefix/");
        var data = Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(data);

        // Act
        var reference = await provider.StoreAsync(stream, "test-key");

        // Assert
        reference.Should().Be("my-prefix/test-key");
        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == "my-prefix/test-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAsync_RespectsBucketNameConfiguration()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "my-custom-bucket");
        var data = Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(data);

        // Act
        await provider.StoreAsync(stream, "test-key");

        // Assert
        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.BucketName == "my-custom-bucket"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAsync_WithNullData_ThrowsArgumentNullException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.StoreAsync(null!, "test-key");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public async Task StoreAsync_WhenS3Fails_ThrowsInvalidOperationException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<PutObjectResponse>(_ => throw new AmazonS3Exception("S3 error"));
        
        var provider = new S3BlobProvider(s3Client, "test-bucket");
        var data = Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(data);

        // Act
        var act = async () => await provider.StoreAsync(stream, "test-key");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to store blob in S3*")
            .WithMessage("*test-bucket*")
            .WithMessage("*test-key*");
    }

    [Fact]
    public async Task RetrieveAsync_WithValidKey_RetrievesData()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var expectedData = Encoding.UTF8.GetBytes("retrieved data");
        var responseStream = new MemoryStream(expectedData);
        
        var response = new GetObjectResponse
        {
            ResponseStream = responseStream
        };
        
        s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var resultStream = await provider.RetrieveAsync("test-key");

        // Assert
        await s3Client.Received(1).GetObjectAsync(
            Arg.Is<GetObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == "test-key"),
            Arg.Any<CancellationToken>());
        
        using var reader = new StreamReader(resultStream);
        var content = await reader.ReadToEndAsync();
        content.Should().Be("retrieved data");
    }

    [Fact]
    public async Task RetrieveAsync_WithMissingBlob_ThrowsKeyNotFoundException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = System.Net.HttpStatusCode.NotFound
        };
        
        s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<GetObjectResponse>(_ => throw notFoundException);
        
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.RetrieveAsync("missing-key");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Blob not found in S3*")
            .WithMessage("*test-bucket*")
            .WithMessage("*missing-key*");
    }

    [Fact]
    public async Task RetrieveAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.RetrieveAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("referenceKey");
    }

    [Fact]
    public async Task RetrieveAsync_WithEmptyKey_ThrowsArgumentNullException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.RetrieveAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("referenceKey");
    }

    [Fact]
    public async Task RetrieveAsync_WhenS3FailsWithOtherError_ThrowsInvalidOperationException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<GetObjectResponse>(_ => throw new AmazonS3Exception("S3 error"));
        
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.RetrieveAsync("test-key");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to retrieve blob from S3*")
            .WithMessage("*test-bucket*")
            .WithMessage("*test-key*");
    }

    [Fact]
    public async Task DeleteAsync_WithValidKey_DeletesBlob()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        await provider.DeleteAsync("test-key");

        // Assert
        await s3Client.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == "test-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.DeleteAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("referenceKey");
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyKey_ThrowsArgumentNullException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.DeleteAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("referenceKey");
    }

    [Fact]
    public async Task DeleteAsync_WhenS3Fails_ThrowsInvalidOperationException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<DeleteObjectResponse>(_ => throw new AmazonS3Exception("S3 error"));
        
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.DeleteAsync("test-key");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to delete blob from S3*")
            .WithMessage("*test-bucket*")
            .WithMessage("*test-key*");
    }

    [Fact]
    public async Task ExistsAsync_WithExistingBlob_ReturnsTrue()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var metadata = new GetObjectMetadataResponse();
        
        s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(metadata));
        
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var exists = await provider.ExistsAsync("test-key");

        // Assert
        exists.Should().BeTrue();
        await s3Client.Received(1).GetObjectMetadataAsync(
            Arg.Is<GetObjectMetadataRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == "test-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistsAsync_WithMissingBlob_ReturnsFalse()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = System.Net.HttpStatusCode.NotFound
        };
        
        s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns<GetObjectMetadataResponse>(_ => throw notFoundException);
        
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var exists = await provider.ExistsAsync("missing-key");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.ExistsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("referenceKey");
    }

    [Fact]
    public async Task ExistsAsync_WithEmptyKey_ThrowsArgumentNullException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.ExistsAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("referenceKey");
    }

    [Fact]
    public async Task ExistsAsync_WhenS3FailsWithOtherError_ThrowsInvalidOperationException()
    {
        // Arrange
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns<GetObjectMetadataResponse>(_ => throw new AmazonS3Exception("S3 error"));
        
        var provider = new S3BlobProvider(s3Client, "test-bucket");

        // Act
        var act = async () => await provider.ExistsAsync("test-key");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to check blob existence in S3*")
            .WithMessage("*test-bucket*")
            .WithMessage("*test-key*");
    }
}
