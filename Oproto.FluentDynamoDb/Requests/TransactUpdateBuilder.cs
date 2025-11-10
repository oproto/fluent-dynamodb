using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactUpdateBuilder :
    IWithKey<TransactUpdateBuilder>, IWithConditionExpression<TransactUpdateBuilder>, IWithAttributeNames<TransactUpdateBuilder>, IWithAttributeValues<TransactUpdateBuilder>, IWithUpdateExpression<TransactUpdateBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    private UpdateExpressionSource? _updateExpressionSource;
    private Expressions.ExpressionContext? _expressionContext;
    private Storage.IFieldEncryptor? _fieldEncryptor;

    public TransactUpdateBuilder(string tableName)
    {
        _req.Update = new Update();
        _req.Update.TableName = tableName;
    }

    /// <summary>
    /// Gets the internal attribute value helper for extension method access.
    /// </summary>
    /// <returns>The AttributeValueInternal instance used by this builder.</returns>
    public AttributeValueInternal GetAttributeValueHelper() => _attrV;

    /// <summary>
    /// Gets the internal attribute name helper for extension method access.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    public AttributeNameInternal GetAttributeNameHelper() => _attrN;

    /// <summary>
    /// Sets the condition expression on the builder.
    /// If a condition expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactUpdateBuilder SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.Update.ConditionExpression))
        {
            _req.Update.ConditionExpression = expression;
        }
        else
        {
            _req.Update.ConditionExpression = $"({_req.Update.ConditionExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactUpdateBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Update.Key == null) _req.Update.Key = new();
        keyAction(_req.Update.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public TransactUpdateBuilder Self => this;

    /// <summary>
    /// Sets the expression context for this builder.
    /// Used internally by expression-based Set() methods to track parameter metadata for encryption.
    /// </summary>
    /// <param name="context">The expression context containing parameter metadata.</param>
    /// <returns>The builder instance for method chaining.</returns>
    internal TransactUpdateBuilder SetExpressionContext(Expressions.ExpressionContext context)
    {
        _expressionContext = context;
        return this;
    }

    /// <summary>
    /// Sets the field encryptor for this builder.
    /// Used internally to enable encryption of parameters marked as requiring encryption.
    /// </summary>
    /// <param name="fieldEncryptor">The field encryptor to use for encrypting sensitive parameters.</param>
    /// <returns>The builder instance for method chaining.</returns>
    internal TransactUpdateBuilder SetFieldEncryptor(Storage.IFieldEncryptor? fieldEncryptor)
    {
        _fieldEncryptor = fieldEncryptor;
        return this;
    }






    /// <summary>
    /// Sets the update expression on the builder.
    /// </summary>
    /// <param name="expression">The processed update expression to set.</param>
    /// <param name="source">The source of the update expression (string-based or expression-based).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when attempting to mix string-based and expression-based Set() methods.</exception>
    public TransactUpdateBuilder SetUpdateExpression(string expression, UpdateExpressionSource source = UpdateExpressionSource.StringBased)
    {
        // Check if we're mixing different approaches
        if (_updateExpressionSource.HasValue && _updateExpressionSource.Value != source)
        {
            var currentApproach = _updateExpressionSource.Value == UpdateExpressionSource.StringBased 
                ? "string-based Set()" 
                : "expression-based Set()";
            var attemptedApproach = source == UpdateExpressionSource.StringBased 
                ? "string-based Set()" 
                : "expression-based Set()";

            throw new InvalidOperationException(
                $"Cannot mix {currentApproach} and {attemptedApproach} methods in the same TransactUpdateBuilder. " +
                $"The builder already has an update expression set using {currentApproach}. " +
                $"Please use only one approach consistently throughout the builder chain. " +
                $"If you need to combine multiple update operations, use multiple property assignments " +
                $"within a single expression-based Set() call, or combine all operations in a single string-based Set() call.");
        }

        _req.Update.UpdateExpression = expression;
        _updateExpressionSource = source;
        return this;
    }






    public TransactUpdateBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Update.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    /// <summary>
    /// Encrypts parameters that are marked as requiring encryption in the expression context.
    /// This method must be called before ToWriteItem() if encryption is needed.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous encryption operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when encryption is required but no IFieldEncryptor is configured.</exception>
    /// <exception cref="Storage.FieldEncryptionException">Thrown when encryption fails.</exception>
    internal async Task EncryptParametersAsync(CancellationToken cancellationToken = default)
    {
        if (_expressionContext == null || _expressionContext.ParameterMetadata.Count == 0)
            return;

        var parametersRequiringEncryption = _expressionContext.ParameterMetadata
            .Where(p => p.RequiresEncryption)
            .ToList();

        if (parametersRequiringEncryption.Count == 0)
            return;

        if (_fieldEncryptor == null)
        {
            var propertyNames = string.Join(", ", parametersRequiringEncryption
                .Select(p => p.PropertyName ?? p.AttributeName ?? "unknown")
                .Distinct());
            
            var attributeNames = string.Join(", ", parametersRequiringEncryption
                .Select(p => p.AttributeName ?? "unknown")
                .Distinct());

            throw new InvalidOperationException(
                $"Field encryption is required for properties [{propertyNames}] (DynamoDB attributes: [{attributeNames}]) but no IFieldEncryptor is configured. " +
                $"To fix this issue: " +
                $"1. Implement the IFieldEncryptor interface (e.g., using AWS KMS or another encryption provider). " +
                $"2. Pass the encryptor to the DynamoDbTableBase constructor, or " +
                $"3. Set it in the DynamoDbOperationContext before executing transaction operations. " +
                $"Example: new MyTable(dynamoDbClient, logger, blobProvider, fieldEncryptor)");
        }

        foreach (var param in parametersRequiringEncryption)
        {
            // Get the current value from the attribute values
            if (!_attrV.AttributeValues.TryGetValue(param.ParameterName, out var attributeValue))
                continue;

            // Skip null or empty values - they don't need encryption
            if (attributeValue.NULL == true || string.IsNullOrEmpty(attributeValue.S))
                continue;

            try
            {
                // Extract plaintext (assuming string value for now)
                var plaintext = System.Text.Encoding.UTF8.GetBytes(attributeValue.S);

                // Create encryption context
                var encryptionContext = new Storage.FieldEncryptionContext
                {
                    ContextId = Storage.DynamoDbOperationContext.EncryptionContextId
                };

                // Encrypt
                var ciphertext = await _fieldEncryptor.EncryptAsync(
                    plaintext,
                    param.PropertyName ?? param.AttributeName ?? "unknown",
                    encryptionContext,
                    cancellationToken);

                // Replace with encrypted value (as binary)
                _attrV.AttributeValues[param.ParameterName] = new AttributeValue
                {
                    B = new System.IO.MemoryStream(ciphertext)
                };

                // Note: Logging not available in TransactUpdateBuilder as it doesn't have access to a logger instance
                // Encryption logging is available in UpdateItemRequestBuilder and TransactWriteItemsRequestBuilder
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                var propertyInfo = param.PropertyName != null && param.AttributeName != null
                    ? $"property '{param.PropertyName}' (DynamoDB attribute: '{param.AttributeName}')"
                    : $"property '{param.PropertyName ?? param.AttributeName ?? "unknown"}'";
                
                throw new Storage.FieldEncryptionException(
                    $"Failed to encrypt {propertyInfo} (parameter: {param.ParameterName}). " +
                    $"Error: {ex.Message}. " +
                    $"Troubleshooting steps: " +
                    $"1. Verify the IFieldEncryptor is properly configured with valid encryption keys. " +
                    $"2. Check that the encryption provider (e.g., AWS KMS) is accessible and has the necessary permissions. " +
                    $"3. Ensure the value being encrypted is in the correct format for your encryption provider. " +
                    $"4. Review the inner exception for more details about the encryption failure.",
                    ex);
            }
        }
    }

    public TransactWriteItem ToWriteItem()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.Update.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        if (_attrV.AttributeValues.Count > 0)
        {
            _req.Update.ExpressionAttributeValues = _attrV.AttributeValues;
        }
        return _req;
    }
}