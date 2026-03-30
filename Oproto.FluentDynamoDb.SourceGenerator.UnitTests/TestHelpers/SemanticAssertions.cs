using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

/// <summary>
/// Provides semantic assertion methods for verifying code structure using syntax tree analysis.
/// These assertions are more maintainable than string matching as they verify code structure
/// rather than exact formatting.
/// </summary>
public static class SemanticAssertions
{
    /// <summary>
    /// Asserts that the source code contains a method with the specified name.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze</param>
    /// <param name="methodName">The name of the method to find</param>
    /// <param name="because">Optional explanation of why this assertion is important</param>
    /// <exception cref="SemanticAssertionException">Thrown when the method is not found</exception>
    public static void ShouldContainMethod(
        this string sourceCode,
        string methodName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .ToList();

        var hasMethod = methods.Any(m => m.Identifier.Text == methodName);

        if (!hasMethod)
        {
            var availableMethods = methods
                .Select(m => m.Identifier.Text)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            var errorMessage = new StringBuilder();
            errorMessage.AppendLine($"Expected source code to contain method '{methodName}'");
            
            if (!string.IsNullOrWhiteSpace(because))
            {
                errorMessage.AppendLine($"Because: {because}");
            }
            
            errorMessage.AppendLine();
            
            if (availableMethods.Any())
            {
                errorMessage.AppendLine("Available methods:");
                foreach (var method in availableMethods)
                {
                    errorMessage.AppendLine($"  - {method}");
                }
            }
            else
            {
                errorMessage.AppendLine("No methods found in the source code.");
            }

            errorMessage.AppendLine();
            errorMessage.AppendLine("Source code context:");
            errorMessage.AppendLine(GetSourceContext(sourceCode, 10));

            throw new SemanticAssertionException(errorMessage.ToString());
        }
    }

    /// <summary>
    /// Asserts that the source code contains an assignment to a variable or property with the specified name.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze</param>
    /// <param name="targetName">The name of the assignment target to find</param>
    /// <param name="because">Optional explanation of why this assertion is important</param>
    /// <exception cref="SemanticAssertionException">Thrown when the assignment is not found</exception>
    public static void ShouldContainAssignment(
        this string sourceCode,
        string targetName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        var assignments = root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .ToList();

        var hasAssignment = assignments.Any(a => 
            a.Left.ToString().Contains(targetName));

        if (!hasAssignment)
        {
            var availableTargets = assignments
                .Select(a => a.Left.ToString())
                .Distinct()
                .OrderBy(target => target)
                .ToList();

            var errorMessage = new StringBuilder();
            errorMessage.AppendLine($"Expected source code to contain assignment to '{targetName}'");
            
            if (!string.IsNullOrWhiteSpace(because))
            {
                errorMessage.AppendLine($"Because: {because}");
            }
            
            errorMessage.AppendLine();
            
            if (availableTargets.Any())
            {
                errorMessage.AppendLine("Available assignment targets:");
                foreach (var target in availableTargets.Take(20))
                {
                    errorMessage.AppendLine($"  - {target}");
                }
                
                if (availableTargets.Count > 20)
                {
                    errorMessage.AppendLine($"  ... and {availableTargets.Count - 20} more");
                }
            }
            else
            {
                errorMessage.AppendLine("No assignments found in the source code.");
            }

            errorMessage.AppendLine();
            errorMessage.AppendLine("Source code context:");
            errorMessage.AppendLine(GetSourceContext(sourceCode, 10));

            throw new SemanticAssertionException(errorMessage.ToString());
        }
    }

    /// <summary>
    /// Asserts that the source code uses a LINQ method (e.g., Select, Where, ToList).
    /// </summary>
    /// <param name="sourceCode">The source code to analyze</param>
    /// <param name="methodName">The name of the LINQ method to find</param>
    /// <param name="because">Optional explanation of why this assertion is important</param>
    /// <exception cref="SemanticAssertionException">Thrown when the LINQ method is not found</exception>
    public static void ShouldUseLinqMethod(
        this string sourceCode,
        string methodName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToList();

        var hasLinqCall = invocations.Any(inv =>
        {
            var expression = inv.Expression.ToString();
            return expression.EndsWith($".{methodName}") || 
                   expression.EndsWith($".{methodName}()");
        });

        if (!hasLinqCall)
        {
            var availableMethods = invocations
                .Select(inv => inv.Expression.ToString())
                .Where(expr => expr.Contains('.'))
                .Select(expr =>
                {
                    var lastDot = expr.LastIndexOf('.');
                    return lastDot >= 0 ? expr.Substring(lastDot + 1) : expr;
                })
                .Distinct()
                .OrderBy(method => method)
                .ToList();

            var errorMessage = new StringBuilder();
            errorMessage.AppendLine($"Expected source code to use LINQ method '{methodName}'");
            
            if (!string.IsNullOrWhiteSpace(because))
            {
                errorMessage.AppendLine($"Because: {because}");
            }
            
            errorMessage.AppendLine();
            
            if (availableMethods.Any())
            {
                errorMessage.AppendLine("Available method calls:");
                foreach (var method in availableMethods.Take(20))
                {
                    errorMessage.AppendLine($"  - {method}");
                }
                
                if (availableMethods.Count > 20)
                {
                    errorMessage.AppendLine($"  ... and {availableMethods.Count - 20} more");
                }
            }
            else
            {
                errorMessage.AppendLine("No method invocations found in the source code.");
            }

            errorMessage.AppendLine();
            errorMessage.AppendLine("Source code context:");
            errorMessage.AppendLine(GetSourceContext(sourceCode, 10));

            throw new SemanticAssertionException(errorMessage.ToString());
        }
    }

