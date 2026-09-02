using Microsoft.CodeAnalysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

/// <summary>
/// Diagnostic descriptors for DynamoDB source generator errors and warnings.
/// </summary>
internal static class DiagnosticDescriptors
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
        description: "Every DynamoDB entity must have exactly one partition key property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB001"));

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
        description: "A DynamoDB entity can only have one partition key property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB002"));

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
        description: "A DynamoDB entity can only have one sort key property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB003"));

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
        description: "Key format must be a valid pattern for DynamoDB key construction.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB004"));

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
        description: "Entities sharing the same table must have distinct sort key patterns for proper discrimination.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB005"));

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
        description: "Every Global Secondary Index must have at least a partition key property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB006"));

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
        description: "Properties with DynamoDB key attributes must also have [DynamoDbAttribute] to specify the attribute name.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB007"));

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
        description: "Related entity patterns should be specific enough to avoid ambiguous matches.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB008"));

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
        description: "Only certain .NET types can be automatically mapped to DynamoDB attribute values.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB009"));

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
        description: "DynamoDB entity classes must be declared as partial to allow the source generator to add implementation code.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB010"));

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
        description: "Multi-item entities require a partition key to group related DynamoDB items together.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB011"));

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
        description: "Multi-item entities should have a sort key to ensure consistent ordering of related items.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB012"));

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
        description: "Collection properties represent multiple values and cannot be used as DynamoDB keys.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB013"));

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
        description: "Multi-item entities should use consistent partition key formats to ensure related items are properly grouped.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB014"));

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
        description: "Related entity types must be valid DynamoDB entity classes.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB015"));

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
        description: "Related entity mapping requires a sort key to match patterns and discriminate entity types.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB016"));

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
        description: "Related entity patterns should be distinct to avoid mapping conflicts.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB017"));

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
        description: "Key formats must use valid placeholder syntax like {0}, {1}, etc. and cannot contain reserved characters.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB018"));

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
        description: "Key formats should ensure uniqueness to avoid DynamoDB key collisions.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB019"));

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
        description: "Entities with circular references cannot be properly serialized to DynamoDB format.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB020"));

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
        description: "Using DynamoDB reserved words as attribute names may cause query issues. Consider using a different attribute name.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB021"));

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
        description: "Entity configuration must comply with DynamoDB constraints and limitations.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB022"));

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
        description: "Certain property types or configurations may impact DynamoDB performance.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB023"));

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
        description: "Properties used in DynamoDB operations must have appropriate attributes defined.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB024"));

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
        description: "Certain property configurations may result in data loss during DynamoDB serialization.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB025"));

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
        description: "GSI projections must be properly configured to include all necessary attributes.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB026"));

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
        description: "Entity design should follow DynamoDB best practices for scalability.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB027"));

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
        description: "Property types must be convertible to DynamoDB AttributeValue format.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB028"));

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
        description: "Entities with many attributes may impact DynamoDB performance and costs.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB029"));

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
        description: "DynamoDB attribute names must follow naming conventions and cannot contain certain characters.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB030"));

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
        description: "Computed properties must reference existing properties in the same entity.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB031"));

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
        description: "Extracted properties must reference existing properties in the same entity.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB032"));

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
        description: "Computed properties cannot have circular dependencies on each other.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB033"));

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
        description: "Computed properties cannot reference themselves as source properties.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB034"));

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
        description: "Extracted property index must be valid for the expected number of components in the source property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB035"));

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
        description: "Computed key formats should produce valid DynamoDB key values.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB036"));

    // Advanced Type System Diagnostics (DYNDB101-DYNDB106)

    /// <summary>
    /// Error when [TimeToLive] is used on a non-DateTime property.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidTtlType = new(
        "DYNDB101",
        "Invalid TTL property type",
        "[TimeToLive] can only be used on DateTime or DateTimeOffset properties, but property '{0}' is type '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "TTL properties must be DateTime or DateTimeOffset to support Unix epoch conversion.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB101"));

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
        description: "JSON blob serialization requires a JSON serializer package reference.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB102"));

    /// <summary>
    /// Error when [BlobStorage] is used without referencing a blob provider package.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingBlobStorageProvider = new(
        "DYNDB103",
        "Missing blob provider package",
        "[BlobStorage] on property '{0}' requires referencing a blob provider package like Oproto.FluentDynamoDb.BlobStorage.S3",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Blob storage requires a blob provider package reference.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB103"));

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
        description: "Certain attribute combinations are not supported together.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB104"));

    /// <summary>
    /// Error when multiple properties have [TimeToLive] attribute.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleTtlFields = new(
        "DYNDB105",
        "Multiple TTL fields",
        "Entity '{0}' has multiple [TimeToLive] properties, but only one TTL field is allowed per entity",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DynamoDB entities can only have one TTL field.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB105"));

    /// <summary>
    /// Error when an unsupported collection type is used.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedCollectionType = new(
        "DYNDB106",
        "Unsupported collection type",
        "Property '{0}' has unsupported collection type '{1}'; use Dictionary<string, T>, HashSet<T>, or List<T> instead",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only specific collection types are supported for DynamoDB mapping.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB106"));

    /// <summary>
    /// Error when [DynamoDbMap] is used on a custom type that isn't marked with [DynamoDbEntity].
    /// </summary>
    public static readonly DiagnosticDescriptor NestedMapTypeMissingEntity = new(
        "DYNDB107",
        "Nested map type missing [DynamoDbEntity]",
        "Property '{0}' with [DynamoDbMap] has type '{1}' which must be marked with [DynamoDbEntity] to generate mapping code",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Custom types used with [DynamoDbMap] must be marked with [DynamoDbEntity] to generate the required mapping methods. This ensures AOT compatibility by avoiding reflection.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB107"));

    // Projection Model Diagnostics (PROJ001-PROJ006, PROJ101-PROJ102)

    /// <summary>
    /// Error when a projection property does not exist on the source entity.
    /// </summary>
    public static readonly DiagnosticDescriptor ProjectionPropertyNotFound = new(
        "PROJ001",
        "Projection property not found",
        "Property '{0}' on projection '{1}' does not exist on source entity '{2}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All properties in a projection model must exist on the source entity.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "PROJ001"));

    /// <summary>
    /// Error when a projection property type does not match the source entity property type.
    /// </summary>
    public static readonly DiagnosticDescriptor ProjectionPropertyTypeMismatch = new(
        "PROJ002",
        "Projection property type mismatch",
        "Property '{0}' type '{1}' on projection '{2}' does not match source entity type '{3}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Projection property types must match the corresponding source entity property types.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "PROJ002"));

    /// <summary>
    /// Error when the source entity type for a projection does not exist or is not a DynamoDB entity.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidProjectionSourceEntity = new(
        "PROJ003",
        "Invalid projection source entity",
        "Source entity type '{0}' for projection '{1}' does not exist or is not a DynamoDB entity",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Projection source entity must be a valid DynamoDB entity class.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "PROJ003"));

    /// <summary>
    /// Error when a projection class is not declared as partial.
    /// </summary>
    public static readonly DiagnosticDescriptor ProjectionMustBePartial = new(
        "PROJ004",
        "Projection must be partial",
        "Projection class '{0}' must be declared as 'partial' to support source generation",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Projection classes must be declared as partial to allow the source generator to add mapping code.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "PROJ004"));

    /// <summary>
    /// Error when [UseProjection] references a non-existent projection type.
    /// </summary>
    public static readonly DiagnosticDescriptor UseProjectionInvalidType = new(
        "PROJ005",
        "UseProjection references invalid type",
        "UseProjection attribute on GSI '{0}' references non-existent or invalid projection type '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "UseProjection attribute must reference a valid projection model type.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "PROJ005"));

    /// <summary>
    /// Error when multiple conflicting [UseProjection] attributes are found for the same GSI.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingUseProjection = new(
        "PROJ006",
        "Conflicting UseProjection attributes",
        "GSI '{0}' has multiple conflicting UseProjection attributes specifying different projection types",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A GSI can only have one projection type constraint across all entities.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "PROJ006"));

    /// <summary>
    /// Warning when a projection includes all properties from the source entity.
    /// </summary>
    public static readonly DiagnosticDescriptor ProjectionIncludesAllProperties = new(
        "PROJ101",
        "Projection includes all properties",
        "Projection '{0}' includes all properties from source entity '{1}'. Consider using the full entity type instead for better performance.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Projections that include all properties provide no optimization benefit over using the full entity type.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "PROJ101"));

    /// <summary>
    /// Warning when a projection has many properties which may impact performance.
    /// </summary>
    public static readonly DiagnosticDescriptor ProjectionHasManyProperties = new(
        "PROJ102",
        "Projection has many properties",
        "Projection '{0}' has {1} properties which may impact performance. Consider reducing the number of projected properties.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Projections with many properties may not provide significant performance benefits.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "PROJ102"));

    // Discriminator Configuration Diagnostics (DISC001-DISC006)

    /// <summary>
    /// Warning when both DiscriminatorValue and DiscriminatorPattern are specified.
    /// </summary>
    public static readonly DiagnosticDescriptor BothDiscriminatorValueAndPattern = new(
        "DISC001",
        "Both DiscriminatorValue and DiscriminatorPattern specified",
        "Entity '{0}' has both DiscriminatorValue and DiscriminatorPattern specified. Only one should be used. DiscriminatorValue will take precedence.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DiscriminatorValue and DiscriminatorPattern are mutually exclusive. Specify only one to avoid confusion.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DISC001"));

    /// <summary>
    /// Error when DiscriminatorValue or DiscriminatorPattern is specified without DiscriminatorProperty.
    /// </summary>
    public static readonly DiagnosticDescriptor DiscriminatorValueWithoutProperty = new(
        "DISC002",
        "DiscriminatorValue or DiscriminatorPattern without DiscriminatorProperty",
        "Entity '{0}' has DiscriminatorValue or DiscriminatorPattern specified but DiscriminatorProperty is missing. Specify DiscriminatorProperty to indicate which attribute contains the discriminator.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DiscriminatorProperty must be specified when using DiscriminatorValue or DiscriminatorPattern to indicate which DynamoDB attribute contains the discriminator.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DISC002"));

    /// <summary>
    /// Error when discriminator pattern has invalid syntax.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidDiscriminatorPattern = new(
        "DISC003",
        "Invalid discriminator pattern syntax",
        "Entity '{0}' has invalid discriminator pattern '{1}': {2}. Patterns should use '*' as a wildcard (e.g., 'USER#*', '*#USER', '*USER*').",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Discriminator patterns must use valid syntax with '*' as wildcard. Complex patterns with multiple wildcards in non-standard positions may not be supported.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DISC003"));

    /// <summary>
    /// Error when two overlapping discriminator patterns have the same specificity score (ambiguous).
    /// </summary>
    public static readonly DiagnosticDescriptor AmbiguousOverlappingDiscriminatorPatterns = new(
        "DISC004",
        "Ambiguous overlapping discriminator patterns",
        "Ambiguous overlapping discriminator patterns: '{0}' on {1} and '{2}' on {3} have the same specificity score on property '{4}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Two overlapping discriminator patterns with the same specificity score cannot be automatically resolved. Change one pattern to be more or less specific to resolve the ambiguity.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DISC004"));

    /// <summary>
    /// Informational diagnostic when overlapping discriminator patterns are resolved by specificity ordering.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlappingDiscriminatorPatternResolved = new(
        "DISC005",
        "Overlapping discriminator pattern resolved",
        "Overlapping discriminator pattern resolved: {0} excludes pattern '{1}' from more-specific entity {2}",
        "DynamoDb",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Overlapping discriminator patterns were resolved by specificity ordering. The less-specific entity's MatchesEntity method will exclude items matching the more-specific pattern.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DISC005"));

    /// <summary>
    /// Error when a computed exclusion guard is tautological — identical to the entity's own positive match.
    /// </summary>
    public static readonly DiagnosticDescriptor TautologicalExclusionGuard = new(
        "DISC006",
        "Tautological exclusion guard detected",
        "Entity '{0}' (pattern '{1}') cannot exclude pattern '{2}' from entity '{3}' because the exclusion check ({4}(\"{5}\")) is identical to the entity's own positive match. This would make MatchesEntity always return false.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A computed exclusion guard is tautological — it uses the same strategy and literal as the entity's own positive match criterion. This indicates the pattern hierarchy cannot be automatically resolved. Consider redesigning the discriminator patterns to use distinct literals.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DISC006"));

    // Security Diagnostics (SEC001-SEC002)

    /// <summary>
    /// Warning when [Encrypted] is used without referencing the Encryption.Kms package.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingEncryptionKms = new(
        "SEC001",
        "Missing Encryption.Kms package",
        "Property '{0}' on entity '{1}' is marked with [Encrypted] but the Oproto.FluentDynamoDb.Encryption.Kms package is not referenced. Add the package reference to enable field-level encryption.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [Encrypted] attribute requires the Oproto.FluentDynamoDb.Encryption.Kms package to provide encryption functionality.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "SEC001"));

    /// <summary>
    /// Error when [GenerateStreamConversion] is used without referencing the Amazon.Lambda.DynamoDBEvents package.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingLambdaEventsPackage = new(
        "SEC002",
        "Missing Amazon.Lambda.DynamoDBEvents package",
        "Entity '{0}' is marked with [GenerateStreamConversion] but the Amazon.Lambda.DynamoDBEvents package is not referenced. Add a package reference to Amazon.Lambda.DynamoDBEvents version 3.1.1 or higher to enable stream conversion code generation.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [GenerateStreamConversion] attribute requires the Amazon.Lambda.DynamoDBEvents package to provide Lambda AttributeValue types for stream processing.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "SEC002"));

    // Table Generation Redesign Diagnostics (FDDB001-FDDB004)

    /// <summary>
    /// Error when multiple entities share a table but no default entity is specified.
    /// </summary>
    public static readonly DiagnosticDescriptor NoDefaultEntitySpecified = new(
        "FDDB001",
        "No default entity specified",
        "Table '{0}' has multiple entities but no default specified; mark one entity with IsDefault = true in [DynamoDbTable] attribute",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When multiple entities share the same table name, one entity must be marked as the default using IsDefault = true in the [DynamoDbTable] attribute. The default entity is used for table-level operations.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB001"));

    /// <summary>
    /// Error when multiple entities in the same table are marked as default.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleDefaultEntities = new(
        "FDDB002",
        "Multiple default entities",
        "Table '{0}' has multiple entities marked as default, but only one entity can be marked with IsDefault = true",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only one entity per table can be marked as the default entity. Remove IsDefault = true from all but one entity in the table.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB002"));

    /// <summary>
    /// Error when multiple [GenerateAccessors] attributes target the same operation.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingAccessorConfiguration = new(
        "FDDB003",
        "Conflicting accessor configuration",
        "Entity '{0}' has multiple [GenerateAccessors] attributes targeting the same operation '{1}', but each operation can only be configured once",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Multiple [GenerateAccessors] attributes cannot target the same DynamoDB operation. Combine the configuration into a single attribute or use different operations.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB003"));

    /// <summary>
    /// Error when [GenerateEntityProperty] has an empty name.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptyEntityPropertyName = new(
        "FDDB004",
        "Empty entity property name",
        "Entity '{0}' has [GenerateEntityProperty] with empty Name; provide a valid name or omit the Name property to use default naming",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Name property in [GenerateEntityProperty] cannot be empty. Either provide a valid custom name or omit the Name property to use the default pluralized entity name.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB004"));

    /// <summary>
    /// Warning when entities in the same table with stream conversion enabled use different discriminator properties.
    /// </summary>
    public static readonly DiagnosticDescriptor InconsistentDiscriminatorProperties = new(
        "FDDB005",
        "Inconsistent discriminator properties",
        "Table '{0}' has entities with stream conversion enabled that use different discriminator properties ({1}), but all entities should use the same discriminator property for consistent stream processing",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When multiple entities in the same table have stream conversion enabled, they should all use the same discriminator property to ensure consistent stream processing behavior. The OnStream method will use the discriminator property from the first entity.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB005"));

    /// <summary>
    /// Error when multiple entities in the same table specify different custom namespaces.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingTableNamespaces = new(
        "FDDB006",
        "Conflicting table namespaces",
        "Table '{0}' has entities with different custom namespaces specified ({1}); all entities sharing a table must use the same namespace or leave it unspecified",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When multiple entities share the same table, they must all specify the same custom namespace or leave the Namespace property unspecified. The generated table class can only be in one namespace.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB006"));

    // Extension Method Wrapper Generation Diagnostics (DYNDB1001-DYNDB1004)

    /// <summary>
    /// Error when [GenerateWrapper] is used on a non-extension method.
    /// </summary>
    public static readonly DiagnosticDescriptor NonExtensionMethodWithGenerateWrapper = new(
        "DYNDB1001",
        "Invalid GenerateWrapper usage",
        "Method '{0}' is marked with [GenerateWrapper] but is not an extension method",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [GenerateWrapper] attribute can only be applied to extension methods. Extension methods must be static and have 'this' as the first parameter modifier.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB1001"));

    /// <summary>
    /// Error when an extension method marked with [GenerateWrapper] does not extend a valid interface.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidExtensionMethodInterface = new(
        "DYNDB1002",
        "Invalid extension method",
        "Extension method '{0}' marked with [GenerateWrapper] does not extend a valid interface",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Extension methods marked with [GenerateWrapper] must extend an interface that is implemented by the builder class. The first parameter must be an interface type.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB1002"));

    /// <summary>
    /// Warning when a required interface for extension methods cannot be found.
    /// </summary>
    public static readonly DiagnosticDescriptor InterfaceNotFound = new(
        "DYNDB1003",
        "Interface not found",
        "Interface '{0}' required by extension methods could not be found for builder '{1}'",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The interface extended by marked extension methods could not be found in the compilation. This may indicate a missing reference or incorrect interface name.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB1003"));

    /// <summary>
    /// Warning when a builder does not implement a required interface for extension methods.
    /// </summary>
    public static readonly DiagnosticDescriptor InterfaceNotImplemented = new(
        "DYNDB1004",
        "Interface not implemented",
        "Builder '{0}' does not implement interface '{1}' required by extension methods marked with [GenerateWrapper]",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The builder class must implement all interfaces that are extended by methods marked with [GenerateWrapper]. Add the interface implementation to the builder class or remove the [GenerateWrapper] attribute from methods extending this interface.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB1004"));

    // Spatial Index Diagnostics (DYNDB108-DYNDB111)

    /// <summary>
    /// Error when S2Level is specified but SpatialIndexType is not S2.
    /// </summary>
    public static readonly DiagnosticDescriptor S2LevelWithoutS2IndexType = new(
        "DYNDB108",
        "S2Level specified without S2 index type",
        "Property '{0}' has S2Level specified but SpatialIndexType is not S2; set SpatialIndexType = SpatialIndexType.S2 to use S2 indexing",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "S2Level can only be used when SpatialIndexType is set to S2. Either set SpatialIndexType = SpatialIndexType.S2 or remove the S2Level property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB108"));

    /// <summary>
    /// Error when H3Resolution is specified but SpatialIndexType is not H3.
    /// </summary>
    public static readonly DiagnosticDescriptor H3ResolutionWithoutH3IndexType = new(
        "DYNDB109",
        "H3Resolution specified without H3 index type",
        "Property '{0}' has H3Resolution specified but SpatialIndexType is not H3; set SpatialIndexType = SpatialIndexType.H3 to use H3 indexing",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "H3Resolution can only be used when SpatialIndexType is set to H3. Either set SpatialIndexType = SpatialIndexType.H3 or remove the H3Resolution property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB109"));

    /// <summary>
    /// Error when GeoHashPrecision is specified but SpatialIndexType is not GeoHash.
    /// </summary>
    public static readonly DiagnosticDescriptor GeoHashPrecisionWithoutGeoHashIndexType = new(
        "DYNDB110",
        "GeoHashPrecision specified without GeoHash index type",
        "Property '{0}' has GeoHashPrecision specified but SpatialIndexType is not GeoHash; set SpatialIndexType = SpatialIndexType.GeoHash or remove the GeoHashPrecision property",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "GeoHashPrecision can only be used when SpatialIndexType is set to GeoHash (or not specified, as GeoHash is the default). Either set SpatialIndexType = SpatialIndexType.GeoHash or remove the GeoHashPrecision property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB110"));

    /// <summary>
    /// Error when spatial index configuration is used on a non-GeoLocation property.
    /// </summary>
    public static readonly DiagnosticDescriptor SpatialIndexOnNonGeoLocation = new(
        "DYNDB111",
        "Spatial index configuration on non-GeoLocation property",
        "Property '{0}' has spatial index configuration but is not of type GeoLocation, and spatial index properties can only be used on GeoLocation properties",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Spatial index configuration properties can only be used on properties of type GeoLocation from the Oproto.FluentDynamoDb.Geospatial package.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB111"));

    /// <summary>
    /// Warning when geospatial package is not referenced but spatial index configuration is present.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingGeospatialPackage = new(
        "DYNDB112",
        "Missing Geospatial package",
        "Property '{0}' has spatial index configuration but the Oproto.FluentDynamoDb.Geospatial package is not referenced; add the package reference to enable spatial indexing",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Spatial index configuration requires the Oproto.FluentDynamoDb.Geospatial package to provide GeoLocation type and spatial encoding functionality.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB112"));

    // Deprecation Diagnostics (DYNDB113+)

    /// <summary>
    /// Warning when the deprecated [Queryable] attribute is used.
    /// </summary>
    public static readonly DiagnosticDescriptor DeprecatedQueryableAttribute = new(
        "DYNDB113",
        "Deprecated [Queryable] attribute",
        "Property '{0}' uses the deprecated [Queryable] attribute. Query capabilities are now derived from [PartitionKey] and [SortKey] attributes. This attribute will be removed in a future version.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [Queryable] attribute is deprecated. Partition keys automatically support equality operations, and sort keys automatically support range operations (equals, begins_with, between, greater_than, less_than). Remove the [Queryable] attribute from your properties.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB113"));

    // Blob Storage Redesign Diagnostics (DYNDB114+)

    /// <summary>
    /// Error when [BlobStorage] is used on a property that is not of type BlobData&lt;T&gt;.
    /// </summary>
    public static readonly DiagnosticDescriptor BlobStorageRequiresBlobDataType = new(
        "DYNDB115",
        "BlobStorage requires BlobData<T> type",
        "Property '{0}' is marked with [BlobStorage] but is not of type BlobData<T>. Change the property type to BlobData<{1}> to use blob storage.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties marked with [BlobStorage] must be of type BlobData<T> where T is the data type to be stored. The BlobData<T> wrapper provides lazy/eager loading control and reference key access.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB115"));

    // Dynamic Fields Diagnostics (FDDB0020-FDDB0021)

    /// <summary>
    /// Error when [EnableDynamicFields] is used on a non-partial class.
    /// </summary>
    public static readonly DiagnosticDescriptor EnableDynamicFieldsRequiresPartial = new(
        "FDDB0020",
        "EnableDynamicFields requires partial class",
        "Class '{0}' is marked with [EnableDynamicFields] but is not declared as 'partial'. The source generator needs to add a DynamicFields property to the class.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Classes marked with [EnableDynamicFields] must be declared as partial to allow the source generator to add the DynamicFields property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB0020"));

    /// <summary>
    /// Warning when [EnableDynamicFields] is used on a class that already has a DynamicFields property.
    /// </summary>
    public static readonly DiagnosticDescriptor DynamicFieldsPropertyAlreadyExists = new(
        "FDDB0021",
        "DynamicFields property already exists",
        "Class '{0}' is marked with [EnableDynamicFields] but already has a DynamicFields property. The generated property will conflict with the existing one.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When using [EnableDynamicFields], the source generator adds a DynamicFields property. If the class already has this property, there will be a conflict.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB0021"));

    // Index Name Conflict Diagnostics (FDDB050-FDDB052)

    /// <summary>
    /// Error when multiple entities define conflicting Name values for the same DynamoDB index.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingIndexNames = new(
        "FDDB050",
        "Conflicting index Name values",
        "Index '{0}' has conflicting Name values: '{1}' and '{2}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When multiple entities define the same DynamoDB index, they must use the same Name property value or only one entity should specify it. All entities must agree on the C# property name for the generated index accessor.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB050"));

    /// <summary>
    /// Warning when multiple entities specify the same Name for an index (informational).
    /// </summary>
    public static readonly DiagnosticDescriptor RedundantIndexNameSpecification = new(
        "FDDB052",
        "Redundant index Name specification",
        "Index '{0}' has Name '{1}' specified on multiple entities",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When multiple entities define the same DynamoDB index, consider specifying the Name property on only one entity to avoid redundancy. The Name will be used for all entities sharing the index.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB052"));

    /// <summary>
    /// Error when type-based table reference uses a non-partial class.
    /// </summary>
    public static readonly DiagnosticDescriptor NonPartialTableType = new(
        "FDDB051",
        "Non-partial table type",
        "Type '{0}' must be declared as partial when used in [DynamoDbTable(typeof({0}))]",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When using type-based table references with [DynamoDbTable(typeof(T))], the referenced type must be declared as a partial class to allow the source generator to add implementation code.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB051"));

    // Projection Interface Enhancement Diagnostics (FDDB060-FDDB062)

    /// <summary>
    /// Error when a projection references a source entity that doesn't exist or isn't properly configured.
    /// </summary>
    public static readonly DiagnosticDescriptor ProjectionSourceEntityNotFound = new(
        "FDDB060",
        "Projection source entity not found",
        "Projection '{0}' references source entity '{1}' which could not be found or is not a valid DynamoDB entity. Ensure the source entity exists and is marked with [DynamoDbTable] attribute.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Projections must reference a valid DynamoDB entity as their source. The source entity must exist in the compilation and be properly configured with [DynamoDbTable] attribute.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB060"));

    /// <summary>
    /// Error when a projection cannot inherit metadata from its source entity.
    /// </summary>
    public static readonly DiagnosticDescriptor ProjectionMetadataInheritanceFailure = new(
        "FDDB061",
        "Projection metadata inheritance failure",
        "Projection '{0}' cannot inherit metadata from source entity '{1}'. Ensure the source entity has proper DynamoDB attributes and metadata including partition key configuration.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Projections inherit metadata (table name, partition key, sort key) from their source entity. The source entity must have valid metadata configuration for inheritance to succeed.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB061"));

    /// <summary>
    /// Error when a projection is used in an incompatible context (e.g., write operations).
    /// </summary>
    public static readonly DiagnosticDescriptor ProjectionInterfaceViolation = new(
        "FDDB062",
        "Projection interface violation",
        "Projection '{0}' cannot be used in this context. Projections are read-only and implement IReadOnlyEntity<T>, not IDynamoDbEntity<T>. For write operations (Put, Update, Delete), use the source entity '{1}' instead.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Projections implement IReadOnlyEntity<T> which only supports read operations (Query, Get). For write operations, use the full source entity type that implements IDynamoDbEntity<T>.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB062"));

    // Index Configuration Conflict Diagnostics (FDDB053-FDDB055)

    /// <summary>
    /// Error when multiple entities define indexes with the same DynamoDB index name but different partition key attributes.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingIndexPartitionKey = new(
        "FDDB053",
        "Conflicting index partition key attribute",
        "Index '{0}' has conflicting partition key attributes: '{1}' on entity '{2}' vs '{3}' on entity '{4}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When multiple entities define the same DynamoDB index, they must use the same DynamoDB attribute for the partition key. Different C# property names are allowed as long as they map to the same DynamoDB attribute name.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB053"));

    /// <summary>
    /// Error when multiple entities define indexes with the same DynamoDB index name but different sort key attributes.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingIndexSortKey = new(
        "FDDB054",
        "Conflicting index sort key attribute",
        "Index '{0}' has conflicting sort key attributes: '{1}' on entity '{2}' vs '{3}' on entity '{4}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When multiple entities define the same DynamoDB index, they must use the same DynamoDB attribute for the sort key (or both have no sort key). Different C# property names are allowed as long as they map to the same DynamoDB attribute name.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB054"));

    /// <summary>
    /// Error when multiple entities define indexes with the same DynamoDB index name but different index types (GSI vs LSI).
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingIndexType = new(
        "FDDB055",
        "Conflicting index type",
        "Index '{0}' has conflicting types: {1} on entity '{2}' vs {3} on entity '{4}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When multiple entities define the same DynamoDB index, they must use the same index type (either all GSI or all LSI). An index cannot be both a Global Secondary Index and a Local Secondary Index.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB055"));

    // Index Projection Diagnostics (FDDB070-FDDB072)

    /// <summary>
    /// Warning when ProjectionType = Include is specified but no ProjectedProperties are defined.
    /// </summary>
    public static readonly DiagnosticDescriptor IncludeProjectionWithoutProperties = new(
        "FDDB070",
        "Include projection without properties",
        "Index '{0}' on entity '{1}' has ProjectionType = Include but no ProjectedProperties are defined. The index will project only the key attributes.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When using ProjectionType = Include, you should specify which non-key attributes to project. Without ProjectedProperties, only the key attributes will be included in the index projection.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB070"));

    /// <summary>
    /// Warning when ProjectionType = KeysOnly is combined with [UseProjection] attribute.
    /// </summary>
    public static readonly DiagnosticDescriptor KeysOnlyWithUseProjection = new(
        "FDDB072",
        "KeysOnly with UseProjection",
        "Index '{0}' on entity '{1}' has both ProjectionType = KeysOnly and [UseProjection] attribute. The [UseProjection] attribute takes precedence and the auto-generated Keys Only projection will not be used.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When both ProjectionType = KeysOnly and [UseProjection] are specified, the [UseProjection] attribute takes precedence. The auto-generated Keys Only projection record will not be generated. Consider removing one of these configurations to avoid confusion.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB072"));

    // Computed Key Accessor Overload Diagnostics (FDDB080-FDDB081)

    /// <summary>
    /// Error when a source property in a computed key cannot be resolved to an entity property.
    /// </summary>
    public static readonly DiagnosticDescriptor UnresolvableComputedKeySourceProperty = new(
        "FDDB080",
        "Unresolvable source property in computed key",
        "Cannot resolve source property '{0}' for computed key on '{1}.{2}'. Convenience overload will not be generated.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A source property referenced in a computed key's SourceProperties array could not be found in the entity's property collection. The typed parameter convenience overload will not be generated for this entity.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB080"));

    // Computed Key Format Validation Diagnostics (FDDB090)

    /// <summary>
    /// Error when a computed property's explicit format string has a placeholder count that doesn't match the source property count.
    /// </summary>
    public static readonly DiagnosticDescriptor ComputedFormatPlaceholderMismatch = new(
        "FDDB090",
        "Format placeholder count mismatch",
        "Computed property '{0}' has format '{1}' with {2} placeholders but {3} source properties",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The format string must contain exactly one placeholder ({0}, {1}, etc.) for each source property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB090"));

    // Index Attribute Redesign Diagnostics (DYNDB120-DYNDB127)

    /// <summary>
    /// Error when a GSI has a [GsiSortKey] but no [GsiPartitionKey] for the same index name.
    /// </summary>
    public static readonly DiagnosticDescriptor GsiSortKeyWithoutPartitionKey = new(
        "DYNDB120",
        "GSI sort key without partition key",
        "GSI '{0}' on entity '{1}' has a sort key but no partition key. Add [GsiPartitionKey(\"{0}\")] to a property.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every Global Secondary Index that has a sort key must also have a partition key. Add a [GsiPartitionKey] attribute with the same index name to a property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB120"));

    /// <summary>
    /// Error when a GSI has multiple [GsiPartitionKey] attributes for the same index name.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateGsiPartitionKey = new(
        "DYNDB121",
        "Duplicate GSI partition keys",
        "GSI '{0}' on entity '{1}' has multiple partition keys: properties '{2}' and '{3}'. Only one is allowed.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A Global Secondary Index can only have one partition key property. Remove the duplicate [GsiPartitionKey] attribute from one of the properties.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB121"));

    /// <summary>
    /// Error when a GSI has multiple [GsiSortKey] attributes for the same index name.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateGsiSortKey = new(
        "DYNDB122",
        "Duplicate GSI sort keys",
        "GSI '{0}' on entity '{1}' has multiple sort keys: properties '{2}' and '{3}'. Only one is allowed.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A Global Secondary Index can only have one sort key property. Remove the duplicate [GsiSortKey] attribute from one of the properties.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB122"));

    /// <summary>
    /// Error when an LSI has multiple [LsiSortKey] attributes for the same index name.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateLsiSortKey = new(
        "DYNDB123",
        "Duplicate LSI sort keys",
        "LSI '{0}' on entity '{1}' has multiple sort keys: properties '{2}' and '{3}'. Only one is allowed.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A Local Secondary Index can only have one sort key property. Remove the duplicate [LsiSortKey] attribute from one of the properties.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB123"));

    /// <summary>
    /// Error when [GsiPartitionKey] has an empty or whitespace index name.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptyGsiPartitionKeyIndexName = new(
        "DYNDB124",
        "Empty GsiPartitionKey index name",
        "[GsiPartitionKey] on property '{0}' has an empty or whitespace index name",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The index name parameter on [GsiPartitionKey] must be a non-empty, non-whitespace string.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB124"));

    /// <summary>
    /// Error when [GsiSortKey] has an empty or whitespace index name.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptyGsiSortKeyIndexName = new(
        "DYNDB125",
        "Empty GsiSortKey index name",
        "[GsiSortKey] on property '{0}' has an empty or whitespace index name",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The index name parameter on [GsiSortKey] must be a non-empty, non-whitespace string.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB125"));

    /// <summary>
    /// Error when [LsiSortKey] has an empty or whitespace index name.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptyLsiSortKeyIndexName = new(
        "DYNDB126",
        "Empty LsiSortKey index name",
        "[LsiSortKey] on property '{0}' has an empty or whitespace index name",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The index name parameter on [LsiSortKey] must be a non-empty, non-whitespace string.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB126"));

    /// <summary>
    /// Error when the same index name is used as both a GSI and an LSI within the same entity.
    /// </summary>
    public static readonly DiagnosticDescriptor GsiLsiIndexNameConflict = new(
        "DYNDB127",
        "GSI/LSI index name conflict",
        "Index name '{0}' on entity '{1}' is used as both a GSI and an LSI. An index name must be exclusively GSI or LSI.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A DynamoDB index name cannot be used as both a Global Secondary Index and a Local Secondary Index within the same entity. Use distinct index names for GSI and LSI.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB127"));

    // Unified Prefix/Computed/Discriminator Diagnostics (FDDB100-FDDB103)

    /// <summary>
    /// Error when a key property's Prefix conflicts with its explicit ComputedAttribute.Format.
    /// The format string does not start with the expected prefix+separator.
    /// </summary>
    public static readonly DiagnosticDescriptor PrefixFormatConflict = new(
        "FDDB100",
        "Key prefix conflicts with explicit computed format",
        "Property '{0}' has Prefix='{1}' (expecting format to start with '{2}') but ComputedAttribute.Format='{3}' does not match",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB100"));

    /// <summary>
    /// Error when an explicit DiscriminatorPattern on DynamoDbTableAttribute conflicts with
    /// the pattern derived from the key format on the same attribute.
    /// </summary>
    public static readonly DiagnosticDescriptor DiscriminatorKeyFormatConflict = new(
        "FDDB101",
        "Explicit discriminator pattern conflicts with key format",
        "Entity '{0}' specifies DiscriminatorPattern on attribute '{1}' as '{2}' but the key format derives pattern '{3}'",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB101"));

    /// <summary>
    /// Warning when two entities have overlapping auto-derived discriminator patterns
    /// with different specificity. Advisory only — exclusion guards are still generated.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlappingAutoDerivedPatterns = new(
        "FDDB102",
        "Overlapping auto-derived discriminator patterns",
        "Entities '{0}' and '{1}' have overlapping auto-derived patterns '{2}' and '{3}' on attribute '{4}' \u2014 consider adding more specificity to key formats",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB102"));

    /// <summary>
    /// Informational diagnostic when an explicit DiscriminatorPattern is redundant because
    /// it exactly matches the auto-derived pattern from the key format.
    /// </summary>
    public static readonly DiagnosticDescriptor RedundantExplicitDiscriminator = new(
        "FDDB103",
        "Redundant explicit discriminator pattern",
        "Entity '{0}' specifies DiscriminatorPattern='{1}' which is automatically derivable from the key format \u2014 the explicit specification can be removed",
        "DynamoDb",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB103"));

    /// <summary>
    /// Informational diagnostic when a same-score discriminator overlap is resolved by
    /// compound promotion using cross-key pattern disambiguation.
    /// </summary>
    public static readonly DiagnosticDescriptor CompoundPromotionResolved = new(
        "FDDB104",
        "Compound discrimination resolved overlap",
        "Entity '{0}' promoted to compound discrimination ({1}: '{2}' + {3}: '{4}') to resolve overlap with '{5}'",
        "DynamoDb",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB104"));

    // Schema Version Attribute Diagnostics (FDDB110-FDDB116)

    /// <summary>
    /// Warning when the assembly does not declare a schema version attribute, defaulting to 1.0.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingSchemaVersionAttribute = new(
        "FDDB110",
        "Missing schema version attribute",
        "Assembly does not declare [FluentDynamoDbSchemaVersion]. Defaulting to schema version 1.0. Add [assembly: FluentDynamoDbSchemaVersion(1, 0)] to suppress this warning.",
        "DynamoDb",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Every assembly using the FluentDynamoDb source generator should declare a schema version to explicitly state which generated code shape it targets.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB110"));

    /// <summary>
    /// Error when the declared schema version is below the minimum supported version.
    /// </summary>
    public static readonly DiagnosticDescriptor DeclaredVersionBelowMinimum = new(
        "FDDB111",
        "Declared version below minimum supported",
        "Declared schema version {0} is no longer supported. Minimum supported version is {1}. See {2} for migration guidance.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The declared schema version is older than the minimum version this generator supports. Update the schema version or pin to an older package version.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB111"));

    /// <summary>
    /// Error when the declared schema version is above the current supported version.
    /// </summary>
    public static readonly DiagnosticDescriptor DeclaredVersionAboveCurrent = new(
        "FDDB112",
        "Declared version above current",
        "Declared schema version {0} is not recognized. Maximum supported version is {1}. Update the Oproto.FluentDynamoDb package to a version that supports schema {0}.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The declared schema version is newer than what this generator supports. Update the Oproto.FluentDynamoDb NuGet package to a version that supports the declared schema version.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB112"));

    /// <summary>
    /// Info when the declared schema version is older but still supported, and an upgrade is available.
    /// </summary>
    public static readonly DiagnosticDescriptor OlderButSupportedVersion = new(
        "FDDB113",
        "Older-but-supported version, upgrade available",
        "Schema version {0} is supported but not current. Consider upgrading to {1} for the latest generated code improvements. See {2}.",
        "DynamoDb",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The declared schema version is still supported but a newer version is available with improved generated code. Consider upgrading when ready.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB113"));

    /// <summary>
    /// Error when the major version in the schema version attribute is less than 1.
    /// </summary>
    public static readonly DiagnosticDescriptor SchemaVersionMajorTooLow = new(
        "FDDB114",
        "Major version less than 1",
        "FluentDynamoDbSchemaVersion major version must be at least 1, but was {0}.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The major component of the schema version must be at least 1. Provide a valid major version value.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB114"));

    /// <summary>
    /// Error when the minor version in the schema version attribute is less than 0.
    /// </summary>
    public static readonly DiagnosticDescriptor SchemaVersionMinorTooLow = new(
        "FDDB115",
        "Minor version less than 0",
        "FluentDynamoDbSchemaVersion minor version must be at least 0, but was {0}.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The minor component of the schema version must be at least 0. Provide a valid minor version value.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB115"));

    /// <summary>
    /// Error when multiple schema version attributes are detected (via IL manipulation).
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleSchemaVersionAttributes = new(
        "FDDB116",
        "Multiple schema version attributes detected",
        "Multiple [FluentDynamoDbSchemaVersion] attributes detected. Remove duplicate declarations.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Multiple schema version attributes were found on the assembly, likely via IL manipulation since AllowMultiple is false. Code generation is halted.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB116"));

    // Constant Key Detection Diagnostics (FDDB120-FDDB123)

    /// <summary>
    /// Error when a constant key property also has a [Computed] attribute.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstantKeyComputedConflict = new(
        "FDDB120",
        "Constant key conflicts with computed attribute",
        "Property '{0}' is a constant key but also has [Computed] — these are mutually exclusive",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A constant key property returns a fixed compile-time value and cannot also be computed from other properties. Remove either the constant value or the [Computed] attribute.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB120"));

    /// <summary>
    /// Error when a constant key property has a Prefix configured.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstantKeyPrefixConflict = new(
        "FDDB121",
        "Prefix not applicable to constant key",
        "Property '{0}' is a constant key but has Prefix configured — prefix is meaningless on a constant value",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A constant key property already contains its full value at compile time. Prefix configuration is meaningless because the value cannot be decomposed into prefix + variable parts.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB121"));

    /// <summary>
    /// Error when an [Extracted] attribute references a constant key property as its source.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstantKeyExtractedConflict = new(
        "FDDB122",
        "Cannot extract from constant key",
        "Property '{0}' has [Extracted] referencing constant key property '{1}' — extraction from a constant is invalid",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Extraction splits a composite key into component parts, but a constant key has a fixed value with no variable components to extract.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB122"));

    /// <summary>
    /// Error when a constant key property has an empty or whitespace-only value.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstantKeyEmptyValue = new(
        "FDDB123",
        "Empty constant key value",
        "Property '{0}' has an empty or whitespace-only constant key value — keys must contain at least one non-whitespace character",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DynamoDB key values must contain at least one non-whitespace character. Provide a meaningful constant value for the key property.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB123"));

    /// <summary>
    /// Error when a property has both [Extracted] and [DynamoDbAttribute] applied.
    /// </summary>
    public static readonly DiagnosticDescriptor ExtractedPropertyHasAttributeMapping = new(
        "FDDB124",
        "Extracted property conflicts with DynamoDbAttribute",
        "Property '{0}' has both [Extracted] and [DynamoDbAttribute]. Extracted properties derive their value from a composite key and must not have independent DynamoDB attribute mapping. Remove one of the attributes.",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An [Extracted] property derives its value from a composite key at read time and should not also map to an independent DynamoDB attribute. Remove either [Extracted] or [DynamoDbAttribute].",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB124"));

    /// <summary>
    /// Error when a computed key property has a redundant Prefix on its key attribute.
    /// </summary>
    public static readonly DiagnosticDescriptor ComputedKeyPrefixConflict = new(
        "FDDB125",
        "Computed key property has redundant Prefix",
        "Property '{0}' is a computed key with Prefix = \"{1}\" configured on its key attribute. " +
        "Prefixes are not applied to computed keys — remove the Prefix and embed it in the " +
        "[Computed] Format if the prefix should appear in the stored value",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A computed key derives its value entirely from [Computed] configuration. " +
        "The Prefix on [PartitionKey] or [SortKey] is silently ignored at runtime. " +
        "Remove the Prefix to avoid confusion, or use Format = \"PREFIX#{0}\" on [Computed] " +
        "if the prefix should appear in the stored value.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB125"));

    /// <summary>
    /// Error when a key property uses expression-body or read-only auto-property syntax but references a non-compile-time-constant value.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstantKeyNonConstReference = new(
        "FDDB126",
        "Key property references non-compile-time-constant value",
        "Property '{0}' uses expression-body or read-only auto-property syntax but its value is not a compile-time constant — use a string literal or a 'const' field instead",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A key property with expression-body (=>) or read-only auto-property ({ get; }) syntax must resolve to a compile-time constant string (string literal or const field). " +
        "References to static readonly fields, properties, or method calls cannot be resolved at compile time and will produce uncompilable generated code. " +
        "Use a string literal (e.g., => \"VALUE\") or a const field (e.g., => MyConstants.Value where Value is const) instead.",
        helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB126"));
}