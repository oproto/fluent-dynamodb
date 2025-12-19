using AwesomeAssertions;
using Oproto.FluentDynamoDb.Logging;
using System.Reflection;

namespace Oproto.FluentDynamoDb.UnitTests.Logging;

public class LogEventIdsTests
{
    [Fact]
    public void AllEventIds_AreUnique()
    {
        // Arrange
        var eventIds = GetAllEventIds();
        
        // Act
        var duplicates = eventIds
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        
        // Assert
        duplicates.Should().BeEmpty("all event IDs should be unique");
    }
    
    [Fact]
    public void MappingOperationEventIds_AreInCorrectRange()
    {
        // Arrange
        var mappingEventIds = new[]
        {
            LogEventIds.MappingToDynamoDbStart,
            LogEventIds.MappingToDynamoDbComplete,
            LogEventIds.MappingFromDynamoDbStart,
            LogEventIds.MappingFromDynamoDbComplete,
            LogEventIds.MappingPropertyStart,
            LogEventIds.MappingPropertyComplete,
            LogEventIds.MappingPropertySkipped
        };
        
        // Act & Assert
        foreach (var eventId in mappingEventIds)
        {
            eventId.Should().BeInRange(1000, 1999, 
                $"mapping operation event IDs should be in range 1000-1999, but {eventId} is not");
        }
    }
    
    [Fact]
    public void TypeConversionEventIds_AreInCorrectRange()
    {
        // Arrange
        var conversionEventIds = new[]
        {
            LogEventIds.ConvertingMap,
            LogEventIds.ConvertingSet,
            LogEventIds.ConvertingList,
            LogEventIds.ConvertingTtl,
            LogEventIds.ConvertingJsonBlob,
            LogEventIds.ConvertingBlobReference
        };
        
        // Act & Assert
        foreach (var eventId in conversionEventIds)
        {
            eventId.Should().BeInRange(2000, 2999, 
                $"type conversion event IDs should be in range 2000-2999, but {eventId} is not");
        }
    }
    
    [Fact]
    public void DynamoDbOperationEventIds_AreInCorrectRange()
    {
        // Arrange
        var operationEventIds = new[]
        {
            LogEventIds.ExecutingGetItem,
            LogEventIds.ExecutingPutItem,
            LogEventIds.ExecutingQuery,
            LogEventIds.ExecutingUpdate,
            LogEventIds.ExecutingTransaction,
            LogEventIds.OperationComplete,
            LogEventIds.ConsumedCapacity
        };
        
        // Act & Assert
        foreach (var eventId in operationEventIds)
        {
            eventId.Should().BeInRange(3000, 3999, 
                $"DynamoDB operation event IDs should be in range 3000-3999, but {eventId} is not");
        }
    }
    
    [Fact]
    public void ErrorEventIds_AreInCorrectRange()
    {
        // Arrange
        var errorEventIds = new[]
        {
            LogEventIds.MappingError,
            LogEventIds.ConversionError,
            LogEventIds.JsonSerializationError,
            LogEventIds.BlobStorageError,
            LogEventIds.DynamoDbOperationError
        };
        
        // Act & Assert
        foreach (var eventId in errorEventIds)
        {
            eventId.Should().BeInRange(9000, 9999, 
                $"error event IDs should be in range 9000-9999, but {eventId} is not");
        }
    }
    
    [Fact]
    public void MappingToDynamoDbStart_HasCorrectValue()
    {
        LogEventIds.MappingToDynamoDbStart.Should().Be(1000);
    }
    
    [Fact]
    public void MappingToDynamoDbComplete_HasCorrectValue()
    {
        LogEventIds.MappingToDynamoDbComplete.Should().Be(1001);
    }
    
    [Fact]
    public void MappingFromDynamoDbStart_HasCorrectValue()
    {
        LogEventIds.MappingFromDynamoDbStart.Should().Be(1010);
    }
    
    [Fact]
    public void MappingFromDynamoDbComplete_HasCorrectValue()
    {
        LogEventIds.MappingFromDynamoDbComplete.Should().Be(1011);
    }
    
    [Fact]
    public void ConvertingMap_HasCorrectValue()
    {
        LogEventIds.ConvertingMap.Should().Be(2000);
    }
    
    [Fact]
    public void ConvertingSet_HasCorrectValue()
    {
        LogEventIds.ConvertingSet.Should().Be(2010);
    }
    
    [Fact]
    public void ExecutingGetItem_HasCorrectValue()
    {
        LogEventIds.ExecutingGetItem.Should().Be(3000);
    }
    
    [Fact]
    public void ExecutingQuery_HasCorrectValue()
    {
        LogEventIds.ExecutingQuery.Should().Be(3020);
    }
    
    [Fact]
    public void MappingError_HasCorrectValue()
    {
        LogEventIds.MappingError.Should().Be(9000);
    }
    
    [Fact]
    public void ConversionError_HasCorrectValue()
    {
        LogEventIds.ConversionError.Should().Be(9010);
    }
    
    private static List<int> GetAllEventIds()
    {
        var eventIds = new List<int>();
        var type = typeof(LogEventIds);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        
        foreach (var field in fields)
        {
            if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(int))
            {
                var value = (int)field.GetValue(null)!;
                eventIds.Add(value);
            }
        }
        
        return eventIds;
    }
}
