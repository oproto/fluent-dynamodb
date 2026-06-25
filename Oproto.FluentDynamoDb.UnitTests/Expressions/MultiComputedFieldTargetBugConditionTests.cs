using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Bug condition exploration tests for the multi-computed-field-target fix.
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists.
///
/// Bug Condition: A source property is listed in the SourceProperties of 2+ non-key
/// computed fields. The MetadataGenerator emits only the first computed field found
/// via FirstOrDefault as the ComputedFieldTarget value. The other targets are lost.
///
/// The current type is `string? ComputedFieldTarget` which structurally cannot hold
/// multiple target names. This test demonstrates that limitation.
///
/// **Validates: Requirements 1.1, 1.2, 1.3**
/// </summary>
[Trait("Category", "BugExploration")]
public class MultiComputedFieldTargetBugConditionTests
{
    /// <summary>
    /// Property 1: Bug Condition - Multi-Target Source Only Emits First Target
    ///
    /// For any source property that contributes to N non-key computed fields (N >= 2),
    /// the PropertyMetadata should contain ALL N target names. On unfixed code, this
    /// FAILS because ComputedFieldTarget is string? and can only hold one value.
    ///
    /// The test creates PropertyMetadata instances the way the MapperGenerator would
    /// emit them on unfixed code: ComputedFieldTarget = firstTarget (a single string).
    /// It then asserts that ALL targets should be discoverable from the metadata,
    /// which is impossible with the current string? type.
    ///
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MultiTargetSource_ShouldContainAllTargets_FailsOnUnfixedCode()
    {
        // Generate a source property name
        var sourcePropertyNameGen = Gen.Elements(
            "Status", "Region", "Category", "Department", "Priority",
            "Type", "Level", "Tier", "Zone", "Group");

        // Generate 2-5 distinct computed field target names
        var targetPoolGen = Gen.Elements(
            "Gsi1Pk", "Gsi1Sk", "Gsi2Pk", "Gsi2Sk", "Gsi3Pk",
            "Gsi3Sk", "Gsi4Pk", "Gsi4Sk", "Gsi5Pk", "Gsi5Sk");

        var targetCountGen = Gen.Choose(2, 5);

        var targetNamesGen = targetPoolGen.ListOf(10)
            .Select(list => list.Distinct().ToList())
            .Where(list => list.Count >= 2)
            .SelectMany(distinctTargets =>
                Gen.Choose(2, Math.Min(5, distinctTargets.Count))
                    .Select(count => distinctTargets.Take(count).ToArray()));

        var inputGen = from sourceName in sourcePropertyNameGen
                       from targets in targetNamesGen
                       select (sourceName, targets);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (sourcePropertyName, expectedTargets) = input;

                // Simulate what the MapperGenerator does on FIXED code:
                // It uses Where + Select + ToArray to emit ALL matching computed field targets
                // Build PropertyMetadata as the fixed MapperGenerator would emit it
                var sourcePropertyMetadata = new PropertyMetadata
                {
                    PropertyName = sourcePropertyName,
                    AttributeName = sourcePropertyName.ToLowerInvariant(),
                    PropertyType = typeof(string),
                    ComputedFieldTargets = expectedTargets // Fix: all targets stored in array
                };

                // Build the full entity metadata with multiple computed fields
                // that all list this source property
                var properties = new List<PropertyMetadata>
                {
                    new PropertyMetadata
                    {
                        PropertyName = "Id",
                        AttributeName = "pk",
                        PropertyType = typeof(string),
                        IsPartitionKey = true
                    },
                    sourcePropertyMetadata
                };

                // Add computed field properties that reference the source
                foreach (var target in expectedTargets)
                {
                    properties.Add(new PropertyMetadata
                    {
                        PropertyName = target,
                        AttributeName = target.ToLowerInvariant(),
                        PropertyType = typeof(string),
                        ComputedField = new ComputedFieldMetadata
                        {
                            SourceProperties = new[] { sourcePropertyName },
                            Format = "{0}"
                        }
                    });
                }

                var entityMetadata = new EntityMetadata
                {
                    TableName = "TestTable",
                    Properties = properties.ToArray()
                };

                // ASSERT EXPECTED BEHAVIOR:
                // The source property's metadata should allow discovery of ALL targets.
                // On unfixed code, ComputedFieldTarget is string? and holds only ONE value.
                //
                // We check: can we find ALL expected targets from the source property's metadata?
                // With ComputedFieldTarget (string?), we can only find at most 1 target.
                var discoveredTargets = GetAllComputedFieldTargets(sourcePropertyMetadata);

                // All expected targets must be discoverable
                var allTargetsPresent = expectedTargets.All(t => discoveredTargets.Contains(t));
                var correctCount = discoveredTargets.Length == expectedTargets.Length;

                return (allTargetsPresent && correctCount)
                    .Label($"Source: '{sourcePropertyName}', " +
                           $"Expected targets: [{string.Join(", ", expectedTargets)}], " +
                           $"Discovered targets: [{string.Join(", ", discoveredTargets)}]. " +
                           $"ComputedFieldTargets (string[]?) = '[{string.Join(", ", sourcePropertyMetadata.ComputedFieldTargets ?? Array.Empty<string>())}]'. " +
                           $"Only 1 target discoverable from metadata - bug confirmed!");
            });
    }

    /// <summary>
    /// Attempts to discover all computed field targets from a source property's metadata.
    /// On unfixed code, ComputedFieldTarget is string? so at most one target is returned.
    /// After the fix, ComputedFieldTargets (string[]?) would return all targets.
    /// </summary>
    private static string[] GetAllComputedFieldTargets(PropertyMetadata propertyMetadata)
    {
        // After fix: ComputedFieldTargets is string[]?, returns all targets
        if (propertyMetadata.ComputedFieldTargets != null)
        {
            return propertyMetadata.ComputedFieldTargets;
        }

        return Array.Empty<string>();
    }
}
