using System.Security.Cryptography;
using System.Text;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Mock field encryptor for integration testing.
/// Uses a simple symmetric encryption scheme with context-specific keys to simulate
/// AWS Encryption SDK behavior without requiring actual KMS access.
/// </summary>
internal sealed class MockFieldEncryptor : IFieldEncryptor
{
    private readonly Dictionary<string, byte[]> _contextKeys;
    private readonly byte[] _defaultKey;

    /// <summary>
    /// Gets the list of encryption calls made to this encryptor.
    /// Useful for verifying encryption behavior in tests.
    /// </summary>
    public List<EncryptCall> EncryptCalls { get; } = new();

    public MockFieldEncryptor(Dictionary<string, byte[]>? contextKeys = null)
    {
        _contextKeys = contextKeys ?? new Dictionary<string, byte[]>();
        _defaultKey = GenerateKey();
    }

    public Task<byte[]> EncryptAsync(
        byte[] plaintext,
        string fieldName,
        FieldEncryptionContext context,
        CancellationToken cancellationToken = default)
    {
        // Track the encryption call
        EncryptCalls.Add(new EncryptCall
        {
            Plaintext = plaintext,
            FieldName = fieldName,
            Context = context
        });

        // Get key based on context
        var key = GetKeyForContext(context.ContextId);
        
        // Create encryption context metadata
        var encryptionContext = BuildEncryptionContext(fieldName, context.ContextId);
        
        // Encrypt using AES-GCM (similar to AWS Encryption SDK)
        using var aes = new AesGcm(key);
        
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        
        // Use encryption context as additional authenticated data
        var aad = SerializeEncryptionContext(encryptionContext);
        
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        
        // Build message format: [version][nonce][tag][context_length][context][ciphertext]
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write((byte)1); // Version
        writer.Write(nonce);
        writer.Write(tag);
        writer.Write(aad.Length);
        writer.Write(aad);
        writer.Write(ciphertext);
        
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> DecryptAsync(
        byte[] ciphertext,
        string fieldName,
        FieldEncryptionContext context,
        CancellationToken cancellationToken = default)
    {
        // Get key based on context
        var key = GetKeyForContext(context.ContextId);
        
        // Parse message format
        using var ms = new MemoryStream(ciphertext);
        using var reader = new BinaryReader(ms);
        
        var version = reader.ReadByte();
        if (version != 1)
            throw new InvalidOperationException($"Unsupported message version: {version}");
        
        var nonce = reader.ReadBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = reader.ReadBytes(AesGcm.TagByteSizes.MaxSize);
        var aadLength = reader.ReadInt32();
        var aad = reader.ReadBytes(aadLength);
        var encryptedData = reader.ReadBytes((int)(ms.Length - ms.Position));
        
        // Validate encryption context
        var expectedContext = BuildEncryptionContext(fieldName, context.ContextId);
        var expectedAad = SerializeEncryptionContext(expectedContext);
        
        if (!aad.SequenceEqual(expectedAad))
        {
            throw new InvalidOperationException(
                "Encryption context mismatch. Data may have been encrypted for a different field or context.");
        }
        
        // Decrypt
        using var aes = new AesGcm(key);
        var plaintext = new byte[encryptedData.Length];
        
        aes.Decrypt(nonce, encryptedData, tag, plaintext, aad);
        
        return Task.FromResult(plaintext);
    }

    private byte[] GetKeyForContext(string? contextId)
    {
        if (contextId != null && _contextKeys.TryGetValue(contextId, out var key))
            return key;
        
        return _defaultKey;
    }

    private static Dictionary<string, string> BuildEncryptionContext(string fieldName, string? contextId)
    {
        var context = new Dictionary<string, string>
        {
            ["field"] = fieldName
        };
        
        if (!string.IsNullOrWhiteSpace(contextId))
        {
            context["context"] = contextId;
        }
        
        return context;
    }

    private static byte[] SerializeEncryptionContext(Dictionary<string, string> context)
    {
        // Simple serialization: key1=value1;key2=value2
        var serialized = string.Join(";", context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return Encoding.UTF8.GetBytes(serialized);
    }

    private static byte[] GenerateKey()
    {
        var key = new byte[32]; // 256-bit key
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// <summary>
    /// Creates a mock encryptor with different keys for different contexts.
    /// </summary>
    public static MockFieldEncryptor CreateWithContextKeys(params string[] contextIds)
    {
        var contextKeys = new Dictionary<string, byte[]>();
        foreach (var contextId in contextIds)
        {
            contextKeys[contextId] = GenerateKey();
        }
        return new MockFieldEncryptor(contextKeys);
    }
}

/// <summary>
/// Represents a call to the EncryptAsync method for test verification.
/// </summary>
internal sealed class EncryptCall
{
    public required byte[] Plaintext { get; init; }
    public required string FieldName { get; init; }
    public required FieldEncryptionContext Context { get; init; }
}
