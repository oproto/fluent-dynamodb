using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using System.Reflection;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Tests for the interface hierarchy between IReadOnlyEntity and IDynamoDbEntity.
/// Validates that the interfaces have correct method signatures and inheritance relationships.
/// </summary>
public class InterfaceHierarchyTests
{
    [Fact]
    public void IReadOnlyEntity_ShouldHaveCorrectMethodSignatures()
    {
        // Arrange
        var interfaceType = typeof(IReadOnlyEntity);
        
        // Act & Assert
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.IsGenericType.Should().BeFalse();
        
        // Should inherit from IEntityMetadataProvider
        interfaceType.GetInterfaces().Should().Contain(typeof(IEntityMetadataProvider));
        
        // Should have FromDynamoDb method (generic on method, not interface)
        var fromDynamoDbMethod = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "FromDynamoDb" && m.IsGenericMethodDefinition);
        fromDynamoDbMethod.Should().NotBeNull();
        fromDynamoDbMethod!.IsStatic.Should().BeTrue();
        fromDynamoDbMethod.IsAbstract.Should().BeTrue();
        
        // FromDynamoDb constraint should be IReadOnlyEntity
        var fromDynamoDbConstraints = fromDynamoDbMethod.GetGenericArguments()[0].GetGenericParameterConstraints();
        fromDynamoDbConstraints.Should().Contain(typeof(IReadOnlyEntity));
        
