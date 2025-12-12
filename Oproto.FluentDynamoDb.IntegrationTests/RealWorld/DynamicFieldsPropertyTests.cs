using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Property-based integration tests for dynamic fields support.
/// These tests verify that read operations correctly populate dynamic fields
/// across many random inputs.
/// 
/// **Feature: dynamic-fields-support, Property 5: Read Operations Populate Dynamic Fields**
/// **Validates: Requirements 3.1, 3.2, 3.3**
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Category", "PropertyTest")]
public class DynamicFieldsPropertyTests : IntegrationTestBase
{
    private const int PropertyTestIterations = 20;
    private static readonly Random Random = new(42); // Fixed seed for reproducibility

    public DynamicFieldsPropertyTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await CreateTableAsync<DynamicFieldsTestEntity>();
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 5: Read Operations Populate Dynamic Fields**
    /// **Validates: Requirements 3.1, 3.2, 3.3**
    /// 
    /// For any DynamoDB item containing unmapped attributes, when retrieved via GetItem,
    /// the returned entity's DynamicFields property SHALL contain all unmapped attributes.
    /// </summary>
    [Fact]
    public async Task GetItem_WithRandomDynamicFields_PopulatesAllUnmappedAttributes()
    {
        var fieldNames = GenerateFieldNames();
        var stringValues = GenerateStringValues();

        for (int i = 0; i < PropertyTestIterations; i++)
        {
            var fieldName = fieldNames[i % fieldNames.Length];
            var fieldValue = stringValues[i % stringValues.Length];

            // Arrange
            var pk = $"prop-get-{Guid.NewGuid():N}";
            var sk = "meta";
            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Test" },
                [fieldName] = new AttributeValue { S = fieldValue }
            };

            // Act
            await DynamoDb.PutItemAsync(TableName, item);

            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

            // Assert
            entity.DynamicFields.ContainsKey(fieldName).Should().BeTrue(
                $"Iteration {i}: GetItem should capture dynamic field '{fieldName}'");
            entity.DynamicFields.GetString(fieldName).Should().Be(fieldValue,
                $"Iteration {i}: GetItem should preserve dynamic field value");
        }
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 5: Read Operations Populate Dynamic Fields**
    /// **Validates: Requirements 3.1, 3.2, 3.3**
    /// 
    /// For any DynamoDB item containing unmapped attributes, when retrieved via Query,
    /// the returned entity's DynamicFields property SHALL contain all unmapped attributes.
    /// </summary>
    [Fact]
    public async Task Query_WithRandomDynamicFields_PopulatesAllUnmappedAttributes()
    {
        var fieldNames = GenerateFieldNames();
        var intValues = GenerateIntValues();

        for (int i = 0; i < PropertyTestIterations; i++)
        {
            var fieldName = fieldNames[i % fieldNames.Length];
            var fieldValue = intValues[i % intValues.Length];

            // Arrange
            var pk = $"prop-query-{Guid.NewGuid():N}";
            var sk = "meta";
            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Test" },
                [fieldName] = new AttributeValue { N = fieldValue.ToString() }
            };

            // Act
            await DynamoDb.PutItemAsync(TableName, item);

            var queryResponse = await DynamoDb.QueryAsync(new QueryRequest
            {
                TableName = TableName,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new AttributeValue { S = pk }
                }
            });

            var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(queryResponse.Items[0]);

