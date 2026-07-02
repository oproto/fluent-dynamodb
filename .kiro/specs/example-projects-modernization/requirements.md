# Requirements Document

## Introduction

Modernize the 11 existing example projects in the `examples/` directory to demonstrate recently added FluentDynamoDb features and best practices. The examples serve as living documentation and onboarding material for new users, so they must reflect the current recommended API surface. This effort removes deprecated patterns (manual key building, explicit discriminator configuration) and introduces new capabilities (auto key mode, typed convenience overloads, per-property encryption key aliases, named blob providers, and KeyInputMode usage).

## Glossary

- **Example_Project**: A standalone .NET console application in the `examples/` directory that demonstrates FluentDynamoDb features against DynamoDB Local.
- **Auto_Key_Mode**: The default `KeyInputMode.Auto` behavior where key prefixes are automatically applied during Put serialization, eliminating the need for manual `Entity.Keys.Pk(value)` calls on Put operations.
- **KeyInputMode**: An enum (`Default`, `Auto`, `Value`, `Raw`) controlling how key values are interpreted before being sent to DynamoDB.
- **Typed_Convenience_Overload**: A source-generated Get/Delete/Update method that accepts individual typed source property parameters for entities with computed keys, instead of requiring manual key string composition.
- **Auto_Derived_Discriminator**: A discriminator pattern automatically derived by the source generator from key prefixes and computed field formats, eliminating the need for explicit `DiscriminatorProperty`/`DiscriminatorValue`/`DiscriminatorPattern` configuration.
- **Named_Blob_Provider**: A blob storage provider registered with a string name via `FluentDynamoDbOptions.WithBlobStorage(name, provider)`, enabling per-property routing to different storage backends.
- **Per_Property_Key_Alias**: The `KeyAlias` parameter on `[Encrypted]` that routes a specific property to a designated KMS key via the resolver's alias map.
- **Schema_Version_Attribute**: The `[assembly: FluentDynamoDbSchemaVersion(major, minor)]` attribute that declares which source generator schema version a consumer assembly targets.
- **Source_Generator**: The compile-time code generator that produces DynamoDB mapping, key builder, and accessor code from entity attribute declarations.

## Requirements

### Requirement 1: Adopt Auto Key Mode for Put Operations

**User Story:** As a developer learning FluentDynamoDb, I want example projects to demonstrate auto key mode for Put operations, so that I understand the recommended approach of passing raw values instead of manually calling key builders.

#### Acceptance Criteria

1. WHEN a Put operation is performed in InvoiceManager, THE Example_Project SHALL set key properties to raw values (e.g., `Pk = customerId`) instead of calling `Customer.Keys.Pk(customerId)`, `Invoice.Keys.Pk(customerId)`, `Invoice.Keys.Sk(invoiceNumber)`, or `InvoiceLine.Keys.Pk(customerId)`.
2. WHEN a Put operation is performed in TransactionDemo, THE Example_Project SHALL set key properties to raw values (e.g., `Pk = accountId`) instead of calling `Account.Keys.Pk(accountId)` or `TransactionRecord.Keys.Pk(targetAccountId)`.
3. WHEN a Put operation is performed in OperationSamples, THE Example_Project SHALL set key properties to raw values (e.g., `Pk = orderId`) instead of calling `Order.Keys.Pk(orderId)` or `OrderLine.Keys.Pk(orderId)`.
4. THE Example_Project SHALL retain inline comments on each modified Put operation explaining that auto key mode applies prefixes automatically during serialization.
5. WHEN a Get, Delete, Update, or ConditionCheck operation uses a key value in InvoiceManager, TransactionDemo, or OperationSamples, THE Example_Project SHALL continue using `Entity.Keys.Pk(value)` and `Entity.Keys.Sk(value)` for those operations, since auto key mode applies only to Put.
6. IF a sort key for a Put operation is constructed from multiple segments without a single-value prefix mapping (e.g., `$"INVOICE#{invoiceNumber}#LINE#{lineNumber}"` or `$"TXN#{timestamp}#{txnId}"`), THEN THE Example_Project SHALL continue constructing that sort key manually rather than relying on auto key mode.

### Requirement 2: Remove Explicit Discriminator Configuration Where Auto-Derivation Applies

**User Story:** As a developer learning FluentDynamoDb, I want example entities to rely on auto-derived discriminator patterns, so that I understand the simplified single-table configuration approach.

