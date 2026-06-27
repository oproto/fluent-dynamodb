using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

namespace Oproto.FluentDynamoDb.UnitTests.Documentation;

/// <summary>
/// Validation tests for diagnostics documentation file existence and structure.
/// Property 2: Documentation file exists for every descriptor
/// Property 3: Documentation files contain all required sections
/// Validates: Requirements 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.7, 7.1
/// </summary>
public class DiagnosticsDocumentationValidationTests
{
    private static readonly string DocsRoot = FindDocsRoot();

    private static string FindDocsRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "docs", "diagnostics")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine(dir!, "docs", "diagnostics");
    }

    private static string GetPrefix(string code) => Regex.Match(code, @"^[A-Z]+").Value;

    private static IEnumerable<DiagnosticDescriptor> GetAllDescriptors()
    {
        return typeof(DiagnosticDescriptors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!);
    }

    public static IEnumerable<object[]> AllDescriptorIds()
    {
        return GetAllDescriptors().Select(d => new object[] { d.Id });
    }

    public static IEnumerable<object[]> AllDocumentationFiles()
    {
        if (!Directory.Exists(DocsRoot))
            return Enumerable.Empty<object[]>();

        return Directory.GetFiles(DocsRoot, "*.md", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("README.md", StringComparison.OrdinalIgnoreCase))
            .Select(f => new object[] { f });
    }

    /// <summary>
    /// Property 2: Documentation file exists for every descriptor.
    /// For each DiagnosticDescriptor ID, a corresponding .md file exists at docs/diagnostics/{PREFIX}/{CODE}.md.
    /// Validates: Requirements 1.3, 7.1
    /// </summary>
    [Theory]
    [MemberData(nameof(AllDescriptorIds))]
    public void DocumentationFileExistsForDescriptor(string diagnosticId)
    {
        var prefix = GetPrefix(diagnosticId);
        var expectedPath = Path.Combine(DocsRoot, prefix, $"{diagnosticId}.md");

        File.Exists(expectedPath).Should().BeTrue(
            $"documentation file should exist at '{prefix}/{diagnosticId}.md' for diagnostic '{diagnosticId}'");
    }

    /// <summary>
    /// Property 3: Documentation files contain all required sections.
    /// Each .md file contains required sections: Code &amp; Severity, Message, Description, Example, Fix.
    /// Validates: Requirements 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.7
    /// </summary>
    [Theory]
    [MemberData(nameof(AllDocumentationFiles))]
    public void DocumentationFileContainsRequiredSections(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);

        var requiredSections = new[]
        {
            "## Code & Severity",
            "## Message",
            "## Description",
            "## Example",
            "## Fix"
        };

        foreach (var section in requiredSections)
        {
            content.Should().Contain(section,
                $"documentation file '{fileName}' should contain section '{section}'");
        }
    }

    /// <summary>
    /// Property 3 (extended): Example and Fix code blocks are ≤30 lines each.
    /// Validates: Requirements 2.4, 2.5
    /// </summary>
    [Theory]
    [MemberData(nameof(AllDocumentationFiles))]
    public void CodeBlocksAreWithinLineLimit(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);

        var codeBlockPattern = new Regex(@"```csharp\s*\n(.*?)```", RegexOptions.Singleline);
        var matches = codeBlockPattern.Matches(content);

        foreach (Match match in matches)
        {
            var codeContent = match.Groups[1].Value.TrimEnd('\n');
            var lineCount = codeContent.Split('\n').Length;

            lineCount.Should().BeLessThanOrEqualTo(30,
                $"code block in '{fileName}' should be at most 30 lines (found {lineCount} lines)");
        }
    }

    /// <summary>
    /// Property 3 (extended): Message section matches the descriptor's messageFormat string.
    /// Validates: Requirements 2.2
    /// </summary>
    [Theory]
    [MemberData(nameof(AllDescriptorIds))]
    public void MessageSectionMatchesDescriptorMessageFormat(string diagnosticId)
    {
        var prefix = GetPrefix(diagnosticId);
        var filePath = Path.Combine(DocsRoot, prefix, $"{diagnosticId}.md");

        if (!File.Exists(filePath))
            return; // Covered by DocumentationFileExistsForDescriptor test

        var content = File.ReadAllText(filePath);
        var descriptor = GetAllDescriptors().First(d => d.Id == diagnosticId);

        // The message format should appear in the Message section as inline code
        var expectedMessage = descriptor.MessageFormat.ToString();
        content.Should().Contain(expectedMessage,
            $"documentation file '{diagnosticId}.md' Message section should contain the descriptor's message format: '{expectedMessage}'");
    }
}