            // Assert
            entity.DynamicFields.ContainsKey(fieldName).Should().BeTrue(
                $"Iteration {i}: Query should capture dynamic field '{fieldName}'");
            entity.DynamicFields.GetInt(fieldName).Should().Be(fieldValue,
                $"Iteration {i}: Query should preserve dynamic field value");
        }
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 5: Read Operations Populate Dynamic Fields**
    /// **Validates: Requirements 3.1, 3.2, 3.3**
    /// 
    /// For any DynamoDB item containing unmapped attributes, when retrieved via Scan,
    /// the returned entity's DynamicFields property SHALL contain all unmapped attributes.
    /// </summary>
    [Fact]
    public async Task Scan_WithRandomDynamicFields_PopulatesAllUnmappedAttributes()
    {
        var fieldNames = GenerateFieldNames();
        var boolValues = new[] { true, false };

        for (int i = 0; i < PropertyTestIterations; i++)
        {
            var fieldName = fieldNames[i % fieldNames.Length];
            var fieldValue = boolValues[i % boolValues.Length];

            // Arrange
            var pk = $"prop-scan-{Guid.NewGuid():N}";
            var sk = "meta";
            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Test" },
                [fieldName] = new AttributeValue { BOOL = fieldValue }
            };

            // Act
            await DynamoDb.PutItemAsync(TableName, item);

            var scanResponse = await DynamoDb.ScanAsync(new ScanRequest
            {
                TableName = TableName,
                FilterExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new AttributeValue { S = pk }
                }
            });

            var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(scanResponse.Items[0]);

            // Assert
            entity.DynamicFields.ContainsKey(fieldName).Should().BeTrue(
                $"Iteration {i}: Scan should capture dynamic field '{fieldName}'");
            entity.DynamicFields.GetBool(fieldName).Should().Be(fieldValue,
                $"Iteration {i}: Scan should preserve dynamic field value");
        }
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 5: Read Operations Populate Dynamic Fields**
    /// **Validates: Requirements 3.1, 3.2, 3.3**
    /// 
    /// For any entity with multiple dynamic fields, all unmapped attributes SHALL be captured.
    /// </summary>
    [Fact]
    public async Task ReadOperations_WithMultipleDynamicFields_CapturesAllFields()
    {
        for (int iteration = 0; iteration < PropertyTestIterations; iteration++)
        {
            var fieldCount = (iteration % 5) + 1; // 1 to 5 fields

            // Arrange
            var pk = $"prop-multi-{Guid.NewGuid():N}";
            var sk = "meta";
            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Test" }
            };

            // Add random dynamic fields
            var dynamicFields = new Dictionary<string, string>();
            for (int i = 0; i < fieldCount; i++)
            {
                var fieldName = $"dynamic_field_{i}_{Guid.NewGuid():N}";
                var fieldValue = $"value_{i}_{iteration}";
                item[fieldName] = new AttributeValue { S = fieldValue };
                dynamicFields[fieldName] = fieldValue;
            }

            // Act
            await DynamoDb.PutItemAsync(TableName, item);

            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

            // Assert
            entity.DynamicFields.Count.Should().Be(fieldCount,
                $"Iteration {iteration}: Should capture exactly {fieldCount} dynamic fields");

            foreach (var kvp in dynamicFields)
            {
                entity.DynamicFields.ContainsKey(kvp.Key).Should().BeTrue(
                    $"Iteration {iteration}: Should contain field '{kvp.Key}'");
                entity.DynamicFields.GetString(kvp.Key).Should().Be(kvp.Value,
                    $"Iteration {iteration}: Field '{kvp.Key}' should have correct value");
            }
        }
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 5: Read Operations Populate Dynamic Fields**
    /// **Validates: Requirements 3.1, 3.2, 3.3**
    /// 
    /// For any DynamoDB item with various data types in dynamic fields, all types SHALL be captured correctly.
    /// </summary>
    [Fact]
    public async Task ReadOperations_WithVariousDataTypes_CapturesAllTypesCorrectly()
    {
        for (int iteration = 0; iteration < PropertyTestIterations; iteration++)
        {
            // Arrange
            var pk = $"prop-types-{Guid.NewGuid():N}";
            var sk = "meta";
            var stringValue = $"string_{iteration}";
            var intValue = iteration * 10;
            var boolValue = iteration % 2 == 0;
            var doubleValue = iteration * 1.5;

            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Test" },
                ["dyn_string"] = new AttributeValue { S = stringValue },
                ["dyn_int"] = new AttributeValue { N = intValue.ToString() },
                ["dyn_bool"] = new AttributeValue { BOOL = boolValue },
                ["dyn_double"] = new AttributeValue { N = doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            };

            // Act
            await DynamoDb.PutItemAsync(TableName, item);

            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var entity = DynamicFieldsTestEntity.FromDynamoDb<DynamicFieldsTestEntity>(response.Item);

            // Assert
            entity.DynamicFields.Count.Should().Be(4,
                $"Iteration {iteration}: Should capture all 4 dynamic fields");
            entity.DynamicFields.GetString("dyn_string").Should().Be(stringValue);
            entity.DynamicFields.GetInt("dyn_int").Should().Be(intValue);
            entity.DynamicFields.GetBool("dyn_bool").Should().Be(boolValue);
            entity.DynamicFields.GetDouble("dyn_double").Should().Be(doubleValue);
        }
    }

    #region Test Data Generators

    private static string[] GenerateFieldNames()
    {
        return new[]
        {
            "custom_field", "user_data", "metadata", "extra_info", "custom_attr",
            "field_a", "field_b", "field_c", "data_1", "data_2",
            "tenant_field", "app_data", "config_value", "setting_1", "option_a",
            "dynamic_prop", "ext_field", "addon_data", "custom_1", "custom_2"
        };
    }

    private static string[] GenerateStringValues()
    {
        return new[]
        {
            "value1", "test_value", "hello world", "12345", "true",
            "sample data", "test string", "abc123", "foo bar", "dynamic value",
            "lorem ipsum", "test123", "data_value", "string_test", "value_abc",
            "random_str", "test_data", "sample_val", "str_123", "val_xyz"
        };
    }

    private static int[] GenerateIntValues()
    {
        return new[]
        {
            0, 1, -1, 42, 100, -100, 999, -999, 12345, -12345,
            int.MaxValue / 2, int.MinValue / 2, 7, 13, 256, 1024, 2048, 4096, 8192, 16384
        };
    }

    #endregion
}
