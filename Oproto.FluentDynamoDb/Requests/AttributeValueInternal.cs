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
            var attrValue = new AttributeValue();
            if (attributeValue != null)
            {
                attrValue.BOOL = attributeValue.Value;
            }
            else
            {
                attrValue.BOOL = false;
                attrValue.IsBOOLSet = false;
            }
            AttributeValues.Add(attributeName, attrValue);
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
                
                // Numeric types - validate format strings to ensure proper error handling
                byte b => new AttributeValue { N = string.IsNullOrEmpty(format) ? b.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(b, format) },
                sbyte sb => new AttributeValue { N = string.IsNullOrEmpty(format) ? sb.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(sb, format) },
                short s => new AttributeValue { N = string.IsNullOrEmpty(format) ? s.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(s, format) },
                ushort us => new AttributeValue { N = string.IsNullOrEmpty(format) ? us.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(us, format) },
                int i => new AttributeValue { N = string.IsNullOrEmpty(format) ? i.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(i, format) },
                uint ui => new AttributeValue { N = string.IsNullOrEmpty(format) ? ui.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(ui, format) },
                long l => new AttributeValue { N = string.IsNullOrEmpty(format) ? l.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(l, format) },
                ulong ul => new AttributeValue { N = string.IsNullOrEmpty(format) ? ul.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(ul, format) },
                float f => new AttributeValue { N = string.IsNullOrEmpty(format) ? f.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(f, format) },
                double d => new AttributeValue { N = string.IsNullOrEmpty(format) ? d.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(d, format) },
                decimal dec => new AttributeValue { N = string.IsNullOrEmpty(format) ? dec.ToString(CultureInfo.InvariantCulture) : FormatNumericValue(dec, format) },
                
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
    /// Formats a numeric value with the specified format string, ensuring proper error handling for invalid formats.
    /// </summary>
    /// <param name="value">The numeric value to format.</param>
    /// <param name="format">The format string to apply.</param>
    /// <returns>The formatted string representation of the value.</returns>
    /// <exception cref="FormatException">Thrown when the format string is invalid for the numeric type.</exception>
    private static string FormatNumericValue(IFormattable value, string format)
    {
        try
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            // Re-throw FormatExceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            // Convert other exceptions to FormatException for consistency
            throw new FormatException($"Invalid format specifier '{format}' for parameter of type {value.GetType().Name}.", ex);
        }
    }

    /// <summary>
    /// Gets the parameter generator instance for this AttributeValueInternal.
    /// Used by extension methods to generate consistent parameter names.
    /// </summary>
    public ParameterGenerator GetParameterGenerator() => _parameterGenerator;
}