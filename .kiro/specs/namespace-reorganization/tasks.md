# Implementation Plan

- [x] 1. Create new folder structure
  - Create Entities, Metadata, Hydration, Providers/Encryption, Providers/BlobStorage, Mapping, and Context folders
  - _Requirements: 1.1, 2.1, 3.1, 4.1, 5.1, 6.1, 7.1, 8.1_

- [x] 2. Move files to new locations (Phase 1 - File Moves Only)
  - [x] 2.1 Move entity contract files to Entities folder
    - Move IDynamoDbEntity.cs, IProjectionModel.cs, IDiscriminatedProjection.cs using `mv`
    - _Requirements: 2.1, 2.3_
  - [x] 2.2 Move metadata files to Metadata folder
    - Move EntityMetadata.cs, PropertyMetadata.cs, RelationshipMetadata.cs, IndexMetadata.cs, IEntityMetadataProvider.cs using `mv`
    - _Requirements: 3.1, 3.3_
  - [x] 2.3 Move hydration files to Hydration folder
    - Move IAsyncEntityHydrator.cs, IEntityHydratorRegistry.cs, DefaultEntityHydratorRegistry.cs using `mv`
    - _Requirements: 4.1, 4.3_
  - [x] 2.4 Move encryption provider files to Providers/Encryption folder
    - Move IFieldEncryptor.cs, FieldEncryptionContext.cs using `mv`
    - _Requirements: 5.1, 5.3_
  - [x] 2.5 Move blob storage provider files to Providers/BlobStorage folder
    - Move IBlobStorageProvider.cs, IJsonBlobSerializer.cs using `mv`
    - _Requirements: 6.1, 6.3_
  - [x] 2.6 Move mapping files to Mapping folder
    - Move MappingErrorHandler.cs, DynamoDbMappingException.cs, DiscriminatorMismatchException.cs, ProjectionValidationException.cs, FieldEncryptionException.cs using `mv`
    - _Requirements: 7.1, 7.3_
  - [x] 2.7 Move context files to Context folder
    - Move DynamoDbOperationContext.cs, DynamoDbOperationContextDiagnostics.cs, OperationContextData.cs using `mv`
    - _Requirements: 8.1, 8.3_

- [-] 3. Update namespace declarations in moved files (Phase 2 - Namespace Updates)
  - [x] 3.1 Update Entities namespace declarations
    - Change namespace from Oproto.FluentDynamoDb.Storage to Oproto.FluentDynamoDb.Entities in all Entities folder files
    - _Requirements: 2.2, 2.3_
  - [x] 3.2 Update Metadata namespace declarations
    - Change namespace from Oproto.FluentDynamoDb.Storage to Oproto.FluentDynamoDb.Metadata in all Metadata folder files
    - _Requirements: 3.2, 3.3_
  - [x] 3.3 Update Hydration namespace declarations
    - Change namespace from Oproto.FluentDynamoDb.Storage to Oproto.FluentDynamoDb.Hydration in all Hydration folder files
    - _Requirements: 4.2, 4.3_
  - [x] 3.4 Update Providers.Encryption namespace declarations
    - Change namespace from Oproto.FluentDynamoDb.Storage to Oproto.FluentDynamoDb.Providers.Encryption in all Providers/Encryption folder files
    - _Requirements: 5.2, 5.3_
  - [x] 3.5 Update Providers.BlobStorage namespace declarations
    - Change namespace from Oproto.FluentDynamoDb.Storage to Oproto.FluentDynamoDb.Providers.BlobStorage in all Providers/BlobStorage folder files
    - _Requirements: 6.2, 6.3_
  - [x] 3.6 Update Mapping namespace declarations
    - Change namespace from Oproto.FluentDynamoDb.Storage to Oproto.FluentDynamoDb.Mapping in all Mapping folder files
    - _Requirements: 7.2, 7.3_
  - [x] 3.7 Update Context namespace declarations
    - Change namespace from Oproto.FluentDynamoDb.Storage to Oproto.FluentDynamoDb.Context in all Context folder files
    - _Requirements: 8.2, 8.3_

