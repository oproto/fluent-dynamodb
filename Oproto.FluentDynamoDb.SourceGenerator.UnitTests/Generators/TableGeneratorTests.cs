using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class TableGeneratorTests
{
    [Fact]
    public void Generator_WithSingleKeyTable_GeneratesCorrectGetOverload()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull("table class should be generated");
        
        var tableSource = tableCode!.SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(tableSource, source);
        
        // Verify single-key Get overload
        tableSource.ShouldContainMethod("Get", "should generate Get method for single key");
        tableSource.Should().Contain("public GetItemRequestBuilder Get(string id)", 
            "should generate Get overload with single partition key parameter");
        tableSource.Should().Contain("base.Get().WithKey(\"id\", id)", 
            "should configure key with correct attribute name");
    }

    [Fact]
    public void Generator_WithSingleKeyTable_GeneratesCorrectUpdateOverload()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify single-key Update overload
        tableSource.ShouldContainMethod("Update", "should generate Update method for single key");
        tableSource.Should().Contain("public UpdateItemRequestBuilder Update(string id)", 
            "should generate Update overload with single partition key parameter");
        tableSource.Should().Contain("base.Update().WithKey(\"id\", id)", 
            "should configure key with correct attribute name");
    }

    [Fact]
    public void Generator_WithSingleKeyTable_GeneratesCorrectDeleteOverload()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify single-key Delete overload
        tableSource.ShouldContainMethod("Delete", "should generate Delete method for single key");
        tableSource.Should().Contain("public DeleteItemRequestBuilder Delete(string id)", 
            "should generate Delete overload with single partition key parameter");
        tableSource.Should().Contain("base.Delete().WithKey(\"id\", id)", 
            "should configure key with correct attribute name");
    }

    [Fact]
    public void Generator_WithCompositeKeyTable_GeneratesCorrectGetOverload()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""transactions-table"")]
    public partial class TransactionEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string PartitionKey { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("TransactionEntityTable.g.cs"));
        tableCode.Should().NotBeNull("table class should be generated");
        
        var tableSource = tableCode!.SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(tableSource, source);
        
        // Verify composite-key Get overload
        tableSource.ShouldContainMethod("Get", "should generate Get method for composite key");
        tableSource.Should().Contain("public GetItemRequestBuilder Get(string pk, string sk)", 
            "should generate Get overload with both partition and sort key parameters");
        tableSource.Should().Contain("base.Get().WithKey(\"pk\", pk, \"sk\", sk)", 
            "should configure both keys with correct attribute names");
    }

    [Fact]
    public void Generator_WithCompositeKeyTable_GeneratesCorrectUpdateOverload()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""transactions-table"")]
    public partial class TransactionEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string PartitionKey { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("TransactionEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify composite-key Update overload
        tableSource.ShouldContainMethod("Update", "should generate Update method for composite key");
        tableSource.Should().Contain("public UpdateItemRequestBuilder Update(string pk, string sk)", 
            "should generate Update overload with both partition and sort key parameters");
        tableSource.Should().Contain("base.Update().WithKey(\"pk\", pk, \"sk\", sk)", 
            "should configure both keys with correct attribute names");
    }

    [Fact]
    public void Generator_WithCompositeKeyTable_GeneratesCorrectDeleteOverload()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""transactions-table"")]
    public partial class TransactionEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string PartitionKey { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("TransactionEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify composite-key Delete overload
        tableSource.ShouldContainMethod("Delete", "should generate Delete method for composite key");
        tableSource.Should().Contain("public DeleteItemRequestBuilder Delete(string pk, string sk)", 
            "should generate Delete overload with both partition and sort key parameters");
        tableSource.Should().Contain("base.Delete().WithKey(\"pk\", pk, \"sk\", sk)", 
            "should configure both keys with correct attribute names");
    }

    [Fact]
    public void Generator_WithSingleKeyTable_GeneratesTableConstructors()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify constructors
        tableSource.Should().Contain("public UserEntityTable(IAmazonDynamoDB client)", 
            "should generate constructor with client parameter");
        tableSource.Should().Contain("public UserEntityTable(IAmazonDynamoDB client, IDynamoDbLogger logger)", 
            "should generate constructor with client and logger parameters");
        tableSource.Should().Contain(": base(client, \"users-table\")", 
            "should call base constructor with table name");
        tableSource.Should().Contain(": base(client, \"users-table\", logger)", 
            "should call base constructor with table name and logger");
    }

    [Fact]
    public void Generator_WithGlobalSecondaryIndex_GeneratesIndexProperty()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""EmailIndex"", IsPartitionKey = true)]
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotBeEmpty(); // Will have DYNDB021 warning for "name"
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify index property
        tableSource.Should().Contain("public DynamoDbIndex EmailIndex", 
            "should generate index property");
        tableSource.Should().Contain("new DynamoDbIndex(this, \"EmailIndex\"", 
            "should instantiate DynamoDbIndex with correct index name");
    }

    [Fact]
    public void Generator_WithMultipleIndexes_GeneratesAllIndexProperties()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""EmailIndex"", IsPartitionKey = true)]
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""StatusIndex"", IsPartitionKey = true)]
        [DynamoDbAttribute(""user_status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify both index properties
        tableSource.Should().Contain("public DynamoDbIndex EmailIndex", 
            "should generate EmailIndex property");
        tableSource.Should().Contain("public DynamoDbIndex StatusIndex", 
            "should generate StatusIndex property");
    }

    [Fact]
    public void Generator_WithIndexHavingSortKey_GeneratesIndexWithBothKeys()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""StatusDateIndex"", IsPartitionKey = true)]
        [DynamoDbAttribute(""user_status"")]
        public string Status { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""StatusDateIndex"", IsSortKey = true)]
        [DynamoDbAttribute(""created_date"")]
        public string CreatedDate { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify index property with projection expression including both keys
        tableSource.Should().Contain("public DynamoDbIndex StatusDateIndex", 
            "should generate index property");
        tableSource.Should().Contain("new DynamoDbIndex(this, \"StatusDateIndex\"", 
            "should instantiate DynamoDbIndex with correct index name");
    }

    [Fact]
    public void Generator_WithNoKeys_DoesNotGenerateTableClass()
    {
        // Arrange - entity without partition key
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invalid-table"")]
    public partial class InvalidEntity
    {
        [DynamoDbAttribute(""field1"")]
        public string Field1 { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should generate other files but table class should handle missing keys gracefully
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("InvalidEntityTable.g.cs"));
        
        if (tableCode != null)
        {
            var tableSource = tableCode.SourceText.ToString();
            // If table is generated, it should not have Get/Update/Delete overloads
            tableSource.Should().NotContain("public GetItemRequestBuilder Get(", 
                "should not generate Get overload without partition key");
            tableSource.Should().NotContain("public UpdateItemRequestBuilder Update(", 
                "should not generate Update overload without partition key");
            tableSource.Should().NotContain("public DeleteItemRequestBuilder Delete(", 
                "should not generate Delete overload without partition key");
        }
    }

    [Fact]
    public void Generator_WithDifferentKeyTypes_GeneratesCorrectParameterTypes()
    {
        // Arrange - using int for partition key
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""products-table"")]
    public partial class ProductEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""product_id"")]
        public int ProductId { get; set; }
        
        [DynamoDbAttribute(""product_name"")]
        public string ProductName { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("ProductEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify int parameter type
        tableSource.Should().Contain("public GetItemRequestBuilder Get(int productId)", 
            "should generate Get overload with int parameter type");
        tableSource.Should().Contain("public UpdateItemRequestBuilder Update(int productId)", 
            "should generate Update overload with int parameter type");
        tableSource.Should().Contain("public DeleteItemRequestBuilder Delete(int productId)", 
            "should generate Delete overload with int parameter type");
    }

    [Fact]
    public void Generator_WithSnakeCaseAttributeName_GeneratesCamelCaseParameterName()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""user_id"")]
        public string UserId { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify camelCase parameter name
        tableSource.Should().Contain("Get(string userId)", 
            "should convert snake_case attribute name to camelCase parameter name");
        tableSource.Should().Contain("base.Get().WithKey(\"user_id\", userId)", 
            "should use original attribute name in WithKey call");
    }

    [Fact]
    public void Generator_GeneratedTableClass_CompilesSuccessfully()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string PartitionKey { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""TestIndex"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi_pk"")]
        public string GsiPartitionKey { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("TestEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(tableSource, source);
    }

    [Fact]
    public void Generator_WithScannableAttribute_GeneratesBothScanOverloads()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    [Scannable]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull("table class should be generated");
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify both Scan overloads are generated
        tableSource.Should().Contain("public ScanRequestBuilder Scan()", 
            "should generate parameterless Scan method");
        tableSource.Should().Contain("public ScanRequestBuilder Scan(string filterExpression, params object[] values)", 
            "should generate expression-based Scan method");
        
        // Verify the implementation
        tableSource.Should().Contain("new ScanRequestBuilder(DynamoDbClient).ForTable(Name)", 
            "parameterless Scan should create builder with table name");
        tableSource.Should().Contain("WithFilterExpressionExtensions.WithFilter(builder, filterExpression, values)", 
            "expression-based Scan should apply filter");
    }

    [Fact]
    public void Generator_WithoutScannableAttribute_DoesNotGenerateScanMethods()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull("table class should be generated");
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify no Scan methods are generated
        tableSource.Should().NotContain("public ScanRequestBuilder Scan()", 
            "should not generate Scan methods without [Scannable] attribute");
    }

    [Fact]
    public void Generator_WithScannableAttribute_IncludesXmlDocumentation()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    [Scannable]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify XML documentation is present
        tableSource.Should().Contain("/// <summary>", 
            "should include XML documentation");
        tableSource.Should().Contain("WARNING", 
            "should include warning about scan operation costs");
    }

    [Fact]
    public void Generator_WithScannableAttribute_GeneratedCodeCompiles()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [Scannable]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string PartitionKey { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("TestEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(tableSource, source);
    }

    [Fact]
    public void Generator_WithScannableAndSingleKey_GeneratesBothScanAndKeyMethods()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    [Scannable]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify single-key methods are generated
        tableSource.Should().Contain("public GetItemRequestBuilder Get(string id)", 
            "should generate Get method for single key");
        tableSource.Should().Contain("public UpdateItemRequestBuilder Update(string id)", 
            "should generate Update method for single key");
        tableSource.Should().Contain("public DeleteItemRequestBuilder Delete(string id)", 
            "should generate Delete method for single key");
        
        // Verify Scan methods are also generated
        tableSource.Should().Contain("public ScanRequestBuilder Scan()", 
            "should generate parameterless Scan method");
        tableSource.Should().Contain("public ScanRequestBuilder Scan(string filterExpression, params object[] values)", 
            "should generate expression-based Scan method");
    }

    [Fact]
    public void Generator_WithScannableAndCompositeKey_GeneratesBothScanAndKeyMethods()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""transactions-table"")]
    [Scannable]
    public partial class TransactionEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string PartitionKey { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("TransactionEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify composite-key methods are generated
        tableSource.Should().Contain("public GetItemRequestBuilder Get(string pk, string sk)", 
            "should generate Get method for composite key");
        tableSource.Should().Contain("public UpdateItemRequestBuilder Update(string pk, string sk)", 
            "should generate Update method for composite key");
        tableSource.Should().Contain("public DeleteItemRequestBuilder Delete(string pk, string sk)", 
            "should generate Delete method for composite key");
        
        // Verify Scan methods are also generated
        tableSource.Should().Contain("public ScanRequestBuilder Scan()", 
            "should generate parameterless Scan method");
        tableSource.Should().Contain("public ScanRequestBuilder Scan(string filterExpression, params object[] values)", 
            "should generate expression-based Scan method");
    }

    [Fact]
    public void Generator_WithScannableAndGSI_GeneratesScanMethodsAndIndexProperties()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    [Scannable]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""id"")]
        public string Id { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""EmailIndex"", IsPartitionKey = true)]
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""user_name"")]
        public string UserName { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        
        var tableCode = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains("UserEntityTable.g.cs"));
        tableCode.Should().NotBeNull();
        
        var tableSource = tableCode!.SourceText.ToString();
        
        // Verify GSI property is generated
        tableSource.Should().Contain("public DynamoDbIndex EmailIndex", 
            "should generate index property");
        
        // Verify Scan methods are generated
        tableSource.Should().Contain("public ScanRequestBuilder Scan()", 
            "should generate parameterless Scan method");
        tableSource.Should().Contain("public ScanRequestBuilder Scan(string filterExpression, params object[] values)", 
            "should generate expression-based Scan method");
        
        // Verify single-key methods are also generated
        tableSource.Should().Contain("public GetItemRequestBuilder Get(string id)", 
            "should generate Get method for single key");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source)
            },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Attributes.DynamoDbTableAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Amazon.DynamoDBv2.Model.AttributeValue).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Storage.IDynamoDbEntity).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll")),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Collections.dll")),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Linq.Expressions.dll"))
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }
}
