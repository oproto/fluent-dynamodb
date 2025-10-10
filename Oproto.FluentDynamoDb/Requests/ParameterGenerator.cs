namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Internal class for generating unique parameter names for DynamoDB expressions.
/// Each builder instance should have its own ParameterGenerator to ensure
/// predictable, debuggable parameter names within a single expression.
/// </summary>
public class ParameterGenerator
{
    private int _counter = 0;
    
    /// <summary>
    /// Generates a unique parameter name in the format ":p{counter}".
    /// </summary>
    /// <returns>A unique parameter name like ":p0", ":p1", etc.</returns>
    public string GenerateParameterName() => $":p{_counter++}";
    
    /// <summary>
    /// Resets the parameter counter to 0. Primarily used for testing scenarios.
    /// </summary>
    public void Reset() => _counter = 0;
}