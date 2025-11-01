using Microsoft.CodeAnalysis;
using System.Linq;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Detects blob provider package references.
/// </summary>
internal class BlobProviderDetector
{
    /// <summary>
    /// Detects which blob provider packages are referenced in the compilation.
    /// </summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <returns>Information about the detected blob provider configuration.</returns>
    public static BlobProviderInfo DetectBlobProvider(Compilation compilation)
    {
        var info = new BlobProviderInfo();

        // Check for S3 blob provider package reference
        info.HasS3BlobProvider = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.BlobStorage.S3", StringComparison.OrdinalIgnoreCase));

        // Check for any custom blob provider packages
        // Custom providers would implement IBlobStorageProvider from the core library
        info.HasAnyBlobProvider = info.HasS3BlobProvider;

        return info;
    }
}

/// <summary>
/// Information about detected blob provider configuration.
/// </summary>
internal class BlobProviderInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether Oproto.FluentDynamoDb.BlobStorage.S3 is referenced.
    /// </summary>
    public bool HasS3BlobProvider { get; set; }

    /// <summary>
    /// Gets a value indicating whether any blob provider is available.
    /// </summary>
    public bool HasAnyBlobProvider { get; set; }
}
