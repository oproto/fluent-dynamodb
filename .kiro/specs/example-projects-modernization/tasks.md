# Implementation Plan: Example Projects Modernization

## Overview

Modernize 11 existing example projects to demonstrate recently added FluentDynamoDb features (auto key mode, auto-derived discriminators, per-property encryption key aliases, named blob providers, KeyInputMode, typed convenience overloads, schema version attribute, and computed field update model). Each change is isolated per project/requirement, minimizing regression risk. All changes preserve existing DynamoDB data shapes and runtime behavior.

## Tasks

- [x] 1. Remove explicit discriminator configuration (Requirement 2)
  - [x] 1.1 Remove discriminator from Invoice entity
    - In `examples/InvoiceManager/Entities/Invoice.cs`, remove `DiscriminatorProperty = "sk"` and `DiscriminatorPattern = "INVOICE#*"` from `[DynamoDbTable]` attribute, keeping `"invoices", IsDefault = true`
    - Update XML doc comment to state discriminator pattern is auto-derived from `[SortKey(Prefix = "INVOICE")]` by the source generator
    - _Requirements: 2.1, 2.7_

  - [x] 1.2 Remove discriminator from InvoiceLine entity
    - In `examples/InvoiceManager/Entities/InvoiceLine.cs`, remove `DiscriminatorProperty = "sk"` and `DiscriminatorPattern = "INVOICE#*#LINE#*"` from `[DynamoDbTable]` attribute, keeping table name `"invoices"`
    - Update XML doc comment to state discriminator pattern is auto-derived from hierarchical key composition by the source generator
    - _Requirements: 2.2, 2.7_

  - [x] 1.3 Remove discriminator from Customer entity
    - In `examples/InvoiceManager/Entities/Customer.cs`, remove `DiscriminatorProperty = "sk"` and `DiscriminatorValue = "PROFILE"` from `[DynamoDbTable]` attribute, keeping table name `"invoices"`
    - Update XML doc comment to state discriminator value is auto-derived from the constant sort key value by the source generator
    - _Requirements: 2.3, 2.7_

  - [x] 1.4 Remove discriminator from Account entity
    - In `examples/TransactionDemo/Entities/Account.cs`, remove `DiscriminatorProperty = "sk"` and `DiscriminatorValue = "PROFILE"` from `[DynamoDbTable]` attribute, keeping `"transaction-demo", IsDefault = true`
    - Update XML doc comment to state discriminator value is auto-derived from the constant sort key value by the source generator
    - _Requirements: 2.4, 2.7_

  - [x] 1.5 Remove discriminator from TransactionRecord entity
    - In `examples/TransactionDemo/Entities/TransactionRecord.cs`, remove `DiscriminatorProperty = "sk"` and `DiscriminatorPattern = "TXN#*"` from `[DynamoDbTable]` attribute, keeping table name `"transaction-demo"`
    - Update XML doc comment to state discriminator pattern is auto-derived from `[SortKey(Prefix = "TXN")]` by the source generator
    - _Requirements: 2.5, 2.7_

  - [x] 1.6 Remove discriminator from FinancialTransaction entity
    - In `examples/TransactionDemo/Entities/FinancialTransaction.cs`, remove `DiscriminatorProperty = "sk"` and `DiscriminatorPattern = "FIN#*"` from `[DynamoDbTable]` attribute, keeping table name `"transaction-demo"`
    - Update XML doc comment to state discriminator pattern is auto-derived from `[SortKey(Prefix = "FIN")]` by the source generator
    - _Requirements: 2.6, 2.7_

- [x] 2. Adopt auto key mode for Put operations (Requirement 1)
  - [x] 2.1 Update InvoiceManager Put operations to use auto key mode
    - In `examples/InvoiceManager/Program.cs`, replace `Customer.Keys.Pk(customerId)` with `customerId`, `Invoice.Keys.Pk(customerId)` with `customerId`, `Invoice.Keys.Sk(invoiceNumber)` with `invoiceNumber`, and `InvoiceLine.Keys.Pk(customerId)` with `customerId` in all Put operations only
    - Add inline comments explaining auto key mode applies prefixes automatically during serialization
    - Leave sort keys constructed from multiple segments unchanged (e.g., `$"INVOICE#{invoiceNumber}#LINE#{lineNumber}"`)
    - Leave all Get/Delete/Update/ConditionCheck operations using `Entity.Keys.Pk(value)` / `Entity.Keys.Sk(value)` unchanged
    - _Requirements: 1.1, 1.4, 1.5, 1.6_

  - [x] 2.2 Update TransactionDemo Put operations to use auto key mode
    - In `examples/TransactionDemo/TransactionComparison.cs`, replace `Account.Keys.Pk(accountId)` with `accountId` and `TransactionRecord.Keys.Pk(targetAccountId)` with `targetAccountId` in Put operations only
    - In `examples/TransactionDemo/Program.cs`, replace `FinancialTransaction.Keys.Pk(accountId)` with `accountId` and `FinancialTransaction.Keys.Sk(...)` with the raw value in the Put operation for `financialTxn`
    - Add inline comments explaining auto key mode applies prefixes during serialization
    - Leave sort keys constructed from multiple segments unchanged (e.g., `$"TXN#{timestamp}#{txnId}"`)
    - Leave all Get/Delete/Update/ConditionCheck operations unchanged
    - _Requirements: 1.2, 1.4, 1.5, 1.6_

  - [x] 2.3 Update OperationSamples Put operations to use auto key mode
    - In `examples/OperationSamples/Samples/PutSamples.cs`, ensure Order entity Put examples set `Pk` to raw value instead of `Order.Keys.Pk(orderId)` (if applicable in the sample usage pattern)
    - Add inline comments explaining auto key mode applies prefixes during serialization
    - Leave all Get/Delete/Update operations in other sample files unchanged
    - _Requirements: 1.3, 1.4, 1.5_

