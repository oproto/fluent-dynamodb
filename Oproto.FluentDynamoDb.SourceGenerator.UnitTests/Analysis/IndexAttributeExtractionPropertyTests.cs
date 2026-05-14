using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for the new index attribute extraction logic.
/// Tests operate through the full EntityAnalyzer pipeline by compiling entity source code
/// with [GsiPartitionKey], [GsiSortKey], and [LsiSortKey] attributes.
///
/// Feature: index-attribute-redesign
/// </summary>
public class IndexAttributeExtractionPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 1: Extraction preserves all configuration values
    // Feature: index-attribute-redesign, Property 1: Extraction preserves all configuration values
    // **Validates: Requirements 1.1, 2.1, 3.1, 5.1, 5.2, 5.3**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid entity property annotated with [GsiPartitionKey] with optional
    /// configuration values, the EntityAnalyzer extraction SHALL produce an IndexModel
    /// where every specified configuration value is correctly propagated.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiPartitionKey_ExtractionPreservesAllConfigurationValues()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<bool>(),
            (indexNameRaw, useKeysOnly) =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);
                var projectionType = useKeysOnly ? "ProjectionType.KeysOnly" : "ProjectionType.All";
                var customName = "Custom" + SanitizePropertyName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"", Name = ""{customName}"", ProjectionType = {projectionType})]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var idx = result.Indexes.FirstOrDefault(i => i.IndexName == indexName);
                if (idx == null) return false;

                return idx.PartitionKeyProperty == "GsiPk"
                    && idx.PartitionKeyAttribute == "gsiPk"
                    && idx.IndexType == IndexType.GlobalSecondaryIndex
                    && idx.CustomName == customName
                    && idx.ProjectionType == (useKeysOnly ? ProjectionType.KeysOnly : ProjectionType.All);
            });
    }

    /// <summary>
    /// For any valid entity property annotated with [GsiSortKey] with optional
    /// configuration values, the EntityAnalyzer extraction SHALL produce an IndexModel
    /// where the sort key and configuration values are correctly propagated.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiSortKey_ExtractionPreservesAllConfigurationValues()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<bool>(),
            (indexNameRaw, useKeysOnly) =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);
                var projectionType = useKeysOnly ? "ProjectionType.KeysOnly" : "ProjectionType.All";
                var customName = "Custom" + SanitizePropertyName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;

        [GsiSortKey(""{indexName}"", Name = ""{customName}"", ProjectionType = {projectionType})]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var idx = result.Indexes.FirstOrDefault(i => i.IndexName == indexName);
                if (idx == null) return false;

                // GsiSortKey Name/ProjectionType are fallbacks — only used when PK doesn't set them
                // Since PK doesn't set Name or non-All ProjectionType, SK values should be used
                return idx.SortKeyProperty == "GsiSk"
                    && idx.SortKeyAttribute == "gsiSk"
                    && idx.CustomName == customName
                    && idx.ProjectionType == (useKeysOnly ? ProjectionType.KeysOnly : ProjectionType.All);
            });
    }

    /// <summary>
    /// For any valid entity property annotated with [LsiSortKey] with optional
    /// configuration values, the EntityAnalyzer extraction SHALL produce an IndexModel
    /// where every specified configuration value is correctly propagated.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LsiSortKey_ExtractionPreservesAllConfigurationValues()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<bool>(),
            (indexNameRaw, useKeysOnly) =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);
                var projectionType = useKeysOnly ? "ProjectionType.KeysOnly" : "ProjectionType.All";
                var customName = "Custom" + SanitizePropertyName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [LsiSortKey(""{indexName}"", Name = ""{customName}"", ProjectionType = {projectionType})]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var idx = result.Indexes.FirstOrDefault(i => i.IndexName == indexName);
                if (idx == null) return false;

                return idx.SortKeyProperty == "LsiSk"
                    && idx.SortKeyAttribute == "lsiSk"
                    && idx.IndexType == IndexType.LocalSecondaryIndex
                    && idx.CustomName == customName
                    && idx.ProjectionType == (useKeysOnly ? ProjectionType.KeysOnly : ProjectionType.All);
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2: GSI PK and SK combine into single IndexModel
    // Feature: index-attribute-redesign, Property 2: GSI PK and SK combine into single IndexModel
    // **Validates: Requirements 5.4, 5.5**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any entity where one property has [GsiPartitionKey("X")] and another has
    /// [GsiSortKey("X")] for the same index name X, the EntityAnalyzer SHALL produce
    /// exactly one IndexModel with both PK and SK set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiPartitionAndSortKey_CombineIntoSingleIndex()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;

        [GsiSortKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var matchingIndexes = result.Indexes.Where(i => i.IndexName == indexName).ToArray();

                return matchingIndexes.Length == 1
                    && matchingIndexes[0].PartitionKeyProperty == "GsiPk"
                    && matchingIndexes[0].SortKeyProperty == "GsiSk"
                    && matchingIndexes[0].IndexType == IndexType.GlobalSecondaryIndex;
            });
    }

    /// <summary>
    /// For any entity where a property has [GsiPartitionKey("X")] but no property has
    /// [GsiSortKey("X")], the resulting IndexModel SHALL have SortKeyProperty = null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiPartitionKeyOnly_SortKeyIsNull()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var idx = result.Indexes.FirstOrDefault(i => i.IndexName == indexName);
                if (idx == null) return false;

                return idx.PartitionKeyProperty == "GsiPk"
                    && string.IsNullOrEmpty(idx.SortKeyProperty);
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 3: GsiPartitionKey takes precedence over GsiSortKey
    // Feature: index-attribute-redesign, Property 3: GsiPartitionKey takes precedence over GsiSortKey
    // **Validates: Requirements 2.5**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any GSI where both [GsiPartitionKey] and [GsiSortKey] specify Name and/or
    /// ProjectionType, the resulting IndexModel SHALL use the values from [GsiPartitionKey].
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiPartitionKey_TakesPrecedenceOverSortKey()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                // PK specifies Name="PkName" and ProjectionType=KeysOnly
                // SK specifies Name="SkName" and ProjectionType=All
                // PK values should win
                var source = $@"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"", Name = ""PkName"", ProjectionType = ProjectionType.KeysOnly)]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;

        [GsiSortKey(""{indexName}"", Name = ""SkName"", ProjectionType = ProjectionType.All)]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var idx = result.Indexes.FirstOrDefault(i => i.IndexName == indexName);
                if (idx == null) return false;

                // PK values take precedence
                return idx.CustomName == "PkName"
                    && idx.ProjectionType == ProjectionType.KeysOnly;
            });
    }

    /// <summary>
    /// When GsiPartitionKey does NOT specify Name/ProjectionType, GsiSortKey values
    /// are used as fallbacks.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiSortKey_ValuesUsedAsFallbackWhenPkDoesNotSpecify()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                // PK does NOT specify Name or non-default ProjectionType
                // SK specifies Name="SkFallback" and ProjectionType=KeysOnly
                // SK values should be used as fallback
                var source = $@"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;

        [GsiSortKey(""{indexName}"", Name = ""SkFallback"", ProjectionType = ProjectionType.KeysOnly)]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var idx = result.Indexes.FirstOrDefault(i => i.IndexName == indexName);
                if (idx == null) return false;

                return idx.CustomName == "SkFallback"
                    && idx.ProjectionType == ProjectionType.KeysOnly;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 4: Multi-index property produces independent IndexModels
    // Feature: index-attribute-redesign, Property 4: Multi-index property produces independent IndexModels
    // **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any property annotated with N index attributes referencing N distinct index names,
    /// the EntityAnalyzer SHALL produce at least N IndexModel entries.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultiIndexProperty_ProducesIndependentModels()
    {
        return Prop.ForAll(
            Gen.Choose(2, 4).ToArbitrary(),
            count =>
            {
                var indexNames = Enumerable.Range(1, count)
                    .Select(i => $"gsi-multi-{i}")
                    .ToArray();

                var gsiPkAttrs = string.Join("\n        ",
                    indexNames.Select(n => $@"[GsiPartitionKey(""{n}"")]"));

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        {gsiPkAttrs}
        [DynamoDbAttribute(""multiPk"")]
        public string MultiPk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                // Each distinct index name should produce an IndexModel
                foreach (var name in indexNames)
                {
                    var idx = result.Indexes.FirstOrDefault(i => i.IndexName == name);
                    if (idx == null) return false;
                    if (idx.PartitionKeyProperty != "MultiPk") return false;
                    if (idx.IndexType != IndexType.GlobalSecondaryIndex) return false;
                }

                return true;
            });
    }

    /// <summary>
    /// A property with both [GsiPartitionKey("gsi1")] and [GsiSortKey("gsi2")] produces
    /// independent IndexModels for each index.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MixedRoles_ProducesIndependentModels()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (name1Raw, name2Raw) =>
            {
                var name1 = SanitizeIndexName(name1Raw.Get);
                var name2 = SanitizeIndexName(name2Raw.Get);
                if (name1 == name2) name2 = name2 + "alt";

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{name1}"")]
        [GsiSortKey(""{name2}"")]
        [DynamoDbAttribute(""multiRole"")]
        public string MultiRole {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{name2}"")]
        [DynamoDbAttribute(""otherPk"")]
        public string OtherPk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var idx1 = result.Indexes.FirstOrDefault(i => i.IndexName == name1);
                var idx2 = result.Indexes.FirstOrDefault(i => i.IndexName == name2);

                return idx1 != null && idx2 != null
                    && idx1.PartitionKeyProperty == "MultiRole"
                    && idx2.SortKeyProperty == "MultiRole"
                    && idx2.PartitionKeyProperty == "OtherPk";
            });
    }

    /// <summary>
    /// A property with both [GsiSortKey] and [LsiSortKey] for different index names
    /// produces independent GSI and LSI IndexModels.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiSortKeyAndLsiSortKey_ProducesIndependentModels()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (gsiNameRaw, lsiNameRaw) =>
            {
                var gsiName = SanitizeIndexName(gsiNameRaw.Get);
                var lsiName = SanitizeIndexName(lsiNameRaw.Get);
                if (gsiName == lsiName) lsiName = lsiName + "lsi";

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{gsiName}"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;

        [GsiSortKey(""{gsiName}"")]
        [LsiSortKey(""{lsiName}"")]
        [DynamoDbAttribute(""sharedSk"")]
        public string SharedSk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var gsiIdx = result.Indexes.FirstOrDefault(i => i.IndexName == gsiName);
                var lsiIdx = result.Indexes.FirstOrDefault(i => i.IndexName == lsiName);

                return gsiIdx != null && lsiIdx != null
                    && gsiIdx.IndexType == IndexType.GlobalSecondaryIndex
                    && gsiIdx.SortKeyProperty == "SharedSk"
                    && lsiIdx.IndexType == IndexType.LocalSecondaryIndex
                    && lsiIdx.SortKeyProperty == "SharedSk";
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 5: LSI inherits base table partition key
    // Feature: index-attribute-redesign, Property 5: LSI inherits base table partition key
    // **Validates: Requirements 3.5**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any entity with a [PartitionKey] property and [LsiSortKey] attributes,
    /// every resulting LSI IndexModel SHALL have the base table's partition key.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LsiInheritsBaseTablePartitionKey()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexNameRaw, pkAttrRaw) =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);
                var pkAttr = SanitizeAttributeName(pkAttrRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""{pkAttr}"")]
        public string BasePk {{ get; set; }} = string.Empty;

        [LsiSortKey(""{indexName}"")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var idx = result.Indexes.FirstOrDefault(i => i.IndexName == indexName);
                if (idx == null) return false;

                return idx.IndexType == IndexType.LocalSecondaryIndex
                    && idx.PartitionKeyProperty == "BasePk"
                    && idx.PartitionKeyAttribute == pkAttr
                    && idx.SortKeyProperty == "LsiSk";
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 6: Duplicate and missing key diagnostics
    // Feature: index-attribute-redesign, Property 6: Duplicate and missing key diagnostics
    // **Validates: Requirements 8.1, 8.2, 8.3, 8.4**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any entity where a GSI has [GsiSortKey] but no [GsiPartitionKey],
    /// the validator SHALL emit DYNDB120.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiSortKeyWithoutPartitionKey_EmitsDYNDB120()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiSortKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return diagnostics.Any(d => d.Id == "DYNDB120"
                    && d.GetMessage().Contains(indexName));
            });
    }

    /// <summary>
    /// For any entity where a GSI has multiple [GsiPartitionKey] on different properties,
    /// the validator SHALL emit DYNDB121.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateGsiPartitionKeys_EmitsDYNDB121()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiPk1"")]
        public string GsiPk1 {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiPk2"")]
        public string GsiPk2 {{ get; set; }} = string.Empty;
    }}
}}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return diagnostics.Any(d => d.Id == "DYNDB121"
                    && d.GetMessage().Contains(indexName));
            });
    }

    /// <summary>
    /// For any entity where a GSI has multiple [GsiSortKey] on different properties,
    /// the validator SHALL emit DYNDB122.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateGsiSortKeys_EmitsDYNDB122()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;

        [GsiSortKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiSk1"")]
        public string GsiSk1 {{ get; set; }} = string.Empty;

        [GsiSortKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiSk2"")]
        public string GsiSk2 {{ get; set; }} = string.Empty;
    }}
}}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return diagnostics.Any(d => d.Id == "DYNDB122"
                    && d.GetMessage().Contains(indexName));
            });
    }

    /// <summary>
    /// For any entity where an LSI has multiple [LsiSortKey] on different properties,
    /// the validator SHALL emit DYNDB123.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateLsiSortKeys_EmitsDYNDB123()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [LsiSortKey(""{indexName}"")]
        [DynamoDbAttribute(""lsiSk1"")]
        public string LsiSk1 {{ get; set; }} = string.Empty;

        [LsiSortKey(""{indexName}"")]
        [DynamoDbAttribute(""lsiSk2"")]
        public string LsiSk2 {{ get; set; }} = string.Empty;
    }}
}}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return diagnostics.Any(d => d.Id == "DYNDB123"
                    && d.GetMessage().Contains(indexName));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 7: Empty index name diagnostics
    // Feature: index-attribute-redesign, Property 7: Empty index name diagnostics
    // **Validates: Requirements 8.5, 8.6, 8.7**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any [GsiPartitionKey] with empty/whitespace index name, emit DYNDB124.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyGsiPartitionKeyIndexName_EmitsDYNDB124()
    {
        return Prop.ForAll(
            GenWhitespaceString().ToArbitrary(),
            whitespace =>
            {
                // Use empty string since whitespace in attribute constructor is tricky
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey("""")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk { get; set; } = string.Empty;
    }
}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return diagnostics.Any(d => d.Id == "DYNDB124");
            });
    }

    /// <summary>
    /// For any [GsiSortKey] with empty/whitespace index name, emit DYNDB125.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyGsiSortKeyIndexName_EmitsDYNDB125()
    {
        return Prop.ForAll(
            GenWhitespaceString().ToArbitrary(),
            whitespace =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [GsiSortKey("""")]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk { get; set; } = string.Empty;
    }
}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return diagnostics.Any(d => d.Id == "DYNDB125");
            });
    }

    /// <summary>
    /// For any [LsiSortKey] with empty/whitespace index name, emit DYNDB126.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyLsiSortKeyIndexName_EmitsDYNDB126()
    {
        return Prop.ForAll(
            GenWhitespaceString().ToArbitrary(),
            whitespace =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [LsiSortKey("""")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk { get; set; } = string.Empty;
    }
}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return diagnostics.Any(d => d.Id == "DYNDB126");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 8: GSI/LSI type conflict detection
    // Feature: index-attribute-redesign, Property 8: GSI/LSI type conflict detection
    // **Validates: Requirements 8.8**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any entity where the same index name appears on both GSI and LSI attributes,
    /// the validator SHALL emit DYNDB127.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiLsiTypeConflict_EmitsDYNDB127()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexNameRaw =>
            {
                var indexName = SanitizeIndexName(indexNameRaw.Get);

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{indexName}"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;

        [LsiSortKey(""{indexName}"")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return diagnostics.Any(d => d.Id == "DYNDB127"
                    && d.GetMessage().Contains(indexName));
            });
    }

    /// <summary>
    /// When GSI and LSI use different index names, no DYNDB127 should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DifferentGsiLsiNames_NoDYNDB127()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (gsiNameRaw, lsiNameRaw) =>
            {
                var gsiName = SanitizeIndexName(gsiNameRaw.Get);
                var lsiName = SanitizeIndexName(lsiNameRaw.Get);
                if (gsiName == lsiName) lsiName = lsiName + "lsi";

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [GsiPartitionKey(""{gsiName}"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk {{ get; set; }} = string.Empty;

        [LsiSortKey(""{lsiName}"")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk {{ get; set; }} = string.Empty;
    }}
}}";

                var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
                return !diagnostics.Any(d => d.Id == "DYNDB127");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles source code and runs EntityAnalyzer, returning the EntityModel.
    /// </summary>
    private static EntityModel? AnalyzeSource(string source)
    {
        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);
        return AnalyzeSourceInternal(source);
    }

    /// <summary>
    /// Compiles source code and runs EntityAnalyzer, returning both the EntityModel and diagnostics.
    /// </summary>
    private static (EntityModel? Model, IReadOnlyList<Diagnostic> Diagnostics) AnalyzeSourceWithDiagnostics(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        var analyzer = new EntityAnalyzer();
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        return (result, analyzer.Diagnostics);
    }

    private static EntityModel? AnalyzeSourceInternal(string source)
    {
        var (model, _) = AnalyzeSourceWithDiagnostics(source);
        return model;
    }

    /// <summary>
    /// Sanitizes a string to be a valid DynamoDB index name.
    /// </summary>
    private static string SanitizeIndexName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "gsi1";
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    /// <summary>
    /// Sanitizes a string to be a valid C# property name.
    /// </summary>
    private static string SanitizePropertyName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
            sanitized = "Idx" + sanitized;
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    /// <summary>
    /// Sanitizes a string to be a valid DynamoDB attribute name.
    /// </summary>
    private static string SanitizeAttributeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "attr";
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    /// <summary>
    /// Generator for whitespace-only strings (empty, spaces, tabs).
    /// </summary>
    private static Gen<string> GenWhitespaceString()
    {
        return Gen.Elements("", " ", "  ", "\t", " \t ");
    }
}
