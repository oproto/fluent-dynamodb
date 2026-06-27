using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Detects and validates the schema version attribute from the consumer compilation.
/// </summary>
internal static class SchemaVersionProvider
{
    private const string SchemaVersionAttributeFullName =
        "Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersionAttribute";

    /// <summary>
    /// Result of schema version detection.
    /// </summary>
    internal readonly struct DetectionResult
    {
        public SchemaVersion Version { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public bool ShouldHaltGeneration { get; }
        public Location? AttributeLocation { get; }

        public DetectionResult(
            SchemaVersion version,
            IReadOnlyList<Diagnostic> diagnostics,
            bool shouldHaltGeneration,
            Location? attributeLocation)
        {
            Version = version;
            Diagnostics = diagnostics;
            ShouldHaltGeneration = shouldHaltGeneration;
            AttributeLocation = attributeLocation;
        }
    }

    /// <summary>
    /// Detects the schema version from assembly-level attributes in the compilation.
    /// </summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <returns>A <see cref="DetectionResult"/> containing the resolved version and any diagnostics.</returns>
    public static DetectionResult Detect(Compilation compilation)
    {
        var diagnostics = new List<Diagnostic>();
        var shouldHalt = false;

        // Find all assembly-level FluentDynamoDbSchemaVersion attributes
        var schemaVersionAttributes = compilation.Assembly.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == SchemaVersionAttributeFullName)
            .ToImmutableArray();

        // Handle missing attribute: default version + FDDB110 warning
        if (schemaVersionAttributes.Length == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MissingSchemaVersionAttribute,
                Location.None));

            return new DetectionResult(
                SchemaVersionConstants.Default,
                diagnostics,
                false,
                null);
        }

        // Handle multiple attributes: FDDB116 error, halt generation
        var firstAttribute = schemaVersionAttributes[0];
        var attributeLocation = firstAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();

        if (schemaVersionAttributes.Length > 1)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MultipleSchemaVersionAttributes,
                attributeLocation ?? Location.None));

            return new DetectionResult(
                SchemaVersionConstants.Default,
                diagnostics,
                true,
                attributeLocation);
        }

        // Extract major and minor from constructor arguments
        var declaredMajor = firstAttribute.ConstructorArguments.Length > 0
            ? (int)(firstAttribute.ConstructorArguments[0].Value ?? 0)
            : 0;
        var declaredMinor = firstAttribute.ConstructorArguments.Length > 1
            ? (int)(firstAttribute.ConstructorArguments[1].Value ?? 0)
            : 0;

        // Validate major >= 1 (else FDDB114)
        if (declaredMajor < 1)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.SchemaVersionMajorTooLow,
                attributeLocation ?? Location.None,
                declaredMajor));
            shouldHalt = true;
        }

        // Validate minor >= 0 (else FDDB115)
        if (declaredMinor < 0)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.SchemaVersionMinorTooLow,
                attributeLocation ?? Location.None,
                declaredMinor));
            shouldHalt = true;
        }

        // If validation failed, return early with halt
        if (shouldHalt)
        {
            return new DetectionResult(
                new SchemaVersion(declaredMajor, declaredMinor),
                diagnostics,
                true,
                attributeLocation);
        }

        var declaredVersion = new SchemaVersion(declaredMajor, declaredMinor);

        // Compare against MinimumSupported (FDDB111 if below)
        if (declaredVersion < SchemaVersionConstants.MinimumSupported)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.DeclaredVersionBelowMinimum,
                attributeLocation ?? Location.None,
                declaredVersion.ToString(),
                SchemaVersionConstants.MinimumSupported.ToString(),
                SchemaVersionConstants.MigrationGuideUrl));
            shouldHalt = true;
        }
        // Compare against Current (FDDB112 if above)
        else if (declaredVersion > SchemaVersionConstants.Current)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.DeclaredVersionAboveCurrent,
                attributeLocation ?? Location.None,
                declaredVersion.ToString(),
                SchemaVersionConstants.Current.ToString()));
            shouldHalt = true;
        }
        // Emit FDDB113 if version is >= MinimumSupported but < Current
        else if (declaredVersion >= SchemaVersionConstants.MinimumSupported
                 && declaredVersion < SchemaVersionConstants.Current)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.OlderButSupportedVersion,
                attributeLocation ?? Location.None,
                declaredVersion.ToString(),
                SchemaVersionConstants.Current.ToString(),
                SchemaVersionConstants.UpgradeGuideUrl));
        }

        return new DetectionResult(
            declaredVersion,
            diagnostics,
            shouldHalt,
            attributeLocation);
    }
}
