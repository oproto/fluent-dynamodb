# Implementation Plan

- [x] 1. Create new attribute classes
  - Create GenerateEntityPropertyAttribute with Name, Generate, and Modifier properties
  - Create GenerateAccessorsAttribute with Operations, Generate, and Modifier properties (AllowMultiple = true)
  - Create AccessModifier enum (Public, Internal, Protected, Private)
  - Create DynamoDbOperation enum with flags (Get, Query, Scan, Put, Delete, Update, All)
  - Add IsDefault property to DynamoDbTableAttribute
  - Add XML documentation for all new attributes with usage examples
  - _Requirements: 2, 4, 5_

- [x] 2. Add diagnostic descriptors for validation errors
  - Create FDDB001: NoDefaultEntitySpecified diagnostic
  - Create FDDB002: MultipleDefaultEntities diagnostic
  - Create FDDB003: ConflictingAccessorConfiguration diagnostic
  - Create FDDB004: EmptyEntityPropertyName diagnostic
  - Include helpful error messages with table/entity names
  - _Requirements: 10_

- [x] 3. Enhance EntityModel to support new configuration
  - Add IsDefault property to EntityModel
  - Add EntityPropertyConfig class with Name, Generate, Modifier properties
  - Add AccessorConfig class with Operations, Generate, Modifier properties
  - Add EntityPropertyConfig property to EntityModel
  - Add List<AccessorConfig> property to EntityModel
  - _Requirements: 2, 4, 5_

- [x] 4. Update EntityAnalyzer to extract new attribute data
  - Extract IsDefault from [DynamoDbTable] attribute
  - Extract [GenerateEntityProperty] configuration (Name, Generate, Modifier)
  - Extract all [GenerateAccessors] configurations (Operations, Generate, Modifier)
  - Validate that [GenerateAccessors] attributes don't conflict (same operation configured twice)
  - Emit FDDB003 diagnostic for conflicting accessor configurations
  - Emit FDDB004 diagnostic for empty entity property names
  - _Requirements: 2, 4, 5, 10_

- [x] 5. Implement entity grouping by table name
  - Group analyzed entities by TableName in source generator
  - Create Dictionary<string, List<EntityModel>> structure
  - Pass grouped entities to table generation logic
  - _Requirements: 1_

- [x] 6. Implement default entity validation
  - Count entities marked with IsDefault = true per table
  - Emit FDDB001 diagnostic when multiple entities exist but no default specified
  - Emit FDDB002 diagnostic when multiple entities are marked as default
  - Allow single-entity tables to work without explicit IsDefault
  - _Requirements: 2, 10_

- [x] 7. Update table class name generation
  - Use table name (not entity name) as basis for table class name
  - Generate {TableName}Table as class name
  - Handle multiple entities sharing same table name
  - _Requirements: 1_

- [x] 8. Generate entity accessor properties
  - For each entity with Generate = true in EntityPropertyConfig, generate accessor property
  - Use custom Name if specified, otherwise pluralize entity class name (simple "add s" rule)
  - Apply visibility modifier from EntityPropertyConfig (public, internal, protected, private)
  - Generate property of type {EntityName}Accessor
  - Initialize accessor in table constructor
  - _Requirements: 3, 4_

- [x] 9. Generate nested entity accessor classes
  - Generate nested {EntityName}Accessor class for each entity
  - Add private readonly field for parent table reference
  - Add internal constructor accepting parent table
  - Generate operation methods based on AccessorConfig list
  - _Requirements: 3, 5_

- [x] 10. Generate operation methods in entity accessor classes
  - Parse AccessorConfig list to determine which operations to generate
  - Default to all operations public if no [GenerateAccessors] specified
  - Apply Generate = false to skip operation generation
  - Apply visibility modifier (public, internal, protected, private) to each operation
  - Generate Get, Query, Scan, Put, Delete, Update methods as configured
  - Each method returns appropriate RequestBuilder<TEntity> type
  - _Requirements: 5_

- [x] 11. Generate table-level operations for default entity
  - If default entity exists, generate table-level Get(), Query(), Scan(), Put(), Delete(), Update() methods
  - Table-level methods delegate to default entity's accessor methods
  - Use default entity type for generic type parameters
  - If no default entity (and multiple entities), don't generate table-level operations
  - _Requirements: 6_

- [x] 12. Generate transaction and batch operations at table level
  - Generate TransactWrite() method at table level only
  - Generate TransactGet() method at table level only
  - Generate BatchWrite() method at table level only
  - Generate BatchGet() method at table level only
  - Do NOT generate these methods on entity accessor classes
  - Methods should accept items of any entity type registered to table
  - _Requirements: 7_

- [x] 13. Add unit tests for attribute definitions
  - Test GenerateEntityPropertyAttribute properties
  - Test GenerateAccessorsAttribute properties and AllowMultiple
  - Test AccessModifier enum values
  - Test DynamoDbOperation enum flags
  - Test IsDefault property on DynamoDbTableAttribute
  - _Requirements: 2, 4, 5_

