using Microsoft.CodeAnalysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

/// <summary>
/// Diagnostic descriptors for DynamoDB source generator errors and warnings.
/// </summary>
public static class DiagnosticDescriptors
{
    /// <summary>
    /// Error when an entity is missing a partition key.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingPartitionKey = new(
        "DYNDB001",
        "Missing partition key",
        "Entity '{0}' must have exactly one property marked with [PartitionKey]",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every DynamoDB entity must have exactly one partition key property.");

    /// <summary>
    /// Error when an entity has multiple partition keys.
    /// </summary>
    public static readonly DiagnosticDescriptor MultiplePartitionKeys = new(
        "DYNDB002",
        "Multiple partition keys",
        "Entity '{0}' has multiple properties marked with [PartitionKey]. Only one is allowed.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A DynamoDB entity can only have one partition key property.");

    /// <summary>
    /// Error when an entity has multiple sort keys.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleSortKeys = new(
        "DYNDB003",
        "Multiple sort keys",
        "Entity '{0}' has multiple properties marked with [SortKey]. Only one is allowed.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A DynamoDB entity can only have one sort key property.");

    /// <summary>
    /// Error when a property has invalid key format.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidKeyFormat = new(
        "DYNDB004",
        "Invalid key format",
        "Property '{0}' has invalid key format: {1}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Key format must be a valid pattern for DynamoDB key construction.");

    /// <summary>
    /// Error when multiple entities in the same table have conflicting sort key patterns.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingEntityTypes = new(
        "DYNDB005",
        "Conflicting entity types",
        "Multiple entities in table '{0}' have conflicting sort key patterns",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Entities sharing the same table must have distinct sort key patterns for proper discrimination.");

    /// <summary>
    /// Error when a GSI is missing required key properties.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGsiConfiguration = new(
        "DYNDB006",
        "Invalid GSI configuration",
        "Global Secondary Index '{0}' on entity '{1}' must have at least a partition key",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every Global Secondary Index must have at least a partition key property.");

    /// <summary>
    /// Error when a property is missing DynamoDbAttribute but has other DynamoDB attributes.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingDynamoDbAttribute = new(
        "DYNDB007",
        "Missing DynamoDbAttribute",
        "Property '{0}' has DynamoDB key attributes but is missing [DynamoDbAttribute]",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties with DynamoDB key attributes must also have [DynamoDbAttribute] to specify the attribute name.");

    /// <summary>
    /// Warning when a related entity pattern might be ambiguous.
    /// </summary>
    public static readonly DiagnosticDescriptor AmbiguousRelatedEntityPattern = new(
        "DYNDB008",
        "Ambiguous related entity pattern",
        "Related entity pattern '{0}' on property '{1}' might match multiple entity types",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Related entity patterns should be specific enough to avoid ambiguous matches.");

    /// <summary>
    /// Error when a property type is not supported for DynamoDB mapping.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        "DYNDB009",
        "Unsupported property type",
        "Property '{0}' has type '{1}' which is not supported for DynamoDB mapping",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only certain .NET types can be automatically mapped to DynamoDB attribute values.");

    /// <summary>
    /// Error when an entity class is not declared as partial.
    /// </summary>
    public static readonly DiagnosticDescriptor EntityMustBePartial = new(
        "DYNDB010",
        "Entity must be partial",
        "Entity class '{0}' must be declared as 'partial' to support source generation",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DynamoDB entity classes must be declared as partial to allow the source generator to add implementation code.");

    /// <summary>
    /// Error when a multi-item entity is missing a partition key.
    /// </summary>
    public static readonly DiagnosticDescriptor MultiItemEntityMissingPartitionKey = new(
        "DYNDB011",
        "Multi-item entity missing partition key",
        "Multi-item entity '{0}' must have a partition key for grouping related items",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Multi-item entities require a partition key to group related DynamoDB items together.");

    /// <summary>
    /// Warning when a multi-item entity is missing a sort key.
    /// </summary>
    public static readonly DiagnosticDescriptor MultiItemEntityMissingSortKey = new(
        "DYNDB012",
        "Multi-item entity missing sort key",
        "Multi-item entity '{0}' should have a sort key for proper item ordering",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Multi-item entities should have a sort key to ensure consistent ordering of related items.");

    /// <summary>
    /// Error when a collection property is marked as a key.
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionPropertyCannotBeKey = new(
        "DYNDB013",
        "Collection property cannot be key",
        "Collection property '{0}' in entity '{1}' cannot be marked as partition key or sort key",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Collection properties represent multiple values and cannot be used as DynamoDB keys.");

    /// <summary>
    /// Warning about partition key format for multi-item entities.
    /// </summary>
    public static readonly DiagnosticDescriptor MultiItemEntityPartitionKeyFormat = new(
        "DYNDB014",
        "Multi-item entity partition key format",
        "Partition key '{0}' in multi-item entity '{1}' should have a consistent format for proper grouping",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Multi-item entities should use consistent partition key formats to ensure related items are properly grouped.");

    /// <summary>
    /// Error when a related entity references an unknown type.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidRelatedEntityType = new(
        "DYNDB015",
        "Invalid related entity type",
        "Related entity property '{0}' references unknown type '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Related entity types must be valid DynamoDB entity classes.");

    /// <summary>
    /// Warning when related entities are defined but no sort key exists.
    /// </summary>
    public static readonly DiagnosticDescriptor RelatedEntitiesRequireSortKey = new(
        "DYNDB016",
        "Related entities require sort key",
        "Entity '{0}' has related entity properties but no sort key for pattern matching",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Related entity mapping requires a sort key to match patterns and discriminate entity types.");

    /// <summary>
    /// Warning when multiple related entities have conflicting patterns.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingRelatedEntityPatterns = new(
        "DYNDB017",
        "Conflicting related entity patterns",
        "Related entity patterns '{0}' and '{1}' in entity '{2}' may conflict",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Related entity patterns should be distinct to avoid mapping conflicts.");

    /// <summary>
    /// Error when key format contains invalid placeholders or syntax.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidKeyFormatSyntax = new(
        "DYNDB018",
        "Invalid key format syntax",
        "Key format '{0}' on property '{1}' contains invalid syntax or placeholders",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Key formats must use valid placeholder syntax like {0}, {1}, etc. and cannot contain reserved characters.");

    /// <summary>
    /// Warning when key format may produce non-unique keys.
    /// </summary>
    public static readonly DiagnosticDescriptor PotentialKeyCollision = new(
        "DYNDB019",
        "Potential key collision",
        "Key format '{0}' on property '{1}' may produce non-unique keys for different values",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Key formats should ensure uniqueness to avoid DynamoDB key collisions.");

    /// <summary>
    /// Error when entity has circular references that cannot be serialized.
    /// </summary>
    public static readonly DiagnosticDescriptor CircularReferenceDetected = new(
        "DYNDB020",
        "Circular reference detected",
        "Entity '{0}' has circular references that cannot be serialized to DynamoDB",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Entities with circular references cannot be properly serialized to DynamoDB format.");

    /// <summary>
    /// Warning when property name conflicts with DynamoDB reserved words.
    /// </summary>
    public static readonly DiagnosticDescriptor ReservedWordUsage = new(
        "DYNDB021",
        "Reserved word usage",
        "Property '{0}' uses DynamoDB reserved word '{1}' as attribute name",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Using DynamoDB reserved words as attribute names may cause query issues. Consider using a different attribute name.");

    /// <summary>
    /// Error when entity configuration would result in invalid DynamoDB operations.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidDynamoDbConfiguration = new(
        "DYNDB022",
        "Invalid DynamoDB configuration",
        "Entity '{0}' configuration is invalid: {1}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Entity configuration must comply with DynamoDB constraints and limitations.");

    /// <summary>
    /// Warning when property type may cause performance issues.
    /// </summary>
    public static readonly DiagnosticDescriptor PerformanceWarning = new(
        "DYNDB023",
        "Performance warning",
        "Property '{0}' of type '{1}' may cause performance issues: {2}",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Certain property types or configurations may impact DynamoDB performance.");

    /// <summary>
    /// Error when required attribute is missing from entity definition.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingRequiredAttribute = new(
        "DYNDB024",
        "Missing required attribute",
        "Property '{0}' in entity '{1}' is missing required attribute '{2}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties used in DynamoDB operations must have appropriate attributes defined.");

    /// <summary>
    /// Warning when attribute configuration may cause data loss.
    /// </summary>
    public static readonly DiagnosticDescriptor PotentialDataLoss = new(
        "DYNDB025",
        "Potential data loss",
        "Property '{0}' configuration may cause data loss during serialization: {1}",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Certain property configurations may result in data loss during DynamoDB serialization.");

    /// <summary>
    /// Error when GSI projection is invalid or incomplete.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGsiProjection = new(
        "DYNDB026",
        "Invalid GSI projection",
        "Global Secondary Index '{0}' has invalid projection configuration: {1}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "GSI projections must be properly configured to include all necessary attributes.");

    /// <summary>
    /// Warning when entity design may not scale well.
    /// </summary>
    public static readonly DiagnosticDescriptor ScalabilityWarning = new(
        "DYNDB027",
        "Scalability warning",
        "Entity '{0}' design may not scale well: {1}",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Entity design should follow DynamoDB best practices for scalability.");

    /// <summary>
    /// Error when property type conversion is not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedTypeConversion = new(
        "DYNDB028",
        "Unsupported type conversion",
        "Cannot convert property '{0}' of type '{1}' to DynamoDB format: {2}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Property types must be convertible to DynamoDB AttributeValue format.");

    /// <summary>
    /// Warning when entity has too many attributes for efficient operations.
    /// </summary>
    public static readonly DiagnosticDescriptor TooManyAttributes = new(
        "DYNDB029",
        "Too many attributes",
        "Entity '{0}' has {1} attributes, which may impact performance",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Entities with many attributes may impact DynamoDB performance and costs.");

    /// <summary>
    /// Error when attribute name is invalid or contains illegal characters.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidAttributeName = new(
        "DYNDB030",
        "Invalid attribute name",
        "Attribute name '{0}' on property '{1}' is invalid: {2}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DynamoDB attribute names must follow naming conventions and cannot contain certain characters.");

    /// <summary>
    /// Error when a computed property references a non-existent source property.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputedKeySource = new(
        "DYNDB031",
        "Invalid computed key source",
        "Computed property '{0}' references non-existent source property '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Computed properties must reference existing properties in the same entity.");

    /// <summary>
    /// Error when an extracted property references a non-existent source property.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidExtractedKeySource = new(
        "DYNDB032",
        "Invalid extracted key source",
        "Extracted property '{0}' references non-existent source property '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Extracted properties must reference existing properties in the same entity.");

    /// <summary>
    /// Error when circular dependencies are detected between computed properties.
    /// </summary>
    public static readonly DiagnosticDescriptor CircularKeyDependency = new(
        "DYNDB033",
        "Circular key dependency",
        "Circular dependency detected between computed properties: {0}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Computed properties cannot have circular dependencies on each other.");

    /// <summary>
    /// Error when a computed property references itself as a source.
    /// </summary>
    public static readonly DiagnosticDescriptor SelfReferencingComputedKey = new(
        "DYNDB034",
        "Self-referencing computed key",
        "Computed property '{0}' cannot reference itself as a source property",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Computed properties cannot reference themselves as source properties.");

    /// <summary>
    /// Error when an extracted property has an invalid index.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidExtractedKeyIndex = new(
        "DYNDB035",
        "Invalid extracted key index",
        "Extracted property '{0}' has invalid index {1} for source property '{2}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Extracted property index must be valid for the expected number of components in the source property.");

    /// <summary>
    /// Warning when a computed property format may produce invalid keys.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputedKeyFormat = new(
        "DYNDB036",
        "Invalid computed key format",
        "Computed property '{0}' has format '{1}' that may produce invalid keys: {2}",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Computed key formats should produce valid DynamoDB key values.");

    // Advanced Type System Diagnostics (DYNDB101-DYNDB106)

    /// <summary>
    /// Error when [TimeToLive] is used on a non-DateTime property.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidTtlType = new(
        "DYNDB101",
        "Invalid TTL property type",
        "[TimeToLive] can only be used on DateTime or DateTimeOffset properties. Property '{0}' is type '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "TTL properties must be DateTime or DateTimeOffset to support Unix epoch conversion.");

    /// <summary>
    /// Error when [JsonBlob] is used without referencing a JSON serializer package.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingJsonSerializer = new(
        "DYNDB102",
        "Missing JSON serializer package",
        "[JsonBlob] on property '{0}' requires referencing a JSON serializer package (SystemTextJson or NewtonsoftJson)",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "JSON blob serialization requires a JSON serializer package reference.");

    /// <summary>
    /// Error when [BlobReference] is used without referencing a blob provider package.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingBlobProvider = new(
        "DYNDB103",
        "Missing blob provider package",
        "[BlobReference] on property '{0}' requires referencing a blob provider package like Oproto.FluentDynamoDb.BlobStorage.S3",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Blob reference storage requires a blob provider package reference.");

    /// <summary>
    /// Error when incompatible attributes are combined on a property.
    /// </summary>
    public static readonly DiagnosticDescriptor IncompatibleAttributes = new(
        "DYNDB104",
        "Incompatible attribute combination",
        "Property '{0}' has incompatible attribute combination: {1}",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Certain attribute combinations are not supported together.");

    /// <summary>
    /// Error when multiple properties have [TimeToLive] attribute.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleTtlFields = new(
        "DYNDB105",
        "Multiple TTL fields",
        "Entity '{0}' has multiple [TimeToLive] properties. Only one TTL field is allowed per entity",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DynamoDB entities can only have one TTL field.");

    /// <summary>
    /// Error when an unsupported collection type is used.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedCollectionType = new(
        "DYNDB106",
        "Unsupported collection type",
        "Property '{0}' has unsupported collection type '{1}'. Use Dictionary<string, T>, HashSet<T>, or List<T>",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only specific collection types are supported for DynamoDB mapping.");

    /// <summary>
    /// Error when [DynamoDbMap] is used on a custom type that isn't marked with [DynamoDbEntity].
    /// </summary>
    public static readonly DiagnosticDescriptor NestedMapTypeMissingEntity = new(
        "DYNDB107",
        "Nested map type missing [DynamoDbEntity]",
        "Property '{0}' with [DynamoDbMap] has type '{1}' which must be marked with [DynamoDbEntity] to generate mapping code. Nested map types require source-generated ToDynamoDb/FromDynamoDb methods to maintain AOT compatibility.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Custom types used with [DynamoDbMap] must be marked with [DynamoDbEntity] to generate the required mapping methods. This ensures AOT compatibility by avoiding reflection.");
}