#### Acceptance Criteria

1. WHEN the Invoice entity in `examples/InvoiceManager/Entities/Invoice.cs` has `[SortKey(Prefix = "INVOICE")]`, THE Example_Project SHALL remove the explicit `DiscriminatorProperty = "sk"` and `DiscriminatorPattern = "INVOICE#*"` named arguments from the `[DynamoDbTable]` attribute, leaving the remaining arguments (`"invoices"`, `IsDefault = true`) unchanged.
2. WHEN the InvoiceLine entity in `examples/InvoiceManager/Entities/InvoiceLine.cs` has a sort key that produces the pattern `INVOICE#*#LINE#*` via hierarchical key composition, THE Example_Project SHALL remove the explicit `DiscriminatorProperty = "sk"` and `DiscriminatorPattern = "INVOICE#*#LINE#*"` named arguments from the `[DynamoDbTable]` attribute, leaving the table name `"invoices"` unchanged.
3. WHEN the Customer entity in `examples/InvoiceManager/Entities/Customer.cs` has a constant sort key value `"PROFILE"` that the source generator can auto-derive as `DiscriminatorValue`, THE Example_Project SHALL remove the explicit `DiscriminatorProperty = "sk"` and `DiscriminatorValue = "PROFILE"` named arguments from the `[DynamoDbTable]` attribute, leaving the table name `"invoices"` unchanged.
4. WHEN the Account entity in `examples/TransactionDemo/Entities/Account.cs` has a constant sort key value `"PROFILE"` that the source generator can auto-derive as `DiscriminatorValue`, THE Example_Project SHALL remove the explicit `DiscriminatorProperty = "sk"` and `DiscriminatorValue = "PROFILE"` named arguments from the `[DynamoDbTable]` attribute, leaving the remaining arguments (`"transaction-demo"`, `IsDefault = true`) unchanged.
5. WHEN the TransactionRecord entity in `examples/TransactionDemo/Entities/TransactionRecord.cs` has `[SortKey(Prefix = "TXN")]` on its sort key property, THE Example_Project SHALL remove the explicit `DiscriminatorProperty = "sk"` and `DiscriminatorPattern = "TXN#*"` named arguments from the `[DynamoDbTable]` attribute, leaving the table name `"transaction-demo"` unchanged.
6. WHEN the FinancialTransaction entity in `examples/TransactionDemo/Entities/FinancialTransaction.cs` has `[SortKey(Prefix = "FIN")]`, THE Example_Project SHALL remove the explicit `DiscriminatorProperty = "sk"` and `DiscriminatorPattern = "FIN#*"` named arguments from the `[DynamoDbTable]` attribute, leaving the table name `"transaction-demo"` unchanged.
7. THE Example_Project SHALL add an XML doc comment `<summary>` element on each modified entity class (Invoice, InvoiceLine, Customer, Account, TransactionRecord, FinancialTransaction) that includes a sentence stating the discriminator pattern or value is auto-derived from the sort key prefix or constant sort key value by the source generator.
8. IF the project is built with `dotnet build` after all discriminator removals, THEN the build SHALL complete with zero errors and zero new warnings related to discriminator configuration (existing unrelated warnings are acceptable).

### Requirement 3: Demonstrate Per-Property Key Aliases for Encryption

**User Story:** As a developer implementing multi-classification encryption, I want the EncryptionDemo to demonstrate per-property key aliases, so that I understand how different properties use different KMS keys.

#### Acceptance Criteria

1. THE Example_Project SHALL add `[Encrypted(KeyAlias = "pii")]` to the SocialSecurityNumber property on SecureRecord in `examples/EncryptionDemo/Entities/SecureRecord.cs`.
2. THE Example_Project SHALL add `[Encrypted(KeyAlias = "financial")]` to the CreditCardNumber property on SecureRecord (retaining the existing `[Sensitive]` attribute alongside it).
3. WHEN configuring the `DefaultKmsKeyResolver` in `examples/EncryptionDemo/Program.cs`, THE Example_Project SHALL pass an `aliasKeyMap` dictionary containing at least `{"pii": "<kms-arn>", "financial": "<kms-arn>"}` to demonstrate per-property key routing.
4. THE Example_Project SHALL include an inline comment adjacent to the `DefaultKmsKeyResolver` construction explaining resolution priority: alias map → context map → default key.
5. THE Example_Project SHALL use the async `IKmsKeyResolver` interface (`ResolveKeyIdAsync`) which is already the current interface shape after the breaking change.

