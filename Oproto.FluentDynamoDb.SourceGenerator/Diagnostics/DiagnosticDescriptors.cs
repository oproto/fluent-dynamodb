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
}