    /// <summary>
    /// Asserts that the source code references a type with the specified name.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze</param>
    /// <param name="typeName">The name of the type to find</param>
    /// <param name="because">Optional explanation of why this assertion is important</param>
    /// <exception cref="SemanticAssertionException">Thrown when the type reference is not found</exception>
    public static void ShouldReferenceType(
        this string sourceCode,
        string typeName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        var identifiers = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .ToList();

        var hasTypeReference = identifiers.Any(id => id.Identifier.Text == typeName);

        if (!hasTypeReference)
        {
            var availableTypes = identifiers
                .Select(id => id.Identifier.Text)
                .Distinct()
                .OrderBy(type => type)
                .ToList();

            var errorMessage = new StringBuilder();
            errorMessage.AppendLine($"Expected source code to reference type '{typeName}'");
            
            if (!string.IsNullOrWhiteSpace(because))
            {
                errorMessage.AppendLine($"Because: {because}");
            }
            
            errorMessage.AppendLine();
            
            if (availableTypes.Any())
            {
                errorMessage.AppendLine("Available type references:");
                foreach (var type in availableTypes.Take(30))
                {
                    errorMessage.AppendLine($"  - {type}");
                }
                
                if (availableTypes.Count > 30)
                {
                    errorMessage.AppendLine($"  ... and {availableTypes.Count - 30} more");
                }
            }
            else
            {
                errorMessage.AppendLine("No type references found in the source code.");
            }

            errorMessage.AppendLine();
            errorMessage.AppendLine("Source code context:");
            errorMessage.AppendLine(GetSourceContext(sourceCode, 10));

            throw new SemanticAssertionException(errorMessage.ToString());
        }
    }

    /// <summary>
    /// Asserts that the source code contains a class with the specified name.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze</param>
    /// <param name="className">The name of the class to find</param>
    /// <param name="because">Optional explanation of why this assertion is important</param>
    /// <exception cref="SemanticAssertionException">Thrown when the class is not found</exception>
    public static void ShouldContainClass(
        this string sourceCode,
        string className,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        // Look for both class and record declarations
        var classes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Select(c => c.Identifier.Text)
            .ToList();

        var records = root.DescendantNodes()
            .OfType<RecordDeclarationSyntax>()
            .Select(r => r.Identifier.Text)
            .ToList();

        var allTypes = classes.Concat(records).ToList();
        var hasType = allTypes.Any(name => name == className);

        if (!hasType)
        {
            var availableTypes = allTypes
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            var errorMessage = new StringBuilder();
            errorMessage.AppendLine($"Expected source code to contain class '{className}'");
            
            if (!string.IsNullOrWhiteSpace(because))
            {
                errorMessage.AppendLine($"Because: {because}");
            }
            
            errorMessage.AppendLine();
            
            if (availableTypes.Any())
            {
                errorMessage.AppendLine("Available classes:");
                foreach (var cls in availableTypes)
                {
                    errorMessage.AppendLine($"  - {cls}");
                }
            }
            else
            {
                errorMessage.AppendLine("No classes found in the source code.");
            }

            errorMessage.AppendLine();
            errorMessage.AppendLine("Source code context:");
            errorMessage.AppendLine(GetSourceContext(sourceCode, 10));

            throw new SemanticAssertionException(errorMessage.ToString());
        }
    }

    /// <summary>
    /// Asserts that the source code contains a constant field with the specified name.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze</param>
    /// <param name="constantName">The name of the constant to find</param>
    /// <param name="because">Optional explanation of why this assertion is important</param>
    /// <exception cref="SemanticAssertionException">Thrown when the constant is not found</exception>
    public static void ShouldContainConstant(
        this string sourceCode,
        string constantName,
        string because = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        var fields = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .ToList();

        var constants = fields
            .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            .SelectMany(f => f.Declaration.Variables)
            .ToList();

        var hasConstant = constants.Any(c => c.Identifier.Text == constantName);

        if (!hasConstant)
        {
            var availableConstants = constants
                .Select(c => c.Identifier.Text)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            var errorMessage = new StringBuilder();
            errorMessage.AppendLine($"Expected source code to contain constant '{constantName}'");
            
            if (!string.IsNullOrWhiteSpace(because))
            {
                errorMessage.AppendLine($"Because: {because}");
            }
            
            errorMessage.AppendLine();
            
            if (availableConstants.Any())
            {
                errorMessage.AppendLine("Available constants:");
                foreach (var constant in availableConstants)
                {
                    errorMessage.AppendLine($"  - {constant}");
                }
            }
            else
            {
                errorMessage.AppendLine("No constants found in the source code.");
            }

            errorMessage.AppendLine();
            errorMessage.AppendLine("Source code context:");
            errorMessage.AppendLine(GetSourceContext(sourceCode, 10));

            throw new SemanticAssertionException(errorMessage.ToString());
        }
    }

    /// <summary>
    /// Gets a truncated context of the source code for error messages.
    /// </summary>
    private static string GetSourceContext(string sourceCode, int maxLines)
    {
        var lines = sourceCode.Split('\n');
        var contextLines = lines.Take(maxLines).ToList();
        
        var context = new StringBuilder();
        for (int i = 0; i < contextLines.Count; i++)
        {
            context.AppendLine($"  {i + 1,4}: {contextLines[i].TrimEnd()}");
        }
        
        if (lines.Length > maxLines)
        {
            context.AppendLine($"  ... ({lines.Length - maxLines} more lines)");
        }
        
        return context.ToString();
    }
}

/// <summary>
/// Exception thrown when a semantic assertion fails.
/// </summary>
public class SemanticAssertionException : Exception
{
    public SemanticAssertionException(string message) : base(message)
    {
    }
}
