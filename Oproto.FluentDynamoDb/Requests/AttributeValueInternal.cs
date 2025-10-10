using Amazon.DynamoDBv2.Model;
using System.Globalization;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Internal class for managing attribute value mappings in DynamoDB expressions.
/// This class handles the collection and type conversion of expression attribute values
/// that are used to parameterize DynamoDB expressions safely.
/// </summary>
public class AttributeValueInternal()
{
    public Dictionary<string, AttributeValue> AttributeValues { get; init; } = new Dictionary<string, AttributeValue>();
    private readonly ParameterGenerator _parameterGenerator = new();
    
    public void WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        foreach(KeyValuePair<string,AttributeValue> kvp in attributeValues)
        {
            this.AttributeValues.Add(kvp.Key, kvp.Value);
        }
    }
    
    public void WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        foreach(KeyValuePair<string,AttributeValue> kvp in attributeValues)
        {
            this.AttributeValues.Add(kvp.Key, kvp.Value);
        }
    }
    
    public void WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse && attributeValue != null)
        {
            AttributeValues.Add(attributeName, new AttributeValue() { S = attributeValue });
        }
    }
    
    public void WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse)
        {
            AttributeValues.Add(attributeName,
                new AttributeValue() { BOOL = attributeValue ?? false, IsBOOLSet = attributeValue != null });
        }
    }
    
    public void WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse)
        {
            AttributeValues.Add(attributeName, new AttributeValue() { N = attributeValue.ToString() });
        }
    }
    
    public void WithValue(
        string attributeName, Dictionary<string,string> attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse)
        {
            AttributeValues.Add(attributeName, new AttributeValue() { M = attributeValue.ToDictionary(x => x.Key, x => new AttributeValue() { S = x.Value }) });
        }
    }
    
    public void WithValue(
        string attributeName, Dictionary<string,AttributeValue> attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse)
        {
            AttributeValues.Add(attributeName, new AttributeValue() { M = attributeValue });
        }
    }
    
    /// <summary>
    /// Adds a formatted value to the attribute values collection and returns the generated parameter name.
    /// This method supports standard .NET format strings and automatically converts values to appropriate AttributeValue types.
    /// </summary>
    /// <param name="value">The value to format and add.</param>
    /// <param name="format">Optional format string (e.g., "o" for DateTime, "F2" for decimals).</param>
    /// <returns>The generated parameter name that can be used in expressions.</returns>
    public string AddFormattedValue(object? value, string? format = null)
    {
        var paramName = _parameterGenerator.GenerateParameterName();
        var formattedValue = FormatValue(value, format);
        AttributeValues.Add(paramName, formattedValue);
        return paramName;
    }
    
    /// <summary>
    /// Converts and formats a value to an appropriate AttributeValue type.
    /// Supports standard .NET format strings and handles null values gracefully.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">Optional format string.</param>
    /// <returns>An AttributeValue representing the formatted value.</returns>
    /// <exception cref="FormatException">Thrown when the format string is invalid for the given value type.</exception>
    private static AttributeValue FormatValue(object? value, string? format)
    {
        if (value == null)
        {
            return new AttributeValue { NULL = true };
        }

        try
        {
            // Handle different value types with optional formatting
            return value switch
            {
                string str => new AttributeValue { S = str },
                
                DateTime dt => new AttributeValue 
                { 
                    S = string.IsNullOrEmpty(format) ? dt.ToString("o", CultureInfo.InvariantCulture) : dt.ToString(format, CultureInfo.InvariantCulture) 
                },
                
                DateTimeOffset dto => new AttributeValue 
                { 
                    S = string.IsNullOrEmpty(format) ? dto.ToString("o", CultureInfo.InvariantCulture) : dto.ToString(format, CultureInfo.InvariantCulture) 
                },
                
                bool b when string.IsNullOrEmpty(format) => new AttributeValue { BOOL = b, IsBOOLSet = true },
                bool b when !string.IsNullOrEmpty(format) => throw new FormatException($"Boolean values do not support format strings. Format '{format}' is not valid for boolean type."),
                
                // Numeric types
                byte b => new AttributeValue { N = string.IsNullOrEmpty(format) ? b.ToString(CultureInfo.InvariantCulture) : b.ToString(format, CultureInfo.InvariantCulture) },
                sbyte sb => new AttributeValue { N = string.IsNullOrEmpty(format) ? sb.ToString(CultureInfo.InvariantCulture) : sb.ToString(format, CultureInfo.InvariantCulture) },
                short s => new AttributeValue { N = string.IsNullOrEmpty(format) ? s.ToString(CultureInfo.InvariantCulture) : s.ToString(format, CultureInfo.InvariantCulture) },
                ushort us => new AttributeValue { N = string.IsNullOrEmpty(format) ? us.ToString(CultureInfo.InvariantCulture) : us.ToString(format, CultureInfo.InvariantCulture) },
                int i => new AttributeValue { N = string.IsNullOrEmpty(format) ? i.ToString(CultureInfo.InvariantCulture) : i.ToString(format, CultureInfo.InvariantCulture) },
                uint ui => new AttributeValue { N = string.IsNullOrEmpty(format) ? ui.ToString(CultureInfo.InvariantCulture) : ui.ToString(format, CultureInfo.InvariantCulture) },
                long l => new AttributeValue { N = string.IsNullOrEmpty(format) ? l.ToString(CultureInfo.InvariantCulture) : l.ToString(format, CultureInfo.InvariantCulture) },
                ulong ul => new AttributeValue { N = string.IsNullOrEmpty(format) ? ul.ToString(CultureInfo.InvariantCulture) : ul.ToString(format, CultureInfo.InvariantCulture) },
                float f => new AttributeValue { N = string.IsNullOrEmpty(format) ? f.ToString(CultureInfo.InvariantCulture) : f.ToString(format, CultureInfo.InvariantCulture) },
                double d => new AttributeValue { N = string.IsNullOrEmpty(format) ? d.ToString(CultureInfo.InvariantCulture) : d.ToString(format, CultureInfo.InvariantCulture) },
                decimal dec => new AttributeValue { N = string.IsNullOrEmpty(format) ? dec.ToString(CultureInfo.InvariantCulture) : dec.ToString(format, CultureInfo.InvariantCulture) },
                
                // Enum handling - convert to string (format strings not supported for enums)
                Enum e when string.IsNullOrEmpty(format) => new AttributeValue { S = e.ToString() },
                Enum e when !string.IsNullOrEmpty(format) => throw new FormatException($"Enum values do not support format strings. Format '{format}' is not valid for enum type {e.GetType().Name}."),
                
                // Guid handling
                Guid g => new AttributeValue { S = string.IsNullOrEmpty(format) ? g.ToString() : g.ToString(format) },
                
                // For any other type, use ToString() with optional formatting if it implements IFormattable
                IFormattable formattable when !string.IsNullOrEmpty(format) => new AttributeValue { S = formattable.ToString(format, CultureInfo.InvariantCulture) },
                
                // Fallback to ToString()
                _ when string.IsNullOrEmpty(format) => new AttributeValue { S = value.ToString() ?? string.Empty },
                _ when !string.IsNullOrEmpty(format) => throw new FormatException($"Type {value.GetType().Name} does not support format strings. Format '{format}' is not valid for this type."),
                _ => new AttributeValue { S = value.ToString() ?? string.Empty }
            };
        }
        catch (FormatException)
        {
            // Re-throw FormatExceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            // Wrap other exceptions in FormatException with more context
            var valueTypeName = value.GetType().Name;
            var formatInfo = string.IsNullOrEmpty(format) ? "no format" : $"format '{format}'";
            throw new FormatException($"Failed to format value of type {valueTypeName} with {formatInfo}: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Gets the parameter generator instance for this AttributeValueInternal.
    /// Used by extension methods to generate consistent parameter names.
    /// </summary>
    public ParameterGenerator GetParameterGenerator() => _parameterGenerator;
}