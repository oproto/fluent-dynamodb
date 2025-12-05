using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Provides debugging utilities for integration tests.
/// These utilities help diagnose test failures by dumping entity and DynamoDB item state.
/// </summary>
public static class DebugUtilities
{
    /// <summary>
    /// Dumps the state of an entity to a formatted string for debugging.
    /// Includes all properties and their values in a readable format.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to dump.</param>
    /// <param name="label">Optional label to identify the entity dump.</param>
    /// <returns>A formatted string representation of the entity state.</returns>
    public static string DumpEntity<TEntity>(TEntity entity, string? label = null)
        where TEntity : class
    {
        var sb = new StringBuilder();
        
        if (!string.IsNullOrEmpty(label))
        {
            sb.AppendLine($"=== {label} ===");
        }
        else
        {
            sb.AppendLine($"=== Entity: {typeof(TEntity).Name} ===");
        }
        
        if (entity == null)
        {
            sb.AppendLine("  (null)");
            return sb.ToString();
        }
        
        var properties = typeof(TEntity).GetProperties();
        
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(entity);
                var formattedValue = FormatPropertyValue(value);
                sb.AppendLine($"  {prop.Name}: {formattedValue}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  {prop.Name}: <Error reading value: {ex.Message}>");
            }
        }
        
        sb.AppendLine("=== End Entity ===");
        return sb.ToString();
    }
    
    /// <summary>
    /// Dumps a DynamoDB item to a formatted string for debugging.
    /// Shows all attributes and their types in a readable format.
    /// </summary>
    /// <param name="item">The DynamoDB item to dump.</param>
    /// <param name="label">Optional label to identify the item dump.</param>
    /// <returns>A formatted string representation of the DynamoDB item.</returns>
    public static string DumpDynamoDbItem(
        Dictionary<string, AttributeValue> item,
        string? label = null)
    {
        var sb = new StringBuilder();
        
        if (!string.IsNullOrEmpty(label))
        {
            sb.AppendLine($"=== {label} ===");
        }
        else
        {
            sb.AppendLine("=== DynamoDB Item ===");
        }
        
        if (item == null)
        {
            sb.AppendLine("  (null)");
            return sb.ToString();
        }
        
        if (item.Count == 0)
        {
            sb.AppendLine("  (empty)");
            return sb.ToString();
        }
        
        foreach (var kvp in item.OrderBy(x => x.Key))
        {
            var attributeName = kvp.Key;
            var attributeValue = kvp.Value;
            var formattedValue = FormatAttributeValue(attributeValue);
            
            sb.AppendLine($"  {attributeName}: {formattedValue}");
        }
        
        sb.AppendLine("=== End DynamoDB Item ===");
        return sb.ToString();
    }
    
    /// <summary>
    /// Dumps both an entity and its corresponding DynamoDB item side-by-side for comparison.
    /// Useful for debugging serialization/deserialization issues.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to dump.</param>
    /// <param name="item">The DynamoDB item to dump.</param>
    /// <param name="label">Optional label to identify the comparison.</param>
    /// <returns>A formatted string showing both representations.</returns>
    public static string DumpEntityAndItem<TEntity>(
        TEntity entity,
        Dictionary<string, AttributeValue> item,
        string? label = null)
        where TEntity : class
    {
        var sb = new StringBuilder();
        
        if (!string.IsNullOrEmpty(label))
        {
            sb.AppendLine($"=== {label} ===");
            sb.AppendLine();
        }
        
        sb.AppendLine(DumpEntity(entity, $"Entity: {typeof(TEntity).Name}"));
        sb.AppendLine();
        sb.AppendLine(DumpDynamoDbItem(item, "DynamoDB Item"));
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Compares two entities and highlights the differences.
    /// Useful for debugging why round-trip tests fail.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="expected">The expected entity.</param>
    /// <param name="actual">The actual entity.</param>
    /// <param name="label">Optional label to identify the comparison.</param>
    /// <returns>A formatted string showing the differences.</returns>
    public static string CompareEntities<TEntity>(
        TEntity expected,
        TEntity actual,
        string? label = null)
        where TEntity : class
    {
        var sb = new StringBuilder();
        
        if (!string.IsNullOrEmpty(label))
        {
            sb.AppendLine($"=== {label} ===");
        }
        else
        {
            sb.AppendLine($"=== Entity Comparison: {typeof(TEntity).Name} ===");
        }
        
        if (expected == null && actual == null)
        {
            sb.AppendLine("  Both entities are null");
            return sb.ToString();
        }
        
        if (expected == null)
        {
            sb.AppendLine("  Expected: (null)");
            sb.AppendLine($"  Actual: {DumpEntity(actual)}");
            return sb.ToString();
        }
        
        if (actual == null)
        {
            sb.AppendLine($"  Expected: {DumpEntity(expected)}");
            sb.AppendLine("  Actual: (null)");
            return sb.ToString();
        }
        
        var properties = typeof(TEntity).GetProperties();
        var hasDifferences = false;
        
        foreach (var prop in properties)
        {
            try
            {
                var expectedValue = prop.GetValue(expected);
                var actualValue = prop.GetValue(actual);
                
                if (!AreValuesEqual(expectedValue, actualValue))
                {
                    hasDifferences = true;
                    sb.AppendLine($"  {prop.Name}:");
                    sb.AppendLine($"    Expected: {FormatPropertyValue(expectedValue)}");
                    sb.AppendLine($"    Actual:   {FormatPropertyValue(actualValue)}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  {prop.Name}: <Error comparing: {ex.Message}>");
            }
        }
        
        if (!hasDifferences)
        {
            sb.AppendLine("  No differences found");
        }
        
        sb.AppendLine("=== End Comparison ===");
        return sb.ToString();
    }
    
    /// <summary>
    /// Writes entity state to the console for debugging during test execution.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to write.</param>
    /// <param name="label">Optional label to identify the output.</param>
    public static void WriteEntity<TEntity>(TEntity entity, string? label = null)
        where TEntity : class
    {
        Console.WriteLine(DumpEntity(entity, label));
    }
    
    /// <summary>
    /// Writes DynamoDB item state to the console for debugging during test execution.
    /// </summary>
    /// <param name="item">The DynamoDB item to write.</param>
    /// <param name="label">Optional label to identify the output.</param>
    public static void WriteDynamoDbItem(
        Dictionary<string, AttributeValue> item,
        string? label = null)
    {
        Console.WriteLine(DumpDynamoDbItem(item, label));
    }
    
    /// <summary>
    /// Writes entity comparison to the console for debugging during test execution.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="expected">The expected entity.</param>
    /// <param name="actual">The actual entity.</param>
    /// <param name="label">Optional label to identify the output.</param>
    public static void WriteComparison<TEntity>(
        TEntity expected,
        TEntity actual,
        string? label = null)
        where TEntity : class
    {
        Console.WriteLine(CompareEntities(expected, actual, label));
    }
    
    private static string FormatPropertyValue(object? value)
    {
        if (value == null)
        {
            return "(null)";
        }
        
        var type = value.GetType();
        
        // Handle collections
        if (value is System.Collections.IEnumerable enumerable && type != typeof(string))
        {
            var items = enumerable.Cast<object>().ToList();
            
            if (items.Count == 0)
            {
                return $"{type.Name} (empty)";
            }
            
            if (items.Count <= 5)
            {
                var itemStrings = items.Select(i => i?.ToString() ?? "(null)");
                return $"{type.Name} [{string.Join(", ", itemStrings)}]";
            }
            
            return $"{type.Name} (count: {items.Count})";
        }
        
        // Handle byte arrays specially
        if (value is byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "byte[] (empty)";
            }
            
            if (bytes.Length <= 16)
            {
                return $"byte[] [{BitConverter.ToString(bytes)}]";
            }
            
            return $"byte[] (length: {bytes.Length})";
        }
        
        return value.ToString() ?? "(null)";
    }
    
    private static string FormatAttributeValue(AttributeValue value)
    {
        if (value == null)
        {
            return "(null)";
        }
        
        // String (S)
        if (value.S != null)
        {
            return $"S: \"{value.S}\"";
        }
        
        // Number (N)
        if (value.N != null)
        {
            return $"N: {value.N}";
        }
        
        // Binary (B)
        if (value.B != null)
        {
            var bytes = value.B.ToArray();
            if (bytes.Length <= 16)
            {
                return $"B: [{BitConverter.ToString(bytes)}]";
            }
            return $"B: (length: {bytes.Length})";
        }
        
        // Boolean (BOOL)
        if (value.IsBOOLSet && value.BOOL.HasValue)
        {
            return $"BOOL: {value.BOOL.Value}";
        }
        
        // Null (NULL)
        if (value.NULL == true)
        {
            return "NULL: true";
        }
        
        // String Set (SS)
        if (value.SS?.Count > 0)
        {
            if (value.SS.Count <= 5)
            {
                return $"SS: [{string.Join(", ", value.SS.Select(s => $"\"{s}\""))}]";
            }
            return $"SS: (count: {value.SS.Count})";
        }
        
        // Number Set (NS)
        if (value.NS?.Count > 0)
        {
            if (value.NS.Count <= 5)
            {
                return $"NS: [{string.Join(", ", value.NS)}]";
            }
            return $"NS: (count: {value.NS.Count})";
        }
        
        // Binary Set (BS)
        if (value.BS?.Count > 0)
        {
            return $"BS: (count: {value.BS.Count})";
        }
        
        // List (L)
        if (value.L?.Count > 0)
        {
            if (value.L.Count <= 3)
            {
                var items = value.L.Select(FormatAttributeValue);
                return $"L: [{string.Join(", ", items)}]";
            }
            return $"L: (count: {value.L.Count})";
        }
        
        // Map (M)
        if (value.M?.Count > 0)
        {
            if (value.M.Count <= 3)
            {
                var items = value.M.Select(kvp => $"{kvp.Key}: {FormatAttributeValue(kvp.Value)}");
                return $"M: {{{string.Join(", ", items)}}}";
            }
            return $"M: (count: {value.M.Count})";
        }
        
        return "(empty AttributeValue)";
    }
    
    private static bool AreValuesEqual(object? expected, object? actual)
    {
        if (expected == null && actual == null)
        {
            return true;
        }
        
        if (expected == null || actual == null)
        {
            return false;
        }
        
        // Handle collections
        if (expected is System.Collections.IEnumerable expectedEnumerable && 
            actual is System.Collections.IEnumerable actualEnumerable &&
            expected.GetType() != typeof(string))
        {
            var expectedItems = expectedEnumerable.Cast<object>().ToList();
            var actualItems = actualEnumerable.Cast<object>().ToList();
            
            if (expectedItems.Count != actualItems.Count)
            {
                return false;
            }
            
            // For sets, order doesn't matter
            if (expected.GetType().Name.Contains("HashSet"))
            {
                return expectedItems.All(e => actualItems.Any(a => Equals(e, a))) &&
                       actualItems.All(a => expectedItems.Any(e => Equals(a, e)));
            }
            
            // For lists, order matters
            return expectedItems.SequenceEqual(actualItems);
        }
        
        return Equals(expected, actual);
    }
}
