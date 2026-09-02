using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Property-based tests for FDDB125 computed key prefix conflict diagnostic.
/// 
/// **Feature: computed-key-prefix-conflict**
/// - Property 1: Computed key with prefix always emits FDDB125 (Validates: Requirements 1.1, 1.2, 2.1, 2.2, 2.3)
/// - Property 2: No false positives for non-conflicting configurations (Validates: Requirements 4.1, 4.2, 4.3, 4.4)
/// 
/// These tests verify that the source generator correctly emits FDDB125 when a computed key
/// property has a non-empty Prefix configured, and does NOT emit it for valid configurations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ComputedKeyPrefixConflictPropertyTests
{
    /// <summary>
    /// Generates simple valid C# property names (alphabetic, starting with uppercase).
    /// </summary>
    private static Gen<string> PropertyNameGen()
    {
        return Gen.Elements(
            "Alpha", "Beta", "Gamma", "Delta", "Epsilon",
            "Zeta", "Eta", "Theta", "Iota", "Kappa",
            "Lambda", "Mu", "Nu", "Xi", "Omicron",
            "MyProp", "TestProp", "DataField", "ValueProp", "ItemKey");
    }

    /// <summary>
    /// Generates simple valid DynamoDB attribute names.
    /// </summary>
    private static Gen<string> AttributeNameGen()
    {
        return Gen.Elements(
            "pk", "sk", "data", "info", "display",
            "value", "item", "record", "field", "attr");
    }

    /// <summary>
    /// Generates safe prefix strings that won't break C# string literals.
    /// </summary>
    private static Gen<string> SafePrefixGen()
    {
        return Gen.Elements(
            "ORDER", "USER", "ITEM", "CUST", "INV",
            "EVENT", "LOG", "META", "DOC", "REC",
            "PFX", "KEY", "VAL", "OBJ", "ENT");
    }

    /// <summary>
    /// Generates simple component property names for [Computed] attributes.
    /// </summary>
    private static Gen<string> ComponentNameGen()
    {
        return Gen.Elements(
            "PartA", "PartB", "PartC", "FieldX", "FieldY",
            "First", "Second", "Third", "Left", "Right");
    }

    /// <summary>
    /// Category (a): Key with prefix but NOT computed — standard valid usage.
    /// Should NOT emit FDDB125 (Requirement 4.1).
    /// </summary>
    private static Gen<string> KeyWithPrefixNotComputedGen()
    {
        return from propName in PropertyNameGen()
               from attrName in AttributeNameGen()
               from prefix in SafePrefixGen()
               from isPartitionKey in Gen.Elements(true, false)
               let keyAttr = isPartitionKey
                   ? $"[PartitionKey(Prefix = \"{prefix}\")]"
                   : $"[SortKey(Prefix = \"{prefix}\")]"
               let skProp = isPartitionKey
                   ? "[SortKey]\n        [DynamoDbAttribute(\"sk\")]\n        public string Sk { get; set; } = string.Empty;"
                   : "[PartitionKey]\n        [DynamoDbAttribute(\"pk\")]\n        public string MainPk { get; set; } = string.Empty;"
               select $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        {keyAttr}
        [DynamoDbAttribute(""{attrName}"")]
        public string {propName} {{ get; set; }} = string.Empty;

        {skProp}
    }}
}}";
    }

    /// <summary>
    /// Category (b): Computed key with NO prefix — valid usage.
    /// Should NOT emit FDDB125 (Requirement 4.2).
    /// </summary>
    private static Gen<string> ComputedKeyNoPrefixGen()
    {
        return from propName in PropertyNameGen()
               from attrName in AttributeNameGen()
               from comp1 in ComponentNameGen()
               from comp2 in ComponentNameGen()
               from isPartitionKey in Gen.Elements(true, false)
               let keyAttr = isPartitionKey ? "[PartitionKey]" : "[SortKey]"
               let skProp = isPartitionKey
                   ? "[SortKey]\n        [DynamoDbAttribute(\"sk\")]\n        public string Sk { get; set; } = string.Empty;"
                   : "[PartitionKey]\n        [DynamoDbAttribute(\"pk\")]\n        public string MainPk { get; set; } = string.Empty;"
               select $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        {keyAttr}
        [DynamoDbAttribute(""{attrName}"")]
        [Computed(""{comp1}"", ""{comp2}"", Separator = ""#"")]
        public string {propName} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""{(isPartitionKey ? "c1" : "c1")}"")]
        [Extracted(""{propName}"", 0)]
        public string {comp1} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""{(isPartitionKey ? "c2" : "c2")}"")]
        [Extracted(""{propName}"", 1)]
        public string {comp2} {{ get; set; }} = string.Empty;

        {skProp}
    }}
}}";
    }

    /// <summary>
    /// Category (c): Computed key with empty prefix — should NOT trigger.
    /// Should NOT emit FDDB125 (Requirement 4.3).
    /// </summary>
    private static Gen<string> ComputedKeyEmptyPrefixGen()
    {
        return from propName in PropertyNameGen()
               from attrName in AttributeNameGen()
               from comp1 in ComponentNameGen()
               from comp2 in ComponentNameGen()
               from isPartitionKey in Gen.Elements(true, false)
               let keyAttr = isPartitionKey
                   ? "[PartitionKey(Prefix = \"\")]"
                   : "[SortKey(Prefix = \"\")]"
               let skProp = isPartitionKey
                   ? "[SortKey]\n        [DynamoDbAttribute(\"sk\")]\n        public string Sk { get; set; }} = string.Empty;"
                   : "[PartitionKey]\n        [DynamoDbAttribute(\"pk\")]\n        public string MainPk { get; set; }} = string.Empty;"
               select $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        {keyAttr}
        [DynamoDbAttribute(""{attrName}"")]
        [Computed(""{comp1}"", ""{comp2}"", Separator = ""#"")]
        public string {propName} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""c1"")]
        [Extracted(""{propName}"", 0)]
        public string {comp1} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""c2"")]
        [Extracted(""{propName}"", 1)]
        public string {comp2} {{ get; set; }} = string.Empty;

        {skProp}
    }}
}}";
    }

    /// <summary>
    /// Category (d): Computed non-key property — should NOT trigger.
    /// Should NOT emit FDDB125 (Requirement 4.4).
    /// </summary>
    private static Gen<string> ComputedNonKeyPropertyGen()
    {
        return from propName in PropertyNameGen()
               from attrName in AttributeNameGen()
               from comp1 in ComponentNameGen()
               from comp2 in ComponentNameGen()
               select $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""{attrName}"")]
        [Computed(""{comp1}"", ""{comp2}"", Separator = "" "")]
        public string {propName} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""c1"")]
        [Extracted(""{propName}"", 0)]
        public string {comp1} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""c2"")]
        [Extracted(""{propName}"", 1)]
        public string {comp2} {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Combines all four non-conflicting categories into a single arbitrary.
    /// </summary>
    private static Arbitrary<string> NonConflictingEntityArb()
    {
        var gen = Gen.OneOf(
            KeyWithPrefixNotComputedGen(),
            ComputedKeyNoPrefixGen(),
            ComputedKeyEmptyPrefixGen(),
            ComputedNonKeyPropertyGen());

        return gen.ToArbitrary();
    }

    #region Property 1 Generators

    /// <summary>
    /// Generates valid C# property names (start with uppercase letter, followed by alphanumeric chars).
    /// </summary>
    private static Arbitrary<string> ValidPropertyNameArb()
    {
        var gen = Gen.Choose(0, 9).SelectMany(length =>
        {
            var firstChar = Gen.Elements(
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                'U', 'V', 'W', 'X', 'Y', 'Z');

            if (length == 0)
                return firstChar.Select(c => c.ToString());

            var restChars = Gen.ArrayOf(length, Gen.Elements(
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                'U', 'V', 'W', 'X', 'Y', 'Z',
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                'u', 'v', 'w', 'x', 'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'));

            return firstChar.SelectMany(first =>
                restChars.Select(rest => first + new string(rest)));
        });

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates non-empty alphanumeric prefix strings (safe for C# string literals).
    /// </summary>
    private static Arbitrary<string> NonEmptyPrefixArb()
    {
        var gen = Gen.Choose(1, 10).SelectMany(length =>
            Gen.ArrayOf(length, Gen.Elements(
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                'U', 'V', 'W', 'X', 'Y', 'Z',
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                'u', 'v', 'w', 'x', 'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
            .Select(chars => new string(chars)));

        return gen.ToArbitrary();
    }

    #endregion

    #region Property 1: Computed key with prefix always emits FDDB125

    /// <summary>
    /// **Feature: computed-key-prefix-conflict, Property 1: Computed key with prefix always emits FDDB125**
    /// **Validates: Requirements 1.1, 1.2, 2.1, 2.2, 2.3**
    /// 
    /// Property: For any property that is both a partition key and a computed property,
    /// and has a non-empty Prefix configured on the key attribute, and does NOT have an
    /// explicit Format on [Computed], the source generator SHALL emit FDDB125 as an error
    /// diagnostic whose message contains both the property name and the configured prefix value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartitionKey_ComputedWithPrefix_NoFormat_EmitsFDDB125()
    {
        return Prop.ForAll(ValidPropertyNameArb(), NonEmptyPrefixArb(), (propertyName, prefix) =>
        {
            var source = GeneratePartitionKeyComputedNoFormatSource(propertyName, prefix);
            var diagnostics = RunSourceGenerator(source);

            var fddb125 = diagnostics.FirstOrDefault(d => d.Id == "FDDB125");

            if (fddb125 == null)
                return false.Label($"FDDB125 not emitted for property '{propertyName}' with prefix '{prefix}' (no Format)");

            var message = fddb125.GetMessage();
            var hasPropertyName = message.Contains(propertyName);
            var hasPrefix = message.Contains(prefix);
            var isError = fddb125.Severity == DiagnosticSeverity.Error;

            return (hasPropertyName && hasPrefix && isError)
                .Label($"Property='{propertyName}', Prefix='{prefix}', Message='{message}', " +
                       $"HasPropertyName={hasPropertyName}, HasPrefix={hasPrefix}, IsError={isError}");
        });
    }

    /// <summary>
    /// **Feature: computed-key-prefix-conflict, Property 1: Computed key with prefix always emits FDDB125**
    /// **Validates: Requirements 1.1, 1.2, 2.1, 2.2, 2.3**
    /// 
    /// Property: For any property that is both a partition key and a computed property,
    /// and has a non-empty Prefix configured on the key attribute, and HAS an explicit Format
    /// on [Computed], the source generator SHALL emit FDDB125 as an error diagnostic whose
    /// message contains both the property name and the configured prefix value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartitionKey_ComputedWithPrefix_WithFormat_EmitsFDDB125()
    {
        return Prop.ForAll(ValidPropertyNameArb(), NonEmptyPrefixArb(), (propertyName, prefix) =>
        {
            var source = GeneratePartitionKeyComputedWithFormatSource(propertyName, prefix);
            var diagnostics = RunSourceGenerator(source);

            var fddb125 = diagnostics.FirstOrDefault(d => d.Id == "FDDB125");

            if (fddb125 == null)
                return false.Label($"FDDB125 not emitted for property '{propertyName}' with prefix '{prefix}' (with Format)");

            var message = fddb125.GetMessage();
            var hasPropertyName = message.Contains(propertyName);
            var hasPrefix = message.Contains(prefix);
            var isError = fddb125.Severity == DiagnosticSeverity.Error;

            return (hasPropertyName && hasPrefix && isError)
                .Label($"Property='{propertyName}', Prefix='{prefix}', Message='{message}', " +
                       $"HasPropertyName={hasPropertyName}, HasPrefix={hasPrefix}, IsError={isError}");
        });
    }

    /// <summary>
    /// **Feature: computed-key-prefix-conflict, Property 1: Computed key with prefix always emits FDDB125**
    /// **Validates: Requirements 1.1, 1.2, 2.1, 2.2, 2.3**
    /// 
    /// Property: For any property that is both a sort key and a computed property,
    /// and has a non-empty Prefix configured on the key attribute, the source generator
    /// SHALL emit FDDB125 as an error diagnostic whose message contains both the property
    /// name and the configured prefix value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortKey_ComputedWithPrefix_EmitsFDDB125()
    {
        return Prop.ForAll(ValidPropertyNameArb(), NonEmptyPrefixArb(), (propertyName, prefix) =>
        {
            var source = GenerateSortKeyComputedWithPrefixSource(propertyName, prefix);
            var diagnostics = RunSourceGenerator(source);

            var fddb125 = diagnostics.FirstOrDefault(d => d.Id == "FDDB125");

            if (fddb125 == null)
                return false.Label($"FDDB125 not emitted for sort key property '{propertyName}' with prefix '{prefix}'");

            var message = fddb125.GetMessage();
            var hasPropertyName = message.Contains(propertyName);
            var hasPrefix = message.Contains(prefix);
            var isError = fddb125.Severity == DiagnosticSeverity.Error;

            return (hasPropertyName && hasPrefix && isError)
                .Label($"Property='{propertyName}', Prefix='{prefix}', Message='{message}', " +
                       $"HasPropertyName={hasPropertyName}, HasPrefix={hasPrefix}, IsError={isError}");
        });
    }

    #endregion

    #region Property 1 Source Generators

    /// <summary>
    /// Generates source code with a computed partition key + prefix, no explicit Format on [Computed].
    /// </summary>
    private static string GeneratePartitionKeyComputedNoFormatSource(string propertyName, string prefix)
    {
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey(Prefix = ""{prefix}"")]
        [DynamoDbAttribute(""pk"")]
        [Computed(""A"", ""B"", Separator = ""#"")]
        public string {propertyName} {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""a"")]
        [Extracted(""{propertyName}"", 0)]
        public string A {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""b"")]
        [Extracted(""{propertyName}"", 1)]
        public string B {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a computed partition key + prefix, WITH explicit Format on [Computed].
    /// </summary>
    private static string GeneratePartitionKeyComputedWithFormatSource(string propertyName, string prefix)
    {
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey(Prefix = ""{prefix}"")]
        [DynamoDbAttribute(""pk"")]
        [Computed(""A"", ""B"", Separator = ""#"", Format = ""{prefix}#{{0}}"")]
        public string {propertyName} {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""a"")]
        [Extracted(""{propertyName}"", 0)]
        public string A {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""b"")]
        [Extracted(""{propertyName}"", 1)]
        public string B {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a computed sort key + prefix.
    /// </summary>
    private static string GenerateSortKeyComputedWithPrefixSource(string propertyName, string prefix)
    {
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey(Prefix = ""{prefix}"")]
        [DynamoDbAttribute(""sk"")]
        [Computed(""A"", ""B"", Separator = ""#"")]
        public string {propertyName} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""a"")]
        [Extracted(""{propertyName}"", 0)]
        public string A {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""b"")]
        [Extracted(""{propertyName}"", 1)]
        public string B {{ get; set; }} = string.Empty;
    }}
}}";
    }

    #endregion

    #region Property 2: No false positives for non-conflicting configurations

    /// <summary>
    /// **Feature: computed-key-prefix-conflict, Property 2: No false positives for non-conflicting configurations**
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    /// 
    /// Property: For any entity where EITHER (a) it has a key with prefix but is not computed,
    /// OR (b) it has a computed key with no prefix, OR (c) it has a computed key with empty prefix,
    /// OR (d) it has a computed non-key property, the source generator SHALL NOT emit FDDB125.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonConflictingConfigurations_ShouldNotEmitFDDB125()
    {
        return Prop.ForAll(NonConflictingEntityArb(), source =>
        {
            var diagnostics = RunSourceGenerator(source);

            var fddb125 = diagnostics.Where(d => d.Id == "FDDB125").ToArray();

            return (fddb125.Length == 0)
                .Label($"FDDB125 should not be emitted for non-conflicting configuration but got {fddb125.Length} diagnostic(s).\nSource:\n{source}");
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Runs the source generator on the provided source and returns the generator diagnostics.
    /// </summary>
    private static IReadOnlyList<Diagnostic> RunSourceGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var generatorDiagnostics);

        return generatorDiagnostics;
    }

    #endregion
}