### Requirement 4: Demonstrate Named Blob Providers

**User Story:** As a developer storing blobs across multiple S3 buckets, I want the S3BlobDemo to demonstrate named blob providers, so that I understand how to route different properties to different storage backends.

#### Acceptance Criteria

1. THE Example_Project SHALL add a new entity or extend MediaItem in S3BlobDemo with at least two `[BlobStorage]` properties using different named providers (e.g., `[BlobStorage(Provider = "images")]` and `[BlobStorage(Provider = "documents")]`).
2. WHEN configuring `FluentDynamoDbOptions` in S3BlobDemo, THE Example_Project SHALL register at least two named blob providers using `WithBlobStorage(name, provider)` in addition to the default provider registered with `WithBlobStorage(provider)`.
3. THE Example_Project SHALL include inline comments explaining that omitting the `Provider` parameter on `[BlobStorage]` uses the default provider, while specifying `Provider = "name"` routes to the named provider.
4. THE Example_Project SHALL include inline comments or console output demonstrating that each property routes to its designated storage backend independently based on the `Provider` value.

### Requirement 5: Demonstrate KeyInputMode Parameter Usage

**User Story:** As a developer needing per-operation control over key prefix behavior, I want at least one example to demonstrate explicit KeyInputMode parameter usage, so that I understand how to override the default auto mode when needed.

#### Acceptance Criteria

1. THE Example_Project SHALL demonstrate at least one Get operation using an explicit `KeyInputMode.Raw` parameter, passing a fully-prefixed key value (e.g., `"ORDER#12345"`) that is used as-is without modification.
2. THE Example_Project SHALL demonstrate at least one Get operation using an explicit `KeyInputMode.Auto` parameter, passing a raw value (e.g., `"12345"`) to show that the system detects the prefix is absent and prepends it automatically.
3. THE Example_Project SHALL include inline comments on each demonstrated mode explaining: (a) what the mode does to the input value, (b) when to use it instead of relying on the default, and (c) that `KeyInputMode.Default` defers to `FluentDynamoDbOptions.DefaultKeyInputMode` which resolves to Auto when not explicitly configured.
4. WHEN demonstrating KeyInputMode, THE Example_Project SHALL place the examples in a dedicated sample file within the `OperationSamples/Samples` folder, following the existing naming convention, to maintain the comparison-focused structure of that project.
5. THE Example_Project SHALL demonstrate both `KeyInputMode.Raw` and `KeyInputMode.Auto` using the same logical entity key so that the behavioral difference between passing a pre-prefixed value versus a raw component value is directly observable by comparison.

### Requirement 6: Demonstrate Typed Convenience Overloads for Computed Keys

**User Story:** As a developer using entities with computed keys, I want an example demonstrating typed convenience overloads, so that I understand how to use strongly-typed parameters instead of manual key composition.

#### Acceptance Criteria

1. THE Example_Project SHALL include at least one entity with a computed partition key composed of two or more source properties using the `[Computed]` attribute with a `Separator` (e.g., `[Computed("Year", "Month", "Day", Separator = "#")]`), along with corresponding `[Extracted]` attributes on each source property to enable round-trip decomposition.
2. WHEN demonstrating the typed Get overload, THE Example_Project SHALL show calling the generated entity accessor Get method with individual typed parameters matching the computed key's source property types followed by the sort key value (e.g., `table.Events.Get(2024, 12, 25, "sortKeyValue").GetItemAsync()`), and SHALL include an inline comment stating that this overload is produced automatically by the source generator for entities with multi-property computed keys.
3. WHEN demonstrating the typed Delete overload, THE Example_Project SHALL show calling the generated entity accessor Delete method with individual typed parameters matching the computed key's source property types followed by the sort key value (e.g., `table.Events.Delete(2024, 12, 25, "sortKeyValue").DeleteAsync()`), and SHALL include an inline comment stating that this overload is produced automatically by the source generator.
4. WHEN demonstrating the typed Update overload, THE Example_Project SHALL show calling the generated entity accessor Update method with individual typed parameters matching the computed key's source property types followed by the sort key value, chained with a `.Set()` lambda expression that updates at least one non-key property (e.g., `table.Events.Update(2024, 12, 25, "sortKeyValue").Set(x => new EventUpdateModel { Title = "Holiday" }).UpdateAsync()`), and SHALL include an inline comment stating that this overload is produced automatically by the source generator.
5. THE Example_Project SHALL include at least one inline comment per demonstrated operation (Get, Delete, Update) explaining that the source generator automatically produces these typed convenience overloads for any entity whose key uses the `[Computed]` attribute with multiple source properties, eliminating the need to manually build composite key strings.

