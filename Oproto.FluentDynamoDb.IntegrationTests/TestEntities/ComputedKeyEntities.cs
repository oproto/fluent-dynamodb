using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Entity with computed PK only (no SK).
/// PK is composed from Year + Month + Day (non-string int types).
/// Exercises: Computed PK only, non-string source property types.
/// </summary>
[DynamoDbTable("test-computed-pk-only")]
public partial class ComputedPkOnlyEvent
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", "Day", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }

    [Extracted("Pk", 2)]
    public int Day { get; set; }

    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;

    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Entity with computed SK only (simple string PK).
/// SK is composed from Region + Category (string types).
/// Exercises: Computed SK only with simple PK.
/// </summary>
[DynamoDbTable("test-computed-sk-only")]
public partial class ComputedSkOnlyOrder
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("Region", "Category", Separator = "#")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Sk", 0)]
    public string Region { get; set; } = string.Empty;

    [Extracted("Sk", 1)]
    public string Category { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
}

/// <summary>
/// Entity with both PK and SK computed.
/// PK composed from TenantId + UserId, SK composed from Year + Month.
/// Exercises: Both keys computed.
/// </summary>
[DynamoDbTable("test-computed-both-keys")]
public partial class ComputedBothKeysEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("TenantId", "UserId", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("Year", "Month", Separator = "#")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string TenantId { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string UserId { get; set; } = string.Empty;

    [Extracted("Sk", 0)]
    public int Year { get; set; }

    [Extracted("Sk", 1)]
    public int Month { get; set; }

    [DynamoDbAttribute("data")]
    public string? Data { get; set; }
}

/// <summary>
/// Entity with computed PK from TenantId + OrderNum.
/// Exercises: Computed key without prefix (prefix on computed keys is invalid per FDDB125).
/// </summary>
[DynamoDbTable("test-computed-with-prefix")]
public partial class ComputedWithPrefixEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("TenantId", "OrderNum", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string TenantId { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string OrderNum { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
}

/// <summary>
/// Entity with computed PK using non-string source property types.
/// Uses int, DateTime, and Guid as source properties.
/// Exercises: Non-string source property types.
/// </summary>
[DynamoDbTable("test-computed-non-string-types")]
public partial class ComputedNonStringTypesEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("AccountId", "CreatedDate", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("TransactionId", "SequenceNumber", Separator = "#")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public Guid AccountId { get; set; }

    [Extracted("Pk", 1)]
    public DateTime CreatedDate { get; set; }

    [Extracted("Sk", 0)]
    public Guid TransactionId { get; set; }

    [Extracted("Sk", 1)]
    public int SequenceNumber { get; set; }

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    [DynamoDbAttribute("note")]
    public string? Note { get; set; }
}
