using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Simple validation test for interface hierarchy that doesn't depend on assertion libraries.
/// This test validates the interface structure by attempting to compile code that uses the interfaces.
/// </summary>
public class InterfaceHierarchyValidationTest
{
    [Fact]
    public void InterfaceHierarchy_CompilationTest_ShouldCompile()
    {
        // This test validates that the interface hierarchy compiles correctly
        // by attempting to use the interfaces in various ways
        
        // Test 1: IReadOnlyEntity should be a valid non-generic interface
        var readOnlyEntityType = typeof(IReadOnlyEntity);
        Assert.True(readOnlyEntityType.IsInterface);
        Assert.False(readOnlyEntityType.IsGenericType);
        
        // Test 2: IDynamoDbEntity should be a valid non-generic interface
        var entityType = typeof(IDynamoDbEntity);
        Assert.True(entityType.IsInterface);
        Assert.False(entityType.IsGenericType);
        
        // Test 3: IDynamoDbEntity should inherit from IReadOnlyEntity
        var entityBaseInterfaces = entityType.GetInterfaces();
        Assert.Contains(typeof(IReadOnlyEntity), entityBaseInterfaces);
        
        // Test 4: All interfaces should inherit from IEntityMetadataProvider
        Assert.Contains(typeof(IEntityMetadataProvider), readOnlyEntityType.GetInterfaces());
        
        // Through inheritance, IDynamoDbEntity should also have IEntityMetadataProvider
        var allEntityInterfaces = GetAllInterfaces(entityType);
        Assert.Contains(typeof(IEntityMetadataProvider), allEntityInterfaces);
    }
    
    [Fact]
    public void InterfaceHierarchy_MethodSignatures_ShouldBeCorrect()
    {
        // Test IReadOnlyEntity method signatures
        var readOnlyEntityType = typeof(IReadOnlyEntity);
        
        // Should have FromDynamoDb method (generic on method)
        var fromDynamoDbMethods = readOnlyEntityType.GetMethods()
            .Where(m => m.Name == "FromDynamoDb" && m.IsGenericMethodDefinition).ToArray();
        Assert.Single(fromDynamoDbMethods);
        
        var fromDynamoDbMethod = fromDynamoDbMethods[0];
        Assert.True(fromDynamoDbMethod.IsStatic);
        Assert.True(fromDynamoDbMethod.IsAbstract);
        
        // Should have GetPartitionKey method
        var getPartitionKeyMethod = readOnlyEntityType.GetMethod("GetPartitionKey");
        Assert.NotNull(getPartitionKeyMethod);
        Assert.True(getPartitionKeyMethod.IsStatic);
        Assert.True(getPartitionKeyMethod.IsAbstract);
        Assert.Equal(typeof(string), getPartitionKeyMethod.ReturnType);
        
        // Test IDynamoDbEntity method signatures
        var entityType = typeof(IDynamoDbEntity);
        
        // Should have ToDynamoDb method
        var toDynamoDbMethods = entityType.GetMethods()
            .Where(m => m.Name == "ToDynamoDb" && m.IsGenericMethodDefinition).ToArray();
        Assert.Single(toDynamoDbMethods);
        
        var toDynamoDbMethod = toDynamoDbMethods[0];
        Assert.True(toDynamoDbMethod.IsStatic);
        Assert.True(toDynamoDbMethod.IsAbstract);
        Assert.Equal(typeof(Dictionary<string, AttributeValue>), toDynamoDbMethod.ReturnType);
        
        // Should have RequiresWriteTransaction property
        var requiresWriteTransactionProperty = entityType.GetProperty("RequiresWriteTransaction");
        Assert.NotNull(requiresWriteTransactionProperty);
        Assert.Equal(typeof(bool), requiresWriteTransactionProperty.PropertyType);
    }
    
    [Fact]
    public void InterfaceHierarchy_MethodConstraints_ShouldBeCorrect()
    {
        // Test IReadOnlyEntity.FromDynamoDb constraint
        var readOnlyEntityType = typeof(IReadOnlyEntity);
        var fromDynamoDbMethod = readOnlyEntityType.GetMethods()
            .First(m => m.Name == "FromDynamoDb" && m.IsGenericMethodDefinition);
        
        var fromDynamoDbConstraints = fromDynamoDbMethod.GetGenericArguments()[0].GetGenericParameterConstraints();
        Assert.Single(fromDynamoDbConstraints);
        Assert.Equal(typeof(IReadOnlyEntity), fromDynamoDbConstraints[0]);
        
        // Test IDynamoDbEntity.ToDynamoDb constraint
        var entityType = typeof(IDynamoDbEntity);
        var toDynamoDbMethod = entityType.GetMethods()
            .First(m => m.Name == "ToDynamoDb" && m.IsGenericMethodDefinition);
        
        var toDynamoDbConstraints = toDynamoDbMethod.GetGenericArguments()[0].GetGenericParameterConstraints();
        Assert.Single(toDynamoDbConstraints);
        Assert.Equal(typeof(IDynamoDbEntity), toDynamoDbConstraints[0]);
        
        // Test IDynamoDbEntity.FromDynamoDb (multi-item) constraint
        var multiItemFromDynamoDbMethod = entityType.GetMethods()
            .First(m => m.Name == "FromDynamoDb" && 
                       m.IsGenericMethodDefinition && 
                       m.GetParameters().Length == 2 &&
                       m.GetParameters()[0].ParameterType == typeof(IList<Dictionary<string, AttributeValue>>));
        
        var multiItemConstraints = multiItemFromDynamoDbMethod.GetGenericArguments()[0].GetGenericParameterConstraints();
        Assert.Single(multiItemConstraints);
        Assert.Equal(typeof(IDynamoDbEntity), multiItemConstraints[0]);
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