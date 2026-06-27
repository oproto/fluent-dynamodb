using System.Reflection;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests validating helpLinkUri on all DiagnosticDescriptors.
/// Validates: Requirements 3.3, 3.5
/// </summary>
public class DiagnosticHelpLinkTests
{
    private static IEnumerable<DiagnosticDescriptor> GetAllDescriptors()
    {
        return typeof(DiagnosticDescriptors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!);
    }

    [Fact]
    public void AllDescriptors_HaveNonEmptyHelpLinkUri()
    {
        var descriptors = GetAllDescriptors().ToList();

        descriptors.Should().NotBeEmpty("there should be at least one DiagnosticDescriptor defined");

        foreach (var descriptor in descriptors)
        {
            descriptor.HelpLinkUri.Should().NotBeNullOrEmpty(
                $"descriptor '{descriptor.Id}' should have a non-empty HelpLinkUri");
        }
    }

    [Fact]
    public void AllDescriptors_HelpLinkUri_MatchesBaseUrlFormat()
    {
        var descriptors = GetAllDescriptors().ToList();

        descriptors.Should().NotBeEmpty();

        foreach (var descriptor in descriptors)
        {
            var expectedUrl = string.Format(DiagnosticHelpLinks.BaseUrlFormat, descriptor.Id);
            descriptor.HelpLinkUri.Should().Be(expectedUrl,
                $"descriptor '{descriptor.Id}' HelpLinkUri should match the formatted BaseUrlFormat");
        }
    }

    [Fact]
    public void BaseUrlFormat_HasCorrectStructure()
    {
        var format = DiagnosticHelpLinks.BaseUrlFormat;

        format.Should().StartWith("https://fluentdynamodb.dev/diagnostics/",
            "BaseUrlFormat should point to the fluentdynamodb.dev diagnostics path");

        // Should contain exactly one {0} placeholder
        var placeholderCount = format.Split("{0}").Length - 1;
        placeholderCount.Should().Be(1,
            "BaseUrlFormat should contain exactly one {0} placeholder");

        // Verify it can be formatted without error
        var result = string.Format(format, "TEST001");
        result.Should().Be("https://fluentdynamodb.dev/diagnostics/TEST001");
    }
}
