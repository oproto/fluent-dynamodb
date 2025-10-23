# Implementation Plan

- [x] 1. Add security attributes to Attributes assembly
  - Create SensitiveAttribute as marker attribute
  - Create EncryptedAttribute with CacheTtlSeconds property (default 300)
  - Note: External blob storage is handled by existing BlobReferenceAttribute
  - _Requirements: 1.5, 6.1, 6.2, 6.3_

- [x] 2. Implement core encryption interfaces in main library
  - [x] 2.1 Create IFieldEncryptor interface with EncryptAsync/DecryptAsync methods
    - Define FieldEncryptionContext class with ContextId and CacheTtlSeconds properties
    - _Requirements: 2.2, 2.3_
  
  - [x] 2.2 Create EncryptionContext ambient context class
    - Implement using AsyncLocal<string?> for thread-safe context flow
    - Add XML documentation explaining thread-safety guarantees
    - _Requirements: 3.6_
  
  - [x] 2.3 Add encryption context support to DynamoDbTableBase
    - Add protected GetEncryptionContext() method
    - Add protected IFieldEncryptor? property
    - _Requirements: 2.2, 3.2_

- [x] 3. Implement logging redaction
  - [x] 3.1 Create SensitiveDataRedactor utility class
    - Implement RedactSensitiveFields method for Dictionary<string, AttributeValue>
    - Use "[REDACTED]" placeholder for sensitive values
    - _Requirements: 1.1, 1.2, 1.4_
  
  - [x] 3.2 Integrate redaction into logging infrastructure
    - Update IDynamoDbLogger calls to redact sensitive fields before logging
    - Pass sensitive field metadata to logging methods
    - _Requirements: 1.3_

- [x] 4. Enhance source generator for security attributes
  - [x] 4.1 Create SecurityAttributeAnalyzer
    - Detect SensitiveAttribute on properties
    - Detect EncryptedAttribute on properties
    - Extract CacheTtlSeconds from EncryptedAttribute
    - _Requirements: 1.5, 4.1, 4.2_
  
  - [x] 4.2 Generate sensitive field metadata
    - Create static HashSet<string> of sensitive field names in metadata class
    - Generate IsSensitiveField(string) helper method
    - _Requirements: 1.1, 1.4_
  
  - [x] 4.3 Generate encryption code in ToItem mapper
    - Check if IFieldEncryptor is available
    - Call EncryptAsync for properties with EncryptedAttribute
    - Pass FieldEncryptionContext with CacheTtlSeconds from attribute
    - Store encrypted data as Binary (B) AttributeValue
    - _Requirements: 2.2, 2.5, 4.1_
  
  - [x] 4.4 Generate decryption code in FromItem mapper
    - Call DecryptAsync for properties with EncryptedAttribute
    - Handle Binary (B) AttributeValue type
    - Pass FieldEncryptionContext with CacheTtlSeconds from attribute
    - _Requirements: 2.3, 4.2_
  
  - [x] 4.5 Generate diagnostic for missing Encryption.Kms reference
    - Emit warning when EncryptedAttribute is used without Encryption.Kms package
    - _Requirements: 4.4_
  
  - [x] 4.6 Support combined Sensitive + Encrypted attributes
    - Apply both logging redaction and encryption when both attributes present
    - _Requirements: 4.5_

- [x] 5. Create Encryption.Kms assembly project
  - Create new project Oproto.FluentDynamoDb.Encryption.Kms
  - Add AWS.EncryptionSDK package reference (3.0.0+)
  - Add project reference to Oproto.FluentDynamoDb
  - Configure for .NET 8.0 target
  - _Requirements: 2.1, 8.3_

- [x] 6. Implement KMS key resolution
  - [x] 6.1 Create IKmsKeyResolver interface
    - Define ResolveKeyId(string? contextId) method
    - Add XML documentation with usage examples
    - _Requirements: 3.2_
  
  - [x] 6.2 Implement DefaultKmsKeyResolver
    - Accept defaultKeyId and optional contextKeyMap in constructor
    - Implement dictionary lookup with fallback to default
    - _Requirements: 3.3, 3.4_
  
  - [x] 6.3 Create AwsEncryptionSdkOptions configuration class
    - Add DefaultKeyId property
    - Add ContextKeyMap property
    - Add EnableCaching property (default true)
    - Add DefaultCacheTtlSeconds property
    - Add MaxMessagesPerDataKey property (default 100)
    - Add MaxBytesPerDataKey property (default 100MB)
    - Add Algorithm property (default AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384)
    - Add ExternalBlobBucket property
    - Add ExternalBlobKeyPrefix property
    - Add AutoExternalBlobThreshold property (default 350KB)
    - _Requirements: 3.4, 6.2, 8.5_

