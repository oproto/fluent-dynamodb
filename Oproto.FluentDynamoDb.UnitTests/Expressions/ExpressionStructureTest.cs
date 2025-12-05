using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Expressions;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Test to understand how C# compiler generates expression trees for method calls.
/// This helps us understand if the issue is in the translator or in how we're testing.
/// </summary>
public class ExpressionStructureTest
{
    private readonly ITestOutputHelper _output;

    public ExpressionStructureTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private class TestUpdateExpressions
    {
        public UpdateExpressionProperty<int> Count { get; } = new();
    }

    private class TestUpdateModel
    {
        public int? Count { get; set; }
    }

    [Fact]
    public void InspectExpressionStructure_WhenUsingNaturalSyntax()
    {
        // This is how developers will write it
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expr = 
            x => new TestUpdateModel { Count = x.Count.Add(1) };

        _output.WriteLine("Expression: " + expr.ToString());
        _output.WriteLine("Body type: " + expr.Body.GetType().Name);

        var memberInit = (MemberInitExpression)expr.Body;
        var binding = (MemberAssignment)memberInit.Bindings[0];
        
        // Handle potential Convert wrapper around the method call
        var bindingExpression = binding.Expression;
        if (bindingExpression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            _output.WriteLine("Unwrapping Convert expression");
            bindingExpression = unary.Operand;
        }
        
        var methodCall = (MethodCallExpression)bindingExpression;

        _output.WriteLine("\nMethod: " + methodCall.Method.Name);
        _output.WriteLine("Arguments count: " + methodCall.Arguments.Count);
        
        for (int i = 0; i < methodCall.Arguments.Count; i++)
        {
            var arg = methodCall.Arguments[i];
            _output.WriteLine($"\nArg {i}:");
            _output.WriteLine($"  Type: {arg.GetType().Name}");
            _output.WriteLine($"  NodeType: {arg.NodeType}");
            _output.WriteLine($"  ToString: {arg}");
            
            if (arg is ConstantExpression constant)
            {
                _output.WriteLine($"  Value: {constant.Value}");
            }
            else if (arg is MemberExpression member)
            {
                _output.WriteLine($"  Member: {member.Member.Name}");
                _output.WriteLine($"  Expression: {member.Expression}");
            }
        }

        // Try to compile the whole expression
        try
        {
            var compiled = expr.Compile();
            _output.WriteLine("\n✓ Full expression compiles successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ Full expression compilation failed: {ex.Message}");
        }

        // Try to compile just the argument
        try
        {
            var argExpr = methodCall.Arguments[1]; // The "1" argument
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(argExpr, typeof(object)));
            var compiled = lambda.Compile();
            var result = compiled();
            _output.WriteLine($"\n✓ Argument alone compiles successfully, value: {result}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ Argument alone compilation failed: {ex.Message}");
        }
    }
}
