using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for diagnostic help link URL format invariants.
/// Feature: diagnostics-reference
/// </summary>
public class DiagnosticHelpLinkPropertyTests
{
    /// <summary>
    /// **Validates: Requirements 3.2, 3.3, 3.4, 3.5**
    /// Property 1: helpLinkUri matches URL format for all descriptors.
    /// For any valid diagnostic code string (matching pattern [A-Z]{2,5}[0-9]{1,4}),
    /// formatting with DiagnosticHelpLinks.BaseUrlFormat produces a URL matching
    /// https://fluentdynamodb.dev/diagnostics/{CODE}.
    /// </summary>
    [Property(MaxTest = 200)]
    [Trait("Feature", "diagnostics-reference")]
    [Trait("Property", "1")]
    public Property BaseUrlFormat_ProducesCorrectUrl_ForAnyValidDiagnosticCode()
    {
        var codeGen = from prefix in Gen.Elements("AB", "ABC", "ABCD", "ABCDE", "DYNDB", "FDDB", "PROJ", "DISC", "SEC")
                      from number in Gen.Choose(1, 9999)
                      select prefix + number.ToString();

        return Prop.ForAll(Arb.From(codeGen), code =>
        {
            var result = string.Format(DiagnosticHelpLinks.BaseUrlFormat, code);
            var expected = $"https://fluentdynamodb.dev/diagnostics/{code}";
            return (result == expected).Label($"Expected '{expected}' but got '{result}'");
        });
    }

    /// <summary>
    /// **Validates: Requirements 3.2, 3.3, 3.4, 3.5**
    /// Property 2: Documentation file exists for every descriptor (code-side validation).
    /// For any DiagnosticDescriptor field selected from the complete set,
    /// HelpLinkUri equals the formatted base URL with that descriptor's Id.
    /// </summary>
    [Property(MaxTest = 200)]
    [Trait("Feature", "diagnostics-reference")]
    [Trait("Property", "2")]
    public Property AllActualDescriptors_HaveCorrectHelpLinkUri()
    {
        var descriptors = typeof(DiagnosticDescriptors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .ToArray();

        var gen = Gen.Elements(descriptors);
        return Prop.ForAll(Arb.From(gen), descriptor =>
        {
            var expectedUrl = string.Format(DiagnosticHelpLinks.BaseUrlFormat, descriptor.Id);
            return (descriptor.HelpLinkUri == expectedUrl)
                .Label($"Descriptor '{descriptor.Id}': expected '{expectedUrl}' but got '{descriptor.HelpLinkUri}'");
        });
    }
}
