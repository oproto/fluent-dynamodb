using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for typed async convenience method generation.
///
/// Feature: typed-async-convenience-methods
/// </summary>
public class TypedAsyncConveniencePropertyTests
{
    private static readonly string[] NonStringTypes = { "int", "long", "DateTime", "Guid", "decimal", "DateOnly" };

    #region Property 1: Eligible entities produce typed async methods with correct signatures

    /// <summary>
    /// **Property 1: Eligible entities produce typed async methods with correct signatures**
    /// **Validates: Requirements 1.1, 1.3, 2.1, 2.3**
    ///
    /// For any EntityModel where ComputedOverloadEligibility.QualifiesForTypedOverload returns true
    /// AND WouldBeAmbiguous returns false AND GetTypedOverloadParameters returns non-null,
    /// the generated code SHALL contain a GetAsync method returning Task&lt;T?&gt; and a DeleteAsync
    /// method returning Task with the correct typed parameter lists.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "1")]
    public Property EligibleEntities_ProduceTypedGetAsync_WithCorrectSignature()
    {
        var entityGen = CreateEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var expectedParamList = string.Join(", ", typedParams.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));

            // Assert: GetAsync method exists with correct return type Task<EntityName?>
            var expectedGetAsyncSignature = $"async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({expectedParamList}";
            var hasGetAsync = generatedCode.Contains(expectedGetAsyncSignature);

            // Assert: GetAsync includes CancellationToken parameter
            var hasGetAsyncCancellationToken = generatedCode.Contains(
                $"GetAsync({expectedParamList}, System.Threading.CancellationToken cancellationToken = default)");