- [x] 14. Add unit tests for entity analysis
  - Test extraction of IsDefault from [DynamoDbTable]
  - Test extraction of [GenerateEntityProperty] configuration
  - Test extraction of multiple [GenerateAccessors] configurations
  - Test validation of conflicting accessor configurations
  - Test validation of empty entity property names
  - _Requirements: 2, 4, 5, 10_

- [x] 15. Add unit tests for entity grouping
  - Test grouping entities by table name
  - Test single entity creates one table
  - Test multiple entities with same table name create one table
  - Test entities with different table names create separate tables
  - _Requirements: 1_

- [x] 16. Add unit tests for default entity validation
  - Test single entity doesn't require IsDefault
  - Test multiple entities without default emits FDDB001
  - Test multiple entities with one default succeeds
  - Test multiple entities with multiple defaults emits FDDB002
  - _Requirements: 2, 10_

- [x] 17. Add unit tests for table class generation
  - Test table class name uses table name not entity name
  - Test table class contains entity accessor properties
  - Test entity accessor properties use custom names when specified
  - Test entity accessor properties respect Generate = false
  - Test entity accessor properties use correct visibility modifiers
  - _Requirements: 1, 3, 4_

- [x] 18. Add unit tests for entity accessor class generation
  - Test nested accessor class is generated for each entity
  - Test accessor class has correct name ({EntityName}Accessor)
  - Test accessor class has parent table field and constructor
  - Test accessor class contains operation methods
  - _Requirements: 3_

- [x] 19. Add unit tests for operation method generation
  - Test all operations generated by default
  - Test Generate = false skips operation
  - Test visibility modifiers applied correctly (public, internal, protected, private)
  - Test multiple [GenerateAccessors] attributes combine correctly
  - Test DynamoDbOperation flags expand correctly (All, Get | Query, etc.)
  - _Requirements: 5_

- [x] 20. Add unit tests for table-level operations
  - Test table-level operations generated when default entity exists
  - Test table-level operations use default entity type
  - Test table-level operations delegate to entity accessor
  - Test no table-level operations when no default entity
  - _Requirements: 6_

- [x] 21. Add unit tests for transaction operations
  - Test TransactWrite generated at table level
  - Test TransactGet generated at table level
  - Test BatchWrite generated at table level
  - Test BatchGet generated at table level
  - Test transaction methods NOT generated on entity accessors
  - _Requirements: 7_

- [ ] 22. Update existing unit tests to use new accessor pattern
  - Identify all tests that use table.Get(), table.Query(), etc.
  - Update to use table.EntityName.Get(), table.EntityName.Query() where appropriate
  - Update tests that expect one table per entity to expect consolidated tables
  - Update tests that reference table class names
  - Ensure all 1000+ tests pass with new generation model
  - _Requirements: 9_

- [ ] 23. Add integration tests for single-entity tables
  - Test single entity table generates correctly
  - Test operations work end-to-end
  - Test default entity behavior with single entity
  - _Requirements: 1, 2_

- [ ] 24. Add integration tests for multi-entity tables
  - Test multi-entity table with default generates correctly
  - Test entity accessor properties work end-to-end
  - Test table-level operations use default entity
  - Test entity-specific operations work correctly
  - _Requirements: 1, 2, 3, 6_

- [ ] 25. Add integration tests for custom configurations
  - Test custom entity property names
  - Test Generate = false for entity properties
  - Test visibility modifiers on entity properties
  - Test Generate = false for operations
  - Test visibility modifiers on operations
  - _Requirements: 4, 5, 8_

- [ ] 26. Add integration tests for transaction operations
  - Test TransactWrite with multiple entity types
  - Test TransactGet with multiple entity types
  - Test BatchWrite with multiple entity types
  - Test BatchGet with multiple entity types
  - _Requirements: 7_

- [ ] 27. Update documentation with single-entity examples
  - Show simple case with one entity per table
  - Show that IsDefault is not required for single entity
  - Show table-level operations work as before
  - _Requirements: 9_

- [ ] 28. Update documentation with multi-entity examples
  - Show multiple entities sharing same table name
  - Show IsDefault = true on default entity
  - Show entity accessor usage (table.Orders.Get())
  - Show table-level operations using default entity
  - _Requirements: 9_

- [ ] 29. Update documentation with customization examples
  - Show [GenerateEntityProperty] with custom name
  - Show [GenerateEntityProperty] with Generate = false
  - Show [GenerateEntityProperty] with visibility modifiers
  - Show [GenerateAccessors] with specific operations
  - Show [GenerateAccessors] with Generate = false
  - Show [GenerateAccessors] with visibility modifiers
  - Show partial class pattern for custom public methods calling internal generated methods
  - _Requirements: 9_

- [ ] 30. Update code examples in documentation
  - Update all examples showing table operations
  - Show both table-level and entity accessor patterns
  - Update examples to reflect new table class naming
  - _Requirements: 9_
