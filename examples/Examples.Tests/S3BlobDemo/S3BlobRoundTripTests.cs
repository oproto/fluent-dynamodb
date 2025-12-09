using Amazon.DynamoDBv2;
using Amazon.S3;
using Examples.Shared;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.BlobStorage.S3;
using Oproto.FluentDynamoDb.Requests.Extensions;
using S3BlobDemo.Entities;

namespace Examples.Tests.S3BlobDemo;

/// <summary>
/// Property-based tests for S3 blob round-trip storage.
/// These tests require both DynamoDB Local and S3 access to be available.
/// 
/// To run these tests, set the following environment variables:
/// - S3_TEST_BUCKET: The S3 bucket name to use for testing
/// - AWS_PROFILE (optional): The AWS profile to use for credentials
/// </summary>
public class S3BlobRoundTripTests
{
    private const string TestTableName = "s3-blob-demo-test";
    private const string TestKeyPrefix = "test-blobs/";

    /// <summary>
    /// **Feature: v0.9.0-enhancements, Property 2: S3 Blob Round-Trip Consistency**
    /// **Validates: Requirements 3.3, 3.4**
    /// 
    /// For any valid binary data stored via BlobReference, uploading to S3 and then
    /// downloading should return identical data.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property S3Blob_RoundTrip_PreservesData()
    {
        return Prop.ForAll(
            GenerateBinaryData(),
            data =>
            {
                var bucketName = Environment.GetEnvironmentVariable("S3_TEST_BUCKET");
                if (string.IsNullOrEmpty(bucketName))
                {
                    return true.ToProperty().Label("Skipped: S3_TEST_BUCKET environment variable not set");
                }

                IAmazonDynamoDB? dynamoClient = null;
                IAmazonS3? s3Client = null;
                string? storedKey = null;

                try
                {
                    // Create clients
                    dynamoClient = DynamoDbSetup.CreateLocalClient();
                    s3Client = CreateS3Client();

                    // Ensure DynamoDB table exists
                    EnsureTestTableExists(dynamoClient);

                    // Create blob provider
                    var blobProvider = new S3BlobProvider(s3Client, bucketName, TestKeyPrefix);

                    // Store data in S3
                    var mediaId = Guid.NewGuid().ToString();
                    var s3Key = $"{mediaId}.bin";

                    using (var uploadStream = new MemoryStream(data))
                    {
                        storedKey = blobProvider.StoreAsync(uploadStream, s3Key).GetAwaiter().GetResult();
                    }

                    // Retrieve data from S3
                    byte[] retrievedData;
                    using (var downloadStream = blobProvider.RetrieveAsync(storedKey).GetAwaiter().GetResult())
                    using (var memoryStream = new MemoryStream())
                    {
                        downloadStream.CopyTo(memoryStream);
                        retrievedData = memoryStream.ToArray();
                    }

                    // Clean up S3 object
                    blobProvider.DeleteAsync(storedKey).GetAwaiter().GetResult();
                    storedKey = null;

                    // Verify data matches
                    var dataMatches = data.SequenceEqual(retrievedData);

                    return dataMatches.ToProperty()
                        .Label($"DataMatches: {dataMatches}, " +
                               $"OriginalSize: {data.Length}, " +
                               $"RetrievedSize: {retrievedData.Length}");
                }
                catch (Amazon.DynamoDBv2.AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                catch (Amazon.S3.AmazonS3Exception ex) when (IsS3ConnectionError(ex))
                {
                    return true.ToProperty().Label($"Skipped: S3 not accessible - {ex.ErrorCode}");
                }
                finally
                {
                    // Clean up S3 object if test failed before cleanup
                    if (storedKey != null && s3Client != null)
                    {
                        try
                        {
                            var blobProvider = new S3BlobProvider(s3Client, bucketName!, TestKeyPrefix);
                            blobProvider.DeleteAsync(storedKey).GetAwaiter().GetResult();
                        }
                        catch { /* Ignore cleanup errors */ }
                    }

                    dynamoClient?.Dispose();
                    s3Client?.Dispose();
                }
            });
    }


    /// <summary>
    /// **Feature: v0.9.0-enhancements, Property 2: S3 Blob Round-Trip Consistency**
    /// **Validates: Requirements 3.3, 3.4**
    /// 
    /// For any valid MediaItem entity with blob data, storing in DynamoDB with S3 reference
    /// and then retrieving should return identical blob data.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property S3Blob_EntityRoundTrip_PreservesData()
    {
        return Prop.ForAll(
            GenerateMediaItemData(),
            testData =>
            {
                var bucketName = Environment.GetEnvironmentVariable("S3_TEST_BUCKET");
                if (string.IsNullOrEmpty(bucketName))
                {
                    return true.ToProperty().Label("Skipped: S3_TEST_BUCKET environment variable not set");
                }

                IAmazonDynamoDB? dynamoClient = null;
                IAmazonS3? s3Client = null;
                string? storedKey = null;
                string? mediaId = null;

                try
                {
                    // Create clients
                    dynamoClient = DynamoDbSetup.CreateLocalClient();
                    s3Client = CreateS3Client();

                    // Ensure DynamoDB table exists
                    EnsureTestTableExists(dynamoClient);

                    // Create blob provider and options
                    var blobProvider = new S3BlobProvider(s3Client, bucketName, TestKeyPrefix);
                    var options = new FluentDynamoDbOptions()
                        .WithBlobStorage(blobProvider);

                    var table = new S3BlobDemoTable(dynamoClient, TestTableName, options);

                    // Create media item
                    mediaId = Guid.NewGuid().ToString();
                    var s3Key = $"{mediaId}.bin";

                    // Store blob in S3
                    using (var uploadStream = new MemoryStream(testData.Data))
                    {
                        storedKey = blobProvider.StoreAsync(uploadStream, s3Key).GetAwaiter().GetResult();
                    }

                    // Create and store media item in DynamoDB
                    var mediaItem = new MediaItem
                    {
                        Id = mediaId,
                        Name = testData.Name,
                        ContentType = testData.ContentType,
                        DataReference = storedKey,
                        SizeBytes = testData.Data.Length,
                        UploadedAt = DateTime.UtcNow,
                        Description = testData.Description
                    };

                    table.MediaItems.PutAsync(mediaItem).GetAwaiter().GetResult();

                    // Retrieve media item from DynamoDB
                    var response = table.MediaItems.Get(mediaId).ToDynamoDbResponseAsync().GetAwaiter().GetResult();
                    var retrieved = response.Item != null 
                        ? MediaItem.FromDynamoDb<MediaItem>(response.Item, options) 
                        : null;

                    if (retrieved == null)
                    {
                        return false.ToProperty().Label("Retrieved media item was null");
                    }

                    // Retrieve blob data from S3
                    byte[] retrievedData;
                    using (var downloadStream = blobProvider.RetrieveAsync(retrieved.DataReference).GetAwaiter().GetResult())
                    using (var memoryStream = new MemoryStream())
                    {
                        downloadStream.CopyTo(memoryStream);
                        retrievedData = memoryStream.ToArray();
                    }

                    // Clean up
                    blobProvider.DeleteAsync(storedKey).GetAwaiter().GetResult();
                    storedKey = null;
                    table.MediaItems.DeleteAsync(mediaId).GetAwaiter().GetResult();
                    mediaId = null;

                    // Verify data matches
                    var dataMatches = testData.Data.SequenceEqual(retrievedData);
                    var metadataMatches = retrieved.Name == testData.Name &&
                                          retrieved.ContentType == testData.ContentType &&
                                          retrieved.SizeBytes == testData.Data.Length;

                    return (dataMatches && metadataMatches).ToProperty()
                        .Label($"DataMatches: {dataMatches}, MetadataMatches: {metadataMatches}, " +
                               $"OriginalSize: {testData.Data.Length}, RetrievedSize: {retrievedData.Length}");
                }
                catch (Amazon.DynamoDBv2.AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                catch (Amazon.S3.AmazonS3Exception ex) when (IsS3ConnectionError(ex))
                {
                    return true.ToProperty().Label($"Skipped: S3 not accessible - {ex.ErrorCode}");
                }
                finally
                {
                    // Clean up S3 object if test failed before cleanup
                    if (storedKey != null && s3Client != null)
                    {
                        try
                        {
                            var blobProvider = new S3BlobProvider(s3Client, bucketName!, TestKeyPrefix);
                            blobProvider.DeleteAsync(storedKey).GetAwaiter().GetResult();
                        }
                        catch { /* Ignore cleanup errors */ }
                    }

                    // Clean up DynamoDB item if test failed before cleanup
                    if (mediaId != null && dynamoClient != null)
                    {
                        try
                        {
                            var table = new S3BlobDemoTable(dynamoClient, TestTableName, new FluentDynamoDbOptions());
                            table.MediaItems.DeleteAsync(mediaId).GetAwaiter().GetResult();
                        }
                        catch { /* Ignore cleanup errors */ }
                    }

                    dynamoClient?.Dispose();
                    s3Client?.Dispose();
                }
            });
    }

    #region Helper Methods

    private static IAmazonS3 CreateS3Client()
    {
        var profileName = Environment.GetEnvironmentVariable("AWS_PROFILE");
        if (!string.IsNullOrEmpty(profileName))
        {
            var chain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();
            if (chain.TryGetAWSCredentials(profileName, out var credentials))
            {
                return new AmazonS3Client(credentials);
            }
        }
        return new AmazonS3Client();
    }

    private static void EnsureTestTableExists(IAmazonDynamoDB client)
    {
        DynamoDbSetup.EnsureTableExistsAsync(client, TestTableName, "pk").GetAwaiter().GetResult();
    }

    private static bool IsDynamoDbConnectionError(Amazon.DynamoDBv2.AmazonDynamoDBException ex)
    {
        return ex.Message.Contains("Unable to connect") ||
               ex.Message.Contains("Connection refused") ||
               ex.Message.Contains("No connection could be made");
    }

    private static bool IsS3ConnectionError(Amazon.S3.AmazonS3Exception ex)
    {
        return ex.ErrorCode == "NoSuchBucket" ||
               ex.ErrorCode == "AccessDenied" ||
               ex.ErrorCode == "InvalidAccessKeyId" ||
               ex.Message.Contains("Unable to connect") ||
               ex.Message.Contains("Connection refused");
    }

    /// <summary>
    /// Generates random binary data for property testing.
    /// </summary>
    private static Arbitrary<byte[]> GenerateBinaryData()
    {
        return Arb.From(
            from size in Gen.Choose(1, 10000)  // 1 byte to 10KB
            from data in Gen.ArrayOf(size, Arb.Generate<byte>())
            select data);
    }

    /// <summary>
    /// Generates random media item test data for property testing.
    /// </summary>
    private static Arbitrary<MediaItemTestData> GenerateMediaItemData()
    {
        var nameGen = Gen.Elements("document.pdf", "image.png", "data.json", "report.xlsx", "backup.zip");
        var contentTypeGen = Gen.Elements("application/pdf", "image/png", "application/json", 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/zip");
        var descriptionGen = Gen.Elements<string?>("Test file", "Important document", "Backup data", null);

        return Arb.From(
            from name in nameGen
            from contentType in contentTypeGen
            from description in descriptionGen
            from size in Gen.Choose(100, 5000)  // 100 bytes to 5KB for faster tests
            from data in Gen.ArrayOf(size, Arb.Generate<byte>())
            select new MediaItemTestData
            {
                Name = name,
                ContentType = contentType,
                Description = description,
                Data = data
            });
    }

    #endregion

    /// <summary>
    /// Test data class for media item property tests.
    /// </summary>
    private class MediaItemTestData
    {
        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
