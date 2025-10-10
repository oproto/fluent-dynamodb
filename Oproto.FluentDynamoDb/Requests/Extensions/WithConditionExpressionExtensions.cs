using System.Text.RegularExpressions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for builders that implement IWithConditionExpression interface.
/// Provides fluent methods for setting condition expressions with support for format strings.
/// 
/// <para><strong>Migration Guide:</strong></para>
/// <para>The existing <c>Where(string)</c> method works exactly as before. The new <c>Where(string, params object[])</c> 
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
/// builder.Where("pk = :pk AND created > :date")
///        .WithValue(":pk", "USER#123")
///        .WithValue(":date", "2024-01-01T00:00:00.000Z");
/// 
/// // New format string style
/// builder.Where("pk = {0} AND created > {1:o}", "USER#123", DateTime.Now);
/// 
/// // Mixed usage - both styles in same builder
/// builder.Where("pk = {0} AND sk = :customSk", "USER#123")
///        .WithValue(":customSk", "PROFILE");
/// </code>
/// </summary>
public static class WithConditionExpressionExtensions
{
    /// <summary>
    /// Specifies the condition expression for the operation.
    /// This is the existing method that accepts a pre-formatted condition expression.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithConditionExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="conditionExpression">The condition expression (e.g., "pk = :pk" or "pk = :pk AND begins_with(sk, :prefix)").</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple condition
    /// .Where("pk = :pk")
    /// .WithValue(":pk", "USER#123")
    /// 
    /// // Complex condition with multiple parameters
    /// .Where("pk = :pk AND begins_with(sk, :prefix)")
    /// .WithValue(":pk", "USER#123")
    /// .WithValue(":prefix", "ORDER#")
    /// </code>
    /// </example>
    public static T Where<T>(this IWithConditionExpression<T> builder, string conditionExpression)
    {
        return builder.SetConditionExpression(conditionExpression);
    }
    
    /// <summary>
    /// Specifies the condition expression using format string syntax with automatic parameter generation.
    /// This method allows you to use {0}, {1}, {2:format} syntax instead of manual parameter naming.
    /// Values are automatically converted to appropriate AttributeValue types and parameters are generated.
    /// </summary>
    /// <typeparam name="T">The type of the builder implementing IWithConditionExpression.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="format">The format string with {0}, {1}, etc. placeholders (e.g., "pk = {0} AND created > {1:o}").</param>
    /// <param name="args">The values to substitute into the format string.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when format string is invalid or parameter count doesn't match.</exception>
    /// <exception cref="FormatException">Thrown when format specifiers are invalid for the given value type.</exception>
    /// <example>
    /// <code>
    /// // Simple format string usage - eliminates manual parameter naming
    /// .Where("pk = {0}", "USER#123")
    /// // Equivalent to: .Where("pk = :p0").WithValue(":p0", "USER#123")
    /// 
    /// // DateTime formatting with ISO 8601
    /// .Where("pk = {0} AND created > {1:o}", "USER#123", DateTime.Now)
    /// // Automatically formats DateTime as "2024-01-15T10:30:00.000Z"
    /// 
    /// // Numeric formatting
    /// .Where("pk = {0} AND amount > {1:F2}", "USER#123", 99.999m)
    /// // Formats decimal as "99.99"
    /// 
    /// // Enum handling (automatic string conversion)
    /// .Where("pk = {0} AND #status = {1}", "USER#123", OrderStatus.Pending)
    /// // Converts enum to string: "Pending"
    /// 
    /// // Complex condition with mixed types
    /// .Where("pk = {0} AND begins_with(sk, {1}) AND created BETWEEN {2:o} AND {3:o}", 
    ///        "USER#123", "ORDER#", startDate, endDate)
    /// 
    /// // Boolean values
    /// .Where("pk = {0} AND active = {1}", "USER#123", true)
    /// 
    /// // Null handling - null values are converted appropriately
    /// .Where("pk = {0} AND optional_field = {1}", "USER#123", nullableValue)
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><strong>Parameter Generation:</strong> Parameters are automatically named as :p0, :p1, :p2, etc.</para>
    /// <para><strong>Type Conversion:</strong> Values are automatically converted to appropriate DynamoDB AttributeValue types.</para>
    /// <para><strong>Format Safety:</strong> All format operations are AOT-safe and don't use reflection.</para>
    /// <para><strong>Error Handling:</strong> Clear error messages are provided for invalid format strings or type mismatches.</para>
    /// </remarks>
    public static T Where<T>(this IWithConditionExpression<T> builder, string format, params object[] args)
    {
        var (processedExpression, _) = ProcessFormatString(format, args, builder.GetAttributeValueHelper());
        return builder.SetConditionExpression(processedExpression);
    }
    
    /// <summary>
    /// Processes a format string by replacing {0}, {1:format} placeholders with generated parameter names
    /// and adding the corresponding values to the attribute value helper.
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
        
        // Process matches in reverse order to avoid index shifting issues
        var matchList = matches.Cast<Match>().OrderByDescending(m => m.Index).ToList();
        
        foreach (var match in matchList)
        {
            var indexStr = match.Groups[1].Value;
            var formatSpec = match.Groups[2].Success ? match.Groups[2].Value : null;
            
            if (int.TryParse(indexStr, out var argIndex) && argIndex < args.Length)
            {
                try
                {
                    var value = args[argIndex];
                    var parameterName = attributeValueHelper.AddFormattedValue(value, formatSpec);
                    
                    // Replace the placeholder with the generated parameter name
                    result = result.Substring(0, match.Index) + parameterName + result.Substring(match.Index + match.Length);
                    parameterCount++;
                }
                catch (FormatException ex)
                {
                    throw new FormatException($"Invalid format specifier '{formatSpec}' for parameter at index {argIndex}. {ex.Message}", ex);
                }
                catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException || ex is FormatException))
                {
                    throw new FormatException($"Error processing parameter at index {argIndex} with format specifier '{formatSpec}': {ex.Message}", ex);
                }
            }
        }

        return (result, parameterCount);
    }
}