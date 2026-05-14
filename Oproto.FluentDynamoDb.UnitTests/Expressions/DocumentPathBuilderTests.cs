using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for DocumentPathBuilder class.
/// Validates document path building for nested properties and list indices.
/// </summary>
public class DocumentPathBuilderTests
{
    private AttributeNameInternal CreateAttributeNames() => new();

    #region Single Property Path Tests

    [Fact]
    public void Build_SingleProperty_ShouldReturnPropertyPlaceholder()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Name", "name");
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0");
        attributeNames.AttributeNames["#attr0"].Should().Be("name");
    }

    [Fact]
    public void Build_SinglePropertyWithoutAttributeName_ShouldUsePropertyName()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Name");
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0");
        attributeNames.AttributeNames["#attr0"].Should().Be("Name");
    }

    #endregion

    #region Nested Property Path Tests

    [Fact]
    public void Build_TwoLevelNestedProperty_ShouldReturnDotSeparatedPath()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Address", "address");
        builder.AddProperty("City", "city");
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0.#attr1");
        attributeNames.AttributeNames["#attr0"].Should().Be("address");
        attributeNames.AttributeNames["#attr1"].Should().Be("city");
    }

    [Fact]
    public void Build_ThreeLevelNestedProperty_ShouldReturnCorrectPath()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Address", "address");
        builder.AddProperty("Country", "country");
        builder.AddProperty("Code", "code");
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0.#attr1.#attr2");
        attributeNames.AttributeNames["#attr0"].Should().Be("address");
        attributeNames.AttributeNames["#attr1"].Should().Be("country");
        attributeNames.AttributeNames["#attr2"].Should().Be("code");
    }

    #endregion

    #region List Index Path Tests

    [Fact]
    public void Build_PropertyWithIndex_ShouldReturnCorrectPath()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Tags", "tags");
        builder.AddIndex(0);
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0[0]");
        attributeNames.AttributeNames["#attr0"].Should().Be("tags");
    }

    [Fact]
    public void Build_PropertyWithLargerIndex_ShouldReturnCorrectPath()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Items", "items");
        builder.AddIndex(42);
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0[42]");
    }

    #endregion

    #region Mixed Property and Index Path Tests

    [Fact]
    public void Build_PropertyIndexProperty_ShouldReturnCorrectPath()
    {
        // Arrange - #items[0].#name pattern
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Items", "items");
        builder.AddIndex(0);
        builder.AddProperty("Name", "name");
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0[0].#attr1");
        attributeNames.AttributeNames["#attr0"].Should().Be("items");
        attributeNames.AttributeNames["#attr1"].Should().Be("name");
    }

    [Fact]
    public void Build_NestedPropertyWithIndex_ShouldReturnCorrectPath()
    {
        // Arrange - #metadata.#keywords[0] pattern
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Metadata", "metadata");
        builder.AddProperty("Keywords", "keywords");
        builder.AddIndex(0);
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0.#attr1[0]");
        attributeNames.AttributeNames["#attr0"].Should().Be("metadata");
        attributeNames.AttributeNames["#attr1"].Should().Be("keywords");
    }

    [Fact]
    public void Build_ComplexMixedPath_ShouldReturnCorrectPath()
    {
        // Arrange - #orders[0].#lineItems[1].#productId pattern
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("Orders", "orders");
        builder.AddIndex(0);
        builder.AddProperty("LineItems", "lineItems");
        builder.AddIndex(1);
        builder.AddProperty("ProductId", "productId");
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0[0].#attr1[1].#attr2");
        attributeNames.AttributeNames["#attr0"].Should().Be("orders");
        attributeNames.AttributeNames["#attr1"].Should().Be("lineItems");
        attributeNames.AttributeNames["#attr2"].Should().Be("productId");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Build_EmptyPath_ShouldReturnEmptyString()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        var result = builder.Build();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SegmentCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act & Assert
        builder.SegmentCount.Should().Be(0);
        
        builder.AddProperty("Address", "address");
        builder.SegmentCount.Should().Be(1);
        
        builder.AddProperty("City", "city");
        builder.SegmentCount.Should().Be(2);
        
        builder.AddIndex(0);
        builder.SegmentCount.Should().Be(3);
    }

    [Fact]
    public void AddProperty_ShouldReturnPlaceholder()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        var placeholder1 = builder.AddProperty("Address", "address");
        var placeholder2 = builder.AddProperty("City", "city");

        // Assert
        placeholder1.Should().Be("#attr0");
        placeholder2.Should().Be("#attr1");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Constructor_NullAttributeNames_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new DocumentPathBuilder(null!);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("attributeNames");
    }

    [Fact]
    public void AddProperty_NullPropertyName_ShouldThrowArgumentException()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act & Assert
        var action = () => builder.AddProperty(null!);
        action.Should().Throw<ArgumentException>()
            .WithParameterName("propertyName");
    }

    [Fact]
    public void AddProperty_EmptyPropertyName_ShouldThrowArgumentException()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act & Assert
        var action = () => builder.AddProperty(string.Empty);
        action.Should().Throw<ArgumentException>()
            .WithParameterName("propertyName");
    }

    [Fact]
    public void AddIndex_NegativeIndex_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act & Assert
        var action = () => builder.AddIndex(-1);
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
    }

    #endregion

    #region Attribute Name Placeholder Generation Tests

    [Fact]
    public void Build_MultipleProperties_ShouldGenerateSequentialPlaceholders()
    {
        // Arrange
        var attributeNames = CreateAttributeNames();
        var builder = new DocumentPathBuilder(attributeNames);

        // Act
        builder.AddProperty("A", "a");
        builder.AddProperty("B", "b");
        builder.AddProperty("C", "c");
        builder.AddProperty("D", "d");
        builder.AddProperty("E", "e");
        builder.AddProperty("F", "f");
        builder.AddProperty("G", "g");
        builder.AddProperty("H", "h");
        builder.AddProperty("I", "i");
        builder.AddProperty("J", "j");
        builder.AddProperty("K", "k"); // 11th property - tests double-digit handling
        var result = builder.Build();

        // Assert
        result.Should().Be("#attr0.#attr1.#attr2.#attr3.#attr4.#attr5.#attr6.#attr7.#attr8.#attr9.#attr10");
        attributeNames.AttributeNames.Should().HaveCount(11);
    }

    #endregion
}
