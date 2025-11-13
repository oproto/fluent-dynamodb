using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Microsoft.CodeAnalysis;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates entity-specific update builder classes that inherit from UpdateItemRequestBuilder.
/// These builders provide simplified Set() methods that don't require explicit generic type parameters.
/// </summary>
internal static class EntitySpecificUpdateBuilderGenerator
{
    /// <summary>
    /// Generates an entity-specific update builder class for the given entity.
    /// </summary>
    /// <param name="entity">The entity model to generate an update builder for.</param>
    /// <param name="extensionMethods">Optional discovered extension methods for wrapper generation.</param>
    /// <returns>The generated update builder class code.</returns>
    public static string GenerateUpdateBuilder(EntityModel entity, Dictionary<string, List<ExtensionMethodInfo>>? extensionMethods = null)
    {
        var sb = new StringBuilder();
        
        // File header
        FileHeaderGenerator.GenerateFileHeader(sb);
        
        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using Amazon.DynamoDBv2;");
        sb.AppendLine("using Amazon.DynamoDBv2.Model;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Logging;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests.Extensions;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {entity.Namespace};");
        sb.AppendLine();
        
        // Class declaration
        var builderClassName = $"{entity.ClassName}UpdateBuilder";
        var updateExpressionsClassName = $"{entity.ClassName}UpdateExpressions";
        var updateModelClassName = $"{entity.ClassName}UpdateModel";
        
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Entity-specific update builder for {entity.ClassName}.");
        sb.AppendLine($"/// Provides simplified Set() methods that don't require explicit generic type parameters.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public class {builderClassName} : UpdateItemRequestBuilder<{entity.ClassName}>");
        sb.AppendLine("{");
        
        // Constructor
        GenerateConstructor(sb, builderClassName);
        
        // Simplified Set() method
        GenerateSimplifiedSetMethod(sb, builderClassName, entity.ClassName, updateExpressionsClassName, updateModelClassName);
        
        // Generate extension method wrappers if available
        // Wrappers are generated after constructor and Set() method to maintain consistent code organization
        if (extensionMethods != null && extensionMethods.Count > 0)
        {
            GenerateExtensionMethodWrappers(sb, builderClassName, entity.ClassName, updateExpressionsClassName, extensionMethods);
        }
        
        // Covariant return type overrides for all fluent methods
        GenerateCovariantReturnTypeOverrides(sb, builderClassName);
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates the constructor for the entity-specific update builder.
    /// </summary>
    private static void GenerateConstructor(StringBuilder sb, string builderClassName)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {builderClassName}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client to use for executing the request.</param>");
        sb.AppendLine($"    /// <param name=\"logger\">Optional logger for operation diagnostics.</param>");
        sb.AppendLine($"    internal {builderClassName}(IAmazonDynamoDB client, IDynamoDbLogger? logger = null)");
        sb.AppendLine($"        : base(client, logger)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates the simplified Set() method that only requires TUpdateModel generic parameter.
    /// </summary>
    private static void GenerateSimplifiedSetMethod(StringBuilder sb, string builderClassName, string entityClassName, string updateExpressionsClassName, string updateModelClassName)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Specifies update operations using a type-safe C# lambda expression.");
        sb.AppendLine($"    /// This simplified method only requires the TUpdateModel generic parameter,");
        sb.AppendLine($"    /// as TEntity and TUpdateExpressions are inferred from the builder type.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <typeparam name=\"TUpdateModel\">The update model type (typically {updateModelClassName}).</typeparam>");
        sb.AppendLine($"    /// <param name=\"expression\">Lambda expression returning an UpdateModel with property assignments.</param>");
        sb.AppendLine($"    /// <param name=\"metadata\">Optional entity metadata. If not provided, attempts to resolve from entity type.</param>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Simple SET operations - no need to specify TEntity and TUpdateExpressions");
        sb.AppendLine($"    /// builder.Set(x => new {updateModelClassName}");
        sb.AppendLine($"    /// {{");
        sb.AppendLine($"    ///     Name = \"John\",");
        sb.AppendLine($"    ///     Status = \"Active\"");
        sb.AppendLine($"    /// }});");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // Atomic ADD operation");
        sb.AppendLine($"    /// builder.Set(x => new {updateModelClassName}");
        sb.AppendLine($"    /// {{");
        sb.AppendLine($"    ///     LoginCount = x.LoginCount.Add(1)");
        sb.AppendLine($"    /// }});");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public {builderClassName} Set<TUpdateModel>(");
        sb.AppendLine($"        Expression<Func<{updateExpressionsClassName}, TUpdateModel>> expression,");
        sb.AppendLine($"        EntityMetadata? metadata = null)");
        sb.AppendLine($"        where TUpdateModel : new()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        // Call the base UpdateItemRequestBuilder extension method which handles the translation");
        sb.AppendLine($"        WithUpdateExpressionExtensions.Set<{entityClassName}, {updateExpressionsClassName}, TUpdateModel>(");
        sb.AppendLine($"            this, expression, metadata);");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates wrapper methods for discovered extension methods.
    /// </summary>
    private static void GenerateExtensionMethodWrappers(
        StringBuilder sb,
        string builderClassName,
        string entityClassName,
        string updateExpressionsClassName,
        Dictionary<string, List<ExtensionMethodInfo>> extensionMethods)
    {
        sb.AppendLine($"    // ===== Extension Method Wrappers =====");
        sb.AppendLine();

        var generatedMethodNames = new HashSet<string>();

        foreach (var interfaceMethods in extensionMethods.Values)
        {
            foreach (var methodInfo in interfaceMethods)
            {
                // Validate wrapper before generation
                if (!ValidateWrapperGeneration(methodInfo, builderClassName, generatedMethodNames))
                {
                    continue;
                }

                if (!methodInfo.RequiresSpecialization)
                {
                    // Generate simple wrapper
                    GenerateSimpleWrapper(sb, builderClassName, entityClassName, methodInfo);
                }
                else
                {
                    // Generate specialized wrapper
                    GenerateSpecializedWrapper(sb, builderClassName, entityClassName, updateExpressionsClassName, methodInfo);
                }

                // Track generated method name to detect conflicts
                generatedMethodNames.Add(methodInfo.MethodName);
            }
        }
    }

    /// <summary>
    /// Validates that a wrapper can be safely generated for the given extension method.
    /// </summary>
    /// <param name="methodInfo">The extension method information.</param>
    /// <param name="builderClassName">The name of the builder class being generated.</param>
    /// <param name="generatedMethodNames">Set of already generated method names to detect conflicts.</param>
    /// <returns>True if the wrapper can be generated, false otherwise.</returns>
    private static bool ValidateWrapperGeneration(
        ExtensionMethodInfo methodInfo,
        string builderClassName,
        HashSet<string> generatedMethodNames)
    {
        // Check if method symbol is available
        if (methodInfo.MethodSymbol == null)
        {
            // Cannot generate wrapper without method symbol
            return false;
        }

        // Validate that the method is actually an extension method
        if (!methodInfo.MethodSymbol.IsExtensionMethod)
        {
            return false;
        }

        // Check for naming conflicts with base class methods and manually generated methods
        // Methods like ForTable, ReturnAllNewValues, etc. are already defined in base class
        // and have covariant return type overrides, so we shouldn't generate wrappers for them
        // Set is manually generated as a simplified method, so we skip the extension method wrapper
        var baseClassMethods = new HashSet<string>
        {
            "ForTable",
            "ReturnAllNewValues",
            "ReturnAllOldValues",
            "ReturnUpdatedNewValues",
            "ReturnUpdatedOldValues",
            "ReturnNone",
            "ReturnTotalConsumedCapacity",
            "ReturnItemCollectionMetrics",
            "ReturnOldValuesOnConditionCheckFailure",
            "Set" // Skip Set extension method wrapper - we have a manually generated simplified version
        };

        if (baseClassMethods.Contains(methodInfo.MethodName))
        {
            // Skip methods that are already overridden in the base class section or manually generated
            return false;
        }

        // Validate generic parameter substitution for specialized wrappers
        if (methodInfo.RequiresSpecialization)
        {
            var pattern = DetermineSpecializationPattern(methodInfo.MethodSymbol);
            if (pattern == SpecializationPattern.None)
            {
                // Method marked as requiring specialization but no pattern detected
                // This is a validation warning but we'll still generate a simple wrapper
            }
            else
            {
                // Validate that we can perform the specialization
                if (!ValidateSpecializationPattern(methodInfo.MethodSymbol, pattern))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that a specialization pattern can be correctly applied to a method.
    /// </summary>
    /// <param name="method">The method symbol to validate.</param>
    /// <param name="pattern">The specialization pattern to apply.</param>
    /// <returns>True if the pattern can be applied, false otherwise.</returns>
    private static bool ValidateSpecializationPattern(IMethodSymbol method, SpecializationPattern pattern)
    {
        switch (pattern)
        {
            case SpecializationPattern.WhereExpression:
                // Validate that method has TEntity type parameter
                return method.TypeParameters.Any(tp => tp.Name == "TEntity");

            case SpecializationPattern.SetExpression:
                // Validate that method has TEntity and TUpdateExpressions type parameters
                return method.TypeParameters.Any(tp => tp.Name == "TEntity") &&
                       method.TypeParameters.Any(tp => tp.Name == "TUpdateExpressions");

            case SpecializationPattern.None:
            default:
                return true;
        }
    }

    /// <summary>
    /// Generates a simple wrapper that only changes the return type.
    /// </summary>
    private static void GenerateSimpleWrapper(StringBuilder sb, string builderClassName, string entityClassName, ExtensionMethodInfo methodInfo)
    {
        if (methodInfo.MethodSymbol == null)
            return;

        var method = methodInfo.MethodSymbol;
        
        // Generate XML documentation
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// {GetMethodSummary(method)}");
        sb.AppendLine($"    /// </summary>");
        
        // Generate parameter documentation
        foreach (var param in method.Parameters.Skip(1)) // Skip 'this' parameter
        {
            sb.AppendLine($"    /// <param name=\"{param.Name}\">{GetParameterDescription(param)}</param>");
        }
        
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        
        // Generate method signature
        sb.Append($"    public {builderClassName} {method.Name}");
        
        // Add generic type parameters if method is generic (excluding T which is the builder type)
        if (method.IsGenericMethod)
        {
            var typeParams = method.TypeParameters.Where(tp => tp.Name != "T").ToList();
            if (typeParams.Count > 0)
            {
                sb.Append("<");
                sb.Append(string.Join(", ", typeParams.Select(tp => tp.Name)));
                sb.Append(">");
            }
        }
        
        sb.Append("(");
        
        // Add parameters (skip 'this' parameter)
        var parameters = method.Parameters.Skip(1).ToList();
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            if (i > 0) sb.Append(", ");
            
            // Add parameter modifiers
            if (param.RefKind == RefKind.Ref)
                sb.Append("ref ");
            else if (param.RefKind == RefKind.Out)
                sb.Append("out ");
            else if (param.RefKind == RefKind.In)
                sb.Append("in ");
            
            sb.Append(param.Type.ToDisplayString());
            sb.Append(" ");
            sb.Append(param.Name);
            
            // Add default value if present
            if (param.HasExplicitDefaultValue)
            {
                sb.Append(" = ");
                sb.Append(FormatDefaultValue(param.ExplicitDefaultValue));
            }
        }
        
        sb.AppendLine(")");
        
        // Add generic constraints
        if (method.IsGenericMethod)
        {
            foreach (var typeParam in method.TypeParameters)
            {
                var constraints = GetTypeParameterConstraints(typeParam);
                if (!string.IsNullOrEmpty(constraints))
                {
                    sb.AppendLine($"        where {typeParam.Name} : {constraints}");
                }
            }
        }
        
        sb.AppendLine($"    {{");
        
        // Generate method body - delegate to extension method
        var extensionClassName = method.ContainingType.Name;
        sb.Append($"        {extensionClassName}.{method.Name}");
        
        // Add generic type arguments if method is generic
        // For simple wrappers, we need to replace T with the base UpdateItemRequestBuilder<TEntity> type
        if (method.IsGenericMethod)
        {
            sb.Append("<");
            var typeArgs = new List<string>();
            foreach (var typeParam in method.TypeParameters)
            {
                if (typeParam.Name == "T")
                {
                    // T is the builder type - use the base UpdateItemRequestBuilder<TEntity> type
                    typeArgs.Add($"UpdateItemRequestBuilder<{entityClassName}>");
                }
                else
                {
                    // Keep other type parameters as-is
                    typeArgs.Add(typeParam.Name);
                }
            }
            sb.Append(string.Join(", ", typeArgs));
            sb.Append(">");
        }
        
        sb.Append("(this");
        
        // Add parameter arguments
        foreach (var param in parameters)
        {
            sb.Append(", ");
            
            // Add ref/out/in modifiers
            if (param.RefKind == RefKind.Ref)
                sb.Append("ref ");
            else if (param.RefKind == RefKind.Out)
                sb.Append("out ");
            else if (param.RefKind == RefKind.In)
                sb.Append("in ");
            
            sb.Append(param.Name);
        }
        
        sb.AppendLine(");");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates a specialized wrapper that fixes generic type parameters.
    /// </summary>
    private static void GenerateSpecializedWrapper(
        StringBuilder sb,
        string builderClassName,
        string entityClassName,
        string updateExpressionsClassName,
        ExtensionMethodInfo methodInfo)
    {
        if (methodInfo.MethodSymbol == null)
            return;

        var method = methodInfo.MethodSymbol;
        
        // Determine specialization pattern
        var specializationPattern = DetermineSpecializationPattern(method);
        
        if (specializationPattern == SpecializationPattern.None)
        {
            // Fall back to simple wrapper
            GenerateSimpleWrapper(sb, builderClassName, entityClassName, methodInfo);
            return;
        }
        
        // Generate XML documentation
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// {GetMethodSummary(method)}");
        if (!string.IsNullOrEmpty(methodInfo.SpecializationNotes))
        {
            sb.AppendLine($"    /// {methodInfo.SpecializationNotes}");
        }
        sb.AppendLine($"    /// </summary>");
        
        // Generate parameter documentation
        foreach (var param in method.Parameters.Skip(1)) // Skip 'this' parameter
        {
            sb.AppendLine($"    /// <param name=\"{param.Name}\">{GetParameterDescription(param)}</param>");
        }
        
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        
        // Generate method signature with specialized generic parameters
        sb.Append($"    public {builderClassName} {method.Name}");
        
        // Add generic type parameters (excluding T and specialized ones)
        // T is always removed because the wrapper returns the entity-specific builder type
        var remainingTypeParams = GetRemainingTypeParameters(method, specializationPattern, alwaysRemoveT: true);
        if (remainingTypeParams.Count > 0)
        {
            sb.Append("<");
            sb.Append(string.Join(", ", remainingTypeParams));
            sb.Append(">");
        }
        
        sb.Append("(");
        
        // Add parameters with specialized types
        var parameters = method.Parameters.Skip(1).ToList();
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            if (i > 0) sb.Append(", ");
            
            // Add parameter modifiers
            if (param.RefKind == RefKind.Ref)
                sb.Append("ref ");
            else if (param.RefKind == RefKind.Out)
                sb.Append("out ");
            else if (param.RefKind == RefKind.In)
                sb.Append("in ");
            
            // Specialize parameter type if needed
            var paramType = SpecializeParameterType(param.Type, entityClassName, updateExpressionsClassName, specializationPattern);
            sb.Append(paramType);
            sb.Append(" ");
            sb.Append(param.Name);
            
            // Add default value if present
            if (param.HasExplicitDefaultValue)
            {
                sb.Append(" = ");
                sb.Append(FormatDefaultValue(param.ExplicitDefaultValue));
            }
        }
        
        sb.AppendLine(")");
        
        // Add generic constraints for remaining type parameters
        if (remainingTypeParams.Count > 0)
        {
            foreach (var typeParam in method.TypeParameters)
            {
                if (remainingTypeParams.Contains(typeParam.Name))
                {
                    var constraints = GetTypeParameterConstraints(typeParam);
                    if (!string.IsNullOrEmpty(constraints))
                    {
                        sb.AppendLine($"        where {typeParam.Name} : {constraints}");
                    }
                }
            }
        }
        
        sb.AppendLine($"    {{");
        
        // Generate method body - delegate to extension method with specialized type arguments
        var extensionClassName = method.ContainingType.Name;
        sb.Append($"        {extensionClassName}.{method.Name}");
        
        // Add generic type arguments with specialization
        if (method.IsGenericMethod)
        {
            sb.Append("<");
            var typeArgs = GetSpecializedTypeArguments(method, entityClassName, updateExpressionsClassName, specializationPattern, remainingTypeParams, builderClassName);
            sb.Append(string.Join(", ", typeArgs));
            sb.Append(">");
        }
        
        sb.Append("(this");
        
        // Add parameter arguments
        foreach (var param in parameters)
        {
            sb.Append(", ");
            
            // Add ref/out/in modifiers
            if (param.RefKind == RefKind.Ref)
                sb.Append("ref ");
            else if (param.RefKind == RefKind.Out)
                sb.Append("out ");
            else if (param.RefKind == RefKind.In)
                sb.Append("in ");
            
            sb.Append(param.Name);
        }
        
        sb.AppendLine(");");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }

    /// <summary>
    /// Determines the specialization pattern for a method based on its signature.
    /// </summary>
    private static SpecializationPattern DetermineSpecializationPattern(IMethodSymbol method)
    {
        // Check for Expression<Func<TEntity, bool>> pattern (Where methods)
        foreach (var param in method.Parameters.Skip(1))
        {
            if (IsExpressionFuncEntityBool(param.Type))
            {
                return SpecializationPattern.WhereExpression;
            }
        }
        
        // Check for Expression<Func<TUpdateExpressions, TUpdateModel>> pattern (Set methods)
        foreach (var param in method.Parameters.Skip(1))
        {
            if (IsExpressionFuncUpdateExpressionsModel(param.Type))
            {
                return SpecializationPattern.SetExpression;
            }
        }
        
        return SpecializationPattern.None;
    }

    /// <summary>
    /// Checks if a type is Expression<Func<TEntity, bool>>.
    /// </summary>
    private static bool IsExpressionFuncEntityBool(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;
        
        if (namedType.Name != "Expression" || namedType.TypeArguments.Length != 1)
            return false;
        
        var funcType = namedType.TypeArguments[0] as INamedTypeSymbol;
        if (funcType == null || funcType.Name != "Func" || funcType.TypeArguments.Length != 2)
            return false;
        
        // Check if return type is bool
        var returnType = funcType.TypeArguments[1];
        return returnType.SpecialType == SpecialType.System_Boolean;
    }

    /// <summary>
    /// Checks if a type is Expression<Func<TUpdateExpressions, TUpdateModel>>.
    /// </summary>
    private static bool IsExpressionFuncUpdateExpressionsModel(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;
        
        if (namedType.Name != "Expression" || namedType.TypeArguments.Length != 1)
            return false;
        
        var funcType = namedType.TypeArguments[0] as INamedTypeSymbol;
        if (funcType == null || funcType.Name != "Func" || funcType.TypeArguments.Length != 2)
            return false;
        
        // Check if first argument is a type parameter (TUpdateExpressions)
        return funcType.TypeArguments[0] is ITypeParameterSymbol;
    }

    /// <summary>
    /// Gets the remaining type parameters after specialization.
    /// </summary>
    private static List<string> GetRemainingTypeParameters(IMethodSymbol method, SpecializationPattern pattern, bool alwaysRemoveT = false)
    {
        var remaining = new List<string>();
        
        foreach (var typeParam in method.TypeParameters)
        {
            bool shouldRemove = pattern switch
            {
                // For WhereExpression, remove both T (builder type) and TEntity (entity type)
                // T is removed because the wrapper returns the entity-specific builder type
                SpecializationPattern.WhereExpression => typeParam.Name == "T" || typeParam.Name == "TEntity",
                // For SetExpression, remove T, TEntity, and TUpdateExpressions
                SpecializationPattern.SetExpression => typeParam.Name == "T" || typeParam.Name == "TEntity" || typeParam.Name == "TUpdateExpressions",
                _ => alwaysRemoveT && typeParam.Name == "T" // For simple wrappers, optionally remove T
            };
            
            if (!shouldRemove)
            {
                remaining.Add(typeParam.Name);
            }
        }
        
        return remaining;
    }

    /// <summary>
    /// Specializes a parameter type by replacing generic type parameters with concrete types.
    /// </summary>
    private static string SpecializeParameterType(ITypeSymbol type, string entityClassName, string updateExpressionsClassName, SpecializationPattern pattern)
    {
        var typeString = type.ToDisplayString();
        
        if (pattern == SpecializationPattern.WhereExpression)
        {
            // Replace TEntity with concrete entity type
            typeString = typeString.Replace("TEntity", entityClassName);
        }
        else if (pattern == SpecializationPattern.SetExpression)
        {
            // Replace TEntity and TUpdateExpressions with concrete types
            typeString = typeString.Replace("TEntity", entityClassName);
            typeString = typeString.Replace("TUpdateExpressions", updateExpressionsClassName);
        }
        
        return typeString;
    }

    /// <summary>
    /// Gets the specialized type arguments for a method call.
    /// </summary>
    private static List<string> GetSpecializedTypeArguments(
        IMethodSymbol method,
        string entityClassName,
        string updateExpressionsClassName,
        SpecializationPattern pattern,
        List<string> remainingTypeParams,
        string builderClassName)
    {
        var typeArgs = new List<string>();
        
        foreach (var typeParam in method.TypeParameters)
        {
            if (typeParam.Name == "T")
            {
                // T is the builder type - use the base UpdateItemRequestBuilder<TEntity> type
                // because the entity-specific builder inherits from it and doesn't implement
                // IWithConditionExpression<EntitySpecificBuilder>, it implements
                // IWithConditionExpression<UpdateItemRequestBuilder<TEntity>> via inheritance
                typeArgs.Add($"UpdateItemRequestBuilder<{entityClassName}>");
            }
            else if (typeParam.Name == "TEntity")
            {
                typeArgs.Add(entityClassName);
            }
            else if (typeParam.Name == "TUpdateExpressions")
            {
                typeArgs.Add(updateExpressionsClassName);
            }
            else if (remainingTypeParams.Contains(typeParam.Name))
            {
                typeArgs.Add(typeParam.Name);
            }
        }
        
        return typeArgs;
    }

    /// <summary>
    /// Gets a summary description for a method.
    /// </summary>
    private static string GetMethodSummary(IMethodSymbol method)
    {
        // Try to extract from XML documentation if available
        var xmlDoc = method.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(xmlDoc))
        {
            // Simple extraction - in production, use proper XML parsing
            var summaryStart = xmlDoc.IndexOf("<summary>");
            var summaryEnd = xmlDoc.IndexOf("</summary>");
            if (summaryStart >= 0 && summaryEnd > summaryStart)
            {
                var summary = xmlDoc.Substring(summaryStart + 9, summaryEnd - summaryStart - 9).Trim();
                return summary;
            }
        }
        
        return $"Wrapper for {method.Name} extension method.";
    }

    /// <summary>
    /// Gets a description for a parameter.
    /// </summary>
    private static string GetParameterDescription(IParameterSymbol param)
    {
        return $"The {param.Name} parameter.";
    }

    /// <summary>
    /// Gets the constraints for a type parameter.
    /// </summary>
    private static string GetTypeParameterConstraints(ITypeParameterSymbol typeParam)
    {
        var constraints = new List<string>();
        
        if (typeParam.HasReferenceTypeConstraint)
            constraints.Add("class");
        
        if (typeParam.HasValueTypeConstraint)
            constraints.Add("struct");
        
        if (typeParam.HasUnmanagedTypeConstraint)
            constraints.Add("unmanaged");
        
        foreach (var constraintType in typeParam.ConstraintTypes)
        {
            constraints.Add(constraintType.ToDisplayString());
        }
        
        if (typeParam.HasConstructorConstraint)
            constraints.Add("new()");
        
        return string.Join(", ", constraints);
    }

    /// <summary>
    /// Formats a default value for code generation.
    /// </summary>
    private static string FormatDefaultValue(object? value)
    {
        if (value == null)
            return "null";
        
        if (value is string str)
            return $"\"{str}\"";
        
        if (value is bool b)
            return b ? "true" : "false";
        
        return value.ToString() ?? "null";
    }

    /// <summary>
    /// Specialization patterns for extension methods.
    /// </summary>
    private enum SpecializationPattern
    {
        None,
        WhereExpression,
        SetExpression
    }

    /// <summary>
    /// Generates covariant return type overrides for all fluent methods.
    /// This ensures that method chaining maintains the entity-specific builder type.
    /// </summary>
    private static void GenerateCovariantReturnTypeOverrides(StringBuilder sb, string builderClassName)
    {
        // ForTable
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Specifies the table name for the update operation.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ForTable(string tableName)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ForTable(tableName);");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // ReturnAllNewValues
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Returns all attributes of the item after the update.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ReturnAllNewValues()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ReturnAllNewValues();");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // ReturnAllOldValues
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Returns all attributes of the item before the update.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ReturnAllOldValues()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ReturnAllOldValues();");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // ReturnUpdatedNewValues
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Returns only the updated attributes after the update.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ReturnUpdatedNewValues()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ReturnUpdatedNewValues();");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // ReturnUpdatedOldValues
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Returns only the updated attributes before the update.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ReturnUpdatedOldValues()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ReturnUpdatedOldValues();");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // ReturnNone
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Returns no attributes in the response.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ReturnNone()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ReturnNone();");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // ReturnTotalConsumedCapacity
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Returns the total consumed capacity for the operation.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ReturnTotalConsumedCapacity()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ReturnTotalConsumedCapacity();");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // ReturnItemCollectionMetrics
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Returns item collection metrics for the operation.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ReturnItemCollectionMetrics()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ReturnItemCollectionMetrics();");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // ReturnOldValuesOnConditionCheckFailure
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Returns old values when a condition check fails.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>The builder instance for method chaining.</returns>");
        sb.AppendLine($"    public new {builderClassName} ReturnOldValuesOnConditionCheckFailure()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        base.ReturnOldValuesOnConditionCheckFailure();");
        sb.AppendLine($"        return this;");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }
}
