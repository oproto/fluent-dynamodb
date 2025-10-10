using System.Text.RegularExpressions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for builders that implement IWithUpdateExpression interface.
/// Provides fluent methods for setting update expressions with support for format strings.
/// 
/// <para><strong>Migration Guide:</strong></para>
/// <para>The existing <c>Set(string)</c> method works exactly as before. The new <c>Set(string, params object[])</c> 
/// overload provides enhanced functionality with automatic parameter generation and formatting.</para>
/// 
/// <para><strong>Format String Syntax:</strong></para>
/// <list type="bullet">
/// <item><c>{0}</c> - Simple parameter substitution</item>
/// <item><c>{0:format}</c> - Parameter with format specifier</item>
/// <item><c>{1:o}</c> - DateTime with ISO 8601 format</item>
/// <item><c>{2:F2}</c> - Decimal with 2 decimal places</item>
/// <item><c>{3:X}</c> - Integer as hexadecimal</item>
/// </list>
/// 
/// <para><strong>Supported Format Specifiers:</strong></para>
/// <list type="table">
/// <listheader><term>Format</term><description>Description</description><description>Example</description></listheader>
/// <item><term>o</term><description>ISO 8601 DateTime</description><description>2024-01-15T10:30:00.000Z</description></item>
/// <item><term>F2</term><description>Fixed-point with 2 decimals</description><description>123.45</description></item>
/// <item><term>X</term><description>Hexadecimal uppercase</description><description>FF</description></item>
/// <item><term>x</term><description>Hexadecimal lowercase</description><description>ff</description></item>
/// <item><term>D</term><description>Decimal integer</description><description>123</description></item>
/// </list>
/// 
/// <para><strong>Usage Examples:</strong></para>
/// <code>
/// // Old style (still supported)
/// builder.Set("SET #name = :name, #status = :status")
///        .WithAttribute("#name", "name")
///        .WithAttribute("#status", "status")
///        .WithValue(":name", "John Doe")
///        .WithValue(":status", "ACTIVE");
/// 
/// // New format string style
/// builder.Set("SET #name = {0}, #status = {1}", "John Doe", "ACTIVE");
/// 
/// // Mixed usage - both styles in same builder
/// builder.Set("SET #name = {0}, #customField = :customValue", "John Doe")
///        .WithValue(":customValue", "custom");
/// 
/// // DateTime formatting
/// builder.Set("SET #name = {0}, #updated = {1:o}", "John Doe", DateTime.Now);
/// 
/// // Numeric operations with formatting
/// builder.Set("ADD #count {0}, #amount {1:F2}", 1, 99.999m);
/// </code>
/// </summary>
public static class WithUpdateExpressionExtensions
{
    /// <summary>
    /// Specifies the update expression for the operation.
    /// This is the existing method that accepts a pre-formatted update expression.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="updateExpression">The update expression (e.g., "SET #name = :name" or "ADD #count :inc").</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // SET operation
    /// .Set("SET #name = :name, #status = :status")
    /// .WithAttribute("#name", "name")
    /// .WithAttribute("#status", "status")
    /// .WithValue(":name", "John Doe")
    /// .WithValue(":status", "ACTIVE")
    /// 
    /// // ADD operation
    /// .Set("ADD #count :inc, #tags :newTags")
    /// .WithAttribute("#count", "count")
    /// .WithAttribute("#tags", "tags")
    /// .WithValue(":inc", 1)
    /// .WithValue(":newTags", new[] { "tag1", "tag2" })
    /// 
    /// // REMOVE operation
    /// .Set("REMOVE #oldField, #tempData")
    /// .WithAttribute("#oldField", "oldField")
    /// .WithAttribute("#tempData", "tempData")
    /// 
    /// // Combined operations
    /// .Set("SET #name = :name ADD #count :inc REMOVE #oldField")
    /// </code>
    /// </example>
    public static T Set<T>(this IWithUpdateExpression<T> builder, string updateExpression)
    {
        return builder.SetUpdateExpression(updateExpression);
    }
    
