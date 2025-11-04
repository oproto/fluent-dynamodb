using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates table class implementations with method-based builder access.
/// </summary>
internal static class TableGenerator
{
    /// <summary>
    /// Generates a table class implementation for multiple entities sharing the same table.
    /// This is the new multi-entity table generation approach.
    /// </summary>
    /// <param name="tableName">The DynamoDB table name.</param>
    /// <param name="entities">List of entities that share this table.</param>
    /// <returns>The generated table class code.</returns>
    public static string GenerateTableClass(string tableName, List<EntityModel> entities)
    {
        if (entities == null || entities.Count == 0)
        {
            return string.Empty;
        }

        // Use the table name to generate the table class name
        var tableClassName = GetTableClassName(tableName);
        
        // Determine the default entity (single entity or marked as default)
        var defaultEntity = entities.Count == 1 
            ? entities[0] 
            : entities.FirstOrDefault(e => e.IsDefault);
        
        var sb = new StringBuilder();
        
        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        
        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using Amazon.DynamoDBv2;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Logging;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests.Extensions;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
        sb.AppendLine();
        
        // Namespace (use the first entity's namespace)
        var primaryEntity = entities[0];
        sb.AppendLine($"namespace {primaryEntity.Namespace};");
        sb.AppendLine();
        
        // Class declaration
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated table class for {tableName} table.");
        sb.AppendLine($"/// Provides method-based access to DynamoDB operations.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public partial class {tableClassName} : DynamoDbTableBase");
        sb.AppendLine("{");
        
        // Generate entity accessor properties
        GenerateEntityAccessorProperties(sb, entities);
        
        // Constructors
        GenerateMultiEntityConstructors(sb, tableName, tableClassName, entities);
        
        // Table-level operations (if default entity exists)
        if (defaultEntity != null)
        {
            GenerateTableLevelOperations(sb, defaultEntity);
        }
        
        // Index properties (from default entity or first entity)
        var entityForIndexes = defaultEntity ?? primaryEntity;
        GenerateIndexProperties(sb, entityForIndexes, tableClassName);
        
        // Transaction and batch operations (always at table level)
        GenerateTransactionAndBatchOperations(sb);
        
        // Generate nested entity accessor classes
        GenerateEntityAccessorClasses(sb, entities);
        
        // Generate typed index classes as nested classes (only for indexes with projections)
        foreach (var index in entityForIndexes.Indexes)
        {
            var projectionType = GetProjectionTypeForIndex(entityForIndexes, index);
            if (projectionType != null)
            {
                sb.AppendLine();
                GenerateTypedIndexClass(sb, entityForIndexes, index, tableClassName);
            }
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates a table class implementation for an entity.
    /// </summary>
    /// <param name="entity">The entity model to generate a table for.</param>
    /// <param name="tableClassName">Optional custom table class name. If not provided, uses entity name.</param>
    /// <returns>The generated table class code.</returns>
    public static string GenerateTableClass(EntityModel entity, string? tableClassName = null)
    {
        // Skip generation for nested entities (DynamoDbEntity)
        var isNestedEntity = entity.TableName?.StartsWith("_entity_") == true;
        if (isNestedEntity)
        {
            return string.Empty;
        }

        // Use provided table class name or derive from table name
        var className = tableClassName ?? GetTableClassName(entity.TableName ?? entity.ClassName);

        var sb = new StringBuilder();
        
        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        
        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using Amazon.DynamoDBv2;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Logging;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests.Extensions;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {entity.Namespace};");
        sb.AppendLine();
        
        // Class declaration
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated table class for {entity.ClassName} entity.");
        sb.AppendLine($"/// Provides method-based access to DynamoDB operations.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public partial class {className} : DynamoDbTableBase");
        sb.AppendLine("{");
        
        // Constructors
        GenerateConstructors(sb, entity, className);
        
        // Query methods
        GenerateQueryMethods(sb, entity);
        
        // Put method
        GeneratePutMethod(sb, entity);
        
        // Get/Update/Delete overloads based on key structure
        GenerateOperationOverloads(sb, entity);
        
        // Scan methods if table is scannable
        GenerateScanMethods(sb, entity);
        
        // Index properties
        GenerateIndexProperties(sb, entity, className);
        
        // Transaction and batch operations (always at table level)
        GenerateTransactionAndBatchOperations(sb);
        
        // Generate typed index classes as nested classes (only for indexes with projections)
        foreach (var index in entity.Indexes)
        {
            var projectionType = GetProjectionTypeForIndex(entity, index);
            if (projectionType != null)
            {
                sb.AppendLine();
                GenerateTypedIndexClass(sb, entity, index, className);
            }
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates entity accessor properties for multi-entity tables.
    /// </summary>
    private static void GenerateEntityAccessorProperties(StringBuilder sb, List<EntityModel> entities)
    {
        foreach (var entity in entities)
        {
            // Skip if Generate is false
            if (!entity.EntityPropertyConfig.Generate)
            {
                continue;
            }
            
            var propertyName = GetEntityPropertyName(entity);
            var accessorClassName = $"{entity.ClassName}Accessor";
            var modifier = GetModifierString(entity.EntityPropertyConfig.Modifier);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Accessor for {entity.ClassName} entity operations.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    {modifier} {accessorClassName} {propertyName} {{ get; }}");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Gets the entity property name, using custom name if specified or pluralizing the entity class name.
    /// </summary>
    private static string GetEntityPropertyName(EntityModel entity)
    {
        // Use custom name if specified
        if (!string.IsNullOrEmpty(entity.EntityPropertyConfig.Name))
        {
            return entity.EntityPropertyConfig.Name;
        }
        
        // Otherwise, pluralize entity class name (simple "add s" rule)
        return entity.ClassName + "s";
    }
    
    /// <summary>
    /// Gets the C# modifier string from AccessModifier enum.
    /// </summary>
    private static string GetModifierString(AccessModifier modifier)
    {
        return modifier switch
        {
            AccessModifier.Public => "public",
            AccessModifier.Internal => "internal",
            AccessModifier.Protected => "protected",
            AccessModifier.Private => "private",
            _ => "public"
        };
    }
    
    /// <summary>
    /// Generates constructors for multi-entity tables.
    /// </summary>
    private static void GenerateMultiEntityConstructors(StringBuilder sb, string tableName, string className, List<EntityModel> entities)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {className}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client.</param>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    public {className}(IAmazonDynamoDB client, string tableName)");
        sb.AppendLine($"        : base(client, tableName)");
        sb.AppendLine($"    {{");
        
        // Initialize entity accessor properties
        foreach (var entity in entities)
        {
            if (!entity.EntityPropertyConfig.Generate)
            {
                continue;
            }
            
            var propertyName = GetEntityPropertyName(entity);
            var accessorClassName = $"{entity.ClassName}Accessor";
            sb.AppendLine($"        {propertyName} = new {accessorClassName}(this);");
        }
        
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {className} with a logger.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client.</param>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    /// <param name=\"logger\">Logger for DynamoDB operations.</param>");
        sb.AppendLine($"    public {className}(IAmazonDynamoDB client, string tableName, IDynamoDbLogger logger)");
        sb.AppendLine($"        : base(client, tableName, logger)");
        sb.AppendLine($"    {{");
        
        // Initialize entity accessor properties
        foreach (var entity in entities)
        {
            if (!entity.EntityPropertyConfig.Generate)
            {
                continue;
            }
            
            var propertyName = GetEntityPropertyName(entity);
            var accessorClassName = $"{entity.ClassName}Accessor";
            sb.AppendLine($"        {propertyName} = new {accessorClassName}(this);");
        }
        
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates nested entity accessor classes for multi-entity tables.
    /// Each accessor class provides entity-specific operation methods.
    /// </summary>
    private static void GenerateEntityAccessorClasses(StringBuilder sb, List<EntityModel> entities)
    {
        foreach (var entity in entities)
        {
            // Skip if Generate is false
            if (!entity.EntityPropertyConfig.Generate)
            {
                continue;
            }
            
            var accessorClassName = $"{entity.ClassName}Accessor";
            
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Nested accessor class for {entity.ClassName} entity operations.");
            sb.AppendLine($"    /// Provides entity-specific DynamoDB operation methods.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {accessorClassName}");
            sb.AppendLine($"    {{");
            
            // Private readonly field for parent table reference
            sb.AppendLine($"        private readonly {GetTableClassName(entity.TableName)} _table;");
            sb.AppendLine();
            
            // Internal constructor accepting parent table
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Initializes a new instance of the {accessorClassName}.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"table\">The parent table instance.</param>");
            sb.AppendLine($"        internal {accessorClassName}({GetTableClassName(entity.TableName)} table)");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            _table = table;");
            sb.AppendLine($"        }}");
            sb.AppendLine();
            
            // Generate operation methods based on AccessorConfig list
            GenerateAccessorOperationMethods(sb, entity);
            
            sb.AppendLine($"    }}");
        }
    }
    
    /// <summary>
    /// Generates operation methods for an entity accessor based on its AccessorConfig list.
    /// </summary>
    private static void GenerateAccessorOperationMethods(StringBuilder sb, EntityModel entity)
    {
        // Determine which operations to generate and their modifiers
        var operationsToGenerate = GetOperationsToGenerate(entity);
        
        foreach (var (operation, modifier) in operationsToGenerate)
        {
            var modifierStr = GetModifierString(modifier);
            
            switch (operation)
            {
                case TableOperation.Query:
                    GenerateAccessorQueryMethods(sb, entity, modifierStr);
                    break;
                    
                case TableOperation.Put:
                    GenerateAccessorPutMethod(sb, entity, modifierStr);
                    break;
                    
                case TableOperation.Get:
                    GenerateAccessorGetMethod(sb, entity, modifierStr);
                    break;
                    
                case TableOperation.Update:
                    GenerateAccessorUpdateMethod(sb, entity, modifierStr);
                    break;
                    
                case TableOperation.Delete:
                    GenerateAccessorDeleteMethod(sb, entity, modifierStr);
                    break;
                    
                case TableOperation.Scan:
                    if (entity.IsScannable)
                    {
                        GenerateAccessorScanMethods(sb, entity, modifierStr);
                    }
                    break;
            }
        }
    }
    
    /// <summary>
    /// Determines which operations to generate based on the entity's AccessorConfig list.
    /// Returns a list of operations with their visibility modifiers.
    /// </summary>
    private static List<(TableOperation, AccessModifier)> GetOperationsToGenerate(EntityModel entity)
    {
        // Default: all operations are public
        var defaultOps = new Dictionary<TableOperation, AccessModifier>
        {
            [TableOperation.Get] = AccessModifier.Public,
            [TableOperation.Query] = AccessModifier.Public,
            [TableOperation.Scan] = AccessModifier.Public,
            [TableOperation.Put] = AccessModifier.Public,
            [TableOperation.Delete] = AccessModifier.Public,
            [TableOperation.Update] = AccessModifier.Public,
        };
        
        // Apply [GenerateAccessors] configurations
        foreach (var config in entity.AccessorConfigs)
        {
            var operations = ExpandOperationFlags(config.Operations);
            
            foreach (var op in operations)
            {
                if (!config.Generate)
                {
                    // Remove operation if Generate = false
                    defaultOps.Remove(op);
                }
                else
                {
                    // Update modifier
                    defaultOps[op] = config.Modifier;
                }
            }
        }
        
        return defaultOps.Select(kvp => (kvp.Key, kvp.Value)).ToList();
    }
    
    /// <summary>
    /// Expands TableOperation flags into individual operations.
    /// </summary>
    private static List<TableOperation> ExpandOperationFlags(TableOperation operations)
    {
        var result = new List<TableOperation>();
        
        if (operations.HasFlag(TableOperation.Get))
            result.Add(TableOperation.Get);
        
        if (operations.HasFlag(TableOperation.Query))
            result.Add(TableOperation.Query);
        
        if (operations.HasFlag(TableOperation.Scan))
            result.Add(TableOperation.Scan);
        
        if (operations.HasFlag(TableOperation.Put))
            result.Add(TableOperation.Put);
        
        if (operations.HasFlag(TableOperation.Delete))
            result.Add(TableOperation.Delete);
        
        if (operations.HasFlag(TableOperation.Update))
            result.Add(TableOperation.Update);
        
        return result;
    }
    
    /// <summary>
    /// Generates Query methods for an entity accessor.
    /// </summary>
    private static void GenerateAccessorQueryMethods(StringBuilder sb, EntityModel entity, string modifier)
    {
        // Parameterless Query() method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder for {entity.ClassName}.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"        {modifier} QueryRequestBuilder<{entity.ClassName}> Query() =>");
        sb.AppendLine($"            _table.Query<{entity.ClassName}>();");
        sb.AppendLine();
        
        // Expression-based Query(string, params object[]) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with a key condition expression.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"keyConditionExpression\">The key condition expression with format placeholders.</param>");
        sb.AppendLine($"        /// <param name=\"values\">The values to substitute into the expression.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with the key condition.</returns>");
        sb.AppendLine($"        {modifier} QueryRequestBuilder<{entity.ClassName}> Query(string keyConditionExpression, params object[] values) =>");
        sb.AppendLine($"            _table.Query<{entity.ClassName}>(keyConditionExpression, values);");
        sb.AppendLine();
        
        // LINQ expression Query(Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with a LINQ expression for the key condition.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with the key condition.</returns>");
        sb.AppendLine($"        {modifier} QueryRequestBuilder<{entity.ClassName}> Query(Expression<Func<{entity.ClassName}, bool>> keyCondition)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query().Where(keyCondition);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // LINQ expression Query(Expression<Func<TEntity, bool>>, Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with LINQ expressions for both key condition and filter.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <param name=\"filterCondition\">The LINQ expression representing the filter condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with both key condition and filter.</returns>");
        sb.AppendLine($"        {modifier} QueryRequestBuilder<{entity.ClassName}> Query(");
        sb.AppendLine($"            Expression<Func<{entity.ClassName}, bool>> keyCondition,");
        sb.AppendLine($"            Expression<Func<{entity.ClassName}, bool>> filterCondition)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query().Where(keyCondition).WithFilter(filterCondition);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates Put method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorPutMethod(StringBuilder sb, EntityModel entity, string modifier)
    {
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new PutItem operation builder for {entity.ClassName}.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"        {modifier} PutItemRequestBuilder<{entity.ClassName}> Put() =>");
        sb.AppendLine($"            _table.Put<{entity.ClassName}>();");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates Get method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorGetMethod(StringBuilder sb, EntityModel entity, string modifier)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetCSharpType(partitionKey.PropertyType);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            sb.AppendLine($"        {modifier} GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {paramName}) =>");
            sb.AppendLine($"            _table.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetCSharpType(sortKey.PropertyType);
            var pkParamName = ToCamelCase(pkAttributeName);
            var skParamName = ToCamelCase(skAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            sb.AppendLine($"        /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            sb.AppendLine($"        {modifier} GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
            sb.AppendLine($"            _table.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates Update method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorUpdateMethod(StringBuilder sb, EntityModel entity, string modifier)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetCSharpType(partitionKey.PropertyType);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <returns>An UpdateItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            sb.AppendLine($"        {modifier} UpdateItemRequestBuilder<{entity.ClassName}> Update({pkPropertyType} {paramName}) =>");
            sb.AppendLine($"            _table.Update<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetCSharpType(sortKey.PropertyType);
            var pkParamName = ToCamelCase(pkAttributeName);
            var skParamName = ToCamelCase(skAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            sb.AppendLine($"        /// <returns>An UpdateItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            sb.AppendLine($"        {modifier} UpdateItemRequestBuilder<{entity.ClassName}> Update({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
            sb.AppendLine($"            _table.Update<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates Delete method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorDeleteMethod(StringBuilder sb, EntityModel entity, string modifier)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetCSharpType(partitionKey.PropertyType);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            sb.AppendLine($"        {modifier} DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {paramName}) =>");
            sb.AppendLine($"            _table.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetCSharpType(sortKey.PropertyType);
            var pkParamName = ToCamelCase(pkAttributeName);
            var skParamName = ToCamelCase(skAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            sb.AppendLine($"        /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            sb.AppendLine($"        {modifier} DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
            sb.AppendLine($"            _table.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates Scan methods for an entity accessor.
    /// </summary>
    private static void GenerateAccessorScanMethods(StringBuilder sb, EntityModel entity, string modifier)
    {
        // Parameterless Scan() method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Scan operation builder for {entity.ClassName}.");
        sb.AppendLine($"        /// WARNING: Scan operations can be very expensive.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"        {modifier} ScanRequestBuilder<{entity.ClassName}> Scan() =>");
        sb.AppendLine($"            new ScanRequestBuilder<{entity.ClassName}>(_table.DynamoDbClient).ForTable(_table.Name);");
        sb.AppendLine();
        
        // Expression-based Scan(string, params object[]) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Scan operation builder with a filter expression.");
        sb.AppendLine($"        /// WARNING: Scan operations are expensive.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"filterExpression\">The filter expression with format placeholders.</param>");
        sb.AppendLine($"        /// <param name=\"values\">The values to substitute into the expression.</param>");
        sb.AppendLine($"        /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured with the filter.</returns>");
        sb.AppendLine($"        {modifier} ScanRequestBuilder<{entity.ClassName}> Scan(string filterExpression, params object[] values)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var builder = Scan();");
        sb.AppendLine($"            return WithFilterExpressionExtensions.WithFilter(builder, filterExpression, values);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // LINQ expression Scan(Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Scan operation builder with a LINQ expression for the filter condition.");
        sb.AppendLine($"        /// WARNING: Scan operations are expensive.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"filterCondition\">The LINQ expression representing the filter condition.</param>");
        sb.AppendLine($"        /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured with the filter.</returns>");
        sb.AppendLine($"        {modifier} ScanRequestBuilder<{entity.ClassName}> Scan(Expression<Func<{entity.ClassName}, bool>> filterCondition)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Scan().WithFilter(filterCondition);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates table-level operations that delegate to the default entity's accessor.
    /// These methods provide convenient access to the default entity's operations directly from the table.
    /// </summary>
    private static void GenerateTableLevelOperations(StringBuilder sb, EntityModel defaultEntity)
    {
        sb.AppendLine($"    // Table-level operations using default entity ({defaultEntity.ClassName})");
        sb.AppendLine();
        
        // Get the entity property name for delegation
        var entityPropertyName = GetEntityPropertyName(defaultEntity);
        
        // Determine which operations to generate based on the default entity's configuration
        var operationsToGenerate = GetOperationsToGenerate(defaultEntity);
        
        foreach (var (operation, modifier) in operationsToGenerate)
        {
            switch (operation)
            {
                case TableOperation.Query:
                    GenerateTableLevelQueryMethods(sb, defaultEntity, entityPropertyName);
                    break;
                    
                case TableOperation.Put:
                    GenerateTableLevelPutMethod(sb, defaultEntity, entityPropertyName);
                    break;
                    
                case TableOperation.Get:
                    GenerateTableLevelGetMethod(sb, defaultEntity, entityPropertyName);
                    break;
                    
                case TableOperation.Update:
                    GenerateTableLevelUpdateMethod(sb, defaultEntity, entityPropertyName);
                    break;
                    
                case TableOperation.Delete:
                    GenerateTableLevelDeleteMethod(sb, defaultEntity, entityPropertyName);
                    break;
                    
                case TableOperation.Scan:
                    if (defaultEntity.IsScannable)
                    {
                        GenerateTableLevelScanMethods(sb, defaultEntity, entityPropertyName);
                    }
                    break;
            }
        }
    }
    
    /// <summary>
    /// Generates table-level Query methods that delegate to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelQueryMethods(StringBuilder sb, EntityModel entity, string entityPropertyName)
    {
        // Parameterless Query() method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Query operation builder for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// Query operations efficiently retrieve items using the primary key and optional sort key conditions.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"    public QueryRequestBuilder<{entity.ClassName}> Query() =>");
        sb.AppendLine($"        {entityPropertyName}.Query();");
        sb.AppendLine();

        // Expression-based Query(string, params object[]) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Query operation builder with a key condition expression for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// Uses format string syntax for parameters: {{0}}, {{1}}, etc.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"keyConditionExpression\">The key condition expression with format placeholders.</param>");
        sb.AppendLine($"    /// <param name=\"values\">The values to substitute into the expression.</param>");
        sb.AppendLine($"    /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with the key condition.</returns>");
        sb.AppendLine($"    public QueryRequestBuilder<{entity.ClassName}> Query(string keyConditionExpression, params object[] values) =>");
        sb.AppendLine($"        {entityPropertyName}.Query(keyConditionExpression, values);");
        sb.AppendLine();

        // LINQ expression Query(Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Query operation builder with a LINQ expression for the key condition for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// Provides type-safe query building with compile-time checking of property access.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"keyCondition\">The LINQ expression representing the key condition (e.g., x => x.PartitionKey == value).</param>");
        sb.AppendLine($"    /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with the key condition.</returns>");
        sb.AppendLine($"    public QueryRequestBuilder<{entity.ClassName}> Query(Expression<Func<{entity.ClassName}, bool>> keyCondition) =>");
        sb.AppendLine($"        {entityPropertyName}.Query(keyCondition);");
        sb.AppendLine();

        // LINQ expression Query(Expression<Func<TEntity, bool>>, Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Query operation builder with LINQ expressions for both key condition and filter for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// Provides type-safe query building with compile-time checking of property access.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"keyCondition\">The LINQ expression representing the key condition (e.g., x => x.PartitionKey == value).</param>");
        sb.AppendLine($"    /// <param name=\"filterCondition\">The LINQ expression representing the filter condition (e.g., x => x.Status == \"ACTIVE\").</param>");
        sb.AppendLine($"    /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with both key condition and filter.</returns>");
        sb.AppendLine($"    public QueryRequestBuilder<{entity.ClassName}> Query(");
        sb.AppendLine($"        Expression<Func<{entity.ClassName}, bool>> keyCondition,");
        sb.AppendLine($"        Expression<Func<{entity.ClassName}, bool>> filterCondition) =>");
        sb.AppendLine($"        {entityPropertyName}.Query(keyCondition, filterCondition);");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates table-level Put method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelPutMethod(StringBuilder sb, EntityModel entity, string entityPropertyName)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new PutItem operation builder for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// PutItem creates a new item or completely replaces an existing item with the same primary key.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"    public PutItemRequestBuilder<{entity.ClassName}> Put() =>");
        sb.AppendLine($"        {entityPropertyName}.Put();");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates table-level Get method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelGetMethod(StringBuilder sb, EntityModel entity, string entityPropertyName)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetCSharpType(partitionKey.PropertyType);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {paramName}) =>");
            sb.AppendLine($"        {entityPropertyName}.Get({paramName});");
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetCSharpType(sortKey.PropertyType);
            var pkParamName = ToCamelCase(pkAttributeName);
            var skParamName = ToCamelCase(skAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
            sb.AppendLine($"        {entityPropertyName}.Get({pkParamName}, {skParamName});");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates table-level Update method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelUpdateMethod(StringBuilder sb, EntityModel entity, string entityPropertyName)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetCSharpType(partitionKey.PropertyType);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <returns>An UpdateItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            sb.AppendLine($"    public UpdateItemRequestBuilder<{entity.ClassName}> Update({pkPropertyType} {paramName}) =>");
            sb.AppendLine($"        {entityPropertyName}.Update({paramName});");
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetCSharpType(sortKey.PropertyType);
            var pkParamName = ToCamelCase(pkAttributeName);
            var skParamName = ToCamelCase(skAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            sb.AppendLine($"    /// <returns>An UpdateItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            sb.AppendLine($"    public UpdateItemRequestBuilder<{entity.ClassName}> Update({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
            sb.AppendLine($"        {entityPropertyName}.Update({pkParamName}, {skParamName});");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates table-level Delete method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelDeleteMethod(StringBuilder sb, EntityModel entity, string entityPropertyName)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetCSharpType(partitionKey.PropertyType);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {paramName}) =>");
            sb.AppendLine($"        {entityPropertyName}.Delete({paramName});");
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetCSharpType(sortKey.PropertyType);
            var pkParamName = ToCamelCase(pkAttributeName);
            var skParamName = ToCamelCase(skAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
            sb.AppendLine($"        {entityPropertyName}.Delete({pkParamName}, {skParamName});");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates table-level Scan methods that delegate to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelScanMethods(StringBuilder sb, EntityModel entity, string entityPropertyName)
    {
        // Parameterless Scan() method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Scan operation builder for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// WARNING: Scan operations read every item in a table or index and can be very expensive.");
        sb.AppendLine($"    /// Use Query operations instead whenever possible.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"    public ScanRequestBuilder<{entity.ClassName}> Scan() =>");
        sb.AppendLine($"        {entityPropertyName}.Scan();");
        sb.AppendLine();

        // Expression-based Scan(string, params object[]) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Scan operation builder with a filter expression for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// Uses format string syntax for parameters: {{0}}, {{1}}, etc.");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// WARNING: Scan operations are expensive. Filter expressions reduce data transfer");
        sb.AppendLine($"    /// but do not reduce consumed read capacity.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"filterExpression\">The filter expression with format placeholders.</param>");
        sb.AppendLine($"    /// <param name=\"values\">The values to substitute into the expression.</param>");
        sb.AppendLine($"    /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured with the filter.</returns>");
        sb.AppendLine($"    public ScanRequestBuilder<{entity.ClassName}> Scan(string filterExpression, params object[] values) =>");
        sb.AppendLine($"        {entityPropertyName}.Scan(filterExpression, values);");
        sb.AppendLine();

        // LINQ expression Scan(Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Scan operation builder with a LINQ expression for the filter condition for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// Provides type-safe filter building with compile-time checking of property access.");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// WARNING: Scan operations are expensive. Filter expressions reduce data transfer");
        sb.AppendLine($"    /// but do not reduce consumed read capacity.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"filterCondition\">The LINQ expression representing the filter condition (e.g., x => x.Status == \"ACTIVE\").</param>");
        sb.AppendLine($"    /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured with the filter.</returns>");
        sb.AppendLine($"    public ScanRequestBuilder<{entity.ClassName}> Scan(Expression<Func<{entity.ClassName}, bool>> filterCondition) =>");
        sb.AppendLine($"        {entityPropertyName}.Scan(filterCondition);");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates transaction and batch operation methods at the table level.
    /// These methods are always generated at the table level and never on entity accessor classes.
    /// They allow coordinating operations across multiple entity types in a single transaction or batch.
    /// </summary>
    private static void GenerateTransactionAndBatchOperations(StringBuilder sb)
    {
        sb.AppendLine("    // Transaction and batch operations (table level only)");
        sb.AppendLine();
        
        // TransactWrite method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new TransactWriteItems operation builder for atomic multi-item writes.");
        sb.AppendLine("    /// Allows you to perform multiple write operations (Put, Update, Delete, ConditionCheck)");
        sb.AppendLine("    /// across multiple entity types as a single atomic transaction. All operations succeed or all fail.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>A TransactWriteItemsRequestBuilder for configuring the transaction.</returns>");
        sb.AppendLine("    /// <example>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine("    /// // Atomic transaction across multiple entities");
        sb.AppendLine("    /// await table.TransactWrite()");
        sb.AppendLine("    ///     .Put(table, put => put.WithItem(order))");
        sb.AppendLine("    ///     .Update(table, update => update");
        sb.AppendLine("    ///         .WithKey(\"id\", accountId)");
        sb.AppendLine("    ///         .Set(\"SET balance = balance - :amount\")");
        sb.AppendLine("    ///         .WithValue(\":amount\", 100))");
        sb.AppendLine("    ///     .ExecuteAsync();");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </example>");
        sb.AppendLine("    public TransactWriteItemsRequestBuilder TransactWrite() =>");
        sb.AppendLine("        new TransactWriteItemsRequestBuilder(DynamoDbClient, Logger);");
        sb.AppendLine();
        
        // TransactGet method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new TransactGetItems operation builder for atomic multi-item reads.");
        sb.AppendLine("    /// Allows you to retrieve multiple items from multiple entity types in a single atomic read transaction.");
        sb.AppendLine("    /// All get operations are performed with snapshot isolation at the same point in time.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>A TransactGetItemsRequestBuilder for configuring the transaction.</returns>");
        sb.AppendLine("    /// <example>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine("    /// // Atomic read transaction across multiple entities");
        sb.AppendLine("    /// var response = await table.TransactGet()");
        sb.AppendLine("    ///     .Get(table, get => get.WithKey(\"id\", orderId))");
        sb.AppendLine("    ///     .Get(table, get => get.WithKey(\"id\", accountId))");
        sb.AppendLine("    ///     .ExecuteAsync();");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </example>");
        sb.AppendLine("    public TransactGetItemsRequestBuilder TransactGet() =>");
        sb.AppendLine("        new TransactGetItemsRequestBuilder(DynamoDbClient, Logger);");
        sb.AppendLine();
        
        // BatchWrite method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new BatchWriteItem operation builder for bulk write operations.");
        sb.AppendLine("    /// Allows putting and deleting multiple items across multiple entity types in a single request,");
        sb.AppendLine("    /// improving performance and reducing API calls compared to individual write operations.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>A BatchWriteItemRequestBuilder for configuring the batch operation.</returns>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// Performance Considerations:");
        sb.AppendLine("    /// - BatchWriteItem can process up to 25 put or delete requests per batch");
        sb.AppendLine("    /// - Each item can be up to 400KB in size");
        sb.AppendLine("    /// - Operations are processed in parallel, improving throughput");
        sb.AppendLine("    /// - Unprocessed items may be returned if the request exceeds capacity limits");
        sb.AppendLine("    /// - No conditional writes are supported in batch operations");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    /// <example>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine("    /// // Batch write across multiple entities");
        sb.AppendLine("    /// await table.BatchWrite()");
        sb.AppendLine("    ///     .WriteToTable(table.Name, builder => builder");
        sb.AppendLine("    ///         .PutItem(orderAttributes)");
        sb.AppendLine("    ///         .DeleteItem(\"id\", oldOrderId))");
        sb.AppendLine("    ///     .ExecuteAsync();");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </example>");
        sb.AppendLine("    public BatchWriteItemRequestBuilder BatchWrite() =>");
        sb.AppendLine("        new BatchWriteItemRequestBuilder(DynamoDbClient, Logger);");
        sb.AppendLine();
        
        // BatchGet method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new BatchGetItem operation builder for bulk read operations.");
        sb.AppendLine("    /// Allows retrieving multiple items from multiple entity types in a single request,");
        sb.AppendLine("    /// improving performance and reducing API calls compared to individual GetItem operations.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>A BatchGetItemRequestBuilder for configuring the batch operation.</returns>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// Performance Considerations:");
        sb.AppendLine("    /// - BatchGetItem can retrieve up to 100 items or 16MB of data per request");
        sb.AppendLine("    /// - Items are retrieved in parallel, improving throughput");
        sb.AppendLine("    /// - Unprocessed keys may be returned if the request exceeds capacity limits");
        sb.AppendLine("    /// - Use consistent reads carefully as they consume twice the read capacity");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    /// <example>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine("    /// // Batch get across multiple entities");
        sb.AppendLine("    /// var response = await table.BatchGet()");
        sb.AppendLine("    ///     .GetFromTable(table.Name, builder => builder");
        sb.AppendLine("    ///         .WithKey(\"id\", orderId1)");
        sb.AppendLine("    ///         .WithKey(\"id\", orderId2))");
        sb.AppendLine("    ///     .ExecuteAsync();");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </example>");
        sb.AppendLine("    public BatchGetItemRequestBuilder BatchGet() =>");
        sb.AppendLine("        new BatchGetItemRequestBuilder(DynamoDbClient, Logger);");
        sb.AppendLine();
    }

    private static void GenerateConstructors(StringBuilder sb, EntityModel entity, string className)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {className}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client.</param>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    public {className}(IAmazonDynamoDB client, string tableName)");
        sb.AppendLine($"        : base(client, tableName)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {className} with a logger.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client.</param>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    /// <param name=\"logger\">Logger for DynamoDB operations.</param>");
        sb.AppendLine($"    public {className}(IAmazonDynamoDB client, string tableName, IDynamoDbLogger logger)");
        sb.AppendLine($"        : base(client, tableName, logger)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }

    private static void GenerateQueryMethods(StringBuilder sb, EntityModel entity)
    {
        // Parameterless Query() method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Query operation builder for this table.");
        sb.AppendLine($"    /// Query operations efficiently retrieve items using the primary key and optional sort key conditions.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"    public QueryRequestBuilder<{entity.ClassName}> Query() =>");
        sb.AppendLine($"        base.Query<{entity.ClassName}>();");
        sb.AppendLine();

        // Expression-based Query(string, params object[]) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Query operation builder with a key condition expression.");
        sb.AppendLine($"    /// Uses format string syntax for parameters: {{0}}, {{1}}, etc.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"keyConditionExpression\">The key condition expression with format placeholders.</param>");
        sb.AppendLine($"    /// <param name=\"values\">The values to substitute into the expression.</param>");
        sb.AppendLine($"    /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with the key condition.</returns>");
        sb.AppendLine($"    public QueryRequestBuilder<{entity.ClassName}> Query(string keyConditionExpression, params object[] values) =>");
        sb.AppendLine($"        base.Query<{entity.ClassName}>(keyConditionExpression, values);");
        sb.AppendLine();

        // LINQ expression Query(Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Query operation builder with a LINQ expression for the key condition.");
        sb.AppendLine($"    /// Provides type-safe query building with compile-time checking of property access.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"keyCondition\">The LINQ expression representing the key condition (e.g., x => x.PartitionKey == value).</param>");
        sb.AppendLine($"    /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with the key condition.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Simple partition key query");
        sb.AppendLine($"    /// var results = await table.Query(x => x.PartitionKey == \"USER#123\").ToListAsync();");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // Partition key with sort key condition");
        sb.AppendLine($"    /// var results = await table.Query(x => x.PartitionKey == \"USER#123\" &amp;&amp; x.SortKey.StartsWith(\"ORDER#\")).ToListAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public QueryRequestBuilder<{entity.ClassName}> Query(Expression<Func<{entity.ClassName}, bool>> keyCondition)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        return Query().Where(keyCondition);");
        sb.AppendLine($"    }}");
        sb.AppendLine();

        // LINQ expression Query(Expression<Func<TEntity, bool>>, Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Query operation builder with LINQ expressions for both key condition and filter.");
        sb.AppendLine($"    /// Provides type-safe query building with compile-time checking of property access.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"keyCondition\">The LINQ expression representing the key condition (e.g., x => x.PartitionKey == value).</param>");
        sb.AppendLine($"    /// <param name=\"filterCondition\">The LINQ expression representing the filter condition (e.g., x => x.Status == \"ACTIVE\").</param>");
        sb.AppendLine($"    /// <returns>A QueryRequestBuilder&lt;{entity.ClassName}&gt; configured with both key condition and filter.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Query with key condition and filter");
        sb.AppendLine($"    /// var results = await table.Query(");
        sb.AppendLine($"    ///     x => x.PartitionKey == \"USER#123\",");
        sb.AppendLine($"    ///     x => x.Status == \"ACTIVE\" &amp;&amp; x.Amount > 100");
        sb.AppendLine($"    /// ).ToListAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public QueryRequestBuilder<{entity.ClassName}> Query(");
        sb.AppendLine($"        Expression<Func<{entity.ClassName}, bool>> keyCondition,");
        sb.AppendLine($"        Expression<Func<{entity.ClassName}, bool>> filterCondition)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        return Query().Where(keyCondition).WithFilter(filterCondition);");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }

    private static void GeneratePutMethod(StringBuilder sb, EntityModel entity)
    {
        // Generic Put<TEntity>() method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new PutItem operation builder for this table.");
        sb.AppendLine($"    /// PutItem creates a new item or completely replaces an existing item with the same primary key.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Put an entity");
        sb.AppendLine($"    /// await table.Put()");
        sb.AppendLine($"    ///     .WithItem(myEntity)");
        sb.AppendLine($"    ///     .ExecuteAsync();");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // Put with condition (only if item doesn't exist)");
        sb.AppendLine($"    /// await table.Put()");
        sb.AppendLine($"    ///     .WithItem(myEntity)");
        sb.AppendLine($"    ///     .Where(\"attribute_not_exists(id)\")");
        sb.AppendLine($"    ///     .ExecuteAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public PutItemRequestBuilder<{entity.ClassName}> Put() =>");
        sb.AppendLine($"        base.Put<{entity.ClassName}>();");
        sb.AppendLine();
    }

    private static void GenerateOperationOverloads(StringBuilder sb, EntityModel entity)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            // No partition key - shouldn't happen for valid tables, but handle gracefully
            return;
        }

        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetCSharpType(partitionKey.PropertyType);
        
        if (sortKey == null)
        {
            // Single partition key table
            GenerateSingleKeyOverloads(sb, entity, pkAttributeName, pkPropertyType);
        }
        else
        {
            // Composite key table
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetCSharpType(sortKey.PropertyType);
            GenerateCompositeKeyOverloads(sb, entity, pkAttributeName, pkPropertyType, skAttributeName, skPropertyType);
        }
    }

    private static void GenerateSingleKeyOverloads(StringBuilder sb, EntityModel entity, string pkAttributeName, string pkPropertyType)
    {
        var paramName = ToCamelCase(pkAttributeName);
        
        // Get overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets an item by its {pkAttributeName} (partition key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
        sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {paramName}) =>");
        sb.AppendLine($"        base.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
        sb.AppendLine();
        
        // Update overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Updates an item by its {pkAttributeName} (partition key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>An UpdateItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
        sb.AppendLine($"    public UpdateItemRequestBuilder<{entity.ClassName}> Update({pkPropertyType} {paramName}) =>");
        sb.AppendLine($"        base.Update<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
        sb.AppendLine();
        
        // Delete overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Deletes an item by its {pkAttributeName} (partition key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
        sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {paramName}) =>");
        sb.AppendLine($"        base.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
        sb.AppendLine();
    }

    private static void GenerateCompositeKeyOverloads(StringBuilder sb, EntityModel entity, 
        string pkAttributeName, string pkPropertyType, string skAttributeName, string skPropertyType)
    {
        var pkParamName = ToCamelCase(pkAttributeName);
        var skParamName = ToCamelCase(skAttributeName);
        
        // Get overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets an item by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
        sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
        sb.AppendLine($"        base.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
        sb.AppendLine();
        
        // Update overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Updates an item by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>An UpdateItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
        sb.AppendLine($"    public UpdateItemRequestBuilder<{entity.ClassName}> Update({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
        sb.AppendLine($"        base.Update<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
        sb.AppendLine();
        
        // Delete overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Deletes an item by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
        sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
        sb.AppendLine($"        base.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
        sb.AppendLine();
    }

    private static void GenerateScanMethods(StringBuilder sb, EntityModel entity)
    {
        if (!entity.IsScannable)
        {
            return;
        }

        // Parameterless Scan() method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Scan operation builder for this table.");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// WARNING: Scan operations read every item in a table or index and can be very expensive.");
        sb.AppendLine($"    /// Use Query operations instead whenever possible. Scan should only be used for:");
        sb.AppendLine($"    /// - Data migration or ETL processes");
        sb.AppendLine($"    /// - Analytics on small tables");
        sb.AppendLine($"    /// - Operations where you truly need to examine every item");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"    public ScanRequestBuilder<{entity.ClassName}> Scan() =>");
        sb.AppendLine($"        new ScanRequestBuilder<{entity.ClassName}>(DynamoDbClient).ForTable(Name);");
        sb.AppendLine();

        // Expression-based Scan(string, params object[]) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Scan operation builder with a filter expression.");
        sb.AppendLine($"    /// Uses format string syntax for parameters: {{0}}, {{1}}, etc.");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// WARNING: Scan operations are expensive. Filter expressions reduce data transfer");
        sb.AppendLine($"    /// but do not reduce consumed read capacity.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"filterExpression\">The filter expression with format placeholders.</param>");
        sb.AppendLine($"    /// <param name=\"values\">The values to substitute into the expression.</param>");
        sb.AppendLine($"    /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured with the filter.</returns>");
        sb.AppendLine($"    public ScanRequestBuilder<{entity.ClassName}> Scan(string filterExpression, params object[] values)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        var builder = Scan();");
        sb.AppendLine($"        return WithFilterExpressionExtensions.WithFilter(builder, filterExpression, values);");
        sb.AppendLine($"    }}");
        sb.AppendLine();

        // LINQ expression Scan(Expression<Func<TEntity, bool>>) method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Scan operation builder with a LINQ expression for the filter condition.");
        sb.AppendLine($"    /// Provides type-safe filter building with compile-time checking of property access.");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// WARNING: Scan operations are expensive. Filter expressions reduce data transfer");
        sb.AppendLine($"    /// but do not reduce consumed read capacity.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"filterCondition\">The LINQ expression representing the filter condition (e.g., x => x.Status == \"ACTIVE\").</param>");
        sb.AppendLine($"    /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured with the filter.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Scan with filter");
        sb.AppendLine($"    /// var results = await table.Scan(x => x.Status == \"ACTIVE\" &amp;&amp; x.Amount > 100).ToListAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public ScanRequestBuilder<{entity.ClassName}> Scan(Expression<Func<{entity.ClassName}, bool>> filterCondition)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        return Scan().WithFilter(filterCondition);");
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }

    private static void GenerateIndexProperties(StringBuilder sb, EntityModel entity, string tableClassName)
    {
        if (entity.Indexes.Length == 0)
        {
            return;
        }

        foreach (var index in entity.Indexes)
        {
            var indexPropertyName = SanitizeIndexName(index.IndexName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Global Secondary Index: {index.IndexName}");
            sb.AppendLine($"    /// Partition Key: {index.PartitionKeyProperty}");
            if (index.HasSortKey)
            {
                sb.AppendLine($"    /// Sort Key: {index.SortKeyProperty}");
            }
            sb.AppendLine($"    /// </summary>");
            
            // Check if projection type exists for this index
            var projectionType = GetProjectionTypeForIndex(entity, index);
            
            if (projectionType != null)
            {
                // Generate typed index class reference when projection exists
                sb.AppendLine($"    public {indexPropertyName}Index {indexPropertyName} => new {indexPropertyName}Index(this);");
            }
            else
            {
                // Generate simple DynamoDbIndex property when no projection
                sb.AppendLine($"    public DynamoDbIndex {indexPropertyName} => new DynamoDbIndex(this, \"{index.IndexName}\");");
            }
            sb.AppendLine();
        }
    }

    private static void GenerateTypedIndexClass(StringBuilder sb, EntityModel entity, IndexModel index, string tableClassName)
    {
        var indexPropertyName = index.IndexName.Replace("-", "").Replace("_", "");
        var indexClassName = $"{indexPropertyName}Index";
        var projectionExpression = BuildProjectionExpression(entity, index);
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Typed index class for {index.IndexName} Global Secondary Index.");
        sb.AppendLine($"    /// Provides type-safe query operations with LINQ expression support and automatic index configuration.");
        sb.AppendLine($"    /// Supports GSI overloading - can query different entity types from the same index.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <remarks>");
        sb.AppendLine($"    /// <para>This nested class provides strongly-typed access to the {index.IndexName} GSI.</para>");
        sb.AppendLine($"    /// <para>Index Name: {index.IndexName}</para>");
        sb.AppendLine($"    /// <para>Partition Key: {index.PartitionKeyProperty}</para>");
        if (index.HasSortKey)
        {
            sb.AppendLine($"    /// <para>Sort Key: {index.SortKeyProperty}</para>");
        }
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// Benefits of using this typed index class:");
        sb.AppendLine($"    /// - Automatic index name configuration (no need to specify \"{index.IndexName}\" manually)");
        sb.AppendLine($"    /// - Type-safe query building with compile-time checking");
        sb.AppendLine($"    /// - LINQ expression support for key conditions and filters");
        sb.AppendLine($"    /// - Automatic projection expression configuration if defined");
        sb.AppendLine($"    /// - Support for querying multiple entity types from the same GSI (GSI overloading)");
        sb.AppendLine($"    /// </remarks>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Access via table property");
        sb.AppendLine($"    /// var index = table.{indexPropertyName};");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // Query with string expression");
        sb.AppendLine($"    /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;(");
        sb.AppendLine($"    ///     \"{index.PartitionKeyProperty} = {{{{0}}}}\", value)");
        sb.AppendLine($"    ///     .ToListAsync();");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // Query with LINQ expression");
        sb.AppendLine($"    /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;(");
        sb.AppendLine($"    ///     x => x.{index.PartitionKeyProperty} == value)");
        sb.AppendLine($"    ///     .ToListAsync();");
        if (index.HasSortKey)
        {
            sb.AppendLine($"    /// ");
            sb.AppendLine($"    /// // Query with composite key condition");
            sb.AppendLine($"    /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;(");
            sb.AppendLine($"    ///     x => x.{index.PartitionKeyProperty} == value &amp;&amp; x.{index.SortKeyProperty}.StartsWith(\"PREFIX\"))");
            sb.AppendLine($"    ///     .ToListAsync();");
        }
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // GSI overloading - query different entity type");
        sb.AppendLine($"    /// var otherResults = await table.{indexPropertyName}.Query&lt;OtherEntity&gt;(");
        sb.AppendLine($"    ///     x => x.{index.PartitionKeyProperty} == value)");
        sb.AppendLine($"    ///     .ToListAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public partial class {indexClassName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {tableClassName} _table;");
        sb.AppendLine();
        
        // Constructor
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Initializes a new instance of the {indexClassName}.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"table\">The parent table.</param>");
        sb.AppendLine($"        public {indexClassName}({tableClassName} table)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            _table = table;");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic Query<T>() method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder for this index with a specific entity type.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{index.IndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured for this index.</returns>");
        sb.AppendLine($"        /// <example>");
        sb.AppendLine($"        /// <code>");
        sb.AppendLine($"        /// // Query for an entity type stored in this GSI");
        sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;()");
        sb.AppendLine($"        ///     .Where(\"gsi1pk = {{0}}\", \"VALUE\")");
        sb.AppendLine($"        ///     .ToListAsync();");
        sb.AppendLine($"        /// </code>");
        sb.AppendLine($"        /// </example>");
        sb.AppendLine($"        public QueryRequestBuilder<T> Query<T>() where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var builder = new QueryRequestBuilder<T>(_table.DynamoDbClient)");
        sb.AppendLine($"                .ForTable(_table.Name)");
        sb.AppendLine($"                .UsingIndex(\"{index.IndexName}\");");
        
        if (!string.IsNullOrEmpty(projectionExpression))
        {
            sb.AppendLine($"            builder = builder.WithProjection(\"{projectionExpression}\");");
        }
        
        sb.AppendLine($"            return builder;");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic Query<T>(string, params object[]) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with a key condition expression and specific entity type.");
        sb.AppendLine($"        /// Uses format string syntax for parameters: {{0}}, {{1}}, etc.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{index.IndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <param name=\"keyConditionExpression\">The key condition expression with format placeholders.</param>");
        sb.AppendLine($"        /// <param name=\"values\">The values to substitute into the expression.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured with the key condition.</returns>");
        sb.AppendLine($"        /// <example>");
        sb.AppendLine($"        /// <code>");
        sb.AppendLine($"        /// // Query with key condition");
        sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;(\"gsi1pk = {{0}}\", \"VALUE\").ToListAsync();");
        sb.AppendLine($"        /// </code>");
        sb.AppendLine($"        /// </example>");
        sb.AppendLine($"        public QueryRequestBuilder<T> Query<T>(string keyConditionExpression, params object[] values) where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return WithConditionExpressionExtensions.Where(Query<T>(), keyConditionExpression, values);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic LINQ expression Query<T>(Expression<Func<T, bool>>) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with a LINQ expression for the key condition and specific entity type.");
        sb.AppendLine($"        /// Provides type-safe query building with compile-time checking of property access.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{index.IndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured with the key condition.</returns>");
        sb.AppendLine($"        /// <example>");
        sb.AppendLine($"        /// <code>");
        sb.AppendLine($"        /// // Query with LINQ expression");
        sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;(x => x.{index.PartitionKeyProperty} == \"VALUE\").ToListAsync();");
        if (index.HasSortKey)
        {
            sb.AppendLine($"        /// ");
            sb.AppendLine($"        /// // With sort key condition");
            sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;(x => x.{index.PartitionKeyProperty} == \"VALUE\" &amp;&amp; x.{index.SortKeyProperty}.StartsWith(\"PREFIX\")).ToListAsync();");
        }
        sb.AppendLine($"        /// </code>");
        sb.AppendLine($"        /// </example>");
        sb.AppendLine($"        public QueryRequestBuilder<T> Query<T>(Expression<Func<T, bool>> keyCondition) where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<T>().Where(keyCondition);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic LINQ expression Query<T>(Expression<Func<T, bool>>, Expression<Func<T, bool>>) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with LINQ expressions for both key condition and filter, and specific entity type.");
        sb.AppendLine($"        /// Provides type-safe query building with compile-time checking of property access.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{index.IndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <param name=\"filterCondition\">The LINQ expression representing the filter condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured with both key condition and filter.</returns>");
        sb.AppendLine($"        /// <example>");
        sb.AppendLine($"        /// <code>");
        sb.AppendLine($"        /// // Query with key condition and filter");
        sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;(");
        sb.AppendLine($"        ///     x => x.{index.PartitionKeyProperty} == \"VALUE\",");
        sb.AppendLine($"        ///     x => x.Status == \"ACTIVE\"");
        sb.AppendLine($"        /// ).ToListAsync();");
        sb.AppendLine($"        /// </code>");
        sb.AppendLine($"        /// </example>");
        sb.AppendLine($"        public QueryRequestBuilder<T> Query<T>(");
        sb.AppendLine($"            Expression<Func<T, bool>> keyCondition,");
        sb.AppendLine($"            Expression<Func<T, bool>> filterCondition) where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<T>().Where(keyCondition).WithFilter(filterCondition);");
        sb.AppendLine($"        }}");
        
        sb.AppendLine("    }");
    }

    private static string BuildProjectionExpression(EntityModel entity, IndexModel index)
    {
        // If projected properties are specified, use them
        if (index.ProjectedProperties.Length > 0)
        {
            return string.Join(", ", index.ProjectedProperties);
        }

        // Otherwise, build from entity properties that are part of this index
        var projectedProps = new List<string>();
        
        // Always include keys
        var pkProp = entity.Properties.FirstOrDefault(p => p.PropertyName == index.PartitionKeyProperty);
        if (pkProp != null && !string.IsNullOrEmpty(pkProp.AttributeName))
        {
            projectedProps.Add(pkProp.AttributeName);
        }
        
        if (index.HasSortKey)
        {
            var skProp = entity.Properties.FirstOrDefault(p => p.PropertyName == index.SortKeyProperty);
            if (skProp != null && !string.IsNullOrEmpty(skProp.AttributeName))
            {
                projectedProps.Add(skProp.AttributeName);
            }
        }
        
        // Add table keys if not already included
        if (entity.PartitionKeyProperty != null && !string.IsNullOrEmpty(entity.PartitionKeyProperty.AttributeName))
        {
            if (!projectedProps.Contains(entity.PartitionKeyProperty.AttributeName))
            {
                projectedProps.Add(entity.PartitionKeyProperty.AttributeName);
            }
        }
        
        if (entity.SortKeyProperty != null && !string.IsNullOrEmpty(entity.SortKeyProperty.AttributeName))
        {
            if (!projectedProps.Contains(entity.SortKeyProperty.AttributeName))
            {
                projectedProps.Add(entity.SortKeyProperty.AttributeName);
            }
        }
        
        return string.Join(", ", projectedProps);
    }

    private static string GetCSharpType(string propertyType)
    {
        // Remove nullable annotation for parameter types
        return propertyType.TrimEnd('?');
    }

    private static string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        // Handle snake_case
        if (text.Contains('_'))
        {
            var parts = text.Split('_');
            return string.Concat(parts.Select((part, index) => 
                index == 0 ? part.ToLowerInvariant() : Capitalize(part)));
        }
        
        // Handle kebab-case
        if (text.Contains('-'))
        {
            var parts = text.Split('-');
            return string.Concat(parts.Select((part, index) => 
                index == 0 ? part.ToLowerInvariant() : Capitalize(part)));
        }
        
        // Handle PascalCase
        if (char.IsUpper(text[0]))
        {
            return char.ToLowerInvariant(text[0]) + text.Substring(1);
        }
        
        return text;
    }

    private static string Capitalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        return char.ToUpperInvariant(text[0]) + text.Substring(1).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the table class name from a table name.
    /// Converts table name to PascalCase and appends "Table".
    /// </summary>
    /// <param name="tableName">The DynamoDB table name.</param>
    /// <returns>The generated table class name.</returns>
    private static string GetTableClassName(string tableName)
    {
        // Split by hyphens and underscores, capitalize each part
        var parts = tableName.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var cleanName = string.Join("", parts.Select(part => 
        {
            if (string.IsNullOrEmpty(part))
                return part;
            return char.ToUpperInvariant(part[0]) + part.Substring(1);
        }));
        
        return $"{cleanName}Table";
    }

    /// <summary>
    /// Sanitizes an index name by removing hyphens and underscores to create a valid C# identifier.
    /// </summary>
    /// <param name="indexName">The DynamoDB index name.</param>
    /// <returns>A sanitized index name suitable for use as a C# property name.</returns>
    private static string SanitizeIndexName(string indexName)
    {
        return indexName.Replace("-", "").Replace("_", "");
    }

    /// <summary>
    /// Determines if a projection type exists for the given index.
    /// Checks if any property in the entity has a [UseProjection] attribute for this index.
    /// </summary>
    /// <param name="entity">The entity model.</param>
    /// <param name="index">The index model.</param>
    /// <returns>The projection type name if found, otherwise null.</returns>
    private static string? GetProjectionTypeForIndex(EntityModel entity, IndexModel index)
    {
        // Check if the partition key property has a [UseProjection] attribute
        var partitionKeyProp = entity.Properties.FirstOrDefault(p => p.PropertyName == index.PartitionKeyProperty);
        if (partitionKeyProp != null)
        {
            var projectionType = DetectUseProjectionAttribute(partitionKeyProp);
            if (projectionType != null)
            {
                return projectionType;
            }
        }
        
        // Check if the index has projected properties (indicates a projection is defined)
        if (index.ProjectedProperties.Length > 0)
        {
            // If projected properties are explicitly defined, we consider this as having a projection
            // However, we don't have the projection type name here, so we return a marker
            // This will be used to determine whether to generate a typed index class
            return "HasProjection";
        }
        
        return null;
    }

    /// <summary>
    /// Detects [UseProjection] attribute on a property and returns the projection type name.
    /// </summary>
    /// <param name="property">The property model.</param>
    /// <returns>The projection type name if found, otherwise null.</returns>
    private static string? DetectUseProjectionAttribute(PropertyModel property)
    {
        if (property.PropertyDeclaration == null)
            return null;

        // Look for UseProjection attribute in the property's attribute lists
        foreach (var attributeList in property.PropertyDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attributeName = attribute.Name.ToString();
                if (attributeName.Contains("UseProjection"))
                {
                    // Extract the type argument from typeof(...)
                    if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.TypeOfExpressionSyntax typeOfExpr)
                    {
                        return typeOfExpr.Type.ToString();
                    }
                }
            }
        }

        return null;
    }
}
