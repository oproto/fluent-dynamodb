namespace Oproto.FluentDynamoDb.Providers.Encryption;

/// <summary>
/// Classifies encryption/decryption failures into recoverable and non-recoverable categories.
/// Used by generated FromDynamoDbAsync code to determine whether to skip or throw.
/// </summary>
public static class EncryptionFailureClassifier
{
    /// <summary>
    /// Determines if the exception represents a data integrity failure that should
    /// always throw regardless of DecryptionFailureMode.
    /// </summary>
    public static bool IsIntegrityFailure(Exception ex)
    {
        var message = GetCombinedMessage(ex);
        return message.Contains("invalid ciphertext") ||
               message.Contains("cannot decrypt") ||
               message.Contains("context validation failed");
    }

    /// <summary>
    /// Determines if the exception represents a recoverable failure that can be
    /// skipped when DecryptionFailureMode.SkipFields is configured.
    /// </summary>
    public static bool IsRecoverable(Exception ex)
    {
        return !IsIntegrityFailure(ex);
    }

    private static string GetCombinedMessage(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        if (ex.InnerException != null)
            message += " " + (ex.InnerException.Message ?? string.Empty);
        return message.ToLowerInvariant();
    }
}
