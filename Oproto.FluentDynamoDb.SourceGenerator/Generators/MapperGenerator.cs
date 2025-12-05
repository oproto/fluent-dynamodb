using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates entity mapping code for converting between C# objects and DynamoDB AttributeValue dictionaries.
/// This is the single, consolidated source of truth for all entity mapping code generation.
/// </summary>
/// <remarks>
/// <para><strong>Architecture:</strong></para>
/// <para>
/// MapperGenerator is responsible for generating all entity mapping logic including:
/// - ToDynamoDb: Converts C# entities to DynamoDB AttributeValue dictionaries
/// - FromDynamoDb: Converts DynamoDB items back to C# entities (single and multi-item overloads)
/// - GetPartitionKey: Extracts partition key from DynamoDB items
/// - MatchesEntity: Determines if a DynamoDB item matches this entity type
/// - GetEntityMetadata: Provides metadata for future LINQ support
/// </para>
/// <para><strong>Performance Optimizations:</strong></para>
/// <list type="bullet">
/// <item><description>Pre-allocated dictionaries: Capacity calculated at compile time to avoid resizing</description></item>
/// <item><description>Aggressive inlining: Hot path methods marked with MethodImpl(AggressiveInlining)</description></item>
/// <item><description>Direct property access: No reflection overhead at runtime</description></item>
/// <item><description>Efficient type conversions: Optimized conversion logic for common types</description></item>
/// </list>
/// <para><strong>Why These Patterns:</strong></para>
/// <list type="bullet">
/// <item><description>Pre-allocated capacity: Dictionary resizing is expensive; knowing the exact size eliminates this cost</description></item>
/// <item><description>AggressiveInlining: Mapping is a hot path; inlining reduces call overhead</description></item>
/// <item><description>Partial class: Allows user code and generated code to coexist seamlessly</description></item>
/// <item><description>Static abstract methods: Enables generic constraints while maintaining AOT compatibility</description></item>
/// </list>
/// </remarks>
internal static class MapperGenerator
{
    /// <summary>
    /// Generates the complete entity implementation with IDynamoDbEntity interface methods.
    /// This is the single source of truth for all entity mapping code generation.
    /// </summary>
    /// <param name="entity">The entity model to generate mapping code for.</param>
    /// <returns>The generated C# source code.</returns>
    public static string GenerateEntityImplementation(EntityModel entity)
    {
        var sb = new StringBuilder();

        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);

        // All necessary using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Amazon.DynamoDBv2.Model;");
        sb.AppendLine("using Oproto.FluentDynamoDb;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Attributes;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Logging;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Entities;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Metadata;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Hydration;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Providers.Encryption;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Providers.BlobStorage;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Mapping;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Context;");
        
        // Add geospatial using statements if needed
        if (entity.HasGeospatialPackage)
        {
            sb.AppendLine("using Oproto.FluentDynamoDb.Geospatial.GeoHash;");
            sb.AppendLine("using Oproto.FluentDynamoDb.Geospatial.S2;");
            sb.AppendLine("using Oproto.FluentDynamoDb.Geospatial.H3;");
        }
        
        sb.AppendLine();

        // Namespace declaration
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");

        // XML documentation
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Generated implementation of IDynamoDbEntity for {entity.ClassName}.");
        sb.AppendLine($"    /// Provides automatic mapping between C# objects and DynamoDB AttributeValue dictionaries.");
        sb.AppendLine($"    /// Includes nested Keys and Fields classes for key building and field name constants.");
        sb.AppendLine($"    /// Table: {entity.TableName}");
        if (entity.IsMultiItemEntity)
        {
            sb.AppendLine($"    /// Multi-item entity: Supports entities that span multiple DynamoDB items.");
        }
        if (entity.Relationships.Length > 0)
        {
            sb.AppendLine($"    /// Related entities: {entity.Relationships.Length} relationship(s) defined.");
        }
        sb.AppendLine($"    /// </summary>");

        // Class declaration - partial class with IDynamoDbEntity interface
        sb.AppendLine($"    public partial class {entity.ClassName} : IDynamoDbEntity");
        sb.AppendLine("    {");

        // Check if entity has blob reference properties or encrypted properties
        var hasBlobReferences = entity.Properties.Any(p => p.AdvancedType?.IsBlobReference == true);
        var hasEncryptedProperties = entity.Properties.Any(p => p.Security?.IsEncrypted == true);

        // Generate all required interface methods
        if (hasBlobReferences || hasEncryptedProperties)
        {
            // For entities with blob references or encrypted properties, generate both:
            // 1. Stub synchronous methods (to satisfy interface) that throw NotSupportedException
            // 2. Actual async methods that handle blob storage and/or encryption
            GenerateToDynamoDbStubMethod(sb, entity);
            GenerateFromDynamoDbSingleStubMethod(sb, entity);
            GenerateFromDynamoDbMultiStubMethod(sb, entity);
            
            GenerateToDynamoDbAsyncMethod(sb, entity);
            GenerateFromDynamoDbSingleAsyncMethod(sb, entity);
            GenerateFromDynamoDbMultiAsyncMethod(sb, entity);
        }
        else
        {
            // Generate synchronous methods for entities without blob references or encryption
            GenerateToDynamoDbMethod(sb, entity);
            GenerateFromDynamoDbSingleMethod(sb, entity);
            GenerateFromDynamoDbMultiMethod(sb, entity);
        }

        GenerateGetPartitionKeyMethod(sb, entity);
        GenerateMatchesEntityMethod(sb, entity);
        GenerateGetEntityMetadataMethod(sb, entity);

        // Generate nested Keys class (skip for nested entities)
        if (!entity.TableName?.StartsWith("_entity_") == true)
        {
            KeysGenerator.GenerateNestedKeysClass(sb, entity);
        }

        // Generate nested Fields class
        FieldsGenerator.GenerateNestedFieldsClass(sb, entity);

        // Closing braces for class and namespace
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateToDynamoDbMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// High-performance conversion from entity to DynamoDB AttributeValue dictionary.");
        sb.AppendLine("        /// Optimized for minimal allocations and maximum throughput.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <typeparam name=\"TSelf\">The entity type implementing IDynamoDbEntity.</typeparam>");
        sb.AppendLine("        /// <param name=\"entity\">The entity instance to convert.</param>");
        sb.AppendLine("        /// <param name=\"options\">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>");
        sb.AppendLine("        /// <returns>A dictionary of DynamoDB AttributeValues representing the entity.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        
        // Generate entry logging
        sb.Append(LoggingCodeGenerator.GenerateToDynamoDbEntryLogging(entity.ClassName));
        sb.AppendLine();
        
        sb.AppendLine($"            if (entity is not {entity.ClassName} typedEntity)");
        sb.AppendLine($"                throw new ArgumentException($\"Expected {entity.ClassName}, got {{entity.GetType().Name}}\", nameof(entity));");
        sb.AppendLine();

        // Wrap entire mapping operation in try-catch
        sb.AppendLine("            try");
        sb.AppendLine("            {");

        // Pre-compute capacity to avoid dictionary resizing (performance optimization)
        var attributeCount = entity.Properties.Count(p => p.HasAttributeMapping);
        sb.AppendLine($"                // Pre-allocate dictionary with exact capacity to avoid resizing");
        sb.AppendLine($"                var item = new Dictionary<string, AttributeValue>({attributeCount});");
        sb.AppendLine();

        // Generate computed key logic before mapping
        var computedProperties = entity.Properties.Where(p => p.IsComputed).ToArray();
        if (computedProperties.Length > 0)
        {
            sb.AppendLine("                // Compute composite keys before mapping");
            foreach (var computedProperty in computedProperties)
            {
                GenerateComputedKeyLogic(sb, computedProperty);
            }
            sb.AppendLine();
        }

        // Generate property mappings for all properties
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GeneratePropertyToAttributeValue(sb, property, entity);
        }

        sb.AppendLine();
        
        // Generate exit logging
        sb.Append(LoggingCodeGenerator.GenerateToDynamoDbExitLogging(entity.ClassName, "item"));
        sb.AppendLine();
        
