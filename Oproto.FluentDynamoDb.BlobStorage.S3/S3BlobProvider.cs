using Amazon.S3;
using Amazon.S3.Model;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.BlobStorage.S3;

/// <summary>
/// S3 implementation of IBlobStorageProvider for storing large data in Amazon S3.
/// </summary>
public class S3BlobProvider : IBlobStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string? _keyPrefix;

    /// <summary>
    /// Initializes a new instance of S3BlobProvider.
    /// </summary>
    /// <param name="s3Client">The S3 client to use for operations</param>
    /// <param name="bucketName">The S3 bucket name where blobs will be stored</param>
    /// <param name="keyPrefix">Optional prefix to prepend to all blob keys</param>
    public S3BlobProvider(IAmazonS3 s3Client, string bucketName, string? keyPrefix = null)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        _keyPrefix = keyPrefix;
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(
        Stream data,
        string? suggestedKey = null,
        CancellationToken cancellationToken = default)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        // Generate unique key if not provided
        var key = suggestedKey ?? Guid.NewGuid().ToString();
        
        // Apply prefix if configured
        if (!string.IsNullOrEmpty(_keyPrefix))
        {
            key = $"{_keyPrefix.TrimEnd('/')}/{key}";
        }

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = data
        };

        try
        {
            await _s3Client.PutObjectAsync(request, cancellationToken);
            return key;
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to store blob in S3. Bucket: {_bucketName}, Key: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> RetrieveAsync(
        string referenceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(referenceKey))
            throw new ArgumentNullException(nameof(referenceKey));

        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = referenceKey
        };

        try
        {
            var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            
            // Copy to memory stream to avoid disposing the response stream prematurely
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException(
                $"Blob not found in S3. Bucket: {_bucketName}, Key: {referenceKey}", ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve blob from S3. Bucket: {_bucketName}, Key: {referenceKey}", ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string referenceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(referenceKey))
            throw new ArgumentNullException(nameof(referenceKey));

        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = referenceKey
        };

        try
        {
            await _s3Client.DeleteObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to delete blob from S3. Bucket: {_bucketName}, Key: {referenceKey}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string referenceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(referenceKey))
            throw new ArgumentNullException(nameof(referenceKey));

        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = referenceKey
            };

            await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to check blob existence in S3. Bucket: {_bucketName}, Key: {referenceKey}", ex);
        }
    }
}
