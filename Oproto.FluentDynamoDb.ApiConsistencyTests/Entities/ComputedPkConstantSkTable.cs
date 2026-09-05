namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

/// <summary>
/// Entity with computed partition key (PkTenantId, PkCompanyId) and a constant sort key.
/// The constant SK uses expression-body syntax (=> "COUNTER#CUSTOMER_NUMBER").
/// Used by bug condition exploration tests to verify that typed overloads correctly
/// omit the constant SK from parameter lists and delegation arguments.
/// Requirements: 1.1, 1.2, 1.3, 1.4, 1.5
/// </summary>
[DynamoDbTable(typeof(ComputedPkConstantSkTable))]
public partial class ComputedPkConstantSkEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("PkTenantId", "PkCompanyId", Format = "{0}#COMPANY#{1}")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public Guid PkTenantId { get; set; }

    [Extracted("Pk", 1)]
    public Guid PkCompanyId { get; set; }

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => "COUNTER#CUSTOMER_NUMBER";

    [DynamoDbAttribute("value")]
    public int Value { get; set; }
}

/// <summary>
/// Type-based table reference for ComputedPkConstantSkEntity.
/// The source generator will generate the table class implementation as a partial class.
/// The generated table class name will be ComputedPkConstantSkTableTable.
/// </summary>
public partial class ComputedPkConstantSkTable { }
