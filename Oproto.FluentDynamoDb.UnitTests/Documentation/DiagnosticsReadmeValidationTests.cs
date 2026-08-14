using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

namespace Oproto.FluentDynamoDb.UnitTests.Documentation;

/// <summary>
/// Validation tests for README completeness and changelog entries.
/// Property 4: README index row exists for every descriptor.
/// Validates: Requirements 1.4, 7.2
/// </summary>
public class DiagnosticsReadmeValidationTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();

    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "CHANGELOG.md")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException(
            "Could not find solution root (no CHANGELOG.md found in parent directories).");
    }

    private static IEnumerable<DiagnosticDescriptor> GetAllDescriptors()
    {
        return typeof(DiagnosticDescriptors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!);
    }

    private static string GetReadmeContent()
    {
        var path = Path.Combine(SolutionRoot, "docs", "diagnostics", "README.md");
        return File.ReadAllText(path);
    }

    [Fact]
    public void Readme_HasAllPrefixes_InAlphabeticalOrder()
    {
        var content = GetReadmeContent();
        var expectedPrefixes = new[] { "DISC", "DYNDB", "FDDB", "PROJ", "SEC" };

        var prefixPositions = new List<(string Prefix, int Position)>();
        foreach (var prefix in expectedPrefixes)
        {
            // Look for the section heading pattern "## {PREFIX} —"
            var pattern = $"## {prefix} —";
            var index = content.IndexOf(pattern, StringComparison.Ordinal);
            index.Should().BeGreaterThan(-1,
                $"README should contain a section heading for prefix '{prefix}' (pattern: '{pattern}')");
            prefixPositions.Add((prefix, index));
        }

        // Verify they appear in ascending order (alphabetical by prefix)
        for (var i = 1; i < prefixPositions.Count; i++)
        {
            prefixPositions[i].Position.Should().BeGreaterThan(prefixPositions[i - 1].Position,
                $"prefix '{prefixPositions[i].Prefix}' should appear after '{prefixPositions[i - 1].Prefix}' in README");
        }
    }

    [Fact]
    public void Readme_TotalCount_MatchesActualFileCount()
    {
        var content = GetReadmeContent();
        var diagnosticsDir = Path.Combine(SolutionRoot, "docs", "diagnostics");

        // Count actual .md files in subdirectories (excluding README.md)
        var actualCount = Directory.GetFiles(diagnosticsDir, "*.md", SearchOption.AllDirectories)
            .Count(f => !f.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));

        // Extract the stated count from README — look for "**{N}** diagnostic codes"
        var match = Regex.Match(content, @"\*\*(\d+)\*\*\s+diagnostic codes");
        match.Success.Should().BeTrue("README should contain a total count in bold (e.g., '**103** diagnostic codes')");

        var statedCount = int.Parse(match.Groups[1].Value);
        statedCount.Should().Be(actualCount,
            $"README states {statedCount} diagnostics but found {actualCount} .md files in docs/diagnostics/ subdirectories");
    }

    [Fact]
    public void Readme_HasRowForEveryDescriptor_WithCodeSeverityAndTitle()
    {
        var content = GetReadmeContent();
        var descriptors = GetAllDescriptors().ToList();

        descriptors.Should().NotBeEmpty("there should be DiagnosticDescriptor definitions to validate");

        foreach (var descriptor in descriptors)
        {
            var code = descriptor.Id;
            var prefix = Regex.Match(code, @"^[A-Z]+").Value;

            // Check that the README contains a row with a link to the code
            // Expected format: | [CODE](PREFIX/CODE.md) | Severity | Title |
            var linkPattern = $"[{code}]({prefix}/{code}.md)";
            content.Should().Contain(linkPattern,
                $"README should contain a linked entry for '{code}' in format '[{code}]({prefix}/{code}.md)'");

            // Check severity is present in the same row
            var severity = descriptor.DefaultSeverity switch
            {
                DiagnosticSeverity.Error => "Error",
                DiagnosticSeverity.Warning => "Warning",
                DiagnosticSeverity.Info => "Info",
                _ => descriptor.DefaultSeverity.ToString()
            };

            // Find the row containing this code and verify it has severity and title
            var lines = content.Split('\n');
            var row = lines.FirstOrDefault(l => l.Contains(linkPattern));
            row.Should().NotBeNull($"README should have a table row containing '{linkPattern}'");
            row.Should().Contain($"| {severity} |",
                $"row for '{code}' should contain severity '{severity}'");

            // Check that the title text appears in the row (trimmed, case-insensitive)
            var title = descriptor.Title.ToString().Trim();
            row!.ToLowerInvariant().Should().Contain(title.ToLowerInvariant(),
                $"row for '{code}' should contain the title '{title}'");
        }
    }

    [Fact]
    public void DocumentationChangelog_HasAppropriatelyFormattedEntry()
    {
        var docChangelogPath = Path.Combine(SolutionRoot, "docs", "DOCUMENTATION_CHANGELOG.md");
        var content = File.ReadAllText(docChangelogPath);

        // Verify it has a "New Feature Documentation" category entry
        content.Should().Contain("New Feature Documentation",
            "DOCUMENTATION_CHANGELOG should have a 'New Feature Documentation' category entry");

        // Verify it mentions docs/diagnostics/
        content.Should().Contain("docs/diagnostics/",
            "DOCUMENTATION_CHANGELOG entry should mention 'docs/diagnostics/' directory");

        // Verify it mentions the URL pattern for the website
        content.Should().Contain("https://fluentdynamodb.dev/diagnostics/",
            "DOCUMENTATION_CHANGELOG entry should specify the URL pattern for per-code pages");

        // Verify it mentions a date heading in YYYY-MM-DD format
        var dateMatch = Regex.Match(content, @"## \[\d{4}-\d{2}-\d{2}\]");
        dateMatch.Success.Should().BeTrue(
            "DOCUMENTATION_CHANGELOG should contain a date heading in ## [YYYY-MM-DD] format");
    }
}