        sb.AppendLine("                return item;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        
        // Generate error logging
        sb.Append(LoggingCodeGenerator.GenerateMappingErrorLogging(entity.ClassName, "", "ex"));
        
        sb.AppendLine("                throw;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GenerateToDynamoDbStubMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        
        var hasBlobReferences = entity.Properties.Any(p => p.AdvancedType?.IsBlobReference == true);
        var hasEncryptedProperties = entity.Properties.Any(p => p.Security?.IsEncrypted == true);
        
        if (hasBlobReferences && hasEncryptedProperties)
        {
            sb.AppendLine("        /// Stub method for interface compliance. This entity has blob references and encrypted properties and requires async methods.");
        }
        else if (hasEncryptedProperties)
        {
            sb.AppendLine("        /// Stub method for interface compliance. This entity has encrypted properties and requires async methods.");
        }
        else
        {
            sb.AppendLine("        /// Stub method for interface compliance. This entity has blob references and requires async methods.");
        }
        
        sb.AppendLine("        /// Use ToDynamoDbAsync instead.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        
        if (hasBlobReferences && hasEncryptedProperties)
        {
            sb.AppendLine($"            throw new NotSupportedException(");
            sb.AppendLine($"                \"{entity.ClassName} has blob reference and encrypted properties and requires async methods. \" +");
            sb.AppendLine($"                \"Use ToDynamoDbAsync with an IBlobStorageProvider and IFieldEncryptor instead.\");");
        }
        else if (hasEncryptedProperties)
        {
            sb.AppendLine($"            throw new NotSupportedException(");
            sb.AppendLine($"                \"{entity.ClassName} has encrypted properties and requires async methods. \" +");
            sb.AppendLine($"                \"Use ToDynamoDbAsync with an IFieldEncryptor instead.\");");
        }
        else
        {
            sb.AppendLine($"            throw new NotSupportedException(");
            sb.AppendLine($"                \"{entity.ClassName} has blob reference properties and requires async methods. \" +");
            sb.AppendLine($"                \"Use ToDynamoDbAsync with an IBlobStorageProvider instead.\");");
        }
        
        sb.AppendLine("        }");
    }

    private static void GenerateFromDynamoDbSingleStubMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Stub method for interface compliance. This entity has blob references and requires async methods.");
        sb.AppendLine("        /// Use FromDynamoDbAsync instead.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        sb.AppendLine($"            throw new NotSupportedException(");
        sb.AppendLine($"                \"{entity.ClassName} has blob reference properties and requires async methods. \" +");
        sb.AppendLine($"                \"Use FromDynamoDbAsync with an IBlobStorageProvider instead.\");");
        sb.AppendLine("        }");
    }

    private static void GenerateFromDynamoDbMultiStubMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Stub method for interface compliance. This entity has blob references and requires async methods.");
        sb.AppendLine("        /// Use FromDynamoDbAsync instead.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        sb.AppendLine($"            throw new NotSupportedException(");
        sb.AppendLine($"                \"{entity.ClassName} has blob reference properties and requires async methods. \" +");
        sb.AppendLine($"                \"Use FromDynamoDbAsync with an IBlobStorageProvider instead.\");");
        sb.AppendLine("        }");
    }

    private static void GenerateToDynamoDbAsyncMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// High-performance async conversion from entity to DynamoDB AttributeValue dictionary.");
        sb.AppendLine("        /// Handles blob reference properties by storing data externally and saving references.");
        sb.AppendLine("        /// Handles encrypted properties by encrypting data before storage.");
        sb.AppendLine("        /// Optimized for minimal allocations and maximum throughput.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <typeparam name=\"TSelf\">The entity type implementing IDynamoDbEntity.</typeparam>");
        sb.AppendLine("        /// <param name=\"entity\">The entity instance to convert.</param>");
        sb.AppendLine("        /// <param name=\"blobProvider\">The blob storage provider for handling blob references.</param>");
        sb.AppendLine("        /// <param name=\"fieldEncryptor\">Optional field encryptor for handling encrypted properties.</param>");
        sb.AppendLine("        /// <param name=\"options\">Optional configuration options including logger. If null, default behavior is used.</param>");
        sb.AppendLine("        /// <param name=\"cancellationToken\">Cancellation token for async operations.</param>");
        sb.AppendLine("        /// <returns>A task that resolves to a dictionary of DynamoDB AttributeValues representing the entity.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static async Task<Dictionary<string, AttributeValue>> ToDynamoDbAsync<TSelf>(");
        sb.AppendLine("            TSelf entity,");
        sb.AppendLine("            IBlobStorageProvider blobProvider,");
        sb.AppendLine("            IFieldEncryptor? fieldEncryptor = null,");
        sb.AppendLine("            FluentDynamoDbOptions? options = null,");
        sb.AppendLine("            CancellationToken cancellationToken = default) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        
        // Generate entry logging
        sb.Append(LoggingCodeGenerator.GenerateToDynamoDbEntryLogging(entity.ClassName));
        sb.AppendLine();
        
        sb.AppendLine($"            if (entity is not {entity.ClassName} typedEntity)");
        sb.AppendLine($"                throw new ArgumentException($\"Expected {entity.ClassName}, got {{entity.GetType().Name}}\", nameof(entity));");
        sb.AppendLine();
        sb.AppendLine("            if (blobProvider == null)");
        sb.AppendLine("                throw new ArgumentNullException(nameof(blobProvider), \"Blob provider is required for entities with blob reference properties\");");
        sb.AppendLine();

        // Wrap entire mapping operation in try-catch
        sb.AppendLine("            try");
        sb.AppendLine("            {");

        // Pre-compute capacity to avoid dictionary resizing (performance optimization)
        var attributeCount = entity.Properties.Count(p => p.HasAttributeMapping);
        sb.AppendLine($"                // Pre-allocate dictionary with exact capacity to avoid resizing");
        sb.AppendLine($"                var item = new Dictionary<string, AttributeValue>({attributeCount});");
        sb.AppendLine();

        // Generate computed key logic before mapping
        var computedProperties = entity.Properties.Where(p => p.IsComputed).ToArray();
        if (computedProperties.Length > 0)
        {
            sb.AppendLine("                // Compute composite keys before mapping");
            foreach (var computedProperty in computedProperties)
            {
                GenerateComputedKeyLogic(sb, computedProperty);
            }
            sb.AppendLine();
        }

        // Generate property mappings for all properties
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GeneratePropertyToAttributeValueAsync(sb, property, entity);
        }

        sb.AppendLine();
        
        // Generate exit logging
        sb.Append(LoggingCodeGenerator.GenerateToDynamoDbExitLogging(entity.ClassName, "item"));
        sb.AppendLine();
        
        sb.AppendLine("                return item;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        
        // Generate error logging
        sb.Append(LoggingCodeGenerator.GenerateMappingErrorLogging(entity.ClassName, "", "ex"));
        
        sb.AppendLine("                throw;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GeneratePropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);

        // Handle encrypted properties - these require async methods
        if (property.Security?.IsEncrypted == true)
        {
            // Encrypted properties cannot be handled in synchronous methods
            // This should have been caught earlier and routed to async methods
            sb.AppendLine($"            // ERROR: {propertyName} is encrypted and requires async methods");
            sb.AppendLine($"            throw new NotSupportedException(\"Property {propertyName} is encrypted and requires async methods. Use ToDynamoDbAsync instead.\");");
            return;
        }

        // Handle GeoLocation properties (requires geospatial package)
        if (IsGeoLocationType(property.PropertyType) && entity.HasGeospatialPackage)
        {
            GenerateGeoLocationPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle TTL properties (Time-To-Live)
        if (property.AdvancedType?.IsTtl == true)
        {
            GenerateTtlPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle JSON blob properties
        if (property.AdvancedType?.IsJsonBlob == true)
        {
            GenerateJsonBlobPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle Map properties (Dictionary types)
        if (property.AdvancedType?.IsMap == true)
        {
            GenerateMapPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle collection properties differently for single-item entities
        if (property.IsCollection)
        {
            GenerateCollectionPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Check if property has format string
        // For DateTime: format string is always handled by GetToAttributeValueExpression (which calls GenerateDateTimeToAttributeValue)
        // For other types: format string requires GenerateFormattedPropertySerialization
        var hasFormatString = !string.IsNullOrEmpty(property.Format);
        var baseType = GetBaseType(property.PropertyType);
        var isDateTime = baseType is "DateTime" or "System.DateTime";
        var needsFormattedSerialization = hasFormatString && !isDateTime;

        // Handle nullable properties
        if (property.IsNullable)
        {
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null)");
            sb.AppendLine("            {");
            // Generate logging for basic property mapping
            sb.Append(LoggingCodeGenerator.GeneratePropertyMappingLogging(propertyName, GetBaseType(property.PropertyType), "ToDynamoDb"));
            
            // Use formatted serialization if format string is present (non-DateTime types)
            if (needsFormattedSerialization)
            {
                GenerateFormattedPropertySerialization(sb, property, $"typedEntity.{escapedPropertyName}.Value", attributeName);
            }
            else
            {
                sb.AppendLine($"                item[\"{attributeName}\"] = {GetToAttributeValueExpression(property, $"typedEntity.{escapedPropertyName}")};");
            }
            
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            // Generate logging for skipped properties
            sb.Append(LoggingCodeGenerator.GeneratePropertySkippedLogging(propertyName, "null value"));
            sb.AppendLine("            }");
        }
        else
        {
            // Generate logging for basic property mapping
            sb.Append(LoggingCodeGenerator.GeneratePropertyMappingLogging(propertyName, GetBaseType(property.PropertyType), "ToDynamoDb"));
            
            // Use formatted serialization if format string is present (non-DateTime types)
            if (needsFormattedSerialization)
            {
                GenerateFormattedPropertySerialization(sb, property, $"typedEntity.{escapedPropertyName}", attributeName);
            }
            else
            {
                sb.AppendLine($"            item[\"{attributeName}\"] = {GetToAttributeValueExpression(property, $"typedEntity.{escapedPropertyName}")};");
            }
        }
    }

    private static void GeneratePropertyToAttributeValueAsync(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);

        // Handle encrypted properties (must be before other handlers)
        if (property.Security?.IsEncrypted == true)
        {
            GenerateEncryptedPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle GeoLocation properties (requires geospatial package)
        if (IsGeoLocationType(property.PropertyType) && entity.HasGeospatialPackage)
        {
            GenerateGeoLocationPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle combined JSON blob + blob reference (serialize to JSON, then store as external blob)
        if (property.AdvancedType?.IsJsonBlob == true && property.AdvancedType?.IsBlobReference == true)
        {
            GenerateCombinedJsonBlobAndBlobReferenceToAttributeValue(sb, property, entity);
            return;
        }

        // Handle blob reference properties (async)
        if (property.AdvancedType?.IsBlobReference == true)
        {
            GenerateBlobReferencePropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle TTL properties (Time-To-Live)
        if (property.AdvancedType?.IsTtl == true)
        {
            GenerateTtlPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle JSON blob properties
        if (property.AdvancedType?.IsJsonBlob == true)
        {
            GenerateJsonBlobPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle Map properties (Dictionary types)
        if (property.AdvancedType?.IsMap == true)
        {
            GenerateMapPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Handle collection properties differently for single-item entities
        if (property.IsCollection)
        {
            GenerateCollectionPropertyToAttributeValue(sb, property, entity);
            return;
        }

        // Check if property has format string
        // For DateTime: format string is always handled by GetToAttributeValueExpression
        // For other types: format string requires GenerateFormattedPropertySerialization
        var hasFormatString = !string.IsNullOrEmpty(property.Format);
        var baseType = GetBaseType(property.PropertyType);
        var isDateTime = baseType is "DateTime" or "System.DateTime";
        var needsFormattedSerialization = hasFormatString && !isDateTime;

        // Handle nullable properties
        if (property.IsNullable)
        {
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null)");
            sb.AppendLine("            {");
            
            // Use formatted serialization if format string is present (non-DateTime types)
            if (needsFormattedSerialization)
            {
                GenerateFormattedPropertySerialization(sb, property, $"typedEntity.{escapedPropertyName}.Value", attributeName);
            }
            else
            {
                sb.AppendLine($"                item[\"{attributeName}\"] = {GetToAttributeValueExpression(property, $"typedEntity.{escapedPropertyName}")};");
            }
            
            sb.AppendLine("            }");
        }
        else
        {
            // Use formatted serialization if format string is present (non-DateTime types)
            if (needsFormattedSerialization)
            {
                GenerateFormattedPropertySerialization(sb, property, $"typedEntity.{escapedPropertyName}", attributeName);
            }
            else
            {
                sb.AppendLine($"            item[\"{attributeName}\"] = {GetToAttributeValueExpression(property, $"typedEntity.{escapedPropertyName}")};");
            }
        }
    }

    private static void GenerateBlobReferencePropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;
        var baseType = GetBaseType(propertyType);

        // Generate suggested key based on entity keys (declare before try block so it's accessible in catch)
        var partitionKeyProperty = entity.Properties.FirstOrDefault(p => p.IsPartitionKey);
        var sortKeyProperty = entity.Properties.FirstOrDefault(p => p.IsSortKey);

        sb.AppendLine($"            // Store blob reference property {propertyName} externally");
        sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                string suggestedKey;");
        if (partitionKeyProperty != null)
        {
            if (sortKeyProperty != null)
            {
                sb.AppendLine($"                suggestedKey = $\"{{typedEntity.{partitionKeyProperty.PropertyName}}}/{{typedEntity.{sortKeyProperty.PropertyName}}}/{propertyName}\";");
            }
            else
            {
                sb.AppendLine($"                suggestedKey = $\"{{typedEntity.{partitionKeyProperty.PropertyName}}}/{propertyName}\";");
            }
        }
        else
        {
            sb.AppendLine($"                suggestedKey = $\"{propertyName}/{{Guid.NewGuid()}}\";");
        }
        
        // Generate logging for blob reference operation
        sb.Append(LoggingCodeGenerator.GenerateBlobReferenceLogging(propertyName, "suggestedKey", "Store"));
        
        sb.AppendLine("                try");
        sb.AppendLine("                {");

        // Convert property to stream based on type
        if (baseType == "byte[]" || baseType == "System.Byte[]")
        {
            // byte[] - convert to MemoryStream
            sb.AppendLine($"                    using var stream = new MemoryStream(typedEntity.{escapedPropertyName});");
        }
        else if (baseType == "Stream" || baseType == "System.IO.Stream" || baseType == "MemoryStream")
        {
            // Already a stream - use directly
            sb.AppendLine($"                    var stream = typedEntity.{escapedPropertyName};");
        }
        else if (baseType == "string" || baseType == "System.String")
        {
            // string - convert to UTF8 bytes then stream
            sb.AppendLine($"                    var bytes = System.Text.Encoding.UTF8.GetBytes(typedEntity.{escapedPropertyName});");
            sb.AppendLine("                    using var stream = new MemoryStream(bytes);");
        }
        else
        {
            // Complex type - serialize to JSON first, then to stream using runtime-configured serializer
            sb.AppendLine($"                    // Serialize complex type to JSON");
            sb.AppendLine("                    if (options?.JsonSerializer == null)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new InvalidOperationException(");
            sb.AppendLine($"                            \"Property '{propertyName}' is a complex type stored as blob reference but no JSON serializer is configured. \" +");
            sb.AppendLine($"                            \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    var json = options.JsonSerializer.Serialize(typedEntity.{escapedPropertyName});");
            sb.AppendLine("                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);");
            sb.AppendLine("                    using var stream = new MemoryStream(bytes);");
        }
        if (partitionKeyProperty != null)
        {
            if (sortKeyProperty != null)
            {
                sb.AppendLine($"                    suggestedKey = $\"{{typedEntity.{partitionKeyProperty.PropertyName}}}/{{typedEntity.{sortKeyProperty.PropertyName}}}/{propertyName}\";");
            }
            else
            {
                sb.AppendLine($"                    suggestedKey = $\"{{typedEntity.{partitionKeyProperty.PropertyName}}}/{propertyName}\";");
            }
        }
        else
        {
            sb.AppendLine($"                    suggestedKey = $\"{propertyName}/{{Guid.NewGuid()}}\";");
        }

        // Store blob and save reference
        sb.AppendLine("                    var reference = await blobProvider.StoreAsync(stream, suggestedKey, cancellationToken);");
        sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ S = reference }};");

        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        
        // Generate error logging for blob storage
        sb.Append(LoggingCodeGenerator.GenerateBlobStorageErrorLogging(propertyName, "suggestedKey", "Store", "ex"));
        
        sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        new AttributeValue {{ S = \"<blob data>\" }},");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex)");
        sb.AppendLine($"                        .WithContext(\"BlobProviderType\", blobProvider.GetType().Name)");
        sb.AppendLine($"                        .WithContext(\"SuggestedKey\", suggestedKey)");
        sb.AppendLine($"                        .WithContext(\"Operation\", \"BlobStorage\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateCombinedJsonBlobAndBlobReferenceToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;
        var baseType = GetBaseType(propertyType);
        var serializerType = property.AdvancedType?.JsonSerializerType;

        // Generate suggested key based on entity keys (declare before try block so it's accessible in catch)
        var partitionKeyProperty = entity.Properties.FirstOrDefault(p => p.IsPartitionKey);
        var sortKeyProperty = entity.Properties.FirstOrDefault(p => p.IsSortKey);

        sb.AppendLine($"            // Combined JSON blob + blob reference: serialize to JSON, then store as external blob");
        sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                string suggestedKey;");
        if (partitionKeyProperty != null)
        {
            if (sortKeyProperty != null)
            {
                sb.AppendLine($"                suggestedKey = $\"{{typedEntity.{partitionKeyProperty.PropertyName}}}/{{typedEntity.{sortKeyProperty.PropertyName}}}/{propertyName}.json\";");
            }
            else
            {
                sb.AppendLine($"                suggestedKey = $\"{{typedEntity.{partitionKeyProperty.PropertyName}}}/{propertyName}.json\";");
            }
        }
        else
        {
            sb.AppendLine($"                suggestedKey = $\"{propertyName}/{{Guid.NewGuid()}}.json\";");
        }
        
        sb.AppendLine("                try");
        sb.AppendLine("                {");

        // Step 1: Serialize property to JSON using runtime-configured serializer
        sb.AppendLine($"                    // Step 1: Serialize property to JSON");
        sb.AppendLine("                    if (options?.JsonSerializer == null)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        throw new InvalidOperationException(");
        sb.AppendLine($"                            \"Property '{propertyName}' has [JsonBlob] attribute but no JSON serializer is configured. \" +");
        sb.AppendLine($"                            \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
        sb.AppendLine("                    }");
        sb.AppendLine($"                    var json = options.JsonSerializer.Serialize(typedEntity.{escapedPropertyName});");

        // Step 2: Convert JSON string to stream
        sb.AppendLine();
        sb.AppendLine($"                    // Step 2: Convert JSON string to stream");
        sb.AppendLine("                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);");
        sb.AppendLine("                    using var stream = new MemoryStream(bytes);");

        // Step 3: Store blob and save reference in DynamoDB
        sb.AppendLine();
        sb.AppendLine($"                    // Step 3: Store JSON blob externally and save reference");
        sb.AppendLine("                    var reference = await blobProvider.StoreAsync(stream, suggestedKey, cancellationToken);");
        sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ S = reference }};");

        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        
        // Generate error logging for combined JSON blob + blob storage
        sb.Append(LoggingCodeGenerator.GenerateBlobStorageErrorLogging(propertyName, "suggestedKey", "Store", "ex"));
        
        sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        new AttributeValue {{ S = \"<json blob data>\" }},");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex)");
        sb.AppendLine($"                        .WithContext(\"CombinedJsonBlobAndBlobReference\", \"Failed during JSON serialization or blob storage\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateTtlPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;
        var baseType = GetBaseType(propertyType);

        sb.AppendLine($"            // Convert TTL property {propertyName} to Unix epoch seconds");
        // Generate logging for TTL conversion
        sb.Append(LoggingCodeGenerator.GenerateTtlConversionLogging(propertyName, "ToDynamoDb"));

        if (baseType == "DateTime" || baseType == "System.DateTime")
        {
            // DateTime TTL conversion
            if (property.IsNullable)
            {
                sb.AppendLine($"            if (typedEntity.{escapedPropertyName}.HasValue)");
                sb.AppendLine("            {");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine("                    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);");
                sb.AppendLine("                    // Validate DateTime is within valid Unix epoch range");
                sb.AppendLine($"                    if (typedEntity.{escapedPropertyName}.Value.ToUniversalTime() < epoch)");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        throw new ArgumentOutOfRangeException(nameof(typedEntity.{escapedPropertyName}), $\"DateTime value {{typedEntity.{escapedPropertyName}.Value.ToUniversalTime()}} is before Unix epoch (1970-01-01). TTL values must be after 1970-01-01.\");");
                sb.AppendLine("                    }");
                sb.AppendLine($"                    if (typedEntity.{escapedPropertyName}.Value.ToUniversalTime() > new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Utc))");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        throw new ArgumentOutOfRangeException(nameof(typedEntity.{escapedPropertyName}), $\"DateTime value {{typedEntity.{escapedPropertyName}.Value.ToUniversalTime()}} exceeds maximum Unix timestamp (2038-01-19). Consider using DateTimeOffset for dates beyond 2038.\");");
                sb.AppendLine("                    }");
                sb.AppendLine($"                    var seconds = (long)(typedEntity.{escapedPropertyName}.Value.ToUniversalTime() - epoch).TotalSeconds;");
                sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ N = seconds.ToString() }};");
                sb.AppendLine("                }");
                sb.AppendLine("                catch (Exception ex)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    throw DynamoDbMappingException.PropertyConversionFailed(");
                sb.AppendLine($"                        typeof({entity.ClassName}),");
                sb.AppendLine($"                        \"{propertyName}\",");
                sb.AppendLine($"                        new AttributeValue {{ S = typedEntity.{escapedPropertyName}.Value.ToString(\"O\") }},");
                sb.AppendLine($"                        typeof({GetTypeForMetadata(propertyType)}),");
                sb.AppendLine("                        ex)");
                sb.AppendLine($"                        .WithContext(\"TtlValue\", typedEntity.{escapedPropertyName}.Value.ToString(\"O\"))");
                sb.AppendLine($"                        .WithContext(\"Operation\", \"TtlConversion\");");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine("            try");
                sb.AppendLine("            {");
                sb.AppendLine("                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);");
                sb.AppendLine($"                var dateTimeUtc = typedEntity.{escapedPropertyName}.ToUniversalTime();");
                sb.AppendLine("                // Validate DateTime is within valid Unix epoch range");
                sb.AppendLine("                if (dateTimeUtc < epoch)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    throw new ArgumentOutOfRangeException(nameof(typedEntity.{escapedPropertyName}), $\"DateTime value {{dateTimeUtc}} is before Unix epoch (1970-01-01). TTL values must be after 1970-01-01.\");");
                sb.AppendLine("                }");
                sb.AppendLine("                if (dateTimeUtc > new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Utc))");
                sb.AppendLine("                {");
                sb.AppendLine($"                    throw new ArgumentOutOfRangeException(nameof(typedEntity.{escapedPropertyName}), $\"DateTime value {{dateTimeUtc}} exceeds maximum Unix timestamp (2038-01-19). Consider using DateTimeOffset for dates beyond 2038.\");");
                sb.AppendLine("                }");
                sb.AppendLine("                var seconds = (long)(dateTimeUtc - epoch).TotalSeconds;");
                sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ N = seconds.ToString() }};");
                sb.AppendLine("            }");
                sb.AppendLine("            catch (Exception ex)");
                sb.AppendLine("            {");
                sb.AppendLine($"                throw DynamoDbMappingException.PropertyConversionFailed(");
                sb.AppendLine($"                    typeof({entity.ClassName}),");
                sb.AppendLine($"                    \"{propertyName}\",");
                sb.AppendLine($"                    new AttributeValue {{ S = typedEntity.{escapedPropertyName}.ToString(\"O\") }},");
                sb.AppendLine($"                    typeof({GetTypeForMetadata(propertyType)}),");
                sb.AppendLine("                    ex)");
                sb.AppendLine($"                    .WithContext(\"TtlValue\", typedEntity.{escapedPropertyName}.ToString(\"O\"))");
                sb.AppendLine($"                    .WithContext(\"Operation\", \"TtlConversion\");");
                sb.AppendLine("            }");
            }
        }
        else if (baseType == "DateTimeOffset" || baseType == "System.DateTimeOffset")
        {
            // DateTimeOffset TTL conversion
            if (property.IsNullable)
            {
                sb.AppendLine($"            if (typedEntity.{escapedPropertyName}.HasValue)");
                sb.AppendLine("            {");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine("                    // Validate DateTimeOffset is within valid Unix epoch range");
                sb.AppendLine($"                    if (typedEntity.{escapedPropertyName}.Value < DateTimeOffset.UnixEpoch)");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        throw new ArgumentOutOfRangeException(nameof(typedEntity.{escapedPropertyName}), $\"DateTimeOffset value {{typedEntity.{escapedPropertyName}.Value}} is before Unix epoch (1970-01-01). TTL values must be after 1970-01-01.\");");
                sb.AppendLine("                    }");
                sb.AppendLine($"                    var seconds = typedEntity.{escapedPropertyName}.Value.ToUnixTimeSeconds();");
                sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ N = seconds.ToString() }};");
                sb.AppendLine("                }");
                sb.AppendLine("                catch (Exception ex)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    throw DynamoDbMappingException.PropertyConversionFailed(");
                sb.AppendLine($"                        typeof({entity.ClassName}),");
                sb.AppendLine($"                        \"{propertyName}\",");
                sb.AppendLine($"                        new AttributeValue {{ S = typedEntity.{escapedPropertyName}.Value.ToString(\"O\") }},");
                sb.AppendLine($"                        typeof({GetTypeForMetadata(propertyType)}),");
                sb.AppendLine("                        ex)");
                sb.AppendLine($"                        .WithContext(\"TtlValue\", typedEntity.{escapedPropertyName}.Value.ToString(\"O\"))");
                sb.AppendLine($"                        .WithContext(\"Operation\", \"TtlConversion\");");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine("            try");
                sb.AppendLine("            {");
                sb.AppendLine("                // Validate DateTimeOffset is within valid Unix epoch range");
                sb.AppendLine($"                if (typedEntity.{escapedPropertyName} < DateTimeOffset.UnixEpoch)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    throw new ArgumentOutOfRangeException(nameof(typedEntity.{escapedPropertyName}), $\"DateTimeOffset value {{typedEntity.{escapedPropertyName}}} is before Unix epoch (1970-01-01). TTL values must be after 1970-01-01.\");");
                sb.AppendLine("                }");
                sb.AppendLine($"                var seconds = typedEntity.{escapedPropertyName}.ToUnixTimeSeconds();");
                sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ N = seconds.ToString() }};");
                sb.AppendLine("            }");
                sb.AppendLine("            catch (Exception ex)");
                sb.AppendLine("            {");
                sb.AppendLine($"                throw DynamoDbMappingException.PropertyConversionFailed(");
                sb.AppendLine($"                    typeof({entity.ClassName}),");
                sb.AppendLine($"                    \"{propertyName}\",");
                sb.AppendLine($"                    new AttributeValue {{ S = typedEntity.{escapedPropertyName}.ToString(\"O\") }},");
                sb.AppendLine($"                    typeof({GetTypeForMetadata(propertyType)}),");
                sb.AppendLine("                    ex)");
                sb.AppendLine($"                    .WithContext(\"TtlValue\", typedEntity.{escapedPropertyName}.ToString(\"O\"))");
                sb.AppendLine($"                    .WithContext(\"Operation\", \"TtlConversion\");");
                sb.AppendLine("            }");
            }
        }
    }

    private static void GenerateJsonBlobPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var baseType = GetBaseType(property.PropertyType);

        sb.AppendLine($"            // Serialize JSON blob property {propertyName}");

        if (property.IsNullable)
        {
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null)");
            sb.AppendLine("            {");
            // Generate null check for JSON serializer
            sb.AppendLine("                if (options?.JsonSerializer == null)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    throw new InvalidOperationException(");
            sb.AppendLine($"                        \"Property '{propertyName}' has [JsonBlob] attribute but no JSON serializer is configured. \" +");
            sb.AppendLine($"                        \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
            sb.AppendLine("                }");
            sb.AppendLine();
            // Generate logging for JSON blob operation
            sb.Append(LoggingCodeGenerator.GenerateJsonBlobLogging(propertyName, baseType, "RuntimeConfigured", "Serialization"));
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var json = options.JsonSerializer.Serialize(typedEntity.{escapedPropertyName});");
            sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ S = json }};");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            
            // Generate error logging for JSON serialization
            sb.Append(LoggingCodeGenerator.GenerateJsonSerializationErrorLogging(propertyName, baseType, "RuntimeConfigured", "ex"));
            
            sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
            sb.AppendLine($"                        typeof({entity.ClassName}),");
            sb.AppendLine($"                        \"{propertyName}\",");
            sb.AppendLine($"                        new AttributeValue {{ S = \"<json serialization failed>\" }},");
            sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
            sb.AppendLine("                        ex)");
            sb.AppendLine($"                        .WithContext(\"SerializerType\", \"RuntimeConfigured\")");
            sb.AppendLine($"                        .WithContext(\"PropertyType\", \"{baseType}\")");
            sb.AppendLine($"                        .WithContext(\"Operation\", \"JsonSerialization\");");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        else
        {
            // Generate null check for JSON serializer
            sb.AppendLine("            if (options?.JsonSerializer == null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                throw new InvalidOperationException(");
            sb.AppendLine($"                    \"Property '{propertyName}' has [JsonBlob] attribute but no JSON serializer is configured. \" +");
            sb.AppendLine($"                    \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var json = options.JsonSerializer.Serialize(typedEntity.{escapedPropertyName});");
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ S = json }};");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            
            // Generate error logging for JSON serialization
            sb.Append(LoggingCodeGenerator.GenerateJsonSerializationErrorLogging(propertyName, baseType, "RuntimeConfigured", "ex"));
            
            sb.AppendLine("                throw DynamoDbMappingException.PropertyConversionFailed(");
            sb.AppendLine($"                    typeof({entity.ClassName}),");
            sb.AppendLine($"                    \"{propertyName}\",");
            sb.AppendLine($"                    new AttributeValue {{ S = \"<json serialization failed>\" }},");
            sb.AppendLine($"                    typeof({GetTypeForMetadata(property.PropertyType)}),");
            sb.AppendLine("                    ex)");
            sb.AppendLine($"                    .WithContext(\"SerializerType\", \"RuntimeConfigured\")");
            sb.AppendLine($"                    .WithContext(\"PropertyType\", \"{baseType}\")");
            sb.AppendLine($"                    .WithContext(\"Operation\", \"JsonSerialization\");");
            sb.AppendLine("            }");
        }
    }

    private static void GenerateTtlPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;
        var baseType = GetBaseType(propertyType);
        var varName = attributeName.ToLowerInvariant().Replace("-", "").Replace("_", "");

        sb.AppendLine($"            // Convert TTL property {propertyName} from Unix epoch seconds");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {varName}Value) && {varName}Value.N != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");

        if (baseType == "DateTime" || baseType == "System.DateTime")
        {
            // DateTime TTL reconstruction
            sb.AppendLine($"                    var seconds = long.Parse({varName}Value.N);");
            sb.AppendLine("                    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);");
            sb.AppendLine($"                    entity.{escapedPropertyName} = epoch.AddSeconds(seconds);");
        }
        else if (baseType == "DateTimeOffset" || baseType == "System.DateTimeOffset")
        {
            // DateTimeOffset TTL reconstruction
            sb.AppendLine($"                    var seconds = long.Parse({varName}Value.N);");
            sb.AppendLine($"                    entity.{escapedPropertyName} = DateTimeOffset.FromUnixTimeSeconds(seconds);");
        }

        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        {varName}Value,");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex)");
        sb.AppendLine($"                        .WithContext(\"TtlValue\", {varName}Value.N ?? \"<null>\")");
        sb.AppendLine($"                        .WithContext(\"Operation\", \"TtlDeserialization\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateJsonBlobPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;
        var baseType = GetBaseType(propertyType);

        sb.AppendLine($"            // Deserialize JSON blob property {propertyName}");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        // Generate null check for JSON serializer
        sb.AppendLine("                if (options?.JsonSerializer == null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    throw new InvalidOperationException(");
        sb.AppendLine($"                        \"Property '{propertyName}' has [JsonBlob] attribute but no JSON serializer is configured. \" +");
        sb.AppendLine($"                        \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine($"                    if ({propertyName.ToLowerInvariant()}Value.S != null)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        entity.{escapedPropertyName} = options.JsonSerializer.Deserialize<{baseType}>({propertyName.ToLowerInvariant()}Value.S);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        
        // Generate error logging for JSON deserialization
        sb.Append(LoggingCodeGenerator.GenerateJsonSerializationErrorLogging(propertyName, baseType, "RuntimeConfigured", "ex"));
        
        sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Value,");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex)");
        sb.AppendLine($"                        .WithContext(\"SerializerType\", \"RuntimeConfigured\")");
        sb.AppendLine($"                        .WithContext(\"PropertyType\", \"{baseType}\")");
        sb.AppendLine($"                        .WithContext(\"Operation\", \"JsonDeserialization\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateMapPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;

        sb.AppendLine($"            // Convert Map property {propertyName} to DynamoDB Map (M)");
        sb.AppendLine($"            // Note: Custom types use nested ToDynamoDb calls (NO REFLECTION) for AOT compatibility");

        // Check if it's Dictionary<string, string>
        if (propertyType.Contains("Dictionary<string, string>") || 
            propertyType.Contains("Dictionary<System.String, System.String>"))
        {
            // Dictionary<string, string> - simple string map
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null && typedEntity.{escapedPropertyName}.Count > 0)");
            sb.AppendLine("            {");
            // Generate logging for Map conversion
            sb.Append(LoggingCodeGenerator.GenerateMapConversionLogging(propertyName, $"typedEntity.{escapedPropertyName}.Count", "ToDynamoDb"));
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var {propertyName.ToLowerInvariant()}Map = new Dictionary<string, AttributeValue>();");
            sb.AppendLine($"                    foreach (var kvp in typedEntity.{escapedPropertyName})");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Map[kvp.Key] = new AttributeValue {{ S = kvp.Value }};");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ M = {propertyName.ToLowerInvariant()}Map }};");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            
            // Generate error logging for Map conversion
            sb.Append(LoggingCodeGenerator.GenerateConversionErrorLogging(propertyName, "Dictionary<string, string>", "DynamoDB Map", "ex"));
            
            sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
            sb.AppendLine($"                        typeof({entity.ClassName}),");
            sb.AppendLine($"                        \"{propertyName}\",");
            sb.AppendLine($"                        new AttributeValue {{ M = new Dictionary<string, AttributeValue>() }},");
            sb.AppendLine($"                        typeof({GetTypeForMetadata(propertyType)}),");
            sb.AppendLine("                        ex)");
            sb.AppendLine($"                        .WithContext(\"MapType\", \"Dictionary<string, string>\")");
            sb.AppendLine($"                        .WithContext(\"Operation\", \"ToDynamoDb\");");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        // Check if it's Dictionary<string, object>
        else if (propertyType.Contains("Dictionary<string, object>") ||
                 propertyType.Contains("Dictionary<System.String, System.Object>"))
        {
            // Dictionary<string, object> - convert object to AttributeValue
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null && typedEntity.{escapedPropertyName}.Count > 0)");
            sb.AppendLine("            {");
            // Generate logging for Map conversion
            sb.Append(LoggingCodeGenerator.GenerateMapConversionLogging(propertyName, $"typedEntity.{escapedPropertyName}.Count", "ToDynamoDb"));
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var {propertyName.ToLowerInvariant()}Map = new Dictionary<string, AttributeValue>();");
            sb.AppendLine($"                    foreach (var kvp in typedEntity.{escapedPropertyName})");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        if (kvp.Value is AttributeValue av)");
            sb.AppendLine($"                            {propertyName.ToLowerInvariant()}Map[kvp.Key] = av;");
            sb.AppendLine($"                        else");
            sb.AppendLine($"                            {propertyName.ToLowerInvariant()}Map[kvp.Key] = new AttributeValue {{ S = kvp.Value?.ToString() ?? string.Empty }};");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ M = {propertyName.ToLowerInvariant()}Map }};");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            
            // Generate error logging for Map conversion
            sb.Append(LoggingCodeGenerator.GenerateConversionErrorLogging(propertyName, "Dictionary<string, object>", "DynamoDB Map", "ex"));
            
            sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
            sb.AppendLine($"                        typeof({entity.ClassName}),");
            sb.AppendLine($"                        \"{propertyName}\",");
            sb.AppendLine($"                        new AttributeValue {{ M = new Dictionary<string, AttributeValue>() }},");
            sb.AppendLine($"                        typeof({GetTypeForMetadata(propertyType)}),");
            sb.AppendLine("                        ex)");
            sb.AppendLine($"                        .WithContext(\"MapType\", \"Dictionary<string, object>\")");
            sb.AppendLine($"                        .WithContext(\"Operation\", \"ToDynamoDb\");");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        // Check if it's Dictionary<string, AttributeValue>
        else if (propertyType.Contains("Dictionary<string, AttributeValue>") ||
                 propertyType.Contains("Dictionary<System.String, Amazon.DynamoDBv2.Model.AttributeValue>"))
        {
            // Dictionary<string, AttributeValue> - direct map
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null && typedEntity.{escapedPropertyName}.Count > 0)");
            sb.AppendLine("            {");
            // Generate logging for Map conversion
            sb.Append(LoggingCodeGenerator.GenerateMapConversionLogging(propertyName, $"typedEntity.{escapedPropertyName}.Count", "ToDynamoDb"));
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ M = typedEntity.{escapedPropertyName} }};");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            
            // Generate error logging for Map conversion
            sb.Append(LoggingCodeGenerator.GenerateConversionErrorLogging(propertyName, "Dictionary<string, AttributeValue>", "DynamoDB Map", "ex"));
            
            sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
            sb.AppendLine($"                        typeof({entity.ClassName}),");
            sb.AppendLine($"                        \"{propertyName}\",");
            sb.AppendLine($"                        new AttributeValue {{ M = new Dictionary<string, AttributeValue>() }},");
            sb.AppendLine($"                        typeof({GetTypeForMetadata(propertyType)}),");
            sb.AppendLine("                        ex)");
            sb.AppendLine($"                        .WithContext(\"MapType\", \"Dictionary<string, AttributeValue>\")");
            sb.AppendLine($"                        .WithContext(\"Operation\", \"ToDynamoDb\");");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        else
        {
            // Custom object with [DynamoDbMap] - use nested ToDynamoDb call
            // The nested type must also be marked with [DynamoDbEntity] to have its own mapping generated
            var simpleTypeName = GetSimpleTypeName(propertyType);
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    // Convert nested entity to map using its generated ToDynamoDb method");
            sb.AppendLine($"                    var {propertyName.ToLowerInvariant()}Map = {simpleTypeName}.ToDynamoDb(typedEntity.{escapedPropertyName});");
            sb.AppendLine($"                    if ({propertyName.ToLowerInvariant()}Map != null && {propertyName.ToLowerInvariant()}Map.Count > 0)");
            sb.AppendLine("                    {");
            // Generate logging for Map conversion (custom object)
            sb.Append(LoggingCodeGenerator.GenerateMapConversionLogging(propertyName, $"{propertyName.ToLowerInvariant()}Map.Count", "ToDynamoDb"));
            sb.AppendLine($"                        item[\"{attributeName}\"] = new AttributeValue {{ M = {propertyName.ToLowerInvariant()}Map }};");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            
            // Generate error logging for Map conversion
            sb.Append(LoggingCodeGenerator.GenerateConversionErrorLogging(propertyName, propertyType, "DynamoDB Map", "ex"));
            
            sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
            sb.AppendLine($"                        typeof({entity.ClassName}),");
            sb.AppendLine($"                        \"{propertyName}\",");
            sb.AppendLine($"                        new AttributeValue {{ M = new Dictionary<string, AttributeValue>() }},");
            sb.AppendLine($"                        typeof({GetTypeForMetadata(propertyType)}),");
            sb.AppendLine("                        ex)");
            sb.AppendLine($"                        .WithContext(\"MapType\", \"CustomObject\")");
            sb.AppendLine($"                        .WithContext(\"NestedType\", \"{propertyType}\")");
            sb.AppendLine($"                        .WithContext(\"Operation\", \"ToDynamoDb\");");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
    }

    private static void GenerateCollectionPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var collectionElementType = GetCollectionElementType(property.PropertyType);

        sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null && typedEntity.{escapedPropertyName}.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");

        // Check if this is a Set type (HashSet)
        var isSet = property.PropertyType.Contains("HashSet<") || 
                    property.PropertyType.Contains("System.Collections.Generic.HashSet<");

        if (isSet)
        {
            // Generate Set-specific code (SS, NS, or BS)
            GenerateSetPropertyToAttributeValue(sb, property, entity, attributeName, propertyName, collectionElementType);
        }
        else
        {
            // Generate List-specific code (L)
            GenerateListPropertyToAttributeValue(sb, property, entity, attributeName, propertyName, collectionElementType);
        }

        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        
        // Generate error logging for collection conversion
        var collectionType = isSet ? "Set" : "List";
        sb.Append(LoggingCodeGenerator.GenerateConversionErrorLogging(propertyName, property.PropertyType, $"DynamoDB {collectionType}", "ex"));
        
        sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        new AttributeValue {{ L = new List<AttributeValue>() }},");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex)");
        sb.AppendLine($"                        .WithContext(\"CollectionType\", \"{collectionType}\")");
        sb.AppendLine($"                        .WithContext(\"ElementType\", \"{collectionElementType}\")");
        sb.AppendLine($"                        .WithContext(\"Operation\", \"ToDynamoDb\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateSetPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity, string attributeName, string propertyName, string collectionElementType)
    {
        var baseElementType = GetBaseType(collectionElementType);
        var escapedPropertyName = EscapePropertyName(propertyName);

        if (baseElementType == "string" || baseElementType == "System.String")
        {
            // String Set (SS)
            // Generate logging for Set conversion
            sb.Append(LoggingCodeGenerator.GenerateSetConversionLogging(propertyName, "String Set", $"typedEntity.{escapedPropertyName}.Count", "ToDynamoDb"));
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ SS = typedEntity.{escapedPropertyName}.ToList() }};");
        }
        else if (IsNumericType(baseElementType))
        {
            // Number Set (NS)
            // Generate logging for Set conversion
            sb.Append(LoggingCodeGenerator.GenerateSetConversionLogging(propertyName, "Number Set", $"typedEntity.{escapedPropertyName}.Count", "ToDynamoDb"));
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue");
            sb.AppendLine("                {");
            sb.AppendLine($"                    NS = typedEntity.{escapedPropertyName}.Select(x => x.ToString()).ToList()");
            sb.AppendLine("                };");
        }
        else if (baseElementType == "byte[]" || baseElementType == "System.Byte[]")
        {
            // Binary Set (BS)
            // Generate logging for Set conversion
            sb.Append(LoggingCodeGenerator.GenerateSetConversionLogging(propertyName, "Binary Set", $"typedEntity.{escapedPropertyName}.Count", "ToDynamoDb"));
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue");
            sb.AppendLine("                {");
            sb.AppendLine($"                    BS = typedEntity.{escapedPropertyName}.Select(x => new MemoryStream(x)).ToList()");
            sb.AppendLine("                };");
        }
        else
        {
            // Unsupported Set element type - this should be caught by validation
            sb.AppendLine($"                throw new NotSupportedException($\"HashSet<{baseElementType}> is not supported. Use HashSet<string>, HashSet<int>, HashSet<decimal>, or HashSet<byte[]>\");");
        }
    }

    private static void GenerateListPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity, string attributeName, string propertyName, string collectionElementType)
    {
        var baseElementType = GetBaseType(collectionElementType);
        var escapedPropertyName = EscapePropertyName(propertyName);

        // Add comment for collection conversion
        sb.AppendLine($"                // Convert collection {propertyName} to native DynamoDB type");
        sb.AppendLine($"                // Convert {property.PropertyType} to DynamoDB List (L)");
        
        // Generate logging for List conversion
        sb.Append(LoggingCodeGenerator.GenerateListConversionLogging(propertyName, $"typedEntity.{escapedPropertyName}.Count", "ToDynamoDb"));
        
        // Use List (L) for all List types
        sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue");
        sb.AppendLine("                {");
        
        // Generate the appropriate conversion based on element type
        var conversionExpression = GetToAttributeValueExpressionForCollectionElement(baseElementType);
        sb.AppendLine($"                    L = typedEntity.{escapedPropertyName}.Select(x => {conversionExpression}).ToList()");
        sb.AppendLine("                };");
    }
    
    private static string GetToAttributeValueExpressionForCollectionElement(string elementType)
    {
        var baseType = GetBaseType(elementType);
        
        return baseType switch
        {
            "string" or "System.String" => "new AttributeValue { S = x }",
            "int" or "System.Int32" => "new AttributeValue { N = x.ToString() }",
            "long" or "System.Int64" => "new AttributeValue { N = x.ToString() }",
            "double" or "System.Double" => "new AttributeValue { N = x.ToString() }",
            "float" or "System.Single" => "new AttributeValue { N = x.ToString() }",
            "decimal" or "System.Decimal" => "new AttributeValue { N = x.ToString() }",
            "bool" or "System.Boolean" => "new AttributeValue { BOOL = x }",
            "DateTime" or "System.DateTime" => "new AttributeValue { S = x.ToString(\"O\") }",
            "DateTimeOffset" or "System.DateTimeOffset" => "new AttributeValue { S = x.ToString(\"O\") }",
            "Guid" or "System.Guid" => "new AttributeValue { S = x.ToString() }",
            "Ulid" or "System.Ulid" => "new AttributeValue { S = x.ToString() }",
            "byte[]" or "System.Byte[]" => "new AttributeValue { B = new MemoryStream(x) }",
            _ => "new AttributeValue { S = x != null ? x.ToString() : string.Empty }"
        };
    }

    private static string GetToAttributeValueExpression(PropertyModel property, string valueExpression)
    {
        var baseType = GetBaseType(property.PropertyType);
        
        // For nullable value types (e.g., DateTime?, int?), we need to access .Value before calling ToString
        // Check if the property type contains "?" which indicates a nullable value type
        // Exclude string since it's a reference type and doesn't have .Value
        var isNullableValueType = property.PropertyType.Contains("?") && baseType != "string";
        var actualValue = isNullableValueType ? $"{valueExpression}.Value" : valueExpression;

        // Handle DateTime with Kind conversion and/or format string
        if ((baseType == "DateTime" || baseType == "System.DateTime") && (property.DateTimeKind.HasValue || !string.IsNullOrEmpty(property.Format)))
        {
            return GenerateDateTimeToAttributeValue(property, actualValue);
        }

        // Handle format strings for other types
        if (!string.IsNullOrEmpty(property.Format))
        {
            return GenerateFormattedToAttributeValue(property, actualValue);
        }

        return baseType switch
        {
            "string" => $"new AttributeValue {{ S = {valueExpression} }}",
            "int" or "System.Int32" => $"new AttributeValue {{ N = {actualValue}.ToString() }}",
            "long" or "System.Int64" => $"new AttributeValue {{ N = {actualValue}.ToString() }}",
            "double" or "System.Double" => $"new AttributeValue {{ N = {actualValue}.ToString() }}",
            "float" or "System.Single" => $"new AttributeValue {{ N = {actualValue}.ToString() }}",
            "decimal" or "System.Decimal" => $"new AttributeValue {{ N = {actualValue}.ToString() }}",
            "bool" or "System.Boolean" => $"new AttributeValue {{ BOOL = {actualValue} }}",
            "DateTime" or "System.DateTime" => $"new AttributeValue {{ S = {actualValue}.ToString(\"O\") }}",
            "DateTimeOffset" or "System.DateTimeOffset" => $"new AttributeValue {{ S = {actualValue}.ToString(\"O\") }}",
            "Guid" or "System.Guid" => $"new AttributeValue {{ S = {actualValue}.ToString() }}",
            "Ulid" or "System.Ulid" => $"new AttributeValue {{ S = {actualValue}.ToString() }}",
            "byte[]" or "System.Byte[]" => $"new AttributeValue {{ B = new System.IO.MemoryStream({valueExpression}) }}",
            _ when IsEnumType(property.PropertyType) => $"new AttributeValue {{ S = {actualValue}.ToString() }}",
            _ => $"new AttributeValue {{ S = {valueExpression} != null ? {valueExpression}.ToString() : \"\" }}"
        };
    }

    private static string GenerateDateTimeToAttributeValue(PropertyModel property, string valueExpression)
    {
        var convertedValue = valueExpression;
        
        // Apply DateTime Kind conversion if specified
        if (property.DateTimeKind.HasValue)
        {
            convertedValue = property.DateTimeKind.Value switch
            {
                DateTimeKind.Utc => $"{valueExpression}.ToUniversalTime()",
                DateTimeKind.Local => $"{valueExpression}.ToLocalTime()",
                _ => valueExpression
            };
        }

        // Apply format string if specified, otherwise use ISO 8601 (O format)
        var format = !string.IsNullOrEmpty(property.Format) ? property.Format : "O";
        
        return $"new AttributeValue {{ S = {convertedValue}.ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture) }}";
    }

    private static string GenerateFormattedToAttributeValue(PropertyModel property, string valueExpression)
    {
        var baseType = GetBaseType(property.PropertyType);
        var format = property.Format!;

        // For numeric types and other IFormattable types, apply format string
        if (baseType is "int" or "System.Int32" or "long" or "System.Int64" or 
            "double" or "System.Double" or "float" or "System.Single" or 
            "decimal" or "System.Decimal")
        {
            return $"new AttributeValue {{ S = {valueExpression}.ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture) }}";
        }

        // For DateTimeOffset with format
        if (baseType is "DateTimeOffset" or "System.DateTimeOffset")
        {
            return $"new AttributeValue {{ S = {valueExpression}.ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture) }}";
        }

        // Default: no format application
        return $"new AttributeValue {{ S = {valueExpression}.ToString() }}";
    }

    /// <summary>
    /// Generates code for serializing a property with format string application.
    /// Supports DateTime, decimal, double, float, int, and IFormattable types.
    /// Uses CultureInfo.InvariantCulture for all formatting.
    /// </summary>
    private static void GenerateFormattedPropertySerialization(StringBuilder sb, PropertyModel property, string valueExpression, string attributeName)
    {
        var baseType = GetBaseType(property.PropertyType);
        var format = property.Format!;
        var propertyName = property.PropertyName;

        // Generate logging for format string application
        sb.Append(LoggingCodeGenerator.GenerateFormatStringApplicationLogging(propertyName, format, baseType));
        sb.AppendLine();

        sb.AppendLine("                try");
        sb.AppendLine("                {");

        // Handle DateTime with Kind conversion
        if (baseType is "DateTime" or "System.DateTime")
        {
            if (property.DateTimeKind.HasValue)
            {
                var convertedValue = property.DateTimeKind.Value switch
                {
                    DateTimeKind.Utc => $"{valueExpression}.ToUniversalTime()",
                    DateTimeKind.Local => $"{valueExpression}.ToLocalTime()",
                    _ => valueExpression
                };
                sb.AppendLine($"                    var convertedValue = {convertedValue};");
                sb.AppendLine($"                    var formatted = convertedValue.ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture);");
            }
            else
            {
                sb.AppendLine($"                    var formatted = {valueExpression}.ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture);");
            }
        }
        // Handle numeric types
        else if (baseType is "int" or "System.Int32" or "long" or "System.Int64" or 
                 "double" or "System.Double" or "float" or "System.Single" or 
                 "decimal" or "System.Decimal")
        {
            sb.AppendLine($"                    var formatted = {valueExpression}.ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture);");
        }
        // Handle DateTimeOffset
        else if (baseType is "DateTimeOffset" or "System.DateTimeOffset")
        {
            sb.AppendLine($"                    var formatted = {valueExpression}.ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture);");
        }
        // Handle IFormattable types
        else
        {
            sb.AppendLine($"                    var formatted = ((IFormattable){valueExpression}).ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture);");
        }

        sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ S = formatted }};");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (FormatException ex)");
        sb.AppendLine("                {");
        
        // Generate enhanced error message with examples
        var exampleFormats = GetExampleFormatsForType(baseType);
        sb.AppendLine($"                    throw new FormatException(");
        sb.AppendLine($"                        $\"Invalid format string '{format}' for property '{propertyName}' (DynamoDB attribute: '{attributeName}') of type '{baseType}'. \" +");
        sb.AppendLine($"                        $\"Error: {{ex.Message}}. \" +");
        sb.AppendLine($"                        $\"Common format strings for {baseType}: {exampleFormats}. \" +");
        sb.AppendLine($"                        \"Ensure the format string is valid for the property type.\",");
        sb.AppendLine($"                        ex);");
        sb.AppendLine("                }");
    }

    /// <summary>
    /// Generates code for deserializing a property with format string parsing.
    /// Supports parsing DateTime with TryParseExact and numeric types with TryParse.
    /// Adds error handling with DynamoDbMappingException for parsing failures.
    /// </summary>
    private static void GenerateFormattedPropertyDeserialization(StringBuilder sb, PropertyModel property, EntityModel entity, string valueExpression, string propertyName)
    {
        var baseType = GetBaseType(property.PropertyType);
        var format = property.Format!;
        var escapedPropertyName = EscapePropertyName(propertyName);

        // Generate logging for format string parsing
        sb.Append(LoggingCodeGenerator.GenerateFormatStringParsingLogging(propertyName, format, baseType));
        sb.AppendLine();

        sb.AppendLine("                try");
        sb.AppendLine("                {");

        // Handle DateTime with format
        if (baseType is "DateTime" or "System.DateTime")
        {
            sb.AppendLine($"                    if (DateTime.TryParseExact({valueExpression}.S, \"{format}\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))");
            sb.AppendLine("                    {");
            
            // Apply DateTime Kind if specified
            if (property.DateTimeKind.HasValue)
            {
                var kindSetting = property.DateTimeKind.Value switch
                {
                    DateTimeKind.Utc => "DateTime.SpecifyKind(parsed, DateTimeKind.Utc)",
                    DateTimeKind.Local => "DateTime.SpecifyKind(parsed, DateTimeKind.Local)",
                    _ => "parsed"
                };
                sb.AppendLine($"                        entity.{escapedPropertyName} = {kindSetting};");
            }
            else
            {
                sb.AppendLine($"                        entity.{escapedPropertyName} = parsed;");
            }
            
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new DynamoDbMappingException(");
            sb.AppendLine($"                            $\"Failed to parse DateTime value '{{{valueExpression}.S}}' for property '{propertyName}' (DynamoDB attribute: '{property.AttributeName}') using format '{format}'. \" +");
            sb.AppendLine($"                            $\"Ensure the stored value matches the format string. \" +");
            sb.AppendLine($"                            $\"Common DateTime formats: 'o' (ISO 8601), 'yyyy-MM-dd' (date only), 'yyyy-MM-dd HH:mm:ss' (date and time).\");");
            sb.AppendLine("                    }");
        }
        // Handle decimal
        else if (baseType is "decimal" or "System.Decimal")
        {
            sb.AppendLine($"                    if (decimal.TryParse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        entity.{escapedPropertyName} = parsed;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new DynamoDbMappingException(");
            sb.AppendLine($"                            $\"Failed to parse decimal value '{{{valueExpression}.S}}' for property '{propertyName}' (DynamoDB attribute: '{property.AttributeName}'). \" +");
            sb.AppendLine($"                            $\"Ensure the stored value is a valid decimal number. \" +");
            sb.AppendLine($"                            $\"If using a format string, verify it matches the stored data format.\");");
            sb.AppendLine("                    }");
        }
        // Handle int
        else if (baseType is "int" or "System.Int32")
        {
            sb.AppendLine($"                    if (int.TryParse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        entity.{escapedPropertyName} = parsed;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new DynamoDbMappingException(");
            sb.AppendLine($"                            $\"Failed to parse int value '{{{valueExpression}.S}}' for property '{propertyName}' (DynamoDB attribute: '{property.AttributeName}'). \" +");
            sb.AppendLine($"                            $\"Ensure the stored value is a valid integer. \" +");
            sb.AppendLine($"                            $\"If using a format string, verify it matches the stored data format.\");");
            sb.AppendLine("                    }");
        }
        // Handle long
        else if (baseType is "long" or "System.Int64")
        {
            sb.AppendLine($"                    if (long.TryParse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        entity.{escapedPropertyName} = parsed;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new DynamoDbMappingException(");
            sb.AppendLine($"                            $\"Failed to parse long value '{{{valueExpression}.S}}' for property '{propertyName}' (DynamoDB attribute: '{property.AttributeName}'). \" +");
            sb.AppendLine($"                            $\"Ensure the stored value is a valid long integer. \" +");
            sb.AppendLine($"                            $\"If using a format string, verify it matches the stored data format.\");");
            sb.AppendLine("                    }");
        }
        // Handle double
        else if (baseType is "double" or "System.Double")
        {
            sb.AppendLine($"                    if (double.TryParse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        entity.{escapedPropertyName} = parsed;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new DynamoDbMappingException(");
            sb.AppendLine($"                            $\"Failed to parse double value '{{{valueExpression}.S}}' for property '{propertyName}' (DynamoDB attribute: '{property.AttributeName}'). \" +");
            sb.AppendLine($"                            $\"Ensure the stored value is a valid double-precision number. \" +");
            sb.AppendLine($"                            $\"If using a format string, verify it matches the stored data format.\");");
            sb.AppendLine("                    }");
        }
        // Handle float
        else if (baseType is "float" or "System.Single")
        {
            sb.AppendLine($"                    if (float.TryParse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        entity.{escapedPropertyName} = parsed;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new DynamoDbMappingException(");
            sb.AppendLine($"                            $\"Failed to parse float value '{{{valueExpression}.S}}' for property '{propertyName}' (DynamoDB attribute: '{property.AttributeName}'). \" +");
            sb.AppendLine($"                            $\"Ensure the stored value is a valid single-precision number. \" +");
            sb.AppendLine($"                            $\"If using a format string, verify it matches the stored data format.\");");
            sb.AppendLine("                    }");
        }
        // Handle DateTimeOffset
        else if (baseType is "DateTimeOffset" or "System.DateTimeOffset")
        {
            sb.AppendLine($"                    if (DateTimeOffset.TryParseExact({valueExpression}.S, \"{format}\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        entity.{escapedPropertyName} = parsed;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new DynamoDbMappingException(");
            sb.AppendLine($"                            $\"Failed to parse DateTimeOffset value '{{{valueExpression}.S}}' for property '{propertyName}' (DynamoDB attribute: '{property.AttributeName}') using format '{format}'. \" +");
            sb.AppendLine($"                            $\"Ensure the stored value matches the format string. \" +");
            sb.AppendLine($"                            $\"Common DateTimeOffset formats: 'o' (ISO 8601), 'yyyy-MM-dd HH:mm:ss zzz' (with timezone).\");");
            sb.AppendLine("                    }");
        }

        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex) when (ex is not DynamoDbMappingException)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    throw new DynamoDbMappingException(");
        sb.AppendLine($"                        $\"Failed to deserialize property '{propertyName}' (DynamoDB attribute: '{property.AttributeName}') of type '{baseType}'. \" +");
        sb.AppendLine($"                        $\"Stored value: '{{{valueExpression}.S}}'. \" +");
        sb.AppendLine($"                        $\"Error: {{{{ex.Message}}}}. \" +");
        sb.AppendLine($"                        $\"Verify the format string matches the stored data format.\",");
        sb.AppendLine($"                        ex);");
        sb.AppendLine("                }");
    }

    private static void GenerateFromDynamoDbSingleMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// High-performance conversion from DynamoDB item to entity with minimal boxing and allocations.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <typeparam name=\"TSelf\">The entity type implementing IDynamoDbEntity.</typeparam>");
        sb.AppendLine("        /// <param name=\"item\">The DynamoDB item to map from.</param>");
        sb.AppendLine("        /// <param name=\"options\">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>");
        sb.AppendLine("        /// <returns>A mapped entity instance.</returns>");
        sb.AppendLine("        /// <exception cref=\"ArgumentException\">Thrown when the type parameter doesn't match the entity type.</exception>");
        sb.AppendLine("        /// <exception cref=\"DynamoDbMappingException\">Thrown when mapping fails due to data conversion issues.</exception>");
        sb.AppendLine($"        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        
        // Generate entry logging
        sb.Append(LoggingCodeGenerator.GenerateFromDynamoDbEntryLogging(entity.ClassName, "item"));
        sb.AppendLine();
        
        sb.AppendLine($"            if (typeof(TSelf) != typeof({entity.ClassName}))");
        sb.AppendLine($"                throw new ArgumentException($\"Expected {entity.ClassName}, got {{typeof(TSelf).Name}}\");");
        sb.AppendLine();

        // Wrap entire mapping operation in try-catch
        sb.AppendLine("            try");
        sb.AppendLine("            {");

        sb.AppendLine($"                var entity = new {entity.ClassName}();");
        sb.AppendLine();

        // Generate property mappings
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GeneratePropertyFromAttributeValue(sb, property, entity);
        }

        // Generate extracted key logic
        var extractedProperties = entity.Properties.Where(p => p.IsExtracted).ToArray();
        if (extractedProperties.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("                // Extract component properties from composite keys");
            foreach (var extractedProperty in extractedProperties)
            {
                GenerateExtractedKeyLogic(sb, extractedProperty);
            }
        }

        sb.AppendLine();
        
        // Generate exit logging
        sb.Append(LoggingCodeGenerator.GenerateFromDynamoDbExitLogging(entity.ClassName));
        sb.AppendLine();
        
        sb.AppendLine("                return (TSelf)(object)entity;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        
        // Generate error logging
        sb.Append(LoggingCodeGenerator.GenerateMappingErrorLogging(entity.ClassName, "", "ex"));
        
        sb.AppendLine("                throw;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GenerateFromDynamoDbSingleAsyncMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// High-performance async conversion from DynamoDB item to entity with minimal boxing and allocations.");
        sb.AppendLine("        /// Handles blob reference properties by retrieving data from external storage.");
        sb.AppendLine("        /// Handles encrypted properties by decrypting data after retrieval.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <typeparam name=\"TSelf\">The entity type implementing IDynamoDbEntity.</typeparam>");
        sb.AppendLine("        /// <param name=\"item\">The DynamoDB item to map from.</param>");
        sb.AppendLine("        /// <param name=\"blobProvider\">The blob storage provider for handling blob references.</param>");
        sb.AppendLine("        /// <param name=\"fieldEncryptor\">Optional field encryptor for handling encrypted properties.</param>");
        sb.AppendLine("        /// <param name=\"options\">Optional configuration options including logger. If null, default behavior is used.</param>");
        sb.AppendLine("        /// <param name=\"cancellationToken\">Cancellation token for async operations.</param>");
        sb.AppendLine("        /// <returns>A task that resolves to a mapped entity instance.</returns>");
        sb.AppendLine("        /// <exception cref=\"ArgumentException\">Thrown when the type parameter doesn't match the entity type.</exception>");
        sb.AppendLine("        /// <exception cref=\"DynamoDbMappingException\">Thrown when mapping fails due to data conversion issues.</exception>");
        sb.AppendLine($"        public static async Task<TSelf> FromDynamoDbAsync<TSelf>(");
        sb.AppendLine("            Dictionary<string, AttributeValue> item,");
        sb.AppendLine("            IBlobStorageProvider blobProvider,");
        sb.AppendLine("            IFieldEncryptor? fieldEncryptor = null,");
        sb.AppendLine("            FluentDynamoDbOptions? options = null,");
        sb.AppendLine("            CancellationToken cancellationToken = default) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        
        // Generate entry logging
        sb.Append(LoggingCodeGenerator.GenerateFromDynamoDbEntryLogging(entity.ClassName, "item"));
        sb.AppendLine();
        
        sb.AppendLine($"            if (typeof(TSelf) != typeof({entity.ClassName}))");
        sb.AppendLine($"                throw new ArgumentException($\"Expected {entity.ClassName}, got {{typeof(TSelf).Name}}\");");
        sb.AppendLine();
        sb.AppendLine("            if (blobProvider == null)");
        sb.AppendLine("                throw new ArgumentNullException(nameof(blobProvider), \"Blob provider is required for entities with blob reference properties\");");
        sb.AppendLine();

        // Wrap entire mapping operation in try-catch
        sb.AppendLine("            try");
        sb.AppendLine("            {");

        sb.AppendLine($"                var entity = new {entity.ClassName}();");
        sb.AppendLine();

        // Generate property mappings
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GeneratePropertyFromAttributeValueAsync(sb, property, entity);
        }

        // Generate extracted key logic
        var extractedProperties = entity.Properties.Where(p => p.IsExtracted).ToArray();
        if (extractedProperties.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("                // Extract component properties from composite keys");
            foreach (var extractedProperty in extractedProperties)
            {
                GenerateExtractedKeyLogic(sb, extractedProperty);
            }
        }

        sb.AppendLine();
        
        // Generate exit logging
        sb.Append(LoggingCodeGenerator.GenerateFromDynamoDbExitLogging(entity.ClassName));
        sb.AppendLine();
        
        sb.AppendLine("                return (TSelf)(object)entity;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        
        // Generate error logging
        sb.Append(LoggingCodeGenerator.GenerateMappingErrorLogging(entity.ClassName, "", "ex"));
        
        sb.AppendLine("                throw;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GenerateFromDynamoDbMultiAsyncMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Creates an entity instance from multiple DynamoDB items (composite entity support).");
        sb.AppendLine("        /// For single-item entities, uses the first item. For multi-item entities, combines all items.");
        sb.AppendLine("        /// Handles blob reference properties by retrieving data from external storage.");
        sb.AppendLine("        /// Handles encrypted properties by decrypting data after retrieval.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <typeparam name=\"TSelf\">The entity type implementing IDynamoDbEntity.</typeparam>");
        sb.AppendLine("        /// <param name=\"items\">The collection of DynamoDB items to map from.</param>");
        sb.AppendLine("        /// <param name=\"blobProvider\">The blob storage provider for handling blob references.</param>");
        sb.AppendLine("        /// <param name=\"fieldEncryptor\">Optional field encryptor for handling encrypted properties.</param>");
        sb.AppendLine("        /// <param name=\"options\">Optional configuration options including logger. If null, default behavior is used.</param>");
        sb.AppendLine("        /// <param name=\"cancellationToken\">Cancellation token for async operations.</param>");
        sb.AppendLine("        /// <returns>A task that resolves to a mapped entity instance.</returns>");
        sb.AppendLine("        /// <exception cref=\"ArgumentException\">Thrown when items collection is null or empty.</exception>");
        sb.AppendLine("        /// <exception cref=\"DynamoDbMappingException\">Thrown when mapping fails due to data conversion issues.</exception>");
        sb.AppendLine($"        public static async Task<TSelf> FromDynamoDbAsync<TSelf>(");
        sb.AppendLine("            IList<Dictionary<string, AttributeValue>> items,");
        sb.AppendLine("            IBlobStorageProvider blobProvider,");
        sb.AppendLine("            IFieldEncryptor? fieldEncryptor = null,");
        sb.AppendLine("            FluentDynamoDbOptions? options = null,");
        sb.AppendLine("            CancellationToken cancellationToken = default) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        sb.AppendLine("            if (items == null || items.Count == 0)");
        sb.AppendLine($"                throw new ArgumentException(\"Items collection cannot be null or empty\", nameof(items));");
        sb.AppendLine();
        sb.AppendLine("            if (blobProvider == null)");
        sb.AppendLine("                throw new ArgumentNullException(nameof(blobProvider), \"Blob provider is required for entities with blob reference properties\");");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");

        if (entity.IsMultiItemEntity)
        {
            sb.AppendLine("                // Multi-item entity: combine all items into a single entity");
            sb.AppendLine("                // Note: Multi-item entities with blob references not yet fully supported");
            sb.AppendLine("                return await FromDynamoDbAsync<TSelf>(items[0], blobProvider, fieldEncryptor, options, cancellationToken);");
        }
        else
        {
            sb.AppendLine("                // Single-item entity: use the first item");
            sb.AppendLine("                return await FromDynamoDbAsync<TSelf>(items[0], blobProvider, fieldEncryptor, options, cancellationToken);");
        }

        sb.AppendLine("            }");
        sb.AppendLine("            catch (DynamoDbMappingException)");
        sb.AppendLine("            {");
        sb.AppendLine("                // Re-throw mapping exceptions as-is");
        sb.AppendLine("                throw;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                throw DynamoDbMappingException.EntityConstructionFailed(");
        sb.AppendLine($"                    typeof({entity.ClassName}),");
        sb.AppendLine("                    items.FirstOrDefault() ?? new Dictionary<string, AttributeValue>(),");
        sb.AppendLine("                    ex)");
        sb.AppendLine("                    .WithContext(\"ItemCount\", items.Count)");
        sb.AppendLine("                    .WithContext(\"MappingType\", \"MultiItem\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GeneratePropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);

        // Handle GeoLocation properties (requires geospatial package)
        if (IsGeoLocationType(property.PropertyType) && entity.HasGeospatialPackage)
        {
            GenerateGeoLocationPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle TTL properties (Time-To-Live)
        if (property.AdvancedType?.IsTtl == true)
        {
            GenerateTtlPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle JSON blob properties
        if (property.AdvancedType?.IsJsonBlob == true)
        {
            GenerateJsonBlobPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle Map properties (Dictionary types)
        if (property.AdvancedType?.IsMap == true)
        {
            GenerateMapPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        if (property.IsCollection)
        {
            GenerateCollectionPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Check if property has format string
        // All types with format strings use GenerateFormattedPropertyDeserialization for consistent error handling
        var hasFormatString = !string.IsNullOrEmpty(property.Format);
        var needsFormattedDeserialization = hasFormatString;

        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        
        // Use formatted deserialization if format string is present (non-DateTime types)
        if (needsFormattedDeserialization)
        {
            GenerateFormattedPropertyDeserialization(sb, property, entity, $"{propertyName.ToLowerInvariant()}Value", propertyName);
        }
        else
        {
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    entity.{escapedPropertyName} = {GetFromAttributeValueExpression(property, $"{propertyName.ToLowerInvariant()}Value")};");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    throw DynamoDbMappingException.PropertyConversionFailed(");
            sb.AppendLine($"                        typeof({entity.ClassName}),");
            sb.AppendLine($"                        \"{propertyName}\",");
            sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Value,");
            sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
            sb.AppendLine("                        ex);");
            sb.AppendLine("                }");
        }
        
        sb.AppendLine("            }");
    }

    private static void GeneratePropertyFromAttributeValueAsync(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);

        // Handle encrypted properties (must be before other handlers)
        if (property.Security?.IsEncrypted == true)
        {
            GenerateEncryptedPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle GeoLocation properties (requires geospatial package)
        if (IsGeoLocationType(property.PropertyType) && entity.HasGeospatialPackage)
        {
            GenerateGeoLocationPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle combined JSON blob + blob reference (retrieve blob, then deserialize from JSON)
        if (property.AdvancedType?.IsJsonBlob == true && property.AdvancedType?.IsBlobReference == true)
        {
            GenerateCombinedJsonBlobAndBlobReferenceFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle blob reference properties (async)
        if (property.AdvancedType?.IsBlobReference == true)
        {
            GenerateBlobReferencePropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle TTL properties (Time-To-Live)
        if (property.AdvancedType?.IsTtl == true)
        {
            GenerateTtlPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle JSON blob properties
        if (property.AdvancedType?.IsJsonBlob == true)
        {
            GenerateJsonBlobPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Handle Map properties (Dictionary types)
        if (property.AdvancedType?.IsMap == true)
        {
            GenerateMapPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        if (property.IsCollection)
        {
            GenerateCollectionPropertyFromAttributeValue(sb, property, entity);
            return;
        }

        // Check if property has format string
        // All types with format strings use GenerateFormattedPropertyDeserialization for consistent error handling
        var hasFormatString = !string.IsNullOrEmpty(property.Format);
        var needsFormattedDeserialization = hasFormatString;

        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        
        // Use formatted deserialization if format string is present (non-DateTime types)
        if (needsFormattedDeserialization)
        {
            GenerateFormattedPropertyDeserialization(sb, property, entity, $"{propertyName.ToLowerInvariant()}Value", propertyName);
        }
        else
        {
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    entity.{escapedPropertyName} = {GetFromAttributeValueExpression(property, $"{propertyName.ToLowerInvariant()}Value")};");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    throw DynamoDbMappingException.PropertyConversionFailed(");
            sb.AppendLine($"                        typeof({entity.ClassName}),");
            sb.AppendLine($"                        \"{propertyName}\",");
            sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Value,");
            sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
            sb.AppendLine("                        ex);");
            sb.AppendLine("                }");
        }
        
        sb.AppendLine("            }");
    }

    private static void GenerateBlobReferencePropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;
        var baseType = GetBaseType(propertyType);

        sb.AppendLine($"            // Retrieve blob reference property {propertyName} from external storage");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine($"                    if ({propertyName.ToLowerInvariant()}Value.S != null)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        var reference = {propertyName.ToLowerInvariant()}Value.S;");
        sb.AppendLine("                        var stream = await blobProvider.RetrieveAsync(reference, cancellationToken);");
        sb.AppendLine();

        // Convert stream back to property type
        if (baseType == "byte[]" || baseType == "System.Byte[]")
        {
            // byte[] - read stream to byte array
            sb.AppendLine("                        using var memoryStream = new MemoryStream();");
            sb.AppendLine("                        await stream.CopyToAsync(memoryStream, cancellationToken);");
            sb.AppendLine($"                        entity.{escapedPropertyName} = memoryStream.ToArray();");
        }
        else if (baseType == "Stream" || baseType == "System.IO.Stream" || baseType == "MemoryStream")
        {
            // Stream - use directly (caller must manage disposal)
            sb.AppendLine($"                        entity.{escapedPropertyName} = stream;");
        }
        else if (baseType == "string" || baseType == "System.String")
        {
            // string - read stream as UTF8 string
            sb.AppendLine("                        using var reader = new StreamReader(stream);");
            sb.AppendLine($"                        entity.{escapedPropertyName} = await reader.ReadToEndAsync();");
        }
        else
        {
            // Complex type - deserialize from JSON using runtime-configured serializer
            sb.AppendLine("                        // Deserialize complex type from JSON");
            sb.AppendLine("                        if (options?.JsonSerializer == null)");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            throw new InvalidOperationException(");
            sb.AppendLine($"                                \"Property '{propertyName}' is a complex type stored as blob reference but no JSON serializer is configured. \" +");
            sb.AppendLine($"                                \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
            sb.AppendLine("                        }");
            sb.AppendLine("                        using var reader = new StreamReader(stream);");
            sb.AppendLine("                        var json = await reader.ReadToEndAsync();");
            sb.AppendLine($"                        entity.{escapedPropertyName} = options.JsonSerializer.Deserialize<{baseType}>(json);");
        }

        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        
        // Generate error logging for blob retrieval
        sb.Append(LoggingCodeGenerator.GenerateBlobStorageErrorLogging(propertyName, $"{propertyName.ToLowerInvariant()}Value.S ?? \"<null>\"", "Retrieve", "ex"));
        
        sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Value,");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine($"                        ex)");
        sb.AppendLine($"                        .WithContext(\"BlobReference\", {propertyName.ToLowerInvariant()}Value.S ?? \"<null>\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateCombinedJsonBlobAndBlobReferenceFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;
        var baseType = GetBaseType(propertyType);
        var serializerType = property.AdvancedType?.JsonSerializerType;

        sb.AppendLine($"            // Combined JSON blob + blob reference: retrieve blob, then deserialize from JSON");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine($"                    if ({propertyName.ToLowerInvariant()}Value.S != null)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        var reference = {propertyName.ToLowerInvariant()}Value.S;");
        sb.AppendLine();

        // Step 1: Retrieve blob using reference
        sb.AppendLine($"                        // Step 1: Retrieve blob from external storage");
        sb.AppendLine("                        var stream = await blobProvider.RetrieveAsync(reference, cancellationToken);");
        sb.AppendLine();

        // Step 2: Read stream as JSON string
        sb.AppendLine($"                        // Step 2: Read stream as JSON string");
        sb.AppendLine("                        using var reader = new StreamReader(stream);");
        sb.AppendLine("                        var json = await reader.ReadToEndAsync();");
        sb.AppendLine();

        // Step 3: Deserialize JSON to property type using runtime-configured serializer
        sb.AppendLine($"                        // Step 3: Deserialize JSON to property type");
        sb.AppendLine("                        if (options?.JsonSerializer == null)");
        sb.AppendLine("                        {");
        sb.AppendLine($"                            throw new InvalidOperationException(");
        sb.AppendLine($"                                \"Property '{propertyName}' has [JsonBlob] attribute but no JSON serializer is configured. \" +");
        sb.AppendLine($"                                \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
        sb.AppendLine("                        }");
        sb.AppendLine($"                        entity.{escapedPropertyName} = options.JsonSerializer.Deserialize<{baseType}>(json);");

        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        
        // Generate error logging for combined JSON blob + blob retrieval
        sb.Append(LoggingCodeGenerator.GenerateBlobStorageErrorLogging(propertyName, $"{propertyName.ToLowerInvariant()}Value.S ?? \"<null>\"", "Retrieve", "ex"));
        
        sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Value,");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex)");
        sb.AppendLine($"                        .WithContext(\"CombinedJsonBlobAndBlobReference\", $\"Failed to retrieve or deserialize blob. Reference: {{{propertyName.ToLowerInvariant()}Value.S ?? \"<null>\"}}\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateMapPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var propertyType = property.PropertyType;

        sb.AppendLine($"            // Convert Map property {propertyName} from DynamoDB Map (M)");
        sb.AppendLine($"            // Note: Custom types use nested FromDynamoDb calls (NO REFLECTION) for AOT compatibility");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value) && {propertyName.ToLowerInvariant()}Value.M != null)");
        sb.AppendLine("            {");
        // Generate logging for Map conversion
        sb.Append(LoggingCodeGenerator.GenerateMapConversionLogging(propertyName, $"{propertyName.ToLowerInvariant()}Value.M.Count", "FromDynamoDb"));
        sb.AppendLine("                try");
        sb.AppendLine("                {");

        // Check if it's Dictionary<string, string>
        if (propertyType.Contains("Dictionary<string, string>") || 
            propertyType.Contains("Dictionary<System.String, System.String>"))
        {
            // Dictionary<string, string> - reconstruct from string map
            sb.AppendLine($"                    entity.{escapedPropertyName} = {propertyName.ToLowerInvariant()}Value.M.ToDictionary(");
            sb.AppendLine("                        kvp => kvp.Key,");
            sb.AppendLine("                        kvp => kvp.Value.S);");
        }
        // Check if it's Dictionary<string, object>
        else if (propertyType.Contains("Dictionary<string, object>") ||
                 propertyType.Contains("Dictionary<System.String, System.Object>"))
        {
            // Dictionary<string, object> - convert AttributeValue to object
            sb.AppendLine($"                    entity.{escapedPropertyName} = {propertyName.ToLowerInvariant()}Value.M.ToDictionary(");
            sb.AppendLine("                        kvp => kvp.Key,");
            sb.AppendLine("                        kvp => (object)kvp.Value);");
        }
        // Check if it's Dictionary<string, AttributeValue>
        else if (propertyType.Contains("Dictionary<string, AttributeValue>") ||
                 propertyType.Contains("Dictionary<System.String, Amazon.DynamoDBv2.Model.AttributeValue>"))
        {
            // Dictionary<string, AttributeValue> - direct assignment
            sb.AppendLine($"                    entity.{escapedPropertyName} = {propertyName.ToLowerInvariant()}Value.M;");
        }
        else
        {
            // Custom object with [DynamoDbMap] - use nested FromDynamoDb call
            // The nested type must also be marked with [DynamoDbEntity] to have its own mapping generated
            var simpleTypeName = GetSimpleTypeName(propertyType);
            sb.AppendLine($"                    // Convert map back to nested entity using its generated FromDynamoDb method");
            sb.AppendLine($"                    entity.{escapedPropertyName} = {simpleTypeName}.FromDynamoDb<{simpleTypeName}>({propertyName.ToLowerInvariant()}Value.M, options);");
        }

        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        
        // Generate error logging for Map conversion
        sb.Append(LoggingCodeGenerator.GenerateConversionErrorLogging(propertyName, "DynamoDB Map", propertyType, "ex"));
        
        sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Value,");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateCollectionPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var collectionElementType = GetCollectionElementType(property.PropertyType);
        var baseElementType = GetBaseType(collectionElementType);

        sb.AppendLine($"            // Convert collection {propertyName} from native DynamoDB type");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        sb.AppendLine($"                try");
        sb.AppendLine("                {");

        // Check if this is a Set type (HashSet)
        var isSet = property.PropertyType.Contains("HashSet<") || 
                    property.PropertyType.Contains("System.Collections.Generic.HashSet<");

        if (isSet)
        {
            // Generate Set-specific code (SS, NS, or BS)
            GenerateSetPropertyFromAttributeValue(sb, property, propertyName, baseElementType);
        }
        else
        {
            // Generate List-specific code (L)
            GenerateListPropertyFromAttributeValue(sb, property, propertyName, collectionElementType);
        }

        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        
        // Generate error logging for collection conversion
        var isSetType = property.PropertyType.Contains("HashSet<") || 
                    property.PropertyType.Contains("System.Collections.Generic.HashSet<");
        var collectionType = isSetType ? "Set" : "List";
        sb.Append(LoggingCodeGenerator.GenerateConversionErrorLogging(propertyName, $"DynamoDB {collectionType}", property.PropertyType, "ex"));
        
        sb.AppendLine("                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Value,");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            // If attribute not found in DynamoDB item, leave property as null (DynamoDB null semantics)");
    }

    private static void GenerateSetPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, string propertyName, string baseElementType)
    {
        // Strip nullable markers from both the property type and element type for instantiation
        // We need to rebuild the collection type with non-nullable element type
        var collectionElementType = GetCollectionElementType(property.PropertyType);
        var nonNullableElementType = collectionElementType.TrimEnd('?');
        var nonNullablePropertyType = $"HashSet<{nonNullableElementType}>";
        var escapedPropertyName = EscapePropertyName(propertyName);
        
        if (baseElementType == "string" || baseElementType == "System.String")
        {
            // String Set (SS)
            sb.AppendLine($"                    // Convert DynamoDB String Set (SS) to HashSet<string>");
            sb.AppendLine($"                    if ({propertyName.ToLowerInvariant()}Value.SS != null && {propertyName.ToLowerInvariant()}Value.SS.Count > 0)");
            sb.AppendLine("                    {");
            // Generate logging for Set conversion
            sb.Append(LoggingCodeGenerator.GenerateSetConversionLogging(propertyName, "String Set", $"{propertyName.ToLowerInvariant()}Value.SS.Count", "FromDynamoDb"));
            sb.AppendLine($"                        entity.{escapedPropertyName} = new {nonNullablePropertyType}({propertyName.ToLowerInvariant()}Value.SS);");
            sb.AppendLine("                    }");
            sb.AppendLine("                    // else: leave as null (DynamoDB null semantics - missing or empty set means null)");
        }
        else if (IsNumericType(baseElementType))
        {
            // Number Set (NS)
            sb.AppendLine($"                    // Convert DynamoDB Number Set (NS) to HashSet<{baseElementType}>");
            sb.AppendLine($"                    if ({propertyName.ToLowerInvariant()}Value.NS != null && {propertyName.ToLowerInvariant()}Value.NS.Count > 0)");
            sb.AppendLine("                    {");
            // Generate logging for Set conversion
            sb.Append(LoggingCodeGenerator.GenerateSetConversionLogging(propertyName, "Number Set", $"{propertyName.ToLowerInvariant()}Value.NS.Count", "FromDynamoDb"));
            sb.AppendLine($"                        entity.{escapedPropertyName} = new {nonNullablePropertyType}({propertyName.ToLowerInvariant()}Value.NS.Select({GetNumericConversionExpression(baseElementType)}));");
            sb.AppendLine("                    }");
            sb.AppendLine("                    // else: leave as null (DynamoDB null semantics - missing or empty set means null)");
        }
        else if (baseElementType == "byte[]" || baseElementType == "System.Byte[]")
        {
            // Binary Set (BS)
            sb.AppendLine($"                    // Convert DynamoDB Binary Set (BS) to HashSet<byte[]>");
            sb.AppendLine($"                    if ({propertyName.ToLowerInvariant()}Value.BS != null && {propertyName.ToLowerInvariant()}Value.BS.Count > 0)");
            sb.AppendLine("                    {");
            // Generate logging for Set conversion
            sb.Append(LoggingCodeGenerator.GenerateSetConversionLogging(propertyName, "Binary Set", $"{propertyName.ToLowerInvariant()}Value.BS.Count", "FromDynamoDb"));
            sb.AppendLine($"                        entity.{escapedPropertyName} = new {nonNullablePropertyType}({propertyName.ToLowerInvariant()}Value.BS.Select(x => x.ToArray()));");
            sb.AppendLine("                    }");
            sb.AppendLine("                    // else: leave as null (DynamoDB null semantics - missing or empty set means null)");
        }
        else
        {
            // Unsupported Set element type
            sb.AppendLine($"                    // ERROR: Unsupported Set element type: {baseElementType}");
            sb.AppendLine($"                    throw new NotSupportedException($\"HashSet<{baseElementType}> is not supported. Use HashSet<string>, HashSet<int>, HashSet<decimal>, or HashSet<byte[]>\");");
        }
    }

    private static void GenerateListPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, string propertyName, string collectionElementType)
    {
        var baseElementType = GetBaseType(collectionElementType);
        var escapedPropertyName = EscapePropertyName(propertyName);
        
        // Handle List (L) for all List types
        sb.AppendLine($"                    // Convert DynamoDB List (L) to List<{collectionElementType}>");
        sb.AppendLine($"                    if ({propertyName.ToLowerInvariant()}Value.L != null && {propertyName.ToLowerInvariant()}Value.L.Count > 0)");
        sb.AppendLine("                    {");
        
        // Generate logging for List conversion
        sb.Append(LoggingCodeGenerator.GenerateListConversionLogging(propertyName, $"{propertyName.ToLowerInvariant()}Value.L.Count", "FromDynamoDb"));
        
        // Strip nullable markers from both the property type and element type for instantiation
        // We need to rebuild the collection type with non-nullable element type
        var nonNullableElementType = collectionElementType.TrimEnd('?');
        var collectionTypeName = GetCollectionTypeName(property.PropertyType);
        var nonNullablePropertyType = $"{collectionTypeName}<{nonNullableElementType}>";
        
        var conversionExpression = GetFromAttributeValueExpressionForCollectionElement(baseElementType);
        sb.AppendLine($"                        entity.{escapedPropertyName} = new {nonNullablePropertyType}({propertyName.ToLowerInvariant()}Value.L.Select({conversionExpression}));");
        sb.AppendLine("                    }");
        sb.AppendLine("                    // else: leave as null (DynamoDB null semantics - missing or empty list means null)");
    }
    
    private static string GetFromAttributeValueExpressionForCollectionElement(string elementType)
    {
        var baseType = GetBaseType(elementType);
        
        return baseType switch
        {
            "string" or "System.String" => "x => x.S",
            "int" or "System.Int32" => "x => int.Parse(x.N)",
            "long" or "System.Int64" => "x => long.Parse(x.N)",
            "double" or "System.Double" => "x => double.Parse(x.N)",
            "float" or "System.Single" => "x => float.Parse(x.N)",
            "decimal" or "System.Decimal" => "x => decimal.Parse(x.N)",
            "bool" or "System.Boolean" => "x => x.BOOL ?? false",
            "DateTime" or "System.DateTime" => "x => DateTime.Parse(x.S)",
            "DateTimeOffset" or "System.DateTimeOffset" => "x => DateTimeOffset.Parse(x.S)",
            "Guid" or "System.Guid" => "x => Guid.Parse(x.S)",
            "Ulid" or "System.Ulid" => "x => Ulid.Parse(x.S)",
            "byte[]" or "System.Byte[]" => "x => x.B.ToArray()",
            _ => "x => x.S"
        };
    }

    private static string GetFromAttributeValueExpression(PropertyModel property, string valueExpression)
    {
        var baseType = GetBaseType(property.PropertyType);
        var isNullable = property.IsNullable;

        // Handle DateTime with Kind and/or format string
        if ((baseType == "DateTime" || baseType == "System.DateTime") && (property.DateTimeKind.HasValue || !string.IsNullOrEmpty(property.Format)))
        {
            return GenerateDateTimeFromAttributeValue(property, valueExpression);
        }

        // Handle format strings for other types
        if (!string.IsNullOrEmpty(property.Format))
        {
            return GenerateFormattedFromAttributeValue(property, valueExpression);
        }

        var conversion = baseType switch
        {
            "string" => $"{valueExpression}.S",
            "int" or "System.Int32" => $"int.Parse({valueExpression}.N)",
            "long" or "System.Int64" => $"long.Parse({valueExpression}.N)",
            "double" or "System.Double" => $"double.Parse({valueExpression}.N)",
            "float" or "System.Single" => $"float.Parse({valueExpression}.N)",
            "decimal" or "System.Decimal" => $"decimal.Parse({valueExpression}.N)",
            "bool" or "System.Boolean" => property.IsNullable ? $"{valueExpression}.BOOL" : $"{valueExpression}.BOOL ?? false",
            "DateTime" or "System.DateTime" => $"DateTime.SpecifyKind(DateTime.Parse({valueExpression}.S, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind), DateTimeKind.Unspecified)",
            "DateTimeOffset" or "System.DateTimeOffset" => $"DateTimeOffset.Parse({valueExpression}.S)",
            "Guid" or "System.Guid" => $"Guid.Parse({valueExpression}.S)",
            "Ulid" or "System.Ulid" => $"Ulid.Parse({valueExpression}.S)",
            "byte[]" or "System.Byte[]" => $"{valueExpression}.B.ToArray()",
            _ when IsEnumType(property.PropertyType) => $"Enum.Parse<{baseType}>({valueExpression}.S)",
            _ => $"{valueExpression}.S"
        };

        return conversion;
    }

    private static string GenerateDateTimeFromAttributeValue(PropertyModel property, string valueExpression)
    {
        var hasFormat = !string.IsNullOrEmpty(property.Format);
        var hasKind = property.DateTimeKind.HasValue;

        if (hasFormat && hasKind)
        {
            // Parse with format and set Kind
            var parseExpression = $"DateTime.ParseExact({valueExpression}.S, \"{property.Format}\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None)";
            
            return property.DateTimeKind.Value switch
            {
                DateTimeKind.Utc => $"DateTime.SpecifyKind({parseExpression}, DateTimeKind.Utc)",
                DateTimeKind.Local => $"DateTime.SpecifyKind({parseExpression}, DateTimeKind.Local)",
                _ => parseExpression
            };
        }
        else if (hasFormat)
        {
            // Parse with format only
            return $"DateTime.ParseExact({valueExpression}.S, \"{property.Format}\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None)";
        }
        else if (hasKind)
        {
            // Parse with default format and set Kind
            var parseExpression = $"DateTime.Parse({valueExpression}.S, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)";
            
            return property.DateTimeKind.Value switch
            {
                DateTimeKind.Utc => $"DateTime.SpecifyKind({parseExpression}, DateTimeKind.Utc)",
                DateTimeKind.Local => $"DateTime.SpecifyKind({parseExpression}, DateTimeKind.Local)",
                _ => parseExpression
            };
        }

        // No format and no Kind specified - default to Unspecified
        // Parse and explicitly set Kind to Unspecified to ensure consistent behavior
        return $"DateTime.SpecifyKind(DateTime.Parse({valueExpression}.S, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind), DateTimeKind.Unspecified)";
    }

    private static string GenerateFormattedFromAttributeValue(PropertyModel property, string valueExpression)
    {
        var baseType = GetBaseType(property.PropertyType);
        var format = property.Format!;

        // For numeric types, parse from formatted string
        if (baseType is "int" or "System.Int32")
        {
            return $"int.Parse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture)";
        }
        if (baseType is "long" or "System.Int64")
        {
            return $"long.Parse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture)";
        }
        if (baseType is "double" or "System.Double")
        {
            return $"double.Parse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture)";
        }
        if (baseType is "float" or "System.Single")
        {
            return $"float.Parse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture)";
        }
        if (baseType is "decimal" or "System.Decimal")
        {
            return $"decimal.Parse({valueExpression}.S, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture)";
        }

        // For DateTimeOffset with format
        if (baseType is "DateTimeOffset" or "System.DateTimeOffset")
        {
            return $"DateTimeOffset.ParseExact({valueExpression}.S, \"{format}\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None)";
        }

        // Default: parse as string
        return $"{valueExpression}.S";
    }

    private static void GenerateFromDynamoDbMultiMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Creates an entity instance from multiple DynamoDB items (composite entity support).");
        sb.AppendLine("        /// For single-item entities, uses the first item. For multi-item entities, combines all items.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <typeparam name=\"TSelf\">The entity type implementing IDynamoDbEntity.</typeparam>");
        sb.AppendLine("        /// <param name=\"items\">The collection of DynamoDB items to map from.</param>");
        sb.AppendLine("        /// <param name=\"options\">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>");
        sb.AppendLine("        /// <returns>A mapped entity instance.</returns>");
        sb.AppendLine("        /// <exception cref=\"ArgumentException\">Thrown when items collection is null or empty.</exception>");
        sb.AppendLine("        /// <exception cref=\"DynamoDbMappingException\">Thrown when mapping fails due to data conversion issues.</exception>");
        sb.AppendLine($"        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        
        // Generate entry logging for multi-item
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            options?.Logger?.LogTrace(LogEventIds.MappingFromDynamoDbStart,");
        sb.AppendLine($"                \"Starting FromDynamoDb mapping for {{EntityType}} with {{ItemCount}} items\",");
        sb.AppendLine($"                \"{entity.ClassName}\", items?.Count ?? 0);");
        sb.AppendLine("            #endif");
        sb.AppendLine();
        
        sb.AppendLine("            if (items == null || items.Count == 0)");
        sb.AppendLine($"                throw new ArgumentException(\"Items collection cannot be null or empty\", nameof(items));");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");

        if (entity.IsMultiItemEntity)
        {
            GenerateMultiItemFromDynamoDb(sb, entity);
        }
        else
        {
            sb.AppendLine("                // Single-item entity: use the first item");
            sb.AppendLine("                return FromDynamoDb<TSelf>(items[0], options);");
        }

        sb.AppendLine("            }");
        sb.AppendLine("            catch (DynamoDbMappingException)");
        sb.AppendLine("            {");
        sb.AppendLine("                // Re-throw mapping exceptions as-is");
        sb.AppendLine("                throw;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                throw DynamoDbMappingException.EntityConstructionFailed(");
        sb.AppendLine($"                    typeof({entity.ClassName}),");
        sb.AppendLine("                    items.FirstOrDefault() ?? new Dictionary<string, AttributeValue>(),");
        sb.AppendLine("                    ex)");
        sb.AppendLine("                    .WithContext(\"ItemCount\", items.Count)");
        sb.AppendLine("                    .WithContext(\"MappingType\", \"MultiItem\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GenerateMultiItemFromDynamoDb(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine("            // Multi-item entity: combine all items into a single entity");
        sb.AppendLine($"            var entity = new {entity.ClassName}();");
        sb.AppendLine();

        // First, populate non-collection properties from the first item (or any item that has them)
        var nonCollectionProperties = entity.Properties.Where(p => p.HasAttributeMapping && !p.IsCollection).ToArray();
        if (nonCollectionProperties.Length > 0)
        {
            sb.AppendLine("            // Populate non-collection properties from first available item");
            sb.AppendLine("            foreach (var item in items)");
            sb.AppendLine("            {");

            foreach (var property in nonCollectionProperties)
            {
                sb.AppendLine($"                if (item.TryGetValue(\"{property.AttributeName}\", out var {property.PropertyName.ToLowerInvariant()}Value))");
                sb.AppendLine("                {");
                sb.AppendLine($"                    entity.{property.PropertyName} = {GetFromAttributeValueExpression(property, $"{property.PropertyName.ToLowerInvariant()}Value")};");
                sb.AppendLine("                }");
            }

            sb.AppendLine("                break; // Use first item for non-collection properties");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // Then, populate collection properties by grouping items
        var collectionProperties = entity.Properties.Where(p => p.IsCollection && p.HasAttributeMapping).ToArray();
        foreach (var collectionProperty in collectionProperties)
        {
            GenerateCollectionPropertyFromItems(sb, entity, collectionProperty);
        }

        // Finally, populate related entity properties based on sort key patterns
        if (entity.Relationships.Length > 0)
        {
            GenerateRelatedEntityMapping(sb, entity);
        }

        sb.AppendLine("            return (TSelf)(object)entity;");
    }

    private static void GenerateCollectionPropertyFromItems(StringBuilder sb, EntityModel entity, PropertyModel collectionProperty)
    {
        var elementType = GetCollectionElementType(collectionProperty.PropertyType);

        sb.AppendLine($"            // Populate {collectionProperty.PropertyName} collection from items");
        sb.AppendLine($"            var {collectionProperty.PropertyName.ToLowerInvariant()}List = new List<{elementType}>();");
        sb.AppendLine();

        // Filter items that contain this collection's attribute
        sb.AppendLine("            foreach (var item in items)");
        sb.AppendLine("            {");
        sb.AppendLine($"                if (item.TryGetValue(\"{collectionProperty.AttributeName}\", out var {collectionProperty.PropertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("                {");

        if (IsComplexType(elementType))
        {
            // For complex types, we'd need to reconstruct the object
            sb.AppendLine($"                    // TODO: Implement complex type reconstruction for {elementType}");
            sb.AppendLine($"                    // For now, create default instance");
            sb.AppendLine($"                    var {collectionProperty.PropertyName.ToLowerInvariant()}Item = new {elementType}();");
            sb.AppendLine($"                    {collectionProperty.PropertyName.ToLowerInvariant()}List.Add({collectionProperty.PropertyName.ToLowerInvariant()}Item);");
        }
        else
        {
            // For primitive types, convert directly
            sb.AppendLine($"                    var {collectionProperty.PropertyName.ToLowerInvariant()}Item = {GetFromAttributeValueExpression(new PropertyModel { PropertyType = elementType, IsNullable = false }, $"{collectionProperty.PropertyName.ToLowerInvariant()}Value")};");
            sb.AppendLine($"                    {collectionProperty.PropertyName.ToLowerInvariant()}List.Add({collectionProperty.PropertyName.ToLowerInvariant()}Item);");
        }

        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            entity.{collectionProperty.PropertyName} = {collectionProperty.PropertyName.ToLowerInvariant()}List;");
        sb.AppendLine();
    }

    private static void GenerateGetPartitionKeyMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Extracts the partition key value from a DynamoDB item.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)");
        sb.AppendLine("        {");

        var partitionKeyProperty = entity.PartitionKeyProperty;
        if (partitionKeyProperty != null)
        {
            sb.AppendLine($"            if (item.TryGetValue(\"{partitionKeyProperty.AttributeName}\", out var pkValue))");
            sb.AppendLine("            {");
            sb.AppendLine("                return pkValue.S != null ? pkValue.S : string.Empty;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return string.Empty;");
        }
        else
        {
            sb.AppendLine("            // No partition key defined");
            sb.AppendLine("            return string.Empty;");
        }

        sb.AppendLine("        }");
    }

    private static void GenerateMatchesEntityMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Determines whether a DynamoDB item matches this entity type.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)");
        sb.AppendLine("        {");

        // Check entity discriminator first if present
        if (!string.IsNullOrEmpty(entity.EntityDiscriminator))
        {
            sb.AppendLine($"            // Check entity discriminator");
            sb.AppendLine($"            if (item.TryGetValue(\"EntityType\", out var entityTypeValue))");
            sb.AppendLine("            {");
            sb.AppendLine($"                return entityTypeValue.S == \"{entity.EntityDiscriminator}\";");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // Use sort key pattern matching for entity type discrimination
        var sortKeyProperty = entity.SortKeyProperty;
        if (sortKeyProperty != null && !string.IsNullOrEmpty(entity.EntityDiscriminator))
        {
            sb.AppendLine("            // Check sort key pattern for entity type discrimination");
            sb.AppendLine($"            if (item.TryGetValue(\"{sortKeyProperty.AttributeName}\", out var sortKeyValue))");
            sb.AppendLine("            {");
            sb.AppendLine("                var sortKey = sortKeyValue.S != null ? sortKeyValue.S : string.Empty;");

            // Generate pattern matching based on entity discriminator
            if (entity.EntityDiscriminator.Contains("*"))
            {
                // Wildcard pattern matching
                var pattern = entity.EntityDiscriminator.Replace("*", "");
                sb.AppendLine($"                return sortKey.StartsWith(\"{pattern}\");");
            }
            else
            {
                // Exact pattern matching
                sb.AppendLine($"                return sortKey == \"{entity.EntityDiscriminator}\" || sortKey.StartsWith(\"{entity.EntityDiscriminator}#\");");
            }

            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // Check if required attributes exist
        var requiredAttributes = entity.Properties
            .Where(p => p.HasAttributeMapping && (p.IsPartitionKey || !p.IsNullable))
            .ToArray();

        if (requiredAttributes.Length > 0)
        {
            sb.AppendLine("            // Check if required attributes exist");
            foreach (var property in requiredAttributes)
            {
                sb.AppendLine($"            if (!item.ContainsKey(\"{property.AttributeName}\"))");
                sb.AppendLine("                return false;");
            }
            sb.AppendLine();
        }

        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
    }

    private static void GenerateGetEntityMetadataMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets metadata about the entity structure for future LINQ support.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static EntityMetadata GetEntityMetadata()");
        sb.AppendLine("        {");
        sb.AppendLine("            return new EntityMetadata");
        sb.AppendLine("            {");
        sb.AppendLine($"                TableName = \"{entity.TableName}\",");

        if (!string.IsNullOrEmpty(entity.EntityDiscriminator))
        {
            sb.AppendLine($"                EntityDiscriminator = \"{entity.EntityDiscriminator}\",");
        }

        sb.AppendLine($"                IsMultiItemEntity = false,");
        sb.AppendLine("                Properties = new PropertyMetadata[]");
        sb.AppendLine("                {");

        // Generate property metadata
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GeneratePropertyMetadata(sb, property);
        }

        sb.AppendLine("                },");
        sb.AppendLine("                Indexes = new IndexMetadata[]");
        sb.AppendLine("                {");

        // Generate index metadata
        foreach (var index in entity.Indexes)
        {
            GenerateIndexMetadata(sb, index);
        }

        sb.AppendLine("                },");
        sb.AppendLine("                Relationships = new RelationshipMetadata[]");
        sb.AppendLine("                {");

        // Generate relationship metadata
        foreach (var relationship in entity.Relationships)
        {
            GenerateRelationshipMetadata(sb, relationship);
        }

        sb.AppendLine("                }");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
    }

    private static void GeneratePropertyMetadata(StringBuilder sb, PropertyModel property)
    {
        sb.AppendLine("                    new PropertyMetadata");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        PropertyName = \"{property.PropertyName}\",");
        sb.AppendLine($"                        AttributeName = \"{property.AttributeName}\",");
        sb.AppendLine($"                        PropertyType = typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine($"                        IsPartitionKey = {property.IsPartitionKey.ToString().ToLowerInvariant()},");
        sb.AppendLine($"                        IsSortKey = {property.IsSortKey.ToString().ToLowerInvariant()},");
        sb.AppendLine($"                        IsCollection = {property.IsCollection.ToString().ToLowerInvariant()},");
        sb.AppendLine($"                        IsNullable = {property.IsNullable.ToString().ToLowerInvariant()},");

        // Add supported operations if available
        if (property.Queryable?.HasSupportedOperations == true)
        {
            var operations = string.Join(", ", property.Queryable.SupportedOperations.Select(op => $"DynamoDbOperation.{op}"));
            sb.AppendLine($"                        SupportedOperations = new[] {{ {operations} }},");
        }
        else if (property.IsPartitionKey)
        {
            // Partition keys only support equality in key conditions
            sb.AppendLine($"                        SupportedOperations = new[] {{ DynamoDbOperation.Equals }},");
        }
        else if (property.IsSortKey)
        {
            // Sort keys support range operations in key conditions
            sb.AppendLine($"                        SupportedOperations = new[] {{ DynamoDbOperation.Equals, DynamoDbOperation.BeginsWith, DynamoDbOperation.Between, DynamoDbOperation.GreaterThan, DynamoDbOperation.LessThan }},");
        }
        else
        {
            // Non-key properties support all operations in filter expressions
            sb.AppendLine($"                        SupportedOperations = new[] {{ DynamoDbOperation.Equals, DynamoDbOperation.GreaterThan, DynamoDbOperation.LessThan, DynamoDbOperation.Contains, DynamoDbOperation.In }},");
        }

        // Add available indexes if specified
        if (property.Queryable?.HasIndexRestrictions == true)
        {
            var indexes = string.Join(", ", property.Queryable.AvailableInIndexes.Select(idx => $"\"{idx}\""));
            sb.AppendLine($"                        AvailableInIndexes = new[] {{ {indexes} }},");
        }

        // Add key format if available
        if (property.KeyFormat != null)
        {
            sb.AppendLine("                        KeyFormat = new KeyFormatMetadata");
            sb.AppendLine("                        {");
            if (!string.IsNullOrEmpty(property.KeyFormat.Prefix))
            {
                sb.AppendLine($"                            Prefix = \"{property.KeyFormat.Prefix}\",");
            }
            if (property.KeyFormat.Separator != "#")
            {
                sb.AppendLine($"                            Separator = \"{property.KeyFormat.Separator}\"");
            }
            sb.AppendLine("                        },");
        }

        // Add format string if available
        if (!string.IsNullOrEmpty(property.Format))
        {
            sb.AppendLine($"                        Format = \"{property.Format}\",");
        }

        // Add IsEncrypted flag if property is encrypted
        if (property.Security?.IsEncrypted == true)
        {
            sb.AppendLine($"                        IsEncrypted = true,");
        }

        // Add IsSensitive flag if property is marked as sensitive
        if (property.Security?.IsSensitive == true)
        {
            sb.AppendLine($"                        IsSensitive = true,");
        }

        // Add DateTimeKind if specified
        if (property.DateTimeKind.HasValue)
        {
            sb.AppendLine($"                        DateTimeKind = DateTimeKind.{property.DateTimeKind.Value}");
        }

        sb.AppendLine("                    },");
    }

    private static void GenerateIndexMetadata(StringBuilder sb, IndexModel index)
    {
        sb.AppendLine("                    new IndexMetadata");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        IndexName = \"{index.IndexName}\",");
        sb.AppendLine($"                        PartitionKeyProperty = \"{index.PartitionKeyProperty}\",");

        if (!string.IsNullOrEmpty(index.SortKeyProperty))
        {
            sb.AppendLine($"                        SortKeyProperty = \"{index.SortKeyProperty}\",");
        }

        if (index.ProjectedProperties.Length > 0)
        {
            var projectedProps = string.Join(", ", index.ProjectedProperties.Select(p => $"\"{p}\""));
            sb.AppendLine($"                        ProjectedProperties = new[] {{ {projectedProps} }},");
        }
        else
        {
            sb.AppendLine("                        ProjectedProperties = Array.Empty<string>(),");
        }

        if (!string.IsNullOrEmpty(index.PartitionKeyFormat))
        {
            sb.AppendLine($"                        KeyFormat = \"{index.PartitionKeyFormat}\"");
        }

        sb.AppendLine("                    },");
    }

    private static void GenerateRelationshipMetadata(StringBuilder sb, RelationshipModel relationship)
    {
        sb.AppendLine("                    new RelationshipMetadata");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        PropertyName = \"{relationship.PropertyName}\",");
        sb.AppendLine($"                        SortKeyPattern = \"{relationship.SortKeyPattern}\",");

        if (!string.IsNullOrEmpty(relationship.EntityType))
        {
            sb.AppendLine($"                        EntityType = typeof({relationship.EntityType}),");
        }

        sb.AppendLine($"                        IsCollection = {relationship.IsCollection.ToString().ToLowerInvariant()}");
        sb.AppendLine("                    },");
    }

    private static string GetBaseType(string typeName)
    {
        // Remove nullable annotations and generic type parameters
        var baseType = typeName.TrimEnd('?');

        // Handle nullable value types like int?, bool?, etc.
        if (baseType.StartsWith("System.Nullable<") && baseType.EndsWith(">"))
        {
            var innerType = baseType.Substring(16, baseType.Length - 17); // Remove "System.Nullable<" and ">"
            return innerType;
        }

        return baseType;
    }

    private static string GetSimpleTypeName(string fullTypeName)
    {
        // Remove nullable annotations
        var typeName = fullTypeName.TrimEnd('?');
        
        // Extract simple type name without namespace
        // e.g., "TestNamespace.ProductAttributes" -> "ProductAttributes"
        var lastDotIndex = typeName.LastIndexOf('.');
        if (lastDotIndex >= 0 && lastDotIndex < typeName.Length - 1)
        {
            return typeName.Substring(lastDotIndex + 1);
        }
        
        return typeName;
    }

    private static string GetTypeForMetadata(string typeName)
    {
        // For metadata, we need the actual type without nullable annotations
        // The typeof operator cannot be used with nullable reference types (e.g., List<string>?)
        // so we strip the trailing ? for reference types
        var baseType = typeName.TrimEnd('?');

        // Convert common type aliases to full type names for typeof()
        return baseType switch
        {
            "string" => "string",
            "int" => "int",
            "long" => "long",
            "double" => "double",
            "float" => "float",
            "decimal" => "decimal",
            "bool" => "bool",
            "byte[]" => "byte[]",
            _ => baseType
        };
    }

    private static bool IsEnumType(string typeName)
    {
        // This is a simplified check - in a real implementation, we'd use semantic analysis
        // For now, assume any type not in our known primitives might be an enum
        var baseType = GetBaseType(typeName);
        var knownPrimitives = new[]
        {
            "string", "int", "long", "double", "float", "decimal", "bool", "DateTime", "DateTimeOffset",
            "Guid", "byte[]", "System.String", "System.Int32", "System.Int64", "System.Double",
            "System.Single", "System.Decimal", "System.Boolean", "System.DateTime", "System.DateTimeOffset",
            "System.Guid", "System.Byte[]", "Ulid", "System.Ulid"
        };

        return !knownPrimitives.Contains(baseType) &&
               !baseType.StartsWith("System.Collections.Generic.") &&
               !baseType.StartsWith("List<") &&
               !baseType.StartsWith("IList<") &&
               !baseType.StartsWith("ICollection<") &&
               !baseType.StartsWith("IEnumerable<");
    }

    private static string GetCollectionElementType(string collectionType)
    {
        // Remove nullable annotation if present
        var baseType = collectionType.TrimEnd('?');
        
        // Extract element type from collection types
        // For "HashSet<int>", we want to extract "int"
        // Start index is after "HashSet<" (8 characters)
        // End index is before ">" (length - 1)
        // Length to extract is: (length - 1) - 8 = length - 9
        if (baseType.StartsWith("HashSet<") && baseType.EndsWith(">"))
        {
            var startIndex = 8;
            var endIndex = baseType.Length - 1;
            return baseType.Substring(startIndex, endIndex - startIndex);
        }
        if (baseType.StartsWith("System.Collections.Generic.HashSet<") && baseType.EndsWith(">"))
        {
            var startIndex = 35;
            var endIndex = baseType.Length - 1;
            return baseType.Substring(startIndex, endIndex - startIndex);
        }
        if (baseType.StartsWith("List<") && baseType.EndsWith(">"))
        {
            var startIndex = 5;
            var endIndex = baseType.Length - 1;
            return baseType.Substring(startIndex, endIndex - startIndex);
        }
        if (baseType.StartsWith("IList<") && baseType.EndsWith(">"))
        {
            var startIndex = 6;
            var endIndex = baseType.Length - 1;
            return baseType.Substring(startIndex, endIndex - startIndex);
        }
        if (baseType.StartsWith("ICollection<") && baseType.EndsWith(">"))
        {
            var startIndex = 12;
            var endIndex = baseType.Length - 1;
            return baseType.Substring(startIndex, endIndex - startIndex);
        }
        if (baseType.StartsWith("IEnumerable<") && baseType.EndsWith(">"))
        {
            var startIndex = 12;
            var endIndex = baseType.Length - 1;
            return baseType.Substring(startIndex, endIndex - startIndex);
        }
        if (baseType.StartsWith("System.Collections.Generic.List<") && baseType.EndsWith(">"))
        {
            var startIndex = 32;  // Position after "System.Collections.Generic.List<"
            var endIndex = baseType.Length - 1;
            return baseType.Substring(startIndex, endIndex - startIndex);
        }

        // Default to object if we can't determine the element type
        return "object";
    }

    private static string GetCollectionTypeName(string collectionType)
    {
        // Remove nullable annotation if present
        var baseType = collectionType.TrimEnd('?');
        
        // Extract just the collection type name without the element type
        if (baseType.StartsWith("HashSet<") || baseType.StartsWith("System.Collections.Generic.HashSet<"))
        {
            return "HashSet";
        }
        if (baseType.StartsWith("List<") || baseType.StartsWith("System.Collections.Generic.List<"))
        {
            return "List";
        }
        if (baseType.StartsWith("IList<"))
        {
            return "List"; // Use concrete List for IList
        }
        if (baseType.StartsWith("ICollection<"))
        {
            return "List"; // Use concrete List for ICollection
        }
        if (baseType.StartsWith("IEnumerable<"))
        {
            return "List"; // Use concrete List for IEnumerable
        }

        // Default to List if we can't determine the collection type
        return "List";
    }

    private static bool IsComplexType(string typeName)
    {
        var baseType = GetBaseType(typeName);
        var primitiveTypes = new[]
        {
            "string", "int", "long", "double", "float", "decimal", "bool", "DateTime", "DateTimeOffset",
            "Guid", "byte[]", "System.String", "System.Int32", "System.Int64", "System.Double",
            "System.Single", "System.Decimal", "System.Boolean", "System.DateTime", "System.DateTimeOffset",
            "System.Guid", "System.Byte[]", "Ulid", "System.Ulid", "object"
        };

        return !primitiveTypes.Contains(baseType);
    }

    private static bool IsNumericType(string typeName)
    {
        var baseType = GetBaseType(typeName);
        var numericTypes = new[]
        {
            "int", "long", "double", "float", "decimal", "byte", "short", "uint", "ulong", "ushort",
            "System.Int32", "System.Int64", "System.Double", "System.Single", "System.Decimal",
            "System.Byte", "System.Int16", "System.UInt32", "System.UInt64", "System.UInt16"
        };

        return numericTypes.Contains(baseType);
    }

    private static string GetToAttributeValueExpressionForCollectionElement(string elementType, string valueExpression)
    {
        var baseType = GetBaseType(elementType);

        return baseType switch
        {
            "string" => $"new AttributeValue {{ S = {valueExpression} }}",
            "int" or "System.Int32" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "long" or "System.Int64" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "double" or "System.Double" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "float" or "System.Single" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "decimal" or "System.Decimal" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "bool" or "System.Boolean" => $"new AttributeValue {{ BOOL = {valueExpression} }}",
            "DateTime" or "System.DateTime" => $"new AttributeValue {{ S = {valueExpression}.ToString(\"O\") }}",
            "DateTimeOffset" or "System.DateTimeOffset" => $"new AttributeValue {{ S = {valueExpression}.ToString(\"O\") }}",
            "Guid" or "System.Guid" => $"new AttributeValue {{ S = {valueExpression}.ToString() }}",
            "Ulid" or "System.Ulid" => $"new AttributeValue {{ S = {valueExpression}.ToString() }}",
            _ when IsEnumType(elementType) => $"new AttributeValue {{ S = {valueExpression}.ToString() }}",
            _ => $"new AttributeValue {{ S = {valueExpression} != null ? {valueExpression}.ToString() : \"\" }}"
        };
    }

    private static string GetNumericConversionExpression(string numericType)
    {
        return numericType switch
        {
            "int" or "System.Int32" => "x => int.Parse(x)",
            "long" or "System.Int64" => "x => long.Parse(x)",
            "double" or "System.Double" => "x => double.Parse(x)",
            "float" or "System.Single" => "x => float.Parse(x)",
            "decimal" or "System.Decimal" => "x => decimal.Parse(x)",
            "byte" or "System.Byte" => "x => byte.Parse(x)",
            "short" or "System.Int16" => "x => short.Parse(x)",
            "uint" or "System.UInt32" => "x => uint.Parse(x)",
            "ulong" or "System.UInt64" => "x => ulong.Parse(x)",
            "ushort" or "System.UInt16" => "x => ushort.Parse(x)",
            _ => "x => x" // fallback to string
        };
    }
    private static void GenerateRelatedEntityMapping(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine("            // Populate related entity properties based on sort key patterns");

        var sortKeyProperty = entity.SortKeyProperty;
        if (sortKeyProperty == null)
        {
            sb.AppendLine("            // No sort key defined - cannot map related entities");
            return;
        }

        foreach (var relationship in entity.Relationships)
        {
            sb.AppendLine();
            sb.AppendLine($"            // Map related entity: {relationship.PropertyName}");

            if (relationship.IsCollection)
            {
                GenerateRelatedEntityCollectionMapping(sb, entity, relationship, sortKeyProperty);
            }
            else
            {
                GenerateRelatedEntitySingleMapping(sb, entity, relationship, sortKeyProperty);
            }
        }
    }

    private static void GenerateRelatedEntityCollectionMapping(StringBuilder sb, EntityModel entity, RelationshipModel relationship, PropertyModel sortKeyProperty)
    {
        var elementType = GetCollectionElementType(relationship.PropertyType);

        sb.AppendLine($"            var {relationship.PropertyName.ToLowerInvariant()}Items = new List<{elementType}>();");
        sb.AppendLine("            foreach (var item in items)");
        sb.AppendLine("            {");
        sb.AppendLine($"                if (item.TryGetValue(\"{sortKeyProperty.AttributeName}\", out var sortKeyValue))");
        sb.AppendLine("                {");
        sb.AppendLine("                    var sortKey = sortKeyValue.S != null ? sortKeyValue.S : string.Empty;");

        // Generate pattern matching logic
        GenerateSortKeyPatternMatching(sb, relationship.SortKeyPattern);

        sb.AppendLine("                    {");

        if (!string.IsNullOrEmpty(relationship.EntityType))
        {
            // Use specific entity type for mapping
            sb.AppendLine($"                        // Map to specific entity type: {relationship.EntityType}");
            sb.AppendLine($"                        if ({relationship.EntityType}.MatchesEntity(item))");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            var relatedEntity = {relationship.EntityType}.FromDynamoDb<{relationship.EntityType}>(item, options);");
            sb.AppendLine($"                            {relationship.PropertyName.ToLowerInvariant()}Items.Add(relatedEntity);");
            sb.AppendLine("                        }");
        }
        else
        {
            // Generic mapping - create instance of element type
            sb.AppendLine($"                        // Generic mapping to {elementType}");
            sb.AppendLine($"                        var relatedEntity = new {elementType}();");
            sb.AppendLine($"                        // TODO: Implement generic property mapping for {elementType}");
            sb.AppendLine($"                        {relationship.PropertyName.ToLowerInvariant()}Items.Add(relatedEntity);");
        }

        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine($"            entity.{relationship.PropertyName} = {relationship.PropertyName.ToLowerInvariant()}Items;");
    }

    private static void GenerateRelatedEntitySingleMapping(StringBuilder sb, EntityModel entity, RelationshipModel relationship, PropertyModel sortKeyProperty)
    {
        var propertyType = relationship.EntityType != null ? relationship.EntityType : GetBaseType(relationship.PropertyType);

        sb.AppendLine("            foreach (var item in items)");
        sb.AppendLine("            {");
        sb.AppendLine($"                if (item.TryGetValue(\"{sortKeyProperty.AttributeName}\", out var sortKeyValue))");
        sb.AppendLine("                {");
        sb.AppendLine("                    var sortKey = sortKeyValue.S != null ? sortKeyValue.S : string.Empty;");

        // Generate pattern matching logic
        GenerateSortKeyPatternMatching(sb, relationship.SortKeyPattern);

        sb.AppendLine("                    {");

        if (!string.IsNullOrEmpty(relationship.EntityType))
        {
            // Use specific entity type for mapping
            sb.AppendLine($"                        // Map to specific entity type: {relationship.EntityType}");
            sb.AppendLine($"                        if ({relationship.EntityType}.MatchesEntity(item))");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            entity.{relationship.PropertyName} = {relationship.EntityType}.FromDynamoDb<{relationship.EntityType}>(item, options);");
            sb.AppendLine("                            break; // Found the related entity");
            sb.AppendLine("                        }");
        }
        else
        {
            // Generic mapping - create instance of property type
            sb.AppendLine($"                        // Generic mapping to {propertyType}");
            sb.AppendLine($"                        entity.{relationship.PropertyName} = new {propertyType}();");
            sb.AppendLine($"                        // TODO: Implement generic property mapping for {propertyType}");
            sb.AppendLine("                        break; // Found the related entity");
        }

        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateSortKeyPatternMatching(StringBuilder sb, string sortKeyPattern)
    {
        if (sortKeyPattern.Contains("*"))
        {
            // Wildcard pattern matching
            var prefix = sortKeyPattern.Replace("*", "");
            sb.AppendLine($"                    if (sortKey.StartsWith(\"{prefix}\"))");
        }
        else
        {
            // Exact pattern matching
            sb.AppendLine($"                    if (sortKey == \"{sortKeyPattern}\" || sortKey.StartsWith(\"{sortKeyPattern}#\"))");
        }
    }

    private static void GenerateComputedKeyLogic(StringBuilder sb, PropertyModel computedProperty)
    {
        var computedKey = computedProperty.ComputedKey!;
        var propertyName = computedProperty.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);

        if (computedKey.HasCustomFormat)
        {
            // Use custom format string
            var formatArgs = string.Join(", ", computedKey.SourceProperties.Select(sp => $"typedEntity.{EscapePropertyName(sp)}"));
            sb.AppendLine($"            typedEntity.{escapedPropertyName} = string.Format(\"{computedKey.Format}\", {formatArgs});");
        }
        else
        {
            // Use separator-based concatenation
            var sourceValues = string.Join($" + \"{computedKey.Separator}\" + ", computedKey.SourceProperties.Select(sp => $"typedEntity.{EscapePropertyName(sp)}"));
            sb.AppendLine($"            typedEntity.{escapedPropertyName} = {sourceValues};");
        }
    }

    private static void GenerateExtractedKeyLogic(StringBuilder sb, PropertyModel extractedProperty)
    {
        var extractedKey = extractedProperty.ExtractedKey!;
        var propertyName = extractedProperty.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var sourceProperty = extractedKey.SourceProperty;
        var escapedSourceProperty = EscapePropertyName(sourceProperty);
        var index = extractedKey.Index;
        var separator = extractedKey.Separator;

        sb.AppendLine($"            if (!string.IsNullOrEmpty(entity.{escapedSourceProperty}))");
        sb.AppendLine("            {");
        sb.AppendLine($"                var {sourceProperty.ToLowerInvariant()}Parts = entity.{escapedSourceProperty}.Split('{separator}');");
        sb.AppendLine($"                if ({sourceProperty.ToLowerInvariant()}Parts.Length > {index})");
        sb.AppendLine("                {");
        sb.AppendLine($"                    entity.{escapedPropertyName} = {sourceProperty.ToLowerInvariant()}Parts[{index}];");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateEncryptedPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var cacheTtlSeconds = property.Security?.EncryptionConfig?.CacheTtlSeconds ?? 300;

        sb.AppendLine($"            // Encrypt {propertyName}");
        sb.AppendLine("            if (fieldEncryptor != null)");
        sb.AppendLine("            {");

        // Handle nullable properties
        if (property.IsNullable)
        {
            sb.AppendLine($"                if (typedEntity.{escapedPropertyName} != null)");
            sb.AppendLine("                {");
        }

        // Convert property value to bytes - use null-forgiving operator for nullable properties since we've already checked for null
        var propertyValueExpression = property.IsNullable 
            ? GetPropertyValueAsStringWithNullForgiving(property, propertyName)
            : GetPropertyValueAsString(property, propertyName);
        sb.AppendLine($"                    var {propertyName}Plaintext = System.Text.Encoding.UTF8.GetBytes({propertyValueExpression});");
        sb.AppendLine();

        // Create encryption context
        sb.AppendLine("                    var encryptionContext = new FieldEncryptionContext");
        sb.AppendLine("                    {");
        sb.AppendLine("                        ContextId = DynamoDbOperationContext.EncryptionContextId,");
        sb.AppendLine($"                        CacheTtlSeconds = {cacheTtlSeconds},");
        
        // Add EntityId for external blob storage path
        var partitionKeyProperty = entity.PartitionKeyProperty;
        if (partitionKeyProperty != null)
        {
            sb.AppendLine($"                        EntityId = typedEntity.{partitionKeyProperty.PropertyName}?.ToString()");
        }
        else
        {
            sb.AppendLine("                        EntityId = null");
        }
        
        sb.AppendLine("                    };");
        sb.AppendLine();

        // Call EncryptAsync
        sb.AppendLine($"                    var {propertyName}Ciphertext = await fieldEncryptor.EncryptAsync(");
        sb.AppendLine($"                        {propertyName}Plaintext,");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine("                        encryptionContext,");
        sb.AppendLine("                        cancellationToken);");
        sb.AppendLine();

        // Store as Binary (B) AttributeValue
        sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ B = new MemoryStream({propertyName}Ciphertext) }};");

        if (property.IsNullable)
        {
            sb.AppendLine("                }");
        }

        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                throw new InvalidOperationException(\"Property {propertyName} is marked with [Encrypted] but no IFieldEncryptor is configured. Add the Oproto.FluentDynamoDb.Encryption.Kms package and configure encryption.\");");
        sb.AppendLine("            }");
    }

    private static void GenerateEncryptedPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        var cacheTtlSeconds = property.Security?.EncryptionConfig?.CacheTtlSeconds ?? 300;

        sb.AppendLine($"            // Decrypt {propertyName}");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        sb.AppendLine("                if (fieldEncryptor != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        
        // Read Binary (B) AttributeValue
        sb.AppendLine($"                        if ({propertyName.ToLowerInvariant()}Value.B != null)");
        sb.AppendLine("                        {");
        sb.AppendLine($"                            byte[] {propertyName}Ciphertext;");
        sb.AppendLine($"                            using (var ms = {propertyName.ToLowerInvariant()}Value.B)");
        sb.AppendLine("                            {");
        sb.AppendLine($"                                {propertyName}Ciphertext = ms.ToArray();");
        sb.AppendLine("                            }");
        sb.AppendLine();

        // Create encryption context
        sb.AppendLine("                            var encryptionContext = new FieldEncryptionContext");
        sb.AppendLine("                            {");
        sb.AppendLine("                                ContextId = DynamoDbOperationContext.EncryptionContextId,");
        sb.AppendLine($"                                CacheTtlSeconds = {cacheTtlSeconds}");
        sb.AppendLine("                            };");
        sb.AppendLine();

        // Call DecryptAsync
        sb.AppendLine($"                            var {propertyName}Plaintext = await fieldEncryptor.DecryptAsync(");
        sb.AppendLine($"                                {propertyName}Ciphertext,");
        sb.AppendLine($"                                \"{propertyName}\",");
        sb.AppendLine("                                encryptionContext,");
        sb.AppendLine("                                cancellationToken);");
        sb.AppendLine();

        // Convert bytes back to property type
        sb.AppendLine($"                            var {propertyName}String = System.Text.Encoding.UTF8.GetString({propertyName}Plaintext);");
        sb.AppendLine($"                            entity.{escapedPropertyName} = {ConvertStringToPropertyType(property, $"{propertyName}String")};");
        
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch (Exception ex)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                            typeof({entity.ClassName}),");
        sb.AppendLine($"                            \"{propertyName}\",");
        sb.AppendLine($"                            {propertyName.ToLowerInvariant()}Value,");
        sb.AppendLine($"                            typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                            ex);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    throw new InvalidOperationException(\"Property {propertyName} is marked with [Encrypted] but no IFieldEncryptor is configured. Add the Oproto.FluentDynamoDb.Encryption.Kms package and configure encryption.\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static string GetPropertyValueAsString(PropertyModel property, string propertyName)
    {
        var baseType = GetBaseType(property.PropertyType);
        var escapedPropertyName = EscapePropertyName(propertyName);

        // For string properties, use directly
        if (baseType == "string" || baseType == "System.String")
        {
            return $"typedEntity.{escapedPropertyName}";
        }

        // For other types, convert to string first
        return $"typedEntity.{escapedPropertyName}.ToString()";
    }

    /// <summary>
    /// Gets the property value as a string expression with null-forgiving operator.
    /// Used when we've already checked for null and need to suppress nullable warnings.
    /// </summary>
    private static string GetPropertyValueAsStringWithNullForgiving(PropertyModel property, string propertyName)
    {
        var baseType = GetBaseType(property.PropertyType);
        var escapedPropertyName = EscapePropertyName(propertyName);

        // For string properties, use directly with null-forgiving operator
        if (baseType == "string" || baseType == "System.String")
        {
            return $"typedEntity.{escapedPropertyName}!";
        }

        // For other types, convert to string first (ToString() on nullable value types after null check is safe)
        return $"typedEntity.{escapedPropertyName}!.ToString()!";
    }

    private static string ConvertStringToPropertyType(PropertyModel property, string stringVariable)
    {
        var baseType = GetBaseType(property.PropertyType);

        // For string properties, use directly
        if (baseType == "string" || baseType == "System.String")
        {
            return stringVariable;
        }

        // For int
        if (baseType == "int" || baseType == "System.Int32")
        {
            return $"int.Parse({stringVariable})";
        }

        // For long
        if (baseType == "long" || baseType == "System.Int64")
        {
            return $"long.Parse({stringVariable})";
        }

        // For double
        if (baseType == "double" || baseType == "System.Double")
        {
            return $"double.Parse({stringVariable})";
        }

        // For decimal
        if (baseType == "decimal" || baseType == "System.Decimal")
        {
            return $"decimal.Parse({stringVariable})";
        }

        // For bool
        if (baseType == "bool" || baseType == "System.Boolean")
        {
            return $"bool.Parse({stringVariable})";
        }

        // For DateTime
        if (baseType == "DateTime" || baseType == "System.DateTime")
        {
            return $"DateTime.Parse({stringVariable})";
        }

        // For Guid
        if (baseType == "Guid" || baseType == "System.Guid")
        {
            return $"Guid.Parse({stringVariable})";
        }

        // Default: assume the type has a Parse method or constructor that takes a string
        return $"{baseType}.Parse({stringVariable})";
    }

    /// <summary>
    /// Escapes a property name if it's a C# reserved keyword by adding @ prefix.
    /// </summary>
    /// <param name="propertyName">The property name to escape.</param>
    /// <returns>The escaped property name.</returns>
    private static string EscapePropertyName(string propertyName)
    {
        // C# reserved keywords that need escaping
        var csharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while"
        };

        // DynamoDB reserved words that might also be used as property names
        var dynamoDbKeywords = new HashSet<string>
        {
            "ABORT", "ABSOLUTE", "ACTION", "ADD", "AFTER", "AGENT", "AGGREGATE", "ALL", "ALLOCATE",
            "ALTER", "ANALYZE", "AND", "ANY", "ARCHIVE", "ARE", "ARRAY", "AS", "ASC", "ASCII",
            "ASENSITIVE", "ASSERTION", "ASYMMETRIC", "AT", "ATOMIC", "ATTACH", "ATTRIBUTE", "AUTH",
            "AUTHORIZATION", "AUTHORIZE", "AUTO", "AVG", "BACK", "BACKUP", "BASE", "BATCH", "BEFORE",
            "BEGIN", "BETWEEN", "BIGINT", "BINARY", "BIT", "BLOB", "BLOCK", "BOOLEAN", "BOTH",
            "BREADTH", "BUCKET", "BULK", "BY", "BYTE", "CALL", "CALLED", "CALLING", "CAPACITY",
            "CASCADE", "CASCADED", "CASE", "CAST", "CATALOG", "CHAR", "CHARACTER", "CHECK", "CLASS",
            "CLOB", "CLOSE", "CLUSTER", "CLUSTERED", "CLUSTERING", "CLUSTERS", "COALESCE", "COLLATE",
            "COLLATION", "COLLECTION", "COLUMN", "COLUMNS", "COMBINE", "COMMENT", "COMMIT", "COMPACT",
            "COMPILE", "COMPRESS", "CONDITION", "CONFLICT", "CONNECT", "CONNECTION", "CONSISTENCY",
            "CONSISTENT", "CONSTRAINT", "CONSTRAINTS", "CONSTRUCTOR", "CONSUMED", "CONTINUE",
            "CONVERT", "COPY", "CORRESPONDING", "COUNT", "COUNTER", "CREATE", "CROSS", "CUBE",
            "CURRENT", "CURSOR", "CYCLE", "DATA", "DATABASE", "DATE", "DATETIME", "DAY", "DEALLOCATE",
            "DEC", "DECIMAL", "DECLARE", "DEFAULT", "DEFERRABLE", "DEFERRED", "DEFINE", "DEFINED",
            "DEFINITION", "DELETE", "DELIMITED", "DEPTH", "DEREF", "DESC", "DESCRIBE", "DESCRIPTOR",
            "DETACH", "DETERMINISTIC", "DIAGNOSTICS", "DIRECTORIES", "DISABLE", "DISCONNECT",
            "DISTINCT", "DISTRIBUTE", "DO", "DOMAIN", "DOUBLE", "DROP", "DUMP", "DURATION", "DYNAMIC",
            "EACH", "ELEMENT", "ELSE", "ELSEIF", "EMPTY", "ENABLE", "END", "EQUAL", "EQUALS", "ERROR",
            "ESCAPE", "ESCAPED", "EVAL", "EVALUATE", "EXCEEDED", "EXCEPT", "EXCEPTION", "EXCEPTIONS",
            "EXCLUSIVE", "EXEC", "EXECUTE", "EXISTS", "EXIT", "EXPLAIN", "EXPLODE", "EXPORT",
            "EXPRESSION", "EXTENDED", "EXTERNAL", "EXTRACT", "FAIL", "FALSE", "FAMILY", "FETCH",
            "FIELDS", "FILE", "FILTER", "FILTERING", "FINAL", "FINISH", "FIRST", "FIXED", "FLATTERN",
            "FLOAT", "FOR", "FORCE", "FOREIGN", "FORMAT", "FORWARD", "FOUND", "FREE", "FROM", "FULL",
            "FUNCTION", "FUNCTIONS", "GENERAL", "GENERATE", "GET", "GLOB", "GLOBAL", "GO", "GOTO",
            "GRANT", "GREATER", "GROUP", "GROUPING", "HANDLER", "HASH", "HAVE", "HAVING", "HEAP",
            "HIDDEN", "HOLD", "HOUR", "IDENTIFIED", "IDENTITY", "IF", "IGNORE", "IMMEDIATE", "IMPORT",
            "IN", "INCLUDING", "INCLUSIVE", "INCREMENT", "INCREMENTAL", "INDEX", "INDEXED", "INDEXES",
            "INDICATOR", "INFINITE", "INITIALLY", "INLINE", "INNER", "INNTER", "INOUT", "INPUT",
            "INSENSITIVE", "INSERT", "INSTEAD", "INT", "INTEGER", "INTERSECT", "INTERVAL", "INTO",
            "INVALIDATE", "IS", "ISOLATION", "ITEM", "ITEMS", "ITERATE", "JOIN", "KEY", "KEYS",
            "LAG", "LANGUAGE", "LARGE", "LAST", "LATERAL", "LEAD", "LEADING", "LEAVE", "LEFT",
            "LENGTH", "LESS", "LEVEL", "LIKE", "LIMIT", "LIMITED", "LINES", "LIST", "LOAD", "LOCAL",
            "LOCALTIME", "LOCALTIMESTAMP", "LOCATION", "LOCATOR", "LOCK", "LOCKS", "LOG", "LOGED",
            "LONG", "LOOP", "LOWER", "MAP", "MATCH", "MATERIALIZED", "MAX", "MAXLEN", "MEMBER",
            "MERGE", "METHOD", "METRICS", "MIN", "MINUS", "MINUTE", "MISSING", "MOD", "MODE",
            "MODIFIES", "MODIFY", "MODULE", "MONTH", "MULTI", "MULTISET", "NAME", "NAMES", "NATIONAL",
            "NATURAL", "NCHAR", "NCLOB", "NEW", "NEXT", "NO", "NONE", "NOT", "NULL", "NULLIF",
            "NUMBER", "NUMERIC", "OBJECT", "OF", "OFFLINE", "OFFSET", "OLD", "ON", "ONLINE", "ONLY",
            "OPAQUE", "OPEN", "OPERATOR", "OPTION", "OR", "ORDER", "ORDINALITY", "OTHER", "OTHERS",
            "OUT", "OUTER", "OUTPUT", "OVER", "OVERLAPS", "OVERRIDE", "OWNER", "PAD", "PARALLEL",
            "PARAMETER", "PARAMETERS", "PARTIAL", "PARTITION", "PARTITIONED", "PARTITIONS", "PATH",
            "PERCENT", "PERCENTILE", "PERMISSION", "PERMISSIONS", "PIPE", "PIPELINED", "PLAN", "POOL",
            "POSITION", "PRECISION", "PREPARE", "PRESERVE", "PRIMARY", "PRIOR", "PRIVATE", "PRIVILEGES",
            "PROCEDURE", "PROCESSED", "PROJECT", "PROJECTION", "PROPERTY", "PROVISIONING", "PUBLIC",
            "PUT", "QUERY", "QUIT", "QUORUM", "RAISE", "RANDOM", "RANGE", "RANK", "RAW", "READ",
            "READS", "REAL", "REBUILD", "RECORD", "RECURSIVE", "REDUCE", "REF", "REFERENCE",
            "REFERENCES", "REFERENCING", "REGEXP", "REGION", "REINDEX", "RELATIVE", "RELEASE",
            "REMAINDER", "RENAME", "REPEAT", "REPLACE", "REQUEST", "RESET", "RESIGNAL", "RESOURCE",
            "RESPONSE", "RESTORE", "RESTRICT", "RESULT", "RETURN", "RETURNING", "RETURNS", "REVERSE",
            "REVOKE", "RIGHT", "ROLE", "ROLES", "ROLLBACK", "ROLLUP", "ROUTINE", "ROW", "ROWS",
            "RULE", "RULES", "SAMPLE", "SATISFIES", "SAVE", "SAVEPOINT", "SCAN", "SCHEMA", "SCOPE",
            "SCROLL", "SEARCH", "SECOND", "SECTION", "SEGMENT", "SEGMENTS", "SELECT", "SELF",
            "SEMI", "SENSITIVE", "SEPARATE", "SEQUENCE", "SERIALIZABLE", "SESSION", "SET", "SETS",
            "SHARD", "SHARE", "SHARED", "SHORT", "SHOW", "SIGNAL", "SIMILAR", "SIZE", "SKEWED",
            "SMALLINT", "SNAPSHOT", "SOME", "SOURCE", "SPACE", "SPACES", "SPARSE", "SPECIFIC",
            "SPECIFICTYPE", "SPLIT", "SQL", "SQLCODE", "SQLERROR", "SQLEXCEPTION", "SQLSTATE",
            "SQLWARNING", "START", "STATE", "STATIC", "STATUS", "STORAGE", "STORE", "STORED",
            "STREAM", "STRING", "STRUCT", "STYLE", "SUB", "SUBMULTISET", "SUBPARTITION", "SUBSTRING",
            "SUBTYPE", "SUM", "SUPER", "SYMMETRIC", "SYNONYM", "SYSTEM", "TABLE", "TABLESAMPLE",
            "TEMP", "TEMPORARY", "TERMINATED", "TEXT", "THAN", "THEN", "THROUGHPUT", "TIME",
            "TIMESTAMP", "TIMEZONE", "TINYINT", "TO", "TOKEN", "TOTAL", "TOUCH", "TRAILING",
            "TRANSACTION", "TRANSFORM", "TRANSLATE", "TRANSLATION", "TREAT", "TRIGGER", "TRIM",
            "TRUE", "TRUNCATE", "TTL", "TUPLE", "TYPE", "UNDER", "UNDO", "UNION", "UNIQUE", "UNIT",
            "UNKNOWN", "UNLOGGED", "UNNEST", "UNPROCESSED", "UNSIGNED", "UNTIL", "UPDATE", "UPPER",
            "URL", "USAGE", "USE", "USER", "USERS", "USING", "UUID", "VACUUM", "VALUE", "VALUED",
            "VALUES", "VARCHAR", "VARIABLE", "VARIANCE", "VARINT", "VARYING", "VIEW", "VIEWS",
            "VIRTUAL", "VOID", "WAIT", "WHEN", "WHENEVER", "WHERE", "WHILE", "WINDOW", "WITH",
            "WITHIN", "WITHOUT", "WORK", "WRAPPED", "WRITE", "YEAR", "ZONE"
        };

        // Check if it's a C# keyword (case-sensitive)
        if (csharpKeywords.Contains(propertyName))
        {
            return "@" + propertyName;
        }

        // Check if it's a DynamoDB reserved word (case-insensitive)
        if (dynamoDbKeywords.Contains(propertyName.ToUpperInvariant()))
        {
            return "@" + propertyName;
        }

        return propertyName;
    }

    /// <summary>
    /// Checks if a property type is GeoLocation.
    /// </summary>
    private static bool IsGeoLocationType(string propertyType)
    {
        var baseType = GetBaseType(propertyType);
        return baseType == "GeoLocation" || 
               baseType == "Oproto.FluentDynamoDb.Geospatial.GeoLocation";
    }

    /// <summary>
    /// Generates code to serialize a GeoLocation property to a spatial index string AttributeValue.
    /// Supports GeoHash, S2, and H3 spatial indexing systems.
    /// Optionally stores full-resolution coordinates as separate attributes.
    /// </summary>
    private static void GenerateGeoLocationPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        
        // Determine spatial index type (default to GeoHash for backward compatibility)
        var spatialIndexType = property.SpatialIndexType ?? "GeoHash";
        
        // Get precision/level/resolution based on index type
        string encodingCall;
        string indexTypeName;
        
        switch (spatialIndexType)
        {
            case "S2":
                var s2Level = property.S2Level ?? 16; // Default S2 level is 16
                encodingCall = $"ToS2Token({s2Level})";
                indexTypeName = "S2";
                break;
            
            case "H3":
                var h3Resolution = property.H3Resolution ?? 9; // Default H3 resolution is 9
                encodingCall = $"ToH3Index({h3Resolution})";
                indexTypeName = "H3";
                break;
            
            case "GeoHash":
            default:
                var geoHashPrecision = property.GeoHashPrecision ?? 6; // Default GeoHash precision is 6
                encodingCall = $"ToGeoHash({geoHashPrecision})";
                indexTypeName = "GeoHash";
                break;
        }

        // Check if coordinate storage is configured
        var hasCoordinateStorage = property.HasCoordinateStorage;
        
        if (hasCoordinateStorage)
        {
            sb.AppendLine($"            // Serialize GeoLocation property {propertyName} to {indexTypeName} with coordinate storage");
        }
        else
        {
            sb.AppendLine($"            // Serialize GeoLocation property {propertyName} to {indexTypeName}");
        }
        
        // Handle nullable GeoLocation
        if (property.IsNullable)
        {
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var {propertyName.ToLowerInvariant()}Index = typedEntity.{escapedPropertyName}.Value.{encodingCall};");
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ S = {propertyName.ToLowerInvariant()}Index }};");
            
            // Add coordinate storage if configured
            if (hasCoordinateStorage)
            {
                sb.AppendLine($"                // Store full-resolution coordinates");
                sb.AppendLine($"                item[\"{property.LatitudeAttributeName}\"] = new AttributeValue {{ N = typedEntity.{escapedPropertyName}.Value.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture) }};");
                sb.AppendLine($"                item[\"{property.LongitudeAttributeName}\"] = new AttributeValue {{ N = typedEntity.{escapedPropertyName}.Value.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture) }};");
            }
            
            sb.AppendLine("            }");
        }
        else
        {
            // Check for default value (latitude and longitude both 0)
            sb.AppendLine($"            if (typedEntity.{escapedPropertyName}.Latitude != 0 || typedEntity.{escapedPropertyName}.Longitude != 0)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var {propertyName.ToLowerInvariant()}Index = typedEntity.{escapedPropertyName}.{encodingCall};");
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ S = {propertyName.ToLowerInvariant()}Index }};");
            
            // Add coordinate storage if configured
            if (hasCoordinateStorage)
            {
                sb.AppendLine($"                // Store full-resolution coordinates");
                sb.AppendLine($"                item[\"{property.LatitudeAttributeName}\"] = new AttributeValue {{ N = typedEntity.{escapedPropertyName}.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture) }};");
                sb.AppendLine($"                item[\"{property.LongitudeAttributeName}\"] = new AttributeValue {{ N = typedEntity.{escapedPropertyName}.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture) }};");
            }
            
            sb.AppendLine("            }");
        }
    }

    /// <summary>
    /// Generates code to deserialize a GeoLocation property from a spatial index string AttributeValue.
    /// Supports GeoHash, S2, and H3 spatial indexing systems.
    /// When coordinate storage is configured, prefers exact coordinates over spatial index decoding.
    /// </summary>
    private static void GenerateGeoLocationPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var escapedPropertyName = EscapePropertyName(propertyName);
        
        // Use property name as suffix for decoded variable to avoid conflicts with multiple GeoLocation properties
        var decodedVarName = $"decoded{propertyName}";
        
        // Determine spatial index type (default to GeoHash for backward compatibility)
        var spatialIndexType = property.SpatialIndexType ?? "GeoHash";
        
        // Get decoding method and index type name based on index type
        string decodingCall;
        string indexTypeName;
        
        switch (spatialIndexType)
        {
            case "S2":
                decodingCall = "S2Extensions.FromS2Token";
                indexTypeName = "S2 token";
                break;
            
            case "H3":
                decodingCall = "H3Extensions.FromH3Index";
                indexTypeName = "H3 index";
                break;
            
            case "GeoHash":
            default:
                decodingCall = "GeoHashExtensions.FromGeoHash";
                indexTypeName = "GeoHash";
                break;
        }

        // Check if coordinate storage is configured
        var hasCoordinateStorage = property.HasCoordinateStorage;
        
        if (hasCoordinateStorage)
        {
            sb.AppendLine($"            // Deserialize GeoLocation property {propertyName} from coordinates (if available) or {indexTypeName}");
            sb.AppendLine($"            // Priority: 1) Exact coordinates, 2) Spatial index decoding");
            sb.AppendLine($"            if (item.TryGetValue(\"{property.LatitudeAttributeName}\", out var {propertyName.ToLowerInvariant()}LatValue) && ");
            sb.AppendLine($"                item.TryGetValue(\"{property.LongitudeAttributeName}\", out var {propertyName.ToLowerInvariant()}LonValue) &&");
            sb.AppendLine($"                {propertyName.ToLowerInvariant()}LatValue.N != null && {propertyName.ToLowerInvariant()}LonValue.N != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                // Reconstruct from exact coordinates");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var latitude = double.Parse({propertyName.ToLowerInvariant()}LatValue.N, System.Globalization.CultureInfo.InvariantCulture);");
            sb.AppendLine($"                    var longitude = double.Parse({propertyName.ToLowerInvariant()}LonValue.N, System.Globalization.CultureInfo.InvariantCulture);");
            sb.AppendLine($"                    // Also read the spatial index if available to preserve it");
            sb.AppendLine($"                    string? spatialIndexValue = null;");
            sb.AppendLine($"                    if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}IndexValue) && {propertyName.ToLowerInvariant()}IndexValue.S != null)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        spatialIndexValue = {propertyName.ToLowerInvariant()}IndexValue.S;");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    entity.{escapedPropertyName} = new Oproto.FluentDynamoDb.Geospatial.GeoLocation(latitude, longitude, spatialIndexValue);");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    throw new DynamoDbMappingException(");
            sb.AppendLine($"                        $\"Failed to deserialize GeoLocation property '{propertyName}' from coordinate attributes ('{property.LatitudeAttributeName}', '{property.LongitudeAttributeName}'). \" +");
            sb.AppendLine($"                        $\"Latitude: '{{{propertyName.ToLowerInvariant()}LatValue.N}}', Longitude: '{{{propertyName.ToLowerInvariant()}LonValue.N}}'. \" +");
            sb.AppendLine($"                        $\"Error: {{ex.Message}}\",");
            sb.AppendLine("                        ex);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine($"            else if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value) && {propertyName.ToLowerInvariant()}Value.S != null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                // Fallback to spatial index decoding (for backward compatibility or when coordinates are missing)");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var spatialIndexString = {propertyName.ToLowerInvariant()}Value.S;");
            sb.AppendLine($"                    var {decodedVarName} = {decodingCall}(spatialIndexString);");
            sb.AppendLine($"                    // Preserve the spatial index by passing it to the constructor");
            sb.AppendLine($"                    entity.{escapedPropertyName} = new Oproto.FluentDynamoDb.Geospatial.GeoLocation({decodedVarName}.Latitude, {decodedVarName}.Longitude, spatialIndexString);");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    throw new DynamoDbMappingException(");
            sb.AppendLine($"                        $\"Failed to deserialize GeoLocation property '{propertyName}' (DynamoDB attribute: '{attributeName}') from {indexTypeName} string '{{{propertyName.ToLowerInvariant()}Value.S}}'. \" +");
            sb.AppendLine($"                        $\"Error: {{ex.Message}}. \" +");
            sb.AppendLine($"                        $\"Ensure the stored value is a valid {indexTypeName} string.\",");
            sb.AppendLine("                        ex);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine($"            // Deserialize GeoLocation property {propertyName} from {indexTypeName}");
            sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
            sb.AppendLine("            {");
            sb.AppendLine($"                if ({propertyName.ToLowerInvariant()}Value.S != null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    try");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        var spatialIndexString = {propertyName.ToLowerInvariant()}Value.S;");
            sb.AppendLine($"                        var {decodedVarName} = {decodingCall}(spatialIndexString);");
            sb.AppendLine($"                        // Preserve the spatial index by passing it to the constructor");
            sb.AppendLine($"                        entity.{escapedPropertyName} = new Oproto.FluentDynamoDb.Geospatial.GeoLocation({decodedVarName}.Latitude, {decodedVarName}.Longitude, spatialIndexString);");
            sb.AppendLine("                    }");
            sb.AppendLine("                    catch (Exception ex)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new DynamoDbMappingException(");
            sb.AppendLine($"                            $\"Failed to deserialize GeoLocation property '{propertyName}' (DynamoDB attribute: '{attributeName}') from {indexTypeName} string '{{{propertyName.ToLowerInvariant()}Value.S}}'. \" +");
            sb.AppendLine($"                            $\"Error: {{ex.Message}}. \" +");
            sb.AppendLine($"                            $\"Ensure the stored value is a valid {indexTypeName} string.\",");
            sb.AppendLine("                            ex);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
    }

    /// <summary>
    /// Gets example format strings for a given type to include in error messages.
    /// </summary>
    private static string GetExampleFormatsForType(string baseType)
    {
        return baseType switch
        {
            "DateTime" or "System.DateTime" => "'o' (ISO 8601), 'yyyy-MM-dd' (date only), 'yyyy-MM-dd HH:mm:ss' (date and time)",
            "DateTimeOffset" or "System.DateTimeOffset" => "'o' (ISO 8601), 'yyyy-MM-dd HH:mm:ss zzz' (with timezone)",
            "decimal" or "System.Decimal" => "'F2' (2 decimal places), 'F4' (4 decimal places), 'N2' (with thousand separators)",
            "double" or "System.Double" or "float" or "System.Single" => "'F2' (2 decimal places), 'E' (scientific notation), 'G' (general)",
            "int" or "System.Int32" or "long" or "System.Int64" => "'D5' (zero-padded to 5 digits), 'N0' (with thousand separators), 'X' (hexadecimal)",
            _ => "'G' (general format)"
        };
    }

}