- [x] 7. Configure AWS Encryption SDK caching
  - [x] 7.1 Create CachingCryptoMaterialsManager factory method
    - Configure cache with TTL from options
    - Set MaxMessagesPerDataKey limit
    - Set MaxBytesPerDataKey limit
    - Cache key includes context ID for multi-context support
    - _Requirements: 3.5, 6.2_
  
  - [x] 7.2 Support disabling cache when EnableCaching is false
    - Use non-caching materials manager when caching disabled
    - _Requirements: 6.3_

- [x] 8. Implement AWS Encryption SDK field encryption
  - [x] 8.1 Create FieldEncryptionException class
    - Add FieldName, ContextId, and KeyId properties
    - Include constructors with inner exception support
    - _Requirements: 5.1, 5.2, 5.3_
  
  - [x] 8.2 Implement AwsEncryptionSdkFieldEncryptor class
    - Accept IKmsKeyResolver, AwsEncryptionSdkOptions, optional IBlobStorageProvider, and optional logger in constructor
    - Initialize AWS Encryption SDK (ESDK) instance
    - Setup CachingCryptoMaterialsManager if caching enabled
    - _Requirements: 2.1, 2.2, 2.3_
  
  - [x] 8.3 Implement EncryptAsync method
    - Resolve KMS key ARN via IKmsKeyResolver.ResolveKeyId
    - Create KMS keyring with resolved key
    - Build encryption context dictionary (field name, context ID, entity type)
    - Call ESDK.EncryptAsync with plaintext and encryption context
    - Return AWS Encryption SDK message format (binary)
    - Log encryption operation at Debug level
    - Handle errors with FieldEncryptionException
    - Note: External blob storage is handled by existing BlobReferenceAttribute infrastructure
    - _Requirements: 2.2, 2.4, 2.5, 5.1, 5.2, 5.4_
  
  - [x] 8.4 Implement DecryptAsync method
    - Resolve KMS key ARN for keyring
    - Create KMS keyring
    - Call ESDK.DecryptAsync with ciphertext (binary)
    - Validate encryption context matches expected values
    - Return plaintext
    - Log decryption operation at Debug level
    - Handle errors with FieldEncryptionException
    - Note: External blob storage is handled by existing BlobReferenceAttribute infrastructure
    - _Requirements: 2.3, 2.4, 5.3, 5.4_
  
  - [x] 8.5 Create helper method for building encryption context
    - Include field name as "field" key
    - Include context ID as "context" key (if provided)
    - Optionally include entity type
    - _Requirements: 2.4, 5.4, 7.4_

- [x] 9. Add request builder encryption extensions
  - Create EncryptionExtensions class with WithEncryptionContext method
  - Support all request builder types (Put, Get, Query, Update, etc.)
  - Store context in request builder state
  - _Requirements: 3.1_

- [x] 10. Write unit tests for attributes
  - [x] 10.1 Test SensitiveAttribute instantiation
    - Verify attribute can be instantiated
    - Verify attribute has correct AttributeUsage settings
    - _Requirements: 1.5_
  
  - [x] 10.2 Test EncryptedAttribute default values and customization
    - Verify default CacheTtlSeconds is 300
    - Test custom CacheTtlSeconds values
    - Verify attribute has correct AttributeUsage settings
    - _Requirements: 6.1, 6.2_

- [x] 11. Write unit tests for logging redaction
  - [x] 11.1 Test SensitiveDataRedactor with single sensitive field
    - Verify sensitive value replaced with [REDACTED]
    - Verify field name preserved
    - _Requirements: 1.1, 1.2, 1.4_
  
  - [x] 11.2 Test SensitiveDataRedactor with multiple sensitive fields
    - Verify all sensitive fields redacted
    - Verify non-sensitive fields unchanged
    - _Requirements: 1.1, 1.2_
  
  - [x] 11.3 Test SensitiveDataRedactor with empty/null items
    - Verify graceful handling of edge cases
    - _Requirements: 1.1_

