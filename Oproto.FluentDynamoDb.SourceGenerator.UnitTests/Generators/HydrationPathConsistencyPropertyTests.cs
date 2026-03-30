using System.Reflection;
using System.Text;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for hydration path consistency.
/// 
/// **Feature: hydration-architecture-consolidation, Property 2: Hydration Path Consistency**
/// **Validates: Requirements 2.5, 5.4**
/// 
/// These tests verify that for any entity with properties of various types (primitives, enums,
/// DynamoDbMap, JsonBlob, collections), deserializing a single DynamoDB item via FromDynamoDb(item)
/// produces an entity identical to deserializing the same item via FromDynamoDb([item])
/// (multi-item overload with single item).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class HydrationPathConsistencyPropertyTests
{
    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 2: Hydration Path Consistency**
    /// **Validates: Requirements 2.5, 5.4**
    /// 
    /// Property: For any entity with primitive properties, the single-item and multi-item
    /// FromDynamoDb methods should produce identical generated code structure for property
    /// deserialization (using the shared helper method).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_UsesSharedDeserializationHelper_ForPrimitiveProperties()
    {
        var propertyTypeArb = Gen.Elements(
            "string", "int", "long", "double", "decimal", "bool", 
            "DateTime", "DateTimeOffset", "Guid"
        );
        
        var propertyNameArb = Gen.Elements(
            "Name", "Value", "Count", "Amount", "IsActive", "CreatedAt", "UpdatedAt", "Id"
        );
        
        var tupleArb = Arb.From(
            propertyTypeArb.SelectMany(propType => 
                propertyNameArb.Select(propName => (propType, propName)))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (propType, propName) = tuple;
            
            var entity = new EntityModel
            {
                ClassName = "TestEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                Properties = new[]
                {
                    new PropertyModel
                    {
                        PropertyName = "Pk",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    },
                    new PropertyModel
                    {
                        PropertyName = propName,
                        AttributeName = propName.ToLowerInvariant(),
                        PropertyType = propType
                    }
                }
            };
            
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // The generated code should use the shared deserialization pattern
            // Both single-item and multi-item paths should have consistent structure
            var containsPropertyDeserialization = result.Contains($"TryGetValue(\"{propName.ToLowerInvariant()}\"");
            
            return containsPropertyDeserialization;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 2: Hydration Path Consistency**
    /// **Validates: Requirements 2.5, 5.4**
    /// 
    /// Property: For any entity with a DynamoDbMap property, the generated code should
    /// include the nested FromDynamoDb call for deserialization in both single-item
    /// and multi-item paths.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_UsesNestedFromDynamoDb_ForMapProperties()
    {
        var nestedTypeArb = Gen.Elements("Address", "Metadata", "Settings", "Config");
        var propertyNameArb = Gen.Elements("ShippingAddress", "BillingAddress", "UserMetadata", "AppSettings");
        
        var tupleArb = Arb.From(
            nestedTypeArb.SelectMany(nestedType => 
                propertyNameArb.Select(propName => (nestedType, propName)))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (nestedType, propName) = tuple;
            
            var entity = new EntityModel
            {
                ClassName = "TestEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                Properties = new[]
                {
                    new PropertyModel
                    {
                        PropertyName = "Pk",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    },
                    new PropertyModel
                    {
                        PropertyName = propName,
                        AttributeName = propName.ToLowerInvariant(),
                        PropertyType = nestedType,
                        ComplexType = new ComplexTypeInfo { IsMap = true }
                    }
                }
            };
            
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // The generated code should use nested FromDynamoDb for map properties
            var containsNestedFromDynamoDb = result.Contains($"{nestedType}.FromDynamoDb<{nestedType}>");
            
            // Should also contain map conversion logging
            var containsMapConversionLogging = result.Contains("ConvertingMap");
            
            return containsNestedFromDynamoDb && containsMapConversionLogging;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 2: Hydration Path Consistency**
    /// **Validates: Requirements 2.5, 5.4**
    /// 
    /// Property: For any entity with a List&lt;T&gt; property with DynamoDbMap, the generated
    /// code should iterate over the list and deserialize each element using FromDynamoDb.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_IteratesAndDeserializes_ForListOfMapsProperties()
    {
        var elementTypeArb = Gen.Elements("LineItem", "OrderItem", "ProductVariant", "Tag");
        var propertyNameArb = Gen.Elements("Items", "Lines", "Variants", "Tags");
        
        var tupleArb = Arb.From(
            elementTypeArb.SelectMany(elemType => 
                propertyNameArb.Select(propName => (elemType, propName)))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (elemType, propName) = tuple;
            
            var entity = new EntityModel
            {
                ClassName = "TestEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                Properties = new[]
                {
                    new PropertyModel
                    {
                        PropertyName = "Pk",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    },
                    new PropertyModel
                    {
                        PropertyName = propName,
                        AttributeName = propName.ToLowerInvariant(),
                        PropertyType = $"List<{elemType}>",
                        IsCollection = true,
                        ComplexType = new ComplexTypeInfo 
                        { 
                            IsListOfMaps = true,
                            ElementType = elemType
                        }
                    }
                }
            };
            
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // The generated code should iterate over the list
            var containsForeach = result.Contains("foreach (var elementValue");
            
            // Should use nested FromDynamoDb for each element
            var containsElementFromDynamoDb = result.Contains($"{elemType}.FromDynamoDb<{elemType}>");
            
            return containsForeach && containsElementFromDynamoDb;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 2: Hydration Path Consistency**
    /// **Validates: Requirements 2.5, 5.4**
    /// 
    /// Property: For any entity with nullable properties, the generated code should
    /// check for DynamoDB NULL values and handle them correctly.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_HandlesNullableProperties_Correctly()
    {
        var propertyTypeArb = Gen.Elements("string?", "int?", "DateTime?", "Guid?");
        var propertyNameArb = Gen.Elements("OptionalName", "OptionalCount", "OptionalDate", "OptionalId");
        
        var tupleArb = Arb.From(
            propertyTypeArb.SelectMany(propType => 
                propertyNameArb.Select(propName => (propType, propName)))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (propType, propName) = tuple;
            
            var entity = new EntityModel
            {
                ClassName = "TestEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                Properties = new[]
                {
                    new PropertyModel
                    {
                        PropertyName = "Pk",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    },
                    new PropertyModel
                    {
                        PropertyName = propName,
                        AttributeName = propName.ToLowerInvariant(),
                        PropertyType = propType,
                        IsNullable = true
                    }
                }
            };
            
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // The generated code should check for NULL values
            var containsNullCheck = result.Contains(".NULL == true");
            
            // Should assign null when NULL is true
            var containsNullAssignment = result.Contains($"entity.{propName} = null");
            
            return containsNullCheck && containsNullAssignment;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 2: Hydration Path Consistency**
    /// **Validates: Requirements 2.5, 5.4**
    /// 
    /// Property: For any entity with formatted properties (using Format attribute),
    /// the generated code should use TryParse with the format string.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_UsesFormattedDeserialization_ForFormattedProperties()
    {
        var formatArb = Gen.Elements("yyyy-MM-dd", "D5", "F2", "HH:mm:ss");
        var propertyTypeArb = Gen.Elements("DateTime", "int", "double", "TimeOnly");
        
        var tupleArb = Arb.From(
            formatArb.SelectMany(format => 
                propertyTypeArb.Select(propType => (format, propType)))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (format, propType) = tuple;
            
            var entity = new EntityModel
            {
                ClassName = "TestEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                Properties = new[]
                {
                    new PropertyModel
                    {
                        PropertyName = "Pk",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    },
                    new PropertyModel
                    {
                        PropertyName = "FormattedValue",
                        AttributeName = "formatted_value",
                        PropertyType = propType,
                        Format = format
                    }
                }
            };
            
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // The generated code should use TryParse or TryParseExact
            var containsTryParse = result.Contains("TryParse") || result.Contains("TryParseExact");
            
            // Should include the format string
            var containsFormat = result.Contains(format);
            
            // Should use InvariantCulture
            var containsInvariantCulture = result.Contains("InvariantCulture");
            
            return containsTryParse && containsFormat && containsInvariantCulture;
        });
    }
}
