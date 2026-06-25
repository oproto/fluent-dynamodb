using System.Linq.Expressions;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Unit tests for computed field validation in UpdateExpressionTranslator.
/// Tests FDDB071/072/073 diagnostics and recomputation logic.
/// </summary>
public class ComputedFieldValidationTests
{
    #region Test Entity Classes

    /// <summary>
    /// Entity with a non-key computed field (Gsi1Pk) composed from Department and Category.
    /// </summary>
    private class ComputedEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Department { get; set; }
        public string? Category { get; set; }
        public string? Gsi1Pk { get; set; }
        public int? Count { get; set; }
    }

    private class ComputedUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> Name { get; } = new();
        public UpdateExpressionProperty<string?> Department { get; } = new();
        public UpdateExpressionProperty<string?> Category { get; } = new();
        public UpdateExpressionProperty<string?> Gsi1Pk { get; } = new();
        public UpdateExpressionProperty<int?> Count { get; } = new();
    }

    private class ComputedUpdateModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Department { get; set; }
        public string? Category { get; set; }
        public string? Gsi1Pk { get; set; }
        public int? Count { get; set; }
    }

    /// <summary>
    /// Entity with a computed field having 3 source properties and a prefix.
    /// </summary>
    private class ThreeSourceEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? Region { get; set; }
        public string? Department { get; set; }
        public string? Category { get; set; }
        public string? CompositeKey { get; set; }
    }

    private class ThreeSourceUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> Region { get; } = new();
        public UpdateExpressionProperty<string?> Department { get; } = new();
        public UpdateExpressionProperty<string?> Category { get; } = new();
        public UpdateExpressionProperty<string?> CompositeKey { get; } = new();
    }

    private class ThreeSourceUpdateModel
    {
        public string? Id { get; set; }
        public string? Region { get; set; }
        public string? Department { get; set; }
        public string? Category { get; set; }
        public string? CompositeKey { get; set; }
    }

    /// <summary>
    /// Entity with two independent computed fields for testing independent validation.
    /// </summary>
    private class MultiComputedEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? FieldA1 { get; set; }
        public string? FieldA2 { get; set; }
        public string? ComputedA { get; set; }
        public string? FieldB1 { get; set; }
        public string? FieldB2 { get; set; }
        public string? ComputedB { get; set; }
    }

    private class MultiComputedUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<string?> FieldA1 { get; } = new();
        public UpdateExpressionProperty<string?> FieldA2 { get; } = new();
        public UpdateExpressionProperty<string?> ComputedA { get; } = new();
        public UpdateExpressionProperty<string?> FieldB1 { get; } = new();
        public UpdateExpressionProperty<string?> FieldB2 { get; } = new();
        public UpdateExpressionProperty<string?> ComputedB { get; } = new();
    }

    private class MultiComputedUpdateModel
    {
        public string? Id { get; set; }
        public string? FieldA1 { get; set; }
        public string? FieldA2 { get; set; }
        public string? ComputedA { get; set; }
        public string? FieldB1 { get; set; }
        public string? FieldB2 { get; set; }
        public string? ComputedB { get; set; }
    }

    #endregion

    #region Helpers

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext(EntityMetadata? metadata = null)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    /// <summary>
    /// Creates metadata for an entity with a non-key computed field Gsi1Pk
    /// composed from Department and Category with separator "#".
    /// </summary>
    private EntityMetadata CreateComputedEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string)
                },
                new PropertyMetadata
                {
                    PropertyName = "Department",
                    AttributeName = "department",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "Gsi1Pk"
                },
                new PropertyMetadata
                {
                    PropertyName = "Category",
                    AttributeName = "category",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "Gsi1Pk"
                },
                new PropertyMetadata
                {
                    PropertyName = "Gsi1Pk",
                    AttributeName = "gsi1pk",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Department", "Category" },
                        Separator = "#"
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Count",
                    AttributeName = "count",
                    PropertyType = typeof(int)
                }
            }
        };
    }

    /// <summary>
    /// Creates metadata for an entity with 3 source properties and a prefix.
    /// CompositeKey = "ORDER" + "#" + Region + "#" + Department + "#" + Category
    /// </summary>
    private EntityMetadata CreateThreeSourceWithPrefixMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "Region",
                    AttributeName = "region",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "CompositeKey"
                },
                new PropertyMetadata
                {
                    PropertyName = "Department",
                    AttributeName = "department",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "CompositeKey"
                },
                new PropertyMetadata
                {
                    PropertyName = "Category",
                    AttributeName = "category",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "CompositeKey"
                },
                new PropertyMetadata
                {
                    PropertyName = "CompositeKey",
                    AttributeName = "gsi1pk",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Region", "Department", "Category" },
                        Separator = "#",
                        Prefix = "ORDER",
                        PrefixSeparator = "#"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates metadata for an entity with two independent computed fields.
    /// ComputedA = FieldA1 + "#" + FieldA2
    /// ComputedB = FieldB1 + "#" + FieldB2
    /// </summary>
    private EntityMetadata CreateMultiComputedMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "FieldA1",
                    AttributeName = "field_a1",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedA"
                },
                new PropertyMetadata
                {
                    PropertyName = "FieldA2",
                    AttributeName = "field_a2",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedA"
                },
                new PropertyMetadata
                {
                    PropertyName = "ComputedA",
                    AttributeName = "computed_a",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "FieldA1", "FieldA2" },
                        Separator = "#"
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "FieldB1",
                    AttributeName = "field_b1",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedB"
                },
                new PropertyMetadata
                {
                    PropertyName = "FieldB2",
                    AttributeName = "field_b2",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "ComputedB"
                },
                new PropertyMetadata
                {
                    PropertyName = "ComputedB",
                    AttributeName = "computed_b",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "FieldB1", "FieldB2" },
                        Separator = "#"
                    }
                }
            }
        };
    }

    #endregion

    #region FDDB071: Entity Parameter Reference Detection (Req 6.1)

    [Fact]
    public void FDDB071_SourcePropertyReferencingEntityParameter_ThrowsWithCorrectMessage()
    {
        // Arrange: Build an expression tree equivalent to:
        //   x => new Model { Department = x.Department.ToString() }
        // The expression references the lambda parameter x, which should trigger FDDB071.
        var translator = CreateTranslator();
        var context = CreateContext(CreateComputedEntityMetadata());

        var parameter = Expression.Parameter(typeof(ComputedUpdateExpressions), "x");
        // Access x.Department (type: UpdateExpressionProperty<string?>)
        var deptProperty = Expression.Property(parameter, nameof(ComputedUpdateExpressions.Department));
        // Call ToString() on x.Department to get a string that references the entity parameter
        var toStringMethod = typeof(object).GetMethod(nameof(object.ToString))!;
        var toStringCall = Expression.Call(deptProperty, toStringMethod);
        // Convert string to string? to match the target property type
        var convertedToNullable = Expression.TypeAs(toStringCall, typeof(string));

        var binding = Expression.Bind(
            typeof(ComputedUpdateModel).GetProperty(nameof(ComputedUpdateModel.Department))!,
            convertedToNullable);
        var memberInit = Expression.MemberInit(Expression.New(typeof(ComputedUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<ComputedUpdateExpressions, ComputedUpdateModel>>(memberInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(lambda, context);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Source properties of computed fields must be assigned constant or local values*")
            .WithMessage("*'Department'*")
            .WithMessage("*references the entity parameter*");
    }

    #endregion

    #region FDDB072: Partial Source Assignment (Req 4.2)

    [Fact]
    public void FDDB072_OneOfThreeSourcesAssigned_ThrowsListingTwoMissing()
    {
        // Arrange: Assign only Region (1 of 3 sources: Region, Department, Category)
        var translator = CreateTranslator();
        var context = CreateContext(CreateThreeSourceWithPrefixMetadata());

        Expression<Func<ThreeSourceUpdateExpressions, ThreeSourceUpdateModel>> expression =
            x => new ThreeSourceUpdateModel { Region = "US-EAST" };

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(expression, context);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*All source properties of computed field 'CompositeKey' must be specified*")
            .WithMessage("*Missing: Department, Category*");
    }

    #endregion

    #region FDDB073: Mixed Direct and Source Assignment (Req 5.2)

    [Fact]
    public void FDDB073_DirectAndSourceAssignment_ThrowsWithCorrectMessage()
    {
        // Arrange: Assign both Gsi1Pk directly AND its source property Department
        var translator = CreateTranslator();
        var context = CreateContext(CreateComputedEntityMetadata());

        Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
            x => new ComputedUpdateModel
            {
                Gsi1Pk = "direct-value",
                Department = "Electronics",
                Category = "Phones"
            };

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(expression, context);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot set both computed field 'Gsi1Pk' and its source properties*")
            .WithMessage("*Use one approach or the other*");
    }

    #endregion

    #region Recomputation with Prefix (Req 7.6)

    [Fact]
    public void Recomputation_WithPrefix_ProducesCorrectSetExpression()
    {
        // Arrange: Assign all 3 sources with prefix configured as "ORDER#"
        // Expected recomputed value: "ORDER" + "#" + "val1" + "#" + "val2" + "#" + "val3" = "ORDER#val1#val2#val3"
        // But with the 3 sources being Region="val1", Department="val2", Category="val3"
        // Concatenation: "val1#val2#val3", with prefix: "ORDER#val1#val2#val3"
        var translator = CreateTranslator();
        var context = CreateContext(CreateThreeSourceWithPrefixMetadata());

        Expression<Func<ThreeSourceUpdateExpressions, ThreeSourceUpdateModel>> expression =
            x => new ThreeSourceUpdateModel
            {
                Region = "val1",
                Department = "val2",
                Category = "val3"
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: Should produce a SET expression for the computed field attribute
        result.Should().Contain("SET");
        // The recomputed value should be "ORDER#val1#val2#val3"
        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == "ORDER#val1#val2#val3");
        // The attribute name should map to gsi1pk
        context.AttributeNames.AttributeNames.Values
            .Should().Contain("gsi1pk");
    }

    #endregion

    #region Multiple Computed Fields: Independent Validation (Req 5.5)

    [Fact]
    public void MultipleComputedFields_FDDB073OnOne_OtherNotAffected()
    {
        // Arrange: ComputedA has mixed assignment (direct + source), ComputedB has valid all-source assignment
        // The FDDB073 for ComputedA should throw before ComputedB is processed
        var translator = CreateTranslator();
        var context = CreateContext(CreateMultiComputedMetadata());

        Expression<Func<MultiComputedUpdateExpressions, MultiComputedUpdateModel>> expression =
            x => new MultiComputedUpdateModel
            {
                // Mixed assignment on ComputedA (direct + source)
                ComputedA = "direct-value",
                FieldA1 = "a1",
                FieldA2 = "a2",
                // Valid all-source assignment for ComputedB
                FieldB1 = "b1",
                FieldB2 = "b2"
            };

        // Act & Assert: FDDB073 thrown for ComputedA
        var act = () => translator.TranslateUpdateExpression(expression, context);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot set both computed field 'ComputedA'*");
    }

    [Fact]
    public void MultipleComputedFields_OneValidOneNot_OnlyInvalidThrows()
    {
        // Arrange: ComputedA has partial assignment (only FieldA1), ComputedB has all sources
        var translator = CreateTranslator();
        var context = CreateContext(CreateMultiComputedMetadata());

        Expression<Func<MultiComputedUpdateExpressions, MultiComputedUpdateModel>> expression =
            x => new MultiComputedUpdateModel
            {
                // Partial assignment on ComputedA (only 1 of 2)
                FieldA1 = "a1",
                // Valid all-source assignment for ComputedB
                FieldB1 = "b1",
                FieldB2 = "b2"
            };

        // Act & Assert: FDDB072 thrown for ComputedA (missing FieldA2)
        var act = () => translator.TranslateUpdateExpression(expression, context);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*All source properties of computed field 'ComputedA'*")
            .WithMessage("*Missing: FieldA2*");
    }

    #endregion

    #region Direct Assignment to Non-Key Computed Field (Req 7.5, 9.3)

    [Fact]
    public void DirectAssignment_ToNonKeyComputedField_ProducesStandardSet()
    {
        // Arrange: Assign Gsi1Pk directly without any source properties
        var translator = CreateTranslator();
        var context = CreateContext(CreateComputedEntityMetadata());

        Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
            x => new ComputedUpdateModel { Gsi1Pk = "Electronics#Phones" };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: Should produce a standard SET expression
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("gsi1pk");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Electronics#Phones");
    }

    #endregion

    #region Existing Features Unchanged (Req 9.4)

    [Fact]
    public void ExistingFeature_SimpleSet_UnchangedWithComputedFieldMetadata()
    {
        // Arrange: Set a non-computed property (Name) with computed field metadata present
        var translator = CreateTranslator();
        var context = CreateContext(CreateComputedEntityMetadata());

        Expression<Func<ComputedUpdateExpressions, ComputedUpdateModel>> expression =
            x => new ComputedUpdateModel { Name = "NewName" };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("NewName");
    }

    [Fact]
    public void ExistingFeature_Remove_UnchangedWithComputedFieldMetadata()
    {
        // Arrange: Remove a non-computed property
        var translator = CreateTranslator();
        var context = CreateContext(CreateComputedEntityMetadata());

        var parameter = Expression.Parameter(typeof(ComputedUpdateExpressions), "x");
        var nameProperty = Expression.Property(parameter, nameof(ComputedUpdateExpressions.Name));
        var removeMethod = typeof(UpdateExpressionPropertyExtensions)
            .GetMethod(nameof(UpdateExpressionPropertyExtensions.Remove))!
            .MakeGenericMethod(typeof(string));
        var methodCall = Expression.Call(removeMethod, nameProperty);
        var binding = Expression.Bind(typeof(ComputedUpdateModel).GetProperty(nameof(ComputedUpdateModel.Name))!, methodCall);
        var memberInit = Expression.MemberInit(Expression.New(typeof(ComputedUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<ComputedUpdateExpressions, ComputedUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().Be("REMOVE #attr0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
    }

    [Fact]
    public void ExistingFeature_Add_UnchangedWithComputedFieldMetadata()
    {
        // Arrange: ADD on a non-computed numeric property
        var translator = CreateTranslator();
        var context = CreateContext(CreateComputedEntityMetadata());

        var parameter = Expression.Parameter(typeof(ComputedUpdateExpressions), "x");
        var countProperty = Expression.Property(parameter, nameof(ComputedUpdateExpressions.Count));
        var addMethod = typeof(UpdateExpressionPropertyExtensions).GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.Add))
            .Where(m => !m.IsGenericMethod)
            .First(m => m.GetParameters()[0].ParameterType == typeof(UpdateExpressionProperty<int?>)
                        || m.GetParameters()[0].ParameterType == typeof(UpdateExpressionProperty<int>));

        // Find the method that works with UpdateExpressionProperty<int?>
        var nullableAddMethod = typeof(UpdateExpressionPropertyExtensions).GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.Add))
            .Where(m => !m.IsGenericMethod)
            .FirstOrDefault(m =>
            {
                var paramType = m.GetParameters()[0].ParameterType;
                return paramType == typeof(UpdateExpressionProperty<int?>);
            });

        // Fall back to int version if nullable not found
        var targetMethod = nullableAddMethod ?? typeof(UpdateExpressionPropertyExtensions).GetMethods()
            .Where(m => m.Name == nameof(UpdateExpressionPropertyExtensions.Add))
            .Where(m => !m.IsGenericMethod)
            .First(m => m.GetParameters()[0].ParameterType == typeof(UpdateExpressionProperty<int>));

        var methodCall = Expression.Call(targetMethod, countProperty, Expression.Constant(
            targetMethod.GetParameters()[1].ParameterType == typeof(int?) ? (int?)5 : 5,
            targetMethod.GetParameters()[1].ParameterType));
        var binding = Expression.Bind(typeof(ComputedUpdateModel).GetProperty(nameof(ComputedUpdateModel.Count))!, methodCall);
        var memberInit = Expression.MemberInit(Expression.New(typeof(ComputedUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<ComputedUpdateExpressions, ComputedUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert
        result.Should().StartWith("ADD");
        result.Should().Contain("#attr0 :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("count");
    }

    [Fact]
    public void ExistingFeature_Arithmetic_UnchangedWithComputedFieldMetadata()
    {
        // Arrange: Arithmetic on a non-computed property (Count = Count + 1)
        var translator = CreateTranslator();
        var context = CreateContext(CreateComputedEntityMetadata());

        var parameter = Expression.Parameter(typeof(ComputedUpdateExpressions), "x");
        var countProperty = Expression.Property(parameter, nameof(ComputedUpdateExpressions.Count));
        var addExpression = Expression.Add(countProperty, Expression.Constant((int?)5, typeof(int?)));
        var binding = Expression.Bind(typeof(ComputedUpdateModel).GetProperty(nameof(ComputedUpdateModel.Count))!, addExpression);
        var memberInit = Expression.MemberInit(Expression.New(typeof(ComputedUpdateModel)), binding);
        var lambda = Expression.Lambda<Func<ComputedUpdateExpressions, ComputedUpdateModel>>(memberInit, parameter);

        // Act
        var result = translator.TranslateUpdateExpression(lambda, context);

        // Assert: Should produce SET #count = #count + :p0
        result.Should().Contain("SET");
        result.Should().Contain("#attr0 = #attr0 +");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("count");
    }

    #endregion
}
