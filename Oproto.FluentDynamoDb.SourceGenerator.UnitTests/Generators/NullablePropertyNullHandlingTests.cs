// Tests for nullable property NULL handling in FromDynamoDb
// Verifies that DynamoDB NULL attribute values ({ NULL: true }) are properly handled
// for nullable properties like DateTime?, int?, etc.

using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for the fix of nullable property NULL handling.
/// 
/// Bug: When a nullable property (e.g., DateTime?) was stored as null in DynamoDB,
/// DynamoDB represents this as { NULL: true }. The generated FromDynamoDb code
/// would find the attribute (TryGetValue succeeds) but then try to parse .S which
/// is null, causing a conversion error like:
/// "Failed to convert DynamoDB attribute 'IncorporationDate' to DateTime. Attribute type: Null"
/// 
/// Fix: For nullable properties, check if the attribute value has NULL == true
/// before attempting to parse the value.
/// </summary>
public class NullablePropertyNullHandlingTests
{
    [Fact]
    public void GenerateEntityImplementation_WithNullableDateTime_GeneratesNullCheck()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Company",
            Namespace = "TestNamespace",
            TableName = "companies",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "IncorporationDate",
                    AttributeName = "incorporationDate",
                    PropertyType = "DateTime?",
                    IsNullable = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify compilation succeeds
        var entitySource = @"
using System;

namespace TestNamespace
{
    public partial class Company
    {
        public string Id { get; set; } = string.Empty;
        public DateTime? IncorporationDate { get; set; }
    }
}";
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Verify the NULL check is generated for nullable DateTime
        result.Should().Contain("incorporationdateValue.NULL == true",
            "should check for DynamoDB NULL attribute value");
        result.Should().Contain("entity.IncorporationDate = null;",
            "should assign null when DynamoDB NULL is true");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullableInt_GeneratesNullCheck()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Product",
            Namespace = "TestNamespace",
            TableName = "products",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "StockCount",
                    AttributeName = "stockCount",
                    PropertyType = "int?",
                    IsNullable = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify compilation succeeds
        var entitySource = @"
using System;

namespace TestNamespace
{
    public partial class Product
    {
        public string Id { get; set; } = string.Empty;
        public int? StockCount { get; set; }
    }
}";
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Verify the NULL check is generated for nullable int
        result.Should().Contain("stockcountValue.NULL == true",
            "should check for DynamoDB NULL attribute value");
        result.Should().Contain("entity.StockCount = null;",
            "should assign null when DynamoDB NULL is true");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullableDecimal_GeneratesNullCheck()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Order",
            Namespace = "TestNamespace",
            TableName = "orders",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "DiscountAmount",
                    AttributeName = "discountAmount",
                    PropertyType = "decimal?",
                    IsNullable = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify compilation succeeds
        var entitySource = @"
using System;

namespace TestNamespace
{
    public partial class Order
    {
        public string Id { get; set; } = string.Empty;
        public decimal? DiscountAmount { get; set; }
    }
}";
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Verify the NULL check is generated for nullable decimal
        result.Should().Contain("discountamountValue.NULL == true",
            "should check for DynamoDB NULL attribute value");
        result.Should().Contain("entity.DiscountAmount = null;",
            "should assign null when DynamoDB NULL is true");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNonNullableProperty_DoesNotGenerateNullCheck()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "User",
            Namespace = "TestNamespace",
            TableName = "users",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Age",
                    AttributeName = "age",
                    PropertyType = "int",
                    IsNullable = false
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify compilation succeeds
        var entitySource = @"
using System;

namespace TestNamespace
{
    public partial class User
    {
        public string Id { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}";
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Verify no NULL check is generated for non-nullable int
        // The property mapping should directly parse without NULL check
        result.Should().Contain("int.Parse(ageValue.N)",
            "should directly parse non-nullable int without NULL check");
        
        // Should not have the NULL check pattern for this property
        result.Should().NotContain("ageValue.NULL == true",
            "should not check for NULL on non-nullable property");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullableBool_GeneratesNullCheck()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Settings",
            Namespace = "TestNamespace",
            TableName = "settings",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "IsEnabled",
                    AttributeName = "isEnabled",
                    PropertyType = "bool?",
                    IsNullable = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify compilation succeeds
        var entitySource = @"
using System;

namespace TestNamespace
{
    public partial class Settings
    {
        public string Id { get; set; } = string.Empty;
        public bool? IsEnabled { get; set; }
    }
}";
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Verify the NULL check is generated for nullable bool
        result.Should().Contain("isenabledValue.NULL == true",
            "should check for DynamoDB NULL attribute value");
        result.Should().Contain("entity.IsEnabled = null;",
            "should assign null when DynamoDB NULL is true");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullableGuid_GeneratesNullCheck()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Document",
            Namespace = "TestNamespace",
            TableName = "documents",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "ParentId",
                    AttributeName = "parentId",
                    PropertyType = "Guid?",
                    IsNullable = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify compilation succeeds
        var entitySource = @"
using System;

namespace TestNamespace
{
    public partial class Document
    {
        public string Id { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
    }
}";
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Verify the NULL check is generated for nullable Guid
        result.Should().Contain("parentidValue.NULL == true",
            "should check for DynamoDB NULL attribute value");
        result.Should().Contain("entity.ParentId = null;",
            "should assign null when DynamoDB NULL is true");
    }

    [Fact]
    public void GenerateEntityImplementation_WithMultipleNullableProperties_GeneratesNullChecksForAll()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Employee",
            Namespace = "TestNamespace",
            TableName = "employees",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "HireDate",
                    AttributeName = "hireDate",
                    PropertyType = "DateTime?",
                    IsNullable = true
                },
                new PropertyModel
                {
                    PropertyName = "TerminationDate",
                    AttributeName = "terminationDate",
                    PropertyType = "DateTime?",
                    IsNullable = true
                },
                new PropertyModel
                {
                    PropertyName = "Salary",
                    AttributeName = "salary",
                    PropertyType = "decimal?",
                    IsNullable = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify compilation succeeds
        var entitySource = @"
using System;

namespace TestNamespace
{
    public partial class Employee
    {
        public string Id { get; set; } = string.Empty;
        public DateTime? HireDate { get; set; }
        public DateTime? TerminationDate { get; set; }
        public decimal? Salary { get; set; }
    }
}";
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Verify NULL checks are generated for all nullable properties
        result.Should().Contain("hiredateValue.NULL == true",
            "should check for DynamoDB NULL for HireDate");
        result.Should().Contain("terminationdateValue.NULL == true",
            "should check for DynamoDB NULL for TerminationDate");
        result.Should().Contain("salaryValue.NULL == true",
            "should check for DynamoDB NULL for Salary");
    }
}
