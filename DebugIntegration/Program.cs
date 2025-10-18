using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;

var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""transactions"")]
    public partial class TransactionEntity
    {
        [PartitionKey(Prefix = ""tenant"", Separator = ""#"")]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey(Prefix = ""txn"", Separator = ""#"")]
        [DynamoDbAttribute(""sk"")]
        public string TransactionId { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""tags"")]
        public List<string>? Tags { get; set; }
        
        [RelatedEntity(""audit#*"")]
        public List<AuditEntry>? AuditEntries { get; set; }
        
        [RelatedEntity(""summary"")]
        public TransactionSummary? Summary { get; set; }
    }
    
    public class AuditEntry
    {
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
    
    public class TransactionSummary
    {
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
    }
}";

var attributeSource = @"
using System;

namespace Oproto.FluentDynamoDb.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamoDbTableAttribute : Attribute
    {
        public string TableName { get; }
        public string? EntityDiscriminator { get; set; }
        public DynamoDbTableAttribute(string tableName) => TableName = tableName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DynamoDbAttributeAttribute : Attribute
    {
        public string AttributeName { get; }
        public DynamoDbAttributeAttribute(string attributeName) => AttributeName = attributeName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PartitionKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string? Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SortKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string? Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class GlobalSecondaryIndexAttribute : Attribute
    {
        public string IndexName { get; }
        public bool IsPartitionKey { get; set; }
        public bool IsSortKey { get; set; }
        public string? KeyFormat { get; set; }
        public GlobalSecondaryIndexAttribute(string indexName) => IndexName = indexName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class RelatedEntityAttribute : Attribute
    {
        public string SortKeyPattern { get; }
        public Type? EntityType { get; set; }
        public RelatedEntityAttribute(string sortKeyPattern) => SortKeyPattern = sortKeyPattern;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class QueryableAttribute : Attribute
    {
        public string[] SupportedOperations { get; set; } = Array.Empty<string>();
        public string[]? AvailableInIndexes { get; set; }
    }
}";

var compilation = CSharpCompilation.Create(
    "TestAssembly",
    new[] { 
        CSharpSyntaxTree.ParseText(source),
        CSharpSyntaxTree.ParseText(attributeSource)
    },
    new[] { 
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
    },
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

var generator = new DynamoDbSourceGenerator();
var driver = CSharpGeneratorDriver.Create(generator);

driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

Console.WriteLine($"Generated {diagnostics.Length} diagnostics:");
foreach (var diagnostic in diagnostics)
{
    Console.WriteLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
}

var generatedSources = outputCompilation.SyntaxTrees
    .Skip(compilation.SyntaxTrees.Count())
    .ToArray();

Console.WriteLine($"Generated {generatedSources.Length} source files");