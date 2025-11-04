using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates static field name constant classes for DynamoDB entities.
/// </summary>
internal static class FieldsGenerator
{
    /// <summary>
    /// Generates a nested static Fields class within an entity class.
    /// This method writes the Fields class directly to an existing StringBuilder,
    /// allowing it to be nested within the entity's partial class definition.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the nested Fields class to.</param>
    /// <param name="entity">The entity model to generate fields for.</param>
    public static void GenerateNestedFieldsClass(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Field name constants for {entity.ClassName} DynamoDB attributes.");
        sb.AppendLine("        /// These constants provide compile-time safety when referencing DynamoDB attribute names");
        sb.AppendLine("        /// in queries, expressions, projections, and other operations.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <remarks>");
        sb.AppendLine($"        /// <para>Generated from entity: {entity.ClassName}</para>");
        sb.AppendLine($"        /// <para>Target table: {entity.TableName}</para>");
        sb.AppendLine($"        /// <para>Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}</para>");
        sb.AppendLine("        /// ");
        sb.AppendLine("        /// Using field constants instead of string literals provides:");
        sb.AppendLine("        /// - Compile-time checking of field names");
        sb.AppendLine("        /// - IntelliSense support for discovering available fields");
        sb.AppendLine("        /// - Refactoring safety when attribute names change");
        sb.AppendLine("        /// - Prevention of typos in attribute name references");
        sb.AppendLine("        /// </remarks>");
        sb.AppendLine("        /// <example>");
        sb.AppendLine("        /// <code>");
        sb.AppendLine($"        /// // Use in projection expressions");
        sb.AppendLine($"        /// var query = table.Query()");
        sb.AppendLine($"        ///     .WithProjection({entity.ClassName}.Fields.Id, {entity.ClassName}.Fields.Name);");
        sb.AppendLine("        /// ");
        sb.AppendLine($"        /// // Use in filter expressions");
        sb.AppendLine($"        /// var query = table.Query()");
        sb.AppendLine($"        ///     .WithFilter($\"{{{{{entity.ClassName}.Fields.Status}}}} = {{{{0}}}}\", \"ACTIVE\");");
        sb.AppendLine("        /// ");
        sb.AppendLine($"        /// // Use in update expressions");
        sb.AppendLine($"        /// var update = table.Update(id)");
        sb.AppendLine($"        ///     .Set($\"SET {{{{{entity.ClassName}.Fields.UpdatedAt}}}} = {{{{0}}}}\", DateTime.UtcNow);");
        sb.AppendLine("        /// </code>");
        sb.AppendLine("        /// </example>");
        sb.AppendLine("        public static partial class Fields");
        sb.AppendLine("        {");

        // Generate main field constants
        GenerateMainFieldConstants(sb, entity, indentLevel: 3);

        // Generate GSI field classes
        GenerateGsiFieldClasses(sb, entity, indentLevel: 3);

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates a static Fields class containing field name constants for the entity.
    /// </summary>
    /// <param name="entity">The entity model to generate fields for.</param>
    /// <returns>The generated C# source code.</returns>
    public static string GenerateFieldsClass(EntityModel entity)
    {
        var sb = new StringBuilder();

        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Field name constants for {entity.ClassName} DynamoDB attributes.");
        sb.AppendLine($"    /// These constants provide compile-time safety when referencing DynamoDB attribute names");
        sb.AppendLine($"    /// in queries, expressions, and other operations.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <remarks>");
        sb.AppendLine($"    /// Generated from entity: {entity.ClassName}");
        sb.AppendLine($"    /// Target table: {entity.TableName}");
        sb.AppendLine($"    /// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine($"    /// </remarks>");
        sb.AppendLine($"    public static partial class {entity.ClassName}Fields");
        sb.AppendLine("    {");

        // Generate main field constants
        GenerateMainFieldConstants(sb, entity, indentLevel: 1);

        // Generate GSI field classes
        GenerateGsiFieldClasses(sb, entity, indentLevel: 1);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the main field constants for entity properties.
    /// </summary>
    /// <param name="indentLevel">The indentation level (1 = 4 spaces, 2 = 8 spaces, etc.)</param>
    private static void GenerateMainFieldConstants(StringBuilder sb, EntityModel entity, int indentLevel = 1)
    {
        var propertiesWithAttributes = entity.Properties
            .Where(p => p.HasAttributeMapping)
            .OrderBy(p => p.PropertyName)
            .ToArray();

        if (propertiesWithAttributes.Length == 0)
            return;

        var indent = new string(' ', indentLevel * 4);

        foreach (var property in propertiesWithAttributes)
        {
            var fieldName = GetSafeFieldName(property.PropertyName);
            var attributeName = property.AttributeName;

            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// DynamoDB attribute name for the {property.PropertyName} property.");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    /// <value>\"{attributeName}\"</value>");
            sb.AppendLine($"{indent}    /// <remarks>");
            sb.AppendLine($"{indent}    /// Use this constant instead of hardcoding the attribute name to ensure");
            sb.AppendLine($"{indent}    /// consistency and enable refactoring support across your codebase.");
            sb.AppendLine($"{indent}    /// </remarks>");
            sb.AppendLine($"{indent}    public const string {fieldName} = \"{attributeName}\";");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Generates nested GSI field classes for Global Secondary Index attributes.
    /// </summary>
    /// <param name="indentLevel">The indentation level (1 = 4 spaces, 2 = 8 spaces, etc.)</param>
    private static void GenerateGsiFieldClasses(StringBuilder sb, EntityModel entity, int indentLevel = 1)
    {
        if (entity.Indexes.Length == 0)
            return;

        foreach (var index in entity.Indexes.OrderBy(i => i.IndexName))
        {
            GenerateGsiFieldClass(sb, entity, index, indentLevel);
        }
    }

    /// <summary>
    /// Generates a nested field class for a specific GSI.
    /// </summary>
    /// <param name="indentLevel">The indentation level (1 = 4 spaces, 2 = 8 spaces, etc.)</param>
    private static void GenerateGsiFieldClass(StringBuilder sb, EntityModel entity, IndexModel index, int indentLevel = 1)
    {
        // Remove "Fields" suffix for nested classes - just use the index name
        var className = GetSafeClassName(index.IndexName);
        var indent = new string(' ', indentLevel * 4);

        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// Field name constants for {index.IndexName} Global Secondary Index.");
        sb.AppendLine($"{indent}    /// Provides attribute name constants for fields projected into this GSI.");
        sb.AppendLine($"{indent}    /// </summary>");
        sb.AppendLine($"{indent}    /// <remarks>");
        sb.AppendLine($"{indent}    /// This nested class contains field constants specifically for the {index.IndexName} GSI.");
        sb.AppendLine($"{indent}    /// Use these constants when building queries, filters, or projections against this index.");
        sb.AppendLine($"{indent}    /// Only fields that are projected into this GSI are included.");
        sb.AppendLine($"{indent}    /// </remarks>");
        sb.AppendLine($"{indent}    /// <example>");
        sb.AppendLine($"{indent}    /// <code>");
        sb.AppendLine($"{indent}    /// // Query GSI with field constants");
        sb.AppendLine($"{indent}    /// var results = await table.{index.IndexName}.Query()");
        sb.AppendLine($"{indent}    ///     .WithFilter($\"{{{{{entity.ClassName}.Fields.{className}.PartitionKey}}}} = {{{{0}}}}\", value)");
        sb.AppendLine($"{indent}    ///     .ToListAsync();");
        sb.AppendLine($"{indent}    /// </code>");
        sb.AppendLine($"{indent}    /// </example>");
        sb.AppendLine($"{indent}    public static partial class {className}");
        sb.AppendLine($"{indent}    {{");

        // Generate partition key field
        var partitionKeyProperty = entity.Properties.FirstOrDefault(p => p.PropertyName == index.PartitionKeyProperty);
        if (partitionKeyProperty != null)
        {
            var fieldName = GetSafeFieldName("PartitionKey");
            sb.AppendLine($"{indent}        /// <summary>");
            sb.AppendLine($"{indent}        /// Partition key attribute name for {index.IndexName} GSI.");
            sb.AppendLine($"{indent}        /// Maps to the {partitionKeyProperty.PropertyName} property.");
            sb.AppendLine($"{indent}        /// </summary>");
            sb.AppendLine($"{indent}        /// <value>\"{partitionKeyProperty.AttributeName}\"</value>");
            sb.AppendLine($"{indent}        public const string {fieldName} = \"{partitionKeyProperty.AttributeName}\";");
            sb.AppendLine();
        }

        // Generate sort key field if exists
        if (!string.IsNullOrEmpty(index.SortKeyProperty))
        {
            var sortKeyProperty = entity.Properties.FirstOrDefault(p => p.PropertyName == index.SortKeyProperty);
            if (sortKeyProperty != null)
            {
                var fieldName = GetSafeFieldName("SortKey");
                sb.AppendLine($"{indent}        /// <summary>");
                sb.AppendLine($"{indent}        /// Sort key attribute name for {index.IndexName} GSI.");
                sb.AppendLine($"{indent}        /// Maps to the {sortKeyProperty.PropertyName} property.");
                sb.AppendLine($"{indent}        /// </summary>");
                sb.AppendLine($"{indent}        /// <value>\"{sortKeyProperty.AttributeName}\"</value>");
                sb.AppendLine($"{indent}        public const string {fieldName} = \"{sortKeyProperty.AttributeName}\";");
                sb.AppendLine();
            }
        }

        // Generate projected field constants for properties that are part of this GSI
        GenerateGsiProjectedFields(sb, entity, index, indentLevel);

        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates field constants for properties projected in a GSI.
    /// </summary>
    /// <param name="indentLevel">The indentation level (1 = 4 spaces, 2 = 8 spaces, etc.)</param>
    private static void GenerateGsiProjectedFields(StringBuilder sb, EntityModel entity, IndexModel index, int indentLevel = 1)
    {
        // Find all properties that have GSI attributes for this index (excluding key properties already handled)
        var projectedProperties = entity.Properties
            .Where(p => p.GlobalSecondaryIndexes.Any(gsi => gsi.IndexName == index.IndexName) &&
                       p.PropertyName != index.PartitionKeyProperty &&
                       p.PropertyName != index.SortKeyProperty &&
                       p.HasAttributeMapping)
            .OrderBy(p => p.PropertyName)
            .ToArray();

        var indent = new string(' ', indentLevel * 4);

        foreach (var property in projectedProperties)
        {
            var fieldName = GetSafeFieldName(property.PropertyName);
            sb.AppendLine($"{indent}        /// <summary>");
            sb.AppendLine($"{indent}        /// DynamoDB attribute name for the {property.PropertyName} property in {index.IndexName} GSI.");
            sb.AppendLine($"{indent}        /// This field is projected into the Global Secondary Index.");
            sb.AppendLine($"{indent}        /// </summary>");
            sb.AppendLine($"{indent}        /// <value>\"{property.AttributeName}\"</value>");
            sb.AppendLine($"{indent}        public const string {fieldName} = \"{property.AttributeName}\";");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Converts a property name to a safe field name, handling reserved words and special cases.
    /// </summary>
    private static string GetSafeFieldName(string propertyName)
    {
        // Handle reserved C# keywords by prefixing with @
        var csharpReservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        // DynamoDB reserved words that should also be escaped for clarity
        var dynamoDbReservedWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "ABORT", "ABSOLUTE", "ACTION", "ADD", "AFTER", "AGENT", "AGGREGATE", "ALL", "ALLOCATE", "ALTER",
            "ANALYZE", "AND", "ANY", "ARCHIVE", "ARE", "ARRAY", "AS", "ASC", "ASCII", "ASENSITIVE",
            "ASSERTION", "ASYMMETRIC", "AT", "ATOMIC", "ATTACH", "ATTRIBUTE", "AUTH", "AUTHORIZATION", "AUTHORIZE", "AUTO",
            "AVG", "BACK", "BACKUP", "BASE", "BATCH", "BEFORE", "BEGIN", "BETWEEN", "BIGINT", "BINARY",
            "BIT", "BLOB", "BLOCK", "BOOLEAN", "BOTH", "BREADTH", "BUCKET", "BULK", "BY", "BYTE",
            "CALL", "CALLED", "CALLING", "CAPACITY", "CASCADE", "CASCADED", "CASE", "CAST", "CATALOG", "CHAR",
            "CHARACTER", "CHECK", "CLASS", "CLOB", "CLOSE", "CLUSTER", "CLUSTERED", "CLUSTERING", "CLUSTERS", "COALESCE",
            "COLLATE", "COLLATION", "COLLECTION", "COLUMN", "COLUMNS", "COMBINE", "COMMENT", "COMMIT", "COMPACT", "COMPILE",
            "COMPRESS", "CONDITION", "CONFLICT", "CONNECT", "CONNECTION", "CONSISTENCY", "CONSISTENT", "CONSTRAINT", "CONSTRAINTS", "CONSTRUCTOR",
            "CONSUMED", "CONTINUE", "CONVERT", "COPY", "CORRESPONDING", "COUNT", "COUNTER", "CREATE", "CROSS", "CUBE",
            "CURRENT", "CURSOR", "CYCLE", "DATA", "DATABASE", "DATE", "DATETIME", "DAY", "DEALLOCATE", "DEC",
            "DECIMAL", "DECLARE", "DEFAULT", "DEFERRABLE", "DEFERRED", "DEFINE", "DEFINED", "DEFINITION", "DELETE", "DELIMITED",
            "DEPTH", "DEREF", "DESC", "DESCRIBE", "DESCRIPTOR", "DETACH", "DETERMINISTIC", "DIAGNOSTICS", "DIRECTORIES", "DISABLE",
            "DISCONNECT", "DISTINCT", "DISTRIBUTE", "DO", "DOMAIN", "DOUBLE", "DROP", "DUMP", "DURATION", "DYNAMIC",
            "EACH", "ELEMENT", "ELSE", "ELSEIF", "EMPTY", "ENABLE", "END", "EQUAL", "EQUALS", "ERROR",
            "ESCAPE", "ESCAPED", "EVAL", "EVALUATE", "EXCEEDED", "EXCEPT", "EXCEPTION", "EXCEPTIONS", "EXCLUSIVE", "EXEC",
            "EXECUTE", "EXISTS", "EXIT", "EXPLAIN", "EXPLODE", "EXPORT", "EXPRESSION", "EXTENDED", "EXTERNAL", "EXTRACT",
            "FAIL", "FALSE", "FAMILY", "FETCH", "FIELDS", "FILE", "FILTER", "FILTERING", "FINAL", "FINISH",
            "FIRST", "FIXED", "FLATTERN", "FLOAT", "FOR", "FORCE", "FOREIGN", "FORMAT", "FORWARD", "FOUND",
            "FREE", "FROM", "FULL", "FUNCTION", "FUNCTIONS", "GENERAL", "GENERATE", "GET", "GLOB", "GLOBAL",
            "GO", "GOTO", "GRANT", "GREATER", "GROUP", "GROUPING", "HANDLER", "HASH", "HAVE", "HAVING",
            "HEAP", "HIDDEN", "HOLD", "HOUR", "IDENTIFIED", "IDENTITY", "IF", "IGNORE", "IMMEDIATE", "IMPORT",
            "IN", "INCLUDING", "INCLUSIVE", "INCREMENT", "INCREMENTAL", "INDEX", "INDEXED", "INDEXES", "INDICATOR", "INFINITE",
            "INITIALLY", "INLINE", "INNER", "INNTER", "INOUT", "INPUT", "INSENSITIVE", "INSERT", "INSTEAD", "INT",
            "INTEGER", "INTERSECT", "INTERVAL", "INTO", "INVALIDATE", "IS", "ISOLATION", "ITEM", "ITEMS", "ITERATE",
            "JOIN", "KEY", "KEYS", "LAG", "LANGUAGE", "LARGE", "LAST", "LATERAL", "LEAD", "LEADING",
            "LEAVE", "LEFT", "LENGTH", "LESS", "LEVEL", "LIKE", "LIMIT", "LIMITED", "LINES", "LIST",
            "LOAD", "LOCAL", "LOCALTIME", "LOCALTIMESTAMP", "LOCATION", "LOCATOR", "LOCK", "LOCKS", "LOG", "LOGED",
            "LONG", "LOOP", "LOWER", "MAP", "MATCH", "MATERIALIZED", "MAX", "MAXLEN", "MEMBER", "MERGE",
            "METHOD", "METRICS", "MIN", "MINUS", "MINUTE", "MISSING", "MOD", "MODE", "MODIFIES", "MODIFY",
            "MODULE", "MONTH", "MULTI", "MULTISET", "NAME", "NAMES", "NATIONAL", "NATURAL", "NCHAR", "NCLOB",
            "NEW", "NEXT", "NO", "NONE", "NOT", "NULL", "NULLIF", "NUMBER", "NUMERIC", "OBJECT",
            "OF", "OFFLINE", "OFFSET", "OLD", "ON", "ONLINE", "ONLY", "OPAQUE", "OPEN", "OPERATOR",
            "OPTION", "OR", "ORDER", "ORDINALITY", "OTHER", "OTHERS", "OUT", "OUTER", "OUTPUT", "OVER",
            "OVERLAPS", "OVERRIDE", "OWNER", "PAD", "PARALLEL", "PARAMETER", "PARAMETERS", "PARTIAL", "PARTITION", "PARTITIONED",
            "PARTITIONS", "PATH", "PERCENT", "PERCENTILE", "PERMISSION", "PERMISSIONS", "PIPE", "PIPELINED", "PLAN", "POOL",
            "POSITION", "PRECISION", "PREPARE", "PRESERVE", "PRIMARY", "PRIOR", "PRIVATE", "PRIVILEGES", "PROCEDURE", "PROCESSED",
            "PROJECT", "PROJECTION", "PROPERTY", "PROVISIONING", "PUBLIC", "PUT", "QUERY", "QUIT", "QUORUM", "RAISE",
            "RANDOM", "RANGE", "RANK", "RAW", "READ", "READS", "REAL", "REBUILD", "RECORD", "RECURSIVE",
            "REDUCE", "REF", "REFERENCE", "REFERENCES", "REFERENCING", "REGEXP", "REGION", "REINDEX", "RELATIVE", "RELEASE",
            "REMAINDER", "RENAME", "REPEAT", "REPLACE", "REQUEST", "RESET", "RESIGNAL", "RESOURCE", "RESPONSE", "RESTORE",
            "RESTRICT", "RESULT", "RETURN", "RETURNING", "RETURNS", "REVERSE", "REVOKE", "RIGHT", "ROLE", "ROLES",
            "ROLLBACK", "ROLLUP", "ROUTINE", "ROW", "ROWS", "RULE", "RULES", "SAMPLE", "SATISFIES", "SAVE",
            "SAVEPOINT", "SCAN", "SCHEMA", "SCOPE", "SCROLL", "SEARCH", "SECOND", "SECTION", "SEGMENT", "SEGMENTS",
            "SELECT", "SELF", "SEMI", "SENSITIVE", "SEPARATE", "SEQUENCE", "SERIALIZABLE", "SESSION", "SET", "SETS",
            "SHARD", "SHARE", "SHARED", "SHORT", "SHOW", "SIGNAL", "SIMILAR", "SIZE", "SKEWED", "SMALLINT",
            "SNAPSHOT", "SOME", "SOURCE", "SPACE", "SPACES", "SPARSE", "SPECIFIC", "SPECIFICTYPE", "SPLIT", "SQL",
            "SQLCODE", "SQLERROR", "SQLEXCEPTION", "SQLSTATE", "SQLWARNING", "START", "STATE", "STATIC", "STATUS", "STORAGE",
            "STORE", "STORED", "STREAM", "STRING", "STRUCT", "STYLE", "SUB", "SUBMULTISET", "SUBPARTITION", "SUBSTRING",
            "SUBTYPE", "SUM", "SUPER", "SYMMETRIC", "SYNONYM", "SYSTEM", "TABLE", "TABLESAMPLE", "TEMP", "TEMPORARY",
            "TERMINATED", "TEXT", "THAN", "THEN", "THROUGHPUT", "TIME", "TIMESTAMP", "TIMEZONE", "TINYINT", "TO",
            "TOKEN", "TOTAL", "TOUCH", "TRAILING", "TRANSACTION", "TRANSFORM", "TRANSLATE", "TRANSLATION", "TREAT", "TRIGGER",
            "TRIM", "TRUE", "TRUNCATE", "TTL", "TUPLE", "TYPE", "UNDER", "UNDO", "UNION", "UNIQUE",
            "UNIT", "UNKNOWN", "UNLOGGED", "UNNEST", "UNPROCESSED", "UNSIGNED", "UNTIL", "UPDATE", "UPPER", "URL",
            "USAGE", "USE", "USER", "USERS", "USING", "UUID", "VACUUM", "VALUE", "VALUED", "VALUES",
            "VARCHAR", "VARIABLE", "VARIANCE", "VARINT", "VARYING", "VIEW", "VIEWS", "VIRTUAL", "VOID", "WAIT",
            "WHEN", "WHENEVER", "WHERE", "WHILE", "WINDOW", "WITH", "WITHIN", "WITHOUT", "WORK", "WRAPPED",
            "WRITE", "YEAR", "ZONE"
        };

        // Escape both C# and DynamoDB reserved words
        if (csharpReservedWords.Contains(propertyName) || dynamoDbReservedWords.Contains(propertyName))
        {
            return $"@{propertyName}";
        }

        // Ensure the field name starts with a letter or underscore
        if (!char.IsLetter(propertyName[0]) && propertyName[0] != '_')
        {
            return $"_{propertyName}";
        }

        return propertyName;
    }

    /// <summary>
    /// Converts an index name to a safe class name.
    /// </summary>
    private static string GetSafeClassName(string indexName)
    {
        // Remove invalid characters and ensure it starts with a letter or underscore
        var safeName = new StringBuilder();

        for (int i = 0; i < indexName.Length; i++)
        {
            char c = indexName[i];

            if (i == 0)
            {
                // First character must be letter or underscore
                if (char.IsLetter(c) || c == '_')
                {
                    safeName.Append(c);
                }
                else if (char.IsDigit(c))
                {
                    safeName.Append('_').Append(c);
                }
                else
                {
                    safeName.Append('_');
                }
            }
            else
            {
                // Subsequent characters can be letters, digits, or underscores
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    safeName.Append(c);
                }
                else
                {
                    safeName.Append('_');
                }
            }
        }

        var result = safeName.ToString();

        // Ensure we don't have an empty result
        if (string.IsNullOrEmpty(result))
        {
            result = "_Index";
        }

        return result;
    }
}