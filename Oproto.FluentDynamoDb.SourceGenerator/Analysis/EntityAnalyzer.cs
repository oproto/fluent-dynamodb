using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes class declarations to extract DynamoDB entity information.
/// </summary>
public class EntityAnalyzer
{
    private readonly List<Diagnostic> _diagnostics = new();

    /// <summary>
    /// Gets the diagnostics collected during analysis.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Analyzes a class declaration and extracts entity model information.
    /// </summary>
    /// <param name="classDecl">The class declaration to analyze.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <returns>The extracted entity model, or null if analysis failed.</returns>
    public EntityModel? AnalyzeEntity(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        _diagnostics.Clear();

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null)
            return null;

        // Check if class is partial
        if (!IsPartialClass(classDecl))
        {
            ReportDiagnostic(DiagnosticDescriptors.EntityMustBePartial, classDecl.Identifier.GetLocation(), classSymbol.Name);
            return null;
        }

        var entityModel = new EntityModel
        {
            ClassName = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            ClassDeclaration = classDecl
        };

        // Extract table information
        if (!ExtractTableInfo(classDecl, semanticModel, entityModel))
            return null;

        // Extract property information
        ExtractProperties(classDecl, semanticModel, entityModel);

        // Validate entity configuration
        ValidateEntityModel(entityModel);

        // Extract index information
        ExtractIndexes(entityModel);

        // Extract relationship information
        ExtractRelationships(classDecl, semanticModel, entityModel);

