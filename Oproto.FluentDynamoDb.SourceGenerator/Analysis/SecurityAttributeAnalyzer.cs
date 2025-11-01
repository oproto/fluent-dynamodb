using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes security attributes (Sensitive and Encrypted) on properties.
/// </summary>
internal class SecurityAttributeAnalyzer
{
    /// <summary>
    /// Analyzes a property for security attributes.
    /// </summary>
    /// <param name="property">The property model to analyze.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <returns>Security information for the property.</returns>
    public SecurityInfo AnalyzeProperty(PropertyModel property, SemanticModel semanticModel)
    {
        if (property.PropertyDeclaration == null)
        {
            return new SecurityInfo();
        }

        var securityInfo = new SecurityInfo();

        // Check for SensitiveAttribute
        var sensitiveAttr = GetAttribute(property.PropertyDeclaration, semanticModel, "SensitiveAttribute");
        securityInfo.IsSensitive = sensitiveAttr != null;

        // Check for EncryptedAttribute
        var encryptedAttr = GetAttribute(property.PropertyDeclaration, semanticModel, "EncryptedAttribute");
        if (encryptedAttr != null)
        {
            securityInfo.IsEncrypted = true;
            securityInfo.EncryptionConfig = ExtractEncryptionConfig(encryptedAttr);
        }

        return securityInfo;
    }

    private EncryptionConfig ExtractEncryptionConfig(AttributeSyntax encryptedAttr)
    {
        var config = new EncryptionConfig
        {
            CacheTtlSeconds = 300 // Default value
        };

        // Extract named arguments
        if (encryptedAttr.ArgumentList != null)
        {
            foreach (var arg in encryptedAttr.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "CacheTtlSeconds" &&
                    arg.Expression is LiteralExpressionSyntax cacheTtlLiteral &&
                    int.TryParse(cacheTtlLiteral.Token.ValueText, out var cacheTtl))
                {
                    config.CacheTtlSeconds = cacheTtl;
                }
            }
        }

        return config;
    }

    private AttributeSyntax? GetAttribute(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, string attributeName)
    {
        if (propertyDecl.AttributeLists.Count == 0)
            return null;

        var targetName = attributeName.Replace("Attribute", "");

        return propertyDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr =>
            {
                var attributeNameText = attr.Name.ToString();

                return attributeNameText == attributeName ||
                       attributeNameText == targetName ||
                       attributeNameText.EndsWith("." + attributeName) ||
                       attributeNameText.EndsWith("." + targetName);
            });
    }
}
