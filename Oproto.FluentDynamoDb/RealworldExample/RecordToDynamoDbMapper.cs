using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.Ledgers.Contracts.Models.Transactions;
using Oproto.Ledgers.Shared.DynamoDb;
using Oproto.Transactions.Service.DataLayer;
using Riok.Mapperly.Abstractions;

namespace Oproto.Transactions.Service;

[Mapper]
public static partial class RecordToDynamoDbMapper
{
    public static Dictionary<string, AttributeValue> TxnLedgerEntryToAttributeDictionary(TransactionEntry txn,
        TransactionLedgerEntry ldgrEntry)
    {
        var pk = TransactionsTable.TransactionPrimaryKey(txn.TenantId, txn.TransactionId);
        var sk = TransactionsTable.TransactionSortKey(ldgrEntry.LedgerId, ldgrEntry.LineItemId);
        var gsi1Pk = TransactionsTable.Gsi1TransactionPrimaryKey(txn.TenantId, ldgrEntry.LedgerId);
        var gsi1Sk = TransactionsTable.Gsi1TransactionSortKey(txn.TransactionId);
        var gsi2Pk = TransactionsTable.Gsi2TransactionPrimaryKey(txn.TenantId, txn.ParentTransactionId);
        var gsi2Sk = gsi2Pk == null ? null : TransactionsTable.Gsi2TransactionSortKey(txn.TransactionId);
        var gsi3Pk = TransactionsTable.Gsi3TransactionPrimaryKey(txn.TenantId, txn.OriginalTransactionId);
        var gsi3Sk = gsi3Pk == null ? null : TransactionsTable.Gsi3TransactionSortKey(txn.TransactionId);


        var gsi5Pk = txn.Status == TransactionStatus.Active || txn.Status == TransactionStatus.Draft
            ? TransactionsTable.Gsi1TransactionPrimaryKey(txn.TenantId, ldgrEntry.LedgerId)
            : null;
        var gsi5Sk = txn.Status == TransactionStatus.Active || txn.Status == TransactionStatus.Draft
            ? TransactionsTable.Gsi1TransactionSortKey(txn.TransactionId)
            : null;

        var values = new Dictionary<string, AttributeValue>
        {
            { TransactionAttributes.Pk, new AttributeValue { S = pk } },
            { TransactionAttributes.Sk, new AttributeValue { S = sk } },
            { TransactionAttributes.Gsi1Pk, new AttributeValue { S = gsi1Pk } },
            { TransactionAttributes.Gsi1Sk, new AttributeValue { S = gsi1Sk } },
            {
                TransactionAttributes.Gsi5Pk, gsi5Pk == null
                    ? new AttributeValue { NULL = true }
                    : new AttributeValue { S = gsi5Pk }
            },
            {
                TransactionAttributes.Gsi5Sk, gsi5Sk == null
                    ? new AttributeValue { NULL = true }
                    : new AttributeValue { S = gsi5Sk }
            },
            { TransactionAttributes.CreatedDate, new AttributeValue { S = txn.CreatedDate.ToString("o") } },
            { TransactionAttributes.CreatedBy, new AttributeValue { S = txn.CreatedBy } },
            { TransactionAttributes.UpdatedDate, new AttributeValue { S = txn.UpdatedDate.ToString("o") } },
            { TransactionAttributes.UpdatedBy, new AttributeValue { S = txn.UpdatedBy } },
            {
                TransactionAttributes.OriginalTransactionId, txn.OriginalTransactionId == null
                    ? new AttributeValue { NULL = true }
                    : new AttributeValue { S = txn.OriginalTransactionId.Value.ToString() }
            },
            {
                TransactionAttributes.ParentTransactionId, txn.ParentTransactionId == null
                    ? new AttributeValue { NULL = true }
                    : new AttributeValue { S = txn.ParentTransactionId.Value.ToString() }
            },
            { TransactionAttributes.Status, new AttributeValue { S = txn.Status.ToString() } },
            { TransactionAttributes.RelationType, new AttributeValue { S = txn.RelationType.ToString() } },
            {
                TransactionAttributes.Reason, txn.Reason == null
                    ? new AttributeValue { NULL = true }
                    : new AttributeValue { S = txn.Reason }
            },
            {
                TransactionAttributes.SourceDocumentId, txn.SourceDocumentId == null
                    ? new AttributeValue { NULL = true }
                    : new AttributeValue { S = txn.SourceDocumentId }
            },
            {
                TransactionAttributes.SourceDocumentType, txn.SourceDocumentType == null
                    ? new AttributeValue { NULL = true }
                    : new AttributeValue { S = txn.SourceDocumentType }
            },
            {
                TransactionAttributes.Metadata, !txn.Metadata.Any()
                    ? new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["_init"] = new() { BOOL = true }
                        }
                    }
                    : new AttributeValue { M = MetadataToDictionaryAttributeValues(txn.Metadata) }
            },
            // Different per ledger entry
            {
                TransactionAttributes.To,
                new AttributeValue { N = ldgrEntry.To.ToString("G", CultureInfo.InvariantCulture) }
            },
            {
                TransactionAttributes.From,
                new AttributeValue { N = ldgrEntry.From.ToString("G", CultureInfo.InvariantCulture) }
            },
            { TransactionAttributes.ChainPosition, new AttributeValue { N = ldgrEntry.ChainPosition.ToString("D") } },
            { TransactionAttributes.LineItemId, new AttributeValue { S = ldgrEntry.LineItemId.ToString() } },
            {
                TransactionAttributes.BalanceTreeNodeId,
                new AttributeValue { S = ldgrEntry.BalanceTreeNodeId.ToString() }
            },
            {
                TransactionAttributes.BalanceTreeNodeChunkId,
                new AttributeValue { N = ldgrEntry.BalanceTreeChunkId.ToString() }
            }
        };

        if (gsi2Pk != null)
        {
            values.Add(TransactionAttributes.Gsi2Pk, new AttributeValue { S = gsi2Pk });
            if (gsi2Sk != null)
            {
                values.Add(TransactionAttributes.Gsi2Sk, new AttributeValue { S = gsi2Sk });
            }
        }

        if (gsi3Pk != null)
        {
            values.Add(TransactionAttributes.Gsi3Pk, new AttributeValue { S = gsi3Pk });
            if (gsi3Sk != null)
            {
                values.Add(TransactionAttributes.Gsi3Sk, new AttributeValue { S = gsi3Sk });
            }
        }

        return values;
    }

    public static TransactionEntry AttributeValueDictionariesToTransactionEntry(
        IList<Dictionary<string, AttributeValue>> items)
    {
        if (items.Count == 0) throw new Exception("Items cannot be empty");

        var firstEntry = items.First();
        var pk = firstEntry.ForKey(TransactionAttributes.Pk)!.S;
        var sk = firstEntry.ForKey(TransactionAttributes.Sk)!.S;
        var pkParts = pk.Split('#');
        if (pkParts.Length != 3) throw new Exception("Invalid transaction key");

        var tenantId = Ulid.Parse(pkParts[0]);
        if (pkParts[1] != "txn") throw new Exception("Invalid transaction key");
        var transactionId = Ulid.Parse(pkParts[2]);

        var ledgerEntries = new List<TransactionLedgerEntry>();
        foreach (var entry in items)
        {
            var sortKey = entry.ForKey(TransactionAttributes.Sk)!.S;
            var sortKeyParts = sortKey.Split('#');
            
            // Parse the new format: {ledgerId}#{lineItemId}
            if (sortKeyParts.Length != 2)
            {
                throw new InvalidOperationException($"Invalid sort key format: {sortKey}. Expected format: {{ledgerId}}#{{lineItemId}}");
            }
            
            var ledgerId = Ulid.Parse(sortKeyParts[0]);
            var lineItemId = Ulid.Parse(sortKeyParts[1]);
            
            var ldgrEntry = new TransactionLedgerEntry
            {
                LedgerId = ledgerId,
                LineItemId = lineItemId,
                From = decimal.Parse(entry.ForKey(TransactionAttributes.From)!.N),
                To = decimal.Parse(entry.ForKey(TransactionAttributes.To)!.N),
                ChainPosition = int.Parse(entry.ForKey(TransactionAttributes.ChainPosition)!.N),
                BalanceTreeNodeId = Ulid.Parse(entry.ForKey(TransactionAttributes.BalanceTreeNodeId)!.S),
                BalanceTreeChunkId = ulong.Parse(entry.ForKey(TransactionAttributes.BalanceTreeNodeChunkId)!.N)
            };
            ledgerEntries.Add(ldgrEntry);
        }

        var txn = new TransactionEntry
        {
            TenantId = tenantId,
            TransactionId = transactionId,
            CreatedDate = DateTime.Parse(firstEntry.ForKey(TransactionAttributes.CreatedDate)!.S),
            CreatedBy = firstEntry.ForKey(TransactionAttributes.CreatedBy)!.S,
            UpdatedDate = DateTime.Parse(firstEntry.ForKey(TransactionAttributes.UpdatedDate)!.S),
            UpdatedBy = firstEntry.ForKey(TransactionAttributes.UpdatedBy)!.S,
            OriginalTransactionId = firstEntry.ForKey(TransactionAttributes.OriginalTransactionId) is
                { NULL: false } originalAttr
                ? Ulid.Parse(originalAttr.S)
                : null,
            ParentTransactionId = firstEntry.ForKey(TransactionAttributes.ParentTransactionId) is { NULL: false }
                ? Ulid.Parse(firstEntry.ForKey(TransactionAttributes.ParentTransactionId)!.S)
                : null,
            Status = Enum.Parse<TransactionStatus>(firstEntry.ForKey(TransactionAttributes.Status)!.S),
            RelationType =
                Enum.Parse<TransactionRelationType>(firstEntry.ForKey(TransactionAttributes.RelationType)!.S),
            Reason = firstEntry.ForKey(TransactionAttributes.Reason) is { NULL: false } reasonAttr
                ? reasonAttr.S
                : null,
            Metadata = firstEntry.ForKey(TransactionAttributes.Metadata) is { NULL: false } metadataAttr
                ? DictionaryAttributeValuesToMetadata(metadataAttr.M)
                : new Dictionary<string, string>(),
            SourceDocumentId = firstEntry.ForKey(TransactionAttributes.SourceDocumentId) is
                { NULL: false } sourceDocumentIdAttr
                ? sourceDocumentIdAttr.S
                : null,
            SourceDocumentType = firstEntry.ForKey(TransactionAttributes.SourceDocumentType) is
                { NULL: false } sourceDocumentTypeAttr
                ? sourceDocumentTypeAttr.S
                : null,
            LedgerEntries = ledgerEntries
        };
        return txn;
    }

    public static Dictionary<string, string> DictionaryAttributeValuesToMetadata(Dictionary<string, AttributeValue> map)
    {
        return map
            .Where(x => x.Key != "_init") // Filter out the special _init key used for empty metadata
            .ToDictionary(x => x.Key, x => x.Value.S);
    }

    public static Dictionary<string, AttributeValue> MetadataToDictionaryAttributeValues(Dictionary<string, string> map)
    {
        return map
            .Where(x => !string.IsNullOrEmpty(x.Value)) // Filter out null or empty values
            .ToDictionary(x => x.Key, x => new AttributeValue(x.Value));
    }

    public static LedgerTransactionResult TransactionEntryToLedgerTransactionResult(TransactionEntry entry)
    {
        var ledgerEntry = entry.LedgerEntries.Single();
        return TransactionEntryToLedgerTransactionResult(entry, ledgerEntry);
    }

    public static LedgerTransactionResult TransactionEntryToLedgerTransactionResult(TransactionEntry entry,
        TransactionLedgerEntry ledgerEntry)
    {
        return new LedgerTransactionResult
        {
            TenantId = entry.TenantId,
            TransactionId = entry.TransactionId,
            OriginalTransactionId = entry.OriginalTransactionId,
            ParentTransactionId = entry.ParentTransactionId,
            Status = entry.Status,
            RelationType = entry.RelationType,
            CreatedDate = entry.CreatedDate,
            CreatedBy = entry.CreatedBy,
            UpdatedDate = entry.UpdatedDate,
            UpdatedBy = entry.UpdatedBy,
            Reason = entry.Reason,
            Metadata = entry.Metadata,
            SourceDocumentId = entry.SourceDocumentId,
            SourceDocumentType = entry.SourceDocumentType,
            LedgerId = ledgerEntry.LedgerId,
            LineItemId = ledgerEntry.LineItemId,
            To = ledgerEntry.To,
            From = ledgerEntry.From,
            ChainPosition = ledgerEntry.ChainPosition,
            BalanceTreeNodeId = ledgerEntry.BalanceTreeNodeId,
            BalanceTreeChunkId = ledgerEntry.BalanceTreeChunkId
        };
    }
}