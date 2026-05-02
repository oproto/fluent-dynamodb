# Implementation Plan: encryption-demo-completion

## Overview

Complete the EncryptionDemo sample application to demonstrate real end-to-end field-level encryption using AWS KMS. Changes are limited to three files in `examples/EncryptionDemo/`: Program.cs, SecureRecord.cs, and README.md. No library code changes required.

## Tasks

- [x] 1. Update SecureRecord entity doc comments
  - [x] 1.1 Remove "pending completion" remarks from SecureRecord.cs
    - Remove the `<remarks>` blocks on `SocialSecurityNumber` and `CreditCardNumber` that say "AWS Encryption SDK integration is pending completion" and "demonstrates the intended API pattern"
    - Keep the `<summary>` XML doc comments intact — they already describe the correct behavior
    - _Requirements: 4.3_

- [x] 2. Update Program.cs startup and configuration
  - [x] 2.1 Remove pending-completion warning banner and AWS profile prompt
    - Remove the yellow "⚠ IMPORTANT: AWS Encryption SDK integration is pending completion" warning block
    - Replace with an updated banner indicating field encryption is fully functional
    - Remove the "Enter AWS Profile name" prompt and the `awsProfile` variable entirely
    - _Requirements: 4.1, 4.2, 1.5_

  - [x] 2.2 Register SecureRecordHydrator on DefaultEntityHydratorRegistry.Instance
    - Add `using Oproto.FluentDynamoDb.Hydration;` import
    - Before building `FluentDynamoDbOptions`, call `DefaultEntityHydratorRegistry.Instance.RegisterSecureRecordHydrator();`
    - This enables the async serialization path with encryption for Put and Get operations
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 2.3 Update CreateSecureRecordAsync to use deferred serialization Put
    - Change `await table.SecureRecords.PutAsync(record);` to `await table.Put<SecureRecord>().WithItem(record).PutAsync();`
    - This ensures the deferred async serialization path is used for encrypted entities
    - Update the console output text to reflect that encrypted fields are now actually encrypted in DynamoDB (not stored as plaintext)
    - _Requirements: 7.1, 7.2_

- [x] 3. Implement round-trip encryption demo menu option
  - [x] 3.1 Add "Round-Trip Encryption Demo" menu option
    - Add a new menu choice between "Show Logging Demo" and "Exit"
    - Wire it to a new `RunRoundTripDemoAsync` method
    - The option should check if encryption is configured; if not, display an info message and return early
    - Track whether encryption is configured with a `bool encryptionConfigured` variable set during startup
    - _Requirements: 3.1, 5.3_

  - [x] 3.2 Implement RunRoundTripDemoAsync method
    - Create a `SecureRecord` with hardcoded sample data: a deterministic ID (e.g., `"demo-round-trip-001"`), sample SSN (`"123-45-6789"`), sample credit card (`"4111-1111-1111-1111"`), label, email, and `CreatedAt`
    - Store via `table.Put<SecureRecord>().WithItem(record).PutAsync()`
    - Perform a direct `client.GetItemAsync(new GetItemRequest { TableName = TableName, Key = ... })` to retrieve raw attributes
    - Display raw attributes: show `pk`, `label`, `email`, `createdAt` as readable strings; show `ssn` and `creditCard` as Base64-encoded binary values
    - Retrieve the same record via `table.SecureRecords.Get(record.Id).GetItemAsync()` to demonstrate automatic decryption
    - Display decrypted values and confirm they match the originals
    - Delete the demo record via `table.SecureRecords.DeleteAsync(record.Id)`
    - Add `using Amazon.DynamoDBv2.Model;` for `GetItemRequest` and `AttributeValue` access
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [x] 4. Checkpoint
  - Ensure the code compiles and all existing menu options are preserved. Ask the user if questions arise.

- [x] 5. Update README.md documentation
  - [x] 5.1 Update README.md to reflect completed encryption
    - Remove the "⚠️ AWS Encryption SDK integration is pending completion" warning note
    - Update the Features Demonstrated section: change `[Encrypted]` description from "(implementation pending)" to indicate it is fully functional
    - Update the Prerequisites section: document that AWS credentials must be configured via environment variables (e.g., `AWS_PROFILE`, `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`), a KMS key ARN is needed for encryption features, and DynamoDB Local is required
    - Remove the "AWS Profile" bullet from the KMS Configuration section (only KMS Key ARN is prompted)
    - Update the Attribute Behavior table: change "Encrypted (pending)" to "Encrypted binary" for `[Encrypted]` and `[Encrypted] + [Sensitive]` rows
    - Add a "Round-Trip Encryption Demo" entry to the Menu Options section describing what it demonstrates
    - Retain existing DynamoDB Local setup instructions
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 4.4_

- [x] 6. Final checkpoint
  - Ensure all changes compile, the pending-completion warnings are fully removed from all three files, and the round-trip demo is wired into the menu. Ask the user if questions arise.

## Notes

- No test tasks are included — the design explicitly states PBT does not apply to this demo application
- Encryption pipeline correctness is already covered by existing library property tests from the encryption-pipeline-fix spec
- All changes are confined to `examples/EncryptionDemo/` — no library code modifications
- The only real AWS dependency is KMS; DynamoDB Local is used for storage
