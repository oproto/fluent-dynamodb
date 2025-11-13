using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Discovers extension methods marked with [GenerateWrapper] attribute for automatic wrapper generation.
/// </summary>
public class ExtensionMethodDiscovery
{
    private readonly Compilation _compilation;
    private readonly List<Diagnostic> _diagnostics = new();

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public ExtensionMethodDiscovery(Compilation compilation)
    {
        _compilation = compilation;
    }

    /// <summary>
    /// Discovers all extension methods marked with [GenerateWrapper] attribute in the specified namespace.
    /// </summary>
    /// <param name="namespaceName">The namespace to scan (e.g., "Oproto.FluentDynamoDb.Requests.Extensions")</param>
    /// <returns>List of discovered extension method information grouped by interface</returns>
    public Dictionary<string, List<ExtensionMethodInfo>> DiscoverExtensionMethods(string namespaceName = "Oproto.FluentDynamoDb.Requests.Extensions")
    {
        var result = new Dictionary<string, List<ExtensionMethodInfo>>();

        // Get the GenerateWrapper attribute symbol
        var generateWrapperAttribute = _compilation.GetTypeByMetadataName("Oproto.FluentDynamoDb.Attributes.GenerateWrapperAttribute");
        if (generateWrapperAttribute == null)
        {
            // Attribute not found - this is expected during initial compilation
            return result;
        }

        // Find all types in the target namespace from both source and referenced assemblies
        var extensionClasses = FindExtensionClasses(namespaceName);
        var referencedExtensionClasses = FindExtensionClassesInReferences(namespaceName);
        
        // Combine both lists
        var allExtensionClasses = extensionClasses.Concat(referencedExtensionClasses).ToList();

        foreach (var extensionClass in allExtensionClasses)
        {
            var methods = extensionClass.GetMembers().OfType<IMethodSymbol>();

            foreach (var method in methods)
            {
                // Check if method has GenerateWrapper attribute
                var attribute = method.GetAttributes()
                    .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateWrapperAttribute));

                if (attribute == null)
                    continue;

                // Validate that this is an extension method
                if (!method.IsExtensionMethod)
                {
                    ReportNonExtensionMethodError(method);
                    continue;
                }

                // Extract attribute properties
                var requiresSpecialization = GetAttributeProperty<bool>(attribute, "RequiresSpecialization");
                var specializationNotes = GetAttributeProperty<string>(attribute, "SpecializationNotes");

                // Get the interface this method extends
                var extendedInterface = GetExtendedInterface(method);
                if (extendedInterface == null)
                {
                    ReportInvalidExtensionMethodError(method);
                    continue;
                }

                // Create extension method info
                var methodInfo = new ExtensionMethodInfo
                {
                    MethodName = method.Name,
                    MethodSymbol = method,
                    ExtendedInterface = extendedInterface,
                    RequiresSpecialization = requiresSpecialization,
                    SpecializationNotes = specializationNotes,
                    Parameters = method.Parameters.ToImmutableArray(),
                    ReturnType = method.ReturnType,
                    TypeParameters = method.TypeParameters.ToImmutableArray(),
                    IsGeneric = method.IsGenericMethod
                };

                // Group by interface
                var interfaceName = extendedInterface.ToDisplayString();
                if (!result.ContainsKey(interfaceName))
                {
                    result[interfaceName] = new List<ExtensionMethodInfo>();
                }

                result[interfaceName].Add(methodInfo);
            }
        }

        return result;
    }

    /// <summary>
    /// Validates that a builder type implements the required interfaces for the discovered extension methods.
    /// </summary>
    /// <param name="builderType">The builder type to validate</param>
    /// <param name="extensionMethods">The discovered extension methods grouped by interface</param>
    /// <returns>True if all required interfaces are implemented</returns>
    public bool ValidateBuilderImplementsInterfaces(
        INamedTypeSymbol builderType,
        Dictionary<string, List<ExtensionMethodInfo>> extensionMethods)
    {
        var allValid = true;

        foreach (var interfaceName in extensionMethods.Keys)
        {
            var requiredInterface = _compilation.GetTypeByMetadataName(interfaceName);
            if (requiredInterface == null)
            {
                // Try to find by simple name
                var simpleName = interfaceName.Split('.').Last().Split('<').First();
                requiredInterface = FindInterfaceBySimpleName(simpleName);
            }

            if (requiredInterface == null)
            {
                ReportInterfaceNotFoundError(interfaceName, builderType);
                allValid = false;
                continue;
            }

            // Check if builder implements this interface
            if (!ImplementsInterface(builderType, requiredInterface))
            {
                ReportInterfaceNotImplementedError(builderType, requiredInterface);
                allValid = false;
            }
        }

        return allValid;
    }

    private List<INamedTypeSymbol> FindExtensionClasses(string namespaceName)
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var syntaxTree in _compilation.SyntaxTrees)
        {
            var semanticModel = _compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                if (classSymbol == null)
                    continue;

                // Check if class is in the target namespace
                if (classSymbol.ContainingNamespace?.ToDisplayString() != namespaceName)
                    continue;

                // Check if class is static (extension methods must be in static classes)
                if (!classSymbol.IsStatic)
                    continue;

                result.Add(classSymbol);
            }
        }

        return result;
    }

    private List<INamedTypeSymbol> FindExtensionClassesInReferences(string namespaceName)
    {
        var result = new List<INamedTypeSymbol>();

        // Search through all referenced assemblies
        foreach (var reference in _compilation.References)
        {
            if (_compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assemblySymbol)
                continue;

            // Get all types in the target namespace from this assembly
            var namespaceSymbol = GetNamespaceSymbol(assemblySymbol.GlobalNamespace, namespaceName);
            if (namespaceSymbol == null)
                continue;

            // Get all types in this namespace
            var types = GetAllTypes(namespaceSymbol);
            
            foreach (var type in types)
            {
                // Check if type is a static class (extension methods must be in static classes)
                if (type.IsStatic && type.TypeKind == TypeKind.Class)
                {
                    result.Add(type);
                }
            }
        }

        return result;
    }

    private INamespaceSymbol? GetNamespaceSymbol(INamespaceSymbol rootNamespace, string fullNamespaceName)
    {
        var parts = fullNamespaceName.Split('.');
        var current = rootNamespace;

        foreach (var part in parts)
        {
            var found = false;
            foreach (var member in current.GetNamespaceMembers())
            {
                if (member.Name == part)
                {
                    current = member;
                    found = true;
                    break;
                }
            }

            if (!found)
                return null;
        }

        return current;
    }

    private List<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
    {
        var result = new List<INamedTypeSymbol>();

        // Add all types directly in this namespace
        result.AddRange(namespaceSymbol.GetTypeMembers());

        // Recursively add types from nested namespaces
        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            result.AddRange(GetAllTypes(nestedNamespace));
        }

        return result;
    }

    private INamedTypeSymbol? GetExtendedInterface(IMethodSymbol method)
    {
        if (!method.IsExtensionMethod || method.Parameters.Length == 0)
            return null;

        var firstParameter = method.Parameters[0];
        var parameterType = firstParameter.Type;

        // If the parameter is a generic type parameter constrained to an interface, get the interface
        if (parameterType is ITypeParameterSymbol typeParam)
        {
            // For extension methods like "this IWithConditionExpression<T> builder",
            // we need to look at the constraint or the actual usage
            // In our case, the first parameter type should be the interface
            return null; // We'll handle this differently
        }

        // For generic extension methods like "this IWithConditionExpression<T> builder",
        // we need to extract the interface from the generic type
        // Check this BEFORE checking for interface type, since generic interfaces match both conditions
        if (parameterType is INamedTypeSymbol genericType && genericType.IsGenericType)
        {
            return genericType.ConstructUnboundGenericType();
        }

        // If the parameter is a named type (interface), return it
        if (parameterType is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Interface)
        {
            return namedType;
        }

        return null;
    }

    private bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
    {
        // Check direct interfaces
        foreach (var iface in type.Interfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface.ConstructUnboundGenericType(), interfaceType.ConstructUnboundGenericType()))
                return true;
        }

        // Check base type
        if (type.BaseType != null)
        {
            return ImplementsInterface(type.BaseType, interfaceType);
        }

        return false;
    }

    private INamedTypeSymbol? FindInterfaceBySimpleName(string simpleName)
    {
        // Search through all types in the compilation
        foreach (var syntaxTree in _compilation.SyntaxTrees)
        {
            var semanticModel = _compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            var interfaceDeclarations = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();

            foreach (var interfaceDecl in interfaceDeclarations)
            {
                var interfaceSymbol = semanticModel.GetDeclaredSymbol(interfaceDecl) as INamedTypeSymbol;
                if (interfaceSymbol?.Name == simpleName)
                {
                    return interfaceSymbol;
                }
            }
        }

        return null;
    }

    private T? GetAttributeProperty<T>(AttributeData attribute, string propertyName)
    {
        var namedArg = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == propertyName);
        if (namedArg.Value.Value is T value)
        {
            return value;
        }

        return default;
    }

    private void ReportNonExtensionMethodError(IMethodSymbol method)
    {
        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor(
                "DYNDB1001",
                "Invalid GenerateWrapper usage",
                "Method '{0}' is marked with [GenerateWrapper] but is not an extension method",
                "DynamoDb.SourceGenerator",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            method.Locations.FirstOrDefault(),
            method.Name);

        _diagnostics.Add(diagnostic);
    }

    private void ReportInvalidExtensionMethodError(IMethodSymbol method)
    {
        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor(
                "DYNDB1002",
                "Invalid extension method",
                "Extension method '{0}' marked with [GenerateWrapper] does not extend a valid interface",
                "DynamoDb.SourceGenerator",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            method.Locations.FirstOrDefault(),
            method.Name);

        _diagnostics.Add(diagnostic);
    }

    private void ReportInterfaceNotFoundError(string interfaceName, INamedTypeSymbol builderType)
    {
        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor(
                "DYNDB1003",
                "Interface not found",
                "Interface '{0}' required by extension methods could not be found for builder '{1}'",
                "DynamoDb.SourceGenerator",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true),
            builderType.Locations.FirstOrDefault(),
            interfaceName,
            builderType.Name);

        _diagnostics.Add(diagnostic);
    }

    private void ReportInterfaceNotImplementedError(INamedTypeSymbol builderType, INamedTypeSymbol interfaceType)
    {
        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor(
                "DYNDB1004",
                "Interface not implemented",
                "Builder '{0}' does not implement interface '{1}' required by extension methods marked with [GenerateWrapper]",
                "DynamoDb.SourceGenerator",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true),
            builderType.Locations.FirstOrDefault(),
            builderType.Name,
            interfaceType.Name);

        _diagnostics.Add(diagnostic);
    }
}

/// <summary>
/// Information about an extension method marked with [GenerateWrapper].
/// </summary>
public class ExtensionMethodInfo
{
    public string MethodName { get; set; } = string.Empty;
    public IMethodSymbol? MethodSymbol { get; set; }
    public INamedTypeSymbol? ExtendedInterface { get; set; }
    public bool RequiresSpecialization { get; set; }
    public string? SpecializationNotes { get; set; }
    public ImmutableArray<IParameterSymbol> Parameters { get; set; }
    public ITypeSymbol? ReturnType { get; set; }
    public ImmutableArray<ITypeParameterSymbol> TypeParameters { get; set; }
    public bool IsGeneric { get; set; }
}