    /// <summary>
    /// Specifies the update expression using format string syntax with automatic parameter generation.
    /// This method allows you to use {0}, {1}, {2:format} syntax instead of manual parameter naming.
    /// Values are automatically converted to appropriate AttributeValue types and parameters are generated.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithUpdateExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="format">The format string with {0}, {1}, etc. placeholders (e.g., "SET #name = {0}, #updated = {1:o}").</param>
    /// <param name="args">The values to substitute into the format string.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when format string is invalid or parameter count doesn't match.</exception>
    /// <exception cref="FormatException">Thrown when format specifiers are invalid for the given value type.</exception>
    /// <example>
    /// <code>
    /// // Simple SET operation - eliminates manual parameter naming
    /// .Set("SET #name = {0}, #status = {1}", "John Doe", "ACTIVE")
    /// // Equivalent to: .Set("SET #name = :p0, #status = :p1").WithValue(":p0", "John Doe").WithValue(":p1", "ACTIVE")
    /// 
    /// // DateTime formatting with ISO 8601
    /// .Set("SET #name = {0}, #updated = {1:o}", "John Doe", DateTime.Now)
    /// // Automatically formats DateTime as "2024-01-15T10:30:00.000Z"
    /// 
    /// // Numeric operations with formatting
    /// .Set("ADD #count {0}, #amount {1:F2}", 1, 99.999m)
    /// // Formats decimal as "99.99"
    /// 
    /// // REMOVE operations (no parameters needed)
    /// .Set("REMOVE #oldField, #tempData")
    /// 
    /// // Complex combined operations
    /// .Set("SET #name = {0}, #updated = {1:o} ADD #count {2} REMOVE #oldField", 
    ///      "John Doe", DateTime.Now, 1)
    /// 
    /// // Boolean and enum values
    /// .Set("SET #active = {0}, #status = {1}", true, OrderStatus.Pending)
    /// 
    /// // Null handling - null values are converted appropriately
    /// .Set("SET #name = {0}, #optional = {1}", "John Doe", nullableValue)
    /// 
    /// // List and set operations
    /// .Set("ADD #tags {0}, #scores {1}", new[] { "tag1", "tag2" }, new[] { 100, 200 })
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><strong>Parameter Generation:</strong> Parameters are automatically named as :p0, :p1, :p2, etc.</para>
    /// <para><strong>Type Conversion:</strong> Values are automatically converted to appropriate DynamoDB AttributeValue types.</para>
    /// <para><strong>Format Safety:</strong> All format operations are AOT-safe and don't use reflection.</para>
    /// <para><strong>Error Handling:</strong> Clear error messages are provided for invalid format strings or type mismatches.</para>
    /// <para><strong>Update Expression Types:</strong> Supports SET, ADD, REMOVE, and DELETE operations with format strings.</para>
    /// </remarks>
    public static T Set<T>(this IWithUpdateExpression<T> builder, string format, params object[] args)
    {
        var (processedExpression, _) = ProcessFormatString(format, args, builder.GetAttributeValueHelper());
        return builder.SetUpdateExpression(processedExpression);
    }
    
