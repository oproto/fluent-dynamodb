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
        "[TimeToLive] can only be used on DateTime or DateTimeOffset properties, but property '{0}' is type '{1}'",
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
        "Entity '{0}' has multiple [TimeToLive] properties, but only one TTL field is allowed per entity",
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
        "Property '{0}' has unsupported collection type '{1}'; use Dictionary<string, T>, HashSet<T>, or List<T> instead",
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
        "Property '{0}' with [DynamoDbMap] has type '{1}' which must be marked with [DynamoDbEntity] to generate mapping code",
        "DynamoDb",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Custom types used with [DynamoDbMap] must be marked with [DynamoDbEntity] to generate the required mapping methods. This ensures AOT compatibility by avoiding reflection.");

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
        description: "All properties in a projection model must exist on the source entity.");

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
        description: "Projection property types must match the corresponding source entity property types.");

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
        description: "Projection source entity must be a valid DynamoDB entity class.");

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
        description: "Projection classes must be declared as partial to allow the source generator to add mapping code.");

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
        description: "UseProjection attribute must reference a valid projection model type.");

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
        description: "A GSI can only have one projection type constraint across all entities.");

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
        description: "Projections that include all properties provide no optimization benefit over using the full entity type.");

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
        description: "Projections with many properties may not provide significant performance benefits.");

    // Discriminator Configuration Diagnostics (DISC001-DISC003)

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
        description: "DiscriminatorValue and DiscriminatorPattern are mutually exclusive. Specify only one to avoid confusion.");

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
        description: "DiscriminatorProperty must be specified when using DiscriminatorValue or DiscriminatorPattern to indicate which DynamoDB attribute contains the discriminator.");

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
        description: "Discriminator patterns must use valid syntax with '*' as wildcard. Complex patterns with multiple wildcards in non-standard positions may not be supported.");

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
        description: "The [Encrypted] attribute requires the Oproto.FluentDynamoDb.Encryption.Kms package to provide encryption functionality.");

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
        description: "The [GenerateStreamConversion] attribute requires the Amazon.Lambda.DynamoDBEvents package to provide Lambda AttributeValue types for stream processing.");

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
        description: "When multiple entities share the same table name, one entity must be marked as the default using IsDefault = true in the [DynamoDbTable] attribute. The default entity is used for table-level operations.");

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
        description: "Only one entity per table can be marked as the default entity. Remove IsDefault = true from all but one entity in the table.");

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
        description: "Multiple [GenerateAccessors] attributes cannot target the same DynamoDB operation. Combine the configuration into a single attribute or use different operations.");

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
        description: "The Name property in [GenerateEntityProperty] cannot be empty. Either provide a valid custom name or omit the Name property to use the default pluralized entity name.");

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
        description: "When multiple entities in the same table have stream conversion enabled, they should all use the same discriminator property to ensure consistent stream processing behavior. The OnStream method will use the discriminator property from the first entity.");

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
        description: "The [GenerateWrapper] attribute can only be applied to extension methods. Extension methods must be static and have 'this' as the first parameter modifier.");

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
        description: "Extension methods marked with [GenerateWrapper] must extend an interface that is implemented by the builder class. The first parameter must be an interface type.");

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
        description: "The interface extended by marked extension methods could not be found in the compilation. This may indicate a missing reference or incorrect interface name.");

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
        description: "The builder class must implement all interfaces that are extended by methods marked with [GenerateWrapper]. Add the interface implementation to the builder class or remove the [GenerateWrapper] attribute from methods extending this interface.");

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
        description: "S2Level can only be used when SpatialIndexType is set to S2. Either set SpatialIndexType = SpatialIndexType.S2 or remove the S2Level property.");

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
        description: "H3Resolution can only be used when SpatialIndexType is set to H3. Either set SpatialIndexType = SpatialIndexType.H3 or remove the H3Resolution property.");

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
        description: "GeoHashPrecision can only be used when SpatialIndexType is set to GeoHash (or not specified, as GeoHash is the default). Either set SpatialIndexType = SpatialIndexType.GeoHash or remove the GeoHashPrecision property.");

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
        description: "Spatial index configuration properties can only be used on properties of type GeoLocation from the Oproto.FluentDynamoDb.Geospatial package.");

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
        description: "Spatial index configuration requires the Oproto.FluentDynamoDb.Geospatial package to provide GeoLocation type and spatial encoding functionality.");
}