            return (hasGetAsync && hasGetAsyncCancellationToken)
                .Label($"GetAsync signature found: {hasGetAsync}, CancellationToken: {hasGetAsyncCancellationToken}. " +
                       $"Entity: {entity.ClassName}, Expected signature fragment: '{expectedGetAsyncSignature}'");
        });
    }

    /// <summary>
    /// **Property 1 (continued): Eligible entities produce typed DeleteAsync with correct signature**
    /// **Validates: Requirements 2.1, 2.3**
    ///
    /// For any eligible EntityModel, the generated code SHALL contain a DeleteAsync method
    /// returning Task with typed parameters, KeyCondition, and CancellationToken.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "1")]
    public Property EligibleEntities_ProduceTypedDeleteAsync_WithCorrectSignature()
    {
        var entityGen = CreateEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var expectedParamList = string.Join(", ", typedParams.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));

            // Assert: DeleteAsync method exists with correct return type Task
            var expectedDeleteAsyncSignature = $"async System.Threading.Tasks.Task DeleteAsync({expectedParamList}";
            var hasDeleteAsync = generatedCode.Contains(expectedDeleteAsyncSignature);

            // Assert: DeleteAsync includes KeyCondition and CancellationToken parameters
            var expectedDeleteFullParams = $"DeleteAsync({expectedParamList}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)";
            var hasDeleteAsyncFullParams = generatedCode.Contains(expectedDeleteFullParams);

            return (hasDeleteAsync && hasDeleteAsyncFullParams)
                .Label($"DeleteAsync signature found: {hasDeleteAsync}, Full params: {hasDeleteAsyncFullParams}. " +
                       $"Entity: {entity.ClassName}, Expected: '{expectedDeleteAsyncSignature}'");
        });
    }

    #endregion

    #region Property 5: FluentResults-enabled entities produce typed Result variants

    /// <summary>
    /// **Property 5: FluentResults-enabled entities produce typed Result variants**
    /// **Validates: Requirements 5.1, 5.3, 6.1, 6.4**
    ///
    /// For any EntityModel where UseFluentResults is true AND the entity qualifies for non-ambiguous
    /// typed overloads, the generated code SHALL contain GetAsyncResult returning
    /// Task&lt;Result&lt;T?&gt;&gt; with the resolved typed parameter list.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "5")]
    public Property FluentResultsEntities_ProduceTypedGetAsyncResult_WithCorrectSignature()
    {
        var entityGen = CreateFluentResultsEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var expectedParamList = string.Join(", ", typedParams.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));

            // Assert: GetAsyncResult method exists with correct return type Task<Result<EntityName?>>
            var expectedGetAsyncResultSignature =
                $"System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({expectedParamList}";
            var hasGetAsyncResult = generatedCode.Contains(expectedGetAsyncResultSignature);

            // Assert: GetAsyncResult includes CancellationToken parameter
            var hasGetAsyncResultCancellationToken = generatedCode.Contains(
                $"GetAsyncResult({expectedParamList}, System.Threading.CancellationToken cancellationToken = default)");

            return (hasGetAsyncResult && hasGetAsyncResultCancellationToken)
                .Label($"GetAsyncResult signature found: {hasGetAsyncResult}, CancellationToken: {hasGetAsyncResultCancellationToken}. " +
                       $"Entity: {entity.ClassName}, Expected signature fragment: '{expectedGetAsyncResultSignature}'");
        });
    }

    /// <summary>
    /// **Property 5 (continued): FluentResults-enabled entities produce typed DeleteAsyncResult**
    /// **Validates: Requirements 6.1, 6.4**
    ///
    /// For any EntityModel where UseFluentResults is true AND the entity qualifies for non-ambiguous
    /// typed overloads, the generated code SHALL contain DeleteAsyncResult returning
    /// Task&lt;Result&gt; with the resolved typed parameter list, KeyCondition, and CancellationToken.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "5")]
    public Property FluentResultsEntities_ProduceTypedDeleteAsyncResult_WithCorrectSignature()
    {
        var entityGen = CreateFluentResultsEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var expectedParamList = string.Join(", ", typedParams.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));

            // Assert: DeleteAsyncResult method exists with correct return type Task<Result>
            var expectedDeleteAsyncResultSignature =
                $"System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({expectedParamList}";
            var hasDeleteAsyncResult = generatedCode.Contains(expectedDeleteAsyncResultSignature);

            // Assert: DeleteAsyncResult includes KeyCondition and CancellationToken parameters
            var expectedDeleteFullParams =
                $"DeleteAsyncResult({expectedParamList}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)";
            var hasDeleteAsyncResultFullParams = generatedCode.Contains(expectedDeleteFullParams);

            return (hasDeleteAsyncResult && hasDeleteAsyncResultFullParams)
                .Label($"DeleteAsyncResult signature found: {hasDeleteAsyncResult}, Full params: {hasDeleteAsyncResultFullParams}. " +
                       $"Entity: {entity.ClassName}, Expected: '{expectedDeleteAsyncResultSignature}'");
        });
    }

    #endregion

    #region Property 4: Single-entity tables produce table-level typed async methods that delegate to accessor

    /// <summary>
    /// **Property 4: Single-entity tables produce table-level typed async methods that delegate to accessor**
    /// **Validates: Requirements 3.1, 3.2, 3.3, 4.1, 4.2, 4.3**
    ///
    /// For any single-entity table configuration where the entity qualifies for typed overloads and the
    /// overload is not ambiguous, the generated table class SHALL contain typed GetAsync and DeleteAsync
    /// methods whose bodies delegate to the entity accessor's corresponding typed async method,
    /// passing all parameters unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "4")]
    public Property SingleEntityTable_ProducesTableLevelTypedGetAsync_DelegatingToAccessor()
    {
        var entityGen = CreateSingleEntityTableGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var expectedParamList = string.Join(", ", typedParams.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
            var fullParamList = $"{expectedParamList}, System.Threading.CancellationToken cancellationToken = default";

            // The entity accessor property name is ClassName + "s"
            var entityPropertyName = entity.ClassName + "s";

            // Assert: table-level GetAsync method exists with expression-body syntax
            var expectedTableGetAsyncSignature =
                $"public System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync({fullParamList}) =>";
            var hasTableGetAsync = generatedCode.Contains(expectedTableGetAsyncSignature);

            // Assert: GetAsync delegates to the entity accessor
            var argList = string.Join(", ", typedParams.Select(p => p.Name));
            var expectedDelegation = $"{entityPropertyName}.GetAsync({argList}, cancellationToken);";
            var hasDelegation = generatedCode.Contains(expectedDelegation);

            return (hasTableGetAsync && hasDelegation)
                .Label($"Table-level GetAsync found: {hasTableGetAsync}, Delegates to accessor: {hasDelegation}. " +
                       $"Entity: {entity.ClassName}, Accessor: {entityPropertyName}, " +
                       $"Expected signature: '{expectedTableGetAsyncSignature}', Expected delegation: '{expectedDelegation}'");
        });
    }

    /// <summary>
    /// **Property 4 (continued): Single-entity tables produce table-level typed DeleteAsync that delegates to accessor**
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    ///
    /// For any single-entity table configuration where the entity qualifies for typed overloads and the
    /// overload is not ambiguous, the generated table class SHALL contain a typed DeleteAsync method
    /// whose body delegates to the entity accessor's typed DeleteAsync, passing all parameters unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "4")]
    public Property SingleEntityTable_ProducesTableLevelTypedDeleteAsync_DelegatingToAccessor()
    {
        var entityGen = CreateSingleEntityTableGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var expectedParamList = string.Join(", ", typedParams.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
            var fullParamList = $"{expectedParamList}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default";

            // The entity accessor property name is ClassName + "s"
            var entityPropertyName = entity.ClassName + "s";

            // Assert: table-level DeleteAsync method exists with expression-body syntax
            var expectedTableDeleteAsyncSignature =
                $"public System.Threading.Tasks.Task DeleteAsync({fullParamList}) =>";
            var hasTableDeleteAsync = generatedCode.Contains(expectedTableDeleteAsyncSignature);

            // Assert: DeleteAsync delegates to the entity accessor
            var argList = string.Join(", ", typedParams.Select(p => p.Name));
            var expectedDelegation = $"{entityPropertyName}.DeleteAsync({argList}, keyCondition, cancellationToken);";
            var hasDelegation = generatedCode.Contains(expectedDelegation);

            return (hasTableDeleteAsync && hasDelegation)
                .Label($"Table-level DeleteAsync found: {hasTableDeleteAsync}, Delegates to accessor: {hasDelegation}. " +
                       $"Entity: {entity.ClassName}, Accessor: {entityPropertyName}, " +
                       $"Expected signature: '{expectedTableDeleteAsyncSignature}', Expected delegation: '{expectedDelegation}'");
        });
    }

    #endregion

    #region Property 6: Single-entity FluentResults tables produce table-level Result variants

    /// <summary>
    /// **Property 6: Single-entity FluentResults tables produce table-level Result variants**
    /// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
    ///
    /// For any single-entity table where the entity has UseFluentResults enabled AND qualifies
    /// for non-ambiguous typed overloads, the generated table class SHALL contain typed
    /// GetAsyncResult method that delegates to the accessor's typed GetAsyncResult.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "6")]
    public Property SingleEntityFluentResultsTable_ProducesTableLevelGetAsyncResult()
    {
        var entityGen = CreateFluentResultsEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var expectedParamList = string.Join(", ", typedParams.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
            expectedParamList += ", System.Threading.CancellationToken cancellationToken = default";

            // The entity property name for delegation (simple pluralization)
            var entityPropertyName = entity.ClassName + "s";

            // Assert: Table-level GetAsyncResult exists with correct signature
            var expectedSignature =
                $"public System.Threading.Tasks.Task<global::FluentResults.Result<{entity.ClassName}?>> GetAsyncResult({expectedParamList})";
            var hasTableLevelGetAsyncResult = generatedCode.Contains(expectedSignature);

            // Assert: Table-level GetAsyncResult delegates to the accessor
            var expectedArgList = string.Join(", ", typedParams.Select(p => p.Name));
            expectedArgList += ", cancellationToken";
            var expectedDelegation = $"{entityPropertyName}.GetAsyncResult({expectedArgList})";
            var hasDelegation = generatedCode.Contains(expectedDelegation);

            return (hasTableLevelGetAsyncResult && hasDelegation)
                .Label($"Table-level GetAsyncResult found: {hasTableLevelGetAsyncResult}, Delegates to accessor: {hasDelegation}. " +
                       $"Entity: {entity.ClassName}, Expected signature: '{expectedSignature}', Expected delegation: '{expectedDelegation}'");
        });
    }

    /// <summary>
    /// **Property 6 (continued): Single-entity FluentResults tables produce table-level DeleteAsyncResult**
    /// **Validates: Requirements 7.2, 7.4**
    ///
    /// For any single-entity table where the entity has UseFluentResults enabled AND qualifies
    /// for non-ambiguous typed overloads, the generated table class SHALL contain typed
    /// DeleteAsyncResult method that delegates to the accessor's typed DeleteAsyncResult.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "6")]
    public Property SingleEntityFluentResultsTable_ProducesTableLevelDeleteAsyncResult()
    {
        var entityGen = CreateFluentResultsEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var expectedParamList = string.Join(", ", typedParams.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
            expectedParamList += ", KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default";

            // The entity property name for delegation (simple pluralization)
            var entityPropertyName = entity.ClassName + "s";

            // Assert: Table-level DeleteAsyncResult exists with correct signature
            var expectedSignature =
                $"public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult({expectedParamList})";
            var hasTableLevelDeleteAsyncResult = generatedCode.Contains(expectedSignature);

            // Assert: Table-level DeleteAsyncResult delegates to the accessor
            var expectedArgList = string.Join(", ", typedParams.Select(p => p.Name));
            expectedArgList += ", keyCondition, cancellationToken";
            var expectedDelegation = $"{entityPropertyName}.DeleteAsyncResult({expectedArgList})";
            var hasDelegation = generatedCode.Contains(expectedDelegation);

            return (hasTableLevelDeleteAsyncResult && hasDelegation)
                .Label($"Table-level DeleteAsyncResult found: {hasTableLevelDeleteAsyncResult}, Delegates to accessor: {hasDelegation}. " +
                       $"Entity: {entity.ClassName}, Expected signature: '{expectedSignature}', Expected delegation: '{expectedDelegation}'");
        });
    }

    #endregion

    #region Property 3: Generated typed async methods delegate correctly to typed builder then terminal

    /// <summary>
    /// **Property 3: Generated typed async methods delegate correctly to typed builder then terminal**
    /// **Validates: Requirements 1.2, 2.2, 5.2, 6.2, 6.3**
    ///
    /// For any eligible EntityModel, the generated typed GetAsync method body SHALL call the typed
    /// Get(...) builder overload followed by .GetItemAsync(cancellationToken).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "3")]
    public Property EligibleEntities_GetAsync_DelegatesToGetThenGetItemAsync()
    {
        var entityGen = CreateEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters to build the delegation args
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var delegationArgs = string.Join(", ", typedParams.Select(p => p.Name));

            // Assert: GetAsync body contains Get( call with the delegation args
            var expectedGetCall = $"Get({delegationArgs}).GetItemAsync(cancellationToken)";
            var hasGetDelegation = generatedCode.Contains(expectedGetCall);

            return hasGetDelegation
                .Label($"GetAsync delegates to Get(...).GetItemAsync(cancellationToken): {hasGetDelegation}. " +
                       $"Entity: {entity.ClassName}, Expected: '{expectedGetCall}'");
        });
    }

    /// <summary>
    /// **Property 3 (continued): Generated typed DeleteAsync delegates correctly**
    /// **Validates: Requirements 2.2**
    ///
    /// For any eligible EntityModel, the generated typed DeleteAsync method body SHALL call the typed
    /// Delete(...) builder, conditionally apply .WithKeyCondition(keyCondition) when keyCondition != KeyCondition.None,
    /// then call .DeleteAsync(cancellationToken).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "3")]
    public Property EligibleEntities_DeleteAsync_DelegatesToDeleteWithConditionalKeyCondition()
    {
        var entityGen = CreateEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters to build the delegation args
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var delegationArgs = string.Join(", ", typedParams.Select(p => p.Name));

            // Assert: DeleteAsync body contains Delete( call
            var expectedDeleteCall = $"var builder = Delete({delegationArgs});";
            var hasDeleteCall = generatedCode.Contains(expectedDeleteCall);

            // Assert: conditional WithKeyCondition pattern
            var hasKeyConditionCheck = generatedCode.Contains("if (keyCondition != KeyCondition.None)");
            var hasWithKeyCondition = generatedCode.Contains("builder.WithKeyCondition(keyCondition);");

            // Assert: terminal DeleteAsync call
            var hasDeleteAsync = generatedCode.Contains("await builder.DeleteAsync(cancellationToken);");

            return (hasDeleteCall && hasKeyConditionCheck && hasWithKeyCondition && hasDeleteAsync)
                .Label($"DeleteAsync delegation: Delete({delegationArgs})={hasDeleteCall}, " +
                       $"keyCondition check={hasKeyConditionCheck}, WithKeyCondition={hasWithKeyCondition}, " +
                       $"builder.DeleteAsync={hasDeleteAsync}. Entity: {entity.ClassName}");
        });
    }

    /// <summary>
    /// **Property 3 (continued): Generated typed GetAsyncResult delegates correctly**
    /// **Validates: Requirements 5.2**
    ///
    /// For any eligible FluentResults EntityModel, the generated typed GetAsyncResult method body SHALL
    /// call the typed Get(...) builder overload followed by .GetItemAsyncResult(cancellationToken)
    /// using expression-body syntax.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "3")]
    public Property FluentResultsEntities_GetAsyncResult_DelegatesToGetThenGetItemAsyncResult()
    {
        var entityGen = CreateFluentResultsEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters to build the delegation args
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var delegationArgs = string.Join(", ", typedParams.Select(p => p.Name));

            // Assert: GetAsyncResult body contains Get( followed by .GetItemAsyncResult(cancellationToken)
            var expectedGetAsyncResultCall = $"Get({delegationArgs}).GetItemAsyncResult(cancellationToken)";
            var hasGetAsyncResultDelegation = generatedCode.Contains(expectedGetAsyncResultCall);

            return hasGetAsyncResultDelegation
                .Label($"GetAsyncResult delegates to Get(...).GetItemAsyncResult(cancellationToken): {hasGetAsyncResultDelegation}. " +
                       $"Entity: {entity.ClassName}, Expected: '{expectedGetAsyncResultCall}'");
        });
    }

    /// <summary>
    /// **Property 3 (continued): Generated typed DeleteAsyncResult delegates correctly**
    /// **Validates: Requirements 6.2, 6.3**
    ///
    /// For any eligible FluentResults EntityModel, the generated typed DeleteAsyncResult method body SHALL
    /// call the typed Delete(...) builder, conditionally apply .WithKeyCondition(keyCondition) when
    /// keyCondition != KeyCondition.None, then call .DeleteAsyncResult(cancellationToken).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "3")]
    public Property FluentResultsEntities_DeleteAsyncResult_DelegatesToDeleteWithConditionalKeyCondition()
    {
        var entityGen = CreateFluentResultsEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Resolve the expected typed parameters to build the delegation args
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity)!;
            var delegationArgs = string.Join(", ", typedParams.Select(p => p.Name));

            // Assert: DeleteAsyncResult body contains Delete( call
            var expectedDeleteCall = $"var builder = Delete({delegationArgs});";
            var hasDeleteCall = generatedCode.Contains(expectedDeleteCall);

            // Assert: conditional WithKeyCondition pattern
            var hasKeyConditionCheck = generatedCode.Contains("if (keyCondition != KeyCondition.None)");
            var hasWithKeyCondition = generatedCode.Contains("builder.WithKeyCondition(keyCondition);");

            // Assert: terminal DeleteAsyncResult call
            var hasDeleteAsyncResult = generatedCode.Contains("return builder.DeleteAsyncResult(cancellationToken);");

            return (hasDeleteCall && hasKeyConditionCheck && hasWithKeyCondition && hasDeleteAsyncResult)
                .Label($"DeleteAsyncResult delegation: Delete({delegationArgs})={hasDeleteCall}, " +
                       $"keyCondition check={hasKeyConditionCheck}, WithKeyCondition={hasWithKeyCondition}, " +
                       $"builder.DeleteAsyncResult={hasDeleteAsyncResult}. Entity: {entity.ClassName}");
        });
    }

    #endregion

    #region Property 7: Eligibility is consistent between typed builder and typed async generation

    /// <summary>
    /// **Property 7: Eligibility is consistent between typed builder and typed async generation**
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    ///
    /// For any randomly generated EntityModel (eligible or ineligible), verify that presence of
    /// typed Get(...) builder overload in output implies presence of typed GetAsync(...) (when
    /// HideGeneratedAsyncMethods is effectively false), and absence of typed Get(...) implies
    /// absence of typed GetAsync(...).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "7")]
    public Property EligibilityConsistency_TypedGetBuilder_ImpliesTypedGetAsync()
    {
        var entityGen = CreateMixedEligibilityEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Determine if this entity has a typed Get(...) builder overload
            // The typed builder returns GetItemRequestBuilder<T> and accepts non-standard params
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
            bool hasTypedBuilderOverload;

            if (typedParams != null && ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                && !ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            {
                // Eligible entity — check if typed Get(...) builder overload is in output
                var expectedParamList = string.Join(", ", typedParams.Select(p =>
                    $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
                var typedGetBuilderSignature = $"GetItemRequestBuilder<{entity.ClassName}> Get({expectedParamList})";
                hasTypedBuilderOverload = generatedCode.Contains(typedGetBuilderSignature);

                // Now check if typed GetAsync(...) is in output
                var typedGetAsyncSignature = $"GetAsync({expectedParamList}, System.Threading.CancellationToken cancellationToken = default)";
                var hasTypedGetAsync = generatedCode.Contains(typedGetAsyncSignature);

                // Property: presence of typed Get builder implies presence of typed GetAsync
                return (hasTypedBuilderOverload == hasTypedGetAsync)
                    .Label($"Eligible entity '{entity.ClassName}': typed Get builder found={hasTypedBuilderOverload}, " +
                           $"typed GetAsync found={hasTypedGetAsync}. " +
                           $"UseFluentResults={entity.UseFluentResults}, HideGeneratedAsyncMethods={entity.HideGeneratedAsyncMethods}");
            }
            else
            {
                // Ineligible entity — neither typed Get builder NOR typed GetAsync should exist
                // We can't easily compute the exact param signature for ineligible entities,
                // but we can verify no typed GetAsync with non-standard (non-string-only) params exists
                hasTypedBuilderOverload = false;

                // For ineligible entities: verify no typed GetAsync exists
                // The standard GetAsync uses string params (pK, sK). Any GetAsync with
                // non-standard params would be a typed GetAsync.
                var hasTypedGetAsync = generatedCode.Contains($"async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync(int ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync(long ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync(DateTime ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync(Guid ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync(decimal ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task<{entity.ClassName}?> GetAsync(DateOnly ");

                return (!hasTypedGetAsync)
                    .Label($"Ineligible entity '{entity.ClassName}': should have no typed GetAsync, " +
                           $"but found one={hasTypedGetAsync}");
            }
        });
    }

    /// <summary>
    /// **Property 7 (continued): Eligibility is consistent between typed Delete builder and typed DeleteAsync**
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    ///
    /// For any randomly generated EntityModel (eligible or ineligible), verify that presence of
    /// typed Delete(...) builder overload in output implies presence of typed DeleteAsync(...)
    /// (when HideGeneratedAsyncMethods is effectively false), and absence of typed Delete(...)
    /// implies absence of typed DeleteAsync(...).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "typed-async-convenience-methods")]
    [Trait("Property", "7")]
    public Property EligibilityConsistency_TypedDeleteBuilder_ImpliesTypedDeleteAsync()
    {
        var entityGen = CreateMixedEligibilityEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Determine if this entity has a typed Delete(...) builder overload
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
            bool hasTypedBuilderOverload;

            if (typedParams != null && ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                && !ComputedOverloadEligibility.WouldBeAmbiguous(entity))
            {
                // Eligible entity — check if typed Delete(...) builder overload is in output
                var expectedParamList = string.Join(", ", typedParams.Select(p =>
                    $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
                var typedDeleteBuilderSignature = $"DeleteItemRequestBuilder<{entity.ClassName}> Delete({expectedParamList})";
                hasTypedBuilderOverload = generatedCode.Contains(typedDeleteBuilderSignature);

                // Now check if typed DeleteAsync(...) is in output
                var typedDeleteAsyncSignature = $"DeleteAsync({expectedParamList}, KeyCondition keyCondition = KeyCondition.None, System.Threading.CancellationToken cancellationToken = default)";
                var hasTypedDeleteAsync = generatedCode.Contains(typedDeleteAsyncSignature);

                // Property: presence of typed Delete builder implies presence of typed DeleteAsync
                return (hasTypedBuilderOverload == hasTypedDeleteAsync)
                    .Label($"Eligible entity '{entity.ClassName}': typed Delete builder found={hasTypedBuilderOverload}, " +
                           $"typed DeleteAsync found={hasTypedDeleteAsync}. " +
                           $"UseFluentResults={entity.UseFluentResults}, HideGeneratedAsyncMethods={entity.HideGeneratedAsyncMethods}");
            }
            else
            {
                // Ineligible entity — neither typed Delete builder NOR typed DeleteAsync should exist
                hasTypedBuilderOverload = false;

                // For ineligible entities: verify no typed DeleteAsync exists with non-string first param
                var hasTypedDeleteAsync = generatedCode.Contains($"async System.Threading.Tasks.Task DeleteAsync(int ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task DeleteAsync(long ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task DeleteAsync(DateTime ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task DeleteAsync(Guid ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task DeleteAsync(decimal ")
                    || generatedCode.Contains($"async System.Threading.Tasks.Task DeleteAsync(DateOnly ");

                return (!hasTypedDeleteAsync)
                    .Label($"Ineligible entity '{entity.ClassName}': should have no typed DeleteAsync, " +
                           $"but found one={hasTypedDeleteAsync}");
            }
        });
    }

    #endregion

    #region Generators

    /// <summary>
    /// Creates a generator for entities that qualify for typed overloads:
    /// - At least one key is computed with 2+ source properties
    /// - At least one non-string source property (to avoid ambiguity)
    /// - UseFluentResults = false (standard async methods generated)
    /// </summary>
    private static Arbitrary<EntityModel> CreateEligibleEntityGenerator()
    {
        var classNameGen = Gen.Elements(
            "ScheduledEvent", "CompositeOrder", "MultiKeyUser", "TimedEntry", "RegionalProduct");
        var tableNameGen = Gen.Elements(
            "scheduled-events", "composite-orders", "multi-key-users", "timed-entries", "regional-products");
        var sourceCountGen = Gen.Choose(2, 4);
        var hasSortKeyGen = Gen.Elements(true, false);
        // Scenario: 0 = computed PK only, 1 = computed SK only, 2 = both computed
        var scenarioGen = Gen.Choose(0, 2);

        var gen = from className in classNameGen
                  from tableName in tableNameGen
                  from sourceCount in sourceCountGen
                  from hasSk in hasSortKeyGen
                  from scenario in scenarioGen
                  let entity = BuildEligibleEntity(className, tableName, sourceCount, hasSk, scenario)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                        && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
                        && OverloadParameterResolver.GetTypedOverloadParameters(entity) != null
                  select entity;

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Creates a generator for FluentResults-enabled entities that qualify for typed overloads:
    /// - At least one key is computed with 2+ source properties
    /// - At least one non-string source property (to avoid ambiguity)
    /// - UseFluentResults = true (FluentResults Result variants generated)
    /// </summary>
    private static Arbitrary<EntityModel> CreateFluentResultsEligibleEntityGenerator()
    {
        var classNameGen = Gen.Elements(
            "ScheduledEvent", "CompositeOrder", "MultiKeyUser", "TimedEntry", "RegionalProduct");
        var tableNameGen = Gen.Elements(
            "scheduled-events", "composite-orders", "multi-key-users", "timed-entries", "regional-products");
        var sourceCountGen = Gen.Choose(2, 4);
        var hasSortKeyGen = Gen.Elements(true, false);
        // Scenario: 0 = computed PK only, 1 = computed SK only, 2 = both computed
        var scenarioGen = Gen.Choose(0, 2);

        var gen = from className in classNameGen
                  from tableName in tableNameGen
                  from sourceCount in sourceCountGen
                  from hasSk in hasSortKeyGen
                  from scenario in scenarioGen
                  let entity = BuildFluentResultsEligibleEntity(className, tableName, sourceCount, hasSk, scenario)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                        && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
                        && OverloadParameterResolver.GetTypedOverloadParameters(entity) != null
                  select entity;

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Builds a FluentResults-enabled entity with computed keys containing at least one non-string source property.
    /// Same as BuildEligibleEntity but with UseFluentResults = true.
    /// </summary>
    private static EntityModel BuildFluentResultsEligibleEntity(
        string className, string tableName, int sourceCount, bool hasSortKey, int scenario)
    {
        var entity = BuildEligibleEntity(className, tableName, sourceCount, hasSortKey, scenario);
        entity.UseFluentResults = true;
        return entity;
    }

    /// <summary>
    /// Builds an entity with computed keys containing at least one non-string source property.
    /// This ensures the typed overload is non-ambiguous with the standard string overload.
    /// </summary>
    private static EntityModel BuildEligibleEntity(
        string className, string tableName, int sourceCount, bool hasSortKey, int scenario)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();
        var skSourceProps = new List<string>();

        // Determine which key(s) are computed based on scenario
        bool pkIsComputed = scenario == 0 || scenario == 2;
        bool skIsComputed = (scenario == 1 || scenario == 2) && hasSortKey;

        // If neither key would be computed, force PK to be computed
        if (!pkIsComputed && !skIsComputed)
        {
            pkIsComputed = true;
        }

        if (pkIsComputed)
        {
            // PK source properties — first is always int to avoid ambiguity
            for (int i = 0; i < sourceCount; i++)
            {
                var propName = $"PkField{i + 1}";
                var propType = i == 0 ? "int" : NonStringTypes[i % NonStringTypes.Length];
                properties.Add(new PropertyModel
                {
                    PropertyName = propName,
                    PropertyType = propType,
                    AttributeName = propName.ToLowerInvariant()
                });
                pkSourceProps.Add(propName);
            }
        }

        // PK property
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true
        };
        if (pkIsComputed)
        {
            pkProperty.ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceProps.ToArray(),
                Separator = "#"
            };
        }
        properties.Add(pkProperty);

        if (hasSortKey)
        {
            if (skIsComputed)
            {
                // SK source properties — first is always long to avoid ambiguity
                for (int i = 0; i < sourceCount; i++)
                {
                    var propName = $"SkField{i + 1}";
                    var propType = i == 0 ? "long" : NonStringTypes[(i + 2) % NonStringTypes.Length];
                    properties.Add(new PropertyModel
                    {
                        PropertyName = propName,
                        PropertyType = propType,
                        AttributeName = propName.ToLowerInvariant()
                    });
                    skSourceProps.Add(propName);
                }
            }

            var skProperty = new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true
            };
            if (skIsComputed)
            {
                skProperty.ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = skSourceProps.ToArray(),
                    Separator = "#"
                };
            }
            properties.Add(skProperty);
        }

        // Add a non-key property
        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
        });

        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            UseFluentResults = false,
            HideGeneratedAsyncMethods = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    /// <summary>
    /// Creates a generator for single-entity table configurations where the entity qualifies for typed overloads.
    /// The entity is passed as the sole entity to GenerateTableClass, making it a single-entity table.
    /// UseFluentResults is false so that traditional async methods (GetAsync, DeleteAsync) are generated at the table level.
    /// </summary>
    private static Arbitrary<EntityModel> CreateSingleEntityTableGenerator()
    {
        var classNameGen = Gen.Elements(
            "ScheduledEvent", "CompositeOrder", "MultiKeyUser", "TimedEntry", "RegionalProduct");
        var tableNameGen = Gen.Elements(
            "scheduled-events", "composite-orders", "multi-key-users", "timed-entries", "regional-products");
        var sourceCountGen = Gen.Choose(2, 4);
        var hasSortKeyGen = Gen.Elements(true, false);
        // Scenario: 0 = computed PK only, 1 = computed SK only, 2 = both computed
        var scenarioGen = Gen.Choose(0, 2);

        var gen = from className in classNameGen
                  from tableName in tableNameGen
                  from sourceCount in sourceCountGen
                  from hasSk in hasSortKeyGen
                  from scenario in scenarioGen
                  let entity = BuildEligibleEntity(className, tableName, sourceCount, hasSk, scenario)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                        && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
                        && OverloadParameterResolver.GetTypedOverloadParameters(entity) != null
                  select entity;

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Creates a generator that produces a mix of eligible and ineligible entities for testing
    /// eligibility consistency. This ensures Property 7 exercises both code paths:
    /// - Eligible: computed keys with non-ambiguous typed overloads
    /// - Ineligible: no computed keys, or single source property (ambiguous), or all-string source properties
    ///
    /// All entities have UseFluentResults = false so standard async methods are generated
    /// (the generateTraditionalAsync flag = true because !UseFluentResults = true).
    /// </summary>
    private static Arbitrary<EntityModel> CreateMixedEligibilityEntityGenerator()
    {
        var classNameGen = Gen.Elements(
            "ScheduledEvent", "CompositeOrder", "MultiKeyUser", "TimedEntry", "RegionalProduct",
            "SimpleItem", "BasicEntry", "PlainRecord", "StandardDoc", "FlatEntity");
        var tableNameGen = Gen.Elements(
            "scheduled-events", "composite-orders", "multi-key-users", "timed-entries", "regional-products",
            "simple-items", "basic-entries", "plain-records", "standard-docs", "flat-entities");
        var sourceCountGen = Gen.Choose(2, 4);
        var hasSortKeyGen = Gen.Elements(true, false);
        // Scenario: 0 = computed PK only, 1 = computed SK only, 2 = both computed
        var scenarioGen = Gen.Choose(0, 2);
        // Ineligibility reason: 0 = no computed key, 1 = all-string source properties (ambiguous),
        // 2 = single source property per key (WouldBeAmbiguous since same count as standard)
        var ineligibleReasonGen = Gen.Choose(0, 2);
        // Whether to generate an eligible or ineligible entity (roughly 50/50 mix)
        var isEligibleGen = Gen.Elements(true, false);

        var gen = from className in classNameGen
                  from tableName in tableNameGen
                  from sourceCount in sourceCountGen
                  from hasSk in hasSortKeyGen
                  from scenario in scenarioGen
                  from ineligibleReason in ineligibleReasonGen
                  from isEligible in isEligibleGen
                  let entity = isEligible
                      ? BuildEligibleEntity(className, tableName, sourceCount, hasSk, scenario)
                      : BuildIneligibleEntity(className, tableName, hasSk, ineligibleReason)
                  select entity;

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Builds an entity that does NOT qualify for typed overloads for various reasons:
    /// - reason 0: No computed keys at all (plain string PK/SK)
    /// - reason 1: Computed key with all-string source properties (would be ambiguous)
    /// - reason 2: Computed key with single source property (same count as standard, ambiguous)
    /// </summary>
    private static EntityModel BuildIneligibleEntity(
        string className, string tableName, bool hasSortKey, int reason)
    {
        var properties = new List<PropertyModel>();

        switch (reason)
        {
            case 0:
                // No computed keys — simple string PK (and optional string SK)
                properties.Add(new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                });
                if (hasSortKey)
                {
                    properties.Add(new PropertyModel
                    {
                        PropertyName = "Sk",
                        PropertyType = "string",
                        AttributeName = "sk",
                        IsSortKey = true
                    });
                }
                break;

            case 1:
                // Computed key with all-string source properties — would be ambiguous
                // because typed params are (string, string) same as standard (pK, sK)
                properties.Add(new PropertyModel
                {
                    PropertyName = "Region",
                    PropertyType = "string",
                    AttributeName = "region"
                });
                properties.Add(new PropertyModel
                {
                    PropertyName = "Category",
                    PropertyType = "string",
                    AttributeName = "category"
                });
                properties.Add(new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Region", "Category" },
                        Separator = "#"
                    }
                });
                if (hasSortKey)
                {
                    properties.Add(new PropertyModel
                    {
                        PropertyName = "Sk",
                        PropertyType = "string",
                        AttributeName = "sk",
                        IsSortKey = true
                    });
                }
                break;

            case 2:
                // Computed key with single source property — same parameter count as standard
                // so it would be ambiguous (1 typed param vs 1 standard string param for PK)
                properties.Add(new PropertyModel
                {
                    PropertyName = "Year",
                    PropertyType = "int",
                    AttributeName = "year"
                });
                properties.Add(new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Year" },
                        Separator = "#"
                    }
                });
                if (hasSortKey)
                {
                    properties.Add(new PropertyModel
                    {
                        PropertyName = "Sk",
                        PropertyType = "string",
                        AttributeName = "sk",
                        IsSortKey = true
                    });
                }
                break;
        }

        // Add a non-key data property
        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
        });

        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            UseFluentResults = false,
            HideGeneratedAsyncMethods = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    #endregion
}