    /// <summary>
    /// Processes a format string by replacing {0}, {1:format} placeholders with generated parameter names
    /// and adding the corresponding values to the attribute value helper.
    /// This method reuses the same logic as WithConditionExpressionExtensions for consistency.
    /// </summary>
    /// <param name="format">The format string with placeholders.</param>
    /// <param name="args">The values to substitute.</param>
    /// <param name="attributeValueHelper">The helper to add generated parameters to.</param>
    /// <returns>A tuple containing the processed expression and the number of parameters generated.</returns>
    /// <exception cref="ArgumentException">Thrown when format string is invalid or parameter count doesn't match.</exception>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="FormatException">Thrown when format specifiers are invalid.</exception>
    private static (string processedExpression, int parameterCount) ProcessFormatString(
        string format, 
        object[] args, 
        AttributeValueInternal attributeValueHelper)
    {
        if (string.IsNullOrEmpty(format))
        {
            throw new ArgumentException("Format string cannot be null or empty.", nameof(format));
        }

        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (attributeValueHelper == null)
        {
            throw new ArgumentNullException(nameof(attributeValueHelper));
        }

        // Regular expression to match format placeholders like {0}, {1:o}, {2:F2}
        var formatPattern = new Regex(@"\{(\d+)(?::([^}]+))?\}", RegexOptions.Compiled);
        var matches = formatPattern.Matches(format);
        
        if (matches.Count == 0)
        {
            // No placeholders found, return as-is
            return (format, 0);
        }

        // Validate format string syntax - check for unmatched braces
        var openBraces = format.Count(c => c == '{');
        var closeBraces = format.Count(c => c == '}');
        if (openBraces != closeBraces)
        {
            throw new FormatException("Format string contains unmatched braces. Each '{' must have a corresponding '}'.");
        }

        // Validate that all referenced indices exist in args and are valid
        var maxIndex = -1;
        var invalidIndices = new List<string>();
        
        foreach (Match match in matches)
        {
            var indexStr = match.Groups[1].Value;
            if (int.TryParse(indexStr, out var index))
            {
                if (index < 0)
                {
                    invalidIndices.Add(indexStr);
                }
                else
                {
                    maxIndex = Math.Max(maxIndex, index);
                }
            }
            else
            {
                invalidIndices.Add(indexStr);
            }
        }

        if (invalidIndices.Count > 0)
        {
            throw new FormatException($"Format string contains invalid parameter indices: {string.Join(", ", invalidIndices)}. Parameter indices must be non-negative integers.");
        }

        if (maxIndex >= args.Length)
        {
            throw new ArgumentException($"Format string references parameter index {maxIndex} but only {args.Length} arguments were provided. Parameter indices must be less than the number of arguments.", nameof(format));
        }

        // Process each placeholder and build the result
        var result = format;
        var parameterCount = 0;
        
        // First, collect all matches and sort them by argument index to ensure consistent parameter generation
        var matchList = matches.Cast<Match>()
            .Select(m => new { 
                Match = m, 
                ArgIndex = int.Parse(m.Groups[1].Value),
                FormatSpec = m.Groups[2].Success ? m.Groups[2].Value : null 
            })
            .OrderBy(x => x.ArgIndex)
            .ToList();
        
        // Generate parameters in argument order
        var parameterMap = new Dictionary<int, string>();
        foreach (var matchInfo in matchList)
        {
            if (!parameterMap.ContainsKey(matchInfo.ArgIndex))
            {
                try
                {
                    var value = args[matchInfo.ArgIndex];
                    var parameterName = attributeValueHelper.AddFormattedValue(value, matchInfo.FormatSpec);
                    parameterMap[matchInfo.ArgIndex] = parameterName;
                }
                catch (FormatException ex)
                {
                    throw new FormatException($"Invalid format specifier '{matchInfo.FormatSpec}' for parameter at index {matchInfo.ArgIndex}. {ex.Message}", ex);
                }
                catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException || ex is FormatException))
                {
                    throw new FormatException($"Error processing parameter at index {matchInfo.ArgIndex} with format specifier '{matchInfo.FormatSpec}': {ex.Message}", ex);
                }
            }
        }
        
        // Now replace placeholders in reverse order to avoid index shifting issues
        var orderedMatches = matches.Cast<Match>().OrderByDescending(m => m.Index).ToList();
        foreach (var match in orderedMatches)
        {
            var argIndex = int.Parse(match.Groups[1].Value);
            var parameterName = parameterMap[argIndex];
            
            // Replace the placeholder with the generated parameter name
            result = result.Substring(0, match.Index) + parameterName + result.Substring(match.Index + match.Length);
            parameterCount++;
        }

        return (result, parameterCount);
    }
}