        // Should have GetPartitionKey method
        var getPartitionKeyMethod = interfaceType.GetMethod("GetPartitionKey",
            BindingFlags.Public | BindingFlags.Static,
            new[] { typeof(Dictionary<string, AttributeValue>) });
        getPartitionKeyMethod.Should().NotBeNull();
        getPartitionKeyMethod!.IsStatic.Should().BeTrue();
        getPartitionKeyMethod.IsAbstract.Should().BeTrue();
        getPartitionKeyMethod.ReturnType.Should().Be(typeof(string));
    }
    
    [Fact]
    public void IDynamoDbEntity_ShouldInheritFromIReadOnlyEntity()
    {
        // Arrange
        var interfaceType = typeof(IDynamoDbEntity);
        
        // Act & Assert
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.IsGenericType.Should().BeFalse();
        
        // Should inherit from IReadOnlyEntity
        interfaceType.GetInterfaces().Should().Contain(typeof(IReadOnlyEntity));
        
        // Should also inherit from IEntityMetadataProvider (through IReadOnlyEntity)
        var allInterfaces = GetAllInterfaces(interfaceType);
        allInterfaces.Should().Contain(typeof(IEntityMetadataProvider));
    }
    
    [Fact]
    public void IDynamoDbEntity_ShouldHaveCorrectMethodSignatures()
    {
        // Arrange
        var interfaceType = typeof(IDynamoDbEntity);
        
        // Act & Assert
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.IsGenericType.Should().BeFalse();
        
        // Should have ToDynamoDb method
        var toDynamoDbMethod = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "ToDynamoDb" && m.IsGenericMethodDefinition);
        toDynamoDbMethod.Should().NotBeNull();
        toDynamoDbMethod!.IsStatic.Should().BeTrue();
        toDynamoDbMethod.IsAbstract.Should().BeTrue();
        toDynamoDbMethod.ReturnType.Should().Be(typeof(Dictionary<string, AttributeValue>));
        
        // Should have FromDynamoDb methods (single item inherited from IReadOnlyEntity, and multiple items)
        var fromDynamoDbMethods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "FromDynamoDb" && m.IsGenericMethodDefinition).ToArray();
        // IDynamoDbEntity declares the multi-item FromDynamoDb, single-item is inherited from IReadOnlyEntity
        fromDynamoDbMethods.Should().HaveCount(1);
        
        // Should have MatchesEntity method
        var matchesEntityMethod = interfaceType.GetMethod("MatchesEntity",
            BindingFlags.Public | BindingFlags.Static,
            new[] { typeof(Dictionary<string, AttributeValue>) });
        matchesEntityMethod.Should().NotBeNull();
        matchesEntityMethod!.IsStatic.Should().BeTrue();
        matchesEntityMethod.IsAbstract.Should().BeTrue();
        matchesEntityMethod.ReturnType.Should().Be(typeof(bool));
        
        // Should have RequiresWriteTransaction property
        var requiresWriteTransactionProperty = interfaceType.GetProperty("RequiresWriteTransaction",
            BindingFlags.Public | BindingFlags.Static);
        requiresWriteTransactionProperty.Should().NotBeNull();
        requiresWriteTransactionProperty!.PropertyType.Should().Be(typeof(bool));
    }
    
    [Fact]
    public void BackwardCompatibility_ExistingMethodSignatures_ShouldBePreserved()
    {
        // Arrange
        var entityInterface = typeof(IDynamoDbEntity);
        
        // Act & Assert - Verify that all original method signatures are still present
        
        // ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        var toDynamoDbMethod = entityInterface.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "ToDynamoDb" && m.IsGenericMethodDefinition);
        toDynamoDbMethod.Should().NotBeNull();
        
        var toDynamoDbConstraints = toDynamoDbMethod!.GetGenericArguments()[0].GetGenericParameterConstraints();
        toDynamoDbConstraints.Should().Contain(typeof(IDynamoDbEntity));
        
        // FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, ...) where TSelf : IDynamoDbEntity
        var multiItemFromDynamoDbMethod = entityInterface.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "FromDynamoDb" && 
                                m.IsGenericMethodDefinition && 
                                m.GetParameters().Length == 2 &&
                                m.GetParameters()[0].ParameterType == typeof(IList<Dictionary<string, AttributeValue>>));
        multiItemFromDynamoDbMethod.Should().NotBeNull();
        
        var multiItemConstraints = multiItemFromDynamoDbMethod!.GetGenericArguments()[0].GetGenericParameterConstraints();
        multiItemConstraints.Should().Contain(typeof(IDynamoDbEntity));
    }
    
    [Fact]
    public void IReadOnlyEntity_FromDynamoDb_ShouldHaveCorrectConstraint()
    {
        // Arrange
        var readOnlyInterface = typeof(IReadOnlyEntity);
        
        // Act
        var fromDynamoDbMethod = readOnlyInterface.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "FromDynamoDb" && 
                                m.IsGenericMethodDefinition && 
                                m.GetParameters().Length == 2 &&
                                m.GetParameters()[0].ParameterType == typeof(Dictionary<string, AttributeValue>));
        
        // Assert
        fromDynamoDbMethod.Should().NotBeNull();
        
        var constraints = fromDynamoDbMethod!.GetGenericArguments()[0].GetGenericParameterConstraints();
        constraints.Should().Contain(typeof(IReadOnlyEntity));
    }
    
    [Fact]
    public void InterfaceHierarchy_ShouldProvideConsistentInheritance()
    {
        // Arrange & Act
        var readOnlyEntityType = typeof(IReadOnlyEntity);
        var entityType = typeof(IDynamoDbEntity);
        
        // Assert
        // IReadOnlyEntity should inherit from IEntityMetadataProvider
        readOnlyEntityType.GetInterfaces().Should().Contain(typeof(IEntityMetadataProvider));
        
        // IDynamoDbEntity should inherit from IReadOnlyEntity
        entityType.GetInterfaces().Should().Contain(typeof(IReadOnlyEntity));
        
        // Through inheritance, IDynamoDbEntity should also have IEntityMetadataProvider
        var allInterfaces = GetAllInterfaces(entityType);
        allInterfaces.Should().Contain(typeof(IEntityMetadataProvider));
    }
    
    private static HashSet<Type> GetAllInterfaces(Type type)
    {
        var interfaces = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(type);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var iface in current.GetInterfaces())
            {
                if (interfaces.Add(iface))
                {
                    queue.Enqueue(iface);
                }
            }
        }
        
        return interfaces;
    }
}