using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates table class implementations with method-based builder access.
/// </summary>
internal static class TableGenerator
{
    /// <summary>
    /// Result of table class generation including the generated code and any diagnostics.
    /// </summary>
    public readonly struct TableGenerationResult
    {
        /// <summary>
        /// Gets the generated table class code.
        /// </summary>
        public string Code { get; }
        
        /// <summary>
        /// Gets the diagnostics collected during generation.
        /// </summary>
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        
        /// <summary>
        /// Initializes a new instance of the TableGenerationResult.
        /// </summary>
        public TableGenerationResult(string code, IReadOnlyList<Diagnostic> diagnostics)
        {
            Code = code;
            Diagnostics = diagnostics;
        }
    }

    /// <summary>
    /// Generates a table class implementation for multiple entities sharing the same table.
    /// This is the new multi-entity table generation approach.
    /// </summary>
    /// <param name="tableName">The DynamoDB table name.</param>
    /// <param name="entities">List of entities that share this table.</param>
    /// <returns>The generated table class code.</returns>
    public static string GenerateTableClass(string tableName, List<EntityModel> entities)
    {
        var result = GenerateTableClassWithDiagnostics(tableName, entities);
        return result.Code;
    }

    /// <summary>
    /// Generates a table class implementation for multiple entities sharing the same table,
    /// returning both the generated code and any diagnostics.
    /// </summary>
    /// <param name="tableName">The DynamoDB table name.</param>
    /// <param name="entities">List of entities that share this table.</param>
    /// <returns>A result containing the generated code and diagnostics.</returns>
    public static TableGenerationResult GenerateTableClassWithDiagnostics(string tableName, List<EntityModel> entities)
    {
        if (entities == null || entities.Count == 0)
        {
            return new TableGenerationResult(string.Empty, Array.Empty<Diagnostic>());
        }

        // Aggregate indexes from all entities and collect diagnostics
        var indexAggregator = new IndexAggregator();
        var aggregatedIndexes = indexAggregator.AggregateIndexes(entities);
        var diagnostics = indexAggregator.Diagnostics.ToList();

        // Check if any entity uses a type-based table reference
        var entityWithTableType = entities.FirstOrDefault(e => e.IsTableTypeReference && !string.IsNullOrEmpty(e.TableTypeName));
        
        // Use the specified type name if available, otherwise generate from table name
        var tableClassName = entityWithTableType?.TableTypeName ?? GetTableClassName(tableName);
        
        // Determine the default entity (single entity or marked as default)
        var defaultEntity = entities.Count == 1 
            ? entities[0] 
            : entities.FirstOrDefault(e => e.IsDefault);
        
        var sb = new StringBuilder();
        
        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        
        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using Amazon.DynamoDBv2;");
        sb.AppendLine("using Amazon.DynamoDBv2.Model;");
        sb.AppendLine("using Oproto.FluentDynamoDb;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Context;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Logging;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Providers.Encryption;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests.Extensions;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Entities;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Hydration;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Metadata;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Utility;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Validation;");
        
        // Add FluentResults using if any entity uses FluentResults
        if (entities.Any(e => e.UseFluentResults))
        {
            sb.AppendLine("using Oproto.FluentDynamoDb.FluentResults;");
        }
        
        // Determine the table namespace:
        // 1. If any entity specifies a custom namespace, use that (validation ensures they're all the same)
        // 2. Otherwise, use the first entity's namespace
        var primaryEntity = entities[0];
        var customNamespace = entities.FirstOrDefault(e => e.TableNamespace != null)?.TableNamespace;
        var tableNamespace = customNamespace ?? primaryEntity.Namespace;
        
        // Add using directives for entity namespaces that differ from the table namespace
        var entityNamespaces = entities
            .Select(e => e.Namespace)
            .Where(ns => ns != tableNamespace)
            .Distinct()
            .OrderBy(ns => ns);
        
        foreach (var ns in entityNamespaces)
        {
            sb.AppendLine($"using {ns};");
        }
        
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {tableNamespace};");
        sb.AppendLine();
        
        // Class declaration - no longer inherits from DynamoDbTableBase
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated table class for {tableName} table.");
        sb.AppendLine($"/// Provides method-based access to DynamoDB operations.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public partial class {tableClassName} : IDynamoDbTable");
        sb.AppendLine("{");
        
        // Generate core properties (previously from DynamoDbTableBase)
        GenerateCoreProperties(sb);
        
        // Generate entity accessor properties
        GenerateEntityAccessorProperties(sb, entities);
        
        // Constructors
        GenerateMultiEntityConstructors(sb, tableName, tableClassName, entities);
        
        // Generate base operation methods (previously from DynamoDbTableBase)
        GenerateBaseOperationMethods(sb);
        
        // Table-level operations (if default entity exists)
        if (defaultEntity != null)
        {
            GenerateTableLevelOperations(sb, defaultEntity);
        }
        
        // Generate generic Scan<TEntity>() methods for all scannable entities
        GenerateGenericScanMethods(sb, entities);
        
        // Generate consolidated index properties from all entities (only if no configuration conflicts)
        if (IndexAggregator.HasNoConflicts(aggregatedIndexes))
        {
            GenerateConsolidatedIndexProperties(sb, aggregatedIndexes, entities, tableClassName);
        }
        
        // Generate nested entity accessor classes
        GenerateEntityAccessorClasses(sb, entities, diagnostics);
        
        // Generate Keys Only projection records for indexes that require them (multi-entity tables)
        if (IndexAggregator.HasNoConflicts(aggregatedIndexes))
        {
            foreach (var aggregatedIndex in aggregatedIndexes)
            {
                // Find the first entity with this index to get the index model
                var entityWithIndex = aggregatedIndex.ReferencingEntities.FirstOrDefault();
                var indexModel = entityWithIndex?.Indexes.FirstOrDefault(i => 
                    string.Equals(i.IndexName, aggregatedIndex.DynamoDbIndexName, StringComparison.OrdinalIgnoreCase));
                
                if (indexModel != null && indexModel.RequiresKeysOnlyProjection && entityWithIndex != null)
                {
                    KeysOnlyProjectionGenerator.GenerateKeysOnlyProjectionRecord(sb, entityWithIndex, indexModel, tableClassName);
                }
            }
        }
        
        // Generate typed index classes for all consolidated indexes with projections (only if no conflicts)
        if (IndexAggregator.HasNoConflicts(aggregatedIndexes))
        {
            foreach (var aggregatedIndex in aggregatedIndexes)
            {
                var projectionType = GetProjectionTypeForAggregatedIndex(aggregatedIndex, entities);
                if (projectionType != null)
                {
                    sb.AppendLine();
                    GenerateTypedIndexClassFromAggregated(sb, aggregatedIndex, entities, tableClassName);
                }
            }
        }
        
        // Generate ValidateSchemaAsync method for schema validation
        if (defaultEntity != null)
        {
            SchemaValidationGenerator.GenerateValidateSchemaAsyncMethodForMultiEntity(sb, tableName, defaultEntity);
        }
        else
        {
            SchemaValidationGenerator.GenerateValidateSchemaAsyncMethod(sb, tableName, primaryEntity);
        }
        
        // Generate CreateTableAsync method for table creation
        if (defaultEntity != null)
        {
            TableCreationGenerator.GenerateCreateTableAsyncMethodForMultiEntity(sb, defaultEntity, entities);
        }
        else
        {
            TableCreationGenerator.GenerateCreateTableAsyncMethod(sb, primaryEntity);
        }
        
        sb.AppendLine("}");
        
        return new TableGenerationResult(sb.ToString(), diagnostics);
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

        // Use provided table class name, type-based reference name, or derive from table name
        var className = tableClassName 
            ?? (entity.IsTableTypeReference && !string.IsNullOrEmpty(entity.TableTypeName) 
                ? entity.TableTypeName 
                : GetTableClassName(entity.TableName ?? entity.ClassName));

        var sb = new StringBuilder();
        
        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        
        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using Amazon.DynamoDBv2;");
        sb.AppendLine("using Amazon.DynamoDBv2.Model;");
        sb.AppendLine("using Oproto.FluentDynamoDb;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Context;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Logging;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Providers.Encryption;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Requests.Extensions;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Entities;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Hydration;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Metadata;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Utility;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Validation;");
        
        // Add FluentResults using if entity uses FluentResults
        if (entity.UseFluentResults)
        {
            sb.AppendLine("using Oproto.FluentDynamoDb.FluentResults;");
        }
        
        // Determine the table namespace (use custom namespace if specified, otherwise use entity's namespace)
        var tableNamespace = entity.TableNamespace ?? entity.Namespace;
        
        // Add using directive for entity namespace if it differs from the table namespace
        if (entity.Namespace != tableNamespace)
        {
            sb.AppendLine($"using {entity.Namespace};");
        }
        
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {tableNamespace};");
        sb.AppendLine();
        
        // Class declaration - no longer inherits from DynamoDbTableBase
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated table class for {entity.ClassName} entity.");
        sb.AppendLine($"/// Provides method-based access to DynamoDB operations.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public partial class {className} : IDynamoDbTable");
        sb.AppendLine("{");
        
        // Generate core properties (previously from DynamoDbTableBase)
        GenerateCoreProperties(sb);
        
        // Constructors
        GenerateConstructors(sb, entity, className);
        
        // Generate base operation methods (previously from DynamoDbTableBase)
        GenerateBaseOperationMethods(sb);
        
        // Query methods
        GenerateQueryMethods(sb, entity);
        
        // Put method
        GeneratePutMethod(sb, entity);
        
        // Get/Update/Delete overloads based on key structure
        GenerateOperationOverloads(sb, entity);
        
        // Scan methods if table is scannable
        GenerateScanMethods(sb, entity);
        
        // Index properties (single-entity table)
        GenerateIndexProperties(sb, entity, className);
        
        // Generate Keys Only projection records for indexes that require them
        foreach (var index in entity.Indexes)
        {
            if (index.RequiresKeysOnlyProjection)
            {
                KeysOnlyProjectionGenerator.GenerateKeysOnlyProjectionRecord(sb, entity, index, className);
            }
        }
        
        // Generate typed index classes as nested classes (only for indexes with projections)
        // For single-entity tables, this includes indexes that use the entity as default projection
        foreach (var index in entity.Indexes)
        {
            var projectionType = DetermineIndexProjectionType(entity, index, isSingleEntityTable: true);
            if (projectionType != null)
            {
                sb.AppendLine();
                GenerateTypedIndexClass(sb, entity, index, className, isSingleEntityTable: true);
            }
        }
        
        // Generate ValidateSchemaAsync method for schema validation
        SchemaValidationGenerator.GenerateValidateSchemaAsyncMethod(sb, entity.TableName ?? entity.ClassName, entity);
        
        // Generate CreateTableAsync method for table creation
        TableCreationGenerator.GenerateCreateTableAsyncMethod(sb, entity);
        
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
    /// No longer calls base constructor - initializes properties directly.
    /// </summary>
    private static void GenerateMultiEntityConstructors(StringBuilder sb, string tableName, string className, List<EntityModel> entities)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {className}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client.</param>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    public {className}(IAmazonDynamoDB client, string tableName)");
        sb.AppendLine($"        : this(client, tableName, null)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {className} with options.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client.</param>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    /// <param name=\"options\">Configuration options including logger, hydrator registry, etc.</param>");
        sb.AppendLine($"    public {className}(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        DynamoDbClient = client;");
        sb.AppendLine($"        Name = tableName;");
        sb.AppendLine($"        Options = options ?? new FluentDynamoDbOptions();");
        sb.AppendLine($"        Logger = Options.Logger;");
        sb.AppendLine($"        FieldEncryptor = Options.FieldEncryptor;");
        
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
        
        // Auto-register hydrators for entities that require async serialization (encryption/blob storage)
        foreach (var entity in entities)
        {
            if (HydratorGenerator.RequiresHydrator(entity))
            {
                sb.AppendLine($"        DefaultEntityHydratorRegistry.Instance.Register{entity.ClassName}Hydrator();");
            }
        }
        
        sb.AppendLine($"    }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates nested entity accessor classes for multi-entity tables.
    /// Each accessor class provides entity-specific operation methods.
    /// </summary>
    private static void GenerateEntityAccessorClasses(StringBuilder sb, List<EntityModel> entities, List<Diagnostic>? diagnostics = null)
    {
        foreach (var entity in entities)
        {
            // Skip if Generate is false
            if (!entity.EntityPropertyConfig.Generate)
            {
                continue;
            }
            
            var accessorClassName = $"{entity.ClassName}Accessor";
            
            // Determine the table class name - use TableTypeName directly for type-based references
            var tableClassName = entity.IsTableTypeReference && !string.IsNullOrEmpty(entity.TableTypeName)
                ? entity.TableTypeName
                : GetTableClassName(entity.TableName);
            
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Nested accessor class for {entity.ClassName} entity operations.");
            sb.AppendLine($"    /// Provides entity-specific DynamoDB operation methods.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {accessorClassName}");
            sb.AppendLine($"    {{");
            
            // Private readonly field for parent table reference
            sb.AppendLine($"        private readonly {tableClassName} _table;");
            sb.AppendLine();
            
            // Internal constructor accepting parent table
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Initializes a new instance of the {accessorClassName}.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"table\">The parent table instance.</param>");
            sb.AppendLine($"        internal {accessorClassName}({tableClassName} table)");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            _table = table;");
            sb.AppendLine($"        }}");
            sb.AppendLine();
            
            // Generate operation methods based on AccessorConfig list
            GenerateAccessorOperationMethods(sb, entity, diagnostics);
            
            sb.AppendLine($"    }}");
        }
    }
    
    /// <summary>
    /// Generates operation methods for an entity accessor based on its AccessorConfig list.
    /// </summary>
    private static void GenerateAccessorOperationMethods(StringBuilder sb, EntityModel entity, List<Diagnostic>? diagnostics = null)
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
                    GenerateAccessorGetMethod(sb, entity, modifierStr, diagnostics);
                    break;
                    
                case TableOperation.Update:
                    GenerateAccessorUpdateMethod(sb, entity, modifierStr, diagnostics);
                    break;
                    
                case TableOperation.Delete:
                    GenerateAccessorDeleteMethod(sb, entity, modifierStr, diagnostics);
                    break;
                    
                case TableOperation.Scan:
                    if (entity.IsScannable)
                    {
                        GenerateAccessorScanMethods(sb, entity, modifierStr);
                    }
                    break;
            }
        }
        
        // Always generate ConditionCheck method (it's always public and available for transactions)
        GenerateAccessorConditionCheckMethod(sb, entity, "public", diagnostics);
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
        
        // QueryAsyncResult FluentResults method (when UseFluentResults is enabled)
        if (entity.UseFluentResults)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Executes a Query operation with a LINQ expression and returns a Result.");
            sb.AppendLine($"        /// This method returns a Result&lt;List&lt;T&gt;&gt; instead of throwing exceptions.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
            sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"        /// <returns>A Result containing the list of {entity.ClassName} entities or error details.</returns>");
            sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result<System.Collections.Generic.List<{entity.ClassName}>>> QueryAsyncResult(");
            sb.AppendLine($"            Expression<Func<{entity.ClassName}, bool>> keyCondition,");
            sb.AppendLine($"            System.Threading.CancellationToken cancellationToken = default) =>");
            sb.AppendLine($"            Query(keyCondition).ToListAsyncResult(cancellationToken);");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates Put method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorPutMethod(StringBuilder sb, EntityModel entity, string modifier)
    {
        // Determine whether to generate traditional async methods
        var generateTraditionalAsync = !entity.UseFluentResults || !entity.HideGeneratedAsyncMethods;
        
        // Parameterless Put() method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new PutItem operation builder for {entity.ClassName}.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"        {modifier} PutItemRequestBuilder<{entity.ClassName}> Put() =>");
        sb.AppendLine($"            _table.Put<{entity.ClassName}>();");
        sb.AppendLine();
        
        // Put(TEntity entity) overload
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new PutItem operation builder with the entity already set.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
        sb.AppendLine($"        /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the entity.</returns>");
        sb.AppendLine($"        {modifier} PutItemRequestBuilder<{entity.ClassName}> Put({entity.ClassName} entity)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return _table.Put<{entity.ClassName}>().WithItem(entity);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Put(Dictionary<string, AttributeValue>) overload for raw attribute dictionaries
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new PutItem operation builder with a raw attribute dictionary.");
        sb.AppendLine($"        /// This overload allows working with DynamoDB attribute dictionaries directly without requiring an entity class.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"item\">The raw DynamoDB attribute dictionary to put.</param>");
        sb.AppendLine($"        /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the item.</returns>");
        sb.AppendLine($"        {modifier} PutItemRequestBuilder<{entity.ClassName}> Put(Dictionary<string, AttributeValue> item)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return _table.Put<{entity.ClassName}>().WithItem(item);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // PutAsync express-route method for entity (conditionally generated)
        if (generateTraditionalAsync)
        {
            // Overload with just cancellation token (delegates to full version)
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Puts a {entity.ClassName} entity into DynamoDB and executes the request.");
            sb.AppendLine($"        /// This is an express-route method that combines Put() and PutAsync().");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
            sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
            sb.AppendLine($"        {modifier} System.Threading.Tasks.Task PutAsync({entity.ClassName} entity, System.Threading.CancellationToken cancellationToken) =>");
            sb.AppendLine($"            PutAsync(entity, KeyCondition.None, cancellationToken);");
            sb.AppendLine();
            
            // Full version with KeyCondition parameter (with default values for backward compatibility)
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Puts a {entity.ClassName} entity into DynamoDB and executes the request.");
            sb.AppendLine($"        /// This is an express-route method that combines Put() and PutAsync().");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
            sb.AppendLine($"        /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
            sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
            sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task PutAsync({entity.ClassName} entity, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            var builder = Put(entity);");
            sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
            sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
            sb.AppendLine($"            await builder.PutAsync(cancellationToken);");
            sb.AppendLine($"        }}");
            sb.AppendLine();
            
            // PutAsync express-route method for raw attribute dictionary
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Puts a raw attribute dictionary into DynamoDB and executes the request.");
            sb.AppendLine($"        /// This is an express-route method that combines Put() and PutAsync() for raw dictionaries.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"item\">The raw DynamoDB attribute dictionary to put.</param>");
            sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
            sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task PutAsync(Dictionary<string, AttributeValue> item, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            await Put(item).PutAsync(cancellationToken);");
            sb.AppendLine($"        }}");
            sb.AppendLine();
        }
        
        // PutAsyncResult FluentResults method (when UseFluentResults is enabled)
        if (entity.UseFluentResults)
        {
            // Overload with just cancellation token (delegates to full version)
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Puts a {entity.ClassName} entity into DynamoDB and returns a Result.");
            sb.AppendLine($"        /// This method returns a Result instead of throwing exceptions.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
            sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"        /// <returns>A Result indicating success or containing error details.</returns>");
            sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> PutAsyncResult({entity.ClassName} entity, System.Threading.CancellationToken cancellationToken) =>");
            sb.AppendLine($"            PutAsyncResult(entity, KeyCondition.None, cancellationToken);");
            sb.AppendLine();
            
            // Full version with KeyCondition parameter (with default values for backward compatibility)
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Puts a {entity.ClassName} entity into DynamoDB and returns a Result.");
            sb.AppendLine($"        /// This method returns a Result instead of throwing exceptions.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
            sb.AppendLine($"        /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
            sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"        /// <returns>A Result indicating success or containing error details.</returns>");
            sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> PutAsyncResult({entity.ClassName} entity, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            var builder = Put(entity);");
            sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
            sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
            sb.AppendLine($"            return builder.PutAsyncResult(cancellationToken);");
            sb.AppendLine($"        }}");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates Get method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorGetMethod(StringBuilder sb, EntityModel entity, string modifier, List<Diagnostic>? diagnostics = null)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetKeyParameterType(partitionKey);
        
        // Determine whether to generate traditional async methods
        var generateTraditionalAsync = !entity.UseFluentResults || !entity.HideGeneratedAsyncMethods;
        
        // Determine KeyInputMode eligibility
        var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"        /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"        /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"        {modifier} GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var resolvedMode = KeyInputModeResolver.Resolve(mode, _table.Options);");
                var pkEffective = GenerateKeyPrefixApplication(partitionKey, paramName, "effectivePk");
                if (pkEffective != null)
                {
                    sb.AppendLine($"            {pkEffective}");
                    var effectivePkName = "effectivePk";
                    if (NeedsSetKeyApproach(partitionKey))
                        sb.AppendLine($"            return _table.Get<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, effectivePkName)};");
                    else
                        sb.AppendLine($"            return _table.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {effectivePkName});");
                }
                else
                {
                    if (NeedsSetKeyApproach(partitionKey))
                        sb.AppendLine($"            return _table.Get<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
                    else
                        sb.AppendLine($"            return _table.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
                }
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        {modifier} GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {paramName}) =>");
                if (NeedsSetKeyApproach(partitionKey))
                {
                    sb.AppendLine($"            _table.Get<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
                }
                else
                {
                    sb.AppendLine($"            _table.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
                }
            }
            sb.AppendLine();
            
            // GetAsync express-route method (conditionally generated)
            if (generateTraditionalAsync)
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and executes the request.");
                sb.AppendLine($"        /// This is an express-route method that combines Get() and GetItemAsync().");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"        /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>The {entity.ClassName} entity if found, otherwise null.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            return await Get({paramName}, mode).GetItemAsync(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                else
                {
                    sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            return await Get({paramName}).GetItemAsync(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                sb.AppendLine();
            }
            
            // GetAsyncResult FluentResults method (when UseFluentResults is enabled)
            if (entity.UseFluentResults)
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and returns a Result.");
                sb.AppendLine($"        /// This method returns a Result&lt;T?&gt; instead of throwing exceptions.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"        /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A Result containing the {entity.ClassName} entity if found, otherwise null, or error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"            Get({paramName}, mode).GetItemAsyncResult(cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"            Get({paramName}).GetItemAsyncResult(cancellationToken);");
                }
                sb.AppendLine();
            }
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
            var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
            var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"        /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"        /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"        {modifier} GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var resolvedMode = KeyInputModeResolver.Resolve(mode, _table.Options);");
                var pkEffective = GenerateKeyPrefixApplication(partitionKey, pkParamName, "effectivePk");
                var skEffective = GenerateKeyPrefixApplication(sortKey, skParamName, "effectiveSk");
                if (pkEffective != null) sb.AppendLine($"            {pkEffective}");
                if (skEffective != null) sb.AppendLine($"            {skEffective}");
                var effectivePkName = pkEffective != null ? "effectivePk" : pkParamName;
                var effectiveSkName = skEffective != null ? "effectiveSk" : skParamName;
                if (useSetKey)
                    sb.AppendLine($"            return _table.Get<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, effectivePkName, sortKey, effectiveSkName)};");
                else
                    sb.AppendLine($"            return _table.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {effectivePkName}, \"{skAttributeName}\", {effectiveSkName});");
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        {modifier} GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
                if (useSetKey)
                {
                    sb.AppendLine($"            _table.Get<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, pkParamName, sortKey, skParamName)};");
                }
                else
                {
                    sb.AppendLine($"            _table.Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
                }
            }
            sb.AppendLine();
            
            // GetAsync express-route method (conditionally generated)
            if (generateTraditionalAsync)
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and executes the request.");
                sb.AppendLine($"        /// This is an express-route method that combines Get() and GetItemAsync().");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"        /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>The {entity.ClassName} entity if found, otherwise null.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            return await Get({pkParamName}, {skParamName}, mode).GetItemAsync(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                else
                {
                    sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            return await Get({pkParamName}, {skParamName}).GetItemAsync(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                sb.AppendLine();
            }
            
            // GetAsyncResult FluentResults method (when UseFluentResults is enabled)
            if (entity.UseFluentResults)
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and returns a Result.");
                sb.AppendLine($"        /// This method returns a Result&lt;T?&gt; instead of throwing exceptions.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"        /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A Result containing the {entity.ClassName} entity if found, otherwise null, or error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"            Get({pkParamName}, {skParamName}, mode).GetItemAsyncResult(cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"            Get({pkParamName}, {skParamName}).GetItemAsyncResult(cancellationToken);");
                }
                sb.AppendLine();
            }
        }
        
        // Generate typed convenience overload if eligible
        GenerateTypedGetOverload(sb, entity, modifier, diagnostics);
    }
    
    /// <summary>
    /// Generates a typed parameter convenience overload for the Get accessor method.
    /// This overload accepts individual source property components instead of pre-built key strings.
    /// </summary>
    private static void GenerateTypedGetOverload(StringBuilder sb, EntityModel entity, string modifier, List<Diagnostic>? diagnostics)
    {
        // Check if entity qualifies for typed overload
        if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
            return;
        
        // Check if the overload would be ambiguous with the standard overload
        if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            return;
        
        // Resolve typed overload parameters
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        if (typedParams == null)
        {
            // Parameter resolution failed - emit FDDB080 diagnostic
            EmitUnresolvableSourcePropertyDiagnostic(entity, diagnostics);
            return;
        }
        
        var partitionKey = entity.PartitionKeyProperty!;
        var sortKey = entity.SortKeyProperty;
        
        bool pkComputed = partitionKey.IsComputed && partitionKey.ComputedKey!.SourceProperties.Length >= 2;
        bool skComputed = sortKey?.IsComputed == true && sortKey.ComputedKey!.SourceProperties.Length >= 2;
        
        // Build the parameter list string
        var paramList = string.Join(", ", typedParams.Select(p => 
            $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
        
        // Build XML doc param elements
        var paramDocs = typedParams.Select(p => 
            $"        /// <param name=\"{p.Name}\">The {p.Name} component value.</param>");
        
        // Emit the typed overload method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Gets a {entity.ClassName} using typed source property parameters.");
        sb.AppendLine($"        /// This convenience overload composes the key internally via {entity.ClassName}.Keys.");
        sb.AppendLine($"        /// </summary>");
        foreach (var doc in paramDocs)
        {
            sb.AppendLine(doc);
        }
        sb.AppendLine($"        /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composed key.</returns>");
        sb.AppendLine($"        {modifier} GetItemRequestBuilder<{entity.ClassName}> Get({paramList})");
        sb.AppendLine($"        {{");
        
        // Build the delegation call arguments
        var delegationArgs = new List<string>();
        
        if (pkComputed)
        {
            // Emit: var computedPk = Entity.Keys.Build{PropertyName}(param1, param2, ...);
            var pkSourceParams = OverloadParameterResolver.ResolveParameters(entity, partitionKey);
            var pkArgs = string.Join(", ", pkSourceParams!.Select(p => p.Name));
            sb.AppendLine($"            var computedPk = {entity.ClassName}.Keys.Build{partitionKey.PropertyName}({pkArgs});");
            delegationArgs.Add("computedPk");
        }
        else
        {
            // PK is not computed — it's a plain string parameter named "pK"
            delegationArgs.Add("pK");
        }
        
        if (sortKey != null)
        {
            if (skComputed)
            {
                // Emit: var computedSk = Entity.Keys.Build{PropertyName}(param1, param2, ...);
                var skSourceParams = OverloadParameterResolver.ResolveParameters(entity, sortKey);
                var skArgs = string.Join(", ", skSourceParams!.Select(p => p.Name));
                sb.AppendLine($"            var computedSk = {entity.ClassName}.Keys.Build{sortKey.PropertyName}({skArgs});");
                delegationArgs.Add("computedSk");
            }
            else
            {
                // SK is not computed — it's a plain string parameter named "sK"
                delegationArgs.Add("sK");
            }
        }
        
        // Delegate to the standard overload
        var delegationArgStr = string.Join(", ", delegationArgs);
        sb.AppendLine($"            return Get({delegationArgStr});");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates a typed parameter convenience overload for the Delete accessor method.
    /// This overload accepts individual source property components instead of pre-built key strings.
    /// </summary>
    private static void GenerateTypedDeleteOverload(StringBuilder sb, EntityModel entity, string modifier, List<Diagnostic>? diagnostics)
    {
        // Check if entity qualifies for typed overload
        if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
            return;
        
        // Check if the overload would be ambiguous with the standard overload
        if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            return;
        
        // Resolve typed overload parameters
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        if (typedParams == null)
        {
            // Parameter resolution failed - emit diagnostic
            EmitUnresolvableSourcePropertyDiagnostic(entity, diagnostics);
            return;
        }
        
        var partitionKey = entity.PartitionKeyProperty!;
        var sortKey = entity.SortKeyProperty;
        
        bool pkComputed = partitionKey.IsComputed && partitionKey.ComputedKey!.SourceProperties.Length >= 2;
        bool skComputed = sortKey?.IsComputed == true && sortKey.ComputedKey!.SourceProperties.Length >= 2;
        
        // Build the parameter list string
        var paramList = string.Join(", ", typedParams.Select(p => 
            $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
        
        // Build XML doc param elements
        var paramDocs = typedParams.Select(p => 
            $"        /// <param name=\"{p.Name}\">The {p.Name} component value.</param>");
        
        // Emit the typed overload method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Deletes a {entity.ClassName} using typed source property parameters.");
        sb.AppendLine($"        /// This convenience overload composes the key internally via {entity.ClassName}.Keys.");
        sb.AppendLine($"        /// </summary>");
        foreach (var doc in paramDocs)
        {
            sb.AppendLine(doc);
        }
        sb.AppendLine($"        /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composed key.</returns>");
        sb.AppendLine($"        {modifier} DeleteItemRequestBuilder<{entity.ClassName}> Delete({paramList})");
        sb.AppendLine($"        {{");
        
        // Build the delegation call arguments
        var delegationArgs = new List<string>();
        
        if (pkComputed)
        {
            var pkSourceParams = OverloadParameterResolver.ResolveParameters(entity, partitionKey);
            var pkArgs = string.Join(", ", pkSourceParams!.Select(p => p.Name));
            sb.AppendLine($"            var computedPk = {entity.ClassName}.Keys.Build{partitionKey.PropertyName}({pkArgs});");
            delegationArgs.Add("computedPk");
        }
        else
        {
            delegationArgs.Add("pK");
        }
        
        if (sortKey != null)
        {
            if (skComputed)
            {
                var skSourceParams = OverloadParameterResolver.ResolveParameters(entity, sortKey);
                var skArgs = string.Join(", ", skSourceParams!.Select(p => p.Name));
                sb.AppendLine($"            var computedSk = {entity.ClassName}.Keys.Build{sortKey.PropertyName}({skArgs});");
                delegationArgs.Add("computedSk");
            }
            else
            {
                delegationArgs.Add("sK");
            }
        }
        
        // Delegate to the standard overload
        var delegationArgStr = string.Join(", ", delegationArgs);
        sb.AppendLine($"            return Delete({delegationArgStr});");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates a typed parameter convenience overload for the Update accessor method.
    /// This overload accepts individual source property components instead of pre-built key strings.
    /// </summary>
    private static void GenerateTypedUpdateOverload(StringBuilder sb, EntityModel entity, string modifier, List<Diagnostic>? diagnostics)
    {
        // Check if entity qualifies for typed overload
        if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
            return;
        
        // Check if the overload would be ambiguous with the standard overload
        if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            return;
        
        // Resolve typed overload parameters
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        if (typedParams == null)
        {
            // Parameter resolution failed - emit diagnostic
            EmitUnresolvableSourcePropertyDiagnostic(entity, diagnostics);
            return;
        }
        
        var partitionKey = entity.PartitionKeyProperty!;
        var sortKey = entity.SortKeyProperty;
        var updateBuilderClassName = $"{entity.ClassName}UpdateBuilder";
        
        bool pkComputed = partitionKey.IsComputed && partitionKey.ComputedKey!.SourceProperties.Length >= 2;
        bool skComputed = sortKey?.IsComputed == true && sortKey.ComputedKey!.SourceProperties.Length >= 2;
        
        // Build the parameter list string
        var paramList = string.Join(", ", typedParams.Select(p => 
            $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
        
        // Build XML doc param elements
        var paramDocs = typedParams.Select(p => 
            $"        /// <param name=\"{p.Name}\">The {p.Name} component value.</param>");
        
        // Emit the typed overload method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Updates a {entity.ClassName} using typed source property parameters.");
        sb.AppendLine($"        /// This convenience overload composes the key internally via {entity.ClassName}.Keys.");
        sb.AppendLine($"        /// </summary>");
        foreach (var doc in paramDocs)
        {
            sb.AppendLine(doc);
        }
        sb.AppendLine($"        /// <returns>A {updateBuilderClassName} configured with the composed key.</returns>");
        sb.AppendLine($"        {modifier} {updateBuilderClassName} Update({paramList})");
        sb.AppendLine($"        {{");
        
        // Build the delegation call arguments
        var delegationArgs = new List<string>();
        
        if (pkComputed)
        {
            var pkSourceParams = OverloadParameterResolver.ResolveParameters(entity, partitionKey);
            var pkArgs = string.Join(", ", pkSourceParams!.Select(p => p.Name));
            sb.AppendLine($"            var computedPk = {entity.ClassName}.Keys.Build{partitionKey.PropertyName}({pkArgs});");
            delegationArgs.Add("computedPk");
        }
        else
        {
            delegationArgs.Add("pK");
        }
        
        if (sortKey != null)
        {
            if (skComputed)
            {
                var skSourceParams = OverloadParameterResolver.ResolveParameters(entity, sortKey);
                var skArgs = string.Join(", ", skSourceParams!.Select(p => p.Name));
                sb.AppendLine($"            var computedSk = {entity.ClassName}.Keys.Build{sortKey.PropertyName}({skArgs});");
                delegationArgs.Add("computedSk");
            }
            else
            {
                delegationArgs.Add("sK");
            }
        }
        
        // Delegate to the standard overload
        var delegationArgStr = string.Join(", ", delegationArgs);
        sb.AppendLine($"            return Update({delegationArgStr});");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates a typed parameter convenience overload for the ConditionCheck accessor method.
    /// This overload accepts individual source property components instead of pre-built key strings.
    /// </summary>
    private static void GenerateTypedConditionCheckOverload(StringBuilder sb, EntityModel entity, string modifier, List<Diagnostic>? diagnostics)
    {
        // Check if entity qualifies for typed overload
        if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
            return;
        
        // Check if the overload would be ambiguous with the standard overload
        if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            return;
        
        // Resolve typed overload parameters
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        if (typedParams == null)
        {
            // Parameter resolution failed - emit diagnostic
            EmitUnresolvableSourcePropertyDiagnostic(entity, diagnostics);
            return;
        }
        
        var partitionKey = entity.PartitionKeyProperty!;
        var sortKey = entity.SortKeyProperty;
        
        bool pkComputed = partitionKey.IsComputed && partitionKey.ComputedKey!.SourceProperties.Length >= 2;
        bool skComputed = sortKey?.IsComputed == true && sortKey.ComputedKey!.SourceProperties.Length >= 2;
        
        // Build the parameter list string
        var paramList = string.Join(", ", typedParams.Select(p => 
            $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
        
        // Build XML doc param elements
        var paramDocs = typedParams.Select(p => 
            $"        /// <param name=\"{p.Name}\">The {p.Name} component value.</param>");
        
        // Emit the typed overload method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a condition check for a {entity.ClassName} using typed source property parameters.");
        sb.AppendLine($"        /// This convenience overload composes the key internally via {entity.ClassName}.Keys.");
        sb.AppendLine($"        /// </summary>");
        foreach (var doc in paramDocs)
        {
            sb.AppendLine(doc);
        }
        sb.AppendLine($"        /// <returns>A ConditionCheckBuilder&lt;{entity.ClassName}&gt; configured with the composed key.</returns>");
        sb.AppendLine($"        {modifier} ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({paramList})");
        sb.AppendLine($"        {{");
        
        // Build the delegation call arguments
        var delegationArgs = new List<string>();
        
        if (pkComputed)
        {
            var pkSourceParams = OverloadParameterResolver.ResolveParameters(entity, partitionKey);
            var pkArgs = string.Join(", ", pkSourceParams!.Select(p => p.Name));
            sb.AppendLine($"            var computedPk = {entity.ClassName}.Keys.Build{partitionKey.PropertyName}({pkArgs});");
            delegationArgs.Add("computedPk");
        }
        else
        {
            delegationArgs.Add("pK");
        }
        
        if (sortKey != null)
        {
            if (skComputed)
            {
                var skSourceParams = OverloadParameterResolver.ResolveParameters(entity, sortKey);
                var skArgs = string.Join(", ", skSourceParams!.Select(p => p.Name));
                sb.AppendLine($"            var computedSk = {entity.ClassName}.Keys.Build{sortKey.PropertyName}({skArgs});");
                delegationArgs.Add("computedSk");
            }
            else
            {
                delegationArgs.Add("sK");
            }
        }
        
        // Delegate to the standard overload
        var delegationArgStr = string.Join(", ", delegationArgs);
        sb.AppendLine($"            return ConditionCheck({delegationArgStr});");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Emits FDDB080 diagnostic when a source property in a computed key cannot be resolved.
    /// </summary>
    private static void EmitUnresolvableSourcePropertyDiagnostic(EntityModel entity, List<Diagnostic>? diagnostics)
    {
        if (diagnostics == null) return;
        
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        // Find which source property failed resolution
        string? failedPropertyName = null;
        string? keyPropertyName = null;
        
        if (partitionKey?.IsComputed == true && partitionKey.ComputedKey!.SourceProperties.Length >= 2)
        {
            foreach (var sourcePropName in partitionKey.ComputedKey.SourceProperties)
            {
                if (!entity.Properties.Any(p => p.PropertyName == sourcePropName))
                {
                    failedPropertyName = sourcePropName;
                    keyPropertyName = partitionKey.PropertyName;
                    break;
                }
            }
        }
        
        if (failedPropertyName == null && sortKey?.IsComputed == true && sortKey.ComputedKey!.SourceProperties.Length >= 2)
        {
            foreach (var sourcePropName in sortKey.ComputedKey.SourceProperties)
            {
                if (!entity.Properties.Any(p => p.PropertyName == sourcePropName))
                {
                    failedPropertyName = sourcePropName;
                    keyPropertyName = sortKey.PropertyName;
                    break;
                }
            }
        }
        
        if (failedPropertyName != null && keyPropertyName != null)
        {
            var location = entity.TypeDeclaration?.Identifier.GetLocation() 
                ?? entity.ClassDeclaration?.Identifier.GetLocation() 
                ?? Location.None;
            
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.UnresolvableComputedKeySourceProperty,
                location,
                failedPropertyName,
                entity.ClassName,
                keyPropertyName));
        }
    }

    /// <summary>
    /// Generates Update method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorUpdateMethod(StringBuilder sb, EntityModel entity, string modifier, List<Diagnostic>? diagnostics = null)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetKeyParameterType(partitionKey);
        var updateBuilderClassName = $"{entity.ClassName}UpdateBuilder";
        
        // Determine KeyInputMode eligibility
        var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"        /// Returns an entity-specific update builder with simplified Set() methods.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"        /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"        /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
            sb.AppendLine($"        /// <returns>A {updateBuilderClassName} configured with the key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"        {modifier} {updateBuilderClassName} Update({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default, KeyCondition keyCondition = KeyCondition.None)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var resolvedMode = KeyInputModeResolver.Resolve(mode, _table.Options);");
                var pkEffective = GenerateKeyPrefixApplication(partitionKey, paramName, "effectivePk");
                if (pkEffective != null)
                {
                    sb.AppendLine($"            {pkEffective}");
                }
                var effectivePkName = pkEffective != null ? "effectivePk" : paramName;
                sb.AppendLine($"            var builder = new {updateBuilderClassName}(_table.DynamoDbClient, _table.Options);");
                sb.AppendLine($"            builder.ForTable(_table.Name);");
                if (NeedsSetKeyApproach(partitionKey))
                    sb.AppendLine($"            builder{GenerateSetKeySingle(partitionKey, effectivePkName)};");
                else
                    sb.AppendLine($"            builder.WithKey(\"{pkAttributeName}\", {effectivePkName});");
                sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                sb.AppendLine($"            return builder;");
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        {modifier} {updateBuilderClassName} Update({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var builder = new {updateBuilderClassName}(_table.DynamoDbClient, _table.Options);");
                sb.AppendLine($"            builder.ForTable(_table.Name);");
                if (NeedsSetKeyApproach(partitionKey))
                {
                    sb.AppendLine($"            builder{GenerateSetKeySingle(partitionKey, paramName)};");
                }
                else
                {
                    sb.AppendLine($"            builder.WithKey(\"{pkAttributeName}\", {paramName});");
                }
                sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                sb.AppendLine($"            return builder;");
                sb.AppendLine($"        }}");
            }
            sb.AppendLine();
            
            // UpdateAsync express-route method
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key) and executes the request.");
            sb.AppendLine($"        /// This is an express-route method that combines Update() and UpdateAsync().");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"configureUpdate\">Action to configure the update builder.</param>");
            sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
            sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task UpdateAsync({pkPropertyType} {paramName}, System.Action<{updateBuilderClassName}> configureUpdate, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            var builder = Update({paramName});");
            sb.AppendLine($"            configureUpdate(builder);");
            sb.AppendLine($"            await builder.UpdateAsync(cancellationToken);");
            sb.AppendLine($"        }}");
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
            var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
            var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"        /// Returns an entity-specific update builder with simplified Set() methods.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"        /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"        /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
            sb.AppendLine($"        /// <returns>A {updateBuilderClassName} configured with the composite key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"        {modifier} {updateBuilderClassName} Update({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default, KeyCondition keyCondition = KeyCondition.None)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var resolvedMode = KeyInputModeResolver.Resolve(mode, _table.Options);");
                var pkEffective = GenerateKeyPrefixApplication(partitionKey, pkParamName, "effectivePk");
                var skEffective = GenerateKeyPrefixApplication(sortKey, skParamName, "effectiveSk");
                if (pkEffective != null) sb.AppendLine($"            {pkEffective}");
                if (skEffective != null) sb.AppendLine($"            {skEffective}");
                var effectivePkName = pkEffective != null ? "effectivePk" : pkParamName;
                var effectiveSkName = skEffective != null ? "effectiveSk" : skParamName;
                sb.AppendLine($"            var builder = new {updateBuilderClassName}(_table.DynamoDbClient, _table.Options);");
                sb.AppendLine($"            builder.ForTable(_table.Name);");
                if (useSetKey)
                    sb.AppendLine($"            builder{GenerateSetKeyComposite(partitionKey, effectivePkName, sortKey, effectiveSkName)};");
                else
                    sb.AppendLine($"            builder.WithKey(\"{pkAttributeName}\", {effectivePkName}, \"{skAttributeName}\", {effectiveSkName});");
                sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                sb.AppendLine($"            return builder;");
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        {modifier} {updateBuilderClassName} Update({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var builder = new {updateBuilderClassName}(_table.DynamoDbClient, _table.Options);");
                sb.AppendLine($"            builder.ForTable(_table.Name);");
                if (NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey))
                {
                    sb.AppendLine($"            builder{GenerateSetKeyComposite(partitionKey, pkParamName, sortKey, skParamName)};");
                }
                else
                {
                    sb.AppendLine($"            builder.WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
                }
                sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                sb.AppendLine($"            return builder;");
                sb.AppendLine($"        }}");
            }
            sb.AppendLine();
            
            // UpdateAsync express-route method
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and executes the request.");
            sb.AppendLine($"        /// This is an express-route method that combines Update() and UpdateAsync().");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"configureUpdate\">Action to configure the update builder.</param>");
            sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
            sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task UpdateAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Action<{updateBuilderClassName}> configureUpdate, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            var builder = Update({pkParamName}, {skParamName});");
            sb.AppendLine($"            configureUpdate(builder);");
            sb.AppendLine($"            await builder.UpdateAsync(cancellationToken);");
            sb.AppendLine($"        }}");
            sb.AppendLine();
        }
        
        // Generate typed convenience overload if eligible
        GenerateTypedUpdateOverload(sb, entity, modifier, diagnostics);
    }
    
    /// <summary>
    /// Generates Delete method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorDeleteMethod(StringBuilder sb, EntityModel entity, string modifier, List<Diagnostic>? diagnostics = null)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetKeyParameterType(partitionKey);
        
        // Determine whether to generate traditional async methods
        var generateTraditionalAsync = !entity.UseFluentResults || !entity.HideGeneratedAsyncMethods;
        
        // Determine KeyInputMode eligibility
        var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"        /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"        /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"        {modifier} DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var resolvedMode = KeyInputModeResolver.Resolve(mode, _table.Options);");
                var pkEffective = GenerateKeyPrefixApplication(partitionKey, paramName, "effectivePk");
                if (pkEffective != null)
                {
                    sb.AppendLine($"            {pkEffective}");
                    var effectivePkName = "effectivePk";
                    if (NeedsSetKeyApproach(partitionKey))
                        sb.AppendLine($"            return _table.Delete<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, effectivePkName)};");
                    else
                        sb.AppendLine($"            return _table.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {effectivePkName});");
                }
                else
                {
                    if (NeedsSetKeyApproach(partitionKey))
                        sb.AppendLine($"            return _table.Delete<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
                    else
                        sb.AppendLine($"            return _table.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
                }
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        {modifier} DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {paramName}) =>");
                if (NeedsSetKeyApproach(partitionKey))
                {
                    sb.AppendLine($"            _table.Delete<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
                }
                else
                {
                    sb.AppendLine($"            _table.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
                }
            }
            sb.AppendLine();
            
            // DeleteAsync express-route method (conditionally generated)
            if (generateTraditionalAsync)
            {
                // Overload with just cancellation token (delegates to full version)
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and executes the request.");
                sb.AppendLine($"        /// This is an express-route method that combines Delete() and DeleteAsync().");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine($"            DeleteAsync({paramName}, KeyCondition.None, KeyInputMode.Default, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine($"            DeleteAsync({paramName}, KeyCondition.None, cancellationToken);");
                }
                sb.AppendLine();
                
                // Full version with KeyCondition parameter
                // Note: Transaction validation is handled in DeleteItemRequestBuilder.ToDynamoDbResponseAsync()
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and executes the request.");
                sb.AppendLine($"        /// This is an express-route method that combines Delete() and DeleteAsync().");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"        /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var builder = Delete({paramName}, mode);");
                    sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                    sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                    sb.AppendLine($"            await builder.DeleteAsync(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                else
                {
                    sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var builder = Delete({paramName});");
                    sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                    sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                    sb.AppendLine($"            await builder.DeleteAsync(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                sb.AppendLine();
            }
            
            // DeleteAsyncResult FluentResults method (when UseFluentResults is enabled)
            if (entity.UseFluentResults)
            {
                // Overload with just cancellation token (delegates to full version)
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and returns a Result.");
                sb.AppendLine($"        /// This method returns a Result instead of throwing exceptions.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A Result indicating success or containing error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine($"            DeleteAsyncResult({paramName}, KeyCondition.None, KeyInputMode.Default, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine($"            DeleteAsyncResult({paramName}, KeyCondition.None, cancellationToken);");
                }
                sb.AppendLine();
                
                // Full version with KeyCondition parameter
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and returns a Result.");
                sb.AppendLine($"        /// This method returns a Result instead of throwing exceptions.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"        /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A Result indicating success or containing error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var builder = Delete({paramName}, mode);");
                    sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                    sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                    sb.AppendLine($"            return builder.DeleteAsyncResult(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                else
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var builder = Delete({paramName});");
                    sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                    sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                    sb.AppendLine($"            return builder.DeleteAsyncResult(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                sb.AppendLine();
            }
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
            var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
            var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"        /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"        /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"        {modifier} DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var resolvedMode = KeyInputModeResolver.Resolve(mode, _table.Options);");
                var pkEffective = GenerateKeyPrefixApplication(partitionKey, pkParamName, "effectivePk");
                var skEffective = GenerateKeyPrefixApplication(sortKey, skParamName, "effectiveSk");
                if (pkEffective != null) sb.AppendLine($"            {pkEffective}");
                if (skEffective != null) sb.AppendLine($"            {skEffective}");
                var effectivePkName = pkEffective != null ? "effectivePk" : pkParamName;
                var effectiveSkName = skEffective != null ? "effectiveSk" : skParamName;
                if (useSetKey)
                    sb.AppendLine($"            return _table.Delete<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, effectivePkName, sortKey, effectiveSkName)};");
                else
                    sb.AppendLine($"            return _table.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {effectivePkName}, \"{skAttributeName}\", {effectiveSkName});");
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        {modifier} DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
                if (useSetKey)
                {
                    sb.AppendLine($"            _table.Delete<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, pkParamName, sortKey, skParamName)};");
                }
                else
                {
                    sb.AppendLine($"            _table.Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
                }
            }
            sb.AppendLine();
            
            // DeleteAsync express-route method (conditionally generated)
            if (generateTraditionalAsync)
            {
                // Overload with just cancellation token (delegates to full version)
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and executes the request.");
                sb.AppendLine($"        /// This is an express-route method that combines Delete() and DeleteAsync().");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine($"            DeleteAsync({pkParamName}, {skParamName}, KeyCondition.None, KeyInputMode.Default, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine($"            DeleteAsync({pkParamName}, {skParamName}, KeyCondition.None, cancellationToken);");
                }
                sb.AppendLine();
                
                // Full version with KeyCondition parameter
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and executes the request.");
                sb.AppendLine($"        /// This is an express-route method that combines Delete() and DeleteAsync().");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"        /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A task representing the async operation.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var builder = Delete({pkParamName}, {skParamName}, mode);");
                    sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                    sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                    sb.AppendLine($"            await builder.DeleteAsync(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                else
                {
                    sb.AppendLine($"        {modifier} async System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var builder = Delete({pkParamName}, {skParamName});");
                    sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                    sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                    sb.AppendLine($"            await builder.DeleteAsync(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                sb.AppendLine();
            }
            
            // DeleteAsyncResult FluentResults method (when UseFluentResults is enabled)
            if (entity.UseFluentResults)
            {
                // Overload with just cancellation token (delegates to full version)
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and returns a Result.");
                sb.AppendLine($"        /// This method returns a Result instead of throwing exceptions.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A Result indicating success or containing error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine($"            DeleteAsyncResult({pkParamName}, {skParamName}, KeyCondition.None, KeyInputMode.Default, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine($"            DeleteAsyncResult({pkParamName}, {skParamName}, KeyCondition.None, cancellationToken);");
                }
                sb.AppendLine();
                
                // Full version with KeyCondition parameter
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and returns a Result.");
                sb.AppendLine($"        /// This method returns a Result instead of throwing exceptions.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                sb.AppendLine($"        /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"        /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"        /// <returns>A Result indicating success or containing error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var builder = Delete({pkParamName}, {skParamName}, mode);");
                    sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                    sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                    sb.AppendLine($"            return builder.DeleteAsyncResult(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                else
                {
                    sb.AppendLine($"        {modifier} System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            var builder = Delete({pkParamName}, {skParamName});");
                    sb.AppendLine($"            if (keyCondition != KeyCondition.None)");
                    sb.AppendLine($"                builder.WithKeyCondition(keyCondition);");
                    sb.AppendLine($"            return builder.DeleteAsyncResult(cancellationToken);");
                    sb.AppendLine($"        }}");
                }
                sb.AppendLine();
            }
        }
        
        // Generate typed convenience overload if eligible
        GenerateTypedDeleteOverload(sb, entity, modifier, diagnostics);
    }
    
    /// <summary>
    /// Generates ConditionCheck method for an entity accessor.
    /// </summary>
    private static void GenerateAccessorConditionCheckMethod(StringBuilder sb, EntityModel entity, string modifier, List<Diagnostic>? diagnostics = null)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetKeyParameterType(partitionKey);
        
        // Determine KeyInputMode eligibility
        var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Creates a condition check operation for a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"        /// Condition checks verify conditions without modifying data and are used within transactions.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"        /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"        /// <returns>A ConditionCheckBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            sb.AppendLine($"        /// <example>");
            sb.AppendLine($"        /// <code>");
            sb.AppendLine($"        /// // Use in a transaction");
            sb.AppendLine($"        /// await DynamoDbTransactions.Write");
            sb.AppendLine($"        ///     .Add(table.{GetEntityPropertyName(entity)}.ConditionCheck({paramName}Value)");
            sb.AppendLine($"        ///         .Where(\"attribute_exists(#status)\")");
            sb.AppendLine($"        ///         .WithAttribute(\"#status\", \"status\"))");
            sb.AppendLine($"        ///     .Add(table.{GetEntityPropertyName(entity)}.Update({paramName}Value).Set(...))");
            sb.AppendLine($"        ///     .ExecuteAsync();");
            sb.AppendLine($"        /// </code>");
            sb.AppendLine($"        /// </example>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"        {modifier} ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var resolvedMode = KeyInputModeResolver.Resolve(mode, _table.Options);");
                var pkEffective = GenerateKeyPrefixApplication(partitionKey, paramName, "effectivePk");
                if (pkEffective != null)
                {
                    sb.AppendLine($"            {pkEffective}");
                    var effectivePkName = "effectivePk";
                    if (NeedsSetKeyApproach(partitionKey))
                        sb.AppendLine($"            return _table.ConditionCheck<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, effectivePkName)};");
                    else
                        sb.AppendLine($"            return _table.ConditionCheck<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {effectivePkName});");
                }
                else
                {
                    if (NeedsSetKeyApproach(partitionKey))
                        sb.AppendLine($"            return _table.ConditionCheck<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
                    else
                        sb.AppendLine($"            return _table.ConditionCheck<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
                }
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        {modifier} ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {paramName}) =>");
                if (NeedsSetKeyApproach(partitionKey))
                {
                    sb.AppendLine($"            _table.ConditionCheck<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
                }
                else
                {
                    sb.AppendLine($"            _table.ConditionCheck<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
                }
            }
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
            var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
            var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Creates a condition check operation for a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"        /// Condition checks verify conditions without modifying data and are used within transactions.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"        /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"        /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"        /// <returns>A ConditionCheckBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            sb.AppendLine($"        /// <example>");
            sb.AppendLine($"        /// <code>");
            sb.AppendLine($"        /// // Use in a transaction");
            sb.AppendLine($"        /// await DynamoDbTransactions.Write");
            sb.AppendLine($"        ///     .Add(table.{GetEntityPropertyName(entity)}.ConditionCheck({pkParamName}Value, {skParamName}Value)");
            sb.AppendLine($"        ///         .Where(\"attribute_exists(#status)\")");
            sb.AppendLine($"        ///         .WithAttribute(\"#status\", \"status\"))");
            sb.AppendLine($"        ///     .Add(table.{GetEntityPropertyName(entity)}.Update({pkParamName}Value, {skParamName}Value).Set(...))");
            sb.AppendLine($"        ///     .ExecuteAsync();");
            sb.AppendLine($"        /// </code>");
            sb.AppendLine($"        /// </example>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"        {modifier} ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var resolvedMode = KeyInputModeResolver.Resolve(mode, _table.Options);");
                var pkEffective = GenerateKeyPrefixApplication(partitionKey, pkParamName, "effectivePk");
                var skEffective = GenerateKeyPrefixApplication(sortKey, skParamName, "effectiveSk");
                if (pkEffective != null) sb.AppendLine($"            {pkEffective}");
                if (skEffective != null) sb.AppendLine($"            {skEffective}");
                var effectivePkName = pkEffective != null ? "effectivePk" : pkParamName;
                var effectiveSkName = skEffective != null ? "effectiveSk" : skParamName;
                if (useSetKey)
                    sb.AppendLine($"            return _table.ConditionCheck<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, effectivePkName, sortKey, effectiveSkName)};");
                else
                    sb.AppendLine($"            return _table.ConditionCheck<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {effectivePkName}, \"{skAttributeName}\", {effectiveSkName});");
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        {modifier} ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
                if (NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey))
                {
                    sb.AppendLine($"            _table.ConditionCheck<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, pkParamName, sortKey, skParamName)};");
                }
                else
                {
                    sb.AppendLine($"            _table.ConditionCheck<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
                }
            }
            sb.AppendLine();
        }
        
        // Generate typed convenience overload if eligible
        GenerateTypedConditionCheckOverload(sb, entity, modifier, diagnostics);
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
        sb.AppendLine($"            new ScanRequestBuilder<{entity.ClassName}>(_table.DynamoDbClient, _table.GetOptions()).ForTable(_table.Name);");
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
        
        // Always generate ConditionCheck method (it's always available for transactions)
        GenerateTableLevelConditionCheckMethod(sb, defaultEntity, entityPropertyName);
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
        
        // QueryAsyncResult FluentResults method (when UseFluentResults is enabled)
        if (entity.UseFluentResults)
        {
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Executes a Query operation with a LINQ expression and returns a Result.");
            sb.AppendLine($"    /// This method returns a Result&lt;List&lt;T&gt;&gt; instead of throwing exceptions.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
            sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"    /// <returns>A Result containing the list of {entity.ClassName} entities or error details.</returns>");
            sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result<System.Collections.Generic.List<{entity.ClassName}>>> QueryAsyncResult(");
            sb.AppendLine($"        Expression<Func<{entity.ClassName}, bool>> keyCondition,");
            sb.AppendLine($"        System.Threading.CancellationToken cancellationToken = default) =>");
            sb.AppendLine($"        {entityPropertyName}.QueryAsyncResult(keyCondition, cancellationToken);");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates table-level Put method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelPutMethod(StringBuilder sb, EntityModel entity, string entityPropertyName)
    {
        // Parameterless Put() method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new PutItem operation builder for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// PutItem creates a new item or completely replaces an existing item with the same primary key.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
        sb.AppendLine($"    public PutItemRequestBuilder<{entity.ClassName}> Put() =>");
        sb.AppendLine($"        {entityPropertyName}.Put();");
        sb.AppendLine();
        
        // Put(TEntity entity) overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new PutItem operation builder with the entity already set for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// PutItem creates a new item or completely replaces an existing item with the same primary key.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
        sb.AppendLine($"    /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the entity.</returns>");
        sb.AppendLine($"    public PutItemRequestBuilder<{entity.ClassName}> Put({entity.ClassName} entity) =>");
        sb.AppendLine($"        {entityPropertyName}.Put(entity);");
        sb.AppendLine();
        
        // Put(Dictionary<string, AttributeValue>) overload for raw attribute dictionaries
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new PutItem operation builder with a raw attribute dictionary for the default entity ({entity.ClassName}).");
        sb.AppendLine($"    /// This overload allows working with DynamoDB attribute dictionaries directly without requiring an entity class.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"item\">The raw DynamoDB attribute dictionary to put.</param>");
        sb.AppendLine($"    /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the item.</returns>");
        sb.AppendLine($"    public PutItemRequestBuilder<{entity.ClassName}> Put(Dictionary<string, AttributeValue> item) =>");
        sb.AppendLine($"        {entityPropertyName}.Put(item);");
        sb.AppendLine();
        
        // PutAsyncResult FluentResults method (when UseFluentResults is enabled)
        if (entity.UseFluentResults)
        {
            // Overload with just cancellation token (delegates to full version)
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Puts a {entity.ClassName} entity into DynamoDB and returns a Result.");
            sb.AppendLine($"    /// This method returns a Result instead of throwing exceptions.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
            sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"    /// <returns>A Result indicating success or containing error details.</returns>");
            sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result> PutAsyncResult({entity.ClassName} entity, System.Threading.CancellationToken cancellationToken) =>");
            sb.AppendLine($"        {entityPropertyName}.PutAsyncResult(entity, cancellationToken);");
            sb.AppendLine();
            
            // Full version with KeyCondition parameter
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Puts a {entity.ClassName} entity into DynamoDB and returns a Result.");
            sb.AppendLine($"    /// This method returns a Result instead of throwing exceptions.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
            sb.AppendLine($"    /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
            sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
            sb.AppendLine($"    /// <returns>A Result indicating success or containing error details.</returns>");
            sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result> PutAsyncResult({entity.ClassName} entity, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default) =>");
            sb.AppendLine($"        {entityPropertyName}.PutAsyncResult(entity, keyCondition, cancellationToken);");
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Generates table-level Get method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelGetMethod(StringBuilder sb, EntityModel entity, string entityPropertyName, List<Diagnostic>? diagnostics = null)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetKeyParameterType(partitionKey);
        
        // Determine whether to generate traditional async methods
        var generateTraditionalAsync = !entity.UseFluentResults || !entity.HideGeneratedAsyncMethods;
        
        // Determine KeyInputMode eligibility
        var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"    /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default) =>");
                sb.AppendLine($"        {entityPropertyName}.Get({paramName}, mode);");
            }
            else
            {
                sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {paramName}) =>");
                sb.AppendLine($"        {entityPropertyName}.Get({paramName});");
            }
            sb.AppendLine();
            
            // GetAsync express-route method (conditionally generated)
            if (generateTraditionalAsync)
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and executes the request.");
                sb.AppendLine($"    /// This is an express-route method that combines Get() and GetItemAsync().");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"    /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>The {entity.ClassName} entity if found, otherwise null.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.GetAsync({paramName}, mode, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.GetAsync({paramName}, cancellationToken);");
                }
                sb.AppendLine();
            }
            
            // GetAsyncResult FluentResults method (when UseFluentResults is enabled)
            if (entity.UseFluentResults)
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and returns a Result.");
                sb.AppendLine($"    /// This method returns a Result&lt;T?&gt; instead of throwing exceptions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"    /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A Result containing the {entity.ClassName} entity if found, otherwise null, or error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.GetAsyncResult({paramName}, mode, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.GetAsyncResult({paramName}, cancellationToken);");
                }
                sb.AppendLine();
            }
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
            var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
            var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"    /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default) =>");
                sb.AppendLine($"        {entityPropertyName}.Get({pkParamName}, {skParamName}, mode);");
            }
            else
            {
                sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
                sb.AppendLine($"        {entityPropertyName}.Get({pkParamName}, {skParamName});");
            }
            sb.AppendLine();
            
            // GetAsync express-route method (conditionally generated)
            if (generateTraditionalAsync)
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and executes the request.");
                sb.AppendLine($"    /// This is an express-route method that combines Get() and GetItemAsync().");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"    /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>The {entity.ClassName} entity if found, otherwise null.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.GetAsync({pkParamName}, {skParamName}, mode, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.GetAsync({pkParamName}, {skParamName}, cancellationToken);");
                }
                sb.AppendLine();
            }
            
            // GetAsyncResult FluentResults method (when UseFluentResults is enabled)
            if (entity.UseFluentResults)
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Gets a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and returns a Result.");
                sb.AppendLine($"    /// This method returns a Result&lt;T?&gt; instead of throwing exceptions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"    /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A Result containing the {entity.ClassName} entity if found, otherwise null, or error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.GetAsyncResult({pkParamName}, {skParamName}, mode, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.GetAsyncResult({pkParamName}, {skParamName}, cancellationToken);");
                }
                sb.AppendLine();
            }
        }
        
        // Generate typed overload at table level if eligible
        GenerateTableLevelTypedGetOverload(sb, entity, entityPropertyName, diagnostics);
    }
    
    /// <summary>
    /// Generates table-level Update method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelUpdateMethod(StringBuilder sb, EntityModel entity, string entityPropertyName, List<Diagnostic>? diagnostics = null)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetKeyParameterType(partitionKey);
        var updateBuilderClassName = $"{entity.ClassName}UpdateBuilder";
        
        // Determine KeyInputMode eligibility
        var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"    /// Returns an entity-specific update builder with simplified Set() methods.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"    /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"    /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
            sb.AppendLine($"    /// <returns>A {updateBuilderClassName} configured with the key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"    public {updateBuilderClassName} Update({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default, KeyCondition keyCondition = KeyCondition.None) =>");
                sb.AppendLine($"        {entityPropertyName}.Update({paramName}, mode, keyCondition);");
            }
            else
            {
                sb.AppendLine($"    public {updateBuilderClassName} Update({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None) =>");
                sb.AppendLine($"        {entityPropertyName}.Update({paramName}, keyCondition);");
            }
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
            var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
            var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Updates a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"    /// Returns an entity-specific update builder with simplified Set() methods.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"    /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"    /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
            sb.AppendLine($"    /// <returns>A {updateBuilderClassName} configured with the composite key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"    public {updateBuilderClassName} Update({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default, KeyCondition keyCondition = KeyCondition.None) =>");
                sb.AppendLine($"        {entityPropertyName}.Update({pkParamName}, {skParamName}, mode, keyCondition);");
            }
            else
            {
                sb.AppendLine($"    public {updateBuilderClassName} Update({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None) =>");
                sb.AppendLine($"        {entityPropertyName}.Update({pkParamName}, {skParamName}, keyCondition);");
            }
            sb.AppendLine();
        }
        
        // Generate typed overload at table level if eligible
        GenerateTableLevelTypedUpdateOverload(sb, entity, entityPropertyName, diagnostics);
    }
    
    /// <summary>
    /// Generates table-level Delete method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelDeleteMethod(StringBuilder sb, EntityModel entity, string entityPropertyName, List<Diagnostic>? diagnostics = null)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetKeyParameterType(partitionKey);
        
        // Determine whether to generate traditional async methods
        var generateTraditionalAsync = !entity.UseFluentResults || !entity.HideGeneratedAsyncMethods;
        
        // Determine KeyInputMode eligibility
        var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"    /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default) =>");
                sb.AppendLine($"        {entityPropertyName}.Delete({paramName}, mode);");
            }
            else
            {
                sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {paramName}) =>");
                sb.AppendLine($"        {entityPropertyName}.Delete({paramName});");
            }
            sb.AppendLine();
            
            // DeleteAsync express-route method (conditionally generated)
            if (generateTraditionalAsync)
            {
                // Overload with just cancellation token (delegates to full version)
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and executes the request.");
                sb.AppendLine($"    /// This is an express-route method that combines Delete() and DeleteAsync().");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A task representing the async operation.</returns>");
                sb.AppendLine($"    public System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken) =>");
                sb.AppendLine($"        {entityPropertyName}.DeleteAsync({paramName}, cancellationToken);");
                sb.AppendLine();
                
                // Full version with KeyCondition parameter
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and executes the request.");
                sb.AppendLine($"    /// This is an express-route method that combines Delete() and DeleteAsync().");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"    /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A task representing the async operation.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.DeleteAsync({paramName}, keyCondition, mode, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.DeleteAsync({paramName}, keyCondition, cancellationToken);");
                }
                sb.AppendLine();
            }
            
            // DeleteAsyncResult FluentResults method (when UseFluentResults is enabled)
            if (entity.UseFluentResults)
            {
                // Overload with just cancellation token (delegates to full version)
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and returns a Result.");
                sb.AppendLine($"    /// This method returns a Result instead of throwing exceptions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A Result indicating success or containing error details.</returns>");
                sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {paramName}, System.Threading.CancellationToken cancellationToken) =>");
                sb.AppendLine($"        {entityPropertyName}.DeleteAsyncResult({paramName}, cancellationToken);");
                sb.AppendLine();
                
                // Full version with KeyCondition parameter
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and returns a Result.");
                sb.AppendLine($"    /// This method returns a Result instead of throwing exceptions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"    /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A Result indicating success or containing error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.DeleteAsyncResult({paramName}, keyCondition, mode, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {paramName}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.DeleteAsyncResult({paramName}, keyCondition, cancellationToken);");
                }
                sb.AppendLine();
            }
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
            var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
            var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"    /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default) =>");
                sb.AppendLine($"        {entityPropertyName}.Delete({pkParamName}, {skParamName}, mode);");
            }
            else
            {
                sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
                sb.AppendLine($"        {entityPropertyName}.Delete({pkParamName}, {skParamName});");
            }
            sb.AppendLine();
            
            // DeleteAsync express-route method (conditionally generated)
            if (generateTraditionalAsync)
            {
                // Overload with just cancellation token (delegates to full version)
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and executes the request.");
                sb.AppendLine($"    /// This is an express-route method that combines Delete() and DeleteAsync().");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A task representing the async operation.</returns>");
                sb.AppendLine($"    public System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken) =>");
                sb.AppendLine($"        {entityPropertyName}.DeleteAsync({pkParamName}, {skParamName}, cancellationToken);");
                sb.AppendLine();
                
                // Full version with KeyCondition parameter
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and executes the request.");
                sb.AppendLine($"    /// This is an express-route method that combines Delete() and DeleteAsync().");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"    /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A task representing the async operation.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.DeleteAsync({pkParamName}, {skParamName}, keyCondition, mode, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task DeleteAsync({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.DeleteAsync({pkParamName}, {skParamName}, keyCondition, cancellationToken);");
                }
                sb.AppendLine();
            }
            
            // DeleteAsyncResult FluentResults method (when UseFluentResults is enabled)
            if (entity.UseFluentResults)
            {
                // Overload with just cancellation token (delegates to full version)
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and returns a Result.");
                sb.AppendLine($"    /// This method returns a Result instead of throwing exceptions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A Result indicating success or containing error details.</returns>");
                sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, System.Threading.CancellationToken cancellationToken) =>");
                sb.AppendLine($"        {entityPropertyName}.DeleteAsyncResult({pkParamName}, {skParamName}, cancellationToken);");
                sb.AppendLine();
                
                // Full version with KeyCondition parameter
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Deletes a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key) and returns a Result.");
                sb.AppendLine($"    /// This method returns a Result instead of throwing exceptions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
                sb.AppendLine($"    /// <param name=\"keyCondition\">Optional key condition to check before the operation. Defaults to None (no condition).</param>");
                if (qualifiesForKeyInputMode)
                    sb.AppendLine($"    /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
                sb.AppendLine($"    /// <returns>A Result indicating success or containing error details.</returns>");
                if (qualifiesForKeyInputMode)
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.DeleteAsyncResult({pkParamName}, {skParamName}, keyCondition, mode, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"    public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {entityPropertyName}.DeleteAsyncResult({pkParamName}, {skParamName}, keyCondition, cancellationToken);");
                }
                sb.AppendLine();
            }
        }
        
        // Generate typed overload at table level if eligible
        GenerateTableLevelTypedDeleteOverload(sb, entity, entityPropertyName, diagnostics);
    }
    
    /// <summary>
    /// Generates table-level ConditionCheck method that delegates to the default entity's accessor.
    /// </summary>
    private static void GenerateTableLevelConditionCheckMethod(StringBuilder sb, EntityModel entity, string entityPropertyName, List<Diagnostic>? diagnostics = null)
    {
        var partitionKey = entity.PartitionKeyProperty;
        var sortKey = entity.SortKeyProperty;
        
        if (partitionKey == null)
        {
            return;
        }
        
        var pkAttributeName = partitionKey.AttributeName;
        var pkPropertyType = GetKeyParameterType(partitionKey);
        
        // Determine KeyInputMode eligibility
        var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);
        
        if (sortKey == null)
        {
            // Single partition key
            var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Creates a condition check operation for a {entity.ClassName} by its {pkAttributeName} (partition key).");
            sb.AppendLine($"    /// Condition checks verify conditions without modifying data and are used within transactions.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"    /// <param name=\"mode\">Controls how the key value prefix is applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"    /// <returns>A ConditionCheckBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"    public ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {paramName}, KeyInputMode mode = KeyInputMode.Default) =>");
                sb.AppendLine($"        {entityPropertyName}.ConditionCheck({paramName}, mode);");
            }
            else
            {
                sb.AppendLine($"    public ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {paramName}) =>");
                sb.AppendLine($"        {entityPropertyName}.ConditionCheck({paramName});");
            }
            sb.AppendLine();
        }
        else
        {
            // Composite key
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
            var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
            var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Creates a condition check operation for a {entity.ClassName} by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
            sb.AppendLine($"    /// Condition checks verify conditions without modifying data and are used within transactions.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
            sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
            if (qualifiesForKeyInputMode)
                sb.AppendLine($"    /// <param name=\"mode\">Controls how key value prefixes are applied. Defaults to the configured default mode.</param>");
            sb.AppendLine($"    /// <returns>A ConditionCheckBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
            
            if (qualifiesForKeyInputMode)
            {
                sb.AppendLine($"    public ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}, KeyInputMode mode = KeyInputMode.Default) =>");
                sb.AppendLine($"        {entityPropertyName}.ConditionCheck({pkParamName}, {skParamName}, mode);");
            }
            else
            {
                sb.AppendLine($"    public ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
                sb.AppendLine($"        {entityPropertyName}.ConditionCheck({pkParamName}, {skParamName});");
            }
            sb.AppendLine();
        }
        
        // Generate typed overload at table level if eligible
        GenerateTableLevelTypedConditionCheckOverload(sb, entity, entityPropertyName, diagnostics);
    }
    
    /// <summary>
    /// Generates a table-level typed parameter convenience overload for Get that delegates to the entity accessor's typed overload.
    /// </summary>
    private static void GenerateTableLevelTypedGetOverload(StringBuilder sb, EntityModel entity, string entityPropertyName, List<Diagnostic>? diagnostics)
    {
        if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
            return;
        
        if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            return;
        
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        if (typedParams == null)
            return; // diagnostic already emitted at entity accessor level
        
        var paramList = string.Join(", ", typedParams.Select(p =>
            $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
        var argList = string.Join(", ", typedParams.Select(p => p.Name));
        
        var paramDocs = typedParams.Select(p =>
            $"    /// <param name=\"{p.Name}\">The {p.Name} component value.</param>");
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets a {entity.ClassName} using typed source property parameters.");
        sb.AppendLine($"    /// This convenience overload delegates to the entity accessor's typed overload.");
        sb.AppendLine($"    /// </summary>");
        foreach (var doc in paramDocs)
        {
            sb.AppendLine(doc);
        }
        sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composed key.</returns>");
        sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({paramList}) =>");
        sb.AppendLine($"        {entityPropertyName}.Get({argList});");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates a table-level typed parameter convenience overload for Delete that delegates to the entity accessor's typed overload.
    /// </summary>
    private static void GenerateTableLevelTypedDeleteOverload(StringBuilder sb, EntityModel entity, string entityPropertyName, List<Diagnostic>? diagnostics)
    {
        if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
            return;
        
        if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            return;
        
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        if (typedParams == null)
            return; // diagnostic already emitted at entity accessor level
        
        var paramList = string.Join(", ", typedParams.Select(p =>
            $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
        var argList = string.Join(", ", typedParams.Select(p => p.Name));
        
        var paramDocs = typedParams.Select(p =>
            $"    /// <param name=\"{p.Name}\">The {p.Name} component value.</param>");
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Deletes a {entity.ClassName} using typed source property parameters.");
        sb.AppendLine($"    /// This convenience overload delegates to the entity accessor's typed overload.");
        sb.AppendLine($"    /// </summary>");
        foreach (var doc in paramDocs)
        {
            sb.AppendLine(doc);
        }
        sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composed key.</returns>");
        sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({paramList}) =>");
        sb.AppendLine($"        {entityPropertyName}.Delete({argList});");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates a table-level typed parameter convenience overload for Update that delegates to the entity accessor's typed overload.
    /// </summary>
    private static void GenerateTableLevelTypedUpdateOverload(StringBuilder sb, EntityModel entity, string entityPropertyName, List<Diagnostic>? diagnostics)
    {
        if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
            return;
        
        if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            return;
        
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        if (typedParams == null)
            return; // diagnostic already emitted at entity accessor level
        
        var updateBuilderClassName = $"{entity.ClassName}UpdateBuilder";
        
        var paramList = string.Join(", ", typedParams.Select(p =>
            $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
        var argList = string.Join(", ", typedParams.Select(p => p.Name));
        
        var paramDocs = typedParams.Select(p =>
            $"    /// <param name=\"{p.Name}\">The {p.Name} component value.</param>");
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Updates a {entity.ClassName} using typed source property parameters.");
        sb.AppendLine($"    /// This convenience overload delegates to the entity accessor's typed overload.");
        sb.AppendLine($"    /// </summary>");
        foreach (var doc in paramDocs)
        {
            sb.AppendLine(doc);
        }
        sb.AppendLine($"    /// <returns>A {updateBuilderClassName} configured with the composed key.</returns>");
        sb.AppendLine($"    public {updateBuilderClassName} Update({paramList}) =>");
        sb.AppendLine($"        {entityPropertyName}.Update({argList});");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates a table-level typed parameter convenience overload for ConditionCheck that delegates to the entity accessor's typed overload.
    /// </summary>
    private static void GenerateTableLevelTypedConditionCheckOverload(StringBuilder sb, EntityModel entity, string entityPropertyName, List<Diagnostic>? diagnostics)
    {
        if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
            return;
        
        if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            return;
        
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        if (typedParams == null)
            return; // diagnostic already emitted at entity accessor level
        
        var paramList = string.Join(", ", typedParams.Select(p =>
            $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
        var argList = string.Join(", ", typedParams.Select(p => p.Name));
        
        var paramDocs = typedParams.Select(p =>
            $"    /// <param name=\"{p.Name}\">The {p.Name} component value.</param>");
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a condition check for a {entity.ClassName} using typed source property parameters.");
        sb.AppendLine($"    /// This convenience overload delegates to the entity accessor's typed overload.");
        sb.AppendLine($"    /// </summary>");
        foreach (var doc in paramDocs)
        {
            sb.AppendLine(doc);
        }
        sb.AppendLine($"    /// <returns>A ConditionCheckBuilder&lt;{entity.ClassName}&gt; configured with the composed key.</returns>");
        sb.AppendLine($"    public ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({paramList}) =>");
        sb.AppendLine($"        {entityPropertyName}.ConditionCheck({argList});");
        sb.AppendLine();
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


    private static void GenerateConstructors(StringBuilder sb, EntityModel entity, string className)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {className}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client.</param>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    public {className}(IAmazonDynamoDB client, string tableName)");
        sb.AppendLine($"        : this(client, tableName, null)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {className} with options.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"client\">The DynamoDB client.</param>");
        sb.AppendLine($"    /// <param name=\"tableName\">The DynamoDB table name.</param>");
        sb.AppendLine($"    /// <param name=\"options\">Configuration options including logger, hydrator registry, etc.</param>");
        sb.AppendLine($"    public {className}(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        DynamoDbClient = client;");
        sb.AppendLine($"        Name = tableName;");
        sb.AppendLine($"        Options = options ?? new FluentDynamoDbOptions();");
        sb.AppendLine($"        Logger = Options.Logger;");
        sb.AppendLine($"        FieldEncryptor = Options.FieldEncryptor;");
        
        // Auto-register hydrator for entities that require async serialization (encryption/blob storage)
        if (HydratorGenerator.RequiresHydrator(entity))
        {
            sb.AppendLine($"        DefaultEntityHydratorRegistry.Instance.Register{entity.ClassName}Hydrator();");
        }
        
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
        sb.AppendLine($"        Query<{entity.ClassName}>();");
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
        sb.AppendLine($"        Query<{entity.ClassName}>(keyConditionExpression, values);");
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
        sb.AppendLine($"        Put<{entity.ClassName}>();");
        sb.AppendLine();
        
        // Put(TEntity entity) overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new PutItem operation builder with the entity already set.");
        sb.AppendLine($"    /// PutItem creates a new item or completely replaces an existing item with the same primary key.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"entity\">The entity to put into DynamoDB.</param>");
        sb.AppendLine($"    /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the entity.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Put an entity directly");
        sb.AppendLine($"    /// await table.Put(myEntity).PutAsync();");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // Put with condition");
        sb.AppendLine($"    /// await table.Put(myEntity)");
        sb.AppendLine($"    ///     .Where(\"attribute_not_exists(id)\")");
        sb.AppendLine($"    ///     .PutAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public PutItemRequestBuilder<{entity.ClassName}> Put({entity.ClassName} entity)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        return Put<{entity.ClassName}>().WithItem(entity);");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        
        // Put(Dictionary<string, AttributeValue>) overload for raw attribute dictionaries
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new PutItem operation builder with a raw attribute dictionary.");
        sb.AppendLine($"    /// This overload allows working with DynamoDB attribute dictionaries directly without requiring an entity class.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"item\">The raw DynamoDB attribute dictionary to put.</param>");
        sb.AppendLine($"    /// <returns>A PutItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the item.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Put a raw attribute dictionary");
        sb.AppendLine($"    /// await table.Put(new Dictionary&lt;string, AttributeValue&gt;");
        sb.AppendLine($"    /// {{");
        sb.AppendLine($"    ///     [\"pk\"] = new AttributeValue {{ S = \"ORDER#123\" }},");
        sb.AppendLine($"    ///     [\"status\"] = new AttributeValue {{ S = \"ACTIVE\" }}");
        sb.AppendLine($"    /// }}).PutAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public PutItemRequestBuilder<{entity.ClassName}> Put(Dictionary<string, AttributeValue> item)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        return Put<{entity.ClassName}>().WithItem(item);");
        sb.AppendLine($"    }}");
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
        var pkPropertyType = GetKeyParameterType(partitionKey);
        
        if (sortKey == null)
        {
            // Single partition key table
            GenerateSingleKeyOverloads(sb, entity, partitionKey, pkAttributeName, pkPropertyType);
        }
        else
        {
            // Composite key table
            var skAttributeName = sortKey.AttributeName;
            var skPropertyType = GetKeyParameterType(sortKey);
            GenerateCompositeKeyOverloads(sb, entity, partitionKey, pkAttributeName, pkPropertyType, sortKey, skAttributeName, skPropertyType);
        }
    }

    private static void GenerateSingleKeyOverloads(StringBuilder sb, EntityModel entity, PropertyModel partitionKey, string pkAttributeName, string pkPropertyType)
    {
        var paramName = NeedsSetKeyApproach(partitionKey) ? "pK" : ToCamelCase(pkAttributeName);
        
        // Get overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets an item by its {pkAttributeName} (partition key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
        sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {paramName}) =>");
        if (NeedsSetKeyApproach(partitionKey))
        {
            sb.AppendLine($"        Get<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
        }
        else
        {
            sb.AppendLine($"        Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
        }
        sb.AppendLine();
        
        // Update overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Updates an item by its {pkAttributeName} (partition key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>An UpdateItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
        sb.AppendLine($"    public UpdateItemRequestBuilder<{entity.ClassName}> Update({pkPropertyType} {paramName}) =>");
        if (NeedsSetKeyApproach(partitionKey))
        {
            sb.AppendLine($"        Update<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
        }
        else
        {
            sb.AppendLine($"        Update<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
        }
        sb.AppendLine();
        
        // Delete overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Deletes an item by its {pkAttributeName} (partition key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
        sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {paramName}) =>");
        if (NeedsSetKeyApproach(partitionKey))
        {
            sb.AppendLine($"        Delete<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
        }
        else
        {
            sb.AppendLine($"        Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
        }
        sb.AppendLine();
        
        // ConditionCheck overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a condition check operation for an item by its {pkAttributeName} (partition key).");
        sb.AppendLine($"    /// Condition checks verify conditions without modifying data and are used within transactions.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{paramName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A ConditionCheckBuilder&lt;{entity.ClassName}&gt; configured with the key.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Use in a transaction");
        sb.AppendLine($"    /// await DynamoDbTransactions.Write");
        sb.AppendLine($"    ///     .Add(table.ConditionCheck({paramName}Value)");
        sb.AppendLine($"    ///         .Where(\"attribute_exists(#status)\")");
        sb.AppendLine($"    ///         .WithAttribute(\"#status\", \"status\"))");
        sb.AppendLine($"    ///     .Add(table.Update(pk).Set(...))");
        sb.AppendLine($"    ///     .ExecuteAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {paramName}) =>");
        if (NeedsSetKeyApproach(partitionKey))
        {
            sb.AppendLine($"        ConditionCheck<{entity.ClassName}>(){GenerateSetKeySingle(partitionKey, paramName)};");
        }
        else
        {
            sb.AppendLine($"        ConditionCheck<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {paramName});");
        }
        sb.AppendLine();
    }

    private static void GenerateCompositeKeyOverloads(StringBuilder sb, EntityModel entity, 
        PropertyModel partitionKey, string pkAttributeName, string pkPropertyType,
        PropertyModel sortKey, string skAttributeName, string skPropertyType)
    {
        var useSetKey = NeedsSetKeyApproach(partitionKey) || NeedsSetKeyApproach(sortKey);
        var pkParamName = useSetKey ? "pK" : ToCamelCase(pkAttributeName);
        var skParamName = useSetKey ? "sK" : ToCamelCase(skAttributeName);
        
        // Get overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets an item by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A GetItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
        sb.AppendLine($"    public GetItemRequestBuilder<{entity.ClassName}> Get({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
        if (useSetKey)
        {
            sb.AppendLine($"        Get<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, pkParamName, sortKey, skParamName)};");
        }
        else
        {
            sb.AppendLine($"        Get<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
        }
        sb.AppendLine();
        
        // Update overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Updates an item by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>An UpdateItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
        sb.AppendLine($"    public UpdateItemRequestBuilder<{entity.ClassName}> Update({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
        if (useSetKey)
        {
            sb.AppendLine($"        Update<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, pkParamName, sortKey, skParamName)};");
        }
        else
        {
            sb.AppendLine($"        Update<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
        }
        sb.AppendLine();
        
        // Delete overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Deletes an item by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A DeleteItemRequestBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
        sb.AppendLine($"    public DeleteItemRequestBuilder<{entity.ClassName}> Delete({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
        if (useSetKey)
        {
            sb.AppendLine($"        Delete<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, pkParamName, sortKey, skParamName)};");
        }
        else
        {
            sb.AppendLine($"        Delete<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
        }
        sb.AppendLine();
        
        // ConditionCheck overload
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a condition check operation for an item by its {pkAttributeName} (partition key) and {skAttributeName} (sort key).");
        sb.AppendLine($"    /// Condition checks verify conditions without modifying data and are used within transactions.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"{pkParamName}\">The {pkAttributeName} value.</param>");
        sb.AppendLine($"    /// <param name=\"{skParamName}\">The {skAttributeName} value.</param>");
        sb.AppendLine($"    /// <returns>A ConditionCheckBuilder&lt;{entity.ClassName}&gt; configured with the composite key.</returns>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Use in a transaction");
        sb.AppendLine($"    /// await DynamoDbTransactions.Write");
        sb.AppendLine($"    ///     .Add(table.ConditionCheck({pkParamName}Value, {skParamName}Value)");
        sb.AppendLine($"    ///         .Where(\"attribute_exists(#status)\")");
        sb.AppendLine($"    ///         .WithAttribute(\"#status\", \"status\"))");
        sb.AppendLine($"    ///     .Add(table.Update(pk, sk).Set(...))");
        sb.AppendLine($"    ///     .ExecuteAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public ConditionCheckBuilder<{entity.ClassName}> ConditionCheck({pkPropertyType} {pkParamName}, {skPropertyType} {skParamName}) =>");
        if (useSetKey)
        {
            sb.AppendLine($"        ConditionCheck<{entity.ClassName}>(){GenerateSetKeyComposite(partitionKey, pkParamName, sortKey, skParamName)};");
        }
        else
        {
            sb.AppendLine($"        ConditionCheck<{entity.ClassName}>().WithKey(\"{pkAttributeName}\", {pkParamName}, \"{skAttributeName}\", {skParamName});");
        }
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
        sb.AppendLine($"        new ScanRequestBuilder<{entity.ClassName}>(DynamoDbClient, Options).ForTable(Name);");
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
        // This method is called for single-entity tables only.
        // For single-entity tables, we use the entity type as the default projection
        // when no [UseProjection] attribute is present and ProjectionType != KeysOnly.
        GenerateIndexPropertiesInternal(sb, entity, tableClassName, isSingleEntityTable: true);
    }

    /// <summary>
    /// Internal method for generating index properties with single-entity table awareness.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="entity">The entity model.</param>
    /// <param name="tableClassName">The table class name.</param>
    /// <param name="isSingleEntityTable">Whether this is a single-entity table.</param>
    private static void GenerateIndexPropertiesInternal(StringBuilder sb, EntityModel entity, string tableClassName, bool isSingleEntityTable)
    {
        if (entity.Indexes.Length == 0)
        {
            return;
        }

        foreach (var index in entity.Indexes)
        {
            // Use ResolvedPropertyName which is either the custom Name or derived from IndexName
            var indexPropertyName = !string.IsNullOrEmpty(index.ResolvedPropertyName) 
                ? index.ResolvedPropertyName 
                : SanitizeIndexName(index.IndexName);
            
            var indexType = index.IsGsi ? "Global Secondary Index" : "Local Secondary Index";
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {indexType}: {index.IndexName}");
            sb.AppendLine($"    /// Partition Key: {index.PartitionKeyProperty}");
            if (index.HasSortKey)
            {
                sb.AppendLine($"    /// Sort Key: {index.SortKeyProperty}");
            }
            if (!string.IsNullOrEmpty(index.CustomName))
            {
                sb.AppendLine($"    /// Custom Property Name: {index.CustomName}");
            }
            sb.AppendLine($"    /// </summary>");
            
            // Determine the projection type for this index
            var projectionType = DetermineIndexProjectionType(entity, index, isSingleEntityTable);
            
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

    /// <summary>
    /// Determines the projection type for an index based on the decision flow:
    /// 1. Explicit [UseProjection] - highest priority
    /// 2. KeysOnly - generates auto projection (handled separately)
    /// 3. Single-entity table - use entity type
    /// 4. Multi-entity table without projection - simple index (null)
    /// </summary>
    /// <param name="entity">The entity model.</param>
    /// <param name="index">The index model.</param>
    /// <param name="isSingleEntityTable">Whether this is a single-entity table.</param>
    /// <returns>The projection type name if found, otherwise null.</returns>
    private static string? DetermineIndexProjectionType(EntityModel entity, IndexModel index, bool isSingleEntityTable)
    {
        // 1. Check for explicit [UseProjection] - highest priority
        var explicitProjection = GetProjectionTypeForIndex(entity, index);
        if (explicitProjection != null && explicitProjection != "HasProjection")
        {
            return explicitProjection;
        }
        
        // 2. Check for KeysOnly - generates auto projection (will be handled by KeysOnlyProjectionGenerator)
        if (index.RequiresKeysOnlyProjection)
        {
            return $"{index.ResolvedPropertyName}KeysProjection";
        }
        
        // 3. Single-entity table - use entity type as default projection
        if (isSingleEntityTable)
        {
            return entity.ClassName;
        }
        
        // 4. Multi-entity table without projection - simple index
        return null;
    }

    /// <summary>
    /// Generates consolidated index properties from aggregated indexes across all entities.
    /// This method replaces single-entity index generation for multi-entity tables.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="aggregatedIndexes">The aggregated indexes from all entities.</param>
    /// <param name="entities">The list of entities sharing this table.</param>
    /// <param name="tableClassName">The table class name.</param>
    private static void GenerateConsolidatedIndexProperties(
        StringBuilder sb, 
        List<AggregatedIndexModel> aggregatedIndexes, 
        List<EntityModel> entities,
        string tableClassName)
    {
        if (aggregatedIndexes.Count == 0)
        {
            return;
        }

        foreach (var aggregatedIndex in aggregatedIndexes)
        {
            var indexPropertyName = aggregatedIndex.ResolvedPropertyName;
            var indexType = aggregatedIndex.Type == IndexType.GlobalSecondaryIndex 
                ? "Global Secondary Index" 
                : "Local Secondary Index";
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {indexType}: {aggregatedIndex.DynamoDbIndexName}");
            sb.AppendLine($"    /// Partition Key: {aggregatedIndex.PartitionKeyProperty}");
            if (!string.IsNullOrEmpty(aggregatedIndex.SortKeyProperty))
            {
                sb.AppendLine($"    /// Sort Key: {aggregatedIndex.SortKeyProperty}");
            }
            
            // List referencing entities
            var entityNames = aggregatedIndex.ReferencingEntities.Select(e => e.ClassName).ToList();
            if (entityNames.Count > 0)
            {
                sb.AppendLine($"    /// Referenced by: {string.Join(", ", entityNames)}");
            }
            
            sb.AppendLine($"    /// </summary>");
            
            // Check if projection type exists for this aggregated index
            var projectionType = GetProjectionTypeForAggregatedIndex(aggregatedIndex, entities);
            
            if (projectionType != null)
            {
                // Generate typed index class reference when projection exists
                sb.AppendLine($"    public {indexPropertyName}Index {indexPropertyName} => new {indexPropertyName}Index(this);");
            }
            else
            {
                // Generate simple DynamoDbIndex property when no projection
                sb.AppendLine($"    public DynamoDbIndex {indexPropertyName} => new DynamoDbIndex(this, \"{aggregatedIndex.DynamoDbIndexName}\");");
            }
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Gets the projection type for an aggregated index by checking all referencing entities.
    /// </summary>
    /// <param name="aggregatedIndex">The aggregated index model.</param>
    /// <param name="entities">The list of entities sharing this table.</param>
    /// <returns>The projection type name if found, otherwise null.</returns>
    private static string? GetProjectionTypeForAggregatedIndex(AggregatedIndexModel aggregatedIndex, List<EntityModel> entities)
    {
        // Check each referencing entity for a projection type
        foreach (var entity in aggregatedIndex.ReferencingEntities)
        {
            var index = entity.Indexes.FirstOrDefault(i => 
                string.Equals(i.IndexName, aggregatedIndex.DynamoDbIndexName, StringComparison.OrdinalIgnoreCase));
            
            if (index != null)
            {
                var projectionType = GetProjectionTypeForIndex(entity, index);
                if (projectionType != null)
                {
                    return projectionType;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Generates a typed index class from an aggregated index model.
    /// This method is used for multi-entity tables with consolidated indexes.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="aggregatedIndex">The aggregated index model.</param>
    /// <param name="entities">The list of entities sharing this table.</param>
    /// <param name="tableClassName">The table class name.</param>
    private static void GenerateTypedIndexClassFromAggregated(
        StringBuilder sb, 
        AggregatedIndexModel aggregatedIndex, 
        List<EntityModel> entities,
        string tableClassName)
    {
        var indexPropertyName = aggregatedIndex.ResolvedPropertyName;
        var indexClassName = $"{indexPropertyName}Index";
        var indexType = aggregatedIndex.Type == IndexType.GlobalSecondaryIndex 
            ? "Global Secondary Index" 
            : "Local Secondary Index";
        
        // Find the first entity with this index to get projection expression
        var firstEntityWithIndex = aggregatedIndex.ReferencingEntities.FirstOrDefault();
        var firstIndex = firstEntityWithIndex?.Indexes.FirstOrDefault(i => 
            string.Equals(i.IndexName, aggregatedIndex.DynamoDbIndexName, StringComparison.OrdinalIgnoreCase));
        
        var projectionExpression = firstEntityWithIndex != null && firstIndex != null 
            ? BuildProjectionExpression(firstEntityWithIndex, firstIndex) 
            : string.Empty;
        
        // Get the projection type for non-generic Query methods
        var projectionType = GetProjectionTypeForAggregatedIndex(aggregatedIndex, entities);
        
        // Get a representative entity for examples in documentation
        var representativeEntity = firstEntityWithIndex ?? entities.FirstOrDefault();
        var entityClassName = representativeEntity?.ClassName ?? "Entity";
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Typed index class for {aggregatedIndex.DynamoDbIndexName} {indexType}.");
        sb.AppendLine($"    /// Inherits from <see cref=\"DynamoDbIndex\"/> and provides type-safe query operations");
        sb.AppendLine($"    /// with LINQ expression support and automatic index configuration.");
        sb.AppendLine($"    /// Supports GSI overloading - can query different entity types from the same index.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <remarks>");
        sb.AppendLine($"    /// <para>This nested class provides strongly-typed access to the {aggregatedIndex.DynamoDbIndexName} index.</para>");
        sb.AppendLine($"    /// <para>Index Name: {aggregatedIndex.DynamoDbIndexName}</para>");
        sb.AppendLine($"    /// <para>Partition Key: {aggregatedIndex.PartitionKeyProperty}</para>");
        if (!string.IsNullOrEmpty(aggregatedIndex.SortKeyProperty))
        {
            sb.AppendLine($"    /// <para>Sort Key: {aggregatedIndex.SortKeyProperty}</para>");
        }
        
        // List referencing entities
        var entityNames = aggregatedIndex.ReferencingEntities.Select(e => e.ClassName).ToList();
        if (entityNames.Count > 0)
        {
            sb.AppendLine($"    /// <para>Referenced by: {string.Join(", ", entityNames)}</para>");
        }
        
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// Benefits of using this typed index class:");
        sb.AppendLine($"    /// - Inherits from DynamoDbIndex for extensibility via partial classes");
        sb.AppendLine($"    /// - Automatic index name configuration (no need to specify \"{aggregatedIndex.DynamoDbIndexName}\" manually)");
        sb.AppendLine($"    /// - Type-safe query building with compile-time checking");
        sb.AppendLine($"    /// - LINQ expression support for key conditions and filters");
        sb.AppendLine($"    /// - Automatic projection expression configuration if defined");
        sb.AppendLine($"    /// - Support for querying multiple entity types from the same index (GSI overloading)");
        sb.AppendLine($"    /// </remarks>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// // Access via table property");
        sb.AppendLine($"    /// var index = table.{indexPropertyName};");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // Query with string expression");
        sb.AppendLine($"    /// var results = await table.{indexPropertyName}.Query&lt;{entityClassName}&gt;(");
        sb.AppendLine($"    ///     \"{aggregatedIndex.PartitionKeyProperty} = {{{{0}}}}\", value)");
        sb.AppendLine($"    ///     .ToListAsync();");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // Query with LINQ expression");
        sb.AppendLine($"    /// var results = await table.{indexPropertyName}.Query&lt;{entityClassName}&gt;(");
        sb.AppendLine($"    ///     x => x.{aggregatedIndex.PartitionKeyProperty} == value)");
        sb.AppendLine($"    ///     .ToListAsync();");
        if (!string.IsNullOrEmpty(aggregatedIndex.SortKeyProperty))
        {
            sb.AppendLine($"    /// ");
            sb.AppendLine($"    /// // Query with composite key condition");
            sb.AppendLine($"    /// var results = await table.{indexPropertyName}.Query&lt;{entityClassName}&gt;(");
            sb.AppendLine($"    ///     x => x.{aggregatedIndex.PartitionKeyProperty} == value &amp;&amp; x.{aggregatedIndex.SortKeyProperty}.StartsWith(\"PREFIX\"))");
            sb.AppendLine($"    ///     .ToListAsync();");
        }
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// // GSI overloading - query different entity type");
        sb.AppendLine($"    /// var otherResults = await table.{indexPropertyName}.Query&lt;OtherEntity&gt;(");
        sb.AppendLine($"    ///     x => x.{aggregatedIndex.PartitionKeyProperty} == value)");
        sb.AppendLine($"    ///     .ToListAsync();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public partial class {indexClassName} : DynamoDbIndex");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {tableClassName} _table;");
        sb.AppendLine();
        
        // Constructor - calls base constructor
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Initializes a new instance of the {indexClassName}.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"table\">The parent table.</param>");
        if (!string.IsNullOrEmpty(projectionExpression))
        {
            sb.AppendLine($"        public {indexClassName}({tableClassName} table) : base(table, \"{aggregatedIndex.DynamoDbIndexName}\", \"{projectionExpression}\")");
        }
        else
        {
            sb.AppendLine($"        public {indexClassName}({tableClassName} table) : base(table, \"{aggregatedIndex.DynamoDbIndexName}\")");
        }
        sb.AppendLine($"        {{");
        sb.AppendLine($"            _table = table;");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic Query<T>() method - uses 'new' to hide base class method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder for this index with a specific entity type.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{aggregatedIndex.DynamoDbIndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured for this index.</returns>");
        sb.AppendLine($"        public new QueryRequestBuilder<T> Query<T>() where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return base.Query<T>();");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic Query<T>(string, params object[]) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with a key condition expression and specific entity type.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <param name=\"keyConditionExpression\">The key condition expression with format placeholders.</param>");
        sb.AppendLine($"        /// <param name=\"values\">The values to substitute into the expression.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured with the key condition.</returns>");
        sb.AppendLine($"        public new QueryRequestBuilder<T> Query<T>(string keyConditionExpression, params object[] values) where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return base.Query<T>(keyConditionExpression, values);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic LINQ expression Query<T>(Expression<Func<T, bool>>) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with a LINQ expression for the key condition.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured with the key condition.</returns>");
        sb.AppendLine($"        public QueryRequestBuilder<T> Query<T>(Expression<Func<T, bool>> keyCondition) where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<T>().Where(keyCondition);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic LINQ expression Query<T>(Expression, Expression) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with LINQ expressions for both key condition and filter.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <param name=\"filterCondition\">The LINQ expression representing the filter condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured with both key condition and filter.</returns>");
        sb.AppendLine($"        public QueryRequestBuilder<T> Query<T>(");
        sb.AppendLine($"            Expression<Func<T, bool>> keyCondition,");
        sb.AppendLine($"            Expression<Func<T, bool>> filterCondition) where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<T>().Where(keyCondition).WithFilter(filterCondition);");
        sb.AppendLine($"        }}");
        
        // Generate non-generic Query methods when a real projection type exists
        if (projectionType != null && projectionType != "HasProjection")
        {
            GenerateNonGenericQueryMethodsForAggregated(sb, aggregatedIndex, indexPropertyName, projectionType);
        }
        
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Generates non-generic Query methods for an aggregated index that default to the projection type.
    /// </summary>
    private static void GenerateNonGenericQueryMethodsForAggregated(
        StringBuilder sb, 
        AggregatedIndexModel aggregatedIndex, 
        string indexPropertyName, 
        string projectionType)
    {
        sb.AppendLine();
        
        // Non-generic Query() method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder for this index using the default projection type.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{projectionType}&gt; configured for this index.</returns>");
        sb.AppendLine($"        public QueryRequestBuilder<{projectionType}> Query()");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<{projectionType}>();");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Non-generic Query(Expression<Func<TProjection, bool>>) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with a LINQ expression for the key condition using the default projection type.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{projectionType}&gt; configured with the key condition.</returns>");
        sb.AppendLine($"        public QueryRequestBuilder<{projectionType}> Query(Expression<Func<{projectionType}, bool>> keyCondition)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<{projectionType}>(keyCondition);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Non-generic Query(Expression, Expression) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with LINQ expressions for both key condition and filter using the default projection type.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <param name=\"filterCondition\">The LINQ expression representing the filter condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{projectionType}&gt; configured with both key condition and filter.</returns>");
        sb.AppendLine($"        public QueryRequestBuilder<{projectionType}> Query(");
        sb.AppendLine($"            Expression<Func<{projectionType}, bool>> keyCondition,");
        sb.AppendLine($"            Expression<Func<{projectionType}, bool>> filterCondition)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<{projectionType}>(keyCondition, filterCondition);");
        sb.AppendLine($"        }}");
    }

    private static void GenerateTypedIndexClass(StringBuilder sb, EntityModel entity, IndexModel index, string tableClassName, bool isSingleEntityTable = false)
    {
        // Use ResolvedPropertyName which is either the custom Name or derived from IndexName
        var indexPropertyName = !string.IsNullOrEmpty(index.ResolvedPropertyName) 
            ? index.ResolvedPropertyName 
            : index.IndexName.Replace("-", "").Replace("_", "");
        var indexClassName = $"{indexPropertyName}Index";
        
        // For Keys Only projections, use the generated projection's ProjectionExpression
        // Otherwise, build the projection expression from entity properties
        var projectionExpression = index.RequiresKeysOnlyProjection
            ? string.Empty  // Will use static property reference instead
            : BuildProjectionExpression(entity, index);
        var keysOnlyProjectionName = $"{indexPropertyName}KeysProjection";
        
        var indexType = index.IsGsi ? "Global Secondary Index" : "Local Secondary Index";
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Typed index class for {index.IndexName} {indexType}.");
        sb.AppendLine($"    /// Inherits from <see cref=\"DynamoDbIndex\"/> and provides type-safe query operations");
        sb.AppendLine($"    /// with LINQ expression support and automatic index configuration.");
        sb.AppendLine($"    /// Supports GSI overloading - can query different entity types from the same index.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <remarks>");
        sb.AppendLine($"    /// <para>This nested class provides strongly-typed access to the {index.IndexName} index.</para>");
        sb.AppendLine($"    /// <para>Index Name: {index.IndexName}</para>");
        sb.AppendLine($"    /// <para>Partition Key: {index.PartitionKeyProperty}</para>");
        if (index.HasSortKey)
        {
            sb.AppendLine($"    /// <para>Sort Key: {index.SortKeyProperty}</para>");
        }
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// Benefits of using this typed index class:");
        sb.AppendLine($"    /// - Inherits from DynamoDbIndex for extensibility via partial classes");
        sb.AppendLine($"    /// - Automatic index name configuration (no need to specify \"{index.IndexName}\" manually)");
        sb.AppendLine($"    /// - Type-safe query building with compile-time checking");
        sb.AppendLine($"    /// - LINQ expression support for key conditions and filters");
        sb.AppendLine($"    /// - Automatic projection expression configuration if defined");
        sb.AppendLine($"    /// - Support for querying multiple entity types from the same index (GSI overloading)");
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
        sb.AppendLine($"    public partial class {indexClassName} : DynamoDbIndex");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {tableClassName} _table;");
        sb.AppendLine();
        
        // Constructor - calls base constructor
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Initializes a new instance of the {indexClassName}.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"table\">The parent table.</param>");
        if (index.RequiresKeysOnlyProjection)
        {
            // For Keys Only projections, use the static property reference from the generated record
            sb.AppendLine($"        public {indexClassName}({tableClassName} table) : base(table, \"{index.IndexName}\", {keysOnlyProjectionName}.ProjectionExpression)");
        }
        else if (!string.IsNullOrEmpty(projectionExpression))
        {
            sb.AppendLine($"        public {indexClassName}({tableClassName} table) : base(table, \"{index.IndexName}\", \"{projectionExpression}\")");
        }
        else
        {
            sb.AppendLine($"        public {indexClassName}({tableClassName} table) : base(table, \"{index.IndexName}\")");
        }
        sb.AppendLine($"        {{");
        sb.AppendLine($"            _table = table;");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic Query<T>() method - uses 'new' to hide base class method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder for this index with a specific entity type.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{index.IndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"T\">The entity type to query and deserialize results into.</typeparam>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;T&gt; configured for this index.</returns>");
        sb.AppendLine($"        /// <example>");
        sb.AppendLine($"        /// <code>");
        sb.AppendLine($"        /// // Query for an entity type stored in this index");
        sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query&lt;{entity.ClassName}&gt;()");
        sb.AppendLine($"        ///     .Where(\"gsi1pk = {{0}}\", \"VALUE\")");
        sb.AppendLine($"        ///     .ToListAsync();");
        sb.AppendLine($"        /// </code>");
        sb.AppendLine($"        /// </example>");
        sb.AppendLine($"        public new QueryRequestBuilder<T> Query<T>() where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return base.Query<T>();");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Generic Query<T>(string, params object[]) method - uses 'new' to hide base class method
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
        sb.AppendLine($"        public new QueryRequestBuilder<T> Query<T>(string keyConditionExpression, params object[] values) where T : class");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return base.Query<T>(keyConditionExpression, values);");
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
        
        // Generate non-generic Query methods when a projection type exists
        // Use DetermineIndexProjectionType to get the correct projection type based on single-entity table status
        var projectionType = DetermineIndexProjectionType(entity, index, isSingleEntityTable);
        if (projectionType != null)
        {
            GenerateNonGenericQueryMethods(sb, entity, index, indexPropertyName, projectionType);
        }
        
        sb.AppendLine("    }");
    }
    
    /// <summary>
    /// Generates non-generic Query methods that default to the projection type.
    /// These methods are only generated when a projection type is defined via [UseProjection].
    /// </summary>
    private static void GenerateNonGenericQueryMethods(StringBuilder sb, EntityModel entity, IndexModel index, string indexPropertyName, string projectionType)
    {
        sb.AppendLine();
        
        // Non-generic Query() method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder for this index using the default projection type.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{index.IndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{projectionType}&gt; configured for this index.</returns>");
        sb.AppendLine($"        /// <example>");
        sb.AppendLine($"        /// <code>");
        sb.AppendLine($"        /// // Query using default projection type");
        sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query()");
        sb.AppendLine($"        ///     .Where(\"gsi1pk = {{0}}\", \"VALUE\")");
        sb.AppendLine($"        ///     .ToListAsync();");
        sb.AppendLine($"        /// </code>");
        sb.AppendLine($"        /// </example>");
        sb.AppendLine($"        public QueryRequestBuilder<{projectionType}> Query()");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<{projectionType}>();");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Non-generic Query(Expression<Func<TProjection, bool>>) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with a LINQ expression for the key condition using the default projection type.");
        sb.AppendLine($"        /// Provides type-safe query building with compile-time checking of property access.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{index.IndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{projectionType}&gt; configured with the key condition.</returns>");
        sb.AppendLine($"        /// <example>");
        sb.AppendLine($"        /// <code>");
        sb.AppendLine($"        /// // Query with LINQ expression using default projection type");
        sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query(x => x.{index.PartitionKeyProperty} == \"VALUE\").ToListAsync();");
        sb.AppendLine($"        /// </code>");
        sb.AppendLine($"        /// </example>");
        sb.AppendLine($"        public QueryRequestBuilder<{projectionType}> Query(Expression<Func<{projectionType}, bool>> keyCondition)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<{projectionType}>(keyCondition);");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        
        // Non-generic Query(Expression, Expression) method
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates a new Query operation builder with LINQ expressions for both key condition and filter using the default projection type.");
        sb.AppendLine($"        /// Provides type-safe query building with compile-time checking of property access.");
        sb.AppendLine($"        /// The IndexName is automatically set to \"{index.IndexName}\".");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"keyCondition\">The LINQ expression representing the key condition.</param>");
        sb.AppendLine($"        /// <param name=\"filterCondition\">The LINQ expression representing the filter condition.</param>");
        sb.AppendLine($"        /// <returns>A QueryRequestBuilder&lt;{projectionType}&gt; configured with both key condition and filter.</returns>");
        sb.AppendLine($"        /// <example>");
        sb.AppendLine($"        /// <code>");
        sb.AppendLine($"        /// // Query with key condition and filter using default projection type");
        sb.AppendLine($"        /// var results = await table.{indexPropertyName}.Query(");
        sb.AppendLine($"        ///     x => x.{index.PartitionKeyProperty} == \"VALUE\",");
        sb.AppendLine($"        ///     x => x.Status == \"ACTIVE\"");
        sb.AppendLine($"        /// ).ToListAsync();");
        sb.AppendLine($"        /// </code>");
        sb.AppendLine($"        /// </example>");
        sb.AppendLine($"        public QueryRequestBuilder<{projectionType}> Query(");
        sb.AppendLine($"            Expression<Func<{projectionType}, bool>> keyCondition,");
        sb.AppendLine($"            Expression<Func<{projectionType}, bool>> filterCondition)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            return Query<{projectionType}>(keyCondition, filterCondition);");
        sb.AppendLine($"        }}");
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

    /// <summary>
    /// Gets the parameter type for a key property in generated accessor methods.
    /// When a key has a prefix or is computed, the parameter type is always "string"
    /// because the caller supplies the fully-formed prefixed/computed key value.
    /// When a key has no prefix and is not computed, the native .NET type is used.
    /// </summary>
    private static string GetKeyParameterType(PropertyModel key)
    {
        var hasPrefix = !string.IsNullOrEmpty(key.KeyFormat?.Prefix);
        var isComputed = key.IsComputed;
        
        if (hasPrefix || isComputed)
        {
            return "string";
        }
        
        return GetCSharpType(key.PropertyType);
    }

    /// <summary>
    /// Determines whether a key property requires the SetKey approach instead of WithKey.
    /// Returns true when the key has a non-string type, no prefix, and is not computed.
    /// </summary>
    private static bool NeedsSetKeyApproach(PropertyModel key)
    {
        var csharpType = GetCSharpType(key.PropertyType);
        var isStringType = csharpType is "string" or "String" or "System.String";
        var hasPrefix = !string.IsNullOrEmpty(key.KeyFormat?.Prefix);
        var isComputed = key.IsComputed;

        return !isStringType && !hasPrefix && !isComputed;
    }

    /// <summary>
    /// Generates SetKey lambda code for a single key property.
    /// Example output: .SetKey(k => { k["pk"] = new AttributeValue { N = pK.ToString() }; })
    /// </summary>
    private static string GenerateSetKeySingle(PropertyModel key, string paramName)
    {
        var avExpression = MapperGenerator.GetToAttributeValueExpression(key, paramName);
        return $".SetKey(k => {{ k[\"{key.AttributeName}\"] = {avExpression}; }})";
    }

    /// <summary>
    /// Generates SetKey lambda code for composite keys (partition + sort).
    /// Example output: .SetKey(k => { k["PK"] = new AttributeValue { S = pK }; k["SK"] = new AttributeValue { S = sK.ToString() }; })
    /// </summary>
    private static string GenerateSetKeyComposite(PropertyModel partitionKey, string pkParamName, PropertyModel sortKey, string skParamName)
    {
        var pkAvExpression = MapperGenerator.GetToAttributeValueExpression(partitionKey, pkParamName);
        var skAvExpression = MapperGenerator.GetToAttributeValueExpression(sortKey, skParamName);
        return $".SetKey(k => {{ k[\"{partitionKey.AttributeName}\"] = {pkAvExpression}; k[\"{sortKey.AttributeName}\"] = {skAvExpression}; }})";
    }

    /// <summary>
    /// Generates a KeyPrefixHelper.ApplyKeyPrefix assignment statement for a key property.
    /// Returns null if the key does not qualify for prefix application (non-string or no prefix configured).
    /// </summary>
    /// <param name="key">The key property to evaluate.</param>
    /// <param name="paramName">The parameter name used in the method signature.</param>
    /// <param name="effectiveVarName">The variable name to assign the effective value to.</param>
    /// <returns>A code statement like "var effectivePk = KeyPrefixHelper.ApplyKeyPrefix(pK, \"ORDER\", \"#\", resolvedMode);", or null if not applicable.</returns>
    private static string? GenerateKeyPrefixApplication(PropertyModel key, string paramName, string effectiveVarName)
    {
        // Only apply to string keys with a configured prefix
        if (key.PropertyType != "string" && key.PropertyType != "String" && key.PropertyType != "System.String")
            return null;
        
        var prefix = key.KeyFormat?.Prefix;
        if (string.IsNullOrEmpty(prefix))
            return null;
        
        var separator = key.KeyFormat?.Separator ?? "#";
        return $"var {effectiveVarName} = KeyPrefixHelper.ApplyKeyPrefix({paramName}, \"{prefix}\", \"{separator}\", resolvedMode);";
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

    /// <summary>
    /// Generates generic Scan&lt;TEntity&gt;() method if any entity in the table has [Scannable].
    /// This replaces the hardcoded Scan&lt;TEntity&gt;() method that was removed from DynamoDbTableBase.
    /// If any entity opts in to scanning, the table gets the generic method.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="entities">The list of entities in this table.</param>
    private static void GenerateGenericScanMethods(StringBuilder sb, List<EntityModel> entities)
    {
        // If any entity has [Scannable], generate the generic Scan<TEntity>() method
        var hasScannableEntity = entities.Any(e => e.IsScannable);
        
        if (!hasScannableEntity)
        {
            return;
        }
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new Scan operation builder for this table.");
        sb.AppendLine($"    /// ");
        sb.AppendLine($"    /// WARNING: Scan operations read every item in a table or index and can be very expensive.");
        sb.AppendLine($"    /// Use Query operations instead whenever possible.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <typeparam name=\"TEntity\">The entity or projection type to scan for. Must implement IReadOnlyEntity.</typeparam>");
        sb.AppendLine($"    /// <returns>A ScanRequestBuilder&lt;TEntity&gt; configured for this table.</returns>");
        sb.AppendLine($"    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class, IReadOnlyEntity =>");
        sb.AppendLine($"        new ScanRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates core properties that were previously inherited from DynamoDbTableBase.
    /// These include DynamoDbClient, Name, Options, Logger, and FieldEncryptor.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    private static void GenerateCoreProperties(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the DynamoDB client instance used for executing operations.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public IAmazonDynamoDB DynamoDbClient { get; private init; }");
        sb.AppendLine();
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the name of the DynamoDB table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public string Name { get; private init; }");
        sb.AppendLine();
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the configuration options for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    protected FluentDynamoDbOptions Options { get; private init; }");
        sb.AppendLine();
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the logger for DynamoDB operations.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    protected IDynamoDbLogger Logger { get; private init; }");
        sb.AppendLine();
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the field encryptor for encrypting and decrypting sensitive properties.");
        sb.AppendLine("    /// Returns null if encryption is not configured for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    protected IFieldEncryptor? FieldEncryptor { get; private init; }");
        sb.AppendLine();
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the field encryptor for this table.");
        sb.AppendLine("    /// This method is used internally by transaction builders to access the encryptor.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>The field encryptor, or null if encryption is not configured.</returns>");
        sb.AppendLine("    internal IFieldEncryptor? GetFieldEncryptor() => FieldEncryptor;");
        sb.AppendLine();
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the configuration options for this table.");
        sb.AppendLine("    /// Used by DynamoDbIndex to pass options to query builders.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>The FluentDynamoDbOptions instance used by this table.</returns>");
        sb.AppendLine("    public FluentDynamoDbOptions? GetOptions() => Options;");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates base operation methods that were previously inherited from DynamoDbTableBase.
    /// These include Query&lt;T&gt;(), Get&lt;T&gt;(), Put&lt;T&gt;(), Update&lt;T&gt;(), Delete&lt;T&gt;(), ConditionCheck&lt;T&gt;(),
    /// PutAsync&lt;T&gt;(), ExecutePartiQL&lt;T&gt;(), and direct SDK request methods.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    private static void GenerateBaseOperationMethods(StringBuilder sb)
    {
        // Query<TEntity>() method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new Query operation builder for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <typeparam name=\"TEntity\">The entity or projection type to query. Must implement IReadOnlyEntity.</typeparam>");
        sb.AppendLine("    /// <returns>A QueryRequestBuilder configured for this table.</returns>");
        sb.AppendLine("    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class, IReadOnlyEntity =>");
        sb.AppendLine("        new QueryRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);");
        sb.AppendLine();
        
        // Query<TEntity>(string, params object[]) method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new Query operation builder with a key condition expression.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public QueryRequestBuilder<TEntity> Query<TEntity>(string keyConditionExpression, params object[] values) where TEntity : class, IReadOnlyEntity");
        sb.AppendLine("    {");
        sb.AppendLine("        var builder = Query<TEntity>();");
        sb.AppendLine("        return WithConditionExpressionExtensions.Where(builder, keyConditionExpression, values);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Get<TEntity>() method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new GetItem operation builder for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public virtual GetItemRequestBuilder<TEntity> Get<TEntity>() where TEntity : class, IReadOnlyEntity =>");
        sb.AppendLine("        new GetItemRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);");
        sb.AppendLine();
        
        // Update<TEntity>() method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new UpdateItem operation builder for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public virtual UpdateItemRequestBuilder<TEntity> Update<TEntity>() where TEntity : class, IDynamoDbEntity =>");
        sb.AppendLine("        new UpdateItemRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);");
        sb.AppendLine();
        
        // Delete<TEntity>() method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new DeleteItem operation builder for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public virtual DeleteItemRequestBuilder<TEntity> Delete<TEntity>() where TEntity : class, IDynamoDbEntity =>");
        sb.AppendLine("        new DeleteItemRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);");
        sb.AppendLine();
        
        // Put<TEntity>() method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new PutItem operation builder for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public PutItemRequestBuilder<TEntity> Put<TEntity>() where TEntity : class, IDynamoDbEntity =>");
        sb.AppendLine("        new PutItemRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);");
        sb.AppendLine();
        
        // ConditionCheck<TEntity>() method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a new ConditionCheck operation builder for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public ConditionCheckBuilder<TEntity> ConditionCheck<TEntity>() where TEntity : class =>");
        sb.AppendLine("        new ConditionCheckBuilder<TEntity>(DynamoDbClient, Name, Options);");
        sb.AppendLine();
        
        // PutAsync<TEntity>(entity) method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Express-route method that executes a PutItem operation.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async System.Threading.Tasks.Task PutAsync<TEntity>(TEntity entity, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("    {");
        sb.AppendLine("        var builder = Put<TEntity>();");
        sb.AppendLine("        builder = EntityExecuteAsyncExtensions.WithItem(builder, entity);");
        sb.AppendLine("        await EntityExecuteAsyncExtensions.PutAsync(builder, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // PutAsync<TEntity>(Dictionary) method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Express-route method that executes a PutItem operation with a raw attribute dictionary.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async System.Threading.Tasks.Task PutAsync<TEntity>(Dictionary<string, AttributeValue> item, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("    {");
        sb.AppendLine("        var builder = Put<TEntity>().WithItem(item);");
        sb.AppendLine("        await EntityExecuteAsyncExtensions.PutAsync(builder, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // ExecutePartiQL<TEntity>() method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a PartiQL request builder for executing SQL-like queries.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public PartiQLRequestBuilder<TEntity> ExecutePartiQL<TEntity>(string statement, params object[] parameters)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("    {");
        sb.AppendLine("        return new PartiQLRequestBuilder<TEntity>(DynamoDbClient, Options).WithStatement(statement, parameters);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // ExecutePartiQL() method (DynamicEntity)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a PartiQL request builder for executing SQL-like queries with DynamicEntity.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public PartiQLRequestBuilder<DynamicEntity> ExecutePartiQL(string statement, params object[] parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        return ExecutePartiQL<DynamicEntity>(statement, parameters);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Direct SDK request methods
        GenerateDirectSdkRequestMethods(sb);
    }

    /// <summary>
    /// Generates direct SDK request methods that were previously in DynamoDbTableBase.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    private static void GenerateDirectSdkRequestMethods(StringBuilder sb)
    {
        // Get<TEntity>(GetItemRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a GetItem operation builder configured with a pre-built SDK request.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public GetItemRequestBuilder<TEntity> Get<TEntity>(GetItemRequest request) where TEntity : class, IReadOnlyEntity");
        sb.AppendLine("        => Get<TEntity>().WithRequest(request);");
        sb.AppendLine();
        
        // GetAsync<TEntity>(GetItemRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Executes a pre-built GetItemRequest and hydrates the result to an entity.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async System.Threading.Tasks.Task<TEntity?> GetAsync<TEntity>(GetItemRequest request, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => await EntityExecuteAsyncExtensions.GetItemAsync(Get<TEntity>(request), cancellationToken);");
        sb.AppendLine();
        
        // Query<TEntity>(QueryRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a Query operation builder configured with a pre-built SDK request.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public QueryRequestBuilder<TEntity> Query<TEntity>(QueryRequest request) where TEntity : class, IReadOnlyEntity");
        sb.AppendLine("        => Query<TEntity>().WithRequest(request);");
        sb.AppendLine();
        
        // QueryAsync<TEntity>(QueryRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Executes a pre-built QueryRequest and hydrates the results to entities.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async System.Threading.Tasks.Task<List<TEntity>> QueryAsync<TEntity>(QueryRequest request, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => await EntityExecuteAsyncExtensions.ToListAsync(Query<TEntity>(request), cancellationToken);");
        sb.AppendLine();
        
        // Scan<TEntity>(ScanRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a Scan operation builder configured with a pre-built SDK request.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public ScanRequestBuilder<TEntity> Scan<TEntity>(ScanRequest request) where TEntity : class, IReadOnlyEntity");
        sb.AppendLine("        => new ScanRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name).WithRequest(request);");
        sb.AppendLine();
        
        // ScanAsync<TEntity>(ScanRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Executes a pre-built ScanRequest and hydrates the results to entities.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async System.Threading.Tasks.Task<List<TEntity>> ScanAsync<TEntity>(ScanRequest request, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => await EntityExecuteAsyncExtensions.ToListAsync(Scan<TEntity>(request), cancellationToken);");
        sb.AppendLine();
        
        // Put<TEntity>(PutItemRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a PutItem operation builder configured with a pre-built SDK request.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public PutItemRequestBuilder<TEntity> Put<TEntity>(PutItemRequest request) where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => Put<TEntity>().WithRequest(request);");
        sb.AppendLine();
        
        // PutAsync<TEntity>(PutItemRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Executes a pre-built PutItemRequest.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async System.Threading.Tasks.Task PutAsync<TEntity>(PutItemRequest request, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => await EntityExecuteAsyncExtensions.PutAsync(Put<TEntity>(request), cancellationToken);");
        sb.AppendLine();
        
        // Update<TEntity>(UpdateItemRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates an UpdateItem operation builder configured with a pre-built SDK request.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public UpdateItemRequestBuilder<TEntity> Update<TEntity>(UpdateItemRequest request) where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => Update<TEntity>().WithRequest(request);");
        sb.AppendLine();
        
        // UpdateAsync<TEntity>(UpdateItemRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Executes a pre-built UpdateItemRequest.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async System.Threading.Tasks.Task UpdateAsync<TEntity>(UpdateItemRequest request, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => await EntityExecuteAsyncExtensions.UpdateAsync(Update<TEntity>(request), cancellationToken);");
        sb.AppendLine();
        
        // Delete<TEntity>(DeleteItemRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a DeleteItem operation builder configured with a pre-built SDK request.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public DeleteItemRequestBuilder<TEntity> Delete<TEntity>(DeleteItemRequest request) where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => Delete<TEntity>().WithRequest(request);");
        sb.AppendLine();
        
        // DeleteAsync<TEntity>(DeleteItemRequest)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Executes a pre-built DeleteItemRequest.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async System.Threading.Tasks.Task DeleteAsync<TEntity>(DeleteItemRequest request, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TEntity : class, IDynamoDbEntity");
        sb.AppendLine("        => await EntityExecuteAsyncExtensions.DeleteAsync(Delete<TEntity>(request), cancellationToken);");
        sb.AppendLine();
    }
}
