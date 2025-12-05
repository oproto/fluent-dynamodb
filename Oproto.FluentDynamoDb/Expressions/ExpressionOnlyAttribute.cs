namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Marks methods that are only valid within expression trees.
/// These methods should never be called directly at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Methods marked with this attribute are designed to be analyzed by the expression translator
/// and converted into DynamoDB expression syntax (query, filter, or update expressions).
/// They throw exceptions if called at runtime.
/// </para>
/// 
/// <para>
/// This attribute serves as documentation for developers and can be used by analyzers to warn
/// about direct invocation of expression-only methods.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class ExpressionOnlyAttribute : Attribute
{
}