- [x] 12. Write unit tests for source generator
  - [x] 12.1 Test sensitive field metadata generation
    - Verify HashSet generation
    - Verify IsSensitiveField method
    - _Requirements: 1.1, 1.4_
  
  - [x] 12.2 Test encryption code generation in ToItem
    - Verify EncryptAsync calls generated
    - Verify FieldEncryptionContext passed correctly
    - Verify Binary AttributeValue storage
    - _Requirements: 2.2, 2.5, 4.1_
  
  - [x] 12.3 Test decryption code generation in FromItem
    - Verify DecryptAsync calls generated
    - Verify Binary AttributeValue reading
    - _Requirements: 2.3, 4.2_
  
  - [x] 12.4 Test diagnostic for missing Encryption.Kms reference
    - Verify warning emitted when EncryptedAttribute used without package
    - _Requirements: 4.4_
  
  - [x] 12.5 Test combined Sensitive + Encrypted attributes
    - Verify both features applied
    - _Requirements: 4.5_

- [x] 13. Write unit tests for encryption components
  - [x] 13.1 Test DefaultKmsKeyResolver
    - Test default key resolution
    - Test context-specific key resolution
    - Test fallback when context not found
    - _Requirements: 3.2, 3.3, 3.4_
  
  - [x] 13.2 Test AwsEncryptionSdkFieldEncryptor with mocked ESDK
    - Mock AWS Encryption SDK encrypt/decrypt operations
    - Test encryption context building
    - Test error handling for each failure mode
    - Test caching configuration
    - Verify AWS Encryption SDK message format
    - _Requirements: 2.2, 2.3, 2.4, 5.1, 5.2, 5.3_
  
  - [x] 13.3 Test FieldEncryptionException
    - Verify properties set correctly
    - Test with and without inner exception
    - _Requirements: 5.1, 5.2, 5.3_
  
  - [x] 13.4 Test AwsEncryptionSdkOptions
    - Verify default values
    - Test custom configuration
    - _Requirements: 6.1, 6.2_

- [ ] 14. Write integration tests
  - [ ] 14.1 Test end-to-end encryption with real DynamoDB Local
    - Create entity with encrypted fields
    - Put to DynamoDB
    - Verify encrypted format in DynamoDB (Binary type, AWS Encryption SDK format)
    - Get from DynamoDB
    - Verify decrypted values match original
    - Verify encryption context is preserved
    - _Requirements: 2.2, 2.3, 2.5_
  
  - [ ] 14.2 Test multi-context encryption
    - Encrypt data for context A
    - Encrypt data for context B
    - Verify different keys used
    - Verify data encrypted with context A cannot be decrypted with context B
    - _Requirements: 3.1, 3.2, 3.5_
  
  - [ ] 14.3 Test logging redaction integration
    - Enable logging
    - Perform operations on entities with sensitive fields
    - Verify log output contains [REDACTED] for sensitive fields
    - Verify non-sensitive fields logged normally
    - _Requirements: 1.1, 1.2, 1.3_
  
  - [ ] 14.4 Test combined security features
    - Entity with both Sensitive and Encrypted attributes
    - Verify encryption happens
    - Verify logging redaction happens
    - Verify decryption works
    - _Requirements: 4.5_
  
  - [ ] 14.5 Test ambient context flow
    - Set EncryptionContext.Current
    - Perform multiple operations
    - Verify context flows through async calls
    - Verify context isolation between async flows
    - _Requirements: 3.6_
  
  - [ ] 14.6 Test encrypted field with BlobReferenceAttribute
    - Create entity with both [Encrypted] and [BlobReference] attributes
    - Put to DynamoDB
    - Verify blob reference stored in DynamoDB
    - Verify encrypted data stored via IBlobStorageProvider
    - Get from DynamoDB
    - Verify transparent decryption from blob storage
    - Note: Tests integration with existing BlobReferenceAttribute infrastructure
    - _Requirements: 8.1, 8.2, 8.6_
