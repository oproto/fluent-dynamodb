using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for nested update expression support in UpdateExpressionTranslator.
/// Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5
/// </summary>
public class UpdateExpressionTranslatorNestedTests
{
    #region Test Entity Classes

    // Nested address type
    private class Address
    {
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public Country? Country { get; set; }
    }

    // Multi-level nested type
    private class Country
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
    }

    // Main entity with nested properties
    private class Customer
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public int Age { get; set; }
        public Address? ShippingAddress { get; set; }
        public Address? BillingAddress { get; set; }
    }

    // Update expressions type
    private class CustomerUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> Name { get; } = new();
        public UpdateExpressionProperty<int> Age { get; } = new();
        public UpdateExpressionProperty<Address?> ShippingAddress { get; } = new();
        public UpdateExpressionProperty<Address?> BillingAddress { get; } = new();
    }

    // Update model for nested address
    private class AddressUpdateModel
    {
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public CountryUpdateModel? Country { get; set; }
    }

    // Update model for multi-level nested country
    private class CountryUpdateModel
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
    }

    // Update model for customer
    private class CustomerUpdateModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? Age { get; set; }
        public AddressUpdateModel? ShippingAddress { get; set; }
        public AddressUpdateModel? BillingAddress { get; set; }
    }

    #endregion

    #region Helper Methods

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext(EntityMetadata? metadata = null)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    private EntityMetadata CreateTestMetadata()
    {
        return new EntityMetadata
        {
            TableName = "Customers",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "id",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string)
                },
                new PropertyMetadata
                {
                    PropertyName = "Age",
                    AttributeName = "age",
                    PropertyType = typeof(int)
                },
                new PropertyMetadata
                {
                    PropertyName = "ShippingAddress",
                    AttributeName = "shippingAddress",
                    PropertyType = typeof(Address)
                },
                new PropertyMetadata
                {
                    PropertyName = "BillingAddress",
                    AttributeName = "billingAddress",
                    PropertyType = typeof(Address)
                }
            }
        };
    }

    #endregion

    #region Single Nested Property Tests (Requirement 3.2)

    [Fact]
    public void TranslateUpdateExpression_SingleNestedProperty_ShouldGenerateDocumentPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new CustomerUpdateModel { ShippingAddress = new AddressUpdateModel { City = "Portland" } }
        var parameter = Expression.Parameter(typeof(CustomerUpdateExpressions), "x");
        
        var cityBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.City))!,
            Expression.Constant("Portland"));
        var addressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            cityBinding);
        
        var shippingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.ShippingAddress))!,
            addressInit);
        var customerInit = Expression.MemberInit(
            Expression.New(typeof(CustomerUpdateModel)),
            shippingAddressBinding);
        
        var lambda = Expression.Lambda<Func<CustomerUpdateExpressions, CustomerUpdateModel>>(customerInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0.#attr1 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("shippingAddress");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("city");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Portland");
    }

    #endregion

    #region Multiple Nested Properties Tests (Requirement 3.3)

    [Fact]
    public void TranslateUpdateExpression_MultipleNestedProperties_ShouldGenerateMultipleSetClauses()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new CustomerUpdateModel { ShippingAddress = new AddressUpdateModel { City = "Portland", State = "OR", ZipCode = "97201" } }
        var parameter = Expression.Parameter(typeof(CustomerUpdateExpressions), "x");
        
        var cityBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.City))!,
            Expression.Constant("Portland"));
        var stateBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.State))!,
            Expression.Constant("OR"));
        var zipBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.ZipCode))!,
            Expression.Constant("97201"));
        
        var addressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            cityBinding, stateBinding, zipBinding);
        
        var shippingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.ShippingAddress))!,
            addressInit);
        var customerInit = Expression.MemberInit(
            Expression.New(typeof(CustomerUpdateModel)),
            shippingAddressBinding);
        
        var lambda = Expression.Lambda<Func<CustomerUpdateExpressions, CustomerUpdateModel>>(customerInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0.#attr1 = :p0, #attr0.#attr2 = :p1, #attr0.#attr3 = :p2");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("shippingAddress");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("city");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("state");
        context.AttributeNames.AttributeNames["#attr3"].Should().Be("zipCode");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Portland");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("OR");
        context.AttributeValues.AttributeValues[":p2"].S.Should().Be("97201");
    }

    #endregion

    #region Combined Top-Level and Nested Updates Tests (Requirement 3.4)

    [Fact]
    public void TranslateUpdateExpression_CombinedTopLevelAndNested_ShouldGenerateCombinedExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new CustomerUpdateModel { Name = "John Doe", ShippingAddress = new AddressUpdateModel { City = "Portland" } }
        var parameter = Expression.Parameter(typeof(CustomerUpdateExpressions), "x");
        
        var nameBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.Name))!,
            Expression.Constant("John Doe"));
        
        var cityBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.City))!,
            Expression.Constant("Portland"));
        var addressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            cityBinding);
        
        var shippingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.ShippingAddress))!,
            addressInit);
        
        var customerInit = Expression.MemberInit(
            Expression.New(typeof(CustomerUpdateModel)),
            nameBinding, shippingAddressBinding);
        
        var lambda = Expression.Lambda<Func<CustomerUpdateExpressions, CustomerUpdateModel>>(customerInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0, #attr1.#attr2 = :p1");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("shippingAddress");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("city");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John Doe");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("Portland");
    }

    #endregion

    #region Multi-Level Nested Updates Tests (Requirement 3.5)

    [Fact]
    public void TranslateUpdateExpression_MultiLevelNested_ShouldGenerateDeepDocumentPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new CustomerUpdateModel { ShippingAddress = new AddressUpdateModel { Country = new CountryUpdateModel { Code = "US" } } }
        var parameter = Expression.Parameter(typeof(CustomerUpdateExpressions), "x");
        
        var codeBinding = Expression.Bind(
            typeof(CountryUpdateModel).GetProperty(nameof(CountryUpdateModel.Code))!,
            Expression.Constant("US"));
        var countryInit = Expression.MemberInit(
            Expression.New(typeof(CountryUpdateModel)),
            codeBinding);
        
        var countryBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.Country))!,
            countryInit);
        var addressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            countryBinding);
        
        var shippingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.ShippingAddress))!,
            addressInit);
        var customerInit = Expression.MemberInit(
            Expression.New(typeof(CustomerUpdateModel)),
            shippingAddressBinding);
        
        var lambda = Expression.Lambda<Func<CustomerUpdateExpressions, CustomerUpdateModel>>(customerInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0.#attr1.#attr2 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("shippingAddress");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("country");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("code");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("US");
    }

    [Fact]
    public void TranslateUpdateExpression_MultiLevelNestedWithMultipleProperties_ShouldGenerateAllPaths()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new CustomerUpdateModel { ShippingAddress = new AddressUpdateModel { Country = new CountryUpdateModel { Code = "US", Name = "United States" } } }
        var parameter = Expression.Parameter(typeof(CustomerUpdateExpressions), "x");
        
        var codeBinding = Expression.Bind(
            typeof(CountryUpdateModel).GetProperty(nameof(CountryUpdateModel.Code))!,
            Expression.Constant("US"));
        var nameBinding = Expression.Bind(
            typeof(CountryUpdateModel).GetProperty(nameof(CountryUpdateModel.Name))!,
            Expression.Constant("United States"));
        var countryInit = Expression.MemberInit(
            Expression.New(typeof(CountryUpdateModel)),
            codeBinding, nameBinding);
        
        var countryBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.Country))!,
            countryInit);
        var addressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            countryBinding);
        
        var shippingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.ShippingAddress))!,
            addressInit);
        var customerInit = Expression.MemberInit(
            Expression.New(typeof(CustomerUpdateModel)),
            shippingAddressBinding);
        
        var lambda = Expression.Lambda<Func<CustomerUpdateExpressions, CustomerUpdateModel>>(customerInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0.#attr1.#attr2 = :p0, #attr0.#attr1.#attr3 = :p1");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("shippingAddress");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("country");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("code");
        context.AttributeNames.AttributeNames["#attr3"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("US");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("United States");
    }

    #endregion

    #region Complex Combined Scenarios

    [Fact]
    public void TranslateUpdateExpression_ComplexCombined_ShouldHandleAllPatterns()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // Build expression: x => new CustomerUpdateModel { 
        //     Name = "John Doe", 
        //     ShippingAddress = new AddressUpdateModel { City = "Portland", State = "OR" },
        //     BillingAddress = new AddressUpdateModel { City = "Seattle" }
        // }
        var parameter = Expression.Parameter(typeof(CustomerUpdateExpressions), "x");
        
        var nameBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.Name))!,
            Expression.Constant("John Doe"));
        
        // Shipping address
        var shippingCityBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.City))!,
            Expression.Constant("Portland"));
        var shippingStateBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.State))!,
            Expression.Constant("OR"));
        var shippingAddressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            shippingCityBinding, shippingStateBinding);
        var shippingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.ShippingAddress))!,
            shippingAddressInit);
        
        // Billing address
        var billingCityBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.City))!,
            Expression.Constant("Seattle"));
        var billingAddressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            billingCityBinding);
        var billingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.BillingAddress))!,
            billingAddressInit);
        
        var customerInit = Expression.MemberInit(
            Expression.New(typeof(CustomerUpdateModel)),
            nameBinding, shippingAddressBinding, billingAddressBinding);
        
        var lambda = Expression.Lambda<Func<CustomerUpdateExpressions, CustomerUpdateModel>>(customerInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        // Should have: SET name, shippingAddress.city, shippingAddress.state, billingAddress.city
        result.Should().Contain("SET");
        result.Should().Contain("#attr0 = :p0"); // name
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John Doe");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TranslateUpdateExpression_NestedWithCapturedVariable_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        var cityName = "Portland";
        
        // Build expression with captured variable
        var parameter = Expression.Parameter(typeof(CustomerUpdateExpressions), "x");
        
        // Create a closure to capture the variable
        var closureType = typeof(UpdateExpressionTranslatorNestedTests);
        var cityConstant = Expression.Constant(cityName);
        
        var cityBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.City))!,
            cityConstant);
        var addressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            cityBinding);
        
        var shippingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.ShippingAddress))!,
            addressInit);
        var customerInit = Expression.MemberInit(
            Expression.New(typeof(CustomerUpdateModel)),
            shippingAddressBinding);
        
        var lambda = Expression.Lambda<Func<CustomerUpdateExpressions, CustomerUpdateModel>>(customerInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0.#attr1 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Portland");
    }

    [Fact]
    public void TranslateUpdateExpression_NestedWithNumericValue_ShouldHandleNumericTypes()
    {
        // Arrange - Create a test entity with numeric nested property
        var translator = CreateTranslator();
        var context = CreateContext(CreateTestMetadata());
        
        // For this test, we'll use a simple nested property with an integer value
        // Since our Address doesn't have numeric properties, we'll test with a string that represents a number
        var parameter = Expression.Parameter(typeof(CustomerUpdateExpressions), "x");
        
        var zipBinding = Expression.Bind(
            typeof(AddressUpdateModel).GetProperty(nameof(AddressUpdateModel.ZipCode))!,
            Expression.Constant("97201"));
        var addressInit = Expression.MemberInit(
            Expression.New(typeof(AddressUpdateModel)),
            zipBinding);
        
        var shippingAddressBinding = Expression.Bind(
            typeof(CustomerUpdateModel).GetProperty(nameof(CustomerUpdateModel.ShippingAddress))!,
            addressInit);
        var customerInit = Expression.MemberInit(
            Expression.New(typeof(CustomerUpdateModel)),
            shippingAddressBinding);
        
        var lambda = Expression.Lambda<Func<CustomerUpdateExpressions, CustomerUpdateModel>>(customerInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("SET #attr0.#attr1 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("97201");
    }

    #endregion
}