### Requirement 7: Add FluentDynamoDbSchemaVersion Attribute to Examples

**User Story:** As a developer setting up a new project, I want example projects to include the schema version attribute, so that I understand the recommended practice of pinning generated code shape explicitly.

#### Acceptance Criteria

1. THE Example_Project SHALL add `[assembly: FluentDynamoDbSchemaVersion(1, 0)]` to a file named `AssemblyInfo.cs` or at the top of `Program.cs` in each example project that contains at least one class annotated with `[DynamoDbTable]`.
2. THE Example_Project SHALL place an inline code comment on the line immediately above the `[assembly: FluentDynamoDbSchemaVersion(1, 0)]` declaration explaining that schema versions decouple generated code shape from NuGet package version.
3. THE Example_Project SHALL include the `using Oproto.FluentDynamoDb.Attributes;` directive in the file containing the schema version attribute.

### Requirement 8: Demonstrate Computed Field Update Model with Source-Property-Based Updates

**User Story:** As a developer updating entities with computed fields, I want an example showing source-property-based updates with automatic recomputation, so that I understand the new update model behavior.

#### Acceptance Criteria

1. THE Example_Project SHALL include an entity definition with at least one non-key computed field (e.g., a GSI partition key) annotated with `[Computed]` specifying source properties and a separator, along with corresponding `[Extracted]` properties, so that the relationship between sources and computed output is visible in the example code.
2. THE Example_Project SHALL include at least one Update operation where setting all source properties of a non-key computed field in the update model triggers the expression translator to produce a SET expression targeting the computed field's DynamoDB attribute with the concatenated value using the configured separator.
3. THE Example_Project SHALL include inline comments on the Update operation explaining that non-key computed fields are recomputed automatically from source property values set in the update model, and that individual source properties are also persisted to their own DynamoDB attributes when they have a `[DynamoDbAttribute]` mapping.
4. WHEN demonstrating computed field updates, THE Example_Project SHALL include an inline comment on the generated update model class stating that partition key, sort key, and extracted properties targeting key fields are excluded from generation and therefore unavailable for assignment in update expressions.
5. WHEN demonstrating computed field updates, THE Example_Project SHALL include an inline comment noting that setting only a subset of source properties for a computed field produces a diagnostic error (FDDB072), requiring all source properties to be assigned together.

### Requirement 9: Preserve Existing Functionality and Correctness

**User Story:** As a developer running the example projects, I want all examples to compile and function correctly after modernization, so that I can use them as reliable reference implementations.

#### Acceptance Criteria

1. WHEN the solution is built after modernization changes are applied, THEN THE Example_Project SHALL compile with zero errors across all example projects (TodoList, InvoiceManager, TransactionDemo, StoreLocator, JsonBlobDemo, EncryptionDemo, DynamicFieldsDemo, DynamicTableDemo, OperationSamples, S3BlobDemo).
2. THE Example_Project SHALL produce the same DynamoDB data shapes after modernization, where "same" means identical attribute names, identical key formats (partition key and sort key string values including prefix and separator characters), and identical prefix patterns (e.g., "CUSTOMER#", "INVOICE#", "ORDER#") for all Put and Update operations.
3. IF an example uses DynamoDB Local for testing, THEN THE Example_Project SHALL pass all existing xUnit tests in the Examples.Tests project against DynamoDB Local without modification to test assertions.
4. THE Example_Project SHALL retain all existing menu options (same option labels and numbering) and interactive functionality (same input prompts and output formatting) in each console application that provides a menu-driven interface.
5. IF a modernization change modifies an entity class definition, THEN THE Example_Project SHALL preserve the same DynamoDB attribute names specified in `[DynamoDbAttribute]` decorators and the same key prefix values specified in `[PartitionKey(Prefix = ...)]` and `[SortKey(Prefix = ...)]` decorators.
