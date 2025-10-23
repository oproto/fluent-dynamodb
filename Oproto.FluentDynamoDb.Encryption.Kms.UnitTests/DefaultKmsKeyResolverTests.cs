namespace Oproto.FluentDynamoDb.Encryption.Kms.UnitTests;

public class DefaultKmsKeyResolverTests
{
    private const string DefaultKeyArn = "arn:aws:kms:us-east-1:123456789012:key/default-key-id";
    private const string TenantAKeyArn = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key-id";
    private const string TenantBKeyArn = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key-id";

    [Fact]
    public void Constructor_WithValidDefaultKey_Succeeds()
    {
        // Act
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn);

        // Assert
        resolver.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDefaultKey_ThrowsArgumentException()
    {
        // Act
        var act = () => new DefaultKmsKeyResolver(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("defaultKeyId");
    }

    [Fact]
    public void Constructor_WithEmptyDefaultKey_ThrowsArgumentException()
    {
        // Act
        var act = () => new DefaultKmsKeyResolver(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("defaultKeyId");
    }

    [Fact]
    public void Constructor_WithWhitespaceDefaultKey_ThrowsArgumentException()
    {
        // Act
        var act = () => new DefaultKmsKeyResolver("   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("defaultKeyId");
    }

    [Fact]
    public void ResolveKeyId_WithNullContext_ReturnsDefaultKey()
    {
        // Arrange
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn);

        // Act
        var result = resolver.ResolveKeyId(null);

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public void ResolveKeyId_WithNullContextMap_ReturnsDefaultKey()
    {
        // Arrange
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap: null);

        // Act
        var result = resolver.ResolveKeyId("tenant-a");

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public void ResolveKeyId_WithContextNotInMap_ReturnsDefaultKey()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act
        var result = resolver.ResolveKeyId("tenant-unknown");

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public void ResolveKeyId_WithContextInMap_ReturnsContextSpecificKey()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn,
            ["tenant-b"] = TenantBKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act
        var result = resolver.ResolveKeyId("tenant-a");

        // Assert
        result.Should().Be(TenantAKeyArn);
    }

    [Fact]
    public void ResolveKeyId_WithMultipleContexts_ReturnsCorrectKeys()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn,
            ["tenant-b"] = TenantBKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act & Assert
        resolver.ResolveKeyId("tenant-a").Should().Be(TenantAKeyArn);
        resolver.ResolveKeyId("tenant-b").Should().Be(TenantBKeyArn);
        resolver.ResolveKeyId("tenant-c").Should().Be(DefaultKeyArn);
        resolver.ResolveKeyId(null).Should().Be(DefaultKeyArn);
    }

    [Fact]
    public void ResolveKeyId_IsCaseSensitive()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act
        var resultLowercase = resolver.ResolveKeyId("tenant-a");
        var resultUppercase = resolver.ResolveKeyId("TENANT-A");

        // Assert
        resultLowercase.Should().Be(TenantAKeyArn);
        resultUppercase.Should().Be(DefaultKeyArn); // Not found, returns default
    }

    [Fact]
    public void ResolveKeyId_WithEmptyContextMap_ReturnsDefaultKey()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>();
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act
        var result = resolver.ResolveKeyId("tenant-a");

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public void ResolveKeyId_IsThreadSafe()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn,
            ["tenant-b"] = TenantBKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);
        var tasks = new List<Task<string>>();

        // Act - Call from multiple threads
        for (int i = 0; i < 100; i++)
        {
            var contextId = i % 2 == 0 ? "tenant-a" : "tenant-b";
            tasks.Add(Task.Run(() => resolver.ResolveKeyId(contextId)));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - All results should be correct
        var results = tasks.Select(t => t.Result).ToList();
        results.Count(r => r == TenantAKeyArn).Should().Be(50);
        results.Count(r => r == TenantBKeyArn).Should().Be(50);
    }
}
