using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for EnableDynamicFields attribute detection in EntityAnalyzer.
/// 
/// **Feature: dynamic-fields-support, Property 1: Source Generator Attribute Detection**
/// **Validates: Requirements 1.1, 1.2, 1.3**
/// 
/// These tests verify that for any entity class, the source generator SHALL generate 
/// dynamic field handling code if and only if the [EnableDynamicFields] attribute is present.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class EnableDynamicFieldsPropertyTests
{
    /// <summary>
    /// **Feature: dynamic-fields-support, Property 1: Source Generator Attribute Detection**
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// 
    /// Property: For any entity class, the source generator SHALL generate dynamic field handling code 
    /// if and only if the [EnableDynamicFields] attribute is present on the class.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EnableDynamicFields_GeneratesDynamicFieldsCodeIfAndOnlyIfAttributePresent()
    {
        // Generate random boolean for whether attribute is present
        var hasAttributeArb = Arb.From<bool>();
        // Generate random boolean for SensitiveLogging property
        var sensitiveLoggingArb = Arb.From<bool>();
        
        return Prop.ForAll(hasAttributeArb, sensitiveLoggingArb, (hasAttribute, sensitiveLogging) =>
        {
            // Generate source code with or without the attribute
            var source = GenerateEntitySource(hasAttribute, sensitiveLogging);
            
            // Parse and analyze
            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);
            
            // The entity should be analyzed successfully
            if (result == null)
            {
                // Analysis failed - this shouldn't happen for valid entities
                return false;
            }
            
            // Property 1: EnableDynamicFields flag should match attribute presence
            if (result.EnableDynamicFields != hasAttribute)
            {
                return false;
            }
            
            // Property 2: If attribute is present, SensitiveLogging should match
            if (hasAttribute && result.DynamicFieldsSensitiveLogging != sensitiveLogging)
            {
                return false;
            }
            
            // Property 3: Generated code should contain DynamicFields property if and only if attribute is present
            var generatedCode = MapperGenerator.GenerateEntityImplementation(result);
            var containsDynamicFieldsProperty = generatedCode.Contains("public DynamicFieldCollection DynamicFields");
            
            if (containsDynamicFieldsProperty != hasAttribute)
            {
                return false;
            }
            
            // Property 4: Generated code should contain _mappedAttributeNames if and only if attribute is present
            var containsMappedAttributeNames = generatedCode.Contains("private static readonly HashSet<string> _mappedAttributeNames");
            
            if (containsMappedAttributeNames != hasAttribute)
            {
                return false;
            }
            
            return true;
        });
    }

    /// <summary>
    /// Property: For any entity with [EnableDynamicFields], the generated _mappedAttributeNames 
    /// HashSet should contain all mapped attribute names from the entity's properties.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MappedAttributeNames_ContainsAllMappedProperties()
    {
        // Generate random number of properties (1 to 5, plus the required partition key)
        var propertyCountArb = Gen.Choose(1, 5).ToArbitrary();
        
        return Prop.ForAll(propertyCountArb, propertyCount =>
        {
            // Generate source code with the specified number of properties
            var (source, attributeNames) = GenerateEntitySourceWithProperties(propertyCount);
            
            // Parse and analyze
            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);
            
            if (result == null)
            {
                return false;
            }
            
            // Generate code
            var generatedCode = MapperGenerator.GenerateEntityImplementation(result);
            
            // Verify all attribute names are in the generated _mappedAttributeNames
            foreach (var attrName in attributeNames)
            {
                if (!generatedCode.Contains($"\"{attrName}\""))
                {
                    return false;
                }
            }
            
            return true;
        });
    }

    /// <summary>
    /// Generates entity source code with or without [EnableDynamicFields] attribute.
    /// </summary>
    private static string GenerateEntitySource(bool hasAttribute, bool sensitiveLogging)
    {
        var attributeLine = hasAttribute 
            ? $"    [EnableDynamicFields(SensitiveLogging = {sensitiveLogging.ToString().ToLowerInvariant()})]"
            : "";
        
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
{attributeLine}
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""data"")]
        public string Data {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates entity source code with [EnableDynamicFields] and a specified number of properties.
    /// Returns the source code and the list of attribute names.
    /// </summary>
    private static (string Source, List<string> AttributeNames) GenerateEntitySourceWithProperties(int additionalPropertyCount)
    {
        var attributeNames = new List<string> { "pk" }; // Always have partition key
        var properties = new System.Text.StringBuilder();
        
        for (int i = 0; i < additionalPropertyCount; i++)
        {
            var attrName = $"prop{i}";
            attributeNames.Add(attrName);
            properties.AppendLine($@"
        [DynamoDbAttribute(""{attrName}"")]
        public string Property{i} {{ get; set; }} = string.Empty;");
        }
        
        var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    [EnableDynamicFields]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
{properties}
    }}
}}";
        
        return (source, attributeNames);
    }

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        
        // Get the TestEntity class declaration
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "TestEntity");

        return (classDecl, semanticModel);
    }
}
