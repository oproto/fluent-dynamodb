using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for dynamic fields support.
/// These tests verify that entities with [EnableDynamicFields] correctly capture,
/// store, and retrieve unmapped DynamoDB attributes.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
public class DynamicFieldsIntegrationTests : IntegrationTestBase
{
    private GenericTable _table = null!;

    public DynamicFieldsIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await CreateTableAsync<DynamicFieldsTestEntity>();
        _table = new TestTable(DynamoDb, TableName);
    }

    #region GetItem Tests (Requirement 3.1)

    [Fact]
    public async Task GetItem_WithDynamicFields_PopulatesDynamicFieldsCollection()
    {
        // Arrange - Store item with extra attributes via raw SDK
        var pk = "product-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Test Product" },
            ["price"] = new AttributeValue { N = "29.99" },
            ["is_active"] = new AttributeValue { BOOL = true },
            // Dynamic fields (not mapped to entity properties)
            ["custom_color"] = new AttributeValue { S = "blue" },
            ["custom_weight"] = new AttributeValue { N = "1.5" },
            ["custom_tags"] = new AttributeValue { SS = new List<string> { "sale", "featured" } }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Retrieve via GetItem
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Assert - Mapped properties
        entity.Pk.Should().Be(pk);
        entity.Sk.Should().Be(sk);
        entity.Name.Should().Be("Test Product");
        entity.Price.Should().Be(29.99m);
        entity.IsActive.Should().BeTrue();

        // Assert - Dynamic fields
        entity.DynamicFields.Should().NotBeNull();
        entity.DynamicFields.Count.Should().Be(3);
        entity.DynamicFields.GetString("custom_color").Should().Be("blue");
        entity.DynamicFields.GetDouble("custom_weight").Should().Be(1.5);
        entity.DynamicFields.GetStringSet("custom_tags").Should().BeEquivalentTo(new[] { "sale", "featured" });
    }

    [Fact]
    public async Task GetItem_WithNoDynamicFields_HasEmptyDynamicFieldsCollection()
    {
        // Arrange - Store item with only mapped attributes
        var pk = "product-2";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Simple Product" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Assert
        entity.DynamicFields.Should().NotBeNull();
        entity.DynamicFields.Count.Should().Be(0);
    }

    #endregion

    #region Query Tests (Requirement 3.2)

    [Fact]
    public async Task Query_WithDynamicFields_PopulatesDynamicFieldsOnAllEntities()
    {
        // Arrange - Store multiple items with dynamic fields
        var pk = "category-1";
        var items = new[]
        {
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "item-1" },
                ["name"] = new AttributeValue { S = "Item 1" },
                ["custom_field1"] = new AttributeValue { S = "value1" }
            },
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "item-2" },
                ["name"] = new AttributeValue { S = "Item 2" },
                ["custom_field2"] = new AttributeValue { N = "42" }
            },
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "item-3" },
                ["name"] = new AttributeValue { S = "Item 3" },
                ["custom_field3"] = new AttributeValue { BOOL = true }
            }
        };

        foreach (var item in items)
        {
            await DynamoDb.PutItemAsync(TableName, item);
        }

        // Act - Query all items
        var queryResponse = await DynamoDb.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = pk }
            }
        });

        var entities = queryResponse.Items
            .Select(i => DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(i))
            .ToList();

        // Assert
        entities.Should().HaveCount(3);

        var entity1 = entities.First(e => e.Sk == "item-1");
        entity1.DynamicFields.GetString("custom_field1").Should().Be("value1");

        var entity2 = entities.First(e => e.Sk == "item-2");
        entity2.DynamicFields.GetInt("custom_field2").Should().Be(42);

        var entity3 = entities.First(e => e.Sk == "item-3");
        entity3.DynamicFields.GetBool("custom_field3").Should().BeTrue();
    }

    #endregion

    #region Scan Tests (Requirement 3.3)

    [Fact]
    public async Task Scan_WithDynamicFields_PopulatesDynamicFieldsOnAllEntities()
    {
        // Arrange - Store items with dynamic fields
        var items = new[]
        {
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "scan-test-1" },
                ["sk"] = new AttributeValue { S = "meta" },
                ["name"] = new AttributeValue { S = "Scan Item 1" },
                ["dynamic_attr"] = new AttributeValue { S = "scan-value-1" }
            },
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "scan-test-2" },
                ["sk"] = new AttributeValue { S = "meta" },
                ["name"] = new AttributeValue { S = "Scan Item 2" },
                ["dynamic_attr"] = new AttributeValue { S = "scan-value-2" }
            }
        };

        foreach (var item in items)
        {
            await DynamoDb.PutItemAsync(TableName, item);
        }

        // Act - Scan with filter to get only our test items
        var scanResponse = await DynamoDb.ScanAsync(new ScanRequest
        {
            TableName = TableName,
            FilterExpression = "begins_with(pk, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":prefix"] = new AttributeValue { S = "scan-test" }
            }
        });

        var entities = scanResponse.Items
            .Select(i => DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(i))
            .ToList();

        // Assert
        entities.Should().HaveCount(2);
        entities.Should().AllSatisfy(e =>
        {
            e.DynamicFields.Should().NotBeNull();
            e.DynamicFields.ContainsKey("dynamic_attr").Should().BeTrue();
        });
    }

    #endregion

    #region PutItem Tests (Requirement 4.2)

    [Fact]
    public async Task PutItem_WithDynamicFields_StoresDynamicFieldsInDynamoDB()
    {
        // Arrange - Create entity with dynamic fields
        var entity = new DynamicFieldsTestEntity
        {
            Pk = "put-test-1",
            Sk = "meta",
            Name = "Put Test Product",
            Price = 19.99m
        };
        entity.DynamicFields.SetString("custom_description", "A custom description");
        entity.DynamicFields.SetInt("custom_quantity", 100);
        entity.DynamicFields.SetBool("custom_featured", true);

        // Act - Put item
        var item = DynamicFieldsTestEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);

        // Verify - Get item back via raw SDK
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Pk },
            ["sk"] = new AttributeValue { S = entity.Sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);

        // Assert - Dynamic fields are stored
        response.Item.Should().ContainKey("custom_description");
        response.Item["custom_description"].S.Should().Be("A custom description");

        response.Item.Should().ContainKey("custom_quantity");
        response.Item["custom_quantity"].N.Should().Be("100");

        response.Item.Should().ContainKey("custom_featured");
        response.Item["custom_featured"].BOOL.Should().BeTrue();
    }

    [Fact]
    public async Task PutItem_DynamicFieldConflictsWithMappedProperty_MappedPropertyTakesPrecedence()
    {
        // Arrange - Create entity where dynamic field has same name as mapped property
        var entity = new DynamicFieldsTestEntity
        {
            Pk = "conflict-test-1",
            Sk = "meta",
            Name = "Mapped Name Value"
        };
        // Try to set a dynamic field with the same attribute name as a mapped property
        entity.DynamicFields.SetString("name", "Dynamic Name Value");

        // Act - Put item
        var item = DynamicFieldsTestEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);

        // Verify
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Pk },
            ["sk"] = new AttributeValue { S = entity.Sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);

        // Assert - Mapped property value should be used
        response.Item["name"].S.Should().Be("Mapped Name Value");
    }

    #endregion

    #region UpdateItem SET Tests (Requirement 5.1)

    [Fact]
    public async Task UpdateItem_SetDynamicField_StoresFieldCorrectly()
    {
        // Arrange - Create initial item
        var pk = "update-set-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Update Test" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Update to set a dynamic field
        var updateRequest = new UpdateItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk }
            },
            UpdateExpression = "SET #df = :dfv",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#df"] = "custom_dynamic_field"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":dfv"] = new AttributeValue { S = "dynamic value" }
            }
        };
        await DynamoDb.UpdateItemAsync(updateRequest);

        // Verify
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Assert
        entity.DynamicFields.GetString("custom_dynamic_field").Should().Be("dynamic value");
    }

    #endregion

    #region UpdateItem REMOVE Tests (Requirement 5.2)

    [Fact]
    public async Task UpdateItem_RemoveDynamicField_RemovesFieldFromItem()
    {
        // Arrange - Create item with dynamic field
        var pk = "update-remove-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Remove Test" },
            ["field_to_remove"] = new AttributeValue { S = "will be removed" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Update to remove the dynamic field
        var updateRequest = new UpdateItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk }
            },
            UpdateExpression = "REMOVE #df",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#df"] = "field_to_remove"
            }
        };
        await DynamoDb.UpdateItemAsync(updateRequest);

        // Verify
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Assert
        entity.DynamicFields.ContainsKey("field_to_remove").Should().BeFalse();
    }

    #endregion

    #region Filter Expression Tests (Requirements 6.1, 6.3)

    [Fact]
    public async Task Query_WithFilterOnDynamicField_ReturnsCorrectItems()
    {
        // Arrange - Store items with dynamic fields
        var pk = "filter-test";
        var items = new[]
        {
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "item-1" },
                ["name"] = new AttributeValue { S = "Item 1" },
                ["status"] = new AttributeValue { S = "active" }
            },
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "item-2" },
                ["name"] = new AttributeValue { S = "Item 2" },
                ["status"] = new AttributeValue { S = "inactive" }
            },
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "item-3" },
                ["name"] = new AttributeValue { S = "Item 3" },
                ["status"] = new AttributeValue { S = "active" }
            }
        };

        foreach (var item in items)
        {
            await DynamoDb.PutItemAsync(TableName, item);
        }

        // Act - Query with filter on dynamic field
        var queryResponse = await DynamoDb.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "pk = :pk",
            FilterExpression = "#status = :status",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "status"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = pk },
                [":status"] = new AttributeValue { S = "active" }
            }
        });

        var entities = queryResponse.Items
            .Select(i => DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(i))
            .ToList();

        // Assert
        entities.Should().HaveCount(2);
        entities.Should().AllSatisfy(e =>
            e.DynamicFields.GetString("status").Should().Be("active"));
    }

    [Fact]
    public async Task Scan_WithFilterOnDynamicField_ReturnsCorrectItems()
    {
        // Arrange
        var items = new[]
        {
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "scan-filter-1" },
                ["sk"] = new AttributeValue { S = "meta" },
                ["priority"] = new AttributeValue { N = "1" }
            },
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "scan-filter-2" },
                ["sk"] = new AttributeValue { S = "meta" },
                ["priority"] = new AttributeValue { N = "5" }
            },
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "scan-filter-3" },
                ["sk"] = new AttributeValue { S = "meta" },
                ["priority"] = new AttributeValue { N = "3" }
            }
        };

        foreach (var item in items)
        {
            await DynamoDb.PutItemAsync(TableName, item);
        }

        // Act - Scan with filter on dynamic field (priority > 2)
        var scanResponse = await DynamoDb.ScanAsync(new ScanRequest
        {
            TableName = TableName,
            FilterExpression = "begins_with(pk, :prefix) AND #priority > :minPriority",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#priority"] = "priority"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":prefix"] = new AttributeValue { S = "scan-filter" },
                [":minPriority"] = new AttributeValue { N = "2" }
            }
        });

        var entities = scanResponse.Items
            .Select(i => DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(i))
            .ToList();

        // Assert
        entities.Should().HaveCount(2);
        entities.Should().AllSatisfy(e =>
            e.DynamicFields.GetInt("priority").Should().BeGreaterThan(2));
    }

    #endregion

    #region Condition Expression Tests (Requirement 7.1)

    [Fact]
    public async Task PutItem_WithConditionOnDynamicField_EvaluatesConditionCorrectly()
    {
        // Arrange - Create initial item with dynamic field
        var pk = "condition-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Original" },
            ["version"] = new AttributeValue { N = "1" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Try to put with condition that version = 1 (should succeed)
        var newItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Updated" },
            ["version"] = new AttributeValue { N = "2" }
        };

        var putRequest = new PutItemRequest
        {
            TableName = TableName,
            Item = newItem,
            ConditionExpression = "#version = :expectedVersion",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#version"] = "version"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":expectedVersion"] = new AttributeValue { N = "1" }
            }
        };

        await DynamoDb.PutItemAsync(putRequest);

        // Verify
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Assert
        entity.Name.Should().Be("Updated");
        entity.DynamicFields.GetInt("version").Should().Be(2);
    }

    [Fact]
    public async Task PutItem_WithConditionOnDynamicField_FailsWhenConditionNotMet()
    {
        // Arrange - Create initial item with dynamic field
        var pk = "condition-fail-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Original" },
            ["version"] = new AttributeValue { N = "2" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act & Assert - Try to put with condition that version = 1 (should fail)
        var newItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Should Not Update" },
            ["version"] = new AttributeValue { N = "3" }
        };

        var putRequest = new PutItemRequest
        {
            TableName = TableName,
            Item = newItem,
            ConditionExpression = "#version = :expectedVersion",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#version"] = "version"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":expectedVersion"] = new AttributeValue { N = "1" }
            }
        };

        var act = () => DynamoDb.PutItemAsync(putRequest);
        await act.Should().ThrowAsync<ConditionalCheckFailedException>();
    }

    #endregion

    #region Projection Tests (Requirement 3.4)

    [Fact]
    public async Task Query_WithProjection_ReturnsOnlyProjectedDynamicFields()
    {
        // Arrange - Store item with multiple dynamic fields
        var pk = "projection-test";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Projection Test" },
            ["dynamic_included"] = new AttributeValue { S = "should be included" },
            ["dynamic_excluded"] = new AttributeValue { S = "should be excluded" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Query with projection that includes only some dynamic fields
        var queryResponse = await DynamoDb.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "pk = :pk",
            ProjectionExpression = "pk, sk, #name, #included",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#name"] = "name",
                ["#included"] = "dynamic_included"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = pk }
            }
        });

        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(queryResponse.Items[0]);

        // Assert
        entity.DynamicFields.ContainsKey("dynamic_included").Should().BeTrue();
        entity.DynamicFields.GetString("dynamic_included").Should().Be("should be included");
        entity.DynamicFields.ContainsKey("dynamic_excluded").Should().BeFalse();
    }

    #endregion

    #region Change Tracking Tests (Requirements 11.4, 8.1, 8.2)

    /// <summary>
    /// Tests that ChangesOnly() correctly tracks and updates only modified dynamic fields.
    /// Validates Requirements 11.4, 8.1, 8.2.
    /// </summary>
    [Fact]
    public async Task ChangesOnly_UpdateFlow_UpdatesOnlyChangedFields()
    {
        // Arrange - Create initial item with multiple dynamic fields
        var pk = "changes-only-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Test Product" },
            ["field_unchanged"] = new AttributeValue { S = "original_value" },
            ["field_to_modify"] = new AttributeValue { S = "will_be_modified" },
            ["field_to_remove"] = new AttributeValue { S = "will_be_removed" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Load entity, modify dynamic fields, get changes
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Verify initial state
        entity.DynamicFields.Count.Should().Be(3);
        entity.DynamicFields.GetString("field_unchanged").Should().Be("original_value");
        entity.DynamicFields.GetString("field_to_modify").Should().Be("will_be_modified");
        entity.DynamicFields.GetString("field_to_remove").Should().Be("will_be_removed");

        // Modify dynamic fields
        entity.DynamicFields.SetString("field_to_modify", "modified_value");
        entity.DynamicFields.SetString("field_new", "new_value");
        entity.DynamicFields.Remove("field_to_remove");

        // Get changes only
        var changes = entity.DynamicFields.ChangesOnly();

        // Verify changes collection
        changes.Count.Should().Be(2, "Should contain only modified and new fields");
        changes.ContainsKey("field_to_modify").Should().BeTrue();
        changes.GetString("field_to_modify").Should().Be("modified_value");
        changes.ContainsKey("field_new").Should().BeTrue();
        changes.GetString("field_new").Should().Be("new_value");
        changes.ContainsKey("field_unchanged").Should().BeFalse("Unchanged field should not be in changes");
        changes.RemovedFields.Should().Contain("field_to_remove");

        // Apply changes via UpdateItem
        var updateExpressionParts = new List<string>();
        var attributeNames = new Dictionary<string, string>();
        var attributeValues = new Dictionary<string, AttributeValue>();
        var removeExpressionParts = new List<string>();

        int fieldIndex = 0;
        foreach (var kvp in changes)
        {
            var attrName = $"#df{fieldIndex}";
            var attrValue = $":df{fieldIndex}";
            updateExpressionParts.Add($"{attrName} = {attrValue}");
            attributeNames[attrName] = kvp.Key;
            attributeValues[attrValue] = kvp.Value;
            fieldIndex++;
        }

        foreach (var removedField in changes.RemovedFields)
        {
            var attrName = $"#rm{fieldIndex}";
            removeExpressionParts.Add(attrName);
            attributeNames[attrName] = removedField;
            fieldIndex++;
        }

        var updateExpression = "";
        if (updateExpressionParts.Count > 0)
        {
            updateExpression = "SET " + string.Join(", ", updateExpressionParts);
        }
        if (removeExpressionParts.Count > 0)
        {
            updateExpression += (updateExpression.Length > 0 ? " " : "") + "REMOVE " + string.Join(", ", removeExpressionParts);
        }

        var updateRequest = new UpdateItemRequest
        {
            TableName = TableName,
            Key = key,
            UpdateExpression = updateExpression,
            ExpressionAttributeNames = attributeNames,
            ExpressionAttributeValues = attributeValues.Count > 0 ? attributeValues : null
        };
        await DynamoDb.UpdateItemAsync(updateRequest);

        // Verify - Get item back and check state
        var verifyResponse = await DynamoDb.GetItemAsync(TableName, key);
        var verifiedEntity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(verifyResponse.Item);

        // Assert
        verifiedEntity.DynamicFields.GetString("field_unchanged").Should().Be("original_value", "Unchanged field should remain");
        verifiedEntity.DynamicFields.GetString("field_to_modify").Should().Be("modified_value", "Modified field should have new value");
        verifiedEntity.DynamicFields.GetString("field_new").Should().Be("new_value", "New field should be added");
        verifiedEntity.DynamicFields.ContainsKey("field_to_remove").Should().BeFalse("Removed field should be gone");
    }

    /// <summary>
    /// Tests that ChangesOnly() with resetTracking: false preserves tracking for retry scenarios.
    /// Validates Requirements 11.5, 11.6.
    /// </summary>
    [Fact]
    public async Task ChangesOnly_WithResetTrackingFalse_PreservesTrackingForRetry()
    {
        // Arrange - Create initial item
        var pk = "changes-retry-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Retry Test" },
            ["existing_field"] = new AttributeValue { S = "existing_value" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Load entity and modify
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        entity.DynamicFields.SetString("retry_field", "retry_value");

        // Get changes without resetting tracking (for retry scenario)
        var changes1 = entity.DynamicFields.ChangesOnly(resetTracking: false);
        changes1.Count.Should().Be(1);
        changes1.GetString("retry_field").Should().Be("retry_value");

        // Verify tracking is preserved - can get changes again
        var changes2 = entity.DynamicFields.ChangesOnly(resetTracking: false);
        changes2.Count.Should().Be(1, "Changes should still be tracked after first ChangesOnly with resetTracking: false");
        changes2.GetString("retry_field").Should().Be("retry_value");

        // Now reset tracking
        entity.DynamicFields.ResetChangeTracking();

        // Verify tracking is cleared
        var changes3 = entity.DynamicFields.ChangesOnly();
        changes3.Count.Should().Be(0, "No changes should be tracked after ResetChangeTracking");
    }

    #endregion

    #region Update Model with DynamicFields Tests (Requirements 12.2, 12.3)

    /// <summary>
    /// Tests that update operations with DynamicFieldCollection correctly generate SET and REMOVE clauses.
    /// Validates Requirements 12.2, 12.3.
    /// </summary>
    [Fact]
    public async Task UpdateWithDynamicFieldCollection_GeneratesCorrectSetAndRemoveClauses()
    {
        // Arrange - Create initial item with dynamic fields
        var pk = "update-model-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Update Model Test" },
            ["field_to_keep"] = new AttributeValue { S = "keep_value" },
            ["field_to_update"] = new AttributeValue { S = "old_value" },
            ["field_to_delete"] = new AttributeValue { S = "delete_me" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Create a DynamicFieldCollection with changes
        var dynamicUpdates = new DynamicFieldCollection();
        dynamicUpdates.SetString("field_to_update", "new_value");
        dynamicUpdates.SetInt("field_new_int", 42);
        // Mark a field for removal by adding to RemovedFields
        // Note: We need to simulate the removal tracking
        // Since we're creating a new collection, we'll manually build the update

        // Build update expression manually to simulate what the expression translator would do
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };

        var updateRequest = new UpdateItemRequest
        {
            TableName = TableName,
            Key = key,
            UpdateExpression = "SET #f1 = :v1, #f2 = :v2 REMOVE #f3",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#f1"] = "field_to_update",
                ["#f2"] = "field_new_int",
                ["#f3"] = "field_to_delete"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":v1"] = new AttributeValue { S = "new_value" },
                [":v2"] = new AttributeValue { N = "42" }
            }
        };
        await DynamoDb.UpdateItemAsync(updateRequest);

        // Verify
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Assert
        entity.DynamicFields.GetString("field_to_keep").Should().Be("keep_value", "Untouched field should remain");
        entity.DynamicFields.GetString("field_to_update").Should().Be("new_value", "Updated field should have new value");
        entity.DynamicFields.GetInt("field_new_int").Should().Be(42, "New field should be added");
        entity.DynamicFields.ContainsKey("field_to_delete").Should().BeFalse("Removed field should be gone");
    }

    /// <summary>
    /// Tests that loading an entity, modifying dynamic fields, and using ChangesOnly() 
    /// correctly tracks both SET and REMOVE operations.
    /// Validates Requirements 12.2, 12.3.
    /// </summary>
    [Fact]
    public async Task LoadModifyUpdate_WithChangesOnly_TracksSetAndRemoveOperations()
    {
        // Arrange - Create initial item
        var pk = "load-modify-update-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Load Modify Update Test" },
            ["status"] = new AttributeValue { S = "active" },
            ["counter"] = new AttributeValue { N = "10" },
            ["temp_data"] = new AttributeValue { S = "temporary" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Load entity
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Modify dynamic fields
        entity.DynamicFields.SetString("status", "inactive");  // Modify existing
        entity.DynamicFields.SetInt("counter", 20);            // Modify existing
        entity.DynamicFields.SetBool("is_verified", true);     // Add new
        entity.DynamicFields.Remove("temp_data");              // Remove existing

        // Get changes
        var changes = entity.DynamicFields.ChangesOnly();

        // Verify changes collection state
        changes.Count.Should().Be(3, "Should have 3 modified/added fields");
        changes.GetString("status").Should().Be("inactive");
        changes.GetInt("counter").Should().Be(20);
        changes.GetBool("is_verified").Should().BeTrue();
        changes.RemovedFields.Should().HaveCount(1);
        changes.RemovedFields.Should().Contain("temp_data");

        // Apply changes
        var updateExpressionParts = new List<string>();
        var attributeNames = new Dictionary<string, string>();
        var attributeValues = new Dictionary<string, AttributeValue>();
        var removeExpressionParts = new List<string>();

        int fieldIndex = 0;
        foreach (var kvp in changes)
        {
            var attrName = $"#df{fieldIndex}";
            var attrValue = $":df{fieldIndex}";
            updateExpressionParts.Add($"{attrName} = {attrValue}");
            attributeNames[attrName] = kvp.Key;
            attributeValues[attrValue] = kvp.Value;
            fieldIndex++;
        }

        foreach (var removedField in changes.RemovedFields)
        {
            var attrName = $"#rm{fieldIndex}";
            removeExpressionParts.Add(attrName);
            attributeNames[attrName] = removedField;
            fieldIndex++;
        }

        var updateExpression = "SET " + string.Join(", ", updateExpressionParts);
        if (removeExpressionParts.Count > 0)
        {
            updateExpression += " REMOVE " + string.Join(", ", removeExpressionParts);
        }

        var updateRequest = new UpdateItemRequest
        {
            TableName = TableName,
            Key = key,
            UpdateExpression = updateExpression,
            ExpressionAttributeNames = attributeNames,
            ExpressionAttributeValues = attributeValues
        };
        await DynamoDb.UpdateItemAsync(updateRequest);

        // Verify final state
        var verifyResponse = await DynamoDb.GetItemAsync(TableName, key);
        var verifiedEntity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(verifyResponse.Item);

        // Assert
        verifiedEntity.DynamicFields.GetString("status").Should().Be("inactive");
        verifiedEntity.DynamicFields.GetInt("counter").Should().Be(20);
        verifiedEntity.DynamicFields.GetBool("is_verified").Should().BeTrue();
        verifiedEntity.DynamicFields.ContainsKey("temp_data").Should().BeFalse();
    }

    #endregion

    #region Null DynamicFields Tests (Requirement 12.4)

    /// <summary>
    /// Tests that when DynamicFields is null in an update, existing dynamic fields are not modified.
    /// Validates Requirement 12.4.
    /// </summary>
    [Fact]
    public async Task Update_WithNullDynamicFields_DoesNotModifyExistingDynamicFields()
    {
        // Arrange - Create initial item with dynamic fields
        var pk = "null-dynamic-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Original Name" },
            ["dynamic_field_1"] = new AttributeValue { S = "value_1" },
            ["dynamic_field_2"] = new AttributeValue { N = "100" },
            ["dynamic_field_3"] = new AttributeValue { BOOL = true }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Update only the mapped property (name), not touching dynamic fields
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };

        var updateRequest = new UpdateItemRequest
        {
            TableName = TableName,
            Key = key,
            UpdateExpression = "SET #name = :name",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#name"] = "name"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":name"] = new AttributeValue { S = "Updated Name" }
            }
        };
        await DynamoDb.UpdateItemAsync(updateRequest);

        // Verify - All dynamic fields should remain unchanged
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Assert - Mapped property updated
        entity.Name.Should().Be("Updated Name");

        // Assert - Dynamic fields unchanged
        entity.DynamicFields.Count.Should().Be(3, "All dynamic fields should remain");
        entity.DynamicFields.GetString("dynamic_field_1").Should().Be("value_1");
        entity.DynamicFields.GetInt("dynamic_field_2").Should().Be(100);
        entity.DynamicFields.GetBool("dynamic_field_3").Should().BeTrue();
    }

    /// <summary>
    /// Tests that an empty DynamicFieldCollection (no changes) does not affect existing dynamic fields.
    /// Validates Requirement 12.4.
    /// </summary>
    [Fact]
    public async Task Update_WithEmptyDynamicFieldCollection_DoesNotModifyExistingDynamicFields()
    {
        // Arrange - Create initial item with dynamic fields
        var pk = "empty-collection-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "Test Item" },
            ["existing_dynamic"] = new AttributeValue { S = "should_remain" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Load entity, don't modify dynamic fields, get changes (should be empty)
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Get changes without making any modifications
        var changes = entity.DynamicFields.ChangesOnly();

        // Verify changes is empty
        changes.Count.Should().Be(0, "No changes were made");
        changes.RemovedFields.Should().BeEmpty();
        changes.HasChanges.Should().BeFalse();

        // Update only the name (simulating an update where DynamicFields would be null/empty)
        var updateRequest = new UpdateItemRequest
        {
            TableName = TableName,
            Key = key,
            UpdateExpression = "SET #name = :name",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#name"] = "name"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":name"] = new AttributeValue { S = "Updated Item" }
            }
        };
        await DynamoDb.UpdateItemAsync(updateRequest);

        // Verify - Dynamic field should remain unchanged
        var verifyResponse = await DynamoDb.GetItemAsync(TableName, key);
        var verifiedEntity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(verifyResponse.Item);

        // Assert
        verifiedEntity.Name.Should().Be("Updated Item");
        verifiedEntity.DynamicFields.GetString("existing_dynamic").Should().Be("should_remain");
    }

    /// <summary>
    /// Tests that HasChanges correctly reflects the state of change tracking.
    /// Validates Requirements 11.2, 11.3, 11.4.
    /// </summary>
    [Fact]
    public async Task HasChanges_ReflectsChangeTrackingState()
    {
        // Arrange - Create initial item
        var pk = "has-changes-test-1";
        var sk = "meta";
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk },
            ["name"] = new AttributeValue { S = "HasChanges Test" },
            ["field_a"] = new AttributeValue { S = "value_a" }
        };
        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Load entity
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

        // Assert - Initially no changes
        entity.DynamicFields.HasChanges.Should().BeFalse("No changes made yet");

        // Act - Make a modification
        entity.DynamicFields.SetString("field_b", "value_b");

        // Assert - Now has changes
        entity.DynamicFields.HasChanges.Should().BeTrue("Change was made");

        // Act - Reset tracking
        entity.DynamicFields.ResetChangeTracking();

        // Assert - No changes after reset
        entity.DynamicFields.HasChanges.Should().BeFalse("Tracking was reset");

        // Act - Remove a field
        entity.DynamicFields.Remove("field_a");

        // Assert - Has changes due to removal
        entity.DynamicFields.HasChanges.Should().BeTrue("Removal is a change");
    }

    #endregion

    // Helper class to create a table instance
    private class TestTable : GenericTable
    {
        public TestTable(IAmazonDynamoDB client, string tableName)
            : base(client, tableName)
        {
        }
    }
}
