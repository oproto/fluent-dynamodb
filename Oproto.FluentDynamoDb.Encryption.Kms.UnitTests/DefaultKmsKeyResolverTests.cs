namespace Oproto.FluentDynamoDb.Encryption.Kms.UnitTests;

public class DefaultKmsKeyResolverTests
{
    private const string DefaultKeyArn = "arn:aws:kms:us-east-1:123456789012:key/default-key-id";
    private const string TenantAKeyArn = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key-id";
    private const string TenantBKeyArn = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key-id";
    private const string PiiKeyArn = "arn:aws:kms:us-east-1:123456789012:key/pii-key-id";
    private const string FinancialKeyArn = "arn:aws:kms:us-east-1:123456789012:key/financial-key-id";

    #region Constructor Tests

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
    public void Constructor_WithNullAliasKeyMap_Succeeds()
    {
        // Act
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap: null, aliasKeyMap: null);

        // Assert
        resolver.Should().NotBeNull();
    }

    #endregion

    #region Context Map Tests (existing, converted to async)

    [Fact]
    public async Task ResolveKeyIdAsync_WithNullContext_ReturnsDefaultKey()
    {
        // Arrange
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn);

        // Act
        var result = await resolver.ResolveKeyIdAsync(null);

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_WithNullContextMap_ReturnsDefaultKey()
    {
        // Arrange
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap: null);

        // Act
        var result = await resolver.ResolveKeyIdAsync("tenant-a");

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_WithContextNotInMap_ReturnsDefaultKey()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act
        var result = await resolver.ResolveKeyIdAsync("tenant-unknown");

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_WithContextInMap_ReturnsContextSpecificKey()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn,
            ["tenant-b"] = TenantBKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act
        var result = await resolver.ResolveKeyIdAsync("tenant-a");

        // Assert
        result.Should().Be(TenantAKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_WithMultipleContexts_ReturnsCorrectKeys()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn,
            ["tenant-b"] = TenantBKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act & Assert
        (await resolver.ResolveKeyIdAsync("tenant-a")).Should().Be(TenantAKeyArn);
        (await resolver.ResolveKeyIdAsync("tenant-b")).Should().Be(TenantBKeyArn);
        (await resolver.ResolveKeyIdAsync("tenant-c")).Should().Be(DefaultKeyArn);
        (await resolver.ResolveKeyIdAsync(null)).Should().Be(DefaultKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_ContextMap_IsCaseSensitive()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act
        var resultLowercase = await resolver.ResolveKeyIdAsync("tenant-a");
        var resultUppercase = await resolver.ResolveKeyIdAsync("TENANT-A");

        // Assert
        resultLowercase.Should().Be(TenantAKeyArn);
        resultUppercase.Should().Be(DefaultKeyArn); // Not found, returns default
    }

    [Fact]
    public async Task ResolveKeyIdAsync_WithEmptyContextMap_ReturnsDefaultKey()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>();
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap);

        // Act
        var result = await resolver.ResolveKeyIdAsync("tenant-a");

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_IsThreadSafe()
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
            tasks.Add(Task.Run(async () => await resolver.ResolveKeyIdAsync(contextId)));
        }

        await Task.WhenAll(tasks);

        // Assert - All results should be correct
        var results = tasks.Select(t => t.Result).ToList();
        results.Count(r => r == TenantAKeyArn).Should().Be(50);
        results.Count(r => r == TenantBKeyArn).Should().Be(50);
    }

    #endregion

    #region Alias Map Tests

    [Fact]
    public async Task ResolveKeyIdAsync_WithAliasInMap_ReturnsAliasKey()
    {
        // Arrange
        var aliasKeyMap = new Dictionary<string, string>
        {
            ["pii"] = PiiKeyArn,
            ["financial"] = FinancialKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, aliasKeyMap: aliasKeyMap);

        // Act
        var result = await resolver.ResolveKeyIdAsync(contextId: null, keyAlias: "pii");

        // Assert
        result.Should().Be(PiiKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_WithAliasNotInMap_FallsThrough_ToContextMap()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn
        };
        var aliasKeyMap = new Dictionary<string, string>
        {
            ["pii"] = PiiKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap, aliasKeyMap);

        // Act
        var result = await resolver.ResolveKeyIdAsync(contextId: "tenant-a", keyAlias: "unknown-alias");

        // Assert
        result.Should().Be(TenantAKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_BothMapsMiss_ReturnsDefault()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn
        };
        var aliasKeyMap = new Dictionary<string, string>
        {
            ["pii"] = PiiKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap, aliasKeyMap);

        // Act
        var result = await resolver.ResolveKeyIdAsync(contextId: "unknown-tenant", keyAlias: "unknown-alias");

        // Assert
        result.Should().Be(DefaultKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_AliasTakesPriorityOverContext()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = TenantAKeyArn
        };
        var aliasKeyMap = new Dictionary<string, string>
        {
            ["pii"] = PiiKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, contextKeyMap, aliasKeyMap);

        // Act - both alias and context are in their respective maps
        var result = await resolver.ResolveKeyIdAsync(contextId: "tenant-a", keyAlias: "pii");

        // Assert - alias wins
        result.Should().Be(PiiKeyArn);
    }

    [Fact]
    public async Task ResolveKeyIdAsync_AliasMap_IsCaseSensitive()
    {
        // Arrange
        var aliasKeyMap = new Dictionary<string, string>
        {
            ["pii"] = PiiKeyArn
        };
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn, aliasKeyMap: aliasKeyMap);

        // Act
        var resultLowercase = await resolver.ResolveKeyIdAsync(contextId: null, keyAlias: "pii");
        var resultUppercase = await resolver.ResolveKeyIdAsync(contextId: null, keyAlias: "PII");
        var resultMixed = await resolver.ResolveKeyIdAsync(contextId: null, keyAlias: "Pii");

        // Assert
        resultLowercase.Should().Be(PiiKeyArn);
        resultUppercase.Should().Be(DefaultKeyArn); // Not found, returns default
        resultMixed.Should().Be(DefaultKeyArn); // Not found, returns default
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task ResolveKeyIdAsync_WithPreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var resolver = new DefaultKmsKeyResolver(DefaultKeyArn);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await resolver.ResolveKeyIdAsync("tenant-a", cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}
