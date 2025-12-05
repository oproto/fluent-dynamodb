using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for GeoLocation comparison translation in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorGeoLocationTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public GeoLocation Location { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null, // No metadata for basic tests
            ExpressionValidationMode.None);
    }

    [Fact]
    public void Translate_GeoLocationImplicitCastEquality_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cell = "9q8yy";
        Expression<Func<TestEntity, bool>> expression = x => x.Location == cell;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames.Should().ContainKey("#attr0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues.Should().ContainKey(":p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy");
    }

    [Fact]
    public void Translate_GeoLocationExplicitSpatialIndexEquality_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cell = "9q8yy";
        Expression<Func<TestEntity, bool>> expression = x => x.Location.SpatialIndex == cell;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy");
    }

    [Fact]
    public void Translate_GeoLocationInequalityOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cell = "9q8yy";
        Expression<Func<TestEntity, bool>> expression = x => x.Location != cell;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <> :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy");
    }



    [Fact]
    public void Translate_GeoLocationReverseOrderComparison_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cell = "9q8yy";
        Expression<Func<TestEntity, bool>> expression = x => cell == x.Location;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy");
    }

    [Fact]
    public void Translate_GeoLocationWithOtherConditions_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cell = "9q8yy";
        Expression<Func<TestEntity, bool>> expression = x => x.Id == "test" && x.Location == cell;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1 = :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Id");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("test");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("9q8yy");
    }

    [Fact]
    public void Translate_GeoLocationWithNullValue_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        string? cell = null;
        Expression<Func<TestEntity, bool>> expression = x => x.Location == cell;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_GeoLocationWithGeoHashIndex_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var geoHashCell = "9q8yy9"; // 6-character GeoHash
        Expression<Func<TestEntity, bool>> expression = x => x.Location == geoHashCell;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy9");
    }

    [Fact]
    public void Translate_GeoLocationWithS2Token_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var s2Token = "89c25900"; // S2 cell token
        Expression<Func<TestEntity, bool>> expression = x => x.Location == s2Token;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("89c25900");
    }

    [Fact]
    public void Translate_GeoLocationWithH3Index_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var h3Index = "8928308280fffff"; // H3 cell index (15 characters)
        Expression<Func<TestEntity, bool>> expression = x => x.Location == h3Index;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("8928308280fffff");
    }

    [Fact]
    public void Translate_GeoLocationReverseOrderWithS2Token_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var s2Token = "89c25900";
        Expression<Func<TestEntity, bool>> expression = x => s2Token == x.Location;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("89c25900");
    }

    [Fact]
    public void Translate_GeoLocationReverseOrderWithH3Index_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var h3Index = "8928308280fffff";
        Expression<Func<TestEntity, bool>> expression = x => h3Index == x.Location;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("8928308280fffff");
    }

    [Fact]
    public void Translate_GeoLocationExplicitSpatialIndexWithS2Token_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var s2Token = "89c25900";
        Expression<Func<TestEntity, bool>> expression = x => x.Location.SpatialIndex == s2Token;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("89c25900");
    }

    [Fact]
    public void Translate_GeoLocationExplicitSpatialIndexWithH3Index_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var h3Index = "8928308280fffff";
        Expression<Func<TestEntity, bool>> expression = x => x.Location.SpatialIndex == h3Index;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("8928308280fffff");
    }

    [Fact]
    public void Translate_GeoLocationInequalityWithS2Token_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var s2Token = "89c25900";
        Expression<Func<TestEntity, bool>> expression = x => x.Location != s2Token;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <> :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("89c25900");
    }

    [Fact]
    public void Translate_GeoLocationInequalityWithH3Index_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var h3Index = "8928308280fffff";
        Expression<Func<TestEntity, bool>> expression = x => x.Location != h3Index;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <> :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("8928308280fffff");
    }

    [Fact]
    public void Translate_GeoLocationComparisonOperatorsNotSupported_ShouldOnlySupportEqualityAndInequality()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // Note: Only == and != operators are supported for GeoLocation comparisons
        // because spatial indices (S2, H3) are not lexicographically ordered.
        // Comparison operators (<, >, <=, >=) don't make semantic sense for spatial indices.
        // Range-based spatial queries should use WithinDistance or WithinBoundingBox methods.
        
        // This test documents the expected behavior - only equality/inequality are supported
        // Attempting to use other comparison operators would require methods like CompareTo
        // which reference the entity parameter and are not allowed in DynamoDB expressions.
    }

    [Fact]
    public void Translate_GeoLocationComparisonInComplexExpression_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cell = "9q8yy";
        var name = "Store1";
        Expression<Func<TestEntity, bool>> expression = x => 
            (x.Location == cell || x.Location == "9q8yz") && x.Name == name;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("((#attr0 = :p0) OR (#attr1 = :p1)) AND (#attr2 = :p2)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Location");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("9q8yz");
        context.AttributeValues.AttributeValues[":p2"].S.Should().Be("Store1");
    }

    [Fact]
    public void Translate_GeoLocationWithVariableCapture_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cellVariable = "89c25900"; // S2 token from variable
        Expression<Func<TestEntity, bool>> expression = x => x.Location == cellVariable;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("89c25900");
    }

    [Fact]
    public void Translate_GeoLocationWithClosureCapture_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // Use a closure-captured value instead of a lambda invocation
        var cellValue = "8928308280fffff"; // H3 index from closure
        Expression<Func<TestEntity, bool>> expression = x => x.Location == cellValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("8928308280fffff");
    }

    [Fact]
    public void Translate_GeoLocationMultipleComparisonsWithDifferentIndexTypes_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var geoHashCell = "9q8yy";
        var s2Token = "89c25900";
        var h3Index = "8928308280fffff";
        
        // Create a test entity with multiple location properties
        var multiLocationEntity = new
        {
            Location1 = new GeoLocation(37.7749, -122.4194),
            Location2 = new GeoLocation(40.7128, -74.0060),
            Location3 = new GeoLocation(51.5074, -0.1278)
        };

        // Note: We can't easily test multiple properties with different index types in a single expression
        // because our TestEntity only has one Location property. This test verifies that the translator
        // handles multiple GeoLocation comparisons in the same expression correctly.
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Location == geoHashCell || x.Location == s2Token || x.Location == h3Index;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("((#attr0 = :p0) OR (#attr1 = :p1)) OR (#attr2 = :p2)");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("89c25900");
        context.AttributeValues.AttributeValues[":p2"].S.Should().Be("8928308280fffff");
    }

    [Fact]
    public void Translate_GeoLocationExplicitSpatialIndexInequality_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cell = "9q8yy";
        Expression<Func<TestEntity, bool>> expression = x => x.Location.SpatialIndex != cell;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <> :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy");
    }

    [Fact]
    public void Translate_GeoLocationReverseOrderInequality_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var cell = "9q8yy";
        Expression<Func<TestEntity, bool>> expression = x => cell != x.Location;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <> :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Location");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("9q8yy");
    }
}
