using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for KeysGenerator format specifier handling.
/// Validates that the KeysGenerator emits (object)paramName for indices with format specifiers
/// and does NOT emit (object)paramName for indices without specifiers.
///
/// **Validates: Requirements 3.1, 3.2, 3.5**
/// </summary>
[Trait("Feature", "computed-field-format-specifiers")]
[Trait("Property", "6")]
public class KeysGenerator_FormatSpecifierPropertyTests
{
    /// <summary>
    /// Supported .NET format specifier strings for test generation.
    /// </summary>
    private static Gen<string> FormatSpecifierGen =>
        Gen.Elements(
            "yyyy-MM-dd", "D4", "G", "N2", "HH:mm:ss", "0.00",
            "dd/MM/yyyy", "F2", "X8", "C", "P0", "E2");

    /// <summary>
    /// Typed source property definitions suitable for format specifier testing.
    /// Each tuple is (PropertyName, PropertyType).
    /// </summary>
    private static readonly (string Name, string Type)[] AvailableSourceProperties = new[]
    {
        ("EventDate", "System.DateOnly"),
        ("Priority", "int"),
        ("Amount", "decimal"),
        ("Score", "double"),
        ("CreatedAt", "System.DateTime"),
        ("Category", "string"),
        ("Name", "string"),
        ("Status", "string"),
        ("Code", "long"),
        ("Rating", "float")
    };

    /// <summary>
    /// Generates a placeholder count (1-3).
    /// </summary>
    private static Gen<int> PlaceholderCountGen => Gen.Choose(1, 3);

    /// <summary>
    /// Generates a boolean array indicating which indices have format specifiers.
    /// Ensures at least one index has a specifier (otherwise it's a no-specifier case).
    /// </summary>
    private static Gen<bool[]> SpecifierMaskWithAtLeastOneGen(int count) =>
        from bools in Gen.ArrayOf(count, Arb.Default.Bool().Generator)
        where bools.Any(b => b) // At least one must have a specifier
        select bools;

    /// <summary>
    /// Generates a boolean array where no index has a format specifier.
    /// </summary>
    private static Gen<bool[]> NoSpecifierMaskGen(int count) =>
        Gen.Constant(Enumerable.Repeat(false, count).ToArray());

    /// <summary>
    /// Builds a format string from placeholder definitions.
    /// </summary>
    private static string BuildFormatString(bool[] specifierMask, string[] specifiers)
    {
        var parts = new string[specifierMask.Length];
        for (int i = 0; i < specifierMask.Length; i++)
        {
            parts[i] = specifierMask[i] ? $"{{{i}:{specifiers[i]}}}" : $"{{{i}}}";
        }
        return string.Join("#", parts);
    }