        return _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) ? null : entityModel;
    }

    private bool IsPartialClass(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Modifiers.Any(m => m.ValueText == "partial");
    }

    private bool ExtractTableInfo(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var tableAttribute = GetAttribute(classDecl, semanticModel, "DynamoDbTableAttribute");
        if (tableAttribute == null)
            return false;

        // Extract table name from constructor argument
        if (tableAttribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax tableNameLiteral)
        {
            entityModel.TableName = tableNameLiteral.Token.ValueText;
        }

        // Extract entity discriminator from named argument
        var discriminatorArg = tableAttribute.ArgumentList?.Arguments
            .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "EntityDiscriminator");
        
        if (discriminatorArg?.Expression is LiteralExpressionSyntax discriminatorLiteral)
        {
            entityModel.EntityDiscriminator = discriminatorLiteral.Token.ValueText;
        }

        return !string.IsNullOrEmpty(entityModel.TableName);
    }

    private void ExtractProperties(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var properties = new List<PropertyModel>();

        foreach (var member in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            var propertyModel = AnalyzeProperty(member, semanticModel);
            if (propertyModel != null)
            {
                properties.Add(propertyModel);
            }
        }

        entityModel.Properties = properties.ToArray();
    }

    private PropertyModel? AnalyzeProperty(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel)
    {
        var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDecl) as IPropertySymbol;
        if (propertySymbol == null)
            return null;

        var propertyModel = new PropertyModel
        {
            PropertyName = propertySymbol.Name,
            PropertyType = propertySymbol.Type.ToDisplayString(),
            PropertyDeclaration = propertyDecl,
            IsNullable = propertySymbol.Type.CanBeReferencedByName && propertySymbol.NullableAnnotation == NullableAnnotation.Annotated,
            IsCollection = IsCollectionType(propertySymbol.Type)
        };

        // Extract DynamoDbAttribute
        var dynamoDbAttribute = GetAttribute(propertyDecl, semanticModel, "DynamoDbAttributeAttribute");
        if (dynamoDbAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax attributeNameLiteral)
        {
            propertyModel.AttributeName = attributeNameLiteral.Token.ValueText;
        }

        // Extract key attributes
        ExtractKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract GSI attributes
        ExtractGsiAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract queryable attributes
        ExtractQueryableAttributes(propertyDecl, semanticModel, propertyModel);

        // Validate property configuration
        ValidatePropertyModel(propertyModel);

        return propertyModel;
    }

    private void ExtractKeyAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        // Check for PartitionKey attribute
        var partitionKeyAttr = GetAttribute(propertyDecl, semanticModel, "PartitionKeyAttribute");
        if (partitionKeyAttr != null)
        {
            propertyModel.IsPartitionKey = true;
            propertyModel.KeyFormat = ExtractKeyFormat(partitionKeyAttr);
        }

        // Check for SortKey attribute
        var sortKeyAttr = GetAttribute(propertyDecl, semanticModel, "SortKeyAttribute");
        if (sortKeyAttr != null)
        {
            propertyModel.IsSortKey = true;
            propertyModel.KeyFormat ??= ExtractKeyFormat(sortKeyAttr);
        }
    }

    private KeyFormatModel ExtractKeyFormat(AttributeSyntax keyAttribute)
    {
        var keyFormat = new KeyFormatModel();

        if (keyAttribute.ArgumentList != null)
        {
            foreach (var arg in keyAttribute.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "Prefix" && 
                    arg.Expression is LiteralExpressionSyntax prefixLiteral)
                {
                    keyFormat.Prefix = prefixLiteral.Token.ValueText;
                }
                else if (arg.NameEquals?.Name.Identifier.ValueText == "Separator" && 
                         arg.Expression is LiteralExpressionSyntax separatorLiteral)
                {
                    keyFormat.Separator = separatorLiteral.Token.ValueText;
                }
            }
        }

        return keyFormat;
    }

    private void ExtractGsiAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var gsiAttributes = GetAttributes(propertyDecl, semanticModel, "GlobalSecondaryIndexAttribute");
        var gsiModels = new List<GlobalSecondaryIndexModel>();

        foreach (var gsiAttr in gsiAttributes)
        {
            var gsiModel = new GlobalSecondaryIndexModel();

            // Extract index name from constructor argument
            if (gsiAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax indexNameLiteral)
            {
                gsiModel.IndexName = indexNameLiteral.Token.ValueText;
            }

            // Extract named arguments
            if (gsiAttr.ArgumentList != null)
            {
                foreach (var arg in gsiAttr.ArgumentList.Arguments)
                {
                    switch (arg.NameEquals?.Name.Identifier.ValueText)
                    {
                        case "IsPartitionKey" when arg.Expression is LiteralExpressionSyntax partitionKeyLiteral:
                            gsiModel.IsPartitionKey = bool.Parse(partitionKeyLiteral.Token.ValueText);
                            break;
                        case "IsSortKey" when arg.Expression is LiteralExpressionSyntax sortKeyLiteral:
                            gsiModel.IsSortKey = bool.Parse(sortKeyLiteral.Token.ValueText);
                            break;
                        case "KeyFormat" when arg.Expression is LiteralExpressionSyntax keyFormatLiteral:
                            gsiModel.KeyFormat = keyFormatLiteral.Token.ValueText;
                            break;
                    }
                }
            }

            gsiModels.Add(gsiModel);
        }

        propertyModel.GlobalSecondaryIndexes = gsiModels.ToArray();
    }

    private void ExtractQueryableAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var queryableAttr = GetAttribute(propertyDecl, semanticModel, "QueryableAttribute");
        if (queryableAttr == null)
            return;

        var queryableModel = new QueryableModel();

        // Extract named arguments
        if (queryableAttr.ArgumentList != null)
        {
            foreach (var arg in queryableAttr.ArgumentList.Arguments)
            {
                switch (arg.NameEquals?.Name.Identifier.ValueText)
                {
                    case "SupportedOperations":
                        // TODO: Extract array of operations - simplified for now
                        break;
                    case "AvailableInIndexes":
                        // TODO: Extract array of index names - simplified for now
                        break;
                }
            }
        }

        propertyModel.Queryable = queryableModel;
    }

    private void ExtractIndexes(EntityModel entityModel)
    {
        var indexes = new Dictionary<string, IndexModel>();

        foreach (var property in entityModel.Properties)
        {
            foreach (var gsi in property.GlobalSecondaryIndexes)
            {
                if (!indexes.TryGetValue(gsi.IndexName, out var indexModel))
                {
                    indexModel = new IndexModel { IndexName = gsi.IndexName };
                    indexes[gsi.IndexName] = indexModel;
                }

                if (gsi.IsPartitionKey)
                {
                    indexModel.PartitionKeyProperty = property.PropertyName;
                    indexModel.PartitionKeyFormat = gsi.KeyFormat;
                }
                else if (gsi.IsSortKey)
                {
                    indexModel.SortKeyProperty = property.PropertyName;
                    indexModel.SortKeyFormat = gsi.KeyFormat;
                }
            }
        }

        entityModel.Indexes = indexes.Values.ToArray();
    }

    private void ExtractRelationships(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var relationships = new List<RelationshipModel>();

        foreach (var member in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            var relatedEntityAttr = GetAttribute(member, semanticModel, "RelatedEntityAttribute");
            if (relatedEntityAttr == null)
                continue;

            var propertySymbol = semanticModel.GetDeclaredSymbol(member) as IPropertySymbol;
            if (propertySymbol == null)
                continue;

            var relationshipModel = new RelationshipModel
            {
                PropertyName = propertySymbol.Name,
                PropertyType = propertySymbol.Type.ToDisplayString(),
                IsCollection = IsCollectionType(propertySymbol.Type)
            };

            // Extract sort key pattern from constructor argument
            if (relatedEntityAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax patternLiteral)
            {
                relationshipModel.SortKeyPattern = patternLiteral.Token.ValueText;
            }

            // Extract entity type from named argument
            var entityTypeArg = relatedEntityAttr.ArgumentList?.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "EntityType");
            
            if (entityTypeArg?.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                relationshipModel.EntityType = typeOfExpr.Type.ToString();
            }

            relationships.Add(relationshipModel);
        }

        entityModel.Relationships = relationships.ToArray();
    }

    private void ValidateEntityModel(EntityModel entityModel)
    {
        var partitionKeyProperties = entityModel.Properties.Where(p => p.IsPartitionKey).ToArray();
        var sortKeyProperties = entityModel.Properties.Where(p => p.IsSortKey).ToArray();

        // Validate partition key
        if (partitionKeyProperties.Length == 0)
        {
            ReportDiagnostic(DiagnosticDescriptors.MissingPartitionKey, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
        }
        else if (partitionKeyProperties.Length > 1)
        {
            ReportDiagnostic(DiagnosticDescriptors.MultiplePartitionKeys, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
        }

        // Validate sort key
        if (sortKeyProperties.Length > 1)
        {
            ReportDiagnostic(DiagnosticDescriptors.MultipleSortKeys, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
        }

        // Validate GSI configurations
        foreach (var index in entityModel.Indexes)
        {
            if (string.IsNullOrEmpty(index.PartitionKeyProperty))
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidGsiConfiguration, 
                    entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                    index.IndexName, entityModel.ClassName);
            }
        }

        // Check if entity is multi-item (has collection properties with DynamoDB attributes)
        entityModel.IsMultiItemEntity = entityModel.Properties.Any(p => p.IsCollection && p.HasAttributeMapping);
        
        // Validate multi-item entity consistency
        if (entityModel.IsMultiItemEntity)
        {
            ValidateMultiItemEntityConsistency(entityModel);
        }

        // Validate related entity configurations
        if (entityModel.Relationships.Length > 0)
        {
            ValidateRelatedEntityConfiguration(entityModel);
        }

        // Additional comprehensive validations
        ValidateEntityComplexity(entityModel);
        ValidateEntityScalability(entityModel);
        ValidateCircularReferences(entityModel);
    }

    private void ValidatePropertyModel(PropertyModel propertyModel)
    {
        // Check if property has key attributes but missing DynamoDbAttribute
        if ((propertyModel.IsPartitionKey || propertyModel.IsSortKey || propertyModel.IsPartOfGsi) && 
            string.IsNullOrEmpty(propertyModel.AttributeName))
        {
            ReportDiagnostic(DiagnosticDescriptors.MissingDynamoDbAttribute, 
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(), 
                propertyModel.PropertyName);
        }

        // Validate property type support
        if (!IsSupportedPropertyType(propertyModel.PropertyType))
        {
            ReportDiagnostic(DiagnosticDescriptors.UnsupportedPropertyType, 
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(), 
                propertyModel.PropertyName, propertyModel.PropertyType);
        }

        // Validate attribute name
        if (!string.IsNullOrEmpty(propertyModel.AttributeName))
        {
            ValidateAttributeName(propertyModel);
        }

        // Validate key format if present
        if (propertyModel.KeyFormat != null)
        {
            ValidateKeyFormat(propertyModel);
        }

        // Check for collection properties used as keys
        if (propertyModel.IsCollection && (propertyModel.IsPartitionKey || propertyModel.IsSortKey))
        {
            ReportDiagnostic(DiagnosticDescriptors.CollectionPropertyCannotBeKey,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, "Entity");
        }

        // Performance warnings for large types
        ValidatePropertyPerformance(propertyModel);
    }

    private void ValidateAttributeName(PropertyModel propertyModel)
    {
        var attributeName = propertyModel.AttributeName;
        
        // Check for invalid characters
        if (attributeName.Contains('\0') || attributeName.Contains('\n') || attributeName.Contains('\r'))
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidAttributeName,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                attributeName, propertyModel.PropertyName, "Contains invalid control characters");
        }

        // Check for reserved words
        var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ABORT", "ABSOLUTE", "ACTION", "ADD", "AFTER", "AGENT", "AGGREGATE", "ALL", "ALLOCATE", "ALTER",
            "ANALYZE", "AND", "ANY", "ARCHIVE", "ARE", "ARRAY", "AS", "ASC", "ASCII", "ASENSITIVE", "ASSERTION",
            "ASYMMETRIC", "AT", "ATOMIC", "ATTACH", "ATTRIBUTE", "AUTH", "AUTHORIZATION", "AUTHORIZE", "AUTO",
            "AVG", "BACK", "BACKUP", "BASE", "BATCH", "BEFORE", "BEGIN", "BETWEEN", "BIGINT", "BINARY", "BIT",
            "BLOB", "BLOCK", "BOOLEAN", "BOTH", "BREADTH", "BUCKET", "BULK", "BY", "BYTE", "CALL", "CALLED",
            "CALLING", "CAPACITY", "CASCADE", "CASCADED", "CASE", "CAST", "CATALOG", "CHAR", "CHARACTER",
            "CHECK", "CLASS", "CLOB", "CLOSE", "CLUSTER", "CLUSTERED", "CLUSTERING", "CLUSTERS", "COALESCE",
            "COLLATE", "COLLATION", "COLLECTION", "COLUMN", "COLUMNS", "COMBINE", "COMMENT", "COMMIT",
            "COMPACT", "COMPILE", "COMPRESS", "CONDITION", "CONFLICT", "CONNECT", "CONNECTION", "CONSISTENCY",
            "CONSISTENT", "CONSTRAINT", "CONSTRAINTS", "CONSTRUCTOR", "CONSUMED", "CONTAINS", "CONTINUE",
            "CONVERT", "COPY", "CORRESPONDING", "COUNT", "COUNTER", "CREATE", "CROSS", "CUBE", "CURRENT",
            "CURSOR", "CYCLE", "DATA", "DATABASE", "DATE", "DATETIME", "DAY", "DEALLOCATE", "DEC", "DECIMAL",
            "DECLARE", "DEFAULT", "DEFERRABLE", "DEFERRED", "DEFINE", "DEFINED", "DEFINITION", "DELETE",
            "DELIMITED", "DEPTH", "DEREF", "DESC", "DESCRIBE", "DESCRIPTOR", "DETACH", "DETERMINISTIC",
            "DIAGNOSTICS", "DIRECTORIES", "DISABLE", "DISCONNECT", "DISTINCT", "DISTRIBUTE", "DO", "DOMAIN",
            "DOUBLE", "DROP", "DUMP", "DURATION", "DYNAMIC", "EACH", "ELEMENT", "ELSE", "ELSEIF", "EMPTY",
            "ENABLE", "END", "EQUAL", "EQUALS", "ERROR", "ESCAPE", "ESCAPED", "EVAL", "EVALUATE", "EXCEEDED",
            "EXCEPT", "EXCEPTION", "EXCEPTIONS", "EXCLUSIVE", "EXEC", "EXECUTE", "EXISTS", "EXIT", "EXPLAIN",
            "EXPLODE", "EXPORT", "EXPRESSION", "EXTENDED", "EXTERNAL", "EXTRACT", "FAIL", "FALSE", "FAMILY",
            "FETCH", "FIELDS", "FILE", "FILTER", "FILTERING", "FINAL", "FINISH", "FIRST", "FIXED", "FLATTERN",
            "FLOAT", "FOR", "FORCE", "FOREIGN", "FORMAT", "FORWARD", "FOUND", "FREE", "FROM", "FULL",
            "FUNCTION", "FUNCTIONS", "GENERAL", "GENERATE", "GET", "GLOB", "GLOBAL", "GO", "GOTO", "GRANT",
            "GREATER", "GROUP", "GROUPING", "HANDLER", "HASH", "HAVE", "HAVING", "HEAP", "HIDDEN", "HOLD",
            "HOUR", "IDENTIFIED", "IDENTITY", "IF", "IGNORE", "IMMEDIATE", "IMPORT", "IN", "INCLUDING",
            "INCLUSIVE", "INCREMENT", "INCREMENTAL", "INDEX", "INDEXED", "INDEXES", "INDICATOR", "INFINITE",
            "INITIALLY", "INLINE", "INNER", "INNTER", "INOUT", "INPUT", "INSENSITIVE", "INSERT", "INSTEAD",
            "INT", "INTEGER", "INTERSECT", "INTERVAL", "INTO", "INVALIDATE", "IS", "ISOLATION", "ITEM",
            "ITEMS", "ITERATE", "JOIN", "KEY", "KEYS", "LAG", "LANGUAGE", "LARGE", "LAST", "LATERAL", "LEAD",
            "LEADING", "LEAVE", "LEFT", "LENGTH", "LESS", "LEVEL", "LIKE", "LIMIT", "LIMITED", "LINES", "LIST",
            "LOAD", "LOCAL", "LOCALTIME", "LOCALTIMESTAMP", "LOCATION", "LOCATOR", "LOCK", "LOCKS", "LOG",
            "LOGED", "LONG", "LOOP", "LOWER", "MAP", "MATCH", "MATERIALIZED", "MAX", "MAXLEN", "MEMBER",
            "MERGE", "METHOD", "METRICS", "MIN", "MINUS", "MINUTE", "MISSING", "MOD", "MODE", "MODIFIES",
            "MODIFY", "MODULE", "MONTH", "MULTI", "MULTISET", "NAME", "NAMES", "NATIONAL", "NATURAL", "NCHAR",
            "NCLOB", "NEW", "NEXT", "NO", "NONE", "NOT", "NULL", "NULLIF", "NUMBER", "NUMERIC", "OBJECT",
            "OF", "OFFLINE", "OFFSET", "OLD", "ON", "ONLINE", "ONLY", "OPAQUE", "OPEN", "OPERATOR", "OPTION",
            "OR", "ORDER", "ORDINALITY", "OTHER", "OTHERS", "OUT", "OUTER", "OUTPUT", "OVER", "OVERLAPS",
            "OVERRIDE", "OWNER", "PAD", "PARALLEL", "PARAMETER", "PARAMETERS", "PARTIAL", "PARTITION",
            "PARTITIONED", "PARTITIONS", "PATH", "PERCENT", "PERCENTILE", "PERMISSION", "PERMISSIONS", "PIPE",
            "PIPELINED", "PLAN", "POOL", "POSITION", "PRECISION", "PREPARE", "PRESERVE", "PRIMARY", "PRIOR",
            "PRIVATE", "PRIVILEGES", "PROCEDURE", "PROCESSED", "PROJECT", "PROJECTION", "PROPERTY", "PROVISIONING",
            "PUBLIC", "PUT", "QUERY", "QUIT", "QUORUM", "RAISE", "RANDOM", "RANGE", "RANK", "RAW", "READ",
            "READS", "REAL", "REBUILD", "RECORD", "RECURSIVE", "REDUCE", "REF", "REFERENCE", "REFERENCES",
            "REFERENCING", "REGEXP", "REGION", "REINDEX", "RELATIVE", "RELEASE", "REMAINDER", "RENAME",
            "REPEAT", "REPLACE", "REQUEST", "RESET", "RESIGNAL", "RESOURCE", "RESPONSE", "RESTORE", "RESTRICT",
            "RESULT", "RETURN", "RETURNING", "RETURNS", "REVERSE", "REVOKE", "RIGHT", "ROLE", "ROLES",
            "ROLLBACK", "ROLLUP", "ROUTINE", "ROW", "ROWS", "RULE", "RULES", "SAMPLE", "SATISFIES", "SAVE",
            "SAVEPOINT", "SCAN", "SCHEMA", "SCOPE", "SCROLL", "SEARCH", "SECOND", "SECTION", "SEGMENT",
            "SELECT", "SELF", "SEMI", "SENSITIVE", "SEPARATE", "SEQUENCE", "SERIALIZABLE", "SESSION", "SET",
            "SETS", "SHARD", "SHARE", "SHARED", "SHORT", "SHOW", "SIGNAL", "SIMILAR", "SIZE", "SKEWED",
            "SMALLINT", "SNAPSHOT", "SOME", "SOURCE", "SPACE", "SPACES", "SPARSE", "SPECIFIC", "SPECIFICTYPE",
            "SPLIT", "SQL", "SQLCODE", "SQLERROR", "SQLEXCEPTION", "SQLSTATE", "SQLWARNING", "START", "STATE",
            "STATIC", "STATUS", "STORAGE", "STORE", "STORED", "STREAM", "STRING", "STRUCT", "STYLE", "SUB",
            "SUBMULTISET", "SUBPARTITION", "SUBSTRING", "SUBTYPE", "SUM", "SUPER", "SYMMETRIC", "SYNONYM",
            "SYSTEM", "TABLE", "TABLESAMPLE", "TEMP", "TEMPORARY", "TERMINATED", "TEXT", "THAN", "THEN",
            "THROUGHPUT", "TIME", "TIMESTAMP", "TIMEZONE", "TINYINT", "TO", "TOKEN", "TOTAL", "TOUCH",
            "TRAILING", "TRANSACTION", "TRANSFORM", "TRANSLATE", "TRANSLATION", "TREAT", "TRIGGER", "TRIM",
            "TRUE", "TRUNCATE", "TTL", "TUPLE", "TYPE", "UNDER", "UNDO", "UNION", "UNIQUE", "UNIT", "UNKNOWN",
            "UNLOGGED", "UNNEST", "UNPROCESSED", "UNSIGNED", "UNTIL", "UPDATE", "UPPER", "URL", "USAGE",
            "USE", "USER", "USERS", "USING", "UUID", "VACUUM", "VALUE", "VALUED", "VALUES", "VARCHAR",
            "VARIABLE", "VARIANCE", "VARINT", "VARYING", "VIEW", "VIEWS", "VIRTUAL", "VOID", "WAIT", "WHEN",
            "WHENEVER", "WHERE", "WHILE", "WINDOW", "WITH", "WITHIN", "WITHOUT", "WORK", "WRAPPED", "WRITE",
            "YEAR", "ZONE"
        };

        if (reservedWords.Contains(attributeName))
        {
            ReportDiagnostic(DiagnosticDescriptors.ReservedWordUsage,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, attributeName);
        }

        // Check attribute name length
        if (attributeName.Length > 255)
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidAttributeName,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                attributeName, propertyModel.PropertyName, "Attribute name exceeds 255 character limit");
        }
    }

    private void ValidateKeyFormat(PropertyModel propertyModel)
    {
        var keyFormat = propertyModel.KeyFormat;
        if (keyFormat == null) return;

        // Validate separator
        if (!string.IsNullOrEmpty(keyFormat.Separator))
        {
            if (keyFormat.Separator.Contains('\0') || keyFormat.Separator.Length > 10)
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidKeyFormatSyntax,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    $"Separator: '{keyFormat.Separator}'", propertyModel.PropertyName);
            }
        }

        // Validate prefix
        if (!string.IsNullOrEmpty(keyFormat.Prefix))
        {
            if (keyFormat.Prefix.Contains('\0') || keyFormat.Prefix.Length > 100)
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidKeyFormatSyntax,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    $"Prefix: '{keyFormat.Prefix}'", propertyModel.PropertyName);
            }

            // Check for potential key collision patterns
            if (keyFormat.Prefix.EndsWith(keyFormat.Separator ?? "#"))
            {
                ReportDiagnostic(DiagnosticDescriptors.PotentialKeyCollision,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    $"{keyFormat.Prefix}{keyFormat.Separator}{{value}}", propertyModel.PropertyName);
            }
        }
    }

    private void ValidatePropertyPerformance(PropertyModel propertyModel)
    {
        // Warn about potentially large string properties
        if (propertyModel.PropertyType == "string" && !propertyModel.IsCollection)
        {
            // This is a heuristic - in practice, you'd need more context
            if (propertyModel.PropertyName.ToLowerInvariant().Contains("description") ||
                propertyModel.PropertyName.ToLowerInvariant().Contains("content") ||
                propertyModel.PropertyName.ToLowerInvariant().Contains("body"))
            {
                ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    propertyModel.PropertyName, propertyModel.PropertyType,
                    "Large string properties may impact DynamoDB performance and costs");
            }
        }

        // Warn about complex collection types
        if (propertyModel.IsCollection && IsComplexCollectionType(propertyModel.PropertyType))
        {
            ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, propertyModel.PropertyType,
                "Complex collection types require JSON serialization which may impact performance");
        }
    }

    private bool IsComplexCollectionType(string collectionType)
    {
        // Check if collection contains complex types
        var elementType = GetCollectionElementType(collectionType);
        return !IsPrimitiveType(elementType);
    }

    private bool IsPrimitiveType(string typeName)
    {
        var primitiveTypes = new HashSet<string>
        {
            "string", "int", "long", "double", "float", "decimal", "bool", "DateTime", "DateTimeOffset",
            "Guid", "byte", "short", "uint", "ulong", "ushort", "sbyte", "char",
            "System.String", "System.Int32", "System.Int64", "System.Double", "System.Single",
            "System.Decimal", "System.Boolean", "System.DateTime", "System.DateTimeOffset",
            "System.Guid", "System.Byte", "System.Int16", "System.UInt32", "System.UInt64",
            "System.UInt16", "System.SByte", "System.Char", "Ulid"
        };

        var baseType = typeName.TrimEnd('?');
        return primitiveTypes.Contains(baseType);
    }

    private string GetCollectionElementType(string collectionType)
    {
        // Extract element type from collection types
        if (collectionType.StartsWith("List<") && collectionType.EndsWith(">"))
        {
            return collectionType.Substring(5, collectionType.Length - 6);
        }
        if (collectionType.StartsWith("IList<") && collectionType.EndsWith(">"))
        {
            return collectionType.Substring(6, collectionType.Length - 7);
        }
        if (collectionType.StartsWith("ICollection<") && collectionType.EndsWith(">"))
        {
            return collectionType.Substring(12, collectionType.Length - 13);
        }
        if (collectionType.StartsWith("IEnumerable<") && collectionType.EndsWith(">"))
        {
            return collectionType.Substring(12, collectionType.Length - 13);
        }
        
        return "object";
    }

    private bool IsCollectionType(ITypeSymbol type)
    {
        // Check if type implements IEnumerable<T> but is not string
        if (type.SpecialType == SpecialType.System_String)
            return false;

        return type.AllInterfaces.Any(i => 
            i.IsGenericType && 
            i.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
    }

    private bool IsSupportedPropertyType(string typeName)
    {
        // Basic type support - this will be expanded in later tasks
        var supportedTypes = new[]
        {
            "string", "int", "long", "double", "float", "decimal", "bool", "DateTime", "DateTimeOffset",
            "Guid", "byte[]", "System.String", "System.Int32", "System.Int64", "System.Double", 
            "System.Single", "System.Decimal", "System.Boolean", "System.DateTime", "System.DateTimeOffset",
            "System.Guid", "System.Byte[]", "Ulid"
        };

        // Remove nullable annotations for checking
        var baseType = typeName.TrimEnd('?');
        
        // Check for nullable value types
        if (baseType.StartsWith("System.Nullable<") || baseType.Contains("?"))
        {
            return true; // Assume nullable types are supported if base type is
        }

        // Check for collections
        if (baseType.StartsWith("System.Collections.Generic.List<") ||
            baseType.StartsWith("List<") ||
            baseType.StartsWith("IList<") ||
            baseType.StartsWith("ICollection<") ||
            baseType.StartsWith("IEnumerable<"))
        {
            return true; // Collections are supported
        }

        return supportedTypes.Contains(baseType);
    }

    private AttributeSyntax? GetAttribute(SyntaxNode node, SemanticModel semanticModel, string attributeName)
    {
        return GetAttributes(node, semanticModel, attributeName).FirstOrDefault();
    }

    private IEnumerable<AttributeSyntax> GetAttributes(SyntaxNode node, SemanticModel semanticModel, string attributeName)
    {
        var attributeLists = node switch
        {
            ClassDeclarationSyntax classDecl => classDecl.AttributeLists,
            PropertyDeclarationSyntax propDecl => propDecl.AttributeLists,
            _ => default
        };

        if (attributeLists.Count == 0)
            return Enumerable.Empty<AttributeSyntax>();

        return attributeLists
            .SelectMany(al => al.Attributes)
            .Where(attr => 
            {
                // First try semantic model resolution
                var symbolInfo = semanticModel.GetSymbolInfo(attr);
                if (symbolInfo.Symbol is IMethodSymbol method)
                {
                    var containingType = method.ContainingType.ToDisplayString();
                    if (containingType.EndsWith(attributeName) || 
                        containingType.EndsWith(attributeName.Replace("Attribute", "")))
                    {
                        return true;
                    }
                }

                // Fallback to syntax-based matching for cases where semantic model can't resolve
                var attributeNameText = attr.Name.ToString();
                var targetName = attributeName.Replace("Attribute", "");
                
                return attributeNameText == attributeName ||
                       attributeNameText == targetName ||
                       attributeNameText.EndsWith("." + attributeName) ||
                       attributeNameText.EndsWith("." + targetName);
            });
    }

    private void ValidateMultiItemEntityConsistency(EntityModel entityModel)
    {
        // Multi-item entities must have a partition key for grouping
        if (entityModel.PartitionKeyProperty == null)
        {
            ReportDiagnostic(DiagnosticDescriptors.MultiItemEntityMissingPartitionKey, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
            return;
        }

        // Multi-item entities should have a sort key for item ordering
        if (entityModel.SortKeyProperty == null)
        {
            ReportDiagnostic(DiagnosticDescriptors.MultiItemEntityMissingSortKey, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
        }

        // Validate that collection properties have appropriate attribute mappings
        var collectionProperties = entityModel.Properties.Where(p => p.IsCollection && p.HasAttributeMapping).ToArray();
        
        foreach (var collectionProperty in collectionProperties)
        {
            // Collection properties in multi-item entities should not conflict with key attributes
            if (collectionProperty.IsPartitionKey || collectionProperty.IsSortKey)
            {
                ReportDiagnostic(DiagnosticDescriptors.CollectionPropertyCannotBeKey, 
                    collectionProperty.PropertyDeclaration?.Identifier.GetLocation(), 
                    collectionProperty.PropertyName, entityModel.ClassName);
            }
        }

        // Ensure partition key generation is consistent
        ValidatePartitionKeyGeneration(entityModel);
    }

    private void ValidatePartitionKeyGeneration(EntityModel entityModel)
    {
        var partitionKeyProperty = entityModel.PartitionKeyProperty;
        if (partitionKeyProperty?.KeyFormat != null)
        {
            // If partition key has a format, ensure it's suitable for multi-item entities
            var keyFormat = partitionKeyProperty.KeyFormat;
            
            // Warn if partition key format might not be suitable for grouping
            if (string.IsNullOrEmpty(keyFormat.Prefix) && string.IsNullOrEmpty(keyFormat.Separator))
            {
                ReportDiagnostic(DiagnosticDescriptors.MultiItemEntityPartitionKeyFormat, 
                    partitionKeyProperty.PropertyDeclaration?.Identifier.GetLocation(), 
                    partitionKeyProperty.PropertyName, entityModel.ClassName);
            }
        }
    }

    private void ValidateRelatedEntityConfiguration(EntityModel entityModel)
    {
        // Check if entity has sort key for pattern matching
        if (entityModel.SortKeyProperty == null)
        {
            ReportDiagnostic(DiagnosticDescriptors.RelatedEntitiesRequireSortKey,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName);
        }

        // Check for conflicting patterns
        var patterns = entityModel.Relationships.Select(r => r.SortKeyPattern).ToArray();
        for (int i = 0; i < patterns.Length; i++)
        {
            for (int j = i + 1; j < patterns.Length; j++)
            {
                if (PatternsConflict(patterns[i], patterns[j]))
                {
                    ReportDiagnostic(DiagnosticDescriptors.ConflictingRelatedEntityPatterns,
                        entityModel.ClassDeclaration?.Identifier.GetLocation(),
                        patterns[i], patterns[j], entityModel.ClassName);
                }
            }
        }

        // Validate each relationship
        foreach (var relationship in entityModel.Relationships)
        {
            ValidateRelationshipModel(relationship, entityModel);
        }
    }

    private void ValidateRelationshipModel(RelationshipModel relationship, EntityModel entityModel)
    {
        // Check for ambiguous patterns
        if (relationship.SortKeyPattern == "*" || string.IsNullOrWhiteSpace(relationship.SortKeyPattern))
        {
            ReportDiagnostic(DiagnosticDescriptors.AmbiguousRelatedEntityPattern,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                relationship.SortKeyPattern, relationship.PropertyName);
        }

        // Validate entity type if specified
        if (!string.IsNullOrEmpty(relationship.EntityType))
        {
            // Basic validation - in a real implementation, we'd check if the type exists
            if (!IsValidEntityType(relationship.EntityType))
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidRelatedEntityType,
                    entityModel.ClassDeclaration?.Identifier.GetLocation(),
                    relationship.PropertyName, relationship.EntityType);
            }
        }
    }

    private bool PatternsConflict(string pattern1, string pattern2)
    {
        // Simple conflict detection - patterns conflict if one is a prefix of another
        if (pattern1 == pattern2)
            return true;

        // Handle wildcard patterns
        var prefix1 = pattern1.Replace("*", "");
        var prefix2 = pattern2.Replace("*", "");

        return prefix1.StartsWith(prefix2) || prefix2.StartsWith(prefix1);
    }

    private bool IsValidEntityType(string entityType)
    {
        // Basic validation - check if it looks like a valid type name
        return !string.IsNullOrWhiteSpace(entityType) && 
               !entityType.Contains(" ") && 
               char.IsUpper(entityType[0]);
    }

    private void ValidateEntityComplexity(EntityModel entityModel)
    {
        // Check for too many attributes
        var attributeCount = entityModel.Properties.Count(p => p.HasAttributeMapping);
        if (attributeCount > 50)
        {
            ReportDiagnostic(DiagnosticDescriptors.TooManyAttributes,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName, attributeCount);
        }

        // Check for complex nested structures
        var complexProperties = entityModel.Properties.Count(p => !IsPrimitiveType(p.PropertyType) && !p.IsCollection);
        if (complexProperties > 10)
        {
            ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName, "Complex nested structure",
                $"Entity has {complexProperties} complex properties which may impact serialization performance");
        }
    }

    private void ValidateEntityScalability(EntityModel entityModel)
    {
        // Check for potential hot partition issues
        var partitionKeyProperty = entityModel.PartitionKeyProperty;
        if (partitionKeyProperty != null)
        {
            var shouldWarn = false;
            var warningReason = "";

            // Only warn for obviously problematic patterns
            if (partitionKeyProperty.KeyFormat != null)
            {
                var keyFormat = partitionKeyProperty.KeyFormat;
                
                // Good: Has prefix or separator indicating composite key structure
                // Note: Default separator is "#", so we need to check if it's meaningful
                var hasCompositeStructure = !string.IsNullOrEmpty(keyFormat.Prefix) || 
                                          (!string.IsNullOrEmpty(keyFormat.Separator) && keyFormat.Separator != "#");
                
                if (!hasCompositeStructure)
                {
                    // Check for obviously sequential patterns
                    var propertyName = partitionKeyProperty.PropertyName.ToLowerInvariant();
                    if (propertyName.Contains("id") && 
                        (propertyName.Contains("sequence") || 
                         propertyName.Contains("counter") || 
                         propertyName.Contains("increment") ||
                         propertyName == "id"))
                    {
                        shouldWarn = true;
                        warningReason = "Sequential ID patterns may create hot partitions";
                    }
                }
            }
            else
            {
                // No key format - check for simple sequential ID patterns
                var propertyName = partitionKeyProperty.PropertyName.ToLowerInvariant();
                var attributeName = partitionKeyProperty.AttributeName?.ToLowerInvariant() ?? "";
                
                if ((propertyName == "id" || attributeName == "id") ||
                    (propertyName.Contains("sequence") || attributeName.Contains("sequence")) ||
                    (propertyName.Contains("counter") || attributeName.Contains("counter")))
                {
                    shouldWarn = true;
                    warningReason = "Simple sequential keys may create hot partitions. Consider composite keys for better distribution";
                }
            }

            if (shouldWarn)
            {
                ReportDiagnostic(DiagnosticDescriptors.ScalabilityWarning,
                    entityModel.ClassDeclaration?.Identifier.GetLocation(),
                    entityModel.ClassName,
                    warningReason);
            }
        }

        // Check for GSI overuse (keep this check as it's valid)
        if (entityModel.Indexes.Length > 5)
        {
            ReportDiagnostic(DiagnosticDescriptors.ScalabilityWarning,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName,
                $"Entity has {entityModel.Indexes.Length} GSIs which may impact write performance and costs");
        }
    }

    private void ValidateCircularReferences(EntityModel entityModel)
    {
        // Basic circular reference detection for related entities
        var entityTypeName = entityModel.ClassName;
        
        foreach (var relationship in entityModel.Relationships)
        {
            if (!string.IsNullOrEmpty(relationship.EntityType))
            {
                // Check if related entity references back to this entity
                if (relationship.EntityType == entityTypeName)
                {
                    ReportDiagnostic(DiagnosticDescriptors.CircularReferenceDetected,
                        entityModel.ClassDeclaration?.Identifier.GetLocation(),
                        entityModel.ClassName);
                    break;
                }
            }
        }

        // Check for self-referencing collection properties
        foreach (var property in entityModel.Properties.Where(p => p.IsCollection))
        {
            var elementType = GetCollectionElementType(property.PropertyType);
            if (elementType == entityTypeName)
            {
                ReportDiagnostic(DiagnosticDescriptors.CircularReferenceDetected,
                    entityModel.ClassDeclaration?.Identifier.GetLocation(),
                    entityModel.ClassName);
                break;
            }
        }
    }

    private void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, location ?? Location.None, messageArgs);
        _diagnostics.Add(diagnostic);
    }
}