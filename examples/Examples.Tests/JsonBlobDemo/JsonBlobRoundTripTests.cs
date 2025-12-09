using System.Text.Json;
using Amazon.DynamoDBv2;
using Examples.Shared;
using JsonBlobDemo;
using JsonBlobDemo.Entities;
using Newtonsoft.Json;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.NewtonsoftJson;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.SystemTextJson;

namespace Examples.Tests.JsonBlobDemo;

/// <summary>
/// Property-based tests for JsonBlob round-trip serialization.
/// These tests require DynamoDB Local to be running on port 8000.
/// </summary>
public class JsonBlobRoundTripTests
{
    private const string TestTableName = "json-blob-demo-test";

    /// <summary>
    /// **Feature: v0.9.0-enhancements, Property 1: JsonBlob Round-Trip Consistency**
    /// **Validates: Requirements 2.5, 2.6**
    /// 
    /// For any valid DocumentMetadata object, storing it as a JsonBlob property and then
    /// retrieving it should produce an equivalent object.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property JsonBlob_RoundTrip_WithSystemTextJsonAot_PreservesData()
    {
        return Prop.ForAll(
            GenerateDocumentMetadata(),
            metadata =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    
                    // Use System.Text.Json with AOT context
                    var options = new FluentDynamoDbOptions()
                        .WithSystemTextJson(DocumentJsonContext.Default);
                    var table = new JsonBlobDemoTable(client, TestTableName, options);

                    var documentId = Guid.NewGuid().ToString();
                    var document = new Document
                    {
                        Id = documentId,
                        Title = "Test Document",
                        CreatedAt = DateTime.UtcNow,
                        Metadata = metadata
                    };

                    // Store the document - use ToDynamoDb with options to serialize JsonBlob
                    var item = Document.ToDynamoDb(document, options);
                    table.Documents.Put(item).PutAsync().GetAwaiter().GetResult();

                    // Retrieve the document
                    var response = table.Documents.Get(documentId).ToDynamoDbResponseAsync().GetAwaiter().GetResult();
                    var retrieved = response.Item != null ? Document.FromDynamoDb<Document>(response.Item, options) : null;

                    // Clean up
                    table.Documents.DeleteAsync(documentId).GetAwaiter().GetResult();

                    if (retrieved == null)
                    {
                        return false.ToProperty().Label("Retrieved document was null");
                    }

                    var metadataMatches = AreMetadataEqual(metadata, retrieved.Metadata);

                    return metadataMatches.ToProperty()
                        .Label($"MetadataMatches: {metadataMatches}, " +
                               $"Author: {metadata.Author} == {retrieved.Metadata.Author}, " +
                               $"Tags: {metadata.Tags.Count} == {retrieved.Metadata.Tags.Count}");
                }
                catch (Amazon.DynamoDBv2.AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: v0.9.0-enhancements, Property 1: JsonBlob Round-Trip Consistency**
    /// **Validates: Requirements 2.5, 2.6**
    /// 
    /// For any valid DocumentMetadata object, storing it with System.Text.Json reflection mode
    /// and then retrieving it should produce an equivalent object.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property JsonBlob_RoundTrip_WithSystemTextJsonReflection_PreservesData()
    {
        return Prop.ForAll(
            GenerateDocumentMetadata(),
            metadata =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    
                    // Use System.Text.Json with reflection
                    var options = new FluentDynamoDbOptions()
                        .WithSystemTextJson(new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                    var table = new JsonBlobDemoTable(client, TestTableName, options);

                    var documentId = Guid.NewGuid().ToString();
                    var document = new Document
                    {
                        Id = documentId,
                        Title = "Test Document",
                        CreatedAt = DateTime.UtcNow,
                        Metadata = metadata
                    };

                    // Store the document - use ToDynamoDb with options to serialize JsonBlob
                    var item = Document.ToDynamoDb(document, options);
                    table.Documents.Put(item).PutAsync().GetAwaiter().GetResult();

                    // Retrieve the document
                    var response = table.Documents.Get(documentId).ToDynamoDbResponseAsync().GetAwaiter().GetResult();
                    var retrieved = response.Item != null ? Document.FromDynamoDb<Document>(response.Item, options) : null;

                    // Clean up
                    table.Documents.DeleteAsync(documentId).GetAwaiter().GetResult();

                    if (retrieved == null)
                    {
                        return false.ToProperty().Label("Retrieved document was null");
                    }

                    var metadataMatches = AreMetadataEqual(metadata, retrieved.Metadata);

                    return metadataMatches.ToProperty()
                        .Label($"MetadataMatches: {metadataMatches}");
                }
                catch (Amazon.DynamoDBv2.AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: v0.9.0-enhancements, Property 1: JsonBlob Round-Trip Consistency**
    /// **Validates: Requirements 2.5, 2.6**
    /// 
    /// For any valid DocumentMetadata object, storing it with Newtonsoft.Json
    /// and then retrieving it should produce an equivalent object.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property JsonBlob_RoundTrip_WithNewtonsoftJson_PreservesData()
    {
        return Prop.ForAll(
            GenerateDocumentMetadata(),
            metadata =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    
                    // Use Newtonsoft.Json
                    var options = new FluentDynamoDbOptions()
                        .WithNewtonsoftJson(new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });
                    var table = new JsonBlobDemoTable(client, TestTableName, options);

                    var documentId = Guid.NewGuid().ToString();
                    var document = new Document
                    {
                        Id = documentId,
                        Title = "Test Document",
                        CreatedAt = DateTime.UtcNow,
                        Metadata = metadata
                    };

                    // Store the document - use ToDynamoDb with options to serialize JsonBlob
                    var item = Document.ToDynamoDb(document, options);
                    table.Documents.Put(item).PutAsync().GetAwaiter().GetResult();

                    // Retrieve the document
                    var response = table.Documents.Get(documentId).ToDynamoDbResponseAsync().GetAwaiter().GetResult();
                    var retrieved = response.Item != null ? Document.FromDynamoDb<Document>(response.Item, options) : null;

                    // Clean up
                    table.Documents.DeleteAsync(documentId).GetAwaiter().GetResult();

                    if (retrieved == null)
                    {
                        return false.ToProperty().Label("Retrieved document was null");
                    }

                    var metadataMatches = AreMetadataEqual(metadata, retrieved.Metadata);

                    return metadataMatches.ToProperty()
                        .Label($"MetadataMatches: {metadataMatches}");
                }
                catch (Amazon.DynamoDBv2.AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    #region Helper Methods

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

    /// <summary>
    /// Compares two DocumentMetadata objects for equality.
    /// </summary>
    private static bool AreMetadataEqual(DocumentMetadata expected, DocumentMetadata actual)
    {
        if (expected.Author != actual.Author)
            return false;

        if (!expected.Tags.SequenceEqual(actual.Tags))
            return false;

        if (expected.CustomFields.Count != actual.CustomFields.Count)
            return false;

        if (!expected.CustomFields.All(kvp => 
            actual.CustomFields.TryGetValue(kvp.Key, out var value) && value == kvp.Value))
            return false;

        if (expected.AdditionalInfo == null && actual.AdditionalInfo == null)
            return true;

        if (expected.AdditionalInfo == null || actual.AdditionalInfo == null)
            return false;

        return expected.AdditionalInfo.Category == actual.AdditionalInfo.Category &&
               expected.AdditionalInfo.Priority == actual.AdditionalInfo.Priority;
    }

    /// <summary>
    /// Generates random DocumentMetadata objects for property testing.
    /// </summary>
    private static Arbitrary<DocumentMetadata> GenerateDocumentMetadata()
    {
        var authorGen = Gen.Elements("Alice", "Bob", "Charlie", "Diana", "Eve", "Frank");
        var tagGen = Gen.Elements("important", "draft", "review", "final", "archived", "public", "private");
        var categoryGen = Gen.Elements("Technical", "Business", "Personal", "Legal", "Marketing");
        var keyGen = Gen.Elements("department", "project", "status", "region", "team");
        var valueGen = Gen.Elements("engineering", "alpha", "active", "us-west", "backend");

        return Arb.From(
            from author in authorGen
            from tagCount in Gen.Choose(0, 5)
            from tags in Gen.ArrayOf(tagCount, tagGen)
            from fieldCount in Gen.Choose(0, 3)
            from keys in Gen.ArrayOf(fieldCount, keyGen)
            from values in Gen.ArrayOf(fieldCount, valueGen)
            from hasAdditionalInfo in Gen.Elements(true, false)
            from category in categoryGen
            from priority in Gen.Choose(1, 5)
            select new DocumentMetadata
            {
                Author = author,
                Tags = tags.Distinct().ToList(),
                CustomFields = keys.Zip(values).DistinctBy(x => x.First)
                    .ToDictionary(x => x.First, x => x.Second),
                AdditionalInfo = hasAdditionalInfo ? new NestedInfo
                {
                    Category = category,
                    Priority = priority
                } : null
            });
    }

    #endregion
}