    /// <summary>
    /// Creates an EntityModel with a computed partition key using the specified format and source properties.
    /// </summary>
    private static EntityModel CreateEntity(string format, (string Name, string Type)[] sourceProperties)
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                IsNullable = false,
                ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties.Select(sp => sp.Name).ToArray(),
                    Format = format,
                    Separator = "#"
                }
            }
        };

        foreach (var (name, type) in sourceProperties)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant(),
                IsNullable = false
            });
        }

        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true
        };
    }

    /// <summary>
    /// Converts a property name to the expected parameter name (camelCase).
    /// </summary>
    private static string ToParameterName(string propertyName) =>
        char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);

    /// <summary>
    /// **Validates: Requirements 3.1, 3.2, 3.5**
    ///
    /// Property 6: Typed Value Preservation for Format Specifier Indices
    ///
    /// For any computed format string containing a format specifier at index I,
    /// the KeysGenerator SHALL emit code that passes the source property at index I
    /// as (object)parameterName (typed value cast to object) rather than the result of GetValueExpression().
    /// Indices WITHOUT format specifiers should NOT have (object) casts.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndicesWithSpecifiers_EmitObjectCast_IndicesWithout_DoNot()
    {
        var inputGen =
            from count in PlaceholderCountGen
            from specifierMask in SpecifierMaskWithAtLeastOneGen(count)
            from specifiers in Gen.ArrayOf(count, FormatSpecifierGen)
            select (count, specifierMask, specifiers);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (count, specifierMask, specifiers) = input;
                var sourceProperties = AvailableSourceProperties.Take(count).ToArray();
                var format = BuildFormatString(specifierMask, specifiers);

                var entity = CreateEntity(format, sourceProperties);
                var result = KeysGenerator.GenerateKeysClass(entity);

                // Check each index
                for (int i = 0; i < count; i++)
                {
                    var paramName = ToParameterName(sourceProperties[i].Name);
                    var objectCast = $"(object){paramName}";

                    if (specifierMask[i])
                    {
                        // Index with format specifier: MUST contain (object)paramName
                        if (!result.Contains(objectCast))
                        {
                            return false.ToProperty()
                                .Label($"Format='{format}': index {i} has specifier but (object){paramName} not found in output");
                        }
                    }
                    else
                    {
                        // Index without format specifier: MUST NOT contain (object)paramName
                        if (result.Contains(objectCast))
                        {
                            return false.ToProperty()
                                .Label($"Format='{format}': index {i} has no specifier but (object){paramName} was found in output");
                        }
                    }
                }

                return true.ToProperty()
                    .Label($"Format='{format}': all indices correctly handled");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 3.2, 3.5**
    ///
    /// When format specifiers are present, CultureInfo.InvariantCulture MUST be included
    /// in the emitted string.Format call.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FormatSpecifiersPresent_EmitsInvariantCulture()
    {
        var inputGen =
            from count in PlaceholderCountGen
            from specifierMask in SpecifierMaskWithAtLeastOneGen(count)
            from specifiers in Gen.ArrayOf(count, FormatSpecifierGen)
            select (count, specifierMask, specifiers);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (count, specifierMask, specifiers) = input;
                var sourceProperties = AvailableSourceProperties.Take(count).ToArray();
                var format = BuildFormatString(specifierMask, specifiers);

                var entity = CreateEntity(format, sourceProperties);
                var result = KeysGenerator.GenerateKeysClass(entity);

                var containsInvariantCulture = result.Contains("System.Globalization.CultureInfo.InvariantCulture");

                return containsInvariantCulture.ToProperty()
                    .Label($"Format='{format}': InvariantCulture expected but not found");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.3**
    ///
    /// When NO format specifiers are present, (object) casts MUST NOT appear for any index,
    /// and CultureInfo.InvariantCulture MUST NOT be included.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoFormatSpecifiers_NoObjectCasts_NoInvariantCulture()
    {
        var inputGen =
            from count in PlaceholderCountGen
            from specifierMask in NoSpecifierMaskGen(count)
            select (count, specifierMask);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (count, specifierMask) = input;
                var sourceProperties = AvailableSourceProperties.Take(count).ToArray();
                var format = BuildFormatString(specifierMask, new string[count]); // specifiers unused

                var entity = CreateEntity(format, sourceProperties);
                var result = KeysGenerator.GenerateKeysClass(entity);

                // No (object) casts should be present for any parameter
                for (int i = 0; i < count; i++)
                {
                    var paramName = ToParameterName(sourceProperties[i].Name);
                    if (result.Contains($"(object){paramName}"))
                    {
                        return false.ToProperty()
                            .Label($"Format='{format}': no specifiers but (object){paramName} found");
                    }
                }

                // InvariantCulture should NOT be present
                if (result.Contains("System.Globalization.CultureInfo.InvariantCulture"))
                {
                    return false.ToProperty()
                        .Label($"Format='{format}': no specifiers but InvariantCulture found");
                }

                return true.ToProperty()
                    .Label($"Format='{format}': correctly no object casts and no InvariantCulture");
            });
    }
}
