using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for bare [RelatedEntity] attribute constructors (Task 10.1) and
/// RelationshipModel state tracking (Task 10.2).
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.5, 4.1, 4.2, 4.3, 4.4, 4.5, 9.5**
/// </summary>
[Trait("Category", "Unit")]
public class BareRelatedEntityTests
{
    #region Task 10.1: RelatedEntityAttribute Constructor Tests

    /// <summary>
    /// Parameterless constructor sets SortKeyPattern to null, signaling
    /// bare inference mode.
    ///
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void ParameterlessConstructor_SetsPatternToNull()
    {
        // Act
        var attr = new RelatedEntityAttribute();

        // Assert
        attr.SortKeyPattern.Should().BeNull(
            "parameterless constructor should leave SortKeyPattern null for bare inference");
    }

    /// <summary>
    /// String constructor preserves the provided pattern value verbatim.
    ///
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Theory]
    [InlineData("ORDER#*#LINE#*")]
    [InlineData("audit#*")]
    [InlineData("summary")]
    [InlineData("")]
    [InlineData("INVOICE#{0}#LINE#{1}")]
    public void StringConstructor_PreservesPatternValue(string pattern)
    {
        // Act
        var attr = new RelatedEntityAttribute(pattern);

        // Assert
        attr.SortKeyPattern.Should().NotBeNull(
            "string constructor should always produce a non-null SortKeyPattern");
        attr.SortKeyPattern.Should().Be(pattern,
            "string constructor should preserve the exact pattern value");
    }

    /// <summary>
    /// String constructor with null argument throws ArgumentNullException.
    ///
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Fact]
    public void StringConstructor_WithNullArgument_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new RelatedEntityAttribute(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>(
            "passing null to the string constructor must throw to distinguish from the parameterless constructor");
    }

    /// <summary>
    /// EntityType named property is independent of which constructor is used.
    /// It can be set on both bare and explicit forms.
    ///
    /// **Validates: Requirements 1.1, 1.3, 9.5**
    /// </summary>
    [Fact]
    public void EntityType_IsIndependentOfConstructorForm_Bare()
    {
        // Act
        var attr = new RelatedEntityAttribute { EntityType = typeof(string) };

        // Assert
        attr.SortKeyPattern.Should().BeNull(
            "parameterless constructor should leave SortKeyPattern null");
        attr.EntityType.Should().Be(typeof(string),
            "EntityType should be settable on bare form");
    }

    /// <summary>
    /// EntityType named property can be set alongside an explicit pattern.
    ///
    /// **Validates: Requirements 1.3, 9.5**
    /// </summary>
    [Fact]
    public void EntityType_IsIndependentOfConstructorForm_Explicit()
    {
        // Act
        var attr = new RelatedEntityAttribute("ORDER#*") { EntityType = typeof(int) };

        // Assert
        attr.SortKeyPattern.Should().Be("ORDER#*",
            "explicit pattern should be preserved");
        attr.EntityType.Should().Be(typeof(int),
            "EntityType should be settable on explicit form");
    }

    /// <summary>
    /// EntityType defaults to null when not explicitly set.
    /// </summary>
    [Fact]
    public void EntityType_DefaultsToNull_WhenNotSet()
    {
        // Act
        var bareAttr = new RelatedEntityAttribute();
        var explicitAttr = new RelatedEntityAttribute("pattern");

        // Assert
        bareAttr.EntityType.Should().BeNull(
            "EntityType should default to null on bare form");
        explicitAttr.EntityType.Should().BeNull(
            "EntityType should default to null on explicit form");
    }

    #endregion

    #region Task 10.2: RelationshipModel State Tracking Tests

    /// <summary>
    /// IsPatternInferred defaults to false on a freshly constructed RelationshipModel.
    ///
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void RelationshipModel_IsPatternInferred_DefaultsToFalse()
    {
        // Act
        var model = new RelationshipModel();

        // Assert
        model.IsPatternInferred.Should().BeFalse(
            "IsPatternInferred should default to false before any resolution");
    }

    /// <summary>
    /// IsWildcardPattern returns false when SortKeyPattern is null (bare, unresolved).
    ///
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void RelationshipModel_IsWildcardPattern_ReturnsFalse_WhenPatternIsNull()
    {
        // Act
        var model = new RelationshipModel { SortKeyPattern = null };

        // Assert
        model.IsWildcardPattern.Should().BeFalse(
            "null SortKeyPattern should yield false for IsWildcardPattern");
    }

    /// <summary>
    /// IsWildcardPattern returns true when SortKeyPattern contains a wildcard.
    /// </summary>
    [Fact]
    public void RelationshipModel_IsWildcardPattern_ReturnsTrue_WhenPatternContainsWildcard()
    {
        // Act
        var model = new RelationshipModel { SortKeyPattern = "ORDER#*#LINE#*" };

        // Assert
        model.IsWildcardPattern.Should().BeTrue(
            "pattern with wildcards should return true");
    }

    /// <summary>
    /// IsWildcardPattern returns false when SortKeyPattern has no wildcard.
    /// </summary>
    [Fact]
    public void RelationshipModel_IsWildcardPattern_ReturnsFalse_WhenPatternHasNoWildcard()
    {
        // Act
        var model = new RelationshipModel { SortKeyPattern = "summary" };

        // Assert
        model.IsWildcardPattern.Should().BeFalse(
            "pattern without wildcards should return false");
    }

    /// <summary>
    /// ResolvedEntityType defaults to null on a freshly constructed RelationshipModel.
    ///
    /// **Validates: Requirements 4.4, 4.5**
    /// </summary>
    [Fact]
    public void RelationshipModel_ResolvedEntityType_DefaultsToNull()
    {
        // Act
        var model = new RelationshipModel();

        // Assert
        model.ResolvedEntityType.Should().BeNull(
            "ResolvedEntityType should default to null before resolution");
    }

    /// <summary>
    /// SortKeyPattern defaults to null on a freshly constructed RelationshipModel.
    /// </summary>
    [Fact]
    public void RelationshipModel_SortKeyPattern_DefaultsToNull()
    {
        // Act
        var model = new RelationshipModel();

        // Assert
        model.SortKeyPattern.Should().BeNull(
            "SortKeyPattern should default to null on a new RelationshipModel");
    }

    /// <summary>
    /// After a simulated resolution, IsPatternInferred is true, SortKeyPattern is set,
    /// and ResolvedEntityType is set — matching the state after PatternResolutionPass runs.
    ///
    /// **Validates: Requirements 4.1, 4.3, 4.4**
    /// </summary>
    [Fact]
    public void RelationshipModel_AfterResolution_StateIsCorrect()
    {
        // Arrange — simulate a bare RelationshipModel before resolution
        var model = new RelationshipModel
        {
            PropertyName = "Lines",
            PropertyType = "System.Collections.Generic.List<TestNamespace.InvoiceLine>",
            IsCollection = true,
            SortKeyPattern = null,
            IsPatternInferred = false,
            ResolvedEntityType = "TestNamespace.InvoiceLine"
        };

        // Act — simulate what PatternResolutionPass.Resolve() does
        model.SortKeyPattern = "INVOICE#*#LINE#*";
        model.IsPatternInferred = true;
        model.EntityType = "InvoiceLine";

        // Assert
        model.IsPatternInferred.Should().BeTrue(
            "IsPatternInferred should be true after resolution");
        model.SortKeyPattern.Should().Be("INVOICE#*#LINE#*",
            "SortKeyPattern should be set to the child's DerivedDiscriminatorPattern");
        model.ResolvedEntityType.Should().Be("TestNamespace.InvoiceLine",
            "ResolvedEntityType should remain set from property type inference");
        model.EntityType.Should().Be("InvoiceLine",
            "EntityType should be set to the child class name during resolution");
        model.IsWildcardPattern.Should().BeTrue(
            "resolved pattern with wildcards should make IsWildcardPattern true");
    }

    /// <summary>
    /// An explicitly provided pattern keeps IsPatternInferred as false and
    /// does not populate ResolvedEntityType (unless EntityType is set).
    ///
    /// **Validates: Requirements 4.2, 4.5**
    /// </summary>
    [Fact]
    public void RelationshipModel_ExplicitPattern_IsPatternInferredRemainsFalse()
    {
        // Arrange — simulate an explicit [RelatedEntity("ORDER#*#LINE#*")]
        var model = new RelationshipModel
        {
            PropertyName = "Lines",
            PropertyType = "System.Collections.Generic.List<TestNamespace.OrderLine>",
            IsCollection = true,
            SortKeyPattern = "ORDER#*#LINE#*",
            EntityType = "OrderLine"
        };

        // Assert — no resolution pass needed for explicit patterns
        model.IsPatternInferred.Should().BeFalse(
            "explicit patterns should leave IsPatternInferred as false");
        model.SortKeyPattern.Should().Be("ORDER#*#LINE#*",
            "explicit SortKeyPattern should be preserved");
        model.ResolvedEntityType.Should().BeNull(
            "ResolvedEntityType should remain null when EntityType is set directly (not inferred from property type)");
    }

    /// <summary>
    /// HasSpecificEntityType returns true when EntityType is set, false when null or whitespace.
    /// </summary>
    [Theory]
    [InlineData("OrderLine", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    public void RelationshipModel_HasSpecificEntityType_ReflectsEntityType(string? entityType, bool expected)
    {
        // Act
        var model = new RelationshipModel { EntityType = entityType };

        // Assert
        model.HasSpecificEntityType.Should().Be(expected);
    }

    #endregion
}