- [x] 4. Update using directives in main library files
  - [x] 4.1 Update Storage folder files
    - Add using directives for new namespaces in DynamoDbTableBase.cs, DynamoDbIndex.cs, IDynamoDbTable.cs
    - _Requirements: 10.1_
  - [x] 4.2 Update Requests folder files
    - Add using directives for new namespaces in all request builder files
    - _Requirements: 10.1_
  - [x] 4.3 Update Expressions folder files
    - Add using directives for new namespaces in expression-related files
    - _Requirements: 10.1_
  - [x] 4.4 Update remaining main library files
    - Add using directives for new namespaces in FluentDynamoDbOptions.cs and other root files
    - _Requirements: 10.1_

- [x] 5. Update source generator to emit new namespaces
  - [x] 5.1 Update MapperGenerator.cs
    - Change emitted using directives from Oproto.FluentDynamoDb.Storage to new namespace structure
    - _Requirements: 9.1, 9.2, 9.3_
  - [x] 5.2 Update TableGenerator.cs
    - Change emitted using directives to include new namespaces
    - _Requirements: 9.1_
  - [x] 5.3 Update HydratorGenerator.cs
    - Change emitted using directives to use Oproto.FluentDynamoDb.Hydration
    - _Requirements: 9.1, 9.4_
  - [x] 5.4 Update remaining generator files
    - Update FieldsGenerator.cs, KeysGenerator.cs, UpdateExpressionsGenerator.cs, and other generators
    - _Requirements: 9.1_

- [x] 6. Checkpoint - Verify main library builds
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Update using directives in test projects
  - [x] 7.1 Update Oproto.FluentDynamoDb.UnitTests
    - Add using directives for new namespaces in all test files
    - _Requirements: 10.1, 10.3_
  - [x] 7.2 Update Oproto.FluentDynamoDb.SourceGenerator.UnitTests
    - Add using directives for new namespaces in source generator test files
    - _Requirements: 10.1, 10.3_
  - [x] 7.3 Update Oproto.FluentDynamoDb.IntegrationTests
    - Add using directives for new namespaces in integration test files
    - _Requirements: 10.1, 10.3_
  - [x] 7.4 Update remaining test projects
    - Update ApiConsistencyTests, AotTests, and extension package test projects
    - _Requirements: 10.1, 10.3_

- [x] 8. Update using directives in extension packages
  - [x] 8.1 Update Oproto.FluentDynamoDb.Geospatial
    - Add using directives for new namespaces
    - _Requirements: 10.1_
  - [x] 8.2 Update Oproto.FluentDynamoDb.Streams
    - Add using directives for new namespaces
    - _Requirements: 10.1_
  - [x] 8.3 Update Oproto.FluentDynamoDb.BlobStorage.S3
    - Add using directives for new namespaces (Providers.BlobStorage)
    - _Requirements: 10.1_
  - [x] 8.4 Update Oproto.FluentDynamoDb.Encryption.Kms
    - Add using directives for new namespaces (Providers.Encryption)
    - _Requirements: 10.1_
  - [x] 8.5 Update remaining extension packages
    - Update FluentResults, Logging.Extensions, NewtonsoftJson, SystemTextJson packages
    - _Requirements: 10.1_

- [x] 9. Update using directives in example projects
  - Update all example projects (InvoiceManager, StoreLocator, TodoList, TransactionDemo, OperationSamples) with new namespaces
  - _Requirements: 10.1_

- [x] 10. Checkpoint - Verify full solution builds and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Update CHANGELOG.md
  - Document namespace reorganization as a breaking change in the Unreleased section
  - _Requirements: 11.1_

- [x] 12. Update documentation files
  - [x] 12.1 Update docs/DOCUMENTATION_CHANGELOG.md
    - Add entry documenting the namespace changes
    - _Requirements: 11.2_
  - [x] 12.2 Update documentation with old namespace references
    - Search for and update any documentation files referencing Oproto.FluentDynamoDb.Storage for moved types
    - _Requirements: 11.3_

- [x] 13. Final Checkpoint - Verify everything passes
  - Ensure all tests pass, ask the user if questions arise.