- [x] 3. Checkpoint - Build verification after discriminator and auto key mode changes
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build examples/` and verify zero errors
  - _Requirements: 2.8, 9.1_

- [x] 4. Demonstrate per-property key aliases for encryption (Requirement 3)
  - [x] 4.1 Add KeyAlias to encrypted properties in SecureRecord
    - In `examples/EncryptionDemo/Entities/SecureRecord.cs`, change `[Encrypted]` on `SocialSecurityNumber` to `[Encrypted(KeyAlias = "pii")]`
    - Change `[Encrypted]` on `CreditCardNumber` to `[Encrypted(KeyAlias = "financial")]`, retaining the existing `[Sensitive]` attribute
    - _Requirements: 3.1, 3.2_

  - [x] 4.2 Update EncryptionDemo Program.cs with aliasKeyMap configuration
    - In `examples/EncryptionDemo/Program.cs`, update `DefaultKmsKeyResolver` construction to pass an `aliasKeyMap` dictionary with `"pii"` and `"financial"` entries
    - Add inline comment explaining resolution priority: alias map → context map → default key
    - Ensure the resolver uses the async `IKmsKeyResolver` interface (`ResolveKeyIdAsync`)
    - _Requirements: 3.3, 3.4, 3.5_

- [x] 5. Demonstrate named blob providers (Requirement 4)
  - [x] 5.1 Extend MediaItem entity with named blob provider properties
    - In `examples/S3BlobDemo/Entities/MediaItem.cs`, add two new `[BlobStorage(Provider = "...")]` properties (e.g., `Thumbnail` with `Provider = "images"` and `Attachment` with `Provider = "documents"`)
    - Add inline comments explaining that omitting `Provider` uses the default provider, while specifying `Provider = "name"` routes to the named provider
    - _Requirements: 4.1, 4.3_

  - [x] 5.2 Register named blob providers in S3BlobDemo Program.cs
    - In `examples/S3BlobDemo/Program.cs`, register at least two named blob providers using `WithBlobStorage(name, provider)` in addition to the existing default provider
    - Add inline comments or console output demonstrating that each property routes to its designated storage backend
    - _Requirements: 4.2, 4.4_

- [x] 6. Demonstrate KeyInputMode parameter usage (Requirement 5)
  - [x] 6.1 Create KeyInputModeSamples.cs in OperationSamples
    - Create new file `examples/OperationSamples/Samples/KeyInputModeSamples.cs`
    - Demonstrate `KeyInputMode.Raw` Get operation passing fully-prefixed key value `"ORDER#12345"` used as-is
    - Demonstrate `KeyInputMode.Auto` Get operation passing raw value `"12345"` with auto prefix prepend
    - Use the same logical entity key (same Order entity) so behavioral difference is directly observable
    - Include inline comments on each mode explaining: what the mode does, when to use it, and that `KeyInputMode.Default` defers to `FluentDynamoDbOptions.DefaultKeyInputMode`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 7. Demonstrate typed convenience overloads for computed keys (Requirement 6)
  - [x] 7.1 Create ScheduledEvent entity with computed partition key
    - Create new file `examples/OperationSamples/Models/ScheduledEvent.cs`
    - Define entity with `[Computed("Year", "Month", "Day", Separator = "#")]` on PK and corresponding `[Extracted]` attributes on Year, Month, Day properties
    - Add `[DynamoDbTable("Orders")]` and `[GenerateEntityProperty(Name = "ScheduledEvents")]`
    - _Requirements: 6.1_

  - [x] 7.2 Create TypedOverloadSamples.cs in OperationSamples
    - Create new file `examples/OperationSamples/Samples/TypedOverloadSamples.cs`
    - Demonstrate typed Get overload: `table.ScheduledEvents.Get(2024, 12, 25, "sortKeyValue").GetItemAsync()`
    - Demonstrate typed Delete overload: `table.ScheduledEvents.Delete(2024, 12, 25, "sortKeyValue").DeleteAsync()`
    - Demonstrate typed Update overload: `table.ScheduledEvents.Update(2024, 12, 25, "sortKeyValue").Set(x => new ScheduledEventUpdateModel { Title = "Holiday" }).UpdateAsync()`
    - Include inline comments on each operation explaining these overloads are produced automatically by the source generator
    - _Requirements: 6.2, 6.3, 6.4, 6.5_

- [x] 8. Demonstrate computed field update model (Requirement 8)
  - [x] 8.1 Create CatalogItem entity with non-key computed field
    - Create new file `examples/OperationSamples/Models/CatalogItem.cs`
    - Define entity with `[Computed("Category", "Region", Separator = "#")]` on a GSI partition key (`Gsi1Pk`), plus `[Extracted]` attributes on Category and Region
    - Include `[GsiPartitionKey("category-region-index")]` on the computed field
    - _Requirements: 8.1_

  - [x] 8.2 Create ComputedFieldUpdateSamples.cs in OperationSamples
    - Create new file `examples/OperationSamples/Samples/ComputedFieldUpdateSamples.cs`
    - Demonstrate Update operation setting all source properties (Category, Region) in the update model, triggering automatic recomputation of gsi1pk
    - Include inline comment explaining non-key computed fields are recomputed automatically from source property values
    - Include inline comment explaining individual source properties are also persisted to their own DynamoDB attributes
    - Include inline comment that PK, SK, and extracted properties targeting key fields are excluded from the generated update model
    - Include inline comment that setting only a subset of source properties produces diagnostic FDDB072
    - _Requirements: 8.2, 8.3, 8.4, 8.5_

- [x] 9. Checkpoint - Build verification after new entities and sample files
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build examples/` and verify zero errors for all new entities and sample files
  - _Requirements: 9.1_

- [x] 10. Add FluentDynamoDbSchemaVersion attribute to all example projects (Requirement 7)
  - [x] 10.1 Create AssemblyInfo.cs for TodoList project
    - Create `examples/TodoList/AssemblyInfo.cs` with `using Oproto.FluentDynamoDb.Attributes;` and `[assembly: FluentDynamoDbSchemaVersion(1, 0)]`
    - Add inline comment explaining schema versions decouple generated code shape from NuGet package version
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.2 Create AssemblyInfo.cs for InvoiceManager project
    - Create `examples/InvoiceManager/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.3 Create AssemblyInfo.cs for TransactionDemo project
    - Create `examples/TransactionDemo/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.4 Create AssemblyInfo.cs for OperationSamples project
    - Create `examples/OperationSamples/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.5 Create AssemblyInfo.cs for DynamicFieldsDemo project
    - Create `examples/DynamicFieldsDemo/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.6 Create AssemblyInfo.cs for DynamicTableDemo project
    - Create `examples/DynamicTableDemo/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.7 Create AssemblyInfo.cs for EncryptionDemo project
    - Create `examples/EncryptionDemo/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.8 Create AssemblyInfo.cs for S3BlobDemo project
    - Create `examples/S3BlobDemo/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.9 Create AssemblyInfo.cs for JsonBlobDemo project
    - Create `examples/JsonBlobDemo/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 10.10 Create AssemblyInfo.cs for StoreLocator project
    - Create `examples/StoreLocator/AssemblyInfo.cs` with the same schema version declaration and comment
    - _Requirements: 7.1, 7.2, 7.3_

- [x] 11. Final checkpoint - Full build and test verification (Requirement 9)
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build examples/` to verify zero errors across all 10 example projects
  - Run `dotnet test` on Examples.Tests project to verify existing tests pass without assertion changes
  - Verify DynamoDB attribute names and key prefix values remain unchanged in all modified entity files
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

## Notes

- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation after each logical group of changes
- The design explicitly states property-based testing does not apply (binary correctness verified by build + integration tests)
- Sort keys with multi-segment manual composition (e.g., `$"INVOICE#{x}#LINE#{y}"` or `$"TXN#{ts}#{id}"`) are intentionally left unchanged per Requirement 1.6
- New entities (ScheduledEvent, CatalogItem) reuse the existing `Orders` table with separate entity accessors
- AssemblyInfo.cs files are identical across projects (same schema version 1.0)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "10.1", "10.2", "10.3", "10.4", "10.5", "10.6", "10.7", "10.8", "10.9", "10.10"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "4.1", "5.1", "6.1", "7.1", "8.1"] },
    { "id": 2, "tasks": ["4.2", "5.2", "7.2", "8.2"] }
  ]